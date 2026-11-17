//! # sosis-bench
//!
//! Derle-çalıştır değerlendirmesi: model bir fonksiyonun gövdesini yazar, program gerçekten
//! derlenip (`rustc`) / çalıştırılıp (`python`) testlerden geçerse görev başarılı sayılır.
//! Sonuç pass@k olarak raporlanır (Codex makalesindeki yansız tahminci).
//!
//! **Uyarı:** Üretilen kod sizin bilgisayarınızda geçici bir klasörde çalıştırılır. Görevler saf
//! fonksiyonlardır ve süre sınırı vardır, ancak bu bir güvenlik kum havuzu (sandbox) değildir.

use std::fs;
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::time::{Duration, Instant};

use anyhow::{bail, Context, Result};
use serde::Deserialize;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Lang {
    Rust,
    Python,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Task {
    pub id: String,
    pub prompt: String,
    /// Referans devam (düzeneğin kendi doğruluğunu sınamak için).
    pub solution: String,
    pub tests: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Suite {
    pub lang: Lang,
    /// Modele dil ipucu olarak verilen dosya adı.
    pub file: String,
    #[serde(rename = "task")]
    pub tasks: Vec<Task>,
}

/// Bir klasördeki tüm `*.toml` görev setlerini yükler.
pub fn load_suites(dir: &Path) -> Result<Vec<Suite>> {
    let mut paths: Vec<PathBuf> = fs::read_dir(dir)
        .with_context(|| format!("görev klasörü okunamadı: {}", dir.display()))?
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().is_some_and(|e| e == "toml"))
        .collect();
    paths.sort();
    let mut out = Vec::new();
    for p in paths {
        let text = fs::read_to_string(&p)?;
        let suite: Suite = toml::from_str(&text)
            .with_context(|| format!("geçersiz görev dosyası: {}", p.display()))?;
        out.push(suite);
    }
    if out.is_empty() {
        bail!("{} içinde görev dosyası yok", dir.display());
    }
    Ok(out)
}

/// Üretilen devamı fonksiyonun bittiği yerde keser. `None`: fonksiyon henüz bitmedi.
pub fn function_end(lang: Lang, prompt: &str, completion: &str) -> Option<usize> {
    match lang {
        Lang::Rust => {
            let mut depth: i64 =
                prompt.matches('{').count() as i64 - prompt.matches('}').count() as i64;
            for (i, c) in completion.char_indices() {
                match c {
                    '{' => depth += 1,
                    '}' => {
                        depth -= 1;
                        if depth <= 0 {
                            return Some(i + 1);
                        }
                    }
                    _ => {}
                }
            }
            None
        }
        Lang::Python => {
            // Gövdeden sonra girintisiz ilk satır fonksiyonun sonudur.
            let mut offset = 0;
            let mut seen_body = false;
            for line in completion.split_inclusive('\n') {
                let trimmed = line.trim();
                if !trimmed.is_empty() {
                    if line.starts_with(|c: char| !c.is_whitespace()) && seen_body {
                        return Some(offset);
                    }
                    seen_body = true;
                }
                offset += line.len();
            }
            None
        }
    }
}

/// Tamamlanmış (veya kesilmiş) devamı programa dönüştürür.
pub fn build_program(task: &Task, body: &str) -> String {
    format!("{}{}\n\n{}", task.prompt, body, task.tests)
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Outcome {
    Pass,
    CompileError(String),
    Fail(String),
    Timeout,
}

impl Outcome {
    pub fn passed(&self) -> bool {
        *self == Outcome::Pass
    }
}

/// Programı geçici bir klasörde derleyip çalıştırır.
pub fn run_program(lang: Lang, src: &str, timeout: Duration) -> Result<Outcome> {
    static COUNTER: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
    let n = COUNTER.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
    let dir = std::env::temp_dir().join(format!("sosis_bench_{}_{n}", std::process::id()));
    fs::create_dir_all(&dir)?;
    let result = match lang {
        Lang::Rust => {
            let file = dir.join("main.rs");
            fs::write(&file, src)?;
            let exe = dir.join(if cfg!(windows) { "prog.exe" } else { "prog" });
            let mut cmd = Command::new("rustc");
            cmd.args(["--edition", "2021", "-A", "warnings", "-o"])
                .arg(&exe)
                .arg(&file);
            match run_with_timeout(cmd, &dir, Duration::from_secs(120))? {
                Some((true, _)) => match run_with_timeout(Command::new(&exe), &dir, timeout)? {
                    Some((true, _)) => Outcome::Pass,
                    Some((false, err)) => Outcome::Fail(err),
                    None => Outcome::Timeout,
                },
                Some((false, err)) => Outcome::CompileError(err),
                None => Outcome::Timeout,
            }
        }
        Lang::Python => {
            let file = dir.join("main.py");
            fs::write(&file, src)?;
            let mut cmd = Command::new(python_cmd());
            cmd.arg(&file);
            match run_with_timeout(cmd, &dir, timeout)? {
                Some((true, _)) => Outcome::Pass,
                Some((false, err))
                    if err.contains("SyntaxError") || err.contains("IndentationError") =>
                {
                    Outcome::CompileError(err)
                }
                Some((false, err)) => Outcome::Fail(err),
                None => Outcome::Timeout,
            }
        }
    };
    fs::remove_dir_all(&dir).ok();
    Ok(result)
}

fn python_cmd() -> &'static str {
    static CMD: std::sync::OnceLock<&'static str> = std::sync::OnceLock::new();
    CMD.get_or_init(|| {
        for c in ["python3", "python"] {
            if Command::new(c)
                .arg("--version")
                .stdout(Stdio::null())
                .stderr(Stdio::null())
                .status()
                .is_ok_and(|s| s.success())
            {
                return c;
            }
        }
        "python3"
    })
}

/// `Some((başarılı_mı, stderr))` ya da zaman aşımında `None`.
fn run_with_timeout(
    mut cmd: Command,
    dir: &Path,
    timeout: Duration,
) -> Result<Option<(bool, String)>> {
    let err_path = dir.join("stderr.txt");
    let err_file = fs::File::create(&err_path)?;
    let mut child = cmd
        .current_dir(dir)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(err_file)
        .spawn()
        .with_context(|| format!("çalıştırılamadı: {cmd:?} (kurulu mu?)"))?;
    let start = Instant::now();
    loop {
        if let Some(status) = child.try_wait()? {
            let mut err = fs::read_to_string(&err_path).unwrap_or_default();
            err.truncate(2000);
            return Ok(Some((status.success(), err)));
        }
        if start.elapsed() > timeout {
            child.kill().ok();
            child.wait().ok();
            return Ok(None);
        }
        std::thread::sleep(Duration::from_millis(10));
    }
}

/// pass@k'nın yansız tahmini: `1 - C(n-c, k) / C(n, k)`.
pub fn pass_at_k(n: usize, c: usize, k: usize) -> f64 {
    if n == 0 || k == 0 {
        return 0.0;
    }
    let k = k.min(n);
    if n - c < k {
        return 1.0;
    }
    let mut prod = 1.0;
    for i in (n - c + 1)..=n {
        prod *= 1.0 - k as f64 / i as f64;
    }
    1.0 - prod
}

#[derive(Debug, Clone)]
pub struct TaskResult {
    pub suite: Lang,
    pub id: String,
    pub samples: usize,
    pub passed: usize,
    /// İlk başarısız örneğin özeti.
    pub first_error: Option<String>,
}

#[derive(Debug, Clone)]
pub struct Report {
    pub results: Vec<TaskResult>,
}

impl Report {
    pub fn pass_at(&self, k: usize, lang: Option<Lang>) -> f64 {
        let rs: Vec<&TaskResult> = self
            .results
            .iter()
            .filter(|r| lang.is_none_or(|l| r.suite == l))
            .collect();
        if rs.is_empty() {
            return 0.0;
        }
        rs.iter()
            .map(|r| pass_at_k(r.samples, r.passed, k))
            .sum::<f64>()
            / rs.len() as f64
    }
}

/// Görevleri değerlendirir. `complete(suite, task, örnek_no)` fonksiyon gövdesini döndürmelidir
/// (fonksiyon sonundan sonraki kısım ayrıca kesilir).
pub fn evaluate(
    suites: &[Suite],
    samples: usize,
    timeout: Duration,
    mut complete: impl FnMut(&Suite, &Task, usize) -> String,
    mut on_result: impl FnMut(&TaskResult),
) -> Result<Report> {
    let mut results = Vec::new();
    for suite in suites {
        for task in &suite.tasks {
            let mut passed = 0;
            let mut first_error = None;
            for s in 0..samples.max(1) {
                let raw = complete(suite, task, s);
                let body = match function_end(suite.lang, &task.prompt, &raw) {
                    Some(end) => &raw[..end],
                    None => raw.as_str(),
                };
                let outcome = run_program(suite.lang, &build_program(task, body), timeout)?;
                if outcome.passed() {
                    passed += 1;
                } else if first_error.is_none() {
                    first_error = Some(match &outcome {
                        Outcome::CompileError(e) => format!("derleme: {}", first_line(e)),
                        Outcome::Fail(e) => format!("test: {}", first_line(e)),
                        Outcome::Timeout => "zaman aşımı".into(),
                        Outcome::Pass => unreachable!(),
                    });
                }
            }
            let r = TaskResult {
                suite: suite.lang,
                id: task.id.clone(),
                samples: samples.max(1),
                passed,
                first_error,
            };
            on_result(&r);
            results.push(r);
        }
    }
    Ok(Report { results })
}

fn first_line(s: &str) -> String {
    s.lines()
        .find(|l| l.contains("error") || l.contains("Error") || l.contains("panicked"))
        .or_else(|| s.lines().next())
        .unwrap_or("")
        .trim()
        .chars()
        .take(160)
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn suites() -> Vec<Suite> {
        load_suites(&Path::new(env!("CARGO_MANIFEST_DIR")).join("../../benchmarks")).unwrap()
    }

    #[test]
    fn pass_at_k_estimator() {
        assert_eq!(pass_at_k(1, 1, 1), 1.0);
        assert_eq!(pass_at_k(1, 0, 1), 0.0);
        assert!((pass_at_k(10, 1, 1) - 0.1).abs() < 1e-9);
        assert!((pass_at_k(10, 1, 10) - 1.0).abs() < 1e-9);
        assert!(
            (pass_at_k(10, 2, 5)
                - (1.0 - (8.0 * 7.0 * 6.0 * 5.0 * 4.0) / (10.0 * 9.0 * 8.0 * 7.0 * 6.0)))
                .abs()
                < 1e-9
        );
    }

    #[test]
    fn truncates_at_function_end() {
        let rust = function_end(
            Lang::Rust,
            "fn a() -> u8 {\n",
            "    if x { 1 } else { 2 }\n}\nfn extra() {}",
        );
        assert_eq!(rust, Some("    if x { 1 } else { 2 }\n}".len()));
        let py = function_end(Lang::Python, "def a():\n", "    return 1\n\nprint('x')\n");
        assert_eq!(py, Some("    return 1\n\n".len()));
        assert_eq!(
            function_end(Lang::Python, "def a():\n", "    return 1\n"),
            None
        );
    }

    /// Referans çözümler tüm testlerden geçmeli; yanlış gövde başarısız olmalı.
    #[test]
    fn reference_solutions_pass() {
        let suites = suites();
        assert!(suites.iter().map(|s| s.tasks.len()).sum::<usize>() >= 40);
        let report = evaluate(
            &suites,
            1,
            Duration::from_secs(20),
            |_, t, _| format!("{}\nfn __after() {{}}\n", t.solution),
            |r| assert_eq!(r.passed, 1, "{:?}: {:?}", r.id, r.first_error),
        )
        .unwrap();
        assert_eq!(report.pass_at(1, None), 1.0);

        let wrong = evaluate(
            &suites[..1],
            1,
            Duration::from_secs(20),
            |s, _, _| match s.lang {
                Lang::Rust => "    todo!()\n}\n".into(),
                Lang::Python => "    return None\n".into(),
            },
            |_| {},
        )
        .unwrap();
        assert_eq!(wrong.pass_at(1, None), 0.0);
    }
}
