//! `sosis` — Sosis AI komut satırı aracı.
//!
//! Tipik akış:
//! ```text
//! sosis tokenizer-train --input ~/kodlarim --out tokenizer.json
//! sosis prepare        --input ~/kodlarim --tokenizer tokenizer.json --out data
//! sosis train          --config configs/sosy-flash-1.0.toml
//! sosis generate       --run runs/sosy-flash-1.0 --file main.rs --prompt "fn fibonacci("
//! ```

use std::io::{BufRead, Write};
use std::path::PathBuf;
use std::time::Instant;

use anyhow::{Context, Result};
use clap::{Args, Parser, Subcommand};
use sosis_data::{CollectOptions, FilterOptions, PrepareOptions};
use sosis_infer::{Generator, SamplingParams};
use sosis_tokenizer::{BpeTrainer, Tokenizer};
use sosis_train::RunConfig;

#[derive(Parser)]
#[command(
    name = "sosis",
    version,
    about = "Sosis AI — kendi GPU'nuzda sıfırdan eğitilen kod modeli (Sosy 1.0 / Sosy-Flash 1.0)"
)]
struct Cli {
    #[command(subcommand)]
    cmd: Cmd,
}

#[derive(Subcommand)]
enum Cmd {
    /// Kod klasörlerinden byte-level BPE tokenizer eğitir.
    TokenizerTrain {
        /// Kaynak klasörler / dosyalar (.jsonl veri setleri de olabilir).
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, default_value_t = 16384)]
        vocab_size: usize,
        /// Eğitimde kullanılacak en fazla metin (MB).
        #[arg(long, default_value_t = 200)]
        max_mb: u64,
        #[arg(long, short, default_value = "tokenizer.json")]
        out: PathBuf,
    },
    /// Kodu filtreler, tekilleştirir, FIM uygular ve token shard'larına yazar.
    Prepare {
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, short, default_value = "tokenizer.json")]
        tokenizer: PathBuf,
        #[arg(long, short, default_value = "data")]
        out: PathBuf,
        #[arg(long, default_value_t = 0.5)]
        fim_rate: f64,
        #[arg(long, default_value_t = 1.0)]
        val_percent: f64,
        /// İş parçacığı sayısı (varsayılan: tüm çekirdekler).
        #[arg(long)]
        threads: Option<usize>,
        /// Yalnızca bu uzantılar (ör. `--ext rs --ext py`). Boşsa varsayılan liste.
        #[arg(long)]
        ext: Vec<String>,
    },
    /// Bir modeli eğitir.
    Train {
        #[arg(long, short)]
        config: PathBuf,
        /// Son checkpoint'ten devam et.
        #[arg(long)]
        resume: bool,
        /// Yapılandırmadaki backend'i geçersiz kıl (cuda | cpu | auto).
        #[arg(long)]
        backend: Option<String>,
        /// Yapılandırmadaki max_steps'i geçersiz kıl.
        #[arg(long)]
        max_steps: Option<usize>,
    },
    /// Kod tamamlama.
    Generate {
        #[command(flatten)]
        model: ModelArgs,
        #[arg(long, short)]
        prompt: Option<String>,
        #[arg(long)]
        prompt_file: Option<PathBuf>,
        #[command(flatten)]
        sampling: SamplingArgs,
    },
    /// Ortayı doldurma (FIM): önek ile sonek arasına kod üretir.
    Fim {
        #[command(flatten)]
        model: ModelArgs,
        #[arg(long)]
        prefix: String,
        #[arg(long, default_value = "")]
        suffix: String,
        #[command(flatten)]
        sampling: SamplingArgs,
    },
    /// Etkileşimli tamamlama: kodu yazın, boş satırla gönderin.
    Repl {
        #[command(flatten)]
        model: ModelArgs,
        #[command(flatten)]
        sampling: SamplingArgs,
    },
    /// Dosyalar üzerinde perplexity ölçer.
    Eval {
        #[command(flatten)]
        model: ModelArgs,
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
    },
    /// Yapılandırmanın parametre sayısı ve VRAM tahmini.
    Info {
        #[arg(long, short, required = true, num_args = 1..)]
        config: Vec<PathBuf>,
    },
}

#[derive(Args)]
struct ModelArgs {
    /// Eğitim çıktı klasörü (ör. runs/sosy-flash-1.0).
    #[arg(long, short)]
    run: PathBuf,
    /// Belirli bir ağırlık dosyası (varsayılan: best.bpk ya da son checkpoint).
    #[arg(long)]
    weights: Option<PathBuf>,
    #[arg(long, default_value = "auto")]
    backend: String,
    /// Dil ipucu olarak dosya adı (ör. main.rs, app.py).
    #[arg(long)]
    file: Option<String>,
}

#[derive(Args)]
struct SamplingArgs {
    #[arg(long, default_value_t = 256)]
    max_tokens: usize,
    #[arg(long, default_value_t = 0.2)]
    temperature: f32,
    #[arg(long, default_value_t = 40)]
    top_k: usize,
    #[arg(long, default_value_t = 0.95)]
    top_p: f32,
    #[arg(long, default_value_t = 1.1)]
    repetition_penalty: f32,
    /// Durdurma dizileri (birden çok verilebilir).
    #[arg(long)]
    stop: Vec<String>,
    #[arg(long, default_value_t = 42)]
    seed: u64,
}

impl SamplingArgs {
    fn params(&self) -> SamplingParams {
        SamplingParams {
            temperature: self.temperature,
            top_k: self.top_k,
            top_p: self.top_p,
            repetition_penalty: self.repetition_penalty,
            max_new_tokens: self.max_tokens,
            stop: self.stop.clone(),
            seed: self.seed,
            ..Default::default()
        }
    }
}

fn load_generator(m: &ModelArgs) -> Result<Generator> {
    let t = Instant::now();
    let g = Generator::load(&m.run, m.weights.as_deref(), &m.backend)?;
    eprintln!(
        "✓ {} yüklendi ({:.1}s)",
        g.config.name,
        t.elapsed().as_secs_f32()
    );
    Ok(g)
}

fn print_stream(s: &str) {
    print!("{s}");
    std::io::stdout().flush().ok();
}

fn main() -> Result<()> {
    match Cli::parse().cmd {
        Cmd::TokenizerTrain {
            input,
            vocab_size,
            max_mb,
            out,
        } => {
            let mut trainer = BpeTrainer::new();
            let docs = sosis_data::feed_tokenizer_trainer(
                &mut trainer,
                &input,
                max_mb * 1024 * 1024,
                &CollectOptions::default(),
                &FilterOptions::default(),
                1337,
            )?;
            eprintln!(
                "{docs} belge, {:.1} MB okundu; BPE eğitiliyor...",
                trainer.bytes_seen as f64 / 1048576.0
            );
            let tok = trainer.train(vocab_size, |done, total| {
                eprint!("\r  birleştirme {done}/{total}")
            });
            eprintln!();
            tok.save(&out)?;
            eprintln!(
                "✓ tokenizer: {} (sözlük {})",
                out.display(),
                tok.vocab_size()
            );
        }
        Cmd::Prepare {
            input,
            tokenizer,
            out,
            fim_rate,
            val_percent,
            threads,
            ext,
        } => {
            let tok = Tokenizer::load(&tokenizer)?;
            let mut collect = CollectOptions::default();
            if !ext.is_empty() {
                collect.extensions = ext;
            }
            let opts = PrepareOptions {
                inputs: input,
                out_dir: out.clone(),
                fim_rate,
                val_percent,
                threads: threads.unwrap_or_else(|| {
                    std::thread::available_parallelism()
                        .map(|n| n.get())
                        .unwrap_or(4)
                }),
                seed: 1337,
                collect,
                filter: FilterOptions::default(),
            };
            let t = Instant::now();
            let meta = sosis_data::prepare(&tok, &opts, |docs, toks| {
                eprint!("\r  {docs} belge, {:.1}M token", toks as f64 / 1e6)
            })?;
            eprintln!();
            eprintln!(
                "✓ {} — eğitim {:.2}M, doğrulama {:.2}M token | {} belge ({} FIM), {} kopya, {} elendi | {:.0}s",
                out.display(),
                meta.train_tokens as f64 / 1e6,
                meta.val_tokens as f64 / 1e6,
                meta.docs,
                meta.fim_docs,
                meta.duplicates,
                meta.rejected,
                t.elapsed().as_secs_f32()
            );
        }
        Cmd::Train {
            config,
            resume,
            backend,
            max_steps,
        } => {
            let mut cfg = RunConfig::load(&config)?;
            if let Some(b) = backend {
                cfg.train.backend = b;
            }
            if let Some(s) = max_steps {
                cfg.train.max_steps = s;
            }
            let (_, _, total) = cfg.vram_estimate_gb();
            eprintln!(
                "🌭 {} | {:.1}M parametre | adım başına {} token | ~{:.1} GB VRAM (kaba tahmin) | backend {}",
                cfg.model.name,
                cfg.model.param_count() as f64 / 1e6,
                cfg.tokens_per_step(),
                total,
                cfg.train.backend
            );
            let out = sosis_train::train(&cfg, resume, |r| {
                eprintln!(
                    "adım {:>6}/{} | kayıp {:.4} | lr {:.2e} | {:.0} token/s",
                    r.step, cfg.train.max_steps, r.loss, r.lr, r.tokens_per_sec
                );
            })?;
            eprintln!(
                "✓ eğitim bitti: {} adım, son kayıp {:.4}, en iyi doğrulama {:.4} → {}",
                out.final_step,
                out.last_loss,
                out.best_val_loss,
                cfg.train.out_dir.display()
            );
        }
        Cmd::Generate {
            model,
            prompt,
            prompt_file,
            sampling,
        } => {
            let prompt = match (prompt, prompt_file) {
                (Some(p), _) => p,
                (None, Some(f)) => std::fs::read_to_string(&f)
                    .with_context(|| format!("okunamadı: {}", f.display()))?,
                (None, None) => anyhow::bail!("--prompt veya --prompt-file verin"),
            };
            let g = load_generator(&model)?;
            print!("{prompt}");
            let t = Instant::now();
            let mut n = 0;
            g.complete(&prompt, model.file.as_deref(), &sampling.params(), |s| {
                n += 1;
                print_stream(s)
            });
            println!();
            eprintln!("— {n} parça, {:.1}s", t.elapsed().as_secs_f32());
        }
        Cmd::Fim {
            model,
            prefix,
            suffix,
            sampling,
        } => {
            let g = load_generator(&model)?;
            let mid = g.fill_in_middle(
                &prefix,
                &suffix,
                model.file.as_deref(),
                &sampling.params(),
                |_| {},
            );
            println!("{prefix}\x1b[32m{mid}\x1b[0m{suffix}");
        }
        Cmd::Repl { model, sampling } => {
            let g = load_generator(&model)?;
            let params = sampling.params();
            eprintln!("Kodunuzu yazın; boş satır gönderir, :q çıkar.");
            let stdin = std::io::stdin();
            let mut buf = String::new();
            loop {
                eprint!("{}", if buf.is_empty() { "sosy> " } else { "....> " });
                let mut line = String::new();
                if stdin.lock().read_line(&mut line)? == 0 || line.trim() == ":q" {
                    break;
                }
                if line.trim().is_empty() && !buf.is_empty() {
                    g.complete(&buf, model.file.as_deref(), &params, print_stream);
                    println!("\n");
                    buf.clear();
                } else {
                    buf.push_str(&line);
                }
            }
        }
        Cmd::Eval { model, input } => {
            let g = load_generator(&model)?;
            let sources = sosis_data::collect::find_sources(&input, &CollectOptions::default())?;
            let mut all = Vec::new();
            for src in &sources {
                sosis_data::collect::read_source(src, |doc| {
                    if let Ok(p) = g.perplexity(&doc.content) {
                        println!("{:>10.2}  {}", p, doc.path);
                        all.push(p.ln());
                    }
                })?;
            }
            if !all.is_empty() {
                let mean = (all.iter().sum::<f32>() / all.len() as f32).exp();
                println!("ortalama perplexity: {mean:.2} ({} dosya)", all.len());
            }
        }
        Cmd::Info { config } => {
            for c in config {
                let cfg = RunConfig::load(&c)?;
                let (states, acts, total) = cfg.vram_estimate_gb();
                println!("{} ({})", cfg.model.name, c.display());
                println!(
                    "  parametre        : {:.1}M",
                    cfg.model.param_count() as f64 / 1e6
                );
                println!(
                    "  katman/d/kafa    : {} / {} / {}",
                    cfg.model.n_layers, cfg.model.d_model, cfg.model.n_heads
                );
                println!("  bağlam           : {}", cfg.model.max_seq_len);
                println!("  adım başına token: {}", cfg.tokens_per_step());
                println!(
                    "  toplam token     : {:.2}B",
                    (cfg.tokens_per_step() * cfg.train.max_steps) as f64 / 1e9
                );
                println!("  VRAM (kaba)      : ~{total:.1} GB  (ağırlık+optimizer {states:.1}, aktivasyon {acts:.1})");
            }
        }
    }
    Ok(())
}
