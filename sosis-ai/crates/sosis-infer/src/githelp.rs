//! Git yardımcısı: commit mesajı ve değişiklik incelemesi.
//!
//! İnceleme iki katmanlıdır: modelin açıklaması + modelden bağımsız, kesin kontroller (eklenen
//! hata ayıklama çıktıları, TODO'lar, `unwrap`, çok uzun satırlar). Küçük bir modelin "incelemesine"
//! körü körüne güvenilmemesi için ikincisi her zaman gösterilir.

use std::path::Path;
use std::process::{Command, Stdio};

use anyhow::{bail, Context, Result};
use sosis_data::gitmine::{commit_msg_request, explain_request, parse_diff};

use crate::chat::{reply, ChatSession};
use crate::{Generator, SamplingParams};

fn git(repo: &Path, args: &[&str]) -> Result<String> {
    let out = Command::new("git")
        .arg("-C")
        .arg(repo)
        .args(args)
        .output()
        .context("git çalıştırılamadı (kurulu mu?)")?;
    if !out.status.success() {
        bail!(
            "git {}: {}",
            args.join(" "),
            String::from_utf8_lossy(&out.stderr).trim()
        );
    }
    Ok(String::from_utf8_lossy(&out.stdout).into_owned())
}

/// Hazırlanmış (staged) değişiklikler.
pub fn staged_diff(repo: &Path) -> Result<String> {
    git(repo, &["diff", "--staged", "--no-color", "--no-ext-diff"])
}

/// Bir aralığın değişiklikleri (ör. `main..HEAD`); boşsa çalışma alanı + staged.
pub fn range_diff(repo: &Path, range: Option<&str>) -> Result<String> {
    match range {
        Some(r) => git(repo, &["diff", "--no-color", "--no-ext-diff", r]),
        None => git(repo, &["diff", "HEAD", "--no-color", "--no-ext-diff"]),
    }
}

/// Diff'i dosyalara böler (`diff --git` başlıklarından).
pub fn split_files(diff: &str) -> Vec<String> {
    let mut out: Vec<String> = Vec::new();
    for line in diff.lines() {
        if line.starts_with("diff --git ") || out.is_empty() {
            out.push(String::new());
        }
        let cur = out.last_mut().unwrap();
        if !line.starts_with("index ") {
            cur.push_str(line);
            cur.push('\n');
        }
    }
    out.retain(|f| !f.trim().is_empty());
    out
}

/// Diff'i yaklaşık `max_chars` karaktere sığdırır: her dosyaya eşit pay, fazlası kırpılır.
pub fn fit_diff(diff: &str, max_chars: usize) -> String {
    if diff.len() <= max_chars {
        return diff.to_string();
    }
    let files = split_files(diff);
    let share = (max_chars / files.len().max(1)).max(200);
    files
        .iter()
        .map(|f| {
            if f.len() <= share {
                return f.clone();
            }
            let mut kept = String::new();
            let mut dropped = 0;
            for l in f.lines() {
                if kept.len() + l.len() + 1 > share {
                    dropped += 1;
                } else {
                    kept.push_str(l);
                    kept.push('\n');
                }
            }
            format!("{kept}… ({dropped} satır kırpıldı)\n")
        })
        .collect()
}

/// Modelin cevabından commit mesajını temizler (kod çitleri, tırnaklar).
pub fn clean_message(answer: &str) -> String {
    let t = answer.trim();
    let t = t
        .strip_prefix("```")
        .map(|r| r.split_once('\n').map(|x| x.1).unwrap_or(r))
        .unwrap_or(t);
    let t = t
        .trim_end_matches("```")
        .trim()
        .trim_matches(['"', '\'', '`'])
        .trim();
    t.to_string()
}

fn ask(g: &Generator, prompt: &str, params: &SamplingParams) -> String {
    let mut s = ChatSession::new(None);
    reply(g, &mut s, prompt, None, params, &mut |_| {})
}

/// Bağlamın yarısı diff'e ayrılır (kabaca 3 karakter ≈ 1 token).
fn budget(g: &Generator) -> usize {
    g.context_len() * 3 / 2
}

pub fn commit_message(g: &Generator, diff: &str, params: &SamplingParams) -> String {
    clean_message(&ask(
        g,
        &commit_msg_request(&fit_diff(diff, budget(g))),
        params,
    ))
}

/// Modelden bağımsız kontroller: (dosya, satır içeriği, not).
pub fn static_checks(diff: &str) -> Vec<(String, String, &'static str)> {
    const RULES: &[(&str, &str)] = &[
        ("dbg!(", "hata ayıklama çıktısı (dbg!) kalmış"),
        ("console.log(", "hata ayıklama çıktısı (console.log) kalmış"),
        (
            "System.Diagnostics.Debug.WriteLine",
            "hata ayıklama çıktısı kalmış",
        ),
        ("TODO", "yeni TODO eklenmiş"),
        ("FIXME", "yeni FIXME eklenmiş"),
        (
            ".unwrap()",
            "unwrap() panik yapabilir; hata yönetimini düşünün",
        ),
        ("println!(\"debug", "hata ayıklama çıktısı kalmış"),
        ("password =", "kodda parola olabilir"),
        ("api_key", "kodda anahtar olabilir"),
    ];
    let mut out = Vec::new();
    let mut file = String::new();
    for line in diff.lines() {
        if let Some(rest) = line.strip_prefix("+++ b/") {
            file = rest.to_string();
            continue;
        }
        let Some(added) = line.strip_prefix('+').filter(|_| !line.starts_with("+++")) else {
            continue;
        };
        for (pat, note) in RULES {
            if added.contains(pat) {
                out.push((file.clone(), added.trim().to_string(), *note));
            }
        }
        if added.chars().count() > 140 {
            out.push((
                file.clone(),
                added.chars().take(60).collect::<String>() + "…",
                "satır çok uzun (>140)",
            ));
        }
    }
    out
}

/// Markdown inceleme raporu.
pub fn review(g: &Generator, diff: &str, params: &SamplingParams) -> String {
    let files = split_files(diff);
    let parsed = parse_diff(diff);
    let mut md = format!("# Değişiklik incelemesi\n\n{} dosya", files.len());
    let (add, rem): (usize, usize) = parsed
        .iter()
        .fold((0, 0), |(a, r), f| (a + f.added, r + f.removed));
    md.push_str(&format!(", +{add} / −{rem} satır\n"));
    let checks = static_checks(diff);
    md.push_str("\n## Kesin kontroller\n\n");
    if checks.is_empty() {
        md.push_str("Sorun bulunmadı.\n");
    }
    for (f, l, note) in &checks {
        md.push_str(&format!("- `{f}`: {note} — `{l}`\n"));
    }
    md.push_str("\n## Model açıklaması (Sosy)\n");
    for (fd, text) in parsed.iter().zip(&files) {
        let answer = ask(g, &explain_request(&fit_diff(text, budget(g))), params);
        md.push_str(&format!("\n### `{}`\n\n{}\n", fd.path, answer.trim()));
    }
    md
}

/// Mesajla commit eder (mesaj stdin'den verilir; kabuk kaçışı gerekmez).
pub fn commit(repo: &Path, message: &str) -> Result<()> {
    let mut child = Command::new("git")
        .arg("-C")
        .arg(repo)
        .args(["commit", "-F", "-"])
        .stdin(Stdio::piped())
        .spawn()?;
    use std::io::Write;
    child.stdin.take().unwrap().write_all(message.as_bytes())?;
    if !child.wait()?.success() {
        bail!("git commit başarısız");
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::test_util::*;

    const DIFF: &str = "diff --git a/a.rs b/a.rs\nindex 1..2 100644\n--- a/a.rs\n+++ b/a.rs\n@@ -1,2 +1,3 @@\n fn a() {\n+    dbg!(x); // TODO kaldır\n }\ndiff --git a/b.py b/b.py\n--- a/b.py\n+++ b/b.py\n@@ -1 +1 @@\n-x = 1\n+x = 2\n";

    #[test]
    fn split_fit_and_checks() {
        let f = split_files(DIFF);
        assert_eq!(f.len(), 2);
        assert!(!f[0].contains("index "));
        let long = DIFF.repeat(50);
        assert!(fit_diff(&long, 1000).len() < long.len());
        let c = static_checks(DIFF);
        assert!(c.iter().any(|(f, _, n)| f == "a.rs" && n.contains("dbg!")));
        assert!(c.iter().any(|(_, _, n)| n.contains("TODO")));
        assert!(!c.iter().any(|(f, _, _)| f == "b.py"));
    }

    #[test]
    fn cleans_messages() {
        assert_eq!(
            clean_message("```\nHasar hatası düzeltildi\n```"),
            "Hasar hatası düzeltildi"
        );
        assert_eq!(clean_message("\"Ekle: envanter\""), "Ekle: envanter");
    }

    #[test]
    fn end_to_end_in_temp_repo() {
        let dir = std::env::temp_dir().join(format!("sosis_githelp_{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(&dir).unwrap();
        let run = |args: &[&str]| {
            assert!(
                Command::new("git")
                    .arg("-C")
                    .arg(&dir)
                    .args(args)
                    .output()
                    .unwrap()
                    .status
                    .success(),
                "{args:?}"
            );
        };
        run(&["init", "-q"]);
        run(&["config", "user.name", "Test"]);
        run(&["config", "user.email", "test@example.com"]);
        run(&["config", "commit.gpgsign", "false"]);
        std::fs::write(dir.join("a.rs"), "fn a() {}\n").unwrap();
        run(&["add", "."]);
        let diff = staged_diff(&dir).unwrap();
        assert!(diff.contains("+fn a() {}"));
        let g = generator(3, 32, 1);
        let msg = commit_message(&g, &diff, &greedy(12));
        let msg = if msg.trim().is_empty() {
            "ilk".to_string()
        } else {
            msg
        };
        commit(&dir, &msg).unwrap();
        assert!(staged_diff(&dir).unwrap().is_empty());
        let rep = review(&g, &DIFF.to_string(), &greedy(8));
        assert!(rep.contains("Kesin kontroller") && rep.contains("dbg!"));
        std::fs::remove_dir_all(&dir).ok();
    }
}
