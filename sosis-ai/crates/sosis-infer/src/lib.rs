//! # sosis-infer
//!
//! Eğitilmiş bir Sosy modelini yerelde çalıştırır: kod tamamlama ve FIM (ortayı doldurma).
//! Ollama, sunucu veya harici API yoktur; her şey bu süreçte, sizin GPU/CPU'nuzda döner.

mod sampling;

pub use sampling::{sample, SamplingParams};

use std::path::{Path, PathBuf};

use anyhow::{bail, Context, Result};
use burn::module::Module;
use burn::tensor::{Device, Int, Tensor, TensorData};
use rand::rngs::StdRng;
use rand::SeedableRng;
use sosis_model::{select_device, KvCache, ModelConfig, Precision, SosyModel};
use sosis_tokenizer::{
    StreamDecoder, Tokenizer, EOS, FILE_SEP, FIM_MIDDLE, FIM_PREFIX, FIM_SUFFIX,
};
use sosis_train::{latest_checkpoint, RunConfig, BEST_FILE, CONFIG_FILE, TOKENIZER_FILE};

pub struct Generator {
    model: SosyModel,
    pub tokenizer: Tokenizer,
    pub config: ModelConfig,
    device: Device,
}

/// Bir çalıştırma klasöründeki ağırlık dosyasını bulur: önce `best.bpk`, yoksa son checkpoint.
pub fn find_weights(run_dir: &Path) -> Option<PathBuf> {
    let best = run_dir.join(BEST_FILE);
    if best.exists() {
        return Some(best);
    }
    latest_checkpoint(run_dir).map(|d| d.join("model.bpk"))
}

impl Generator {
    /// `run_dir`: eğitim çıktı klasörü (`config.toml`, `tokenizer.json`, ağırlıklar).
    /// `weights` verilmezse `best.bpk` / son checkpoint kullanılır.
    pub fn load(run_dir: &Path, weights: Option<&Path>, backend: &str) -> Result<Self> {
        let cfg = RunConfig::load(run_dir.join(CONFIG_FILE)).with_context(|| {
            format!(
                "{} içinde config.toml yok — bu bir eğitim klasörü mü?",
                run_dir.display()
            )
        })?;
        let tokenizer = Tokenizer::load(run_dir.join(TOKENIZER_FILE))?;
        let weights = match weights {
            Some(w) => w.to_path_buf(),
            None => {
                find_weights(run_dir).context("ağırlık bulunamadı (best.bpk ya da checkpoint)")?
            }
        };
        let device = select_device(backend, Precision::F32)?;
        let model = cfg
            .model
            .init(&device)
            .try_load_file(&weights)
            .with_context(|| format!("ağırlıklar yüklenemedi: {}", weights.display()))?;
        Ok(Self {
            model,
            tokenizer,
            config: cfg.model,
            device,
        })
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
        }
    }

    fn step(&self, ids: &[u32], cache: &mut KvCache) -> Vec<f32> {
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

    /// Token düzeyinde üretim. `on_token` her yeni token için çağrılır; `false` dönerse durur.
    pub fn generate_ids(
        &self,
        prompt: &[u32],
        params: &SamplingParams,
        mut on_token: impl FnMut(u32) -> bool,
    ) -> Vec<u32> {
        if prompt.is_empty() {
            return vec![];
        }
        let max_ctx = self.config.max_seq_len;
        // Bağlam dolduğunda son %75'lik pencere ile yeniden başlanır.
        let keep = (max_ctx * 3 / 4).max(1);
        let mut rng = StdRng::seed_from_u64(params.seed);
        let mut cache = KvCache::new(self.model.n_layers());
        let mut history: Vec<u32> = prompt.to_vec();
        let start = history.len().saturating_sub(max_ctx - 1);
        let mut logits = self.step(&history[start..], &mut cache);
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
                logits = self.step(&history[s..], &mut cache);
            } else {
                logits = self.step(&[next], &mut cache);
            }
        }
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
        let mut dec = StreamDecoder::default();
        let mut out = String::new();
        self.generate_ids(ids, params, |id| {
            let piece = dec.push(&self.tokenizer, id);
            if !piece.is_empty() {
                on_text(&piece);
                out.push_str(&piece);
            }
            !params
                .stop
                .iter()
                .any(|s| !s.is_empty() && out.contains(s.as_str()))
        });
        if let Some(cut) = params
            .stop
            .iter()
            .filter(|s| !s.is_empty())
            .filter_map(|s| out.find(s.as_str()))
            .min()
        {
            out.truncate(cut);
        }
        out
    }

    /// Bir metnin modele göre perplexity'si (değerlendirme için).
    pub fn perplexity(&self, text: &str) -> Result<f32> {
        let mut ids = vec![FILE_SEP];
        ids.extend(self.tokenizer.encode(text));
        ids.truncate(self.config.max_seq_len + 1);
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
