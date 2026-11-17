//! `sosis` — Sosis AI komut satırı aracı.
//!
//! Tipik akış:
//! ```text
//! sosis tokenizer-train --input ~/kodlarim --out tokenizer.json
//! sosis prepare        --input ~/kodlarim --tokenizer tokenizer.json --out data
//! sosis train          --config configs/sosy-flash-1.0.toml
//! sosis generate       --run runs/sosy-flash-1.0 --file main.rs --prompt "fn fibonacci("
//! ```

mod web;

use std::io::{BufRead, Write};
use std::path::PathBuf;
use std::time::Instant;

use anyhow::{Context, Result};
use clap::{Args, Parser, Subcommand};
use sosis_data::{CollectOptions, FilterOptions, PrepareOptions};
use sosis_infer::chat::{reply, ChatSession};
use sosis_infer::rag::CodeIndex;
use sosis_infer::{Generator, LoadOptions, SamplingParams};
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
        #[arg(long, default_value_t = 32768)]
        vocab_size: usize,
        /// Eğitimde kullanılacak en fazla metin (MB).
        #[arg(long, default_value_t = 1000)]
        max_mb: u64,
        /// RAM sınırı (GB); tokenizer istatistikleri bunun ~%35'ine ulaşınca okuma durur.
        #[arg(long, default_value_t = sosis_data::ram::DEFAULT_MAX_RAM_GB)]
        max_ram_gb: f64,
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
    /// Koddaki belgelenmiş fonksiyonlardan (///, <summary>, docstring) soru→kod örnekleri çıkarır.
    SftData {
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, short, default_value = "sft.jsonl")]
        out: PathBuf,
    },
    /// SFT .jsonl dosyalarını maskeli eğitim verisine çevirir (yalnızca cevaplar öğrenilir).
    PrepareSft {
        /// .jsonl dosyaları (`messages` veya `instruction`/`response` biçimi).
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, short, default_value = "tokenizer.json")]
        tokenizer: PathBuf,
        #[arg(long, short, default_value = "data-sft")]
        out: PathBuf,
        #[arg(long, default_value_t = 2.0)]
        val_percent: f64,
        /// Sistem mesajı (varsayılan: Sosy tanıtımı).
        #[arg(long)]
        system: Option<String>,
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
    /// Sohbet (talimat ince ayarı yapılmış model, ör. runs/sosy-pro-1.0-chat). Geçmiş hatırlanır.
    Chat {
        #[command(flatten)]
        model: ModelArgs,
        #[command(flatten)]
        sampling: SamplingArgs,
        /// Oturumların kaydedildiği klasör.
        #[arg(long, default_value = "sessions")]
        sessions: PathBuf,
        /// Kayıtlı bir oturumu sürdür.
        #[arg(long)]
        session: Option<String>,
        /// Sistem mesajı.
        #[arg(long)]
        system: Option<String>,
    },
    /// Yerel web arayüzü: sohbet, tamamlama, FIM (yalnızca bu bilgisayardan erişilir).
    Serve {
        #[command(flatten)]
        model: ModelArgs,
        #[arg(long, default_value = "sessions")]
        sessions: PathBuf,
        #[arg(long, default_value_t = 8080)]
        port: u16,
    },
    /// Proje hafızası için kod tabanını indeksler (BM25).
    Index {
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, short, default_value = ".sosis-index")]
        out: PathBuf,
    },
    /// İndekste arama (hata ayıklama).
    Search {
        #[arg(long, default_value = ".sosis-index")]
        index: PathBuf,
        query: String,
        #[arg(long, short, default_value_t = 5)]
        k: usize,
    },
    /// Dosyalar üzerinde perplexity ölçer.
    Eval {
        #[command(flatten)]
        model: ModelArgs,
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
    },
    /// Derle-çalıştır değerlendirmesi: model fonksiyon yazar, rustc/python ile test edilir (pass@k).
    Bench {
        /// Eğitim klasörü (`--check-solutions` ile gerekmez).
        #[arg(long, short)]
        run: Option<PathBuf>,
        #[arg(long)]
        weights: Option<PathBuf>,
        #[arg(long, default_value = "auto")]
        backend: String,
        /// Görev klasörü (*.toml).
        #[arg(long, default_value = "benchmarks")]
        suite: PathBuf,
        /// Görev başına örnek sayısı (pass@k için; >1 ise --temperature 0.8 önerilir).
        #[arg(long, default_value_t = 1)]
        samples: usize,
        #[arg(long, default_value_t = 0.2)]
        temperature: f32,
        #[arg(long, default_value_t = 256)]
        max_tokens: usize,
        /// Yalnızca bu dil (rust | python).
        #[arg(long)]
        lang: Option<String>,
        /// Model yerine referans çözümleri test et (düzeneğin kendi kontrolü).
        #[arg(long)]
        check_solutions: bool,
        /// Program başına zaman aşımı (saniye).
        #[arg(long, default_value_t = 10)]
        timeout: u64,
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
    /// Bağlam uzunluğu; eğitimdekinden büyükse RoPE ölçeklemeyle uzatılır (ör. 4096).
    #[arg(long)]
    context: Option<usize>,
    /// Proje hafızası: `sosis index` ile oluşturulan indeks (ilgili kod bağlama eklenir).
    #[arg(long)]
    index: Option<PathBuf>,
}

impl ModelArgs {
    fn load_index(&self) -> Result<Option<CodeIndex>> {
        match &self.index {
            Some(p) => {
                let idx = CodeIndex::load(p)?;
                eprintln!("📚 proje hafızası: {} parça", idx.chunks.len());
                Ok(Some(idx))
            }
            None => Ok(None),
        }
    }

    fn load_options(&self) -> LoadOptions {
        LoadOptions {
            weights: self.weights.clone(),
            backend: self.backend.clone(),
            context: self.context,
        }
    }
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
    let g = Generator::load(&m.run, &m.load_options())?;
    eprintln!(
        "✓ {} yüklendi ({:.1}s, bağlam {} token)",
        g.config.name,
        t.elapsed().as_secs_f32(),
        g.context_len()
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
            max_ram_gb,
            out,
        } => {
            let mut trainer = BpeTrainer::new();
            let docs = sosis_data::feed_tokenizer_trainer(
                &mut trainer,
                &input,
                max_mb * 1024 * 1024,
                max_ram_gb,
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
        Cmd::SftData { input, out } => {
            let ex = sosis_data::sft::extract_from_code(&input, &CollectOptions::default())?;
            sosis_data::sft::write_jsonl(&out, &ex)?;
            eprintln!("✓ {} örnek → {}", ex.len(), out.display());
            if let Some(e) = ex.first() {
                eprintln!(
                    "örnek soru: {}",
                    e.messages[0].content.lines().next().unwrap_or("")
                );
            }
        }
        Cmd::PrepareSft {
            input,
            tokenizer,
            out,
            val_percent,
            system,
        } => {
            let tok = Tokenizer::load(&tokenizer)?;
            let mut ex = Vec::new();
            for p in &input {
                let n = ex.len();
                ex.extend(sosis_data::sft::read_jsonl(p)?);
                eprintln!("  {}: {} örnek", p.display(), ex.len() - n);
            }
            let meta =
                sosis_data::sft::prepare_sft(&tok, &ex, &out, val_percent, system.as_deref())?;
            eprintln!(
                "✓ {} — {} örnek, eğitim {:.2}M / doğrulama {:.2}M token",
                out.display(),
                meta.docs,
                meta.train_tokens as f64 / 1e6,
                meta.val_tokens as f64 / 1e6
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
            let mem = cfg.memory_estimate();
            eprintln!(
                "🌭 {} | {:.1}M parametre | adım başına {} token | ~{:.1} GB VRAM, ~{:.1} GB RAM (kaba) | backend {}",
                cfg.model.name,
                cfg.model.param_count() as f64 / 1e6,
                cfg.tokens_per_step(),
                mem.vram_total_gb,
                mem.ram_total_gb,
                cfg.train.backend
            );
            let out = sosis_train::train(&cfg, resume, |r| {
                let vram = r
                    .vram_gb
                    .map(|g| format!(" | VRAM {g:.2} GB"))
                    .unwrap_or_default();
                eprintln!(
                    "adım {:>6}/{} | kayıp {:.4} | lr {:.2e} | {:.0} token/s{vram} | RAM {:.1} GB",
                    r.step, cfg.train.max_steps, r.loss, r.lr, r.tokens_per_sec, r.ram_gb
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
            let index = model.load_index()?;
            print!("{prompt}");
            let t = Instant::now();
            let mut n = 0;
            let mut on = |s: &str| {
                n += 1;
                print_stream(s)
            };
            match &index {
                Some(idx) => {
                    g.complete_with_index(
                        &prompt,
                        model.file.as_deref(),
                        idx,
                        &sampling.params(),
                        &mut on,
                    );
                }
                None => {
                    g.complete(&prompt, model.file.as_deref(), &sampling.params(), on);
                }
            }
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
            let index = model.load_index()?;
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
                    match &index {
                        Some(idx) => {
                            g.complete_with_index(
                                &buf,
                                model.file.as_deref(),
                                idx,
                                &params,
                                &mut print_stream,
                            );
                        }
                        None => {
                            g.complete(&buf, model.file.as_deref(), &params, print_stream);
                        }
                    }
                    println!("\n");
                    buf.clear();
                } else {
                    buf.push_str(&line);
                }
            }
        }
        Cmd::Chat {
            model,
            sampling,
            sessions,
            session,
            system,
        } => {
            let g = load_generator(&model)?;
            let index = model.load_index()?;
            let params = sampling.params();
            let mut s = match &session {
                Some(id) => ChatSession::load(&sessions, id)?,
                None => ChatSession::new(system.as_deref()),
            };
            eprintln!(
                "Sohbet ({}). Komutlar: /yeni  /liste  /yükle <id>  /çık   — oturum: {}",
                g.config.name, s.id
            );
            for m in &s.messages {
                eprintln!("{:?}: {}", m.role, m.content);
            }
            let stdin = std::io::stdin();
            loop {
                eprint!("\nsen> ");
                let mut line = String::new();
                if stdin.lock().read_line(&mut line)? == 0 {
                    break;
                }
                let line = line.trim();
                match line
                    .split_once(' ')
                    .map(|(a, b)| (a, b.trim()))
                    .unwrap_or((line, ""))
                {
                    ("", _) => continue,
                    ("/çık" | "/cik" | "/q", _) => break,
                    ("/yeni" | "/new", _) => {
                        s = ChatSession::new(system.as_deref());
                        eprintln!("yeni oturum: {}", s.id);
                        continue;
                    }
                    ("/liste" | "/list", _) => {
                        for x in ChatSession::list(&sessions) {
                            eprintln!("  {}  {} ({} mesaj)", x.id, x.title, x.messages.len());
                        }
                        continue;
                    }
                    ("/yükle" | "/yukle" | "/load", id) => {
                        match ChatSession::load(&sessions, id) {
                            Ok(x) => {
                                eprintln!("yüklendi: {} ({} mesaj)", x.title, x.messages.len());
                                s = x;
                            }
                            Err(e) => eprintln!("hata: {e}"),
                        }
                        continue;
                    }
                    _ => {}
                }
                print!("sosy> ");
                reply(&g, &mut s, line, index.as_ref(), &params, &mut print_stream);
                println!();
                s.save(&sessions)?;
            }
        }
        Cmd::Serve {
            model,
            sessions,
            port,
        } => {
            let g = load_generator(&model)?;
            let index = model.load_index()?;
            web::serve(g, index, sessions, port)?;
        }
        Cmd::Index { input, out } => {
            let t = Instant::now();
            let idx = CodeIndex::build(&input)?;
            idx.save(&out)?;
            eprintln!(
                "✓ {} parça indekslendi → {} ({:.1}s)",
                idx.chunks.len(),
                out.display(),
                t.elapsed().as_secs_f32()
            );
        }
        Cmd::Search { index, query, k } => {
            let idx = CodeIndex::load(&index)?;
            for (score, c) in idx.search(&query, k) {
                println!("{score:6.2}  {}:{}", c.path, c.line);
                for l in c.text.lines().take(4) {
                    println!("        {l}");
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
        Cmd::Bench {
            run,
            weights,
            backend,
            suite,
            samples,
            temperature,
            max_tokens,
            lang,
            check_solutions,
            timeout,
        } => {
            use sosis_bench::{evaluate, function_end, load_suites, Lang};
            let mut suites = load_suites(&suite)?;
            if let Some(l) = lang {
                let want = match l.as_str() {
                    "rust" | "rs" => Lang::Rust,
                    "python" | "py" => Lang::Python,
                    other => anyhow::bail!("bilinmeyen dil: {other}"),
                };
                suites.retain(|s| s.lang == want);
            }
            let n_tasks: usize = suites.iter().map(|s| s.tasks.len()).sum();
            let generator = if check_solutions {
                eprintln!("referans çözümler test ediliyor ({n_tasks} görev)");
                None
            } else {
                let run = run.context("--run verin (veya --check-solutions)")?;
                let g = Generator::load(
                    &run,
                    &LoadOptions {
                        weights,
                        backend,
                        context: None,
                    },
                )?;
                eprintln!("✓ {} — {n_tasks} görev x {samples} örnek", g.config.name);
                Some((g, run))
            };
            let t0 = Instant::now();
            let report = evaluate(
                &suites,
                samples,
                std::time::Duration::from_secs(timeout),
                |suite, task, sample| match &generator {
                    None => task.solution.clone(),
                    Some((g, _)) => {
                        let params = SamplingParams {
                            temperature,
                            max_new_tokens: max_tokens,
                            seed: 1000 + sample as u64,
                            ..Default::default()
                        };
                        g.complete_until(&task.prompt, Some(&suite.file), &params, &mut |out| {
                            function_end(suite.lang, &task.prompt, out)
                        })
                    }
                },
                |r| {
                    let mark = if r.passed == r.samples {
                        "✓"
                    } else if r.passed > 0 {
                        "~"
                    } else {
                        "✗"
                    };
                    let err = r.first_error.as_deref().unwrap_or("");
                    println!(
                        "{mark} {:?}/{:<18} {}/{}  {err}",
                        r.suite, r.id, r.passed, r.samples
                    );
                },
            )?;
            let mut summary = String::new();
            for (name, l) in [
                ("toplam", None),
                ("rust", Some(Lang::Rust)),
                ("python", Some(Lang::Python)),
            ] {
                if !report
                    .results
                    .iter()
                    .any(|r| l.is_none_or(|l| r.suite == l))
                {
                    continue;
                }
                let mut line = format!("{name:>7}: pass@1 {:5.1}%", 100.0 * report.pass_at(1, l));
                if samples >= 5 {
                    line += &format!("  pass@5 {:5.1}%", 100.0 * report.pass_at(5, l));
                }
                if samples > 1 && samples != 5 {
                    line += &format!(
                        "  pass@{samples} {:5.1}%",
                        100.0 * report.pass_at(samples, l)
                    );
                }
                summary.push_str(&line);
                summary.push('\n');
            }
            println!("\n{summary}— {:.0}s", t0.elapsed().as_secs_f32());
            if let Some((_, run)) = &generator {
                let path = run.join("bench.txt");
                std::fs::write(&path, &summary)?;
                eprintln!("sonuç kaydedildi: {}", path.display());
            }
        }
        Cmd::Info { config } => {
            for c in config {
                let cfg = RunConfig::load(&c)?;
                let mem = cfg.memory_estimate();
                let t = &cfg.train;
                println!("{} ({})", cfg.model.name, c.display());
                println!(
                    "  parametre        : {:.1}M",
                    cfg.model.param_count() as f64 / 1e6
                );
                println!(
                    "  katman/d/kafa/kv : {} / {} / {} / {}",
                    cfg.model.n_layers,
                    cfg.model.d_model,
                    cfg.model.n_heads,
                    cfg.model.kv_heads()
                );
                println!(
                    "  bağlam           : eğitim {} (seq_len {}), çıkarım {}",
                    cfg.model.max_seq_len,
                    t.seq_len,
                    cfg.inference.context.unwrap_or(cfg.model.context_len())
                );
                println!(
                    "  hassasiyet       : {:?}{}{}",
                    t.precision,
                    if t.optimizer_offload {
                        ", optimizer RAM'de"
                    } else {
                        ""
                    },
                    if t.gradient_checkpointing {
                        ", gradient checkpointing"
                    } else {
                        ""
                    }
                );
                println!("  adım başına token: {}", cfg.tokens_per_step());
                println!(
                    "  toplam token     : {:.2}B",
                    (cfg.tokens_per_step() * cfg.train.max_steps) as f64 / 1e9
                );
                println!(
                    "  VRAM (kaba)      : ~{:.1} GB  (ağırlık+gradyan+optimizer {:.1}, aktivasyon+logit {:.1})",
                    mem.vram_total_gb, mem.vram_states_gb, mem.vram_activations_gb
                );
                println!(
                    "  RAM (kaba)       : ~{:.1} GB  (sınır {:.0} GB)",
                    mem.ram_total_gb, t.max_ram_gb
                );
                if cfg.distill.is_some() {
                    println!("  damıtma          : öğretmen modeli ayrıca VRAM kullanır");
                }
            }
        }
    }
    Ok(())
}
