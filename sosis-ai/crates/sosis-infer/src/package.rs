//! `.sosy` model paketi: config + tokenizer + ağırlıklar + model kartı tek dosyada (tar).
//!
//! Paket her zaman **tek başına çalışır**: LoRA adaptörü tabana katılır, istenirse int8'e
//! nicemlenir. `-r model.sosy` ile doğrudan kullanılır; ilk kullanımda `~/.cache/sosis/<özet>/`
//! klasörüne açılır.

use std::collections::hash_map::DefaultHasher;
use std::fs;
use std::hash::{Hash, Hasher};
use std::path::{Path, PathBuf};

use anyhow::{bail, Context, Result};
use burn::module::Module;
use sosis_model::{select_device, Precision};
use sosis_train::{find_weights, RunConfig, BEST_FILE, CONFIG_FILE, TOKENIZER_FILE};

use crate::load_model;

pub const EXTENSION: &str = "sosy";
pub const CARD_FILE: &str = "MODEL_CARD.md";

fn cache_root() -> PathBuf {
    std::env::var_os("HOME")
        .or_else(|| std::env::var_os("USERPROFILE"))
        .map(|h| PathBuf::from(h).join(".cache").join("sosis"))
        .unwrap_or_else(|| std::env::temp_dir().join("sosis-cache"))
}

/// Model kartı (Markdown).
fn model_card(cfg: &RunConfig, run: &Path, int8: bool) -> String {
    let m = &cfg.model;
    let state = sosis_train::latest_checkpoint(run)
        .and_then(|d| fs::read_to_string(d.join("state.json")).ok())
        .and_then(|s| serde_json::from_str::<serde_json::Value>(&s).ok());
    let tokens = state
        .as_ref()
        .and_then(|s| s["tokens_seen"].as_u64())
        .map(|t| {
            if t >= 1_000_000_000 {
                format!("{:.2} milyar", t as f64 / 1e9)
            } else {
                format!("{:.1} milyon", t as f64 / 1e6)
            }
        })
        .unwrap_or_else(|| "?".into());
    let val = state
        .as_ref()
        .and_then(|s| s["best_val_loss"].as_f64())
        .filter(|v| v.is_finite())
        .map(|v| format!("{v:.3} (perplexity {:.1})", v.exp()))
        .unwrap_or_else(|| "?".into());
    let bench = fs::read_to_string(run.join("bench.txt"))
        .unwrap_or_else(|_| "ölçülmedi (`sosis bench`)\n".into());
    format!(
        "# {name}\n\nSosis AI ile **sıfırdan** eğitilmiş kod modeli. Harici API veya servis kullanmadan yerelde çalışır.\n\n\
         | | |\n|---|---|\n\
         | Parametre | {params:.1}M |\n\
         | Mimari | {layers} katman, d={d}, {heads} kafa ({kv} KV), SwiGLU, RoPE |\n\
         | Sözlük | {vocab} |\n\
         | Bağlam | eğitim {ctx}, çıkarım {ictx} |\n\
         | Ağırlıklar | {wt} |\n\
         | Görülen token | {tokens} |\n\
         | En iyi doğrulama kaybı | {val} |\n\n\
         ## Değerlendirme\n\n```\n{bench}```\n\n\
         ## Kullanım\n\n```\nsosis serve -r {file}\nsosis generate -r {file} --file main.rs -p \"fn \"\n```\n\n\
         ## Sınırlar\n\nKüçük, sıfırdan eğitilmiş bir modeldir: kısa fonksiyonlarda ve kalıplarda iyidir; uzun \
         programlarda hata yapabilir. Üretilen kodu çalıştırmadan önce gözden geçirin.\n",
        name = m.name,
        params = m.param_count() as f64 / 1e6,
        layers = m.n_layers,
        d = m.d_model,
        heads = m.n_heads,
        kv = m.kv_heads(),
        vocab = m.vocab_size,
        ctx = m.max_seq_len,
        ictx = cfg.inference.context.unwrap_or(m.context_len()),
        wt = if int8 { "int8 (Linear) + f32" } else { "f32" },
        file = format!("model.{EXTENSION}"),
    )
}

/// Bir eğitim klasöründen tek başına çalışan `.sosy` paketi üretir.
pub fn pack(run: &Path, out: &Path, int8: bool) -> Result<()> {
    let mut cfg = RunConfig::load(run.join(CONFIG_FILE))?;
    let weights = find_weights(run).context("ağırlık bulunamadı")?;
    let device = select_device("cpu", Precision::F32)?;
    // LoRA ise birleştirilir; nicemli ise nicemli yüklenir.
    let mut model = load_model(&cfg, &weights, &device)?;
    let already_q = cfg.inference.quantized.is_some();
    if int8 && !already_q {
        model = sosis_model::quant::quantize_int8(model, &device);
        cfg.inference.quantized = Some("int8".into());
    }
    cfg.train.lora = None;
    cfg.train.init_from = None;
    cfg.distill = None;
    cfg.dpo = None;

    let stage = std::env::temp_dir().join(format!("sosis_pack_{}", std::process::id()));
    let _ = fs::remove_dir_all(&stage);
    fs::create_dir_all(&stage)?;
    model
        .save_file(stage.join(BEST_FILE))
        .map_err(|e| anyhow::anyhow!("{e:?}"))?;
    fs::write(stage.join(CONFIG_FILE), cfg.to_toml()?)?;
    fs::copy(run.join(TOKENIZER_FILE), stage.join(TOKENIZER_FILE))?;
    fs::write(
        stage.join(CARD_FILE),
        model_card(&cfg, run, int8 || already_q),
    )?;

    if let Some(dir) = out.parent().filter(|p| !p.as_os_str().is_empty()) {
        fs::create_dir_all(dir)?;
    }
    let mut tar = tar::Builder::new(fs::File::create(out)?);
    for f in [CARD_FILE, CONFIG_FILE, TOKENIZER_FILE, BEST_FILE] {
        tar.append_path_with_name(stage.join(f), f)?;
    }
    tar.into_inner()?;
    fs::remove_dir_all(&stage).ok();
    Ok(())
}

/// `.sosy` dosyasıysa önbelleğe açıp klasörünü, değilse yolu olduğu gibi döndürür.
pub fn resolve_run(path: &Path) -> Result<PathBuf> {
    let is_pkg = path.is_file() && path.extension().is_some_and(|e| e == EXTENSION);
    if !is_pkg {
        return Ok(path.to_path_buf());
    }
    let meta = fs::metadata(path)?;
    let mut h = DefaultHasher::new();
    fs::canonicalize(path)?.hash(&mut h);
    meta.len().hash(&mut h);
    meta.modified().ok().hash(&mut h);
    let dir = cache_root().join(format!("{:016x}", h.finish()));
    if dir.join(CONFIG_FILE).exists() && dir.join(BEST_FILE).exists() {
        return Ok(dir);
    }
    let tmp = dir.with_extension("tmp");
    let _ = fs::remove_dir_all(&tmp);
    fs::create_dir_all(&tmp)?;
    let mut archive = tar::Archive::new(fs::File::open(path)?);
    for entry in archive.entries()? {
        let mut entry = entry?;
        // unpack_in, klasör dışına çıkan yolları (../) reddeder.
        if !entry.unpack_in(&tmp)? {
            bail!("pakette güvenli olmayan dosya yolu: {:?}", entry.path()?);
        }
    }
    for f in [CONFIG_FILE, TOKENIZER_FILE, BEST_FILE] {
        if !tmp.join(f).exists() {
            bail!("geçersiz paket: {f} yok");
        }
    }
    let _ = fs::remove_dir_all(&dir);
    fs::rename(&tmp, &dir)?;
    Ok(dir)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_path_traversal() {
        let dir = std::env::temp_dir().join(format!("sosis_pkg_{}", std::process::id()));
        fs::create_dir_all(&dir).unwrap();
        let pkg = dir.join("kotu.sosy");
        {
            let mut b = tar::Builder::new(fs::File::create(&pkg).unwrap());
            let data = b"x";
            let mut h = tar::Header::new_old();
            // Builder'ın yol kontrolünü atlayarak "../" içeren bir girdi yaz.
            let name = b"../kacak.txt";
            h.as_old_mut().name[..name.len()].copy_from_slice(name);
            h.set_size(1);
            h.set_mode(0o644);
            h.set_cksum();
            b.append(&h, &data[..]).unwrap();
            b.into_inner().unwrap();
        }
        let r = resolve_run(&pkg);
        assert!(r.is_err(), "kötü paket kabul edildi");
        assert!(!cache_root().join("kacak.txt").exists());
        fs::remove_dir_all(&dir).ok();
    }
}
