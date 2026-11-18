//! Proje ajanı için terminal arayüzü: renkli diff, plan ve dosya onayı.

use std::io::{BufRead, Write};

use sosis_infer::agent::{AgentUi, FileChange};

pub struct TerminalUi {
    /// Tüm değişiklikleri sormadan uygula (--evet).
    pub yes_all: bool,
}

fn ask(prompt: &str) -> String {
    eprint!("{prompt}");
    let _ = std::io::stderr().flush();
    let mut s = String::new();
    let _ = std::io::stdin().lock().read_line(&mut s);
    s.trim().to_lowercase()
}

/// Diff'i ANSI renkleriyle yazdırır.
pub fn print_diff(diff: &str) {
    for l in diff.lines() {
        let color = if l.starts_with("+++") || l.starts_with("---") {
            "1"
        } else if l.starts_with('+') {
            "32"
        } else if l.starts_with('-') {
            "31"
        } else if l.starts_with("@@") {
            "36"
        } else {
            "0"
        };
        eprintln!("\x1b[{color}m{l}\x1b[0m");
    }
}

impl AgentUi for TerminalUi {
    fn text(&mut self, s: &str) {
        print!("{s}");
        let _ = std::io::stdout().flush();
    }

    fn info(&mut self, s: &str) {
        eprintln!("{s}");
    }

    fn approve_plan(&mut self, plan: &str) -> Option<String> {
        eprintln!("\n\x1b[1mPlan:\x1b[0m\n{plan}\n");
        if self.yes_all {
            return Some(plan.to_string());
        }
        match ask("[o]nayla / [d]üzenle / [r]eddet: ").chars().next() {
            Some('o' | 'e' | 'y') | None => Some(plan.to_string()),
            Some('d') => {
                eprintln!("Yeni planı yazın (boş satırla bitirin):");
                let mut edited = String::new();
                loop {
                    let mut l = String::new();
                    if std::io::stdin().lock().read_line(&mut l).unwrap_or(0) == 0
                        || l.trim().is_empty()
                    {
                        break;
                    }
                    edited.push_str(&l);
                }
                Some(edited)
            }
            _ => None,
        }
    }

    fn approve_change(&mut self, change: &FileChange) -> bool {
        eprintln!(
            "\n\x1b[1m{} {}\x1b[0m",
            if change.old.is_none() {
                "Yeni dosya:"
            } else {
                "Değişiklik:"
            },
            change.path
        );
        print_diff(&change.diff());
        if self.yes_all {
            return true;
        }
        match ask("Uygulansın mı? [e]vet / [h]ayır / [t]ümü: ")
            .chars()
            .next()
        {
            Some('t') => {
                self.yes_all = true;
                true
            }
            Some('e' | 'y') => true,
            _ => false,
        }
    }
}
