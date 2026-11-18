//! # sosis-train
//!
//! Elle yazılmış eğitim döngüsü: AdamW, ısınma + kosinüs LR, gradyan biriktirme,
//! periyodik doğrulama, checkpoint ve kaldığı yerden devam. İsteğe bağlı:
//! - **optimizer offload**: f32 ana ağırlıklar + AdamW RAM'de, GPU'da bf16 kopya ([`offload`])
//! - **damıtma**: öğretmen modelin olasılıklarından da öğrenme (`[distill]` bölümü)
//! - **init_from**: başka bir eğitimin ağırlıklarıyla başlama (bağlam genişletme, SFT)
//!
//! Çıktı klasörü düzeni (`train.out_dir`):
//! ```text
//! config.toml            kullanılan yapılandırma (çıkarım için)
//! tokenizer.json         tokenizer kopyası (klasör kendi başına yeterli olsun diye)
//! best.bpk               en düşük doğrulama kaybına sahip ağırlıklar
//! latest                 son checkpoint'in adı
//! checkpoints/step_N/    model.bpk + optim.bpk + state.json
//! log.csv                adım, kayıp, lr, token/s, VRAM, RAM
//! ```

pub mod config;
pub mod offload;

pub use config::{DistillConfig, InferenceConfig, MemoryEstimate, RunConfig, TrainConfig};

use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::mpsc;
use std::time::Instant;

use anyhow::{bail, Context, Result};
use burn::grad_clipping::GradientClippingConfig;
use burn::module::Module;
use burn::optim::{AdamWConfig, GradientsAccumulator, GradientsParams, ModuleOptimizer};
use burn::tensor::activation::log_softmax;
use burn::tensor::{Device, FloatDType, Int, Tensor, TensorData};
use offload::{CastMapper, OffloadTrainer};
use rand::rngs::StdRng;
use rand::SeedableRng;
use serde::{Deserialize, Serialize};
use sosis_data::ram::RamGuard;
use sosis_data::{DataMeta, TokenDataset, TRAIN_FILE, VAL_FILE};
use sosis_model::{select_device, Precision, SosyModel};

pub const BEST_FILE: &str = "best.bpk";
pub const CONFIG_FILE: &str = "config.toml";
pub const TOKENIZER_FILE: &str = "tokenizer.json";

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrainState {
    /// Tamamlanan optimizer adımı sayısı.
    pub step: usize,
    pub best_val_loss: f32,
    pub tokens_seen: u64,
}

/// Eğitimden bir adım raporu (CLI ve testler için).
#[derive(Debug, Clone)]
pub struct StepReport {
    pub step: usize,
    pub loss: f32,
    pub lr: f64,
    pub tokens_per_sec: f64,
    /// Backend bellek havuzunun ayırdığı VRAM (ölçülebiliyorsa).
    pub vram_gb: Option<f64>,
    /// Sürecin RAM kullanımı.
    pub ram_gb: f64,
}

pub struct TrainOutcome {
    pub final_step: usize,
    pub last_loss: f32,
    pub best_val_loss: f32,
}

fn to_tensor(v: Vec<i64>, b: usize, t: usize, device: &Device) -> Tensor<2, Int> {
    Tensor::from_data(TensorData::new(v, [b, t]), device)
}

fn checkpoint_dir(out: &Path, step: usize) -> PathBuf {
    out.join("checkpoints").join(format!("step_{step:07}"))
}

/// Son checkpoint klasörü (varsa).
pub fn latest_checkpoint(out: &Path) -> Option<PathBuf> {
    let name = fs::read_to_string(out.join("latest")).ok()?;
    let dir = out.join("checkpoints").join(name.trim());
    dir.join("model.bpk").exists().then_some(dir)
}

/// Bir eğitim klasöründeki ağırlıklar (`best.bpk`, yoksa son checkpoint) ya da doğrudan bir `.bpk` dosyası.
pub fn find_weights(path: &Path) -> Option<PathBuf> {
    if path.is_file() {
        return Some(path.to_path_buf());
    }
    let best = path.join(BEST_FILE);
    if best.exists() {
        return Some(best);
    }
    latest_checkpoint(path).map(|d| d.join("model.bpk"))
}

/// Gradyan alan parametre sayısı.
pub fn trainable_params(model: &SosyModel) -> usize {
    struct Count(usize);
    impl burn::module::ModuleVisitor for Count {
        fn visit_float<const D: usize>(&mut self, p: &burn::module::Param<Tensor<D>>) {
            let v = p.val();
            if v.is_require_grad() {
                self.0 += v.shape().num_elements();
            }
        }
    }
    let mut c = Count(0);
    model.visit(&mut c);
    c.0
}

fn rec_err<E: std::fmt::Debug>(e: E) -> anyhow::Error {
    anyhow::anyhow!("{e:?}")
}

fn build_optimizer(t: &TrainConfig) -> ModuleOptimizer {
    let clip = (t.grad_clip > 0.0).then_some(GradientClippingConfig::Norm(t.grad_clip));
    AdamWConfig::new()
        .with_beta_1(t.beta1)
        .with_beta_2(t.beta2)
        .with_epsilon(1e-8)
        .with_weight_decay(t.weight_decay)
        .with_grad_clipping(clip)
        .init()
}

/// Doğrudan (tek cihaz) ya da offload'lı eğitim motoru (tek örnek; varyant boyutu önemsiz).
#[allow(clippy::large_enum_variant)]
enum Engine {
    Direct {
        model: SosyModel,
        optim: ModuleOptimizer,
        accum: GradientsAccumulator<SosyModel>,
        device: Device,
    },
    Offload(Box<OffloadTrainer>),
}

impl Engine {
    fn model(&self) -> &SosyModel {
        match self {
            Engine::Direct { model, .. } => model,
            Engine::Offload(o) => o.working(),
        }
    }

    fn device(&self) -> &Device {
        match self {
            Engine::Direct { device, .. } => device,
            Engine::Offload(o) => o.gpu(),
        }
    }

    fn backward(&mut self, loss: Tensor<1>) {
        match self {
            Engine::Direct { model, accum, .. } => {
                let grads = GradientsParams::from_grads(loss.backward(), model);
                accum.accumulate(model, grads);
            }
            Engine::Offload(o) => o.backward(loss),
        }
    }

    fn step(&mut self, lr: f64) {
        match self {
            Engine::Direct {
                model,
                optim,
                accum,
                ..
            } => {
                let grads = accum.grads();
                *model = optim.step(lr, model.clone(), grads);
            }
            Engine::Offload(o) => o.step(lr),
        }
    }

    /// Kaydedilecek ağırlıklar (offload'da f32 ana kopya).
    fn weights(&self) -> &SosyModel {
        match self {
            Engine::Direct { model, .. } => model,
            Engine::Offload(o) => o.master(),
        }
    }

    fn optim(&self) -> &ModuleOptimizer {
        match self {
            Engine::Direct { optim, .. } => optim,
            Engine::Offload(o) => o.optimizer(),
        }
    }
}

/// Damıtma için gradyansız öğretmen.
struct Teacher {
    model: SosyModel,
    device: Device,
    alpha: f32,
    temperature: f32,
}

impl Teacher {
    fn load(
        d: &DistillConfig,
        student_vocab: usize,
        base: &Device,
        dtype: FloatDType,
    ) -> Result<Self> {
        let cfg = RunConfig::load(d.teacher.join(CONFIG_FILE)).with_context(|| {
            format!("öğretmen yapılandırması okunamadı: {}", d.teacher.display())
        })?;
        if cfg.model.vocab_size != student_vocab {
            bail!(
                "öğretmen ({}) ve öğrenci ({}) aynı tokenizer'ı kullanmalı",
                cfg.model.vocab_size,
                student_vocab
            );
        }
        let weights = find_weights(&d.teacher).context("öğretmen ağırlıkları bulunamadı")?;
        let device = base.clone().without_autodiff();
        let model = cfg
            .model
            .init(&device)
            .try_load_file(&weights)
            .map_err(rec_err)?
            .map(&mut CastMapper {
                dtype,
                train_on: None,
            });
        eprintln!(
            "🎓 öğretmen: {} ({:.1}M parametre)",
            cfg.model.name,
            cfg.model.param_count() as f64 / 1e6
        );
        Ok(Self {
            model,
            device,
            alpha: d.alpha,
            temperature: d.temperature,
        })
    }

    /// `α·CE + (1−α)·T²·KL(öğretmen ‖ öğrenci)`.
    fn loss(
        &self,
        student: &SosyModel,
        x: &[i64],
        y: Tensor<2, Int>,
        b: usize,
        t: usize,
        device: &Device,
    ) -> Tensor<1> {
        let ce = student.loss(to_tensor(x.to_vec(), b, t, device), y);
        let temp = self.temperature;
        let s_logits = student.logits_flat(to_tensor(x.to_vec(), b, t, device));
        let t_logits = self
            .model
            .logits_flat(to_tensor(x.to_vec(), b, t, &self.device))
            .to_device(device)
            .detach();
        let log_ps = log_softmax(s_logits.div_scalar(temp), 1);
        let log_pt = log_softmax(t_logits.div_scalar(temp), 1);
        let kl = (log_pt.clone().exp() * (log_pt - log_ps)).sum_dim(1).mean();
        ce.mul_scalar(self.alpha) + kl.mul_scalar((1.0 - self.alpha) * temp * temp)
    }
}

/// Doğrulama kümesinde ortalama kayıp (sabit tohumla, her seferinde aynı pencereler).
pub fn evaluate(model: &SosyModel, val: &TokenDataset, cfg: &TrainConfig, device: &Device) -> f32 {
    let model = model.valid();
    let device = device.clone().without_autodiff();
    let mut rng = StdRng::seed_from_u64(0xE7A1);
    let (b, t) = (
        cfg.micro_batch,
        cfg.seq_len.min(val.len().saturating_sub(2)),
    );
    let mut total = 0.0;
    for _ in 0..cfg.eval_batches {
        let (x, y) = val.sample_batch(&mut rng, b, t);
        let loss: f32 = model
            .loss(to_tensor(x, b, t, &device), to_tensor(y, b, t, &device))
            .into_scalar();
        total += loss;
    }
    total / cfg.eval_batches.max(1) as f32
}

type StepBatches = Vec<(Vec<i64>, Vec<i64>)>;

/// Adım başına tohum: devam edildiğinde de aynı veri sırası.
fn step_batches(ds: &TokenDataset, t: &TrainConfig, step: usize) -> StepBatches {
    let mut rng = StdRng::seed_from_u64(t.seed.wrapping_mul(1_000_003).wrapping_add(step as u64));
    (0..t.grad_accum)
        .map(|_| ds.sample_batch(&mut rng, t.micro_batch, t.seq_len))
        .collect()
}

/// Eğitimi başlatır (veya `resume` ise son checkpoint'ten sürdürür).
pub fn train(
    cfg: &RunConfig,
    resume: bool,
    mut on_step: impl FnMut(&StepReport),
) -> Result<TrainOutcome> {
    let t = &cfg.train;
    let out = &t.out_dir;
    fs::create_dir_all(out.join("checkpoints"))?;

    let meta =
        DataMeta::load(&t.data_dir).context("meta.json yok — önce `sosis prepare` çalıştırın")?;
    if meta.vocab_size > cfg.model.vocab_size {
        bail!(
            "tokenizer sözlüğü ({}) model vocab_size'dan ({}) büyük",
            meta.vocab_size,
            cfg.model.vocab_size
        );
    }
    let train_ds = TokenDataset::open(&t.data_dir.join(TRAIN_FILE))?;
    let val_ds = TokenDataset::open(&t.data_dir.join(VAL_FILE))?;
    let val_ds = if val_ds.len() > t.seq_len + 2 {
        val_ds
    } else {
        TokenDataset::open(&t.data_dir.join(TRAIN_FILE))?
    };

    // Çıkarımın kendi başına çalışabilmesi için yapılandırma + tokenizer kopyası.
    fs::write(out.join(CONFIG_FILE), cfg.to_toml()?)?;
    if t.tokenizer.exists() {
        fs::copy(&t.tokenizer, out.join(TOKENIZER_FILE))?;
    }

    // --- Cihazlar ---
    let lora = t.lora.clone();
    if lora.is_some() && t.optimizer_offload {
        eprintln!("ℹ LoRA'da optimizer durumu zaten küçük; optimizer_offload kapatıldı");
    }
    let offload = t.optimizer_offload && lora.is_none();
    let precision = if offload { Precision::F32 } else { t.precision };
    let base = select_device(&t.backend, precision)?;
    base.seed(t.seed);
    let device = if t.gradient_checkpointing {
        base.clone().autodiff().gradient_checkpointing()
    } else {
        base.clone().autodiff()
    };
    let work_dtype = if t.precision == Precision::Bf16 {
        FloatDType::BF16
    } else {
        FloatDType::F32
    };
    let cpu = if offload {
        Some(select_device("cpu", Precision::F32).context(
            "optimizer_offload için CPU backend'i gerekir (varsayılan `cpu` özelliğiyle derleyin)",
        )?)
    } else {
        None
    };
    // Ana ağırlıkların yaşadığı cihaz.
    let home = cpu.clone().unwrap_or_else(|| device.clone());

    // --- Model + optimizer ---
    let mut model = cfg.model.init(&home);
    let mut optim = build_optimizer(t);
    let mut state = TrainState {
        step: 0,
        best_val_loss: f32::INFINITY,
        tokens_seen: 0,
    };
    let checkpoint = if resume { latest_checkpoint(out) } else { None };
    if let Some(l) = &lora {
        if checkpoint.is_none() {
            let init = t
                .init_from
                .as_ref()
                .context("LoRA için `init_from` (taban model) gerekir")?;
            let w = find_weights(init)
                .with_context(|| format!("taban ağırlığı bulunamadı: {}", init.display()))?;
            model = model.try_load_file(&w).map_err(rec_err)?;
            eprintln!("⤷ taban ağırlıklar: {}", w.display());
        }
        model = sosis_model::lora::apply(model, l);
        eprintln!(
            "🧩 {} r={} α={}: {:.2}M eğitilebilir parametre (taban donuk)",
            if l.qlora { "QLoRA" } else { "LoRA" },
            l.rank,
            l.alpha,
            trainable_params(&model) as f64 / 1e6
        );
    }
    match (&checkpoint, &t.init_from) {
        (Some(dir), _) => {
            model = model
                .try_load_file(dir.join("model.bpk"))
                .context("model checkpoint'i yüklenemedi")?;
            optim = optim.load(dir.join("optim.bpk")).map_err(rec_err)?;
            state = serde_json::from_str(&fs::read_to_string(dir.join("state.json"))?)?;
            eprintln!("↻ {} adımından devam ediliyor", state.step);
        }
        (None, Some(init)) if lora.is_none() => {
            let w = find_weights(init)
                .with_context(|| format!("init_from ağırlığı bulunamadı: {}", init.display()))?;
            model = model.try_load_file(&w).map_err(rec_err)?;
            eprintln!("⤷ başlangıç ağırlıkları: {}", w.display());
        }
        _ => {
            if resume && checkpoint.is_none() {
                eprintln!("checkpoint bulunamadı, sıfırdan başlanıyor");
            }
        }
    }

    let mut engine = match cpu {
        Some(cpu) => {
            eprintln!(
                "⇄ optimizer offload: ana ağırlıklar + AdamW RAM'de, GPU'da {:?} kopya",
                work_dtype
            );
            Engine::Offload(Box::new(OffloadTrainer::new(
                model,
                optim,
                cpu,
                device.clone(),
                work_dtype,
            )))
        }
        None => Engine::Direct {
            model,
            optim,
            accum: GradientsAccumulator::new(),
            device: device.clone(),
        },
    };

    let teacher = match &cfg.distill {
        Some(d) => Some(Teacher::load(d, cfg.model.vocab_size, &device, work_dtype)?),
        None => None,
    };

    let mut log = fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(out.join("log.csv"))?;
    if log.metadata()?.len() == 0 {
        writeln!(
            log,
            "step,loss,val_loss,lr,tokens_per_sec,tokens_seen,vram_gb,ram_gb"
        )?;
    }

    let (b, sl) = (t.micro_batch, t.seq_len);
    let tokens_per_step = cfg.tokens_per_step() as f64;
    let mut last_loss = f32::NAN;
    let mut ram = RamGuard::new(t.max_ram_gb, 1);
    let mut ram_warned = false;
    let first_step = state.step;

    // Arka planda bir sonraki adımların batch'lerini hazırlayan iş parçacığı.
    std::thread::scope(|scope| -> Result<()> {
        let (tx, rx) = mpsc::sync_channel::<StepBatches>(t.prefetch_steps.max(1));
        let train_ds = &train_ds;
        scope.spawn(move || {
            for step in first_step..t.max_steps {
                if tx.send(step_batches(train_ds, t, step)).is_err() {
                    break;
                }
            }
        });

        while state.step < t.max_steps {
            let started = Instant::now();
            let lr = t.lr_at(state.step);
            let batches = rx.recv().context("veri iş parçacığı durdu")?;

            let mut loss_sum: Option<Tensor<1>> = None;
            for (x, y) in batches {
                let dev = engine.device().clone();
                let yt = to_tensor(y, b, sl, &dev);
                let loss = match &teacher {
                    Some(tch) => tch.loss(engine.model(), &x, yt, b, sl, &dev),
                    None => engine.model().loss(to_tensor(x, b, sl, &dev), yt),
                };
                let scaled = loss.clone().div_scalar(t.grad_accum as f32);
                engine.backward(scaled);
                let l = loss.detach();
                loss_sum = Some(match loss_sum {
                    Some(s) => s + l,
                    None => l,
                });
            }
            engine.step(lr);
            state.step += 1;
            state.tokens_seen += tokens_per_step as u64;

            let s = state.step;
            let do_log = s.is_multiple_of(t.log_every.max(1)) || s == 1;
            let do_eval = s.is_multiple_of(t.eval_every.max(1)) || s == t.max_steps;
            let do_save = s.is_multiple_of(t.save_every.max(1)) || s == t.max_steps;

            if do_log || do_eval || do_save {
                // Okuma, GPU'yu senkronlar; yalnızca log adımlarında yapılır.
                last_loss = loss_sum.unwrap().into_scalar::<f32>() / t.grad_accum as f32;
                if !last_loss.is_finite() {
                    bail!("kayıp sonsuz/NaN oldu (adım {s}). lr'yi düşürün veya precision = \"f32\" deneyin.");
                }
            }
            let tps = tokens_per_step / started.elapsed().as_secs_f64().max(1e-9);
            let mut val_loss = None;
            if do_eval {
                let v = evaluate(engine.model(), &val_ds, t, engine.device());
                val_loss = Some(v);
                if v < state.best_val_loss {
                    state.best_val_loss = v;
                    if lora.is_some() {
                        // LoRA: yalnızca adaptörler (taban init_from'da).
                        sosis_model::lora::save_adapter(engine.weights(), &out.join(BEST_FILE))?;
                    } else {
                        engine
                            .weights()
                            .clone()
                            .save_file(out.join(BEST_FILE))
                            .map_err(rec_err)?;
                    }
                }
            }
            if do_log || do_eval {
                let over = ram.over(1.0);
                if over && !ram_warned {
                    eprintln!(
                        "⚠ RAM {:.1} GB > sınır {:.0} GB — micro_batch/prefetch_steps'i düşürün",
                        ram.last_gb(),
                        t.max_ram_gb
                    );
                    ram_warned = true;
                }
                let vram_gb = engine
                    .device()
                    .memory_pool_usage()
                    .map(|u| u.bytes_reserved as f64 / 1024f64.powi(3));
                let report = StepReport {
                    step: s,
                    loss: last_loss,
                    lr,
                    tokens_per_sec: tps,
                    vram_gb,
                    ram_gb: ram.last_gb(),
                };
                on_step(&report);
                let v = val_loss.map(|v| v.to_string()).unwrap_or_default();
                let vr = vram_gb.map(|g| format!("{g:.2}")).unwrap_or_default();
                writeln!(
                    log,
                    "{s},{last_loss},{v},{lr},{tps:.0},{},{vr},{:.2}",
                    state.tokens_seen,
                    ram.last_gb()
                )?;
                if let Some(v) = val_loss {
                    eprintln!(
                        "  doğrulama kaybı: {v:.4}  (en iyi {:.4}, perplexity {:.2})",
                        state.best_val_loss,
                        v.exp()
                    );
                }
            }
            if do_save {
                save_checkpoint(
                    out,
                    engine.weights(),
                    engine.optim(),
                    &state,
                    t.keep_checkpoints,
                )?;
            }
        }
        Ok(())
    })?;

    Ok(TrainOutcome {
        final_step: state.step,
        last_loss,
        best_val_loss: state.best_val_loss,
    })
}

fn save_checkpoint(
    out: &Path,
    model: &SosyModel,
    optim: &ModuleOptimizer,
    state: &TrainState,
    keep: usize,
) -> Result<()> {
    let dir = checkpoint_dir(out, state.step);
    fs::create_dir_all(&dir)?;
    model
        .clone()
        .save_file(dir.join("model.bpk"))
        .map_err(rec_err)?;
    optim.save(dir.join("optim.bpk")).map_err(rec_err)?;
    fs::write(dir.join("state.json"), serde_json::to_string_pretty(state)?)?;
    fs::write(
        out.join("latest"),
        dir.file_name().unwrap().to_string_lossy().as_bytes(),
    )?;

    // Eski checkpoint'leri temizle.
    let mut all: Vec<PathBuf> = fs::read_dir(out.join("checkpoints"))?
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.is_dir())
        .collect();
    all.sort();
    if all.len() > keep.max(1) {
        for old in &all[..all.len() - keep.max(1)] {
            fs::remove_dir_all(old).ok();
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn lr_schedule() {
        let toml = include_str!("../../../configs/sosy-tiny.toml");
        let cfg: RunConfig = toml::from_str(toml).unwrap();
        let t = &cfg.train;
        assert!(t.lr_at(0) < t.lr);
        assert!((t.lr_at(t.warmup_steps) - t.lr).abs() < 1e-9);
        assert!((t.lr_at(t.max_steps) - t.min_lr).abs() < 1e-9);
        let mid = t.lr_at((t.warmup_steps + t.max_steps) / 2);
        assert!(mid < t.lr && mid > t.min_lr);
    }
}
