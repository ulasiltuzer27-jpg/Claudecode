//! `sosis` — Sosis AI komut satırı aracı.
//!
//! Tipik akış:
//! ```text
//! sosis tokenizer-train --input ~/kodlarim --out tokenizer.json
//! sosis prepare        --input ~/kodlarim --tokenizer tokenizer.json --out data
//! sosis train          --config configs/sosy-flash-1.0.toml
//! sosis generate       --run runs/sosy-flash-1.0 --file main.rs --prompt "fn fibonacci("
//! ```

mod dashboard;
mod download;
mod watch;
mod web;

use std::io::{BufRead, Write};
use std::path::PathBuf;
use std::time::Instant;

use anyhow::{Context, Result};
use clap::{Args, Parser, Subcommand};
use sosis_data::{CollectOptions, FilterOptions, PrepareOptions};
use sosis_infer::chat::{reply, ChatSession};
use sosis_infer::rag::CodeIndex;
use sosis_infer::repair::solve;
use sosis_infer::{Generator, LoadOptions, SamplingParams};
use sosis_model::BurnModule as _;
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
        /// Yakın kopyaları MinHash ile ele (eşik: tahmini Jaccard benzerliği).
        #[arg(long, num_args = 0..=1, default_missing_value = "0.8")]
        near_dedup: Option<f64>,
        /// Bu klasördeki değerlendirme görevlerini içeren belgeleri at (ör. benchmarks).
        #[arg(long)]
        decontaminate: Option<PathBuf>,
    },
    /// Koddaki belgelenmiş fonksiyonlardan (///, <summary>, docstring) soru→kod örnekleri çıkarır.
    SftData {
        #[arg(long, short, required = true, num_args = 1..)]
        input: Vec<PathBuf>,
        #[arg(long, short, default_value = "sft.jsonl")]
        out: PathBuf,
        /// Planlama örneklerini (plan → onay → kod) üretme.
        #[arg(long)]
        no_plans: bool,
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
        /// Damıtma: bu öğretmen modelden de öğren (ör. runs/sosy-pro-1.0). Aynı tokenizer şart.
        #[arg(long)]
        teacher: Option<PathBuf>,
        /// Çıktı klasörünü geçersiz kıl.
        #[arg(long)]
        out: Option<PathBuf>,
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
    /// LoRA adaptörünü tabana kalıcı olarak katar (tek başına çalışan model).
    Merge {
        #[arg(long, short)]
        run: PathBuf,
        #[arg(long, short)]
        out: PathBuf,
    },
    /// Ağırlıkları int8'e nicemler (çıkarım için ~4 kat küçük, daha az VRAM).
    Quantize {
        #[arg(long, short)]
        run: PathBuf,
        #[arg(long, short)]
        out: PathBuf,
    },
    /// Görevi çöz: model kod yazar, kod derlenip/çalıştırılır, hata varsa düzeltir (sohbet modeli).
    Solve {
        #[command(flatten)]
        model: ModelArgs,
        #[command(flatten)]
        sampling: SamplingArgs,
        /// Görev açıklaması.
        task: String,
        /// rust | python
        #[arg(long, default_value = "rust")]
        lang: String,
        /// Koda eklenecek test dosyası (Rust: `fn main` içinde assert'ler; Python: assert'ler).
        #[arg(long)]
        tests: Option<PathBuf>,
        #[arg(long, default_value_t = 3)]
        rounds: usize,
        #[arg(long, default_value = "sessions")]
        sessions: PathBuf,
    },
    /// DPO tercih çiftleri üretir: model her görev için N cevap yazar; testi geçen = tercih edilen,
    /// geçmeyen = reddedilen. Görevleri benchmarks/'tan ayrı tutun (ör. tasks/).
    DpoData {
        #[command(flatten)]
        model: ModelArgs,
        /// Test içeren görev klasörü (benchmarks biçimi).
        #[arg(long, default_value = "tasks")]
        tasks: PathBuf,
        #[arg(long, default_value_t = 6)]
        samples: usize,
        #[arg(long, default_value_t = 0.9)]
        temperature: f32,
        #[arg(long, default_value_t = 384)]
        max_tokens: usize,
        /// Görev başına en fazla çift.
        #[arg(long, default_value_t = 2)]
        pairs_per_task: usize,
        #[arg(long, short, default_value = "dpo.jsonl")]
        out: PathBuf,
    },
    /// Hugging Face'ten açık veri seti dosyalarını indirir (bir kerelik; yarım kalırsa kaldığı yerden).
    Download {
        /// Veri seti, ör. codeparrot/github-code-clean
        #[arg(long)]
        dataset: String,
        /// Dosya deseni (birden çok verilebilir), ör. "data/*.parquet"
        #[arg(long)]
        include: Vec<String>,
        /// Yalnızca bu alt klasörü listele.
        #[arg(long, default_value = "")]
        dir: String,
        #[arg(long, default_value = "main")]
        revision: String,
        #[arg(long, short, default_value = "indirilenler")]
        out: PathBuf,
        /// En fazla indirilecek toplam boyut (GB).
        #[arg(long, default_value_t = 20.0)]
        max_gb: f64,
        #[arg(long, default_value = download::DEFAULT_ENDPOINT)]
        endpoint: String,
    },
    /// Eğitimi gözet: çökerse --resume ile yeniden başlat; bellek yetmezse batch'i küçült.
    Watch {
        #[arg(long, short)]
        config: PathBuf,
        #[arg(long, default_value_t = 20)]
        max_restarts: usize,
        /// Bellek yetmezliğinde micro_batch'i yarıya indir, grad_accum'u ikiye katla.
        #[arg(long)]
        auto_oom: bool,
        /// Yeniden başlatmadan önce bekleme (sn).
        #[arg(long, default_value_t = 30)]
        delay: u64,
        /// train'e aktarılacak ek seçenekler (ör. --backend cuda).
        #[arg(last = true)]
        extra: Vec<String>,
    },
    /// Eğitimi canlı izleme paneli (kayıp, hız, VRAM/RAM, kalan süre).
    Dashboard {
        #[arg(long, short)]
        run: PathBuf,
        #[arg(long, default_value_t = 8081)]
        port: u16,
        /// Yerel ağdan (telefon/tablet) erişime aç; rastgele erişim anahtarı ister.
        #[arg(long)]
        lan: bool,
    },
    /// Tek dosyalık model paketi (.sosy): config + tokenizer + ağırlıklar + model kartı.
    /// LoRA adaptörü tabana katılır. `-r model.sosy` ile doğrudan kullanılır.
    Pack {
        #[arg(long, short)]
        run: PathBuf,
        #[arg(long, short)]
        out: PathBuf,
        /// Ağırlıkları int8'e nicemle (~4 kat küçük).
        #[arg(long)]
        int8: bool,
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
        /// Onarım modu (sohbet modeli): hata mesajıyla en fazla N tur düzeltme; 1. tur ile karşılaştırılır.
        #[arg(long, default_value_t = 0)]
        repair: usize,
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
    /// Spekülatif kod çözme: aynı tokenizer'lı küçük taslak model (ör. runs/sosy-flash-1.0).
    #[arg(long)]
    draft: Option<PathBuf>,
    /// Tur başına taslak token sayısı.
    #[arg(long, default_value_t = 4)]
    draft_k: usize,
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
    let mut g = Generator::load(&m.run, &m.load_options())?;
    if let Some(d) = &m.draft {
        let draft = Generator::load(
            d,
            &LoadOptions {
                weights: None,
                backend: m.backend.clone(),
                context: Some(g.context_len()),
            },
        )?;
        g = g.with_draft(draft, m.draft_k)?;
    }
    eprintln!(
        "✓ {} yüklendi ({:.1}s, bağlam {} token{})",
        g.config.name,
        t.elapsed().as_secs_f32(),
        g.context_len(),
        g.draft_name()
            .map(|d| format!(", taslak: {d}, k={}", m.draft_k))
            .unwrap_or_default()
    );
    Ok(g)
}

/// Eğitim sırasında checkpoint'lerde otomatik bench; sonucu `bench.csv`'ye yazar, pass@1 döndürür.
fn auto_bench(
    cfg: &RunConfig,
    suites: Option<&[sosis_bench::Suite]>,
    info: &sosis_train::CheckpointInfo,
) -> Option<f64> {
    let t = &cfg.train;
    let every = t.bench_every?;
    let suites = suites?;
    if !info.step.is_multiple_of(every.max(1)) && info.step != info.max_steps {
        return None;
    }
    let started = Instant::now();
    let g = Generator::load(
        &info.out_dir,
        &LoadOptions {
            weights: Some(info.dir.join("model.bpk")),
            backend: t.bench_backend.clone(),
            context: None,
        },
    )
    .map_err(|e| eprintln!("  bench: model yüklenemedi: {e}"))
    .ok()?;
    let temperature = if t.bench_samples > 1 { 0.8 } else { 0.0 };
    let report = sosis_infer::evalrun::bench_completion(
        &g,
        suites,
        t.bench_samples,
        temperature,
        t.bench_max_tokens,
        std::time::Duration::from_secs(10),
        |_| {},
    )
    .map_err(|e| eprintln!("  bench hatası: {e}"))
    .ok()?;
    use sosis_bench::Lang;
    let (all, rust, py) = (
        report.pass_at(1, None),
        report.pass_at(1, Some(Lang::Rust)),
        report.pass_at(1, Some(Lang::Python)),
    );
    let path = info.out_dir.join("bench.csv");
    let new = !path.exists();
    if let Ok(mut f) = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(&path)
    {
        if new {
            let _ = writeln!(f, "step,pass1,rust,python");
        }
        let _ = writeln!(f, "{},{all:.4},{rust:.4},{py:.4}", info.step);
    }
    eprintln!(
        "  bench (adım {}): pass@1 %{:.1} (rust %{:.1}, python %{:.1}) — {:.0}s",
        info.step,
        100.0 * all,
        100.0 * rust,
        100.0 * py,
        started.elapsed().as_secs_f32()
    );
    Some(all)
}

fn parse_lang(l: &str) -> Result<sosis_bench::Lang> {
    Ok(match l {
        "rust" | "rs" => sosis_bench::Lang::Rust,
        "python" | "py" => sosis_bench::Lang::Python,
        other => anyhow::bail!("bilinmeyen dil: {other} (rust | python)"),
    })
}

fn outcome_text(o: &sosis_bench::Outcome) -> String {
    match o {
        sosis_bench::Outcome::Pass => "✓ çalıştı".into(),
        sosis_bench::Outcome::CompileError(e) => {
            format!("✗ derleme hatası: {}", e.lines().next().unwrap_or(""))
        }
        sosis_bench::Outcome::Fail(e) => format!(
            "✗ test başarısız: {}",
            e.lines()
                .find(|l| l.contains("panicked") || l.contains("Error"))
                .unwrap_or("")
        ),
        sosis_bench::Outcome::Timeout => "✗ zaman aşımı".into(),
    }
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
            near_dedup,
            decontaminate,
        } => {
            let tok = Tokenizer::load(&tokenizer)?;
            let mut contam_texts = Vec::new();
            if let Some(dir) = &decontaminate {
                for suite in sosis_bench::load_suites(dir)? {
                    for t in suite.tasks {
                        contam_texts.push(format!("{}{}", t.prompt, t.solution));
                        contam_texts.push(t.tests);
                    }
                }
                eprintln!("sızıntı temizliği: {} metin", contam_texts.len());
            }
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
                near_dedup,
                decontaminate: contam_texts,
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
            if near_dedup.is_some() || decontaminate.is_some() {
                eprintln!(
                    "  yakın kopya: {} | sızıntı: {}",
                    meta.near_duplicates, meta.contaminated
                );
            }
        }
        Cmd::SftData {
            input,
            out,
            no_plans,
        } => {
            let ex =
                sosis_data::sft::extract_from_code(&input, &CollectOptions::default(), !no_plans)?;
            sosis_data::sft::write_jsonl(&out, &ex)?;
            let plans = ex.iter().filter(|e| e.messages.len() == 4).count();
            eprintln!(
                "✓ {} örnek ({} planlama örneği) → {}",
                ex.len(),
                plans,
                out.display()
            );
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
            teacher,
            out,
        } => {
            let mut cfg = RunConfig::load(&config)?;
            if let Some(t) = teacher {
                cfg.distill = Some(sosis_train::DistillConfig {
                    teacher: t,
                    alpha: cfg.distill.as_ref().map(|d| d.alpha).unwrap_or(0.5),
                    temperature: cfg.distill.as_ref().map(|d| d.temperature).unwrap_or(2.0),
                });
            }
            if let Some(o) = out {
                cfg.train.out_dir = o;
            }
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
            let bench_suites = match cfg.train.bench_every {
                Some(_) => Some(sosis_bench::load_suites(&cfg.train.bench_suite)?),
                None => None,
            };
            let out = sosis_train::train_with_hooks(
                &cfg,
                resume,
                |r| {
                    let vram = r
                        .vram_gb
                        .map(|g| format!(" | VRAM {g:.2} GB"))
                        .unwrap_or_default();
                    eprintln!(
                    "adım {:>6}/{} | kayıp {:.4} | lr {:.2e} | {:.0} token/s{vram} | RAM {:.1} GB",
                    r.step, cfg.train.max_steps, r.loss, r.lr, r.tokens_per_sec, r.ram_gb
                );
                },
                |info| auto_bench(&cfg, bench_suites.as_deref(), info),
            )?;
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
            let spec = g
                .last_spec
                .lock()
                .unwrap()
                .map(|st| {
                    format!(
                        ", taslak kabul %{:.0} ({} hedef geçişi)",
                        100.0 * st.acceptance(),
                        st.target_passes
                    )
                })
                .unwrap_or_default();
            eprintln!("— {n} parça, {:.1}s{spec}", t.elapsed().as_secs_f32());
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
                "Sohbet ({}). Komutlar: /yeni  /liste  /yükle <id>  /plan <görev>  /çöz [rust|python] <görev>  /çık   — oturum: {}",
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
                    ("/plan", task) if !task.is_empty() => {
                        print!("sosy> ");
                        let plan = sosis_infer::plan::make_plan(
                            &g,
                            &mut s,
                            task,
                            index.as_ref(),
                            &params,
                            &mut print_stream,
                        );
                        println!();
                        s.save(&sessions)?;
                        eprint!(
                            "Plan ({} adım). [o]nayla / [d]üzenle / [r]eddet: ",
                            sosis_infer::plan::step_count(&plan)
                        );
                        let mut ans = String::new();
                        stdin.lock().read_line(&mut ans)?;
                        let approved = match ans.trim().chars().next() {
                            Some('o' | 'e' | 'y') => Some(plan),
                            Some('d') => {
                                eprintln!("Yeni planı yazın (boş satırla bitirin):");
                                let mut edited = String::new();
                                loop {
                                    let mut l = String::new();
                                    if stdin.lock().read_line(&mut l)? == 0 || l.trim().is_empty() {
                                        break;
                                    }
                                    edited.push_str(&l);
                                }
                                Some(edited)
                            }
                            _ => None,
                        };
                        match approved {
                            Some(p) => {
                                print!("sosy> ");
                                sosis_infer::plan::execute_plan(
                                    &g,
                                    &mut s,
                                    &p,
                                    index.as_ref(),
                                    &params,
                                    &mut print_stream,
                                );
                                println!();
                                s.save(&sessions)?;
                            }
                            None => {
                                eprintln!("Plan reddedildi; yeni bir istekle devam edebilirsiniz.")
                            }
                        }
                        continue;
                    }
                    ("/çöz" | "/coz" | "/solve", rest) => {
                        let (lang, task) = match rest.split_once(' ') {
                            Some((l, t)) if parse_lang(l).is_ok() => (parse_lang(l)?, t),
                            _ => (sosis_bench::Lang::Rust, rest),
                        };
                        print!("sosy> ");
                        let attempts = solve(
                            &g,
                            &mut s,
                            task,
                            lang,
                            None,
                            3,
                            index.as_ref(),
                            &params,
                            &mut print_stream,
                            |a| eprintln!("\n— tur {}: {}", a.round, outcome_text(&a.outcome)),
                        )?;
                        s.save(&sessions)?;
                        if !attempts.iter().any(|a| a.passed()) {
                            eprintln!("3 turda çalışan kod üretilemedi.");
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
        Cmd::Merge { run, out } => {
            let mut cfg = RunConfig::load(run.join(sosis_train::CONFIG_FILE))?;
            if cfg.train.lora.is_none() {
                anyhow::bail!("bu bir LoRA eğitimi değil");
            }
            let weights = sosis_train::find_weights(&run).context("adaptör bulunamadı")?;
            let device = sosis_model::select_device("cpu", sosis_model::Precision::F32)?;
            let merged = sosis_infer::load_model(&cfg, &weights, &device)?;
            std::fs::create_dir_all(&out)?;
            merged
                .save_file(out.join(sosis_train::BEST_FILE))
                .map_err(|e| anyhow::anyhow!("{e:?}"))?;
            cfg.train.lora = None;
            cfg.train.init_from = None;
            std::fs::write(out.join(sosis_train::CONFIG_FILE), cfg.to_toml()?)?;
            std::fs::copy(
                run.join(sosis_train::TOKENIZER_FILE),
                out.join(sosis_train::TOKENIZER_FILE),
            )?;
            eprintln!("✓ {} + taban → {}", weights.display(), out.display());
        }
        Cmd::Quantize { run, out } => {
            let mut cfg = RunConfig::load(run.join(sosis_train::CONFIG_FILE))?;
            if cfg.inference.quantized.is_some() {
                anyhow::bail!("bu model zaten nicemli");
            }
            let weights = sosis_train::find_weights(&run).context("ağırlık bulunamadı")?;
            let device = sosis_model::select_device("cpu", sosis_model::Precision::F32)?;
            let model = cfg.model.init(&device).try_load_file(&weights)?;
            let q = sosis_model::quant::quantize_int8(model, &device);
            std::fs::create_dir_all(&out)?;
            q.save_file(out.join(sosis_train::BEST_FILE))
                .map_err(|e| anyhow::anyhow!("{e:?}"))?;
            cfg.inference.quantized = Some("int8".into());
            cfg.model.name = format!("{} (int8)", cfg.model.name);
            std::fs::write(out.join(sosis_train::CONFIG_FILE), cfg.to_toml()?)?;
            std::fs::copy(
                run.join(sosis_train::TOKENIZER_FILE),
                out.join(sosis_train::TOKENIZER_FILE),
            )?;
            let (a, b) = (
                std::fs::metadata(&weights)?.len(),
                std::fs::metadata(out.join(sosis_train::BEST_FILE))?.len(),
            );
            eprintln!(
                "✓ {} → {} ({:.1} MB → {:.1} MB)",
                weights.display(),
                out.display(),
                a as f64 / 1048576.0,
                b as f64 / 1048576.0
            );
        }
        Cmd::Solve {
            model,
            sampling,
            task,
            lang,
            tests,
            rounds,
            sessions,
        } => {
            let lang = parse_lang(&lang)?;
            let tests = tests.map(std::fs::read_to_string).transpose()?;
            let g = load_generator(&model)?;
            let index = model.load_index()?;
            let mut s = ChatSession::new(None);
            let attempts = solve(
                &g,
                &mut s,
                &task,
                lang,
                tests.as_deref(),
                rounds,
                index.as_ref(),
                &sampling.params(),
                &mut print_stream,
                |a| eprintln!("\n— tur {}: {}", a.round, outcome_text(&a.outcome)),
            )?;
            s.save(&sessions)?;
            match attempts.iter().find(|a| a.passed()) {
                Some(a) => eprintln!("✓ {}. turda çalıştı (oturum {})", a.round, s.id),
                None => eprintln!("✗ {} turda çözülemedi (oturum {})", attempts.len(), s.id),
            }
        }
        Cmd::DpoData {
            model,
            tasks,
            samples,
            temperature,
            max_tokens,
            pairs_per_task,
            out,
        } => {
            use sosis_infer::repair::{build_program, extract_code};
            use sosis_tokenizer::chat::{Message, Role};
            if tasks.canonicalize().ok() == PathBuf::from("benchmarks").canonicalize().ok() {
                eprintln!(
                    "⚠ benchmarks/ ile tercih verisi üretmek, bench sonuçlarını anlamsız kılar."
                );
            }
            let g = load_generator(&model)?;
            let suites = sosis_bench::load_suites(&tasks)?;
            let mut pairs = Vec::new();
            for suite in &suites {
                let fence = match suite.lang {
                    sosis_bench::Lang::Rust => "rust",
                    sosis_bench::Lang::Python => "python",
                };
                for task in &suite.tasks {
                    let ask = format!(
                        "Şu fonksiyonu tamamla. Fonksiyonun tamamını tek bir kod bloğunda yaz.\n```{fence}\n{}```",
                        task.prompt
                    );
                    let (mut good, mut bad) = (Vec::new(), Vec::new());
                    for i in 0..samples {
                        let params = SamplingParams {
                            temperature,
                            max_new_tokens: max_tokens,
                            seed: 7_000 + i as u64,
                            ..Default::default()
                        };
                        let mut s = ChatSession::new(None);
                        let answer = reply(&g, &mut s, &ask, None, &params, &mut |_| {});
                        let code = extract_code(&answer, suite.lang);
                        let outcome = sosis_bench::run_program(
                            suite.lang,
                            &build_program(&code, Some(&task.tests), suite.lang),
                            std::time::Duration::from_secs(10),
                        )?;
                        if outcome.passed() {
                            good.push(answer)
                        } else {
                            bad.push(answer)
                        }
                    }
                    let n = good.len().min(bad.len()).min(pairs_per_task);
                    println!(
                        "{:?}/{:<18} geçen {}/{}  → {} çift",
                        suite.lang,
                        task.id,
                        good.len(),
                        samples,
                        n
                    );
                    for (c, r) in good.into_iter().zip(bad).take(n) {
                        pairs.push(sosis_data::sft::DpoPair {
                            context: vec![Message::new(Role::User, ask.clone())],
                            chosen: c,
                            rejected: r,
                        });
                    }
                }
            }
            sosis_data::sft::write_dpo_jsonl(&out, &pairs)?;
            eprintln!("✓ {} çift → {}", pairs.len(), out.display());
            if pairs.is_empty() {
                eprintln!("  Hiç çift çıkmadı: model ya hep geçiyor ya hep kalıyor. --samples veya --temperature'ı artırın.");
            }
        }
        Cmd::Dashboard { run, port, lan } => dashboard::serve(run, port, lan)?,
        Cmd::Download {
            dataset,
            include,
            dir,
            revision,
            out,
            max_gb,
            endpoint,
        } => {
            let paths =
                download::run(&endpoint, &dataset, &revision, &dir, &include, &out, max_gb)?;
            eprintln!(
                "✓ {} dosya → {}\nSonraki adım: sosis prepare -i {} -t tokenizer.json -o data --near-dedup --decontaminate benchmarks",
                paths.len(),
                out.display(),
                out.join(dataset.replace('/', "__")).display()
            );
        }
        Cmd::Watch {
            config,
            max_restarts,
            auto_oom,
            delay,
            extra,
        } => {
            let cfg = RunConfig::load(&config)?;
            let mut log = watch::TeeLog::open(cfg.train.out_dir.join("watch.log"))?;
            let opts = watch::WatchOptions {
                max_restarts,
                auto_oom,
                delay: std::time::Duration::from_secs(delay),
            };
            let n = watch::run_watch(&config, &opts, &mut log, || {
                watch::spawn_train(&config, &extra)
            })?;
            eprintln!("bekçi: {n} yeniden başlatmayla tamamlandı");
        }
        Cmd::Pack { run, out, int8 } => {
            sosis_infer::package::pack(&run, &out, int8)?;
            eprintln!(
                "✓ {} ({:.1} MB) — model kartı içinde: MODEL_CARD.md",
                out.display(),
                std::fs::metadata(&out)?.len() as f64 / 1048576.0
            );
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
            repair,
        } => {
            use sosis_bench::{evaluate, load_suites, Lang};
            if repair > 0 {
                let run = run.context("--repair için --run (sohbet modeli) verin")?;
                let g = Generator::load(
                    &run,
                    &LoadOptions {
                        weights,
                        backend,
                        context: None,
                    },
                )?;
                let mut suites = load_suites(&suite)?;
                if let Some(l) = &lang {
                    let want = parse_lang(l)?;
                    suites.retain(|s| s.lang == want);
                }
                let params = SamplingParams {
                    temperature,
                    max_new_tokens: max_tokens.max(256),
                    ..Default::default()
                };
                let (mut first, mut fixed, mut total) = (0usize, 0usize, 0usize);
                for suite in &suites {
                    let fence = match suite.lang {
                        Lang::Rust => "rust",
                        Lang::Python => "python",
                    };
                    for task in &suite.tasks {
                        total += 1;
                        let ask = format!(
                            "Şu fonksiyonu tamamla. Fonksiyonun tamamını tek bir kod bloğunda yaz.\n```{fence}\n{}```",
                            task.prompt
                        );
                        let mut session = ChatSession::new(None);
                        let mut pending = Some(ask.clone());
                        let attempts = sosis_infer::repair::repair_loop(
                            &ask,
                            suite.lang,
                            Some(&task.tests),
                            repair,
                            std::time::Duration::from_secs(timeout),
                            |history| {
                                let user = pending.take().unwrap_or_else(|| {
                                    history
                                        .last()
                                        .map(|m| m.content.clone())
                                        .unwrap_or_default()
                                });
                                sosis_infer::chat::reply(
                                    &g,
                                    &mut session,
                                    &user,
                                    None,
                                    &params,
                                    &mut |_| {},
                                )
                            },
                            |_| {},
                        )?;
                        let ok1 = attempts.first().is_some_and(|a| a.passed());
                        let okn = attempts.iter().any(|a| a.passed());
                        first += ok1 as usize;
                        fixed += okn as usize;
                        println!(
                            "{} {:?}/{:<18} {} tur",
                            if okn { "✓" } else { "✗" },
                            suite.lang,
                            task.id,
                            attempts.len()
                        );
                    }
                }
                let pct = |n: usize| 100.0 * n as f64 / total.max(1) as f64;
                println!(
                    "\npass@1 (onarımsız) {:.1}%  →  onarımlı ({repair} tura kadar) {:.1}%",
                    pct(first),
                    pct(fixed)
                );
                return Ok(());
            }
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
            let print_result = |r: &sosis_bench::TaskResult| {
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
            };
            let to = std::time::Duration::from_secs(timeout);
            let report = match &generator {
                None => evaluate(
                    &suites,
                    samples,
                    to,
                    |_, task, _| task.solution.clone(),
                    print_result,
                )?,
                Some((g, _)) => sosis_infer::evalrun::bench_completion(
                    g,
                    &suites,
                    samples,
                    temperature,
                    max_tokens,
                    to,
                    print_result,
                )?,
            };
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
