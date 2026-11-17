//! # sosis-model
//!
//! Sosy model ailesinin mimarisi: Llama tarzı, yalnızca-decoder Transformer.
//!
//! - RMSNorm (pre-norm)
//! - RoPE (rotary konum kodlaması)
//! - SwiGLU ileri besleme katmanı
//! - Giriş embedding'i ile çıkış katmanı ağırlık paylaşımlı (weight tying)
//! - Çıkarım için KV-cache

mod device;

pub use device::{select_device, Precision};

use burn::module::Module;
use burn::nn::loss::CrossEntropyLossConfig;
use burn::nn::{
    Embedding, EmbeddingConfig, Initializer, Linear, LinearConfig, RmsNorm, RmsNormConfig,
    RotaryEncoding, RotaryEncodingConfig, SwiGlu, SwiGluConfig,
};
use burn::tensor::module::attention;
use burn::tensor::ops::AttentionModuleOptions;
use burn::tensor::{Device, Int, Tensor};
use serde::{Deserialize, Serialize};

/// Model hiperparametreleri (`configs/*.toml` içindeki `[model]` bölümü).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ModelConfig {
    /// Görünen ad, ör. "Sosy 1.0".
    pub name: String,
    pub vocab_size: usize,
    pub d_model: usize,
    pub n_layers: usize,
    pub n_heads: usize,
    /// SwiGLU gizli boyutu (genelde ~8/3 * d_model).
    pub ffn_hidden: usize,
    /// En uzun bağlam (token).
    pub max_seq_len: usize,
    #[serde(default = "default_theta")]
    pub rope_theta: f32,
    #[serde(default = "default_eps")]
    pub norm_eps: f64,
    #[serde(default = "default_std")]
    pub init_std: f64,
}

fn default_theta() -> f32 {
    10_000.0
}
fn default_eps() -> f64 {
    1e-5
}
fn default_std() -> f64 {
    0.02
}

impl ModelConfig {
    pub fn head_dim(&self) -> usize {
        self.d_model / self.n_heads
    }

    pub fn validate(&self) -> anyhow::Result<()> {
        anyhow::ensure!(
            self.d_model.is_multiple_of(self.n_heads),
            "d_model, n_heads'e tam bölünmeli"
        );
        anyhow::ensure!(
            self.head_dim().is_multiple_of(2),
            "head_dim çift olmalı (RoPE)"
        );
        anyhow::ensure!(
            self.vocab_size > 0 && self.n_layers > 0,
            "geçersiz boyutlar"
        );
        Ok(())
    }

    /// Ağırlık paylaşımıyla toplam parametre sayısı (RoPE tamponları hariç).
    pub fn param_count(&self) -> usize {
        let d = self.d_model;
        let attn = d * 3 * d + d * d;
        let mlp = 3 * d * self.ffn_hidden;
        let norms = 2 * d;
        self.vocab_size * d + self.n_layers * (attn + mlp + norms) + d
    }

    pub fn init(&self, device: &Device) -> SosyModel {
        self.validate().expect("geçersiz model yapılandırması");
        let std = self.init_std;
        // GPT-2 / Llama: artık (residual) çıkış projeksiyonları 1/sqrt(2L) ile ölçeklenir.
        let resid_std = std / (2.0 * self.n_layers as f64).sqrt();
        let normal = |s: f64| Initializer::Normal { mean: 0.0, std: s };
        let linear = |i: usize, o: usize, s: f64| {
            LinearConfig::new(i, o)
                .with_bias(false)
                .with_initializer(normal(s))
                .init(device)
        };
        let blocks = (0..self.n_layers)
            .map(|_| Block {
                attn_norm: RmsNormConfig::new(self.d_model)
                    .with_epsilon(self.norm_eps)
                    .init(device),
                attn: Attention {
                    qkv: linear(self.d_model, 3 * self.d_model, std),
                    out: linear(self.d_model, self.d_model, resid_std),
                    n_heads: self.n_heads,
                    head_dim: self.head_dim(),
                },
                mlp_norm: RmsNormConfig::new(self.d_model)
                    .with_epsilon(self.norm_eps)
                    .init(device),
                mlp: Mlp {
                    gate_up: SwiGluConfig::new(self.d_model, self.ffn_hidden)
                        .with_initializer(normal(std))
                        .init(device),
                    down: linear(self.ffn_hidden, self.d_model, resid_std),
                },
            })
            .collect();
        SosyModel {
            embed: EmbeddingConfig::new(self.vocab_size, self.d_model)
                .with_initializer(normal(std))
                .init(device),
            blocks,
            norm: RmsNormConfig::new(self.d_model)
                .with_epsilon(self.norm_eps)
                .init(device),
            rope: RotaryEncodingConfig::new(self.max_seq_len, self.head_dim())
                .with_theta(self.rope_theta)
                .init(device),
            max_seq_len: self.max_seq_len,
        }
    }
}

#[derive(Module, Debug)]
pub struct Attention {
    qkv: Linear,
    out: Linear,
    n_heads: usize,
    head_dim: usize,
}

impl Attention {
    fn forward(
        &self,
        x: Tensor<3>,
        rope: &RotaryEncoding,
        start: usize,
        cache: Option<&mut Option<(Tensor<4>, Tensor<4>)>>,
    ) -> Tensor<3> {
        let [b, t, d] = x.dims();
        let (h, hd) = (self.n_heads, self.head_dim);
        let qkv = self.qkv.forward(x);
        let split = |i: usize| {
            qkv.clone()
                .narrow(2, i * d, d)
                .reshape([b, t, h, hd])
                .swap_dims(1, 2)
        };
        let q = rope.apply(split(0), start);
        let mut k = rope.apply(split(1), start);
        let mut v = split(2);

        if let Some(slot) = cache {
            if let Some((pk, pv)) = slot.take() {
                k = Tensor::cat(vec![pk, k], 2);
                v = Tensor::cat(vec![pv, v], 2);
            }
            *slot = Some((k.clone(), v.clone()));
        }
        // Tek token'lık adımda (önceki her şeye bakar) maske gerekmez; çok token'lı geçişler
        // boş önbellekten başlar ve nedensel (causal) maskeyle hesaplanır.
        let is_causal = t > 1;
        debug_assert!(
            !is_causal || start == 0,
            "çok token'lı geçiş boş önbellekle başlamalı"
        );
        let y = attention(
            q,
            k,
            v,
            None,
            None,
            AttentionModuleOptions {
                scale: None,
                softcap: None,
                is_causal,
            },
        );
        self.out.forward(y.swap_dims(1, 2).reshape([b, t, d]))
    }
}

#[derive(Module, Debug)]
pub struct Mlp {
    gate_up: SwiGlu,
    down: Linear,
}

#[derive(Module, Debug)]
pub struct Block {
    attn_norm: RmsNorm,
    attn: Attention,
    mlp_norm: RmsNorm,
    mlp: Mlp,
}

#[derive(Module, Debug)]
pub struct SosyModel {
    embed: Embedding,
    blocks: Vec<Block>,
    norm: RmsNorm,
    rope: RotaryEncoding,
    max_seq_len: usize,
}

/// Katman başına anahtar/değer önbelleği.
#[derive(Debug, Clone, Default)]
pub struct KvCache {
    layers: Vec<Option<(Tensor<4>, Tensor<4>)>>,
    /// Önbellekteki token sayısı.
    pub len: usize,
}

impl KvCache {
    pub fn new(n_layers: usize) -> Self {
        Self {
            layers: vec![None; n_layers],
            len: 0,
        }
    }

    pub fn clear(&mut self) {
        self.layers.iter_mut().for_each(|l| *l = None);
        self.len = 0;
    }
}

impl SosyModel {
    pub fn max_seq_len(&self) -> usize {
        self.max_seq_len
    }

    pub fn n_layers(&self) -> usize {
        self.blocks.len()
    }

    /// Son normalizasyona kadar gizli durumlar: `[b, t, d]`.
    fn hidden(&self, tokens: Tensor<2, Int>, mut cache: Option<&mut KvCache>) -> Tensor<3> {
        let [_, t] = tokens.dims();
        let start = cache.as_ref().map(|c| c.len).unwrap_or(0);
        assert!(
            start + t <= self.max_seq_len,
            "bağlam uzunluğu aşıldı ({} > {})",
            start + t,
            self.max_seq_len
        );
        let mut x = self.embed.forward(tokens);
        for (i, block) in self.blocks.iter().enumerate() {
            let slot = cache.as_deref_mut().map(|c| &mut c.layers[i]);
            let a = block
                .attn
                .forward(block.attn_norm.forward(x.clone()), &self.rope, start, slot);
            x = x + a;
            let m = block
                .mlp
                .down
                .forward(block.mlp.gate_up.forward(block.mlp_norm.forward(x.clone())));
            x = x + m;
        }
        if let Some(c) = cache {
            c.len += t;
        }
        self.norm.forward(x)
    }

    /// Paylaşımlı embedding ile logit'ler: `[n, d] -> [n, vocab]`.
    fn project(&self, h: Tensor<2>) -> Tensor<2> {
        h.matmul(self.embed.weight.val().transpose())
    }

    /// Tam ileri geçiş: `tokens [b, t] -> logits [b, t, vocab]`.
    pub fn forward(&self, tokens: Tensor<2, Int>, cache: Option<&mut KvCache>) -> Tensor<3> {
        let [b, t] = tokens.dims();
        let h = self.hidden(tokens, cache);
        let d = h.dims()[2];
        let logits = self.project(h.reshape([b * t, d]));
        let v = logits.dims()[1];
        logits.reshape([b, t, v])
    }

    /// Yalnızca son konumun logit'leri: `[b, vocab]` (üretimde bellek tasarrufu).
    pub fn forward_last(&self, tokens: Tensor<2, Int>, cache: Option<&mut KvCache>) -> Tensor<2> {
        let [b, t] = tokens.dims();
        let h = self.hidden(tokens, cache);
        let d = h.dims()[2];
        self.project(h.narrow(1, t - 1, 1).reshape([b, d]))
    }

    /// Bir sonraki token tahmini için ortalama çapraz entropi kaybı.
    pub fn loss(&self, inputs: Tensor<2, Int>, targets: Tensor<2, Int>) -> Tensor<1> {
        let [b, t] = inputs.dims();
        let device = inputs.device();
        let h = self.hidden(inputs, None);
        let d = h.dims()[2];
        let logits = self.project(h.reshape([b * t, d]));
        CrossEntropyLossConfig::new()
            .init(&device)
            .forward(logits, targets.reshape([b * t]))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use burn::tensor::TensorData;

    fn tiny() -> ModelConfig {
        ModelConfig {
            name: "test".into(),
            vocab_size: 64,
            d_model: 32,
            n_layers: 2,
            n_heads: 4,
            ffn_hidden: 64,
            max_seq_len: 32,
            rope_theta: 10_000.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        }
    }

    fn toks(v: Vec<i64>, b: usize, t: usize, device: &Device) -> Tensor<2, Int> {
        Tensor::from_data(TensorData::new(v, [b, t]), device)
    }

    #[test]
    fn shapes_and_param_count() {
        let device = Device::flex();
        let cfg = tiny();
        let model = cfg.init(&device);
        assert_eq!(model.num_params(), cfg.param_count());
        let x = toks((0..16).collect(), 2, 8, &device);
        assert_eq!(model.forward(x.clone(), None).dims(), [2, 8, 64]);
        assert_eq!(model.forward_last(x, None).dims(), [2, 64]);
    }

    #[test]
    fn kv_cache_matches_full_forward() {
        let device = Device::flex();
        let cfg = tiny();
        let model = cfg.init(&device);
        let seq: Vec<i64> = vec![3, 7, 1, 9, 12, 5];
        let full = model.forward(toks(seq.clone(), 1, 6, &device), None);
        let full_last: Vec<f32> = full
            .narrow(1, 5, 1)
            .reshape([64])
            .to_data()
            .try_to_vec::<f32>()
            .unwrap();

        let mut cache = KvCache::new(cfg.n_layers);
        let _ = model.forward_last(toks(seq[..3].to_vec(), 1, 3, &device), Some(&mut cache));
        let mut last = None;
        for &tk in &seq[3..] {
            last = Some(model.forward_last(toks(vec![tk], 1, 1, &device), Some(&mut cache)));
        }
        assert_eq!(cache.len, 6);
        let inc: Vec<f32> = last
            .unwrap()
            .reshape([64])
            .to_data()
            .try_to_vec::<f32>()
            .unwrap();
        for (a, b) in full_last.iter().zip(&inc) {
            assert!((a - b).abs() < 1e-4, "{a} != {b}");
        }
    }

    #[test]
    fn causal_no_future_leak() {
        let device = Device::flex();
        let model = tiny().init(&device);
        let a = model.forward(toks(vec![1, 2, 3, 4], 1, 4, &device), None);
        let b = model.forward(toks(vec![1, 2, 3, 60], 1, 4, &device), None);
        let a: Vec<f32> = a.narrow(1, 0, 3).to_data().try_to_vec::<f32>().unwrap();
        let b: Vec<f32> = b.narrow(1, 0, 3).to_data().try_to_vec::<f32>().unwrap();
        for (x, y) in a.iter().zip(&b) {
            assert!((x - y).abs() < 1e-5);
        }
    }

    #[test]
    fn initial_loss_near_uniform() {
        let device = Device::flex();
        let model = tiny().init(&device);
        let x = toks((0..32).map(|i| i % 64).collect(), 2, 16, &device);
        let y = toks((1..33).map(|i| i % 64).collect(), 2, 16, &device);
        let loss: f32 = model.loss(x, y).into_scalar();
        let uniform = (64f32).ln();
        assert!(
            (loss - uniform).abs() < 0.3,
            "loss {loss}, beklenen ~{uniform}"
        );
    }
}
