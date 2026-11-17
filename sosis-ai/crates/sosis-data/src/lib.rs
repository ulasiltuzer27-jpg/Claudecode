//! # sosis-data
//!
//! Ham kod → eğitim token'ları. Belge biçimi:
//!
//! ```text
//! <|file_sep|>yol/dosya.rs\n<içerik><|endoftext|>
//! <|file_sep|>yol/dosya.rs\n<|fim_prefix|>ön<|fim_suffix|>son<|fim_middle|>orta<|endoftext|>   (FIM)
//! ```

pub mod collect;
pub mod filter;
pub mod shard;

use std::collections::HashSet;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, AtomicUsize, Ordering};
use std::sync::{mpsc, Mutex};

use anyhow::{bail, Result};
use rand::seq::SliceRandom;
use rand::{Rng, SeedableRng};
use serde::{Deserialize, Serialize};
use sosis_tokenizer::{BpeTrainer, Tokenizer, EOS, FILE_SEP, FIM_MIDDLE, FIM_PREFIX, FIM_SUFFIX};

pub use collect::{CollectOptions, Document, Source};
pub use filter::FilterOptions;
pub use shard::{ShardWriter, TokenDataset};

pub const TRAIN_FILE: &str = "train.bin";
pub const VAL_FILE: &str = "val.bin";
pub const META_FILE: &str = "meta.json";

/// Bir belgeyi model biçiminde token'lara çevirir. `fim` verilirse (ön, orta) kesme noktaları
/// karakter indeksleri olarak kullanılır.
pub fn encode_document(tok: &Tokenizer, doc: &Document, fim: Option<(usize, usize)>) -> Vec<u32> {
    let mut ids = vec![FILE_SEP];
    if !doc.path.is_empty() {
        ids.extend(tok.encode(&format!("{}\n", doc.path)));
    }
    match fim {
        None => ids.extend(tok.encode(&doc.content)),
        Some((a, b)) => {
            let byte_at = |ci: usize| {
                doc.content
                    .char_indices()
                    .nth(ci)
                    .map(|(i, _)| i)
                    .unwrap_or(doc.content.len())
            };
            let (a, b) = (byte_at(a), byte_at(b));
            let (pre, mid, suf) = (&doc.content[..a], &doc.content[a..b], &doc.content[b..]);
            ids.push(FIM_PREFIX);
            ids.extend(tok.encode(pre));
            ids.push(FIM_SUFFIX);
            ids.extend(tok.encode(suf));
            ids.push(FIM_MIDDLE);
            ids.extend(tok.encode(mid));
        }
    }
    ids.push(EOS);
    ids
}

/// FIM için iki rastgele kesme noktası (karakter indeksi, sıralı).
fn fim_points(rng: &mut impl Rng, n_chars: usize) -> (usize, usize) {
    let a = rng.gen_range(0..=n_chars);
    let b = rng.gen_range(0..=n_chars);
    (a.min(b), a.max(b))
}

pub struct PrepareOptions {
    pub inputs: Vec<PathBuf>,
    pub out_dir: PathBuf,
    /// Belgelerin FIM biçimine çevrilme olasılığı (StarCoder: 0.5).
    pub fim_rate: f64,
    /// Doğrulama kümesine ayrılan belge yüzdesi.
    pub val_percent: f64,
    pub threads: usize,
    pub seed: u64,
    pub collect: CollectOptions,
    pub filter: FilterOptions,
}

#[derive(Debug, Default, Serialize, Deserialize)]
pub struct DataMeta {
    pub vocab_size: usize,
    pub train_tokens: u64,
    pub val_tokens: u64,
    pub docs: u64,
    pub fim_docs: u64,
    pub duplicates: u64,
    pub rejected: u64,
}

impl DataMeta {
    pub fn load(dir: &Path) -> Result<Self> {
        Ok(serde_json::from_str(&std::fs::read_to_string(
            dir.join(META_FILE),
        )?)?)
    }
}

/// Kaynakları tarar, filtreler, tekilleştirir, token'lar ve `train.bin`/`val.bin` yazar.
pub fn prepare(
    tok: &Tokenizer,
    opts: &PrepareOptions,
    progress: impl Fn(u64, u64) + Sync,
) -> Result<DataMeta> {
    if tok.vocab_size() > u16::MAX as usize {
        bail!(
            "vocab_size {} > 65535, u16 shard'lara sığmaz",
            tok.vocab_size()
        );
    }
    std::fs::create_dir_all(&opts.out_dir)?;
    let sources = collect::find_sources(&opts.inputs, &opts.collect)?;
    if sources.is_empty() {
        bail!("hiç kaynak dosya bulunamadı: {:?}", opts.inputs);
    }

    let seen = Mutex::new(HashSet::<u64>::new());
    let next = AtomicUsize::new(0);
    let docs = AtomicU64::new(0);
    let fim_docs = AtomicU64::new(0);
    let dups = AtomicU64::new(0);
    let rejected = AtomicU64::new(0);
    let (txs, rx) = mpsc::sync_channel::<(bool, Vec<u16>)>(256);

    let mut train = ShardWriter::create(&opts.out_dir.join(TRAIN_FILE))?;
    let mut val = ShardWriter::create(&opts.out_dir.join(VAL_FILE))?;

    std::thread::scope(|s| -> Result<()> {
        for _ in 0..opts.threads.max(1) {
            let tx = txs.clone();
            let (sources, seen, next) = (&sources, &seen, &next);
            let (docs, fim_docs, dups, rejected) = (&docs, &fim_docs, &dups, &rejected);
            s.spawn(move || loop {
                let i = next.fetch_add(1, Ordering::Relaxed);
                let Some(src) = sources.get(i) else { break };
                let _ = collect::read_source(src, |doc| {
                    if filter::check(&doc.content, &opts.filter).is_err() {
                        rejected.fetch_add(1, Ordering::Relaxed);
                        return;
                    }
                    let h = filter::content_hash(&doc.content);
                    if !seen.lock().unwrap().insert(h) {
                        dups.fetch_add(1, Ordering::Relaxed);
                        return;
                    }
                    // Belge başına deterministik rastgelelik.
                    let mut rng = rand::rngs::StdRng::seed_from_u64(h ^ opts.seed);
                    let is_val = ((h % 10_000) as f64) < opts.val_percent * 100.0;
                    let fim = if rng.gen_bool(opts.fim_rate.clamp(0.0, 1.0)) {
                        fim_docs.fetch_add(1, Ordering::Relaxed);
                        Some(fim_points(&mut rng, doc.content.chars().count()))
                    } else {
                        None
                    };
                    let ids: Vec<u16> = encode_document(tok, &doc, fim)
                        .into_iter()
                        .map(|t| t as u16)
                        .collect();
                    docs.fetch_add(1, Ordering::Relaxed);
                    let _ = tx.send((is_val, ids));
                });
            });
        }
        drop(txs);
        let mut n = 0u64;
        for (is_val, ids) in rx {
            if is_val {
                val.write(&ids)?
            } else {
                train.write(&ids)?
            }
            n += 1;
            if n.is_multiple_of(1000) {
                progress(n, train.tokens + val.tokens);
            }
        }
        Ok(())
    })?;

    let meta = DataMeta {
        vocab_size: tok.vocab_size(),
        train_tokens: train.finish()?,
        val_tokens: val.finish()?,
        docs: docs.into_inner(),
        fim_docs: fim_docs.into_inner(),
        duplicates: dups.into_inner(),
        rejected: rejected.into_inner(),
    };
    std::fs::write(
        opts.out_dir.join(META_FILE),
        serde_json::to_string_pretty(&meta)?,
    )?;
    Ok(meta)
}

/// Tokenizer eğitimi için kaynaklardan (karıştırılmış sırayla) en fazla `max_bytes` metin besler.
pub fn feed_tokenizer_trainer(
    trainer: &mut BpeTrainer,
    inputs: &[PathBuf],
    max_bytes: u64,
    collect_opts: &CollectOptions,
    filter_opts: &FilterOptions,
    seed: u64,
) -> Result<u64> {
    let mut sources = collect::find_sources(inputs, collect_opts)?;
    sources.shuffle(&mut rand::rngs::StdRng::seed_from_u64(seed));
    let mut seen = HashSet::new();
    let mut docs = 0;
    for src in &sources {
        if trainer.bytes_seen >= max_bytes {
            break;
        }
        collect::read_source(src, |doc| {
            if trainer.bytes_seen >= max_bytes
                || filter::check(&doc.content, filter_opts).is_err()
                || !seen.insert(filter::content_hash(&doc.content))
            {
                return;
            }
            if !doc.path.is_empty() {
                trainer.feed(&format!("{}\n", doc.path));
            }
            trainer.feed(&doc.content);
            docs += 1;
        })?;
    }
    Ok(docs)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fim_document_roundtrip() {
        let tok = {
            let mut t = BpeTrainer::new();
            t.feed("fn main() { println!(\"hi\"); }");
            t.train(300, |_, _| {})
        };
        let doc = Document {
            path: "a.rs".into(),
            content: "fn main() { println!(\"ğü\"); }".into(),
        };
        let plain = encode_document(&tok, &doc, None);
        assert_eq!(plain[0], FILE_SEP);
        assert_eq!(*plain.last().unwrap(), EOS);
        assert_eq!(tok.decode(&plain, true), format!("a.rs\n{}", doc.content));

        let fim = encode_document(&tok, &doc, Some((3, 25)));
        let p = fim.iter().position(|&t| t == FIM_PREFIX).unwrap();
        let s = fim.iter().position(|&t| t == FIM_SUFFIX).unwrap();
        let m = fim.iter().position(|&t| t == FIM_MIDDLE).unwrap();
        assert!(p < s && s < m);
        let pre = tok.decode(&fim[p + 1..s], true);
        let suf = tok.decode(&fim[s + 1..m], true);
        let mid = tok.decode(&fim[m + 1..fim.len() - 1], true);
        assert_eq!(format!("{pre}{mid}{suf}"), doc.content);
    }
}
