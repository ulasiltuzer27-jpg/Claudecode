//! Git geçmişinden API'siz eğitim verisi.
//!
//! Küçük ve metin tabanlı commit'lerden (≤3 dosya, ≤80 değişen satır) üç tür örnek çıkar:
//! 1. **commit mesajı**: diff → mesaj
//! 2. **açıklama**: diff → "bu değişiklik ne yapıyor" (mesaj + gövde)
//! 3. **düzenleme**: görev (commit başlığı) + eski kod → ARA/DEĞİŞTİR blokları (proje ajanının biçimi)

use std::path::Path;
use std::process::Command;

use anyhow::{bail, Context, Result};

use crate::collect::DEFAULT_EXTENSIONS;
use crate::edits::{edit_request, format_blocks, EditBlock};
use crate::sft::SftExample;

const MAX_FILES: usize = 3;
const MAX_CHANGED: usize = 80;
const MAX_DIFF_CHARS: usize = 4000;

#[derive(Debug, Clone, Default)]
pub struct Hunk {
    /// Bağlam + silinen satırlar (eski hal).
    pub old: Vec<String>,
    /// Bağlam + eklenen satırlar (yeni hal).
    pub new: Vec<String>,
}

#[derive(Debug, Clone, Default)]
pub struct FileDiff {
    pub path: String,
    pub hunks: Vec<Hunk>,
    pub binary: bool,
    pub new_file: bool,
    pub deleted: bool,
    pub added: usize,
    pub removed: usize,
}

#[derive(Debug, Clone)]
pub struct Commit {
    pub sha: String,
    pub subject: String,
    pub body: String,
    /// `index` satırları çıkarılmış ham diff.
    pub diff: String,
    pub files: Vec<FileDiff>,
}

/// `git diff` / `git log -p` çıktısındaki dosya farklarını ayrıştırır.
pub fn parse_diff(text: &str) -> Vec<FileDiff> {
    let mut files: Vec<FileDiff> = Vec::new();
    let mut in_hunk = false;
    for line in text.lines() {
        if let Some(rest) = line.strip_prefix("diff --git ") {
            let path = rest
                .rsplit_once(" b/")
                .map(|(_, p)| p)
                .unwrap_or(rest)
                .to_string();
            files.push(FileDiff {
                path,
                ..Default::default()
            });
            in_hunk = false;
            continue;
        }
        let Some(f) = files.last_mut() else { continue };
        if line.starts_with("Binary files") || line.starts_with("GIT binary patch") {
            f.binary = true;
        } else if line.starts_with("new file mode") {
            f.new_file = true;
        } else if line.starts_with("deleted file mode") {
            f.deleted = true;
        } else if line.starts_with("@@") {
            f.hunks.push(Hunk::default());
            in_hunk = true;
        } else if in_hunk {
            let h = f.hunks.last_mut().unwrap();
            match line.as_bytes().first() {
                Some(b' ') => {
                    h.old.push(line[1..].to_string());
                    h.new.push(line[1..].to_string());
                }
                Some(b'-') => {
                    h.old.push(line[1..].to_string());
                    f.removed += 1;
                }
                Some(b'+') => {
                    h.new.push(line[1..].to_string());
                    f.added += 1;
                }
                Some(b'\\') => {}
                None => {
                    h.old.push(String::new());
                    h.new.push(String::new());
                }
                _ => in_hunk = false,
            }
        }
    }
    files
}

fn clean_diff(diff: &str) -> String {
    let d: String = diff
        .lines()
        .filter(|l| !l.starts_with("index "))
        .collect::<Vec<_>>()
        .join("\n");
    if d.len() > MAX_DIFF_CHARS {
        let cut = d
            .char_indices()
            .take_while(|(i, _)| *i < MAX_DIFF_CHARS)
            .last()
            .map(|(i, _)| i)
            .unwrap_or(0);
        format!("{}\n… (kırpıldı)", &d[..cut])
    } else {
        d
    }
}

/// Depodaki commit'leri okur (birleştirme commit'leri hariç).
pub fn read_commits(repo: &Path, max: usize) -> Result<Vec<Commit>> {
    let out = Command::new("git")
        .arg("-C")
        .arg(repo)
        .args([
            "log",
            "--no-merges",
            "--no-color",
            "--no-ext-diff",
            "-p",
            "--unified=3",
        ])
        .arg(format!("-n{max}"))
        .arg("--format=%x1e%H%n%s%n%b%x1f")
        .output()
        .context("git çalıştırılamadı (kurulu mu?)")?;
    if !out.status.success() {
        bail!(
            "git log başarısız: {}",
            String::from_utf8_lossy(&out.stderr).trim()
        );
    }
    let text = String::from_utf8_lossy(&out.stdout);
    let mut commits = Vec::new();
    for rec in text.split('\u{1e}').filter(|r| !r.trim().is_empty()) {
        let Some((head, diff)) = rec.split_once('\u{1f}') else {
            continue;
        };
        let mut lines = head.lines();
        let sha = lines.next().unwrap_or("").to_string();
        let subject = lines.next().unwrap_or("").trim().to_string();
        let body = lines.collect::<Vec<_>>().join("\n").trim().to_string();
        commits.push(Commit {
            sha,
            subject,
            body,
            diff: clean_diff(diff.trim()),
            files: parse_diff(diff),
        });
    }
    Ok(commits)
}

fn is_code(path: &str) -> bool {
    let ext = Path::new(path)
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("")
        .to_lowercase();
    DEFAULT_EXTENSIONS.contains(&ext.as_str())
}

/// Eğitime uygun mu: küçük, metin, kod dosyaları, anlamlı mesaj.
pub fn usable(c: &Commit) -> bool {
    let changed: usize = c.files.iter().map(|f| f.added + f.removed).sum();
    let s = c.subject.to_lowercase();
    !c.files.is_empty()
        && c.files.len() <= MAX_FILES
        && changed > 0
        && changed <= MAX_CHANGED
        && c.files
            .iter()
            .all(|f| !f.binary && !f.deleted && is_code(&f.path))
        && c.subject.chars().count() >= 8
        && !["wip", "merge", "fixup!", "squash!", "revert \""]
            .iter()
            .any(|p| s.starts_with(p))
}

/// Ortak istemler (komutlar da bunları kullanır).
pub fn commit_msg_request(diff: &str) -> String {
    format!(
        "Şu değişiklik için kısa ve açıklayıcı bir commit mesajı yaz:\n```diff\n{}\n```",
        diff.trim()
    )
}

pub fn explain_request(diff: &str) -> String {
    format!(
        "Bu değişiklik ne yapıyor? Kısaca açıkla:\n```diff\n{}\n```",
        diff.trim()
    )
}

fn message(c: &Commit) -> String {
    if !c.body.is_empty() && c.body.len() <= 400 {
        format!("{}\n\n{}", c.subject, c.body)
    } else {
        c.subject.clone()
    }
}

/// Bir commit'ten düzenleme örneğinin blokları ve ilgili eski kod parçaları.
pub fn edit_blocks(c: &Commit) -> (Vec<EditBlock>, Vec<(String, String)>) {
    let mut blocks = Vec::new();
    let mut snippets = Vec::new();
    for f in &c.files {
        if f.new_file {
            let content: Vec<String> = f.hunks.iter().flat_map(|h| h.new.clone()).collect();
            blocks.push(EditBlock {
                path: f.path.clone(),
                search: String::new(),
                replace: content.join("\n"),
            });
            continue;
        }
        for h in &f.hunks {
            if h.old.iter().all(|l| l.trim().is_empty()) {
                continue; // bağlamsız ekleme: güvenilir çapa yok
            }
            snippets.push((f.path.clone(), h.old.join("\n")));
            blocks.push(EditBlock {
                path: f.path.clone(),
                search: h.old.join("\n"),
                replace: h.new.join("\n"),
            });
        }
    }
    (blocks, snippets)
}

/// Commit'lerden SFT örnekleri.
pub fn examples_from_commits(commits: &[Commit]) -> Vec<SftExample> {
    let mut out = Vec::new();
    for c in commits.iter().filter(|c| usable(c)) {
        out.push(SftExample::qa(commit_msg_request(&c.diff), message(c)));
        out.push(SftExample::qa(explain_request(&c.diff), message(c)));
        let (blocks, snippets) = edit_blocks(c);
        if !blocks.is_empty() {
            out.push(SftExample::qa(
                edit_request(&c.subject, &snippets),
                format_blocks(&blocks),
            ));
        }
    }
    out
}

/// Bir depodan örnekler.
pub fn mine(repo: &Path, max_commits: usize) -> Result<Vec<SftExample>> {
    Ok(examples_from_commits(&read_commits(repo, max_commits)?))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::edits::{apply_block, parse_blocks};

    fn git(dir: &Path, args: &[&str]) {
        let ok = Command::new("git")
            .arg("-C")
            .arg(dir)
            .args([
                "-c",
                "user.name=Test",
                "-c",
                "user.email=test@example.com",
                "-c",
                "commit.gpgsign=false",
            ])
            .args(args)
            .output()
            .unwrap()
            .status
            .success();
        assert!(ok, "git {args:?}");
    }

    #[test]
    fn mines_examples_and_edits_reproduce_commit() {
        let dir = std::env::temp_dir().join(format!("sosis_git_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();
        git(&dir, &["init", "-q"]);
        let v1 = "fn hasar(can: i32) -> i32 {\n    can - 10\n}\n\nfn main() {\n    println!(\"{}\", hasar(100));\n}\n";
        std::fs::write(dir.join("oyun.rs"), v1).unwrap();
        git(&dir, &["add", "."]);
        git(&dir, &["commit", "-q", "-m", "İlk sürüm: hasar fonksiyonu"]);
        let v2 = v1.replace("can - 10", "(can - 10).max(0)");
        std::fs::write(dir.join("oyun.rs"), &v2).unwrap();
        std::fs::write(dir.join("resim.png"), [0u8, 159, 146, 150]).unwrap();
        git(&dir, &["add", "oyun.rs"]);
        git(
            &dir,
            &[
                "commit",
                "-q",
                "-m",
                "Can sıfırın altına düşmesin",
                "-m",
                "Negatif can hatası giderildi.",
            ],
        );
        git(&dir, &["add", "resim.png"]);
        git(
            &dir,
            &["commit", "-q", "-m", "Görsel eklendi (ikili dosya)"],
        );

        let commits = read_commits(&dir, 10).unwrap();
        assert_eq!(commits.len(), 3);
        let fix = commits
            .iter()
            .find(|c| c.subject.starts_with("Can sıfırın"))
            .unwrap();
        assert_eq!(fix.body, "Negatif can hatası giderildi.");
        assert!(usable(fix));
        assert!(!usable(
            commits
                .iter()
                .find(|c| c.subject.starts_with("Görsel"))
                .unwrap()
        ));

        let ex = examples_from_commits(&commits);
        // İlk sürüm (yeni dosya) + düzeltme commit'i: her biri 3 örnek.
        assert_eq!(ex.len(), 6, "{ex:#?}");
        let edit = ex
            .iter()
            .find(|e| e.messages[0].content.starts_with("Görev: Can sıfırın"))
            .unwrap();
        // Düzenleme blokları, eski dosyaya uygulanınca commit'in yeni halini üretmeli.
        let mut content = v1.to_string();
        for b in parse_blocks(&edit.messages[1].content) {
            content = apply_block(&content, &b).unwrap();
        }
        assert_eq!(content, v2);
        assert!(ex.iter().any(|e| e.messages[1]
            .content
            .contains("Negatif can hatası giderildi.")));
        std::fs::remove_dir_all(&dir).ok();
    }
}
