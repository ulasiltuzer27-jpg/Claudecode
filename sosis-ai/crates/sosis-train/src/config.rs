//! `configs/*.toml` dosyalarının yapısı.

use std::path::{Path, PathBuf};

use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use sosis_model::{ModelConfig, Precision};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RunConfig {
    pub model: ModelConfig,
    pub train: TrainConfig,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrainConfig {
    /// `sosis prepare` çıktısı (train.bin, val.bin, meta.json).
    pub data_dir: PathBuf,
    /// Eğitilmiş tokenizer dosyası.
    pub tokenizer: PathBuf,
    /// Checkpoint'lerin ve logların yazılacağı klasör.
    pub out_dir: PathBuf,
    /// "cuda", "cpu" veya "auto".
    #[serde(default = "d_backend")]
    pub backend: String,
    #[serde(default)]
    pub precision: Precision,
    /// Aktivasyonları geri yayılımda yeniden hesaplayarak VRAM tasarrufu.
    #[serde(default)]
    pub gradient_checkpointing: bool,

    pub seq_len: usize,
    /// GPU'ya tek seferde giren dizi sayısı.
    pub micro_batch: usize,
    /// Bir optimizer adımı için biriktirilen micro-batch sayısı.
    pub grad_accum: usize,
    pub max_steps: usize,
    pub warmup_steps: usize,
    pub lr: f64,
    pub min_lr: f64,
    #[serde(default = "d_wd")]
    pub weight_decay: f32,
    #[serde(default = "d_b1")]
    pub beta1: f32,
    #[serde(default = "d_b2")]
    pub beta2: f32,
    /// Parametre tensörü başına gradyan normu sınırı (0 = kapalı).
    #[serde(default = "d_clip")]
    pub grad_clip: f32,

    #[serde(default = "d_log")]
    pub log_every: usize,
    pub eval_every: usize,
    pub eval_batches: usize,
    pub save_every: usize,
    /// Saklanacak en yeni checkpoint sayısı (en iyisi ayrıca tutulur).
    #[serde(default = "d_keep")]
    pub keep_checkpoints: usize,
    #[serde(default = "d_seed")]
    pub seed: u64,
}

fn d_backend() -> String {
    "auto".into()
}
fn d_wd() -> f32 {
    0.1
}
fn d_b1() -> f32 {
    0.9
}
fn d_b2() -> f32 {
    0.95
}
fn d_clip() -> f32 {
    1.0
}
fn d_log() -> usize {
    10
}
fn d_keep() -> usize {
    3
}
fn d_seed() -> u64 {
    1337
}

impl RunConfig {
    /// Yapılandırmayı okur; göreli yollar yapılandırma dosyasının klasörünün bir üstüne
    /// (proje köküne) göre değil, *çalışma dizinine* göre yorumlanır.
    pub fn load(path: impl AsRef<Path>) -> Result<Self> {
        let path = path.as_ref();
        let text = std::fs::read_to_string(path)
            .with_context(|| format!("okunamadı: {}", path.display()))?;
        let cfg: RunConfig =
            toml::from_str(&text).with_context(|| format!("geçersiz TOML: {}", path.display()))?;
        cfg.model.validate()?;
        anyhow::ensure!(
            cfg.train.seq_len <= cfg.model.max_seq_len,
            "seq_len > max_seq_len"
        );
        anyhow::ensure!(
            cfg.train.micro_batch > 0 && cfg.train.grad_accum > 0,
            "batch boyutları > 0 olmalı"
        );
        Ok(cfg)
    }

    pub fn to_toml(&self) -> Result<String> {
        Ok(toml::to_string_pretty(self)?)
    }

    /// Eğitim VRAM'inin *kaba* tahmini (GB): ağırlık + gradyan + AdamW durumu + aktivasyonlar + logit'ler.
    /// Gradient checkpointing ve backend'in bellek havuzu bunu değiştirir; yalnızca boyut seçimi için rehberdir.
    pub fn vram_estimate_gb(&self) -> (f64, f64, f64) {
        let m = &self.model;
        let bytes = if self.train.precision == Precision::Bf16 {
            2.0
        } else {
            4.0
        };
        let params = m.param_count() as f64;
        let states = params * (bytes * 2.0 + 8.0); // ağırlık + gradyan (+ f32 AdamW m, v)
        let tokens = (self.train.micro_batch * self.train.seq_len) as f64;
        let per_token = 12.0 * m.d_model as f64
            + 4.0 * m.ffn_hidden as f64
            + 2.0 * (m.n_heads * self.train.seq_len) as f64;
        let mut acts = tokens * per_token * m.n_layers as f64 * bytes;
        if self.train.gradient_checkpointing {
            acts *= 0.5;
        }
        let logits = tokens * m.vocab_size as f64 * 4.0 * 3.0;
        let gb = 1024f64.powi(3);
        (
            states / gb,
            (acts + logits) / gb,
            (states + acts + logits) / gb,
        )
    }

    /// Bir optimizer adımındaki token sayısı.
    pub fn tokens_per_step(&self) -> usize {
        self.train.seq_len * self.train.micro_batch * self.train.grad_accum
    }
}

impl TrainConfig {
    /// Doğrusal ısınma + kosinüs azalma.
    pub fn lr_at(&self, step: usize) -> f64 {
        if step < self.warmup_steps {
            return self.lr * (step + 1) as f64 / self.warmup_steps as f64;
        }
        if step >= self.max_steps {
            return self.min_lr;
        }
        let progress =
            (step - self.warmup_steps) as f64 / (self.max_steps - self.warmup_steps).max(1) as f64;
        let cos = 0.5 * (1.0 + (std::f64::consts::PI * progress).cos());
        self.min_lr + (self.lr - self.min_lr) * cos
    }
}
