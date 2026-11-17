//! Backend / cihaz seçimi.

use anyhow::{bail, Result};
use burn::tensor::{Device, FloatDType};
use serde::{Deserialize, Serialize};

/// Kayan nokta hassasiyeti.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "lowercase")]
pub enum Precision {
    /// Tam hassasiyet (varsayılan, en kararlı).
    #[default]
    F32,
    /// bfloat16 — RTX 40 serisinde daha hızlı ve daha az VRAM; deneysel
    /// (parametreler de bf16 tutulur, ana-ağırlık kopyası yoktur).
    Bf16,
}

/// `"cuda"`, `"cpu"` veya `"auto"` adına göre cihaz oluşturur.
///
/// Hassasiyet ayarı tensör oluşturulmadan *önce* yapılmalıdır; bu yüzden ilk iş bu fonksiyon çağrılır.
pub fn select_device(backend: &str, precision: Precision) -> Result<Device> {
    let mut device = match backend {
        "cuda" | "gpu" => cuda()?,
        "cpu" => cpu()?,
        "auto" => cuda().or_else(|_| cpu())?,
        other => bail!("bilinmeyen backend: {other} (cuda | cpu | auto)"),
    };
    if precision == Precision::Bf16 {
        if let Err(e) = device.configure(FloatDType::BF16) {
            bail!("bf16 ayarlanamadı: {e:?}");
        }
    }
    Ok(device)
}

#[cfg(feature = "cuda")]
fn cuda() -> Result<Device> {
    use burn::tensor::DeviceType;
    // Sürücü/GPU yoksa sessizce çökmek yerine anlaşılır bir hata ver ("auto" CPU'ya düşer).
    let found = std::panic::catch_unwind(|| Device::enumerate(DeviceType::Cuda).len()).unwrap_or(0);
    if found == 0 {
        bail!("CUDA GPU bulunamadı (NVIDIA sürücüsü kurulu mu? `nvidia-smi` çalışıyor mu?)");
    }
    Ok(Device::cuda(0))
}

#[cfg(not(feature = "cuda"))]
fn cuda() -> Result<Device> {
    bail!("Bu derleme CUDA desteği içermiyor. `--features cuda` ile derleyin.")
}

#[cfg(feature = "cpu")]
fn cpu() -> Result<Device> {
    Ok(Device::flex())
}

#[cfg(not(feature = "cpu"))]
fn cpu() -> Result<Device> {
    bail!("Bu derleme CPU backend'i içermiyor. `--features cpu` ile derleyin.")
}
