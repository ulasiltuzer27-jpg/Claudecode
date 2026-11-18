//! int8 ağırlık nicemleme (çıkarım için). Yalnızca dikkat ve MLP'deki Linear ağırlıkları
//! nicemlenir; embedding (çıkış katmanıyla paylaşılır) ve normlar tam hassasiyette kalır.

use burn::module::{Module, ParamGroup, Quantizer};
use burn::tensor::quantization::{Calibration, QuantValue};
use burn::tensor::Device;

use crate::SosyModel;

/// Nicemlenecek parametre yolları.
pub const LINEAR_WEIGHTS: &str =
    r"^blocks\.\d+\.(attn\.(qkv|out)|mlp\.(down|gate_up\.linear_(inner|outer)))\.weight$";

/// Linear ağırlıklarını simetrik int8'e (Q8S) çevirir.
pub fn quantize_int8(model: SosyModel, device: &Device) -> SosyModel {
    let scheme = device
        .settings()
        .quantization
        .scheme
        .with_value(QuantValue::Q8S);
    let mut q = Quantizer::new(Calibration::MinMax, scheme);
    let group = ParamGroup::from_regex(LINEAR_WEIGHTS).expect("geçerli regex");
    model.quantize_weights_group(&mut q, group)
}

#[cfg(test)]
mod tests {
    use super::*;
    use burn::module::{ModuleVisitor, Param};
    use burn::tensor::{DType, Int, Tensor, TensorData};

    struct Count {
        q: usize,
        f: usize,
    }
    impl ModuleVisitor for Count {
        fn visit_float<const D: usize>(&mut self, p: &Param<Tensor<D>>) {
            if matches!(p.val().dtype(), DType::QFloat(_)) {
                self.q += 1
            } else {
                self.f += 1
            }
        }
    }

    #[test]
    fn int8_close_to_f32() {
        let device = Device::flex();
        let cfg = crate::ModelConfig {
            name: "q".into(),
            vocab_size: 64,
            d_model: 64,
            n_layers: 2,
            n_heads: 4,
            n_kv_heads: Some(2),
            ffn_hidden: 128,
            max_seq_len: 32,
            rope_theta: 10_000.0,
            rope_scaling: 1.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        };
        let model = cfg.init(&device);
        let x = Tensor::<2, Int>::from_data(
            TensorData::new((0..16i64).collect::<Vec<_>>(), [1, 16]),
            &device,
        );
        let a: Vec<f32> = model
            .forward(x.clone(), None)
            .to_data()
            .convert::<f32>()
            .try_to_vec()
            .unwrap();
        let qm = quantize_int8(model, &device);
        let mut c = Count { q: 0, f: 0 };
        qm.visit(&mut c);
        assert_eq!(
            c.q,
            2 * 5,
            "katman başına 5 Linear ağırlığı: {} nicemli, {} değil",
            c.q,
            c.f
        );
        let b: Vec<f32> = qm
            .forward(x, None)
            .to_data()
            .convert::<f32>()
            .try_to_vec()
            .unwrap();
        let dot: f32 = a.iter().zip(&b).map(|(x, y)| x * y).sum();
        let na: f32 = a.iter().map(|x| x * x).sum::<f32>().sqrt();
        let nb: f32 = b.iter().map(|x| x * x).sum::<f32>().sqrt();
        let cos = dot / (na * nb);
        eprintln!("int8 kosinüs benzerliği: {cos:.5}");
        assert!(cos > 0.98, "{cos}");

        // Kaydet → nicemli iskelete yükle → aynı çıktı; dosya küçülür.
        let dir = std::env::temp_dir().join(format!("sosis_q_{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let (pf, pq) = (dir.join("f.bpk"), dir.join("q.bpk"));
        cfg.init(&device).save_file(&pf).unwrap();
        qm.clone().save_file(&pq).unwrap();
        let loaded = quantize_int8(cfg.init(&device), &device)
            .try_load_file(&pq)
            .unwrap();
        let x = Tensor::<2, Int>::from_data(
            TensorData::new((0..16i64).collect::<Vec<_>>(), [1, 16]),
            &device,
        );
        let c2: Vec<f32> = loaded
            .forward(x, None)
            .to_data()
            .convert::<f32>()
            .try_to_vec()
            .unwrap();
        let diff = b
            .iter()
            .zip(&c2)
            .map(|(x, y)| (x - y).abs())
            .fold(0f32, f32::max);
        assert!(diff < 1e-5, "yüklenen nicemli model farklı: {diff}");
        let (sf, sq) = (
            std::fs::metadata(&pf).unwrap().len(),
            std::fs::metadata(&pq).unwrap().len(),
        );
        eprintln!("dosya: f32 {sf} B, int8 {sq} B");
        assert!(sq < sf);
        std::fs::remove_dir_all(&dir).ok();
    }
}
