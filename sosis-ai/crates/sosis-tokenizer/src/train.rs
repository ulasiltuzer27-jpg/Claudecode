//! Byte-level BPE eğitimi (artımlı çift sayımı + tembel öncelik kuyruğu).

use std::cmp::Reverse;
use std::collections::{BinaryHeap, HashMap, HashSet};

use crate::{pretokenize, Tokenizer, BYTE_OFFSET};

/// Eğitim sırasında parça frekanslarını toplar; büyük veri setlerini akış halinde besleyebilmek için.
#[derive(Default)]
pub struct BpeTrainer {
    chunk_counts: HashMap<Vec<u8>, u64>,
    pub bytes_seen: u64,
}

impl BpeTrainer {
    pub fn new() -> Self {
        Self::default()
    }

    /// Bir belgeyi eğitim istatistiklerine ekler.
    pub fn feed(&mut self, text: &str) {
        self.bytes_seen += text.len() as u64;
        for chunk in pretokenize::split(text) {
            if let Some(c) = self.chunk_counts.get_mut(chunk.as_bytes()) {
                *c += 1;
            } else {
                self.chunk_counts.insert(chunk.as_bytes().to_vec(), 1);
            }
        }
    }

    /// `vocab_size` (özel token'lar + 256 bayt + birleştirmeler) boyutunda bir tokenizer üretir.
    pub fn train(self, vocab_size: usize, mut progress: impl FnMut(usize, usize)) -> Tokenizer {
        let base = BYTE_OFFSET as usize + 256;
        assert!(vocab_size >= base, "vocab_size en az {base} olmalı");
        let target_merges = vocab_size - base;

        // Deterministik sıra: parçaları sırala.
        let mut entries: Vec<(Vec<u8>, u64)> = self.chunk_counts.into_iter().collect();
        entries.sort_unstable();
        let mut words: Vec<Vec<u32>> = Vec::with_capacity(entries.len());
        let mut counts: Vec<i64> = Vec::with_capacity(entries.len());
        for (bytes, c) in entries {
            words.push(bytes.iter().map(|&b| b as u32 + BYTE_OFFSET).collect());
            counts.push(c as i64);
        }

        let mut pair_counts: HashMap<(u32, u32), i64> = HashMap::new();
        let mut where_: HashMap<(u32, u32), HashSet<usize>> = HashMap::new();
        for (wi, w) in words.iter().enumerate() {
            for p in w.windows(2) {
                let pair = (p[0], p[1]);
                *pair_counts.entry(pair).or_default() += counts[wi];
                where_.entry(pair).or_default().insert(wi);
            }
        }

        // (sayım, en küçük çift önce) — eşitlikte deterministik.
        let mut heap: BinaryHeap<(i64, Reverse<(u32, u32)>)> =
            pair_counts.iter().map(|(&p, &c)| (c, Reverse(p))).collect();

        let mut merges: Vec<(u32, u32)> = Vec::with_capacity(target_merges);
        while merges.len() < target_merges {
            let Some((c, Reverse(pair))) = heap.pop() else {
                break;
            };
            if pair_counts.get(&pair).copied().unwrap_or(0) != c {
                continue; // bayat kayıt
            }
            if c < 2 {
                break;
            }
            let new_id = base as u32 + merges.len() as u32;
            merges.push(pair);

            let affected = where_.remove(&pair).unwrap_or_default();
            let mut touched: HashSet<(u32, u32)> = HashSet::new();
            for wi in affected {
                let old = &words[wi];
                if !old.windows(2).any(|p| (p[0], p[1]) == pair) {
                    continue;
                }
                let mut new = Vec::with_capacity(old.len());
                let mut i = 0;
                while i < old.len() {
                    if i + 1 < old.len() && (old[i], old[i + 1]) == pair {
                        new.push(new_id);
                        i += 2;
                    } else {
                        new.push(old[i]);
                        i += 1;
                    }
                }
                let wc = counts[wi];
                for p in old.windows(2) {
                    let k = (p[0], p[1]);
                    *pair_counts.get_mut(&k).unwrap() -= wc;
                    touched.insert(k);
                }
                for p in new.windows(2) {
                    let k = (p[0], p[1]);
                    *pair_counts.entry(k).or_default() += wc;
                    where_.entry(k).or_default().insert(wi);
                    touched.insert(k);
                }
                words[wi] = new;
            }
            pair_counts.remove(&pair);
            for k in touched {
                if let Some(&c) = pair_counts.get(&k) {
                    if c > 0 {
                        heap.push((c, Reverse(k)));
                    }
                }
            }
            if merges.len().is_multiple_of(500) {
                progress(merges.len(), target_merges);
            }
        }
        progress(merges.len(), target_merges);
        Tokenizer::from_merges(merges)
    }
}
