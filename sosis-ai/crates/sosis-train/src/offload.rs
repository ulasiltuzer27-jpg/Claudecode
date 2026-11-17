//! Karışık hassasiyet + CPU'ya taşınan optimizer ("ZeRO-offload" benzeri).
//!
//! - **CPU / RAM**: f32 ana ağırlıklar, AdamW durumu (m, v) ve gradyan birikimi.
//! - **GPU / VRAM**: yalnızca bf16 (veya f32) çalışma kopyası + aktivasyonlar.
//!
//! Her micro-batch sonunda gradyanlar CPU'ya taşınıp f32 olarak biriktirilir; optimizer adımı
//! CPU'da yapılır ve güncel ağırlıklar GPU'ya geri kopyalanır. ~280M parametrelik bir model için
//! bu, 8 GB kartta ~3.4 GB VRAM'i RAM'e kaydırır.

use burn::module::{Module, ModuleMapper, ModuleVisitor, Param};
use burn::optim::{GradientsAccumulator, GradientsParams, ModuleOptimizer};
use burn::tensor::{Device, FloatDType, Tensor};
use sosis_model::SosyModel;

/// Tüm float parametreleri verilen dtype'a çevirir. `train_on` verilirse parametreler o (autodiff'li)
/// cihaza taşınır, cihazın checkpoint stratejisini alır ve yaprak-gradyan olarak işaretlenir.
pub struct CastMapper {
    pub dtype: FloatDType,
    pub train_on: Option<Device>,
}

impl ModuleMapper for CastMapper {
    fn map_float<const D: usize>(&mut self, param: Param<Tensor<D>>) -> Param<Tensor<D>> {
        let dtype = self.dtype;
        let target = self.train_on.clone();
        param.map(move |t| {
            let t = t.detach();
            match target {
                None => t.cast(dtype),
                Some(dev) => {
                    let mut t = t
                        .without_autodiff()
                        .to_device(&dev.clone().without_autodiff())
                        .cast(dtype)
                        .autodiff();
                    if let Some(s) = dev.gradient_checkpointing_strategy() {
                        t = t.with_gradient_checkpointing_strategy(s);
                    }
                    t.require_grad()
                }
            }
        })
    }
}

/// Gradyanları başka cihaza taşıyıp f32'ye çevirir (parametre kimlikleri korunur).
struct GradTransfer<'a> {
    from: GradientsParams,
    to: GradientsParams,
    device: &'a Device,
}

impl ModuleVisitor for GradTransfer<'_> {
    fn visit_float<const D: usize>(&mut self, param: &Param<Tensor<D>>) {
        if let Some(g) = self.from.remove::<D>(param.id) {
            self.to
                .register(param.id, g.to_device(self.device).cast(FloatDType::F32));
        }
    }
}

pub struct OffloadTrainer {
    master: SosyModel,
    working: SosyModel,
    optim: ModuleOptimizer,
    accum: GradientsAccumulator<SosyModel>,
    cpu: Device,
    gpu: Device,
    dtype: FloatDType,
}

impl OffloadTrainer {
    /// `master`: CPU'da f32 model (herhangi bir CPU cihazında; autodiff'li CPU cihazına taşınır —
    /// optimizer bunu ister, CPU'da ileri geçiş yapılmadığı için ek maliyeti yoktur).
    /// `gpu`: autodiff'li hesap cihazı.
    pub fn new(
        master: SosyModel,
        optim: ModuleOptimizer,
        cpu: Device,
        gpu: Device,
        dtype: FloatDType,
    ) -> Self {
        let cpu_ad = cpu.clone().autodiff();
        let master = master.map(&mut CastMapper {
            dtype: FloatDType::F32,
            train_on: Some(cpu_ad),
        });
        let cpu = cpu.without_autodiff();
        let working = Self::make_working(&master, &gpu, dtype);
        Self {
            master,
            working,
            optim,
            accum: GradientsAccumulator::new(),
            cpu,
            gpu,
            dtype,
        }
    }

    fn make_working(master: &SosyModel, gpu: &Device, dtype: FloatDType) -> SosyModel {
        master.clone().map(&mut CastMapper {
            dtype,
            train_on: Some(gpu.clone()),
        })
    }

    /// İleri/geri geçişlerin yapıldığı GPU kopyası.
    pub fn working(&self) -> &SosyModel {
        &self.working
    }

    /// Kaydedilecek f32 ana ağırlıklar.
    pub fn master(&self) -> &SosyModel {
        &self.master
    }

    pub fn optimizer(&self) -> &ModuleOptimizer {
        &self.optim
    }

    pub fn gpu(&self) -> &Device {
        &self.gpu
    }

    /// Bir micro-batch kaybının gradyanlarını hesaplar ve CPU'da biriktirir.
    pub fn backward(&mut self, loss: Tensor<1>) {
        let grads = GradientsParams::from_grads(loss.backward(), &self.working);
        let mut transfer = GradTransfer {
            from: grads,
            to: GradientsParams::new(),
            device: &self.cpu,
        };
        self.working.visit(&mut transfer);
        self.accum.accumulate(&self.master, transfer.to);
    }

    /// CPU'da optimizer adımı + GPU kopyasını tazeleme.
    pub fn step(&mut self, lr: f64) {
        let grads = self.accum.grads();
        self.master = self.optim.step(lr, self.master.clone(), grads);
        self.working = Self::make_working(&self.master, &self.gpu, self.dtype);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use burn::grad_clipping::GradientClippingConfig;
    use burn::optim::AdamWConfig;
    use burn::tensor::{Int, TensorData};
    use sosis_model::ModelConfig;

    fn cfg() -> ModelConfig {
        ModelConfig {
            name: "t".into(),
            vocab_size: 48,
            d_model: 32,
            n_layers: 2,
            n_heads: 4,
            n_kv_heads: Some(2),
            ffn_hidden: 64,
            max_seq_len: 16,
            rope_theta: 10_000.0,
            rope_scaling: 1.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        }
    }

    fn optim() -> ModuleOptimizer {
        AdamWConfig::new()
            .with_epsilon(1e-8)
            .with_grad_clipping(Some(GradientClippingConfig::Norm(1.0)))
            .init()
    }

    fn batch(device: &Device) -> (Tensor<2, Int>, Tensor<2, Int>) {
        let x: Vec<i64> = (0..16).map(|i| (i * 7) % 47 + 1).collect();
        let y: Vec<i64> = (0..16).map(|i| (i * 7 + 3) % 47 + 1).collect();
        (
            Tensor::from_data(TensorData::new(x, [2, 8]), device),
            Tensor::from_data(TensorData::new(y, [2, 8]), device),
        )
    }

    fn weights(m: &SosyModel) -> Vec<f32> {
        struct Collect(Vec<f32>);
        impl ModuleVisitor for Collect {
            fn visit_float<const D: usize>(&mut self, p: &Param<Tensor<D>>) {
                self.0.extend(
                    p.val()
                        .to_data()
                        .convert::<f32>()
                        .try_to_vec::<f32>()
                        .unwrap(),
                );
            }
        }
        let mut c = Collect(vec![]);
        m.visit(&mut c);
        c.0
    }

    /// f32 offload adımı, doğrudan autodiff cihazında yapılan adımla aynı ağırlıkları vermeli.
    #[test]
    fn offload_f32_matches_direct_step() {
        let cpu = Device::flex();
        let ad = Device::flex().autodiff();
        cpu.seed(3);
        let master = cfg().init(&cpu);

        // Doğrudan yol.
        let mut direct = master.clone().map(&mut CastMapper {
            dtype: FloatDType::F32,
            train_on: Some(ad.clone()),
        });
        let mut opt = optim();
        for _ in 0..3 {
            let (x, y) = batch(&ad);
            let g = GradientsParams::from_grads(direct.loss(x, y).backward(), &direct);
            direct = opt.step(1e-2, direct, g);
        }

        // Offload yolu (f32, aynı başlangıç).
        let mut off = OffloadTrainer::new(master, optim(), cpu, ad.clone(), FloatDType::F32);
        for _ in 0..3 {
            let (x, y) = batch(&ad);
            let loss = off.working().loss(x, y);
            off.backward(loss);
            off.step(1e-2);
        }

        let (a, b) = (weights(&direct), weights(off.master()));
        assert_eq!(a.len(), b.len());
        let max = a
            .iter()
            .zip(&b)
            .map(|(x, y)| (x - y).abs())
            .fold(0f32, f32::max);
        assert!(max < 1e-5, "en büyük fark {max}");
    }

    /// bf16 çalışma kopyasıyla da kayıp düşmeli; ana ağırlıklar f32 kalmalı.
    #[test]
    fn offload_bf16_learns() {
        let cpu = Device::flex();
        let ad = Device::flex().autodiff();
        if !ad.supports_dtype(FloatDType::BF16) {
            eprintln!("bu backend bf16 desteklemiyor, test atlandı");
            return;
        }
        cpu.seed(5);
        let mut off =
            OffloadTrainer::new(cfg().init(&cpu), optim(), cpu, ad.clone(), FloatDType::BF16);
        let (x, y) = batch(&ad);
        let first: f32 = off.working().loss(x.clone(), y.clone()).into_scalar();
        for _ in 0..40 {
            let loss = off.working().loss(x.clone(), y.clone());
            off.backward(loss);
            off.step(5e-3);
        }
        let last: f32 = off.working().loss(x, y).into_scalar();
        eprintln!("bf16 offload: {first:.3} → {last:.3}");
        assert!(last < first * 0.5, "{first} → {last}");
        let dt = off.master().clone().into_record();
        let _ = dt; // kayıt alınabiliyor
        assert!(weights(off.master()).iter().all(|w| w.is_finite()));
    }
}
