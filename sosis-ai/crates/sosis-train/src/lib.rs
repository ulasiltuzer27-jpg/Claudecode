//! # sosis-train
//!
//! Elle yazılmış eğitim döngüsü: AdamW, ısınma + kosinüs LR, gradyan biriktirme,
//! periyodik doğrulama, checkpoint ve kaldığı yerden devam.
//!
//! Çıktı klasörü düzeni (`train.out_dir`):
//! ```text
//! config.toml            kullanılan yapılandırma (çıkarım için)
//! tokenizer.json         tokenizer kopyası (klasör kendi başına yeterli olsun diye)
//! best.bpk               en düşük doğrulama kaybına sahip ağırlıklar
//! latest                 son checkpoint'in adı
//! checkpoints/step_N/    model.bpk + optim.bpk + state.json
//! log.csv                adım, kayıp, lr, token/s
//! ```

pub mod config;

pub use config::{RunConfig, TrainConfig};

use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::time::Instant;

use anyhow::{bail, Context, Result};
use burn::grad_clipping::GradientClippingConfig;
use burn::module::Module;
use burn::optim::{AdamWConfig, GradientsAccumulator, GradientsParams, ModuleOptimizer};
use burn::tensor::{Device, Int, Tensor, TensorData};
use rand::rngs::StdRng;
use rand::SeedableRng;
use serde::{Deserialize, Serialize};
use sosis_data::{DataMeta, TokenDataset, TRAIN_FILE, VAL_FILE};
use sosis_model::{select_device, SosyModel};

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

    let base = select_device(&t.backend, t.precision)?;
    base.seed(t.seed);
    let device = if t.gradient_checkpointing {
        base.autodiff().gradient_checkpointing()
    } else {
        base.autodiff()
    };

    let mut model = cfg.model.init(&device);
    let mut optim = build_optimizer(t);
    let mut state = TrainState {
        step: 0,
        best_val_loss: f32::INFINITY,
        tokens_seen: 0,
    };

    if resume {
        match latest_checkpoint(out) {
            Some(dir) => {
                model = model
                    .try_load_file(dir.join("model.bpk"))
                    .context("model checkpoint'i yüklenemedi")?;
                optim = optim
                    .load(dir.join("optim.bpk"))
                    .map_err(|e| anyhow::anyhow!("{e:?}"))?;
                state = serde_json::from_str(&fs::read_to_string(dir.join("state.json"))?)?;
                eprintln!("↻ {} adımından devam ediliyor", state.step);
            }
            None => eprintln!("checkpoint bulunamadı, sıfırdan başlanıyor"),
        }
    }

    let mut log = fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(out.join("log.csv"))?;
    if log.metadata()?.len() == 0 {
        writeln!(log, "step,loss,val_loss,lr,tokens_per_sec,tokens_seen")?;
    }

    let (b, sl) = (t.micro_batch, t.seq_len);
    let tokens_per_step = cfg.tokens_per_step() as f64;
    let mut last_loss = f32::NAN;

    while state.step < t.max_steps {
        let step = state.step;
        let started = Instant::now();
        // Adım başına tohum: devam edildiğinde de aynı veri sırası.
        let mut rng =
            StdRng::seed_from_u64(t.seed.wrapping_mul(1_000_003).wrapping_add(step as u64));
        let lr = t.lr_at(step);

        let mut accum = GradientsAccumulator::<SosyModel>::new();
        let mut loss_sum: Option<Tensor<1>> = None;
        for _ in 0..t.grad_accum {
            let (x, y) = train_ds.sample_batch(&mut rng, b, sl);
            let loss = model.loss(to_tensor(x, b, sl, &device), to_tensor(y, b, sl, &device));
            let scaled = loss.clone().div_scalar(t.grad_accum as f32);
            let grads = GradientsParams::from_grads(scaled.backward(), &model);
            accum.accumulate(&model, grads);
            let l = loss.detach();
            loss_sum = Some(match loss_sum {
                Some(s) => s + l,
                None => l,
            });
        }
        model = optim.step(lr, model, accum.grads());
        state.step += 1;
        state.tokens_seen += tokens_per_step as u64;

        let s = state.step;
        let do_log = s.is_multiple_of(t.log_every.max(1)) || s == 1;
        let do_eval = s.is_multiple_of(t.eval_every.max(1)) || s == t.max_steps;
        let do_save = s.is_multiple_of(t.save_every.max(1)) || s == t.max_steps;

        let mut val_loss = None;
        if do_log || do_eval || do_save {
            // Okuma, GPU'yu senkronlar; yalnızca log adımlarında yapılır.
            last_loss = loss_sum.unwrap().into_scalar::<f32>() / t.grad_accum as f32;
            if !last_loss.is_finite() {
                bail!("kayıp sonsuz/NaN oldu (adım {s}). lr'yi düşürün veya precision = \"f32\" deneyin.");
            }
        }
        let tps = tokens_per_step / started.elapsed().as_secs_f64().max(1e-9);
        if do_eval {
            let v = evaluate(&model, &val_ds, t, &device);
            val_loss = Some(v);
            if v < state.best_val_loss {
                state.best_val_loss = v;
                model
                    .clone()
                    .save_file(out.join(BEST_FILE))
                    .map_err(|e| anyhow::anyhow!("{e:?}"))?;
            }
        }
        if do_log || do_eval {
            on_step(&StepReport {
                step: s,
                loss: last_loss,
                lr,
                tokens_per_sec: tps,
            });
            let v = val_loss.map(|v| v.to_string()).unwrap_or_default();
            writeln!(
                log,
                "{s},{last_loss},{v},{lr},{tps:.0},{}",
                state.tokens_seen
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
            save_checkpoint(out, &model, &optim, &state, t.keep_checkpoints)?;
        }
    }

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
        .map_err(|e| anyhow::anyhow!("{e:?}"))?;
    optim
        .save(dir.join("optim.bpk"))
        .map_err(|e| anyhow::anyhow!("{e:?}"))?;
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
