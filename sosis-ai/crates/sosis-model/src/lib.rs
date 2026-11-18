//! # sosis-model
//!
//! Sosy model ailesinin mimarisi: Llama tarzı, yalnızca-decoder Transformer.
//!
//! - RMSNorm (pre-norm)
//! - RoPE (rotary konum kodlaması, NeoX düzeni; NTK-uyumlu bağlam ölçekleme)
//! - GQA: anahtar/değer kafaları sorgu kafalarından az olabilir (KV-cache küçülür)
//! - SwiGLU ileri besleme katmanı
//! - Giriş embedding'i ile çıkış katmanı ağırlık paylaşımlı (weight tying)
//! - Çıkarım için KV-cache

mod device;
pub mod lora;
pub mod quant;

pub use burn::module::Module as BurnModule;
pub use device::{select_device, Precision};

use burn::module::Module;
use burn::nn::loss::CrossEntropyLossConfig;
use burn::nn::{
    Embedding, EmbeddingConfig, Initializer, Linear, LinearConfig, RmsNorm, RmsNormConfig, SwiGlu,
    SwiGluConfig,
};
use burn::tensor::module::attention;
use burn::tensor::ops::AttentionModuleOptions;
use burn::tensor::{Bool, Device, FloatDType, Int, Tensor, TensorData};
use serde::{Deserialize, Serialize};

/// Kayıpta yok sayılan hedef id'si (tokenizer'daki `<|pad|>`). Talimat ince ayarında
/// soru token'larının hedefleri buna çevrilerek yalnızca cevaptan öğrenilir.
pub const IGNORE_ID: usize = 0;

/// Model hiperparametreleri (`configs/*.toml` içindeki `[model]` bölümü).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ModelConfig {
    /// Görünen ad, ör. "Sosy 1.0".
    pub name: String,
    pub vocab_size: usize,
    pub d_model: usize,
    pub n_layers: usize,
    pub n_heads: usize,
    /// Anahtar/değer kafa sayısı (GQA). Verilmezse `n_heads` (klasik çok kafalı dikkat).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub n_kv_heads: Option<usize>,
    /// SwiGLU gizli boyutu (genelde ~8/3 * d_model).
    pub ffn_hidden: usize,
    /// Eğitildiği en uzun bağlam (token).
    pub max_seq_len: usize,
    #[serde(default = "default_theta")]
    pub rope_theta: f32,
    /// Çıkarımda bağlamı bu katsayıyla uzatır (NTK-uyumlu RoPE). 1.0 = kapalı.
    /// Ör. 2048'de eğitilmiş model için 2.0 → 4096 token.
    #[serde(default = "default_one")]
    pub rope_scaling: f32,
    #[serde(default = "default_eps")]
    pub norm_eps: f64,
    #[serde(default = "default_std")]
    pub init_std: f64,
}

fn default_theta() -> f32 {
    10_000.0
}
fn default_one() -> f32 {
    1.0
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

    pub fn kv_heads(&self) -> usize {
        self.n_kv_heads.unwrap_or(self.n_heads)
    }

    /// Ölçekleme dahil kullanılabilir bağlam uzunluğu.
    pub fn context_len(&self) -> usize {
        (self.max_seq_len as f32 * self.rope_scaling.max(1.0)) as usize
    }

    /// NTK-uyumlu ölçekleme sonrası RoPE tabanı.
    pub fn effective_theta(&self) -> f64 {
        let hd = self.head_dim() as f64;
        let s = self.rope_scaling.max(1.0) as f64;
        self.rope_theta as f64 * s.powf(hd / (hd - 2.0))
    }

    pub fn validate(&self) -> anyhow::Result<()> {
        anyhow::ensure!(
            self.d_model.is_multiple_of(self.n_heads),
            "d_model, n_heads'e tam bölünmeli"
        );
        anyhow::ensure!(
            self.head_dim().is_multiple_of(2) && self.head_dim() > 2,
            "head_dim çift olmalı (RoPE)"
        );
        anyhow::ensure!(
            self.kv_heads() > 0 && self.n_heads.is_multiple_of(self.kv_heads()),
            "n_heads, n_kv_heads'e tam bölünmeli"
        );
        anyhow::ensure!(
            self.vocab_size > 0 && self.n_layers > 0,
            "geçersiz boyutlar"
        );
        anyhow::ensure!(self.rope_scaling >= 1.0, "rope_scaling >= 1 olmalı");
        Ok(())
    }

    /// Ağırlık paylaşımıyla toplam parametre sayısı.
    pub fn param_count(&self) -> usize {
        let d = self.d_model;
        let hd = self.head_dim();
        let attn = d * (self.n_heads + 2 * self.kv_heads()) * hd + self.n_heads * hd * d;
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
        let (h, kv, hd) = (self.n_heads, self.kv_heads(), self.head_dim());
        let blocks = (0..self.n_layers)
            .map(|_| Block {
                attn_norm: RmsNormConfig::new(self.d_model)
                    .with_epsilon(self.norm_eps)
                    .init(device),
                attn: Attention {
                    qkv: linear(self.d_model, (h + 2 * kv) * hd, std),
                    out: linear(h * hd, self.d_model, resid_std),
                    n_heads: h,
                    n_kv_heads: kv,
                    head_dim: hd,
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
            context_len: self.context_len(),
            head_dim: hd,
            rope_theta: self.effective_theta(),
        }
    }
}

/// Bir ileri geçiş için RoPE tabloları: `[1, 1, t, head_dim]`, girdinin dtype'ında.
struct RopeTables {
    cos: Tensor<4>,
    sin: Tensor<4>,
}

impl RopeTables {
    fn new(start: usize, t: usize, hd: usize, theta: f64, like: &Tensor<3>) -> Self {
        let half = hd / 2;
        let mut cos = Vec::with_capacity(t * hd);
        let mut sin = Vec::with_capacity(t * hd);
        for p in start..start + t {
            for j in 0..hd {
                let i = j % half;
                let angle = p as f64 * theta.powf(-2.0 * i as f64 / hd as f64);
                cos.push(angle.cos() as f32);
                sin.push(angle.sin() as f32);
            }
        }
        let device = like.device();
        let dtype = like.dtype();
        let mk = |v: Vec<f32>| {
            Tensor::<4>::from_data(TensorData::new(v, [1, 1, t, hd]), &device).cast(dtype)
        };
        Self {
            cos: mk(cos),
            sin: mk(sin),
        }
    }

    /// `x: [b, heads, t, hd]` → döndürülmüş `x` (NeoX: ilk yarı / ikinci yarı).
    fn apply(&self, x: Tensor<4>) -> Tensor<4> {
        let hd = x.dims()[3];
        let half = hd / 2;
        let x1 = x.clone().narrow(3, 0, half);
        let x2 = x.clone().narrow(3, half, half);
        let rotated = Tensor::cat(vec![x2.neg(), x1], 3);
        x * self.cos.clone() + rotated * self.sin.clone()
    }
}

#[derive(Module, Debug)]
pub struct Attention {
    qkv: Linear,
    out: Linear,
    n_heads: usize,
    n_kv_heads: usize,
    head_dim: usize,
}

impl Attention {
    fn forward(
        &self,
        x: Tensor<3>,
        rope: &RopeTables,
        start: usize,
        cache: Option<&mut Option<(Tensor<4>, Tensor<4>)>>,
    ) -> Tensor<3> {
        let [b, t, _] = x.dims();
        let (h, kv, hd) = (self.n_heads, self.n_kv_heads, self.head_dim);
        let qkv = self.qkv.forward(x);
        let heads = |offset: usize, n: usize| {
            qkv.clone()
                .narrow(2, offset * hd, n * hd)
                .reshape([b, t, n, hd])
                .swap_dims(1, 2)
        };
        let q = rope.apply(heads(0, h));
        let mut k = rope.apply(heads(h, kv));
        let mut v = heads(h + kv, kv);

        if let Some(slot) = cache {
            if let Some((pk, pv)) = slot.take() {
                k = Tensor::cat(vec![pk, k], 2);
                v = Tensor::cat(vec![pv, v], 2);
            }
            *slot = Some((k.clone(), v.clone()));
        }
        // GQA: her KV kafası `h / kv` sorgu kafasına hizmet eder.
        if kv != h {
            let g = h / kv;
            let tk = k.dims()[2];
            let repeat = |z: Tensor<4>| {
                z.unsqueeze_dim::<5>(2)
                    .expand([b, kv, g, tk, hd])
                    .reshape([b, h, tk, hd])
            };
            k = repeat(k);
            v = repeat(v);
        }
        // Tek token'lık adımda (önceki her şeye bakar) maske gerekmez. Boş önbellekten başlayan
        // çok token'lı geçiş hızlı nedensel yolu kullanır; önbellek doluyken (start > 0, ör.
        // spekülatif doğrulama, önek önbelleği) açık maske kurulur: sorgu i, anahtar j'yi
        // ancak j <= start + i ise görür.
        let tk = k.dims()[2];
        let (is_causal, mask) = if t == 1 {
            (false, None)
        } else if start == 0 && tk == t {
            (true, None)
        } else {
            let past = tk - t;
            let m: Vec<bool> = (0..t)
                .flat_map(|i| (0..tk).map(move |j| j > past + i))
                .collect();
            let m = Tensor::<2, Bool>::from_data(TensorData::new(m, [t, tk]), &q.device())
                .unsqueeze::<4>()
                .expand([b, h, t, tk]);
            (false, Some(m))
        };
        let y = attention(
            q,
            k,
            v,
            mask,
            None,
            AttentionModuleOptions {
                scale: None,
                softcap: None,
                is_causal,
            },
        );
        self.out.forward(y.swap_dims(1, 2).reshape([b, t, h * hd]))
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
    context_len: usize,
    head_dim: usize,
    rope_theta: f64,
}

/// Katman başına anahtar/değer önbelleği (GQA'da yalnızca KV kafaları saklanır).
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

    /// Önbelleği ilk `len` token'a geri sarar (spekülatif ret, önek yeniden kullanımı).
    pub fn truncate(&mut self, len: usize) {
        if len >= self.len {
            return;
        }
        if len == 0 {
            return self.clear();
        }
        for slot in self.layers.iter_mut() {
            if let Some((k, v)) = slot.take() {
                *slot = Some((k.narrow(2, 0, len), v.narrow(2, 0, len)));
            }
        }
        self.len = len;
    }
}

impl SosyModel {
    /// Kullanılabilir en uzun bağlam (RoPE ölçekleme dahil).
    pub fn max_seq_len(&self) -> usize {
        self.context_len
    }

    pub fn n_layers(&self) -> usize {
        self.blocks.len()
    }

    /// Son normalizasyona kadar gizli durumlar: `[b, t, d]`.
    fn hidden(&self, tokens: Tensor<2, Int>, mut cache: Option<&mut KvCache>) -> Tensor<3> {
        let [_, t] = tokens.dims();
        let start = cache.as_ref().map(|c| c.len).unwrap_or(0);
        assert!(
            start + t <= self.context_len,
            "bağlam uzunluğu aşıldı ({} > {})",
            start + t,
            self.context_len
        );
        let mut x = self.embed.forward(tokens);
        let rope = RopeTables::new(start, t, self.head_dim, self.rope_theta, &x);
        for (i, block) in self.blocks.iter().enumerate() {
            let slot = cache.as_deref_mut().map(|c| &mut c.layers[i]);
            let a = block
                .attn
                .forward(block.attn_norm.forward(x.clone()), &rope, start, slot);
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

    /// Düzleştirilmiş f32 logit'ler `[b*t, vocab]` (kayıp ve damıtma için).
    pub fn logits_flat(&self, inputs: Tensor<2, Int>) -> Tensor<2> {
        let [b, t] = inputs.dims();
        let h = self.hidden(inputs, None);
        let d = h.dims()[2];
        self.project(h.reshape([b * t, d])).cast(FloatDType::F32)
    }

    /// Dizi başına toplam log-olasılık `[b]`: hedefi [`IGNORE_ID`] olmayan konumlar üzerinden
    /// `Σ log p(hedef)`. DPO'da cevabın modele göre olasılığı için kullanılır.
    pub fn seq_logprob(&self, inputs: Tensor<2, Int>, targets: Tensor<2, Int>) -> Tensor<1> {
        let [b, t] = inputs.dims();
        let logp = burn::tensor::activation::log_softmax(self.logits_flat(inputs), 1);
        let flat = targets.reshape([b * t]);
        let mask = flat.clone().not_equal_elem(IGNORE_ID as i64).float();
        let picked = logp.gather(1, flat.unsqueeze_dim::<2>(1)).reshape([b * t]);
        (picked * mask).reshape([b, t]).sum_dim(1).reshape([b])
    }

    /// Bir sonraki token tahmini için ortalama çapraz entropi kaybı (f32).
    /// Hedefi [`IGNORE_ID`] olan konumlar kayba katılmaz.
    pub fn loss(&self, inputs: Tensor<2, Int>, targets: Tensor<2, Int>) -> Tensor<1> {
        let [b, t] = inputs.dims();
        let device = inputs.device();
        let logits = self.logits_flat(inputs);
        CrossEntropyLossConfig::new()
            .with_pad_tokens(Some(vec![IGNORE_ID]))
            .init(&device)
            .forward(logits, targets.reshape([b * t]))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use burn::tensor::TensorData;

    fn tiny(kv: Option<usize>) -> ModelConfig {
        ModelConfig {
            name: "test".into(),
            vocab_size: 64,
            d_model: 32,
            n_layers: 2,
            n_heads: 4,
            n_kv_heads: kv,
            ffn_hidden: 64,
            max_seq_len: 32,
            rope_theta: 10_000.0,
            rope_scaling: 1.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        }
    }

    fn toks(v: Vec<i64>, b: usize, t: usize, device: &Device) -> Tensor<2, Int> {
        Tensor::from_data(TensorData::new(v, [b, t]), device)
    }

    fn vec(t: Tensor<2>) -> Vec<f32> {
        t.to_data().convert::<f32>().try_to_vec::<f32>().unwrap()
    }

    #[test]
    fn shapes_and_param_count() {
        let device = Device::flex();
        for kv in [None, Some(2), Some(1)] {
            let cfg = tiny(kv);
            let model = cfg.init(&device);
            assert_eq!(model.num_params(), cfg.param_count(), "kv={kv:?}");
            let x = toks((0..16).collect(), 2, 8, &device);
            assert_eq!(model.forward(x.clone(), None).dims(), [2, 8, 64]);
            assert_eq!(model.forward_last(x, None).dims(), [2, 64]);
        }
        assert!(tiny(Some(1)).param_count() < tiny(None).param_count());
    }

    #[test]
    fn kv_cache_matches_full_forward() {
        let device = Device::flex();
        for kv in [None, Some(2)] {
            let cfg = tiny(kv);
            let model = cfg.init(&device);
            let seq: Vec<i64> = vec![3, 7, 1, 9, 12, 5];
            let full = model.forward(toks(seq.clone(), 1, 6, &device), None);
            let full_last = vec(full.narrow(1, 5, 1).reshape([1, 64]));

            let mut cache = KvCache::new(cfg.n_layers);
            let _ = model.forward_last(toks(seq[..3].to_vec(), 1, 3, &device), Some(&mut cache));
            let mut last = None;
            for &tk in &seq[3..] {
                last = Some(model.forward_last(toks(vec![tk], 1, 1, &device), Some(&mut cache)));
            }
            assert_eq!(cache.len, 6);
            for (a, b) in full_last.iter().zip(&vec(last.unwrap())) {
                assert!((a - b).abs() < 1e-4, "kv={kv:?}: {a} != {b}");
            }
        }
    }

    #[test]
    fn chunked_prefill_and_truncate_match_full() {
        let device = Device::flex();
        for kv in [None, Some(2)] {
            let cfg = tiny(kv);
            let model = cfg.init(&device);
            let seq: Vec<i64> = vec![3, 7, 1, 9, 12, 5, 33, 2];
            let full = vec(model
                .forward(toks(seq.clone(), 1, 8, &device), None)
                .reshape([8, 64]));
            // 3 + 5 token'lık iki parça (ikincisi ofsetli, maskeli).
            let mut cache = KvCache::new(cfg.n_layers);
            let _ = model.forward(toks(seq[..3].to_vec(), 1, 3, &device), Some(&mut cache));
            let part = vec(model
                .forward(toks(seq[3..].to_vec(), 1, 5, &device), Some(&mut cache))
                .reshape([5, 64]));
            for (a, b) in full[3 * 64..].iter().zip(&part) {
                assert!((a - b).abs() < 1e-4, "ofsetli geçiş farklı: {a} != {b}");
            }
            // Geri sar ve farklı bir devamla sürdür.
            cache.truncate(4);
            assert_eq!(cache.len, 4);
            let alt: Vec<i64> = vec![3, 7, 1, 9, 40, 41];
            let full_alt = vec(model
                .forward(toks(alt.clone(), 1, 6, &device), None)
                .reshape([6, 64]));
            let cont = vec(model
                .forward(toks(alt[4..].to_vec(), 1, 2, &device), Some(&mut cache))
                .reshape([2, 64]));
            for (a, b) in full_alt[4 * 64..].iter().zip(&cont) {
                assert!((a - b).abs() < 1e-4, "truncate sonrası farklı: {a} != {b}");
            }
        }
    }

    #[test]
    fn causal_no_future_leak() {
        let device = Device::flex();
        let model = tiny(Some(2)).init(&device);
        let a = model.forward(toks(vec![1, 2, 3, 4], 1, 4, &device), None);
        let b = model.forward(toks(vec![1, 2, 3, 60], 1, 4, &device), None);
        let a = vec(a.narrow(1, 0, 3).reshape([3, 64]));
        let b = vec(b.narrow(1, 0, 3).reshape([3, 64]));
        for (x, y) in a.iter().zip(&b) {
            assert!((x - y).abs() < 1e-5);
        }
    }

    #[test]
    fn rope_is_relative() {
        // RoPE ile q·k yalnızca konum farkına bağlıdır.
        let device = Device::flex();
        let like = Tensor::<3>::zeros([1, 1, 1], &device);
        let q = Tensor::<4>::random([1, 1, 1, 8], burn::tensor::Distribution::Default, &device);
        let k = Tensor::<4>::random([1, 1, 1, 8], burn::tensor::Distribution::Default, &device);
        let dot = |pq: usize, pk: usize| -> f32 {
            let rq = RopeTables::new(pq, 1, 8, 10_000.0, &like).apply(q.clone());
            let rk = RopeTables::new(pk, 1, 8, 10_000.0, &like).apply(k.clone());
            (rq * rk).sum().into_scalar()
        };
        assert!((dot(3, 1) - dot(13, 11)).abs() < 1e-4);
        assert!((dot(3, 1) - dot(3, 2)).abs() > 1e-6);
    }

    #[test]
    fn rope_scaling_extends_context() {
        let device = Device::flex();
        let mut cfg = tiny(None);
        cfg.rope_scaling = 2.0;
        assert_eq!(cfg.context_len(), 64);
        let model = cfg.init(&device);
        let x = toks((0..48).map(|i| i % 64).collect(), 1, 48, &device);
        assert_eq!(model.forward(x, None).dims(), [1, 48, 64]);
    }

    #[test]
    fn ignored_targets_do_not_count() {
        let device = Device::flex();
        let model = tiny(None).init(&device);
        let x = toks((1..9).collect(), 1, 8, &device);
        let full: f32 = model
            .loss(x.clone(), toks((2..10).collect(), 1, 8, &device))
            .into_scalar();
        // Yarısı yok sayılan hedefler: kayıp yalnızca son 4 konumdan.
        let masked: f32 = model
            .loss(x, toks(vec![0, 0, 0, 0, 6, 7, 8, 9], 1, 8, &device))
            .into_scalar();
        assert!(full.is_finite() && masked.is_finite());
        assert!((full - masked).abs() > 1e-6);
    }

    #[test]
    fn seq_logprob_matches_manual() {
        let device = Device::flex();
        let model = tiny(None).init(&device);
        let x = toks(vec![5, 6, 7, 8], 1, 4, &device);
        let y = toks(vec![0, 7, 0, 9], 1, 4, &device); // yalnızca 2 konum sayılır
        let lp: f32 = model.seq_logprob(x.clone(), y).into_scalar();
        let logits = vec(model.forward(x, None).reshape([4, 64]));
        let lsm = |row: &[f32], k: usize| {
            let m = row.iter().cloned().fold(f32::MIN, f32::max);
            let z: f32 = row.iter().map(|v| (v - m).exp()).sum();
            row[k] - m - z.ln()
        };
        let manual = lsm(&logits[64..128], 7) + lsm(&logits[192..256], 9);
        assert!((lp - manual).abs() < 1e-4, "{lp} != {manual}");
    }

    #[test]
    fn initial_loss_near_uniform() {
        let device = Device::flex();
        let model = tiny(None).init(&device);
        let x = toks((0..32).map(|i| i % 63 + 1).collect(), 2, 16, &device);
        let y = toks((1..33).map(|i| i % 63 + 1).collect(), 2, 16, &device);
        let loss: f32 = model.loss(x, y).into_scalar();
        let uniform = (64f32).ln();
        assert!(
            (loss - uniform).abs() < 0.3,
            "loss {loss}, beklenen ~{uniform}"
        );
    }
}
