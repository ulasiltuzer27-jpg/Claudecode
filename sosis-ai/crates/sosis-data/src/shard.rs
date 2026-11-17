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

/// Eğitim için bellek-eşlemli token dizisi. Dosya RAM'e tamamen yüklenmez.
pub struct TokenDataset {
    mmap: Mmap,
}

impl TokenDataset {
    pub fn open(path: &Path) -> Result<Self> {
        let f = File::open(path).with_context(|| format!("açılamadı: {}", path.display()))?;
        // SAFETY: dosya eğitim sırasında değiştirilmemelidir (salt-okunur kullanım).
        let mmap = unsafe { Mmap::map(&f)? };
        if mmap.len() % 2 != 0 {
            bail!("bozuk shard (tek bayt sayısı): {}", path.display());
        }
        Ok(Self { mmap })
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
            let start = rng.gen_range(0..self.len() - seq_len - 1);
            for i in start..start + seq_len {
                x.push(self.get(i) as i64);
                y.push(self.get(i + 1) as i64);
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
