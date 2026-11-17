//! # sosis-tokenizer
//!
//! Sosis AI için sıfırdan yazılmış byte-level BPE tokenizer.
//!
//! Kimlik (id) düzeni:
//! - `0..16`   özel token'lar (`<|endoftext|>`, FIM token'ları, sohbet rolleri, yedekler)
//! - `16..272` ham baytlar (her UTF-8 metin kayıpsız kodlanabilir)
//! - `272..`   öğrenilmiş birleştirmeler

pub mod chat;
pub mod pretokenize;
mod train;

pub use train::BpeTrainer;

use std::collections::HashMap;
use std::path::Path;
use std::sync::Mutex;

use anyhow::{bail, Context, Result};
use serde::{Deserialize, Serialize};

pub const PAD: u32 = 0;
pub const EOS: u32 = 1;
pub const FIM_PREFIX: u32 = 2;
pub const FIM_MIDDLE: u32 = 3;
pub const FIM_SUFFIX: u32 = 4;
pub const FILE_SEP: u32 = 5;
pub const USER: u32 = 6;
pub const ASSISTANT: u32 = 7;
pub const SYSTEM: u32 = 8;

/// Özel token sayısı (ileride kullanmak için yedekler dahil).
pub const NUM_SPECIAL: u32 = 16;
/// Ham baytların başladığı id.
pub const BYTE_OFFSET: u32 = NUM_SPECIAL;

pub const SPECIAL_TOKENS: [&str; NUM_SPECIAL as usize] = [
    "<|pad|>",
    "<|endoftext|>",
    "<|fim_prefix|>",
    "<|fim_middle|>",
    "<|fim_suffix|>",
    "<|file_sep|>",
    "<|user|>",
    "<|assistant|>",
    "<|system|>",
    "<|reserved_9|>",
    "<|reserved_10|>",
    "<|reserved_11|>",
    "<|reserved_12|>",
    "<|reserved_13|>",
    "<|reserved_14|>",
    "<|reserved_15|>",
];

const CACHE_LIMIT: usize = 1 << 20;

#[derive(Serialize, Deserialize)]
struct TokenizerFile {
    name: String,
    version: u32,
    specials: Vec<String>,
    merges: Vec<(u32, u32)>,
}

/// Eğitilmiş BPE tokenizer.
pub struct Tokenizer {
    merges: Vec<(u32, u32)>,
    ranks: HashMap<(u32, u32), u32>,
    /// Her id'nin bayt karşılığı (özel token'lar için metin hali).
    vocab: Vec<Vec<u8>>,
    cache: Mutex<HashMap<String, Vec<u32>>>,
}

impl Tokenizer {
    pub(crate) fn from_merges(merges: Vec<(u32, u32)>) -> Self {
        let mut vocab: Vec<Vec<u8>> = SPECIAL_TOKENS
            .iter()
            .map(|s| s.as_bytes().to_vec())
            .collect();
        vocab.extend((0..=255u8).map(|b| vec![b]));
        let mut ranks = HashMap::with_capacity(merges.len());
        for (i, &(a, b)) in merges.iter().enumerate() {
            let mut bytes = vocab[a as usize].clone();
            bytes.extend_from_slice(&vocab[b as usize]);
            vocab.push(bytes);
            ranks.insert((a, b), i as u32);
        }
        Self {
            merges,
            ranks,
            vocab,
            cache: Mutex::new(HashMap::new()),
        }
    }

    /// Toplam sözlük boyutu.
    pub fn vocab_size(&self) -> usize {
        self.vocab.len()
    }

    pub fn save(&self, path: impl AsRef<Path>) -> Result<()> {
        let file = TokenizerFile {
            name: "sosis-bpe".into(),
            version: 1,
            specials: SPECIAL_TOKENS.iter().map(|s| s.to_string()).collect(),
            merges: self.merges.clone(),
        };
        let path = path.as_ref();
        if let Some(dir) = path.parent() {
            std::fs::create_dir_all(dir).ok();
        }
        std::fs::write(path, serde_json::to_string(&file)?)
            .with_context(|| format!("tokenizer yazılamadı: {}", path.display()))
    }

    pub fn load(path: impl AsRef<Path>) -> Result<Self> {
        let path = path.as_ref();
        let text = std::fs::read_to_string(path)
            .with_context(|| format!("tokenizer okunamadı: {}", path.display()))?;
        let file: TokenizerFile = serde_json::from_str(&text)?;
        if file.version != 1 || file.specials.len() != NUM_SPECIAL as usize {
            bail!("desteklenmeyen tokenizer dosyası");
        }
        let n = BYTE_OFFSET + 256;
        for (i, &(a, b)) in file.merges.iter().enumerate() {
            if a >= n + i as u32 || b >= n + i as u32 {
                bail!("bozuk birleştirme #{i}");
            }
        }
        Ok(Self::from_merges(file.merges))
    }

    /// Düz metni kodlar. Metindeki `<|endoftext|>` gibi diziler sıradan metin sayılır.
    pub fn encode(&self, text: &str) -> Vec<u32> {
        let mut out = Vec::with_capacity(text.len() / 3);
        for chunk in pretokenize::split(text) {
            self.encode_chunk(chunk, &mut out);
        }
        out
    }

    /// Metni kodlar; içindeki özel token yazımlarını (`<|fim_prefix|>` vb.) özel id'lere çevirir.
    pub fn encode_with_special(&self, text: &str) -> Vec<u32> {
        let mut out = Vec::new();
        let mut rest = text;
        while !rest.is_empty() {
            let next = SPECIAL_TOKENS
                .iter()
                .enumerate()
                .filter_map(|(id, s)| rest.find(s).map(|p| (p, id, s.len())))
                .min();
            match next {
                Some((p, id, len)) => {
                    out.extend(self.encode(&rest[..p]));
                    out.push(id as u32);
                    rest = &rest[p + len..];
                }
                None => {
                    out.extend(self.encode(rest));
                    break;
                }
            }
        }
        out
    }

    fn encode_chunk(&self, chunk: &str, out: &mut Vec<u32>) {
        if let Some(ids) = self.cache.lock().unwrap().get(chunk) {
            out.extend_from_slice(ids);
            return;
        }
        let mut ids: Vec<u32> = chunk.bytes().map(|b| b as u32 + BYTE_OFFSET).collect();
        while ids.len() > 1 {
            let best = ids
                .windows(2)
                .enumerate()
                .filter_map(|(i, p)| self.ranks.get(&(p[0], p[1])).map(|&r| (r, i)))
                .min();
            let Some((rank, _)) = best else { break };
            let pair = self.merges[rank as usize];
            let new_id = BYTE_OFFSET + 256 + rank;
            let mut merged = Vec::with_capacity(ids.len());
            let mut i = 0;
            while i < ids.len() {
                if i + 1 < ids.len() && (ids[i], ids[i + 1]) == pair {
                    merged.push(new_id);
                    i += 2;
                } else {
                    merged.push(ids[i]);
                    i += 1;
                }
            }
            ids = merged;
        }
        out.extend_from_slice(&ids);
        let mut cache = self.cache.lock().unwrap();
        if cache.len() < CACHE_LIMIT {
            cache.insert(chunk.to_string(), ids);
        }
    }

    /// Id'lerin ham baytları. `skip_special` ise özel token'lar atlanır.
    pub fn decode_bytes(&self, ids: &[u32], skip_special: bool) -> Vec<u8> {
        let mut out = Vec::new();
        for &id in ids {
            if skip_special && id < NUM_SPECIAL {
                continue;
            }
            if let Some(b) = self.vocab.get(id as usize) {
                out.extend_from_slice(b);
            }
        }
        out
    }

    pub fn decode(&self, ids: &[u32], skip_special: bool) -> String {
        String::from_utf8_lossy(&self.decode_bytes(ids, skip_special)).into_owned()
    }

    /// Bir token'ın okunabilir hali (hata ayıklama için).
    pub fn token_str(&self, id: u32) -> String {
        self.vocab
            .get(id as usize)
            .map(|b| String::from_utf8_lossy(b).into_owned())
            .unwrap_or_default()
    }
}

/// Akış halinde çözümleme: yarım UTF-8 karakterleri bir sonraki token'a kadar bekletir.
#[derive(Default)]
pub struct StreamDecoder {
    pending: Vec<u8>,
}

impl StreamDecoder {
    pub fn push(&mut self, tok: &Tokenizer, id: u32) -> String {
        self.pending.extend(tok.decode_bytes(&[id], true));
        match std::str::from_utf8(&self.pending) {
            Ok(s) => {
                let s = s.to_string();
                self.pending.clear();
                s
            }
            Err(e) => {
                let valid = e.valid_up_to();
                // Geçersiz (yarım değil) bayt varsa kaybetmeden ilerle.
                if e.error_len().is_some() {
                    let s = String::from_utf8_lossy(&self.pending).into_owned();
                    self.pending.clear();
                    return s;
                }
                let s = String::from_utf8_lossy(&self.pending[..valid]).into_owned();
                self.pending.drain(..valid);
                s
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const CORPUS: &str = r#"
fn add(a: i32, b: i32) -> i32 {
    a + b
}

fn main() {
    let total = add(1, 2);
    println!("toplam = {}", total);
    for i in 0..10 {
        println!("{}", i);
    }
}

def add(a, b):
    return a + b

for i in range(10):
    print(i)
"#;

    fn trained() -> Tokenizer {
        let mut t = BpeTrainer::new();
        for _ in 0..20 {
            t.feed(CORPUS);
        }
        t.train(400, |_, _| {})
    }

    #[test]
    fn roundtrip() {
        let tok = trained();
        assert!(tok.vocab_size() > 272);
        for text in [CORPUS, "", "yeni metin: ğüşıöç 🌭 sosis", "    \n\t\t}"] {
            let ids = tok.encode(text);
            assert_eq!(tok.decode(&ids, false), text);
        }
    }

    #[test]
    fn compresses() {
        let tok = trained();
        let ids = tok.encode(CORPUS);
        assert!(
            ids.len() * 2 < CORPUS.len(),
            "{} token / {} bayt",
            ids.len(),
            CORPUS.len()
        );
    }

    #[test]
    fn specials() {
        let tok = trained();
        let ids = tok.encode_with_special("<|fim_prefix|>fn a<|fim_suffix|>}<|fim_middle|>");
        assert_eq!(ids[0], FIM_PREFIX);
        assert!(ids.contains(&FIM_SUFFIX));
        assert_eq!(*ids.last().unwrap(), FIM_MIDDLE);
        // Düz kodlamada özel token metin olarak kalır.
        assert!(!tok.encode("<|endoftext|>").contains(&EOS));
    }

    #[test]
    fn save_load() {
        let tok = trained();
        let dir = std::env::temp_dir().join("sosis_tok_test");
        let path = dir.join("tok.json");
        tok.save(&path).unwrap();
        let tok2 = Tokenizer::load(&path).unwrap();
        assert_eq!(tok.encode(CORPUS), tok2.encode(CORPUS));
    }

    #[test]
    fn stream_decoder_handles_split_utf8() {
        let tok = Tokenizer::from_merges(vec![]);
        let mut sd = StreamDecoder::default();
        let text = "ğ🌭";
        let mut out = String::new();
        for id in tok.encode(text) {
            out.push_str(&sd.push(&tok, id));
        }
        assert_eq!(out, text);
    }
}
