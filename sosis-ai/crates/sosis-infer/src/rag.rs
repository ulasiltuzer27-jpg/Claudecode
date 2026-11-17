//! Proje hafızası (RAG): kod tabanınızı parçalara böler, BM25 ile arar ve en ilgili parçaları
//! modelin bağlamına ekler. Ek bir model veya servis gerektirmez; indeks tek bir JSON dosyasıdır.

use std::collections::HashMap;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};
use sosis_data::collect::{find_sources, read_source, CollectOptions};
use sosis_tokenizer::Tokenizer;

const CHUNK_LINES: usize = 40;
const MIN_BREAK: usize = 24;
const K1: f64 = 1.2;
const B: f64 = 0.75;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Chunk {
    pub path: String,
    /// 1'den başlayan satır numarası.
    pub line: usize,
    pub text: String,
}

#[derive(Serialize, Deserialize)]
struct IndexFile {
    version: u32,
    roots: Vec<PathBuf>,
    chunks: Vec<Chunk>,
}

/// BM25 indeksi.
pub struct CodeIndex {
    pub roots: Vec<PathBuf>,
    pub chunks: Vec<Chunk>,
    /// terim → (parça, frekans) listesi
    postings: HashMap<String, Vec<(u32, u32)>>,
    lengths: Vec<u32>,
    avg_len: f64,
}

/// Arama terimleri: tanımlayıcılar küçük harfle, ayrıca camelCase / snake_case parçaları.
pub fn terms(text: &str) -> Vec<String> {
    let mut out = Vec::new();
    for word in text.split(|c: char| !(c.is_alphanumeric() || c == '_')) {
        if word.is_empty() || word.len() > 64 {
            continue;
        }
        let lower = word.to_lowercase();
        let mut parts = Vec::new();
        let mut cur = String::new();
        let mut prev_lower = false;
        for c in word.chars() {
            if c == '_' {
                if !cur.is_empty() {
                    parts.push(std::mem::take(&mut cur));
                }
                prev_lower = false;
                continue;
            }
            if c.is_uppercase() && prev_lower && !cur.is_empty() {
                parts.push(std::mem::take(&mut cur));
            }
            prev_lower = c.is_lowercase() || c.is_ascii_digit();
            cur.extend(c.to_lowercase());
        }
        if !cur.is_empty() {
            parts.push(cur);
        }
        if lower.chars().count() >= 2 {
            out.push(lower.clone());
        }
        if parts.len() > 1 {
            out.extend(
                parts
                    .into_iter()
                    .filter(|p| p.chars().count() >= 2 && *p != lower),
            );
        }
    }
    out
}

/// Metni ~40 satırlık parçalara böler; mümkünse boş satırlarda keser.
pub fn split_chunks(path: &str, text: &str) -> Vec<Chunk> {
    let lines: Vec<&str> = text.lines().collect();
    let mut out = Vec::new();
    let mut start = 0;
    while start < lines.len() {
        let mut end = (start + CHUNK_LINES).min(lines.len());
        if end < lines.len() {
            if let Some(b) = (start + MIN_BREAK..end)
                .rev()
                .find(|&i| lines[i].trim().is_empty())
            {
                end = b + 1;
            }
        }
        let body = lines[start..end].join("\n");
        if !body.trim().is_empty() {
            out.push(Chunk {
                path: path.to_string(),
                line: start + 1,
                text: body,
            });
        }
        start = end;
    }
    out
}

impl CodeIndex {
    pub fn build(roots: &[PathBuf]) -> Result<Self> {
        let mut chunks = Vec::new();
        for src in find_sources(roots, &CollectOptions::default())? {
            read_source(&src, |doc| {
                if doc.content.len() < 2_000_000 {
                    chunks.extend(split_chunks(&doc.path, &doc.content));
                }
            })?;
        }
        Ok(Self::from_chunks(roots.to_vec(), chunks))
    }

    fn from_chunks(roots: Vec<PathBuf>, chunks: Vec<Chunk>) -> Self {
        let mut postings: HashMap<String, Vec<(u32, u32)>> = HashMap::new();
        let mut lengths = Vec::with_capacity(chunks.len());
        for (i, c) in chunks.iter().enumerate() {
            let mut tf: HashMap<String, u32> = HashMap::new();
            // Yol da aranabilir olsun (ör. "player" → Player.cs).
            let ts = terms(&format!("{} {}", c.path, c.text));
            lengths.push(ts.len() as u32);
            for t in ts {
                *tf.entry(t).or_default() += 1;
            }
            for (t, f) in tf {
                postings.entry(t).or_default().push((i as u32, f));
            }
        }
        let avg_len = lengths.iter().map(|&l| l as f64).sum::<f64>() / lengths.len().max(1) as f64;
        Self {
            roots,
            chunks,
            postings,
            lengths,
            avg_len,
        }
    }

    pub fn save(&self, path: &Path) -> Result<()> {
        let f = IndexFile {
            version: 1,
            roots: self.roots.clone(),
            chunks: self.chunks.clone(),
        };
        std::fs::write(path, serde_json::to_vec(&f)?)
            .with_context(|| format!("indeks yazılamadı: {}", path.display()))
    }

    pub fn load(path: &Path) -> Result<Self> {
        let bytes =
            std::fs::read(path).with_context(|| format!("indeks okunamadı: {}", path.display()))?;
        let f: IndexFile = serde_json::from_slice(&bytes)?;
        Ok(Self::from_chunks(f.roots, f.chunks))
    }

    /// En ilgili `k` parça (skor, parça).
    pub fn search(&self, query: &str, k: usize) -> Vec<(f64, &Chunk)> {
        let n = self.chunks.len() as f64;
        let mut scores: HashMap<u32, f64> = HashMap::new();
        let mut qterms = terms(query);
        qterms.sort();
        qterms.dedup();
        for t in &qterms {
            let Some(list) = self.postings.get(t) else {
                continue;
            };
            let df = list.len() as f64;
            let idf = ((n - df + 0.5) / (df + 0.5) + 1.0).ln();
            for &(doc, f) in list {
                let f = f as f64;
                let len = self.lengths[doc as usize] as f64;
                let s =
                    idf * f * (K1 + 1.0) / (f + K1 * (1.0 - B + B * len / self.avg_len.max(1.0)));
                *scores.entry(doc).or_default() += s;
            }
        }
        let mut ranked: Vec<(f64, u32)> = scores.into_iter().map(|(d, s)| (s, d)).collect();
        ranked.sort_by(|a, b| b.0.total_cmp(&a.0).then(a.1.cmp(&b.1)));
        ranked
            .into_iter()
            .take(k)
            .map(|(s, d)| (s, &self.chunks[d as usize]))
            .collect()
    }

    /// Token bütçesine sığan en ilgili parçalar.
    pub fn retrieve(&self, query: &str, tok: &Tokenizer, budget_tokens: usize) -> Vec<&Chunk> {
        let mut out = Vec::new();
        let mut used = 0;
        for (_, c) in self.search(query, 16) {
            let n = tok.encode(&c.text).len() + 16;
            if used + n > budget_tokens {
                continue;
            }
            used += n;
            out.push(c);
        }
        out
    }
}

/// Tamamlama için: parçaları eğitimdeki çok dosyalı biçimde token'lara çevirir
/// (`<|file_sep|>yol\n...<|endoftext|>`), ana istemin önüne eklenir.
pub fn context_ids(tok: &Tokenizer, chunks: &[&Chunk]) -> Vec<u32> {
    let mut ids = Vec::new();
    for c in chunks {
        ids.push(sosis_tokenizer::FILE_SEP);
        ids.extend(tok.encode(&format!("{}\n{}", c.path, c.text)));
        ids.push(sosis_tokenizer::EOS);
    }
    ids
}

/// Sohbet için: parçaları markdown kod blokları olarak biçimlendirir.
pub fn format_for_chat(chunks: &[&Chunk]) -> String {
    let mut s = String::from("Projeden ilgili kod parçaları:\n");
    for c in chunks {
        s.push_str(&format!(
            "\n{} (satır {}):\n```\n{}\n```\n",
            c.path, c.line, c.text
        ));
    }
    s
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn splits_identifiers() {
        let t = terms("fn takeDamage(player_health: u32) PlayerHealth");
        for want in [
            "takedamage",
            "take",
            "damage",
            "player_health",
            "player",
            "health",
            "playerhealth",
        ] {
            assert!(t.contains(&want.to_string()), "{want} yok: {t:?}");
        }
    }

    #[test]
    fn chunks_cover_text() {
        let text: String = (0..100)
            .map(|i| {
                if i % 30 == 29 {
                    "\n".to_string()
                } else {
                    format!("satır {i}\n")
                }
            })
            .collect();
        let cs = split_chunks("a.rs", &text);
        assert!(cs.len() >= 3);
        assert_eq!(cs[0].line, 1);
        assert!(cs.iter().all(|c| c.text.lines().count() <= CHUNK_LINES));
    }

    #[test]
    fn bm25_ranks_relevant_chunk_first() {
        let chunks = vec![
            Chunk {
                path: "src/inventory.rs".into(),
                line: 1,
                text: "fn add_item(slot: usize) { items.push(slot) }".into(),
            },
            Chunk {
                path: "src/player.rs".into(),
                line: 1,
                text: "fn take_damage(&mut self, amount: u32) { self.health -= amount; }".into(),
            },
            Chunk {
                path: "src/world.rs".into(),
                line: 1,
                text: "fn generate_terrain(seed: u64) { noise(seed) }".into(),
            },
        ];
        let idx = CodeIndex::from_chunks(vec![], chunks);
        let r = idx.search("oyuncu hasar alınca health nasıl azalıyor? takeDamage", 3);
        assert_eq!(r[0].1.path, "src/player.rs");
        let r = idx.search("terrain", 3);
        assert_eq!(r.len(), 1);
        assert_eq!(r[0].1.path, "src/world.rs");
    }
}
