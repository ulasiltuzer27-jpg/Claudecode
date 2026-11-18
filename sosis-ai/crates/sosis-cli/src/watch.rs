//! `sosis watch`: eğitimi gözeten bekçi. `sosis train --resume`'u alt süreç olarak çalıştırır;
//! çökerse bekleyip yeniden başlatır. Bellek yetmezliğinde (`--auto-oom`) micro_batch'i yarıya
//! indirip grad_accum'u ikiye katlar (adım başına token aynı kalır). Kayıp NaN olursa durur.

use std::collections::VecDeque;
use std::io::{BufRead, BufReader, Write};
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::time::Duration;

use anyhow::{bail, Context, Result};

pub struct WatchOptions {
    pub max_restarts: usize,
    pub auto_oom: bool,
    pub delay: Duration,
}

/// Bir çalıştırmanın sonucu: başarılı mı, hata çıktısının son satırları.
pub struct RunResult {
    pub success: bool,
    pub tail: String,
}

#[derive(Debug, PartialEq, Eq)]
pub enum Verdict {
    Done,
    Restart,
    RestartAfterOom {
        micro_batch: usize,
        grad_accum: usize,
    },
    Stop(String),
}

const OOM_MARKERS: &[&str] = &[
    "out of memory",
    "OutOfMemory",
    "CUDA_ERROR_OUT_OF_MEMORY",
    "Out of memory",
    "failed to allocate",
];

pub fn is_oom(tail: &str) -> bool {
    OOM_MARKERS.iter().any(|m| tail.contains(m))
}

pub fn is_nan(tail: &str) -> bool {
    tail.contains("NaN")
}

/// TOML metninde `anahtar = sayı` satırını okur (yorumlar korunur).
pub fn read_int(text: &str, key: &str) -> Option<usize> {
    text.lines().find_map(|l| {
        let l = l.trim();
        let rest = l.strip_prefix(key)?.trim_start().strip_prefix('=')?;
        rest.split('#').next()?.trim().parse().ok()
    })
}

/// `anahtar = sayı` satırını yeni değerle değiştirir; satır sonu yorumu korunur.
pub fn set_int(text: &str, key: &str, value: usize) -> String {
    text.lines()
        .map(|l| {
            let t = l.trim_start();
            match t.strip_prefix(key).map(str::trim_start) {
                Some(rest) if rest.starts_with('=') => {
                    let indent = &l[..l.len() - t.len()];
                    let comment = rest
                        .find('#')
                        .map(|i| format!("  {}", rest[i..].trim_end()))
                        .unwrap_or_default();
                    format!("{indent}{key} = {value}{comment}")
                }
                _ => l.to_string(),
            }
        })
        .collect::<Vec<_>>()
        .join("\n")
        + if text.ends_with('\n') { "\n" } else { "" }
}

/// Bir çalıştırmanın sonucuna göre ne yapılacağı.
pub fn decide(result: &RunResult, config_text: &str, opts: &WatchOptions) -> Verdict {
    if result.success {
        return Verdict::Done;
    }
    if is_nan(&result.tail) {
        return Verdict::Stop(
            "kayıp NaN oldu; lr'yi düşürün (yeniden başlatmak aynı sonucu verir)".into(),
        );
    }
    if is_oom(&result.tail) {
        if !opts.auto_oom {
            return Verdict::Stop("bellek yetmedi; --auto-oom ile otomatik küçültmeyi açın ya da micro_batch'i düşürün".into());
        }
        let (mb, ga) = (
            read_int(config_text, "micro_batch").unwrap_or(1),
            read_int(config_text, "grad_accum").unwrap_or(1),
        );
        if mb <= 1 {
            return Verdict::Stop(
                "micro_batch zaten 1; gradient_checkpointing / bf16 / optimizer_offload deneyin"
                    .into(),
            );
        }
        return Verdict::RestartAfterOom {
            micro_batch: mb / 2,
            grad_accum: ga * 2,
        };
    }
    Verdict::Restart
}

/// Bekçi döngüsü; `spawn` bir eğitim çalıştırması yapar.
pub fn run_watch(
    config: &Path,
    opts: &WatchOptions,
    log: &mut dyn Write,
    mut spawn: impl FnMut() -> Result<RunResult>,
) -> Result<usize> {
    let mut restarts = 0;
    loop {
        writeln!(log, "▶ eğitim başlatılıyor (yeniden başlatma: {restarts})")?;
        let result = spawn()?;
        let text = std::fs::read_to_string(config)?;
        match decide(&result, &text, opts) {
            Verdict::Done => {
                writeln!(log, "✓ eğitim tamamlandı")?;
                return Ok(restarts);
            }
            Verdict::Stop(why) => {
                writeln!(log, "■ durduruldu: {why}")?;
                bail!("{why}");
            }
            Verdict::RestartAfterOom {
                micro_batch,
                grad_accum,
            } => {
                let new = set_int(
                    &set_int(&text, "micro_batch", micro_batch),
                    "grad_accum",
                    grad_accum,
                );
                std::fs::write(config, new)?;
                writeln!(
                    log,
                    "⚠ bellek yetmedi → micro_batch {micro_batch}, grad_accum {grad_accum}"
                )?;
            }
            Verdict::Restart => writeln!(
                log,
                "⚠ eğitim çöktü; {} sn sonra devam",
                opts.delay.as_secs()
            )?,
        }
        restarts += 1;
        if restarts > opts.max_restarts {
            writeln!(log, "■ en fazla yeniden başlatma sayısına ulaşıldı")?;
            bail!("{} kez yeniden başlatıldı, vazgeçildi", opts.max_restarts);
        }
        std::thread::sleep(opts.delay);
    }
}

/// Gerçek alt süreç: `sosis train -c <config> --resume`; hata çıktısı ekrana aktarılır ve son
/// 60 satırı saklanır.
pub fn spawn_train(config: &Path, extra: &[String]) -> Result<RunResult> {
    let exe = std::env::current_exe().context("sosis yolu bulunamadı")?;
    let mut child = Command::new(exe)
        .arg("train")
        .arg("-c")
        .arg(config)
        .arg("--resume")
        .args(extra)
        .stdout(Stdio::inherit())
        .stderr(Stdio::piped())
        .spawn()?;
    let mut tail: VecDeque<String> = VecDeque::new();
    for line in BufReader::new(child.stderr.take().unwrap()).lines() {
        let line = line.unwrap_or_default();
        eprintln!("{line}");
        tail.push_back(line);
        if tail.len() > 60 {
            tail.pop_front();
        }
    }
    let status = child.wait()?;
    Ok(RunResult {
        success: status.success(),
        tail: tail.into_iter().collect::<Vec<_>>().join("\n"),
    })
}

/// `runs/<model>/watch.log` dosyası + ekran.
pub struct TeeLog {
    file: std::fs::File,
}

impl TeeLog {
    pub fn open(path: PathBuf) -> Result<Self> {
        if let Some(d) = path.parent() {
            std::fs::create_dir_all(d)?;
        }
        Ok(Self {
            file: std::fs::OpenOptions::new()
                .create(true)
                .append(true)
                .open(path)?,
        })
    }
}

impl Write for TeeLog {
    fn write(&mut self, buf: &[u8]) -> std::io::Result<usize> {
        std::io::stderr().write_all(buf)?;
        self.file.write(buf)
    }
    fn flush(&mut self) -> std::io::Result<()> {
        self.file.flush()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const CFG: &str =
        "[train]\nmicro_batch = 8      # VRAM yetmezse düşürün\ngrad_accum = 16\nlr = 1e-3\n";

    fn opts(auto_oom: bool) -> WatchOptions {
        WatchOptions {
            max_restarts: 3,
            auto_oom,
            delay: Duration::ZERO,
        }
    }

    fn cfg_file(name: &str) -> PathBuf {
        let p =
            std::env::temp_dir().join(format!("sosis_watch_{name}_{}.toml", std::process::id()));
        std::fs::write(&p, CFG).unwrap();
        p
    }

    #[test]
    fn edits_keep_comments() {
        let t = set_int(CFG, "micro_batch", 4);
        assert!(t.contains("micro_batch = 4  # VRAM yetmezse düşürün"));
        assert_eq!(read_int(&t, "micro_batch"), Some(4));
        assert_eq!(read_int(&t, "grad_accum"), Some(16));
    }

    #[test]
    fn restarts_after_crash_then_finishes() {
        let p = cfg_file("crash");
        let mut n = 0;
        let mut log = Vec::new();
        let r = run_watch(&p, &opts(false), &mut log, || {
            n += 1;
            Ok(RunResult {
                success: n >= 3,
                tail: "panicked: bağlantı koptu".into(),
            })
        })
        .unwrap();
        assert_eq!(r, 2);
        assert!(String::from_utf8(log).unwrap().contains("tamamlandı"));
    }

    #[test]
    fn oom_halves_batch_and_preserves_tokens() {
        let p = cfg_file("oom");
        let mut n = 0;
        run_watch(&p, &opts(true), &mut Vec::new(), || {
            n += 1;
            Ok(RunResult {
                success: n >= 2,
                tail: "CUDA_ERROR_OUT_OF_MEMORY".into(),
            })
        })
        .unwrap();
        let t = std::fs::read_to_string(&p).unwrap();
        assert_eq!(read_int(&t, "micro_batch"), Some(4));
        assert_eq!(read_int(&t, "grad_accum"), Some(32));
    }

    #[test]
    fn stops_on_nan_and_on_limit() {
        let p = cfg_file("nan");
        let err = run_watch(&p, &opts(true), &mut Vec::new(), || {
            Ok(RunResult {
                success: false,
                tail: "kayıp sonsuz/NaN oldu".into(),
            })
        });
        assert!(err.is_err());
        let p = cfg_file("limit");
        let mut n = 0;
        let err = run_watch(&p, &opts(false), &mut Vec::new(), || {
            n += 1;
            Ok(RunResult {
                success: false,
                tail: "panik".into(),
            })
        });
        assert!(err.is_err());
        assert_eq!(n, 4); // ilk + 3 yeniden başlatma
    }
}
