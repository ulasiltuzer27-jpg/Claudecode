//! Token shard'ları: `u16` little-endian düz ikili dosyalar ve bellek-eşlemli okuyucu.

use std::fs::File;
use std::io::{BufWriter, Write};
use std::path::Path;

use anyhow::{bail, Context, Result};
use memmap2::Mmap;
use rand::Rng;

pub struct ShardWriter {
    w: BufWriter<File>,
    pub tokens: u64,
}

impl ShardWriter {
    pub fn create(path: &Path) -> Result<Self> {
        let f =
            File::create(path).with_context(|| format!("oluşturulamadı: {}", path.display()))?;
        Ok(Self {
            w: BufWriter::with_capacity(1 << 22, f),
            tokens: 0,
        })
    }

    pub fn write(&mut self, ids: &[u16]) -> Result<()> {
        let mut buf = Vec::with_capacity(ids.len() * 2);
        for &t in ids {
            buf.extend_from_slice(&t.to_le_bytes());
        }
        self.w.write_all(&buf)?;
        self.tokens += ids.len() as u64;
        Ok(())
    }

    pub fn finish(mut self) -> Result<u64> {
        self.w.flush()?;
        Ok(self.tokens)
    }
}

/// Talimat ince ayarı için token başına "öğrenilecek mi?" baytı (`train.bin` → `train.mask`).
pub fn mask_path(shard: &Path) -> std::path::PathBuf {
    shard.with_extension("mask")
}

pub struct MaskWriter {
    w: BufWriter<File>,
}

impl MaskWriter {
    pub fn create(path: &Path) -> Result<Self> {
        let f =
            File::create(path).with_context(|| format!("oluşturulamadı: {}", path.display()))?;
        Ok(Self {
            w: BufWriter::with_capacity(1 << 20, f),
        })
    }

    pub fn write(&mut self, mask: &[bool]) -> Result<()> {
        let bytes: Vec<u8> = mask.iter().map(|&m| m as u8).collect();
        self.w.write_all(&bytes)?;
        Ok(())
    }

    pub fn finish(mut self) -> Result<()> {
        self.w.flush()?;
        Ok(())
    }
}

/// Hedefi öğrenilmeyecek konumlar için kullanılan id (`<|pad|>`; kayıpta yok sayılır).
pub const IGNORE_TARGET: i64 = sosis_tokenizer::PAD as i64;

/// Eğitim için bellek-eşlemli token dizisi. Dosya RAM'e tamamen yüklenmez.
/// Yanında `.mask` dosyası varsa maskesiz konumların hedefleri [`IGNORE_TARGET`] olur.
pub struct TokenDataset {
    mmap: Mmap,
    mask: Option<Mmap>,
}

impl TokenDataset {
    pub fn open(path: &Path) -> Result<Self> {
        let f = File::open(path).with_context(|| format!("açılamadı: {}", path.display()))?;
        // SAFETY: dosya eğitim sırasında değiştirilmemelidir (salt-okunur kullanım).
        let mmap = unsafe { Mmap::map(&f)? };
        if mmap.len() % 2 != 0 {
            bail!("bozuk shard (tek bayt sayısı): {}", path.display());
        }
        let mp = mask_path(path);
        let mask = if mp.exists() {
            // SAFETY: yukarıdakiyle aynı.
            let m = unsafe { Mmap::map(&File::open(&mp)?)? };
            if m.len() != mmap.len() / 2 {
                bail!("maske boyutu token sayısıyla uyuşmuyor: {}", mp.display());
            }
            Some(m)
        } else {
            None
        };
        Ok(Self { mmap, mask })
    }

    pub fn is_masked(&self) -> bool {
        self.mask.is_some()
    }

    #[inline]
    fn learn(&self, i: usize) -> bool {
        self.mask.as_ref().is_none_or(|m| m[i] != 0)
    }

    pub fn len(&self) -> usize {
        self.mmap.len() / 2
    }

    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    #[inline]
    pub fn get(&self, i: usize) -> u16 {
        u16::from_le_bytes([self.mmap[2 * i], self.mmap[2 * i + 1]])
    }

    /// Rastgele konumlardan `batch` adet `seq_len+1` uzunluğunda pencere alır.
    /// Dönen `(girdi, hedef)` düzleştirilmiş `[batch * seq_len]` dizileridir; hedef bir kaydırılmıştır.
    pub fn sample_batch(
        &self,
        rng: &mut impl Rng,
        batch: usize,
        seq_len: usize,
    ) -> (Vec<i64>, Vec<i64>) {
        assert!(
            self.len() > seq_len + 1,
            "veri seti seq_len için çok küçük ({} token)",
            self.len()
        );
        let mut x = Vec::with_capacity(batch * seq_len);
        let mut y = Vec::with_capacity(batch * seq_len);
        for _ in 0..batch {
            let mut start = rng.gen_range(0..self.len() - seq_len - 1);
            // Maskeli veride tamamen yok sayılan bir pencere NaN kayıp verir; öğrenilecek
            // en az bir hedef içeren pencere seçilene kadar yeniden dene.
            if self.mask.is_some() {
                for _ in 0..64 {
                    if (start + 1..=start + seq_len).any(|i| self.learn(i)) {
                        break;
                    }
                    start = rng.gen_range(0..self.len() - seq_len - 1);
                }
            }
            for i in start..start + seq_len {
                x.push(self.get(i) as i64);
                y.push(if self.learn(i + 1) {
                    self.get(i + 1) as i64
                } else {
                    IGNORE_TARGET
                });
            }
        }
        (x, y)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rand::SeedableRng;

    #[test]
    fn masked_targets_are_ignored() {
        let dir = std::env::temp_dir().join(format!("sosis_mask_{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let p = dir.join("t.bin");
        let mut w = ShardWriter::create(&p).unwrap();
        let ids: Vec<u16> = (100..300).collect();
        w.write(&ids).unwrap();
        w.finish().unwrap();
        let mut m = MaskWriter::create(&mask_path(&p)).unwrap();
        m.write(&(0..200).map(|i| i % 4 == 0).collect::<Vec<_>>())
            .unwrap();
        m.finish().unwrap();
        let ds = TokenDataset::open(&p).unwrap();
        assert!(ds.is_masked());
        let mut rng = rand::rngs::StdRng::seed_from_u64(1);
        let (x, y) = ds.sample_batch(&mut rng, 4, 16);
        for (xi, yi) in x.iter().zip(&y) {
            let next = xi + 1;
            if (next - 100) % 4 == 0 {
                assert_eq!(*yi, next);
            } else {
                assert_eq!(*yi, IGNORE_TARGET);
            }
        }
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn write_read_sample() {
        let dir = std::env::temp_dir().join("sosis_shard_test");
        std::fs::create_dir_all(&dir).unwrap();
        let p = dir.join("t.bin");
        let mut w = ShardWriter::create(&p).unwrap();
        let ids: Vec<u16> = (0..1000).collect();
        w.write(&ids).unwrap();
        assert_eq!(w.finish().unwrap(), 1000);
        let ds = TokenDataset::open(&p).unwrap();
        assert_eq!(ds.len(), 1000);
        let mut rng = rand::rngs::StdRng::seed_from_u64(0);
        let (x, y) = ds.sample_batch(&mut rng, 4, 16);
        assert_eq!(x.len(), 64);
        for i in 0..64 {
            assert_eq!(x[i] + 1, y[i]);
        }
    }
}

/// Birden çok veri kümesini ağırlıklarına göre karıştıran örnekleyici (ör. kod + %15 Türkçe metin).
/// Her pencere, ağırlıklara göre seçilen bir kümeden alınır.
pub struct MixedDataset {
    sets: Vec<(TokenDataset, f64)>,
}

impl MixedDataset {
    /// `main`: ana küme; `extra`: (küme, ağırlık). Ana kümenin ağırlığı `1 − Σ ek ağırlıklar`.
    pub fn new(main: TokenDataset, extra: Vec<(TokenDataset, f64)>) -> Result<Self> {
        let sum: f64 = extra.iter().map(|(_, w)| *w).sum();
        if sum >= 1.0 || extra.iter().any(|(_, w)| *w <= 0.0) {
            bail!("karışım ağırlıkları 0'dan büyük ve toplamı 1'den küçük olmalı (toplam {sum})");
        }
        let mut sets = vec![(main, 1.0 - sum)];
        sets.extend(extra);
        Ok(Self { sets })
    }

    pub fn single(main: TokenDataset) -> Self {
        Self {
            sets: vec![(main, 1.0)],
        }
    }

    pub fn sample_batch(
        &self,
        rng: &mut impl Rng,
        batch: usize,
        seq_len: usize,
    ) -> (Vec<i64>, Vec<i64>) {
        let (mut x, mut y) = (
            Vec::with_capacity(batch * seq_len),
            Vec::with_capacity(batch * seq_len),
        );
        for _ in 0..batch {
            let mut r = rng.gen::<f64>();
            let mut idx = 0;
            for (i, (_, w)) in self.sets.iter().enumerate() {
                if r < *w {
                    idx = i;
                    break;
                }
                r -= w;
                idx = i;
            }
            let (xi, yi) = self.sets[idx].0.sample_batch(rng, 1, seq_len);
            x.extend(xi);
            y.extend(yi);
        }
        (x, y)
    }
}

#[cfg(test)]
mod mix_tests {
    use super::*;
    use rand::SeedableRng;

    #[test]
    fn mixing_ratio_holds() {
        let dir = std::env::temp_dir().join(format!("sosis_mix_{}", std::process::id()));
        std::fs::create_dir_all(&dir).unwrap();
        let mk = |name: &str, base: u16| {
            let p = dir.join(name);
            let mut w = ShardWriter::create(&p).unwrap();
            w.write(&vec![base; 2000]).unwrap();
            w.finish().unwrap();
            TokenDataset::open(&p).unwrap()
        };
        let mixed = MixedDataset::new(mk("kod.bin", 100), vec![(mk("tr.bin", 200), 0.15)]).unwrap();
        let mut rng = rand::rngs::StdRng::seed_from_u64(9);
        let (x, _) = mixed.sample_batch(&mut rng, 4000, 4);
        let tr = x.chunks(4).filter(|c| c[0] == 200).count() as f64 / 4000.0;
        assert!((tr - 0.15).abs() < 0.02, "Türkçe oranı {tr}");
        assert!(MixedDataset::new(mk("a.bin", 1), vec![(mk("b.bin", 2), 1.2)]).is_err());
        std::fs::remove_dir_all(&dir).ok();
    }
}
