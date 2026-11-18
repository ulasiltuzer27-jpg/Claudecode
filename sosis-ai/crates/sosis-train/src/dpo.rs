//! DPO (Direct Preference Optimization): model, aynı soruya verilmiş iyi ve kötü cevap çiftlerinden
//! iyi olanı tercih etmeyi öğrenir. Ayrı bir ödül modeli gerekmez.
//!
//! Kayıp: `−log σ(β·[(log π(c) − log π_ref(c)) − (log π(r) − log π_ref(r))])`; log-olasılıklar
//! yalnızca cevap token'ları üzerinden. Referans (π_ref) eğitim başlangıcındaki modeldir; onun
//! değerleri **bir kez** hesaplanıp saklanır, böylece eğitim sırasında ikinci bir model bellekte
//! tutulmaz. LoRA ile birlikte kullanılması önerilir.

use std::fs;
use std::io::Write;
use std::time::Instant;

use anyhow::{bail, Context, Result};
use burn::module::Module;
use burn::optim::{GradientsAccumulator, GradientsParams};
use burn::tensor::activation::log_sigmoid;
use burn::tensor::{Device, Int, Tensor, TensorData};
use rand::seq::SliceRandom;
use rand::SeedableRng;
use serde::{Deserialize, Serialize};
use sosis_data::sft::{encode_answer, read_dpo_jsonl};
use sosis_model::{select_device, SosyModel};
use sosis_tokenizer::Tokenizer;

use crate::{build_optimizer, find_weights, rec_err, RunConfig, StepReport, TrainOutcome};
use crate::{BEST_FILE, CONFIG_FILE, TOKENIZER_FILE};

/// `[dpo]` ayarları.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DpoConfig {
    /// Tercih çiftleri (`sosis dpo-data` çıktısı veya elle yazılmış .jsonl).
    pub data: std::path::PathBuf,
    /// Referanstan ne kadar uzaklaşılabileceği (küçük = daha serbest).
    #[serde(default = "d_beta")]
    pub beta: f32,
}

fn d_beta() -> f32 {
    0.1
}

/// Kodlanmış bir çift: (girdi, hedef) dizileri, maskesiz hedefler PAD.
struct Encoded {
    chosen: (Vec<i64>, Vec<i64>),
    rejected: (Vec<i64>, Vec<i64>),
}

/// Bağlam + cevap → (girdi, hedef). Uzunsa baştan kırpılır (cevap korunur).
fn to_io(ids: &[u32], mask: &[bool], max_len: usize) -> (Vec<i64>, Vec<i64>) {
    let start = ids.len().saturating_sub(max_len + 1);
    let (ids, mask) = (&ids[start..], &mask[start..]);
    let x = ids[..ids.len() - 1].iter().map(|&i| i as i64).collect();
    let y = (1..ids.len())
        .map(|i| if mask[i] { ids[i] as i64 } else { 0 })
        .collect();
    (x, y)
}

/// Çiftleri `[2B, T]` (önce chosen'lar, sonra rejected'lar) sağdan doldurulmuş tensörlere çevirir.
fn batch(pairs: &[&Encoded], device: &Device) -> (Tensor<2, Int>, Tensor<2, Int>) {
    let rows: Vec<&(Vec<i64>, Vec<i64>)> = pairs
        .iter()
        .map(|p| &p.chosen)
        .chain(pairs.iter().map(|p| &p.rejected))
        .collect();
    let t = rows.iter().map(|r| r.0.len()).max().unwrap_or(1).max(1);
    let (mut x, mut y) = (Vec::new(), Vec::new());
    for (xi, yi) in rows.iter().map(|r| (&r.0, &r.1)) {
        x.extend(
            xi.iter()
                .copied()
                .chain(std::iter::repeat_n(0, t - xi.len())),
        );
        y.extend(
            yi.iter()
                .copied()
                .chain(std::iter::repeat_n(0, t - yi.len())),
        );
    }
    let n = rows.len();
    (
        Tensor::from_data(TensorData::new(x, [n, t]), device),
        Tensor::from_data(TensorData::new(y, [n, t]), device),
    )
}

pub fn train_dpo(cfg: &RunConfig, mut on_step: impl FnMut(&StepReport)) -> Result<TrainOutcome> {
    let t = &cfg.train;
    let d = cfg.dpo.as_ref().context("[dpo] bölümü yok")?;
    let out = &t.out_dir;
    fs::create_dir_all(out)?;
    fs::write(out.join(CONFIG_FILE), cfg.to_toml()?)?;
    fs::copy(&t.tokenizer, out.join(TOKENIZER_FILE))
        .with_context(|| format!("tokenizer kopyalanamadı: {}", t.tokenizer.display()))?;
    let tok = Tokenizer::load(&t.tokenizer)?;

    let raw = read_dpo_jsonl(&d.data)?;
    if raw.is_empty() {
        bail!("{} içinde DPO çifti yok", d.data.display());
    }
    let pairs: Vec<Encoded> = raw
        .iter()
        .map(|p| {
            let (ci, cm) = encode_answer(&tok, &p.context, &p.chosen);
            let (ri, rm) = encode_answer(&tok, &p.context, &p.rejected);
            Encoded {
                chosen: to_io(&ci, &cm, t.seq_len),
                rejected: to_io(&ri, &rm, t.seq_len),
            }
        })
        .collect();

    // --- Model: init_from (genelde SFT modeli) + isteğe bağlı LoRA ---
    let base = select_device(&t.backend, t.precision)?;
    base.seed(t.seed);
    let device = if t.gradient_checkpointing {
        base.clone().autodiff().gradient_checkpointing()
    } else {
        base.clone().autodiff()
    };
    let init = t
        .init_from
        .as_ref()
        .context("DPO için `init_from` (genelde sohbet/SFT modeli) gerekir")?;
    let w =
        find_weights(init).with_context(|| format!("ağırlık bulunamadı: {}", init.display()))?;
    let mut model: SosyModel = cfg.model.init(&device).try_load_file(&w).map_err(rec_err)?;
    if let Some(l) = &t.lora {
        model = sosis_model::lora::apply(model, l);
    }
    eprintln!(
        "⚖ DPO: {} çift, β={} {}",
        pairs.len(),
        d.beta,
        if t.lora.is_some() {
            "(LoRA)"
        } else {
            "(tam ince ayar)"
        }
    );

    // --- Referans log-olasılıkları (bir kez) ---
    let infer_dev = device.clone().without_autodiff();
    let frozen = model.valid();
    let mut reference: Vec<(f32, f32)> = Vec::with_capacity(pairs.len());
    for chunk in pairs.chunks(t.micro_batch.max(1)) {
        let refs: Vec<&Encoded> = chunk.iter().collect();
        let (x, y) = batch(&refs, &infer_dev);
        let lp: Vec<f32> = frozen
            .seq_logprob(x, y)
            .to_data()
            .convert::<f32>()
            .try_to_vec()
            .map_err(rec_err)?;
        let b = chunk.len();
        reference.extend((0..b).map(|i| (lp[i], lp[b + i])));
    }
    drop(frozen);

    // --- Eğitim ---
    let mut optim = build_optimizer(t);
    let mut log = fs::File::create(out.join("log.csv"))?;
    writeln!(log, "step,loss,reward_acc,margin,lr")?;
    let mut order: Vec<usize> = (0..pairs.len()).collect();
    let mut rng = rand::rngs::StdRng::seed_from_u64(t.seed);
    let mut cursor = order.len();
    let mut last_loss = f32::NAN;
    let beta = d.beta;

    for step in 0..t.max_steps {
        let started = Instant::now();
        let lr = t.lr_at(step);
        let mut accum = GradientsAccumulator::<SosyModel>::new();
        let (mut loss_sum, mut acc_sum, mut margin_sum) = (0.0f32, 0.0f32, 0.0f32);
        let mut tokens = 0usize;
        for _ in 0..t.grad_accum {
            let mut idx = Vec::with_capacity(t.micro_batch);
            while idx.len() < t.micro_batch.max(1) {
                if cursor >= order.len() {
                    order.shuffle(&mut rng);
                    cursor = 0;
                }
                idx.push(order[cursor]);
                cursor += 1;
            }
            let chunk: Vec<&Encoded> = idx.iter().map(|&i| &pairs[i]).collect();
            let b = chunk.len();
            let (x, y) = batch(&chunk, &device);
            tokens += x.dims()[0] * x.dims()[1];
            let lp = model.seq_logprob(x, y);
            let (rc, rr): (Vec<f32>, Vec<f32>) = idx.iter().map(|&i| reference[i]).unzip();
            let rc = Tensor::<1>::from_data(TensorData::new(rc, [b]), &device);
            let rr = Tensor::<1>::from_data(TensorData::new(rr, [b]), &device);
            let z =
                ((lp.clone().narrow(0, 0, b) - rc) - (lp.narrow(0, b, b) - rr)).mul_scalar(beta);
            let loss = log_sigmoid(z.clone()).neg().mean();
            let zs: Vec<f32> = z
                .clone()
                .detach()
                .to_data()
                .convert::<f32>()
                .try_to_vec()
                .map_err(rec_err)?;
            acc_sum += zs.iter().filter(|v| **v > 0.0).count() as f32 / b as f32;
            margin_sum += zs.iter().sum::<f32>() / b as f32 / beta;
            loss_sum += loss.clone().detach().into_scalar::<f32>();
            let grads = GradientsParams::from_grads(
                loss.div_scalar(t.grad_accum as f32).backward(),
                &model,
            );
            accum.accumulate(&model, grads);
        }
        model = optim.step(lr, model, accum.grads());
        let n = t.grad_accum as f32;
        last_loss = loss_sum / n;
        if !last_loss.is_finite() {
            bail!(
                "DPO kaybı NaN oldu (adım {}); lr veya beta'yı düşürün",
                step + 1
            );
        }
        let s = step + 1;
        writeln!(
            log,
            "{s},{last_loss},{},{},{lr}",
            acc_sum / n,
            margin_sum / n
        )?;
        if s.is_multiple_of(t.log_every.max(1)) || s == 1 || s == t.max_steps {
            on_step(&StepReport {
                step: s,
                loss: last_loss,
                lr,
                tokens_per_sec: tokens as f64 / started.elapsed().as_secs_f64().max(1e-9),
                vram_gb: None,
                ram_gb: 0.0,
            });
            eprintln!(
                "  ödül doğruluğu %{:.0}, ortalama fark {:.3}",
                100.0 * acc_sum / n,
                margin_sum / n
            );
        }
        if s.is_multiple_of(t.save_every.max(1)) || s == t.max_steps {
            if t.lora.is_some() {
                sosis_model::lora::save_adapter(&model, &out.join(BEST_FILE))?;
            } else {
                model
                    .clone()
                    .save_file(out.join(BEST_FILE))
                    .map_err(rec_err)?;
            }
        }
    }
    Ok(TrainOutcome {
        final_step: t.max_steps,
        last_loss,
        best_val_loss: f32::NAN,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn io_masks_and_truncation() {
        let ids = [10u32, 11, 12, 13, 14];
        let mask = [false, false, false, true, true];
        let (x, y) = to_io(&ids, &mask, 10);
        assert_eq!(x, vec![10, 11, 12, 13]);
        assert_eq!(y, vec![0, 0, 13, 14]);
        // Uzunsa baştan kırpılır, cevap korunur.
        let (x, y) = to_io(&ids, &mask, 2);
        assert_eq!(x, vec![12, 13]);
        assert_eq!(y, vec![13, 14]);
    }
}
