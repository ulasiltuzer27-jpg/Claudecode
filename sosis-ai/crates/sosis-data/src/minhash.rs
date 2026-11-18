//! Gelişmiş veri temizliği.
//!
//! - **Yakın kopya eleme (MinHash LSH):** küçük farklarla (yorum, boşluk, değişken adı) tekrar eden
//!   dosyaları bulur. Kelime 5-gram'ları → 128 MinHash imzası → 32 bant × 4 satır LSH; aday
//!   çiftlerin tahmini Jaccard benzerliği eşiği geçerse belge elenir.
//! - **Sızıntı temizliği (decontamination):** değerlendirme görevlerinin metinleriyle 13 kelimelik
//!   ortak dizisi olan belgeleri atar; böylece `sosis bench` sonuçları ezberi ölçmez.

use std::collections::{HashMap, HashSet};

pub const NUM_PERM: usize = 128;
pub const BANDS: usize = 32;
pub const ROWS: usize = NUM_PERM / BANDS;
const SHINGLE: usize = 5;
const CONTAM_NGRAM: usize = 13;

/// Küçük harfli kelimeler (harf/rakam/_ dizileri); biçimlendirmeden bağımsız.
pub fn words(text: &str) -> Vec<String> {
    text.split(|c: char| !(c.is_alphanumeric() || c == '_'))
        .filter(|w| !w.is_empty())
        .map(|w| w.to_lowercase())
        .collect()
}

fn fnv1a(bytes: &[u8], seed: u64) -> u64 {
    let mut h = 0xcbf2_9ce4_8422_2325u64 ^ seed;
    for &b in bytes {
        h ^= b as u64;
        h = h.wrapping_mul(0x0000_0100_0000_01b3);
    }
    h
}

fn mix(mut x: u64) -> u64 {
    // splitmix64 sonlandırıcı
    x ^= x >> 30;
    x = x.wrapping_mul(0xbf58_476d_1ce4_e5b9);
    x ^= x >> 27;
    x = x.wrapping_mul(0x94d0_49bb_1331_11eb);
    x ^ (x >> 31)
}

fn ngram_hashes(ws: &[String], n: usize) -> Vec<u64> {
    if ws.is_empty() {
        return vec![];
    }
    if ws.len() < n {
        return vec![fnv1a(ws.join(" ").as_bytes(), 0)];
    }
    ws.windows(n)
        .map(|w| fnv1a(w.join(" ").as_bytes(), 0))
        .collect()
}

/// 128 değerlik MinHash imzası.
pub fn signature(text: &str) -> Vec<u32> {
    let shingles = ngram_hashes(&words(text), SHINGLE);
    let mut sig = vec![u32::MAX; NUM_PERM];
    for h in shingles {
        for (i, s) in sig.iter_mut().enumerate() {
            let v = mix(h ^ mix(i as u64 + 1)) as u32;
            if v < *s {
                *s = v;
            }
        }
    }
    sig
}

/// İki imzanın tahmini Jaccard benzerliği.
pub fn similarity(a: &[u32], b: &[u32]) -> f64 {
    a.iter().zip(b).filter(|(x, y)| x == y).count() as f64 / a.len().max(1) as f64
}

/// Artımlı yakın kopya dizini.
pub struct NearDedup {
    threshold: f64,
    buckets: HashMap<(u8, u64), Vec<u32>>,
    sigs: Vec<Vec<u32>>,
}

impl NearDedup {
    pub fn new(threshold: f64) -> Self {
        Self {
            threshold,
            buckets: HashMap::new(),
            sigs: Vec::new(),
        }
    }

    /// Belge daha önce görülen birine yakınsa `true` (eklenmez); değilse dizine ekler.
    pub fn is_duplicate(&mut self, text: &str) -> bool {
        let sig = signature(text);
        let keys: Vec<(u8, u64)> = (0..BANDS)
            .map(|b| {
                let band = &sig[b * ROWS..(b + 1) * ROWS];
                let bytes: Vec<u8> = band.iter().flat_map(|v| v.to_le_bytes()).collect();
                (b as u8, fnv1a(&bytes, b as u64))
            })
            .collect();
        let mut seen = HashSet::new();
        for k in &keys {
            if let Some(cands) = self.buckets.get(k) {
                for &c in cands {
                    if seen.insert(c) && similarity(&sig, &self.sigs[c as usize]) >= self.threshold
                    {
                        return true;
                    }
                }
            }
        }
        let id = self.sigs.len() as u32;
        self.sigs.push(sig);
        for k in keys {
            self.buckets.entry(k).or_default().push(id);
        }
        false
    }

    pub fn len(&self) -> usize {
        self.sigs.len()
    }

    pub fn is_empty(&self) -> bool {
        self.sigs.is_empty()
    }
}

/// Değerlendirme metinlerinin 13-gram'ları.
pub struct Contamination {
    grams: HashSet<u64>,
}

impl Contamination {
    pub fn new(texts: &[String]) -> Self {
        let mut grams = HashSet::new();
        for t in texts {
            grams.extend(ngram_hashes(&words(t), CONTAM_NGRAM));
        }
        Self { grams }
    }

    pub fn is_empty(&self) -> bool {
        self.grams.is_empty()
    }

    /// Belge bir değerlendirme metniyle 13 kelimelik ortak dizi içeriyor mu?
    pub fn contains(&self, text: &str) -> bool {
        if self.grams.is_empty() {
            return false;
        }
        let ws = words(text);
        if ws.len() < CONTAM_NGRAM {
            return false;
        }
        ws.windows(CONTAM_NGRAM)
            .any(|w| self.grams.contains(&fnv1a(w.join(" ").as_bytes(), 0)))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const A: &str = "fn parse_config(path: &str) -> Result<Config> {\n    let text = std::fs::read_to_string(path)?;\n    let cfg: Config = toml::from_str(&text)?;\n    validate(&cfg)?;\n    Ok(cfg)\n}\n\nfn validate(cfg: &Config) -> Result<()> {\n    if cfg.port == 0 { bail!(\"port\") }\n    if cfg.name.is_empty() { bail!(\"name\") }\n    Ok(())\n}\n";

    #[test]
    fn near_duplicates_detected() {
        let mut d = NearDedup::new(0.8);
        assert!(!d.is_duplicate(A));
        // Yalnızca biçim ve bir yorum farkı.
        let reformatted = format!("// yapılandırma okuma\n{}", A.replace("    ", "\t"));
        assert!(d.is_duplicate(&reformatted));
        // Tamamen farklı kod.
        let other = "def train(model, data):\n    for epoch in range(10):\n        for batch in data:\n            loss = model(batch)\n            loss.backward()\n    return model\n";
        assert!(!d.is_duplicate(other));
        assert_eq!(d.len(), 2);
    }

    #[test]
    fn similarity_estimates_jaccard() {
        let s1 = signature(A);
        assert_eq!(similarity(&s1, &s1), 1.0);
        let s2 =
            signature("tamamen alakasız bir metin burada duruyor ve hiç ortak kelime yok gibi");
        assert!(similarity(&s1, &s2) < 0.1);
    }

    #[test]
    fn contamination_detects_benchmark_text() {
        let bench = vec!["/// n'inci Fibonacci sayısını döndürür: fib(0) = 0, fib(1) = 1.\nfn fibonacci(n: u64) -> u64 {\n    let (mut a, mut b) = (0u64, 1u64);".to_string()];
        let c = Contamination::new(&bench);
        let leaked = format!("// bir proje\nuse std::io;\n{}\n    a\n}}", bench[0]);
        assert!(c.contains(&leaked));
        assert!(!c.contains(A));
    }
}
