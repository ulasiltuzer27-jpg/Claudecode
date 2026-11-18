//! Proje ajanı: kod tabanınızda çok adımlı değişiklik yapar. Adımları **program** yönetir; model
//! yalnızca plan ve `DOSYA / ARA / DEĞİŞTİR` blokları yazar.
//!
//! 1. İlgili kod bulunur (BM25 indeksi).
//! 2. Plan yazılır → kullanıcı onaylar / düzenler / reddeder.
//! 3. Model düzenleme bloklarını yazar; bloklar bellekte uygulanır (tam ya da boşluk-toleranslı
//!    eşleşme). Uygulanamayan bloklar modele geri bildirilir.
//! 4. Dosya dosya diff önizlemesi → kullanıcı onayı → yedek alınıp yazılır.
//! 5. Kullanıcının verdiği kontrol komutu (`--check "cargo test"`) çalıştırılır; hata varsa çıktı
//!    modele verilip düzeltme istenir.
//!
//! Güvenlik: yalnızca `root` altındaki dosyalar düzenlenir; model komut çalıştıramaz; her uygulama
//! öncesi yedek alınır (`.sosis/yedek/<zaman>/`), `undo_last` ile geri alınır.

use std::collections::BTreeMap;
use std::fs;
use std::path::{Component, Path, PathBuf};
use std::process::{Command, Stdio};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

use anyhow::{bail, Context, Result};
use serde::{Deserialize, Serialize};
use sosis_data::edits::{apply_block, edit_request, parse_blocks};
use sosis_tokenizer::chat::{plan_approved, plan_request};

use crate::rag::CodeIndex;

pub const BACKUP_DIR: &str = ".sosis/yedek";

/// Önerilen bir dosya değişikliği.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileChange {
    /// `root`'a göre yol.
    pub path: String,
    /// Dosya yoksa `None` (yeni dosya).
    pub old: Option<String>,
    pub new: String,
}

impl FileChange {
    pub fn diff(&self) -> String {
        let old = self.old.as_deref().unwrap_or("");
        similar::TextDiff::from_lines(old, &self.new)
            .unified_diff()
            .context_radius(3)
            .header(&format!("a/{}", self.path), &format!("b/{}", self.path))
            .to_string()
    }
}

#[derive(Debug, Clone, Default)]
pub struct Proposal {
    pub changes: Vec<FileChange>,
    /// Uygulanamayan bloklar (modele geri bildirilecek).
    pub errors: Vec<String>,
    pub raw: String,
}

/// `root` altında güvenli yol: mutlak yol, `..` ve kök dışına çıkan bağlantılar reddedilir.
pub fn safe_path(root: &Path, rel: &str) -> Result<PathBuf> {
    let rel = rel.trim().trim_start_matches("./");
    let p = Path::new(rel);
    if rel.is_empty()
        || p.is_absolute()
        || p.components().any(|c| !matches!(c, Component::Normal(_)))
    {
        bail!("güvenli olmayan yol: {rel}");
    }
    let full = root.join(p);
    let root_c = fs::canonicalize(root)?;
    // Var olan en yakın üst klasörün gerçek yolu kök içinde olmalı (sembolik bağlantı kaçışı).
    let mut probe = full.clone();
    while !probe.exists() {
        if !probe.pop() {
            break;
        }
    }
    if probe.exists() && !fs::canonicalize(&probe)?.starts_with(&root_c) {
        bail!("kök dışına çıkan yol: {rel}");
    }
    if rel.starts_with(".sosis/") || rel.starts_with(".git/") {
        bail!("korunan klasör: {rel}");
    }
    Ok(full)
}

/// Görevle ilgili kod parçaları (yol, metin).
pub fn find_snippets(index: &CodeIndex, query: &str, k: usize) -> Vec<(String, String)> {
    index
        .search(query, k)
        .into_iter()
        .map(|(_, c)| (c.path.clone(), c.text.clone()))
        .collect()
}

/// Model cevabındaki blokları bellekte uygular.
pub fn apply_answer(root: &Path, answer: &str) -> Proposal {
    let blocks = parse_blocks(answer);
    let mut files: BTreeMap<String, (Option<String>, String)> = BTreeMap::new();
    let mut errors = Vec::new();
    if blocks.is_empty() {
        errors.push(
            "Cevapta hiç DOSYA / ARA / DEĞİŞTİR bloğu yok. Değişiklikleri bu biçimle yaz.".into(),
        );
    }
    for b in blocks {
        let full = match safe_path(root, &b.path) {
            Ok(p) => p,
            Err(e) => {
                errors.push(e.to_string());
                continue;
            }
        };
        let entry = files.entry(b.path.clone()).or_insert_with(|| {
            let old = fs::read_to_string(&full).ok();
            let cur = old.clone().unwrap_or_default();
            (old, cur)
        });
        match apply_block(&entry.1, &b) {
            Ok(n) => entry.1 = n,
            Err(e) => {
                let preview: String = b.search.lines().take(3).collect::<Vec<_>>().join(" ⏎ ");
                errors.push(format!("{}: {e} (ARA: \"{preview}\")", b.path));
            }
        }
    }
    let changes = files
        .into_iter()
        .filter(|(_, (old, new))| old.as_deref() != Some(new.as_str()))
        .map(|(path, (old, new))| FileChange { path, old, new })
        .collect();
    Proposal {
        changes,
        errors,
        raw: answer.to_string(),
    }
}

/// Değişiklikler için istem (görev + onaylı plan + ilgili kod + varsa geri bildirim).
pub fn edit_prompt(
    task: &str,
    plan: Option<&str>,
    snippets: &[(String, String)],
    feedback: Option<&str>,
) -> String {
    let mut t = task.to_string();
    if let Some(p) = plan {
        t.push_str(&format!(
            "\n\n{}",
            plan_approved(p).replace("Şimdi bu plana göre kodu yaz.", "")
        ));
    }
    let mut s = edit_request(&t, snippets);
    if let Some(f) = feedback {
        s.push_str(&format!("\n\nÖnceki denemede sorun çıktı:\n{f}"));
    }
    s
}

#[derive(Debug, Serialize, Deserialize)]
struct Manifest {
    /// (yol, önceden var mıydı)
    files: Vec<(String, bool)>,
}

/// Değişiklikleri yedek alarak yazar; yedek kimliğini döndürür.
pub fn apply_changes(root: &Path, changes: &[FileChange]) -> Result<String> {
    let id = format!(
        "{}",
        SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_millis())
            .unwrap_or(0)
    );
    let bdir = root.join(BACKUP_DIR).join(&id);
    fs::create_dir_all(&bdir)?;
    let mut manifest = Manifest { files: vec![] };
    for c in changes {
        safe_path(root, &c.path)?;
        if let Some(old) = &c.old {
            let b = bdir.join(&c.path);
            fs::create_dir_all(b.parent().unwrap())?;
            fs::write(&b, old)?;
        }
        manifest.files.push((c.path.clone(), c.old.is_some()));
    }
    fs::write(
        bdir.join("manifest.json"),
        serde_json::to_string_pretty(&manifest)?,
    )?;
    for c in changes {
        let full = safe_path(root, &c.path)?;
        if let Some(d) = full.parent() {
            fs::create_dir_all(d)?;
        }
        fs::write(&full, &c.new)?;
    }
    Ok(id)
}

/// Son uygulamayı geri alır; geri alınan dosyaları döndürür.
pub fn undo_last(root: &Path) -> Result<Vec<String>> {
    let base = root.join(BACKUP_DIR);
    let mut ids: Vec<String> = fs::read_dir(&base)
        .with_context(|| "geri alınacak değişiklik yok")?
        .filter_map(|e| e.ok())
        .filter(|e| e.path().join("manifest.json").exists())
        .map(|e| e.file_name().to_string_lossy().into_owned())
        .collect();
    ids.sort_by_key(|s| s.parse::<u128>().unwrap_or(0));
    let id = ids.pop().context("geri alınacak değişiklik yok")?;
    let bdir = base.join(&id);
    let m: Manifest = serde_json::from_str(&fs::read_to_string(bdir.join("manifest.json"))?)?;
    for (path, existed) in &m.files {
        let full = safe_path(root, path)?;
        if *existed {
            fs::write(&full, fs::read_to_string(bdir.join(path))?)?;
        } else {
            let _ = fs::remove_file(&full);
        }
    }
    fs::remove_dir_all(&bdir)?;
    Ok(m.files.into_iter().map(|(p, _)| p).collect())
}

/// Kullanıcının verdiği kontrol komutu (kabukta, `root` içinde, süre sınırlı).
pub fn run_check(root: &Path, cmd: &str, timeout: Duration) -> Result<(bool, String)> {
    let mut c = if cfg!(windows) {
        let mut c = Command::new("cmd");
        c.args(["/C", cmd]);
        c
    } else {
        let mut c = Command::new("sh");
        c.args(["-c", cmd]);
        c
    };
    let log = std::env::temp_dir().join(format!("sosis_check_{}.log", std::process::id()));
    let f = fs::File::create(&log)?;
    let mut child = c
        .current_dir(root)
        .stdin(Stdio::null())
        .stdout(f.try_clone()?)
        .stderr(f)
        .spawn()
        .with_context(|| format!("kontrol komutu çalıştırılamadı: {cmd}"))?;
    let start = Instant::now();
    let ok = loop {
        if let Some(st) = child.try_wait()? {
            break st.success();
        }
        if start.elapsed() > timeout {
            child.kill().ok();
            child.wait().ok();
            let _ = fs::remove_file(&log);
            return Ok((false, format!("zaman aşımı ({} sn)", timeout.as_secs())));
        }
        std::thread::sleep(Duration::from_millis(50));
    };
    let out = fs::read_to_string(&log).unwrap_or_default();
    let _ = fs::remove_file(&log);
    // Son 40 satır (hatalar genelde sondadır).
    let lines: Vec<&str> = out.lines().collect();
    Ok((ok, lines[lines.len().saturating_sub(40)..].join("\n")))
}

/// Kullanıcı etkileşimi (terminal veya test).
pub trait AgentUi {
    fn text(&mut self, s: &str);
    fn info(&mut self, s: &str);
    /// Onaylanan (gerekirse düzenlenen) plan; reddedilirse `None`.
    fn approve_plan(&mut self, plan: &str) -> Option<String>;
    /// Bu dosya değişikliği uygulansın mı?
    fn approve_change(&mut self, change: &FileChange) -> bool;
}

pub struct AgentOptions {
    pub root: PathBuf,
    pub check: Option<String>,
    pub max_rounds: usize,
    pub plan: bool,
    pub snippets: usize,
}

#[derive(Debug, Default)]
pub struct AgentReport {
    pub applied: Vec<String>,
    pub rounds: usize,
    pub check_passed: Option<bool>,
    pub backups: Vec<String>,
}

/// Ajan döngüsü. `respond(kullanıcı_mesajı)` modelin cevabını döndürür (konuşma sürer).
pub fn run_agent(
    task: &str,
    opts: &AgentOptions,
    index: &CodeIndex,
    ui: &mut dyn AgentUi,
    mut respond: impl FnMut(&str) -> String,
) -> Result<AgentReport> {
    let mut report = AgentReport::default();
    let snippets = find_snippets(index, task, opts.snippets);
    ui.info(&format!(
        "İlgili kod: {}",
        snippets
            .iter()
            .map(|s| s.0.as_str())
            .collect::<Vec<_>>()
            .join(", ")
    ));

    let plan = if opts.plan {
        let p = respond(&plan_request(task));
        match ui.approve_plan(&p) {
            Some(p) => Some(p),
            None => {
                ui.info("Plan reddedildi; değişiklik yapılmadı.");
                return Ok(report);
            }
        }
    } else {
        None
    };

    let mut feedback: Option<String> = None;
    let mut context = snippets;
    for round in 1..=opts.max_rounds.max(1) {
        report.rounds = round;
        ui.info(&format!("— tur {round}: değişiklikler yazılıyor"));
        let answer = respond(&edit_prompt(
            task,
            plan.as_deref(),
            &context,
            feedback.as_deref(),
        ));
        let prop = apply_answer(&opts.root, &answer);
        for e in &prop.errors {
            ui.info(&format!("  ✗ {e}"));
        }
        let approved: Vec<FileChange> = prop
            .changes
            .iter()
            .filter(|c| ui.approve_change(c))
            .cloned()
            .collect();
        if approved.is_empty() {
            if prop.errors.is_empty() {
                ui.info("Uygulanacak değişiklik kalmadı.");
                break;
            }
            feedback = Some(prop.errors.join("\n"));
            continue;
        }
        report.backups.push(apply_changes(&opts.root, &approved)?);
        for c in &approved {
            ui.info(&format!("  ✓ yazıldı: {}", c.path));
            if !report.applied.contains(&c.path) {
                report.applied.push(c.path.clone());
            }
        }
        // Sonraki turlar için güncel dosya içerikleri bağlam olur.
        context = approved
            .iter()
            .map(|c| {
                (
                    c.path.clone(),
                    c.new.lines().take(120).collect::<Vec<_>>().join("\n"),
                )
            })
            .collect();
        let Some(cmd) = &opts.check else {
            if prop.errors.is_empty() {
                break;
            }
            feedback = Some(prop.errors.join("\n"));
            continue;
        };
        ui.info(&format!("  ▶ kontrol: {cmd}"));
        let (ok, out) = run_check(&opts.root, cmd, Duration::from_secs(600))?;
        report.check_passed = Some(ok);
        if ok && prop.errors.is_empty() {
            ui.info("  ✓ kontrol geçti");
            break;
        }
        ui.info(if ok {
            "  ⚠ kontrol geçti ama bazı bloklar uygulanamadı"
        } else {
            "  ✗ kontrol başarısız"
        });
        let mut fb = prop.errors.join("\n");
        if !ok {
            fb.push_str(&format!(
                "\nKontrol komutu `{cmd}` başarısız oldu:\n```\n{out}\n```"
            ));
        }
        feedback = Some(fb);
    }
    Ok(report)
}

#[cfg(test)]
mod tests {
    use super::*;

    struct AutoUi {
        log: Vec<String>,
        accept: bool,
    }
    impl AgentUi for AutoUi {
        fn text(&mut self, s: &str) {
            self.log.push(s.into());
        }
        fn info(&mut self, s: &str) {
            self.log.push(s.into());
        }
        fn approve_plan(&mut self, plan: &str) -> Option<String> {
            self.accept
                .then(|| format!("{plan}\n3. (kullanıcı ekledi) test et"))
        }
        fn approve_change(&mut self, _c: &FileChange) -> bool {
            self.accept
        }
    }

    fn project(name: &str) -> PathBuf {
        let root = std::env::temp_dir().join(format!("sosis_agent_{name}_{}", std::process::id()));
        let _ = fs::remove_dir_all(&root);
        fs::create_dir_all(root.join("src")).unwrap();
        fs::write(
            root.join("src/oyun.py"),
            "def hasar(can):\n    return can - 10\n",
        )
        .unwrap();
        fs::write(
            root.join("test.py"),
            "from src.oyun import hasar\nassert hasar(5) == 0\nassert hasar(50) == 40\n",
        )
        .unwrap();
        fs::write(root.join("src/__init__.py"), "").unwrap();
        root
    }

    #[test]
    fn rejects_unsafe_paths() {
        let root = project("paths");
        for bad in [
            "../dışarı.py",
            "/etc/passwd",
            ".git/config",
            ".sosis/yedek/x",
            "a/../../b",
        ] {
            assert!(safe_path(&root, bad).is_err(), "{bad}");
        }
        assert!(safe_path(&root, "src/yeni.py").is_ok());
        let p = apply_answer(
            &root,
            "DOSYA: ../kaçış.py\n<<<<<<< ARA\n=======\nx = 1\n>>>>>>> DEĞİŞTİR\n",
        );
        assert!(p.changes.is_empty() && !p.errors.is_empty());
        fs::remove_dir_all(&root).ok();
    }

    #[test]
    fn plan_edit_check_fix_loop_and_undo() {
        let root = project("loop");
        let index = CodeIndex::build(std::slice::from_ref(&root)).unwrap();
        let python = if Command::new("python3").arg("--version").output().is_ok() {
            "python3"
        } else {
            "python"
        };
        let opts = AgentOptions {
            root: root.clone(),
            check: Some(format!("{python} test.py")),
            max_rounds: 3,
            plan: true,
            snippets: 3,
        };
        let mut calls = 0;
        let mut prompts = Vec::new();
        let mut ui = AutoUi {
            log: vec![],
            accept: true,
        };
        let report = run_agent("Can negatif olmasın", &opts, &index, &mut ui, |msg| {
            calls += 1;
            prompts.push(msg.to_string());
            match calls {
                1 => "Plan:\n1. hasar'ı sınırla\n2. negatifleri sıfırla".into(),
                // 1. tur: yanlış düzeltme (kontrol başarısız olur).
                2 => "DOSYA: src/oyun.py\n<<<<<<< ARA\n    return can - 10\n=======\n    return abs(can - 10)\n>>>>>>> DEĞİŞTİR\n".into(),
                // 2. tur: doğru düzeltme (girinti hatalı yazılmış ARA da bulunur).
                _ => "DOSYA: src/oyun.py\n<<<<<<< ARA\nreturn abs(can - 10)\n=======\n    return max(can - 10, 0)\n>>>>>>> DEĞİŞTİR\n".into(),
            }
        })
        .unwrap();
        assert_eq!(report.rounds, 2, "{:?}", ui.log);
        assert_eq!(report.check_passed, Some(true));
        assert_eq!(report.applied, vec!["src/oyun.py".to_string()]);
        assert!(
            prompts[1].contains("kullanıcı ekledi"),
            "onaylı plan istemde olmalı"
        );
        assert!(
            prompts[2].contains("başarısız") && prompts[2].contains("AssertionError"),
            "{}",
            prompts[2]
        );
        assert_eq!(
            fs::read_to_string(root.join("src/oyun.py")).unwrap(),
            "def hasar(can):\n    return max(can - 10, 0)\n"
        );

        // Geri al: önce 2. tur, sonra 1. tur.
        undo_last(&root).unwrap();
        assert!(fs::read_to_string(root.join("src/oyun.py"))
            .unwrap()
            .contains("abs("));
        undo_last(&root).unwrap();
        assert_eq!(
            fs::read_to_string(root.join("src/oyun.py")).unwrap(),
            "def hasar(can):\n    return can - 10\n"
        );
        assert!(undo_last(&root).is_err());
        fs::remove_dir_all(&root).ok();
    }

    #[test]
    fn declined_changes_are_not_written_and_new_files_undo() {
        let root = project("decline");
        let index = CodeIndex::build(std::slice::from_ref(&root)).unwrap();
        let opts = AgentOptions {
            root: root.clone(),
            check: None,
            max_rounds: 1,
            plan: false,
            snippets: 2,
        };
        let mut ui = AutoUi {
            log: vec![],
            accept: false,
        };
        run_agent("x", &opts, &index, &mut ui, |_| {
            "DOSYA: src/oyun.py\n<<<<<<< ARA\n    return can - 10\n=======\n    return 0\n>>>>>>> DEĞİŞTİR\n".into()
        })
        .unwrap();
        assert!(fs::read_to_string(root.join("src/oyun.py"))
            .unwrap()
            .contains("can - 10"));
        // Yeni dosya oluşturma ve geri alma.
        let prop = apply_answer(
            &root,
            "DOSYA: src/yeni.py\n<<<<<<< ARA\n=======\nX = 1\n>>>>>>> DEĞİŞTİR\n",
        );
        assert!(prop.changes[0].old.is_none());
        assert!(prop.changes[0].diff().contains("+X = 1"));
        apply_changes(&root, &prop.changes).unwrap();
        assert!(root.join("src/yeni.py").exists());
        undo_last(&root).unwrap();
        assert!(!root.join("src/yeni.py").exists());
        fs::remove_dir_all(&root).ok();
    }
}
