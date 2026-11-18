//! `configs/*.toml` dosyalarının yapısı.

use std::path::{Path, PathBuf};

use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use sosis_model::{ModelConfig, Precision};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RunConfig {
    pub model: ModelConfig,
    pub train: TrainConfig,
    /// Varsa öğretmen modelden damıtma yapılır.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub distill: Option<DistillConfig>,
    #[serde(default)]
    pub inference: InferenceConfig,
    /// Varsa tercih eğitimi (DPO) yapılır.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub dpo: Option<crate::dpo::DpoConfig>,
}

/// `[inference]`: çıkarım varsayılanları (eğitimi etkilemez).
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct InferenceConfig {
    /// Varsayılan bağlam; eğitimdekinden uzunsa RoPE ölçeklemeyle uzatılır.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub context: Option<usize>,
    /// Ağırlıklar nicemli mi? (`"int8"`; `sosis quantize` yazar.)
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub quantized: Option<String>,
}

/// `[distill]`: öğrenci, öğretmenin olasılık dağılımını da taklit eder.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DistillConfig {
    /// Öğretmenin eğitim klasörü (ör. runs/sosy-pro-1.0). Aynı tokenizer'ı kullanmalı.
    pub teacher: PathBuf,
    /// Kayıp = alpha·CE + (1−alpha)·T²·KL.
    #[serde(default = "d_alpha")]
    pub alpha: f32,
    #[serde(default = "d_temp")]
    pub temperature: f32,
}

fn d_alpha() -> f32 {
    0.5
}
fn d_temp() -> f32 {
    2.0
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
    /// f32 ana ağırlıkları ve AdamW durumunu RAM'de tutar; GPU'da yalnızca çalışma kopyası kalır.
    #[serde(default)]
    pub optimizer_offload: bool,
    /// LoRA / QLoRA: taban (init_from) donar, yalnızca küçük adaptörler eğitilir.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub lora: Option<sosis_model::lora::LoraConfig>,
    /// Eğitime bu ağırlıklarla başla (eğitim klasörü veya .bpk). `--resume` varsa yok sayılır.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub init_from: Option<PathBuf>,
    /// Bu sürecin kullanması beklenen en fazla RAM (GB); aşılırsa uyarılır.
    #[serde(default = "d_ram")]
    pub max_ram_gb: f64,
    /// Her N adımda (checkpoint kaydında) derle-çalıştır bench'i çalıştır; en iyisi best_bench.bpk.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub bench_every: Option<usize>,
    #[serde(default = "d_bench_suite")]
    pub bench_suite: PathBuf,
    #[serde(default = "d_one")]
    pub bench_samples: usize,
    /// Bench'in çalışacağı backend; "cpu" eğitimin VRAM'ine dokunmaz.
    #[serde(default = "d_bench_backend")]
    pub bench_backend: String,
    #[serde(default = "d_bench_tokens")]
    pub bench_max_tokens: usize,
    /// Arka planda önceden hazırlanan adım sayısı.
    #[serde(default = "d_prefetch")]
    pub prefetch_steps: usize,

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
fn d_ram() -> f64 {
    sosis_data::ram::DEFAULT_MAX_RAM_GB
}
fn d_bench_suite() -> PathBuf {
    "benchmarks".into()
}
fn d_one() -> usize {
    1
}
fn d_bench_backend() -> String {
    "cpu".into()
}
fn d_bench_tokens() -> usize {
    256
}
fn d_prefetch() -> usize {
    2
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

    /// Eğitim belleğinin *kaba* tahmini (GB). Gradient checkpointing ve backend'in bellek havuzu
    /// bunu değiştirir; yalnızca boyut seçimi için rehberdir.
    pub fn memory_estimate(&self) -> MemoryEstimate {
        let m = &self.model;
        let t = &self.train;
        let work = if t.precision == Precision::Bf16 {
            2.0
        } else {
            4.0
        };
        let params = m.param_count() as f64;
        let gb = 1024f64.powi(3);
        // Çalışma kopyası + gradyanları GPU'da; f32 ana kopya + AdamW (m, v) + f32 biriktirme.
        let (gpu_states, ram_states) = if t.optimizer_offload {
            (params * work * 2.0, params * 4.0 * 4.0)
        } else {
            (params * (work * 2.0 + 8.0), 0.0)
        };
        let tokens = (t.micro_batch * t.seq_len) as f64;
        let per_token = 12.0 * m.d_model as f64
            + 4.0 * m.ffn_hidden as f64
            + 2.0 * (m.n_heads * t.seq_len) as f64;
        let mut acts = tokens * per_token * m.n_layers as f64 * work;
        if t.gradient_checkpointing {
            acts *= 0.5;
        }
        let logits = tokens * m.vocab_size as f64 * (work + 8.0);
        // Ön-yüklenen batch'ler + süreç tabanı (~1.5 GB). Damıtmada öğretmen ayrıca eklenir.
        let prefetch = (t.prefetch_steps * t.grad_accum) as f64 * tokens * 16.0;
        MemoryEstimate {
            vram_states_gb: gpu_states / gb,
            vram_activations_gb: (acts + logits) / gb,
            vram_total_gb: (gpu_states + acts + logits) / gb + 0.4,
            ram_total_gb: (ram_states + prefetch) / gb + 1.5,
        }
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

#[derive(Debug, Clone, Copy)]
pub struct MemoryEstimate {
    pub vram_states_gb: f64,
    pub vram_activations_gb: f64,
    /// CUDA bağlamı (~0.4 GB) dahil.
    pub vram_total_gb: f64,
    pub ram_total_gb: f64,
}
