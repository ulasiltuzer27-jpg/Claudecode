//! # sosis-infer
//!
//! Eğitilmiş bir Sosy modelini yerelde çalıştırır: kod tamamlama ve FIM (ortayı doldurma).
//! Ollama, sunucu veya harici API yoktur; her şey bu süreçte, sizin GPU/CPU'nuzda döner.

pub mod chat;
pub mod rag;
mod sampling;
pub mod speculative;

pub use sampling::{distribution, sample, sample_dense, SamplingParams};

use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Mutex;

use anyhow::{bail, Context, Result};
use burn::module::Module;
use burn::tensor::{Device, Int, Tensor, TensorData};
use rand::rngs::StdRng;
use rand::SeedableRng;
use sosis_model::{select_device, KvCache, ModelConfig, Precision, SosyModel};
use sosis_tokenizer::{
    StreamDecoder, Tokenizer, EOS, FILE_SEP, FIM_MIDDLE, FIM_PREFIX, FIM_SUFFIX,
};
pub use sosis_train::find_weights;
use sosis_train::{RunConfig, CONFIG_FILE, TOKENIZER_FILE};

pub struct Generator {
    model: SosyModel,
    pub tokenizer: Tokenizer,
    pub config: ModelConfig,
    device: Device,
    /// Önek önbelleği: son üretimde KV-cache'te kalan token'lar ve önbelleğin kendisi.
    prefix: Mutex<Option<(Vec<u32>, KvCache)>>,
    /// Son üretimde önbellekten yeniden kullanılan token sayısı (istatistik/test).
    pub last_reused: AtomicUsize,
    /// Spekülatif kod çözme için taslak model ve tur başına taslak sayısı.
    draft: Option<(Box<Generator>, usize)>,
    /// Son spekülatif üretimin istatistikleri.
    pub last_spec: Mutex<Option<speculative::SpecStats>>,
}

/// İki dizinin ortak önek uzunluğu.
fn common_prefix(a: &[u32], b: &[u32]) -> usize {
    a.iter().zip(b).take_while(|(x, y)| x == y).count()
}

/// Model yükleme seçenekleri.
#[derive(Debug, Clone)]
pub struct LoadOptions {
    /// Belirli bir ağırlık dosyası (varsayılan: `best.bpk` ya da son checkpoint).
    pub weights: Option<PathBuf>,
    /// "cuda", "cpu" veya "auto".
    pub backend: String,
    /// Eğitim bağlamından uzunsa RoPE ölçeklemeyle uzatılır (ör. 2048'de eğitilmiş → 4096).
    pub context: Option<usize>,
}

impl Default for LoadOptions {
    fn default() -> Self {
        Self {
            weights: None,
            backend: "auto".into(),
            context: None,
        }
    }
}

impl Generator {
    /// `run_dir`: eğitim çıktı klasörü (`config.toml`, `tokenizer.json`, ağırlıklar).
    pub fn load(run_dir: &Path, opts: &LoadOptions) -> Result<Self> {
        let mut cfg = RunConfig::load(run_dir.join(CONFIG_FILE)).with_context(|| {
            format!(
                "{} içinde config.toml yok — bu bir eğitim klasörü mü?",
                run_dir.display()
            )
        })?;
        if let Some(ctx) = opts.context.or(cfg.inference.context) {
            let s = ctx as f32 / cfg.model.max_seq_len as f32;
            cfg.model.rope_scaling = s.max(cfg.model.rope_scaling).max(1.0);
        }
        let tokenizer = Tokenizer::load(run_dir.join(TOKENIZER_FILE))?;
        let weights = match &opts.weights {
            Some(w) => w.clone(),
            None => {
                find_weights(run_dir).context("ağırlık bulunamadı (best.bpk ya da checkpoint)")?
            }
        };
        let device = select_device(&opts.backend, Precision::F32)?;
        let mut skeleton = cfg.model.init(&device);
        match cfg.inference.quantized.as_deref() {
            None => {}
            Some("int8") => skeleton = sosis_model::quant::quantize_int8(skeleton, &device),
            Some(other) => bail!("bilinmeyen nicemleme: {other}"),
        }
        let model = skeleton
            .try_load_file(&weights)
            .with_context(|| format!("ağırlıklar yüklenemedi: {}", weights.display()))?;
        Ok(Self {
            model,
            tokenizer,
            config: cfg.model,
            device,
            prefix: Mutex::new(None),
            last_reused: AtomicUsize::new(0),
            draft: None,
            last_spec: Mutex::new(None),
        })
    }

    /// Kullanılabilir bağlam uzunluğu (ölçekleme dahil).
    pub fn context_len(&self) -> usize {
        self.config.context_len()
    }

    /// Bellekteki bir modelden (ör. testlerde) üretici oluşturur.
    pub fn from_parts(
        model: SosyModel,
        tokenizer: Tokenizer,
        config: ModelConfig,
        device: Device,
    ) -> Self {
        Self {
            model: model.valid(),
            tokenizer,
            config,
            device,
            prefix: Mutex::new(None),
            last_reused: AtomicUsize::new(0),
            draft: None,
            last_spec: Mutex::new(None),
        }
    }

    pub(crate) fn step(&self, ids: &[u32], cache: &mut KvCache) -> Vec<f32> {
        let data: Vec<i64> = ids.iter().map(|&i| i as i64).collect();
        let x: Tensor<2, Int> =
            Tensor::from_data(TensorData::new(data, [1, ids.len()]), &self.device);
        let logits = self.model.forward_last(x, Some(cache));
        logits
            .reshape([self.config.vocab_size])
            .to_data()
            .convert::<f32>()
            .try_to_vec::<f32>()
            .expect("logit okunamadı")
    }

    /// Her konumun logit'leri (spekülatif doğrulama için): `ids.len()` adet vektör.
    pub(crate) fn step_all(&self, ids: &[u32], cache: &mut KvCache) -> Vec<Vec<f32>> {
        let data: Vec<i64> = ids.iter().map(|&i| i as i64).collect();
        let x: Tensor<2, Int> =
            Tensor::from_data(TensorData::new(data, [1, ids.len()]), &self.device);
        let v = self.config.vocab_size;
        let flat = self
            .model
            .forward(x, Some(cache))
            .reshape([ids.len() * v])
            .to_data()
            .convert::<f32>()
            .try_to_vec::<f32>()
            .expect("logit okunamadı");
        flat.chunks(v).map(|c| c.to_vec()).collect()
    }

    /// `tokens` için önbelleği hazırlar ve son konumun logit'lerini döndürür. Bir önceki
    /// üretimle ortak önek varsa o kısım yeniden hesaplanmaz.
    pub(crate) fn prefill(&self, tokens: &[u32]) -> (KvCache, Vec<f32>) {
        let previous = self.prefix.lock().unwrap().take();
        if let Some((ids, mut cache)) = previous {
            // Son token'ın logit'leri saklanmadığı için en az bir token yeniden işlenir.
            let common = common_prefix(&ids, tokens)
                .min(tokens.len() - 1)
                .min(cache.len);
            if common > 0 {
                cache.truncate(common);
                self.last_reused.store(common, Ordering::Relaxed);
                let logits = self.step(&tokens[common..], &mut cache);
                return (cache, logits);
            }
        }
        self.last_reused.store(0, Ordering::Relaxed);
        let mut cache = KvCache::new(self.model.n_layers());
        let logits = self.step(tokens, &mut cache);
        (cache, logits)
    }

    /// Önek önbelleğini temizler.
    pub fn clear_prefix_cache(&self) {
        *self.prefix.lock().unwrap() = None;
    }

    /// Spekülatif kod çözmeyi açar: `draft` (aynı tokenizer'lı küçük model) her turda `k`
    /// token önerir. Tamamlama, FIM, sohbet ve web arayüzü otomatik olarak hızlanır.
    pub fn with_draft(mut self, draft: Generator, k: usize) -> Result<Self> {
        speculative::check_compatible(&self, &draft)?;
        self.draft = Some((Box::new(draft), k.max(1)));
        Ok(self)
    }

    pub fn draft_name(&self) -> Option<&str> {
        self.draft.as_ref().map(|(d, _)| d.config.name.as_str())
    }

    /// Token düzeyinde üretim. `on_token` her yeni token için çağrılır; `false` dönerse durur.
    pub fn generate_ids(
        &self,
        prompt: &[u32],
        params: &SamplingParams,
        on_token: impl FnMut(u32) -> bool,
    ) -> Vec<u32> {
        match &self.draft {
            Some((draft, k)) => {
                let (out, st) = speculative::generate(self, draft, prompt, params, *k, on_token);
                *self.last_spec.lock().unwrap() = Some(st);
                out
            }
            None => self.generate_plain(prompt, params, on_token),
        }
    }

    /// Yalnızca bu modelle (taslaksız) üretim.
    pub(crate) fn generate_plain(
        &self,
        prompt: &[u32],
        params: &SamplingParams,
        mut on_token: impl FnMut(u32) -> bool,
    ) -> Vec<u32> {
        if prompt.is_empty() {
            return vec![];
        }
        let max_ctx = self.config.context_len();
        // Bağlam dolduğunda son %75'lik pencere ile yeniden başlanır.
        let keep = (max_ctx * 3 / 4).max(1);
        let mut rng = StdRng::seed_from_u64(params.seed);
        let mut history: Vec<u32> = prompt.to_vec();
        let start = history.len().saturating_sub(max_ctx - 1);
        // Önbellekteki token'lar (konumlarıyla birebir).
        let mut in_cache: Vec<u32> = history[start..].to_vec();
        let (mut cache, mut logits) = self.prefill(&in_cache);
        let mut generated = Vec::new();

        for _ in 0..params.max_new_tokens {
            let next = sample(&mut logits, &history, params, &mut rng);
            if next == EOS || next == FILE_SEP {
                break;
            }
            generated.push(next);
            history.push(next);
            if !on_token(next) {
                break;
            }
            if cache.len >= max_ctx {
                cache.clear();
                let s = history.len() - keep.min(history.len());
                in_cache = history[s..].to_vec();
                logits = self.step(&in_cache, &mut cache);
            } else {
                in_cache.push(next);
                logits = self.step(&[next], &mut cache);
            }
        }
        *self.prefix.lock().unwrap() = Some((in_cache, cache));
        generated
    }

    fn header(&self, file: Option<&str>) -> Vec<u32> {
        let mut ids = vec![FILE_SEP];
        if let Some(f) = file.filter(|f| !f.is_empty()) {
            ids.extend(self.tokenizer.encode(&format!("{f}\n")));
        }
        ids
    }

    /// Kod tamamlama: `prompt`'un devamını üretir. `file` (ör. `main.rs`) dil ipucu verir.
    pub fn complete(
        &self,
        prompt: &str,
        file: Option<&str>,
        params: &SamplingParams,
        mut on_text: impl FnMut(&str),
    ) -> String {
        let mut ids = self.header(file);
        ids.extend(self.tokenizer.encode(prompt));
        self.stream(&ids, params, &mut on_text)
    }

    /// FIM: `prefix` ile `suffix` arasına gelecek kodu üretir.
    pub fn fill_in_middle(
        &self,
        prefix: &str,
        suffix: &str,
        file: Option<&str>,
        params: &SamplingParams,
        mut on_text: impl FnMut(&str),
    ) -> String {
        let mut ids = self.header(file);
        ids.push(FIM_PREFIX);
        ids.extend(self.tokenizer.encode(prefix));
        ids.push(FIM_SUFFIX);
        ids.extend(self.tokenizer.encode(suffix));
        ids.push(FIM_MIDDLE);
        self.stream(&ids, params, &mut on_text)
    }

    fn stream(
        &self,
        ids: &[u32],
        params: &SamplingParams,
        on_text: &mut impl FnMut(&str),
    ) -> String {
        self.stream_until(ids, params, on_text, &mut |_| None)
    }

    /// Hazır token dizisinden akışlı üretim. `until(çıktı)` bir kesme noktası döndürürse üretim
    /// durur ve çıktı oraya kadar kesilir. `params.stop` dizileri de ayrıca uygulanır.
    pub fn stream_until(
        &self,
        ids: &[u32],
        params: &SamplingParams,
        on_text: &mut dyn FnMut(&str),
        until: &mut dyn FnMut(&str) -> Option<usize>,
    ) -> String {
        let mut dec = StreamDecoder::default();
        let mut out = String::new();
        let mut cut: Option<usize> = None;
        let stop_at = |out: &str| {
            params
                .stop
                .iter()
                .filter(|s| !s.is_empty())
                .filter_map(|s| out.find(s.as_str()))
                .min()
        };
        self.generate_ids(ids, params, |id| {
            let piece = dec.push(&self.tokenizer, id);
            if piece.is_empty() {
                return true;
            }
            out.push_str(&piece);
            if let Some(c) = until(&out).or_else(|| stop_at(&out)) {
                // Yalnızca kesme noktasına kadar olan kısmı akıt.
                let shown = out.len() - piece.len();
                if c > shown {
                    on_text(&out[shown..c]);
                }
                cut = Some(c);
                return false;
            }
            on_text(&piece);
            true
        });
        if let Some(c) = cut.or_else(|| stop_at(&out)) {
            out.truncate(c);
        }
        out
    }

    /// Kod tamamlama; `until` (ör. "fonksiyon bitti mi?") bir kesme noktası verince durur.
    pub fn complete_until(
        &self,
        prompt: &str,
        file: Option<&str>,
        params: &SamplingParams,
        until: &mut dyn FnMut(&str) -> Option<usize>,
    ) -> String {
        let mut ids = self.header(file);
        ids.extend(self.tokenizer.encode(prompt));
        self.stream_until(&ids, params, &mut |_| {}, until)
    }

    /// Proje bağlamıyla (RAG) kod tamamlama: ilgili parçalar istemin önüne eklenir.
    pub fn complete_with_index(
        &self,
        prompt: &str,
        file: Option<&str>,
        index: &rag::CodeIndex,
        params: &SamplingParams,
        on_text: &mut dyn FnMut(&str),
    ) -> String {
        let main = self.encode_prompt(prompt, file);
        let budget = self
            .context_len()
            .saturating_sub(main.len() + params.max_new_tokens)
            / 2;
        let query = format!("{} {}", file.unwrap_or(""), prompt);
        let chunks = index.retrieve(&query, &self.tokenizer, budget);
        let mut ids = rag::context_ids(&self.tokenizer, &chunks);
        ids.extend(main);
        self.stream_until(&ids, params, on_text, &mut |_| None)
    }

    /// Belge başlığı + metin → token'lar (`<|file_sep|>dosya\n...`).
    pub fn encode_prompt(&self, prompt: &str, file: Option<&str>) -> Vec<u32> {
        let mut ids = self.header(file);
        ids.extend(self.tokenizer.encode(prompt));
        ids
    }

    /// Bir metnin modele göre perplexity'si (değerlendirme için).
    pub fn perplexity(&self, text: &str) -> Result<f32> {
        let mut ids = vec![FILE_SEP];
        ids.extend(self.tokenizer.encode(text));
        ids.truncate(self.config.context_len() + 1);
        if ids.len() < 2 {
            bail!("metin çok kısa");
        }
        let n = ids.len() - 1;
        let to = |v: &[u32]| -> Tensor<2, Int> {
            Tensor::from_data(
                TensorData::new(v.iter().map(|&i| i as i64).collect::<Vec<_>>(), [1, n]),
                &self.device,
            )
        };
        let loss: f32 = self.model.loss(to(&ids[..n]), to(&ids[1..])).into_scalar();
        Ok(loss.exp())
    }
}

#[cfg(test)]
pub(crate) mod test_util {
    use super::*;
    use sosis_tokenizer::BpeTrainer;

    pub fn tiny_config(seed_name: &str, d_model: usize, layers: usize) -> ModelConfig {
        ModelConfig {
            name: seed_name.into(),
            vocab_size: 320,
            d_model,
            n_layers: layers,
            n_heads: 4,
            n_kv_heads: Some(2),
            ffn_hidden: d_model * 2,
            max_seq_len: 64,
            rope_theta: 10_000.0,
            rope_scaling: 1.0,
            norm_eps: 1e-5,
            init_std: 0.5, // rastgele ama belirgin dağılımlar
        }
    }

    pub fn tokenizer() -> Tokenizer {
        let mut t = BpeTrainer::new();
        t.feed("fn main() { let x = 1; println!(\"{}\", x); }");
        t.train(300, |_, _| {})
    }

    /// `Device::seed` süreç geneli olduğundan paralel testlerde model oluşturma sıralanır.
    static SEED_LOCK: std::sync::Mutex<()> = std::sync::Mutex::new(());

    pub fn generator(seed: u64, d_model: usize, layers: usize) -> Generator {
        let _guard = SEED_LOCK.lock().unwrap_or_else(|e| e.into_inner());
        let device = Device::flex();
        device.seed(seed);
        let cfg = tiny_config("t", d_model, layers);
        let model = cfg.init(&device);
        Generator::from_parts(model, tokenizer(), cfg, device)
    }

    pub fn greedy(n: usize) -> SamplingParams {
        SamplingParams {
            temperature: 0.0,
            repetition_penalty: 1.0,
            max_new_tokens: n,
            ..Default::default()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::test_util::*;
    use super::*;

    #[test]
    fn prefix_cache_reuses_and_matches_fresh() {
        let g = generator(11, 32, 2);
        let p = greedy(12);
        let prompt: Vec<u32> = (20..40).collect();
        let a = g.generate_ids(&prompt, &p, |_| true);
        assert_eq!(g.last_reused.load(Ordering::Relaxed), 0);

        // Aynı istem: öneki yeniden kullanır, çıktı aynı.
        let b = g.generate_ids(&prompt, &p, |_| true);
        assert!(g.last_reused.load(Ordering::Relaxed) >= prompt.len() - 1);
        assert_eq!(a, b);

        // Sohbet gibi: önceki istem + cevap + yeni soru.
        let mut longer = prompt.clone();
        longer.extend(&a);
        longer.extend([50, 51, 52]);
        let cached = g.generate_ids(&longer, &p, |_| true);
        assert!(g.last_reused.load(Ordering::Relaxed) > prompt.len());
        g.clear_prefix_cache();
        let fresh = g.generate_ids(&longer, &p, |_| true);
        assert_eq!(g.last_reused.load(Ordering::Relaxed), 0);
        assert_eq!(cached, fresh);
    }
}
