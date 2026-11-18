//! LoRA / QLoRA: büyük modeli kendi projenize az VRAM ve kısa sürede uyarlama.
//!
//! Taban ağırlıklar donar; dikkat ve MLP'deki her Linear ağırlığına `taban + (α/r)·(A@B)` biçiminde
//! küçük, eğitilebilir A [d_in×r] ve B [r×d_out] matrisleri eklenir. Kaydedilen adaptör dosyası
//! yalnızca bu matrisleri içerir (Sosy-Pro için r=16'da ~20 MB). QLoRA'da taban ayrıca int8'dir.

use std::path::Path;

use anyhow::{Context, Result};
use burn::module::{Lora, Module, ParamGroup, QLora, Quantizer};
use burn::store::ModuleRecord;
use burn::tensor::quantization::{Calibration, QuantValue};
use serde::{Deserialize, Serialize};

use crate::quant::LINEAR_WEIGHTS;
use crate::SosyModel;

/// Adaptör parametrelerinin yolları.
pub const ADAPTER_PARAMS: &str = r"\.lora\.[ab]$";

/// `[train.lora]` ayarları.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct LoraConfig {
    #[serde(default = "d_rank")]
    pub rank: usize,
    #[serde(default = "d_alpha")]
    pub alpha: f64,
    /// Taban ağırlıkları int8 tut (daha az VRAM).
    #[serde(default)]
    pub qlora: bool,
}

fn d_rank() -> usize {
    16
}
fn d_alpha() -> f64 {
    32.0
}

impl Default for LoraConfig {
    fn default() -> Self {
        Self {
            rank: d_rank(),
            alpha: d_alpha(),
            qlora: false,
        }
    }
}

/// Adaptörleri ekler; taban ağırlıkları dondurur.
pub fn apply(model: SosyModel, cfg: &LoraConfig) -> SosyModel {
    let group = ParamGroup::from_regex(LINEAR_WEIGHTS).expect("geçerli regex");
    let lora = Lora::new(cfg.rank, cfg.alpha).set_param_group(group);
    if cfg.qlora {
        let device = model.devices()[0].clone();
        let scheme = device
            .settings()
            .quantization
            .scheme
            .with_value(QuantValue::Q8S);
        let mut quantizer = Quantizer::new(Calibration::MinMax, scheme);
        quantizer.set_param_group(ParamGroup::from_regex(LINEAR_WEIGHTS).expect("geçerli regex"));
        model.apply_qlora(QLora::new(lora, quantizer))
    } else {
        model.apply_lora(lora)
    }
}

/// Yalnızca adaptör ağırlıklarını kaydeder.
pub fn save_adapter(model: &SosyModel, path: &Path) -> Result<()> {
    let group = ParamGroup::from_regex(ADAPTER_PARAMS).expect("geçerli regex");
    model
        .clone()
        .into_record_group(group)
        .save(path)
        .map_err(|e| anyhow::anyhow!("adaptör kaydedilemedi: {e:?}"))
}

/// Adaptörleri, [`apply`] edilmiş bir modele yükler.
pub fn load_adapter(model: SosyModel, path: &Path) -> Result<SosyModel> {
    let record = ModuleRecord::load(path)
        .map_err(|e| anyhow::anyhow!("{e:?}"))
        .with_context(|| format!("adaptör okunamadı: {}", path.display()))?
        .allow_partial(true);
    model
        .try_load_record(record)
        .map_err(|e| anyhow::anyhow!("adaptör yüklenemedi: {e:?}"))
}

/// Adaptörleri taban ağırlıklarına kalıcı olarak katar (çıkarımda ek maliyet kalmaz).
pub fn merge(model: SosyModel) -> SosyModel {
    model.valid().materialize()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ModelConfig;
    use burn::optim::{AdamWConfig, GradientsParams};
    use burn::tensor::{Device, Int, Tensor, TensorData};

    fn cfg() -> ModelConfig {
        ModelConfig {
            name: "l".into(),
            vocab_size: 48,
            d_model: 32,
            n_layers: 2,
            n_heads: 4,
            n_kv_heads: Some(2),
            ffn_hidden: 64,
            max_seq_len: 16,
            rope_theta: 1e4,
            rope_scaling: 1.0,
            attention: "standard".into(),
            norm_eps: 1e-5,
            init_std: 0.02,
        }
    }

    fn logits(m: &SosyModel, d: &Device) -> Vec<f32> {
        let x =
            Tensor::<2, Int>::from_data(TensorData::new((1..9i64).collect::<Vec<_>>(), [1, 8]), d);
        m.forward(x, None)
            .to_data()
            .convert::<f32>()
            .try_to_vec()
            .unwrap()
    }

    #[test]
    fn lora_trains_only_adapters_and_roundtrips() {
        let device = Device::flex().autodiff();
        let base = cfg().init(&device);
        let base_logits = logits(&base, &device);
        let mut m = apply(
            base,
            &LoraConfig {
                rank: 4,
                alpha: 8.0,
                qlora: false,
            },
        );
        // Başlangıçta B = 0: çıktı tabanla aynı.
        assert_eq!(logits(&m, &device), base_logits);

        let mut opt = AdamWConfig::new().init();
        let x = Tensor::<2, Int>::from_data(
            TensorData::new((1..9i64).collect::<Vec<_>>(), [1, 8]),
            &device,
        );
        let y = Tensor::<2, Int>::from_data(
            TensorData::new((2..10i64).collect::<Vec<_>>(), [1, 8]),
            &device,
        );
        let first: f32 = m.loss(x.clone(), y.clone()).into_scalar();
        for _ in 0..30 {
            let g = GradientsParams::from_grads(m.loss(x.clone(), y.clone()).backward(), &m);
            assert_eq!(g.len(), 2 * 5 * 2, "yalnızca adaptörler gradyan almalı");
            m = opt.step(1e-2, m, g);
        }
        let last: f32 = m.loss(x.clone(), y.clone()).into_scalar();
        assert!(last < first * 0.9, "{first} → {last}");

        // Adaptörü kaydet → yeni (aynı tabanlı) modele yükle → aynı çıktı.
        let dir = std::env::temp_dir().join(format!("sosis_lora_{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let pa = dir.join("adapter.bpk");
        save_adapter(&m, &pa).unwrap();
        let tuned = logits(&m, &device);

        // Tam kayıt (devam için) → adaptörlü iskelete yüklenir → aynı çıktı.
        let full = dir.join("full.bpk");
        m.clone().save_file(&full).unwrap();
        let restored = apply(
            cfg().init(&device),
            &LoraConfig {
                rank: 4,
                alpha: 8.0,
                qlora: false,
            },
        )
        .try_load_file(&full)
        .unwrap();
        assert_eq!(logits(&restored, &device), tuned);
        // Yalnızca adaptör dosyası yüklenebilir ve tam kayıttan çok küçüktür.
        load_adapter(
            apply(
                cfg().init(&device),
                &LoraConfig {
                    rank: 4,
                    alpha: 8.0,
                    qlora: false,
                },
            ),
            &pa,
        )
        .unwrap();
        assert!(
            std::fs::metadata(&pa).unwrap().len() < std::fs::metadata(&full).unwrap().len() / 2
        );

        // Birleştirme: adaptörlü çıktı = birleştirilmiş çıktı.
        let merged = merge(m.clone());
        let a = logits(&m.valid(), &device.clone().without_autodiff());
        let b = logits(&merged, &device.clone().without_autodiff());
        let diff = a
            .iter()
            .zip(&b)
            .map(|(x, y)| (x - y).abs())
            .fold(0f32, f32::max);
        assert!(diff < 1e-4, "merge farkı {diff}");
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn qlora_applies() {
        let device = Device::flex().autodiff();
        let m = apply(
            cfg().init(&device),
            &LoraConfig {
                rank: 4,
                alpha: 8.0,
                qlora: true,
            },
        );
        let x = Tensor::<2, Int>::from_data(
            TensorData::new((1..9i64).collect::<Vec<_>>(), [1, 8]),
            &device,
        );
        let loss = m.loss(x.clone(), x);
        let g = GradientsParams::from_grads(loss.backward(), &m);
        assert_eq!(g.len(), 2 * 5 * 2);
    }
}
