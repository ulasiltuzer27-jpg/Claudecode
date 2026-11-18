//! Kendini düzelten kod döngüsü: model kodu yazar → kod derlenip çalıştırılır → hata varsa hata
//! mesajı modele geri verilir ve düzeltmesi istenir (en fazla N tur).
//!
//! Derleme/çalıştırma `sosis-bench` düzeneğiyle yapılır (geçici klasör, süre sınırı). Üretilen kod
//! bilgisayarınızda çalışır; bu bir güvenlik kum havuzu değildir.

use std::time::Duration;

use anyhow::Result;
use sosis_bench::{run_program, Lang, Outcome};
use sosis_tokenizer::chat::{Message, Role};

use crate::chat::{reply, ChatSession};
use crate::rag::CodeIndex;
use crate::{Generator, SamplingParams};

/// Bir deneme ve sonucu.
#[derive(Debug, Clone)]
pub struct Attempt {
    pub round: usize,
    pub code: String,
    pub outcome: Outcome,
}

impl Attempt {
    pub fn passed(&self) -> bool {
        self.outcome.passed()
    }
}

/// Metindeki ilk kod bloğu (dil etiketli olan tercih edilir); blok yoksa metnin tamamı.
pub fn extract_code(text: &str, lang: Lang) -> String {
    let tags: &[&str] = match lang {
        Lang::Rust => &["rust", "rs"],
        Lang::Python => &["python", "py"],
    };
    let parts: Vec<&str> = text.split("```").collect();
    let mut first = None;
    for (i, p) in parts.iter().enumerate() {
        if i % 2 == 0 || i + 1 >= parts.len() {
            continue; // kapanmamış blok
        }
        let (tag, body) = p.split_once('\n').unwrap_or(("", p));
        let tag = tag.trim().to_lowercase();
        if tags.contains(&tag.as_str()) {
            return body.trim_end().to_string();
        }
        first.get_or_insert(body.trim_end().to_string());
    }
    first.unwrap_or_else(|| text.trim().to_string())
}

/// Kodu (ve varsa testleri) çalıştırılabilir bir programa dönüştürür.
pub fn build_program(code: &str, tests: Option<&str>, lang: Lang) -> String {
    let mut p = code.to_string();
    if let Some(t) = tests {
        p.push_str("\n\n");
        p.push_str(t);
    }
    if lang == Lang::Rust && !p.contains("fn main") {
        p.push_str("\n\nfn main() {}\n"); // yalnızca derleme kontrolü
    }
    p
}

/// Hata mesajını modele verilecek kısa bir düzeltme isteğine çevirir.
pub fn feedback(outcome: &Outcome) -> String {
    let (kind, err) = match outcome {
        Outcome::CompileError(e) => ("derlenmedi", e.as_str()),
        Outcome::Fail(e) => ("testlerden geçmedi", e.as_str()),
        Outcome::Timeout => ("zaman aşımına uğradı (sonsuz döngü olabilir)", ""),
        Outcome::Pass => ("geçti", ""),
    };
    let err: String = err.lines().take(25).collect::<Vec<_>>().join("\n");
    if err.is_empty() {
        format!("Kod {kind}. Lütfen düzeltip kodun tamamını tekrar yaz.")
    } else {
        format!("Kod {kind}:\n```\n{err}\n```\nLütfen hatayı düzeltip kodun tamamını tekrar yaz.")
    }
}

/// Modelden bağımsız döngü: `respond(geçmiş)` bir sonraki asistan cevabını döndürür.
pub fn repair_loop(
    task: &str,
    lang: Lang,
    tests: Option<&str>,
    max_rounds: usize,
    timeout: Duration,
    mut respond: impl FnMut(&[Message]) -> String,
    mut on_attempt: impl FnMut(&Attempt),
) -> Result<Vec<Attempt>> {
    let mut history = vec![Message::new(Role::User, task)];
    let mut attempts = Vec::new();
    for round in 1..=max_rounds.max(1) {
        let answer = respond(&history);
        history.push(Message::new(Role::Assistant, answer.clone()));
        let code = extract_code(&answer, lang);
        let outcome = run_program(lang, &build_program(&code, tests, lang), timeout)?;
        let a = Attempt {
            round,
            code,
            outcome,
        };
        on_attempt(&a);
        let done = a.passed();
        if !done {
            history.push(Message::new(Role::User, feedback(&a.outcome)));
        }
        attempts.push(a);
        if done {
            break;
        }
    }
    Ok(attempts)
}

/// Gerçek modelle döngü: konuşma `session`'a yazılır (sohbet hafızasında kalır).
#[allow(clippy::too_many_arguments)]
pub fn solve(
    generator: &Generator,
    session: &mut ChatSession,
    task: &str,
    lang: Lang,
    tests: Option<&str>,
    max_rounds: usize,
    index: Option<&CodeIndex>,
    params: &SamplingParams,
    on_text: &mut dyn FnMut(&str),
    mut on_attempt: impl FnMut(&Attempt),
) -> Result<Vec<Attempt>> {
    let mut pending = Some(task.to_string());
    repair_loop(
        task,
        lang,
        tests,
        max_rounds,
        Duration::from_secs(20),
        |history| {
            // Model, geçmişin son kullanıcı mesajına cevap verir (ilk tur: görev).
            let user = pending.take().unwrap_or_else(|| {
                history
                    .last()
                    .map(|m| m.content.clone())
                    .unwrap_or_default()
            });
            reply(generator, session, &user, index, params, on_text)
        },
        |a| on_attempt(a),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extracts_preferred_block() {
        let t = "Açıklama\n```text\nçıktı\n```\nKod:\n```rust\nfn a() -> u8 { 1 }\n```\n";
        assert_eq!(extract_code(t, Lang::Rust), "fn a() -> u8 { 1 }");
        assert_eq!(extract_code("```\nx = 1\n```", Lang::Python), "x = 1");
        assert_eq!(extract_code("def f(): pass", Lang::Python), "def f(): pass");
        // Kapanmamış blok tamamı alınır.
        assert_eq!(extract_code("```rust\nfn a(", Lang::Rust), "```rust\nfn a(");
    }

    #[test]
    fn fixes_after_feedback() {
        let tests = "fn main() { assert_eq!(double(4), 8); }";
        let mut calls = 0;
        let attempts = repair_loop(
            "double fonksiyonunu yaz",
            Lang::Rust,
            Some(tests),
            3,
            Duration::from_secs(60),
            |history| {
                calls += 1;
                match calls {
                    // 1. tur: derlenmez.
                    1 => "```rust\nfn double(x: i32) -> i32 { x * 2 \n```".into(),
                    // 2. tur: derlenir ama yanlış; geri bildirim mesajı hatayı içermeli.
                    2 => {
                        assert!(history.last().unwrap().content.contains("derlenmedi"));
                        "```rust\nfn double(x: i32) -> i32 { x + 2 }\n```".into()
                    }
                    _ => {
                        assert!(history
                            .last()
                            .unwrap()
                            .content
                            .contains("testlerden geçmedi"));
                        "İşte düzeltilmiş hali:\n```rust\nfn double(x: i32) -> i32 { x * 2 }\n```"
                            .into()
                    }
                }
            },
            |_| {},
        )
        .unwrap();
        assert_eq!(attempts.len(), 3);
        assert!(matches!(attempts[0].outcome, Outcome::CompileError(_)));
        assert!(matches!(attempts[1].outcome, Outcome::Fail(_)));
        assert!(attempts[2].passed());
    }

    #[test]
    fn python_and_compile_only() {
        let a = repair_loop(
            "x",
            Lang::Python,
            Some("assert inc(1) == 2"),
            1,
            Duration::from_secs(20),
            |_| "```python\ndef inc(x):\n    return x + 1\n```".into(),
            |_| {},
        )
        .unwrap();
        assert!(a[0].passed());
        // Testsiz Rust: yalnızca derleme kontrolü.
        let b = repair_loop(
            "x",
            Lang::Rust,
            None,
            1,
            Duration::from_secs(60),
            |_| "fn f() -> u8 { 3 }".into(),
            |_| {},
        )
        .unwrap();
        assert!(b[0].passed());
    }
}
