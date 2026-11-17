//! Spekülatif kod çözme: küçük **taslak** model (ör. Sosy-Flash) birkaç token önerir, büyük
//! **hedef** model (ör. Sosy-Pro) hepsini tek ileri geçişte doğrular.
//!
//! Kabul kuralı (Leviathan vd. 2023 / Chen vd. 2023): taslak token `x`, `min(1, p(x)/q(x))`
//! olasılıkla kabul edilir; reddedilirse `max(0, p − q)` dağılımından örnek çekilir. Böylece
//! çıktı dağılımı **yalnızca hedef modelle üretimle aynıdır**; temperature = 0'da çıktı birebir aynıdır.
//! İki model aynı tokenizer'ı kullanmalıdır.

use anyhow::{bail, Result};
use rand::rngs::StdRng;
use rand::{Rng, SeedableRng};
use sosis_tokenizer::{EOS, FILE_SEP};

use crate::sampling::{distribution, sample_dense};
use crate::{Generator, SamplingParams};

#[derive(Debug, Clone, Copy, Default)]
pub struct SpecStats {
    /// Taslağın önerdiği token sayısı.
    pub drafted: usize,
    /// Hedefin kabul ettiği taslak token sayısı.
    pub accepted: usize,
    /// Hedef modelin ileri geçiş sayısı (ön-doldurma hariç).
    pub target_passes: usize,
}

impl SpecStats {
    pub fn acceptance(&self) -> f64 {
        self.accepted as f64 / self.drafted.max(1) as f64
    }
}

/// İki modelin birlikte kullanılabilir olup olmadığını kontrol eder.
pub fn check_compatible(target: &Generator, draft: &Generator) -> Result<()> {
    if target.config.vocab_size != draft.config.vocab_size
        || target.tokenizer.vocab_size() != draft.tokenizer.vocab_size()
    {
        bail!(
            "taslak ({}) ve hedef ({}) aynı tokenizer'ı kullanmalı",
            draft.config.name,
            target.config.name
        );
    }
    Ok(())
}

/// `p` hedef, `q` taslak dağılımıyla `x` için kabul/ret; ret durumunda düzeltilmiş örnek.
fn accept_or_resample(p: &[f32], q: &[f32], x: u32, rng: &mut impl Rng) -> (bool, u32) {
    let (px, qx) = (p[x as usize], q[x as usize]);
    let accept = if qx <= 0.0 {
        false
    } else if px >= qx {
        true
    } else {
        rng.gen::<f32>() < px / qx
    };
    if accept {
        return (true, x);
    }
    let residual: Vec<f32> = p.iter().zip(q).map(|(a, b)| (a - b).max(0.0)).collect();
    if residual.iter().sum::<f32>() <= 0.0 {
        return (false, sample_dense(p, rng));
    }
    (false, sample_dense(&residual, rng))
}

/// Spekülatif üretim. `k`: tur başına taslak token sayısı.
/// Bağlam sınırına yaklaşılırsa normal (yalnızca hedef) üretime geri döner.
pub fn generate(
    target: &Generator,
    draft: &Generator,
    prompt: &[u32],
    params: &SamplingParams,
    k: usize,
    mut on_token: impl FnMut(u32) -> bool,
) -> (Vec<u32>, SpecStats) {
    let mut stats = SpecStats::default();
    let k = k.max(1);
    let ctx = target.context_len().min(draft.context_len());
    if prompt.is_empty() || prompt.len() + params.max_new_tokens + k + 2 > ctx {
        let out = target.generate_plain(prompt, params, on_token);
        return (out, stats);
    }
    let mut rng = StdRng::seed_from_u64(params.seed);
    let (mut t_cache, mut t_last) = target.prefill(prompt);
    let (mut d_cache, mut d_last) = draft.prefill(prompt);
    let mut history: Vec<u32> = prompt.to_vec();
    // Hedef önbelleğe henüz girmemiş, çıktıya yazılmış token.
    let mut pending: Option<u32> = None;
    let mut out = Vec::new();

    'outer: while out.len() < params.max_new_tokens {
        // 1) Taslak: (varsa) bekleyen token'ı işle, sonra k token öner.
        if let Some(tok) = pending {
            d_last = draft.step(&[tok], &mut d_cache);
        }
        let room = (params.max_new_tokens - out.len()).min(k);
        let mut drafts = Vec::with_capacity(room);
        let mut qs = Vec::with_capacity(room);
        let mut h = history.clone();
        for i in 0..room {
            let q = distribution(&d_last, &h, params);
            let x = sample_dense(&q, &mut rng);
            drafts.push(x);
            qs.push(q);
            h.push(x);
            if x == EOS || x == FILE_SEP {
                break;
            }
            if i + 1 < room {
                d_last = draft.step(&[x], &mut d_cache);
            }
        }
        stats.drafted += drafts.len();

        // 2) Hedef: bekleyen + taslaklar tek geçişte.
        let base = t_cache.len;
        let mut chunk: Vec<u32> = pending.into_iter().collect();
        chunk.extend(&drafts);
        let logits = target.step_all(&chunk, &mut t_cache);
        stats.target_passes += 1;
        // ps[i]: drafts[i] konumundaki hedef dağılımının logit'leri; ps[n]: bonus.
        let mut ps: Vec<&[f32]> = Vec::with_capacity(drafts.len() + 1);
        if pending.is_none() {
            ps.push(&t_last);
        }
        ps.extend(logits.iter().map(|v| v.as_slice()));
        let offset = pending.is_some() as usize;

        // 3) Kabul / ret.
        let mut accepted = 0;
        for (i, &x) in drafts.iter().enumerate() {
            let p = distribution(ps[i], &history, params);
            let (ok, tok) = accept_or_resample(&p, &qs[i], x, &mut rng);
            out.push(tok);
            history.push(tok);
            let stop = tok == EOS || tok == FILE_SEP || !on_token(tok);
            if ok {
                accepted += 1;
                stats.accepted += 1;
            }
            if stop {
                if tok == EOS || tok == FILE_SEP {
                    out.pop();
                }
                // Önbellekleri tutarlı bırak: kabul edilenlere kadar.
                t_cache.truncate(base + offset + accepted);
                d_cache.truncate((base + offset + accepted).min(d_cache.len));
                break 'outer;
            }
            if !ok {
                // Ret: hedef ve taslak önbellekleri kabul edilen öneke geri sarılır;
                // düzeltilmiş token bir sonraki turda işlenir.
                t_cache.truncate(base + offset + accepted);
                d_cache.truncate((base + offset + accepted).min(d_cache.len));
                pending = Some(tok);
                continue 'outer;
            }
        }
        // Hepsi kabul: bonus token hedefin son dağılımından.
        if out.len() >= params.max_new_tokens {
            break;
        }
        let p = distribution(ps[drafts.len()], &history, params);
        let bonus = sample_dense(&p, &mut rng);
        if bonus == EOS || bonus == FILE_SEP {
            break;
        }
        out.push(bonus);
        history.push(bonus);
        if !on_token(bonus) {
            break;
        }
        // Taslak, son taslak token'ı henüz işlemedi; bekleyen olarak ikisini de besle.
        let last = *drafts.last().unwrap();
        d_cache.truncate(base + offset + drafts.len() - 1);
        d_last = draft.step(&[last], &mut d_cache);
        t_last = Vec::new();
        pending = Some(bonus);
    }
    let _ = t_last;
    (out, stats)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::test_util::*;

    #[test]
    fn greedy_speculative_equals_target_greedy() {
        let target = generator(21, 32, 2);
        let p = greedy(24);
        let prompt: Vec<u32> = (30..42).collect();
        let reference = target.generate_plain(&prompt, &p, |_| true);
        target.clear_prefix_cache();

        // Farklı (daha küçük) taslak: çıktı yine birebir aynı olmalı.
        let draft = generator(99, 32, 1);
        for k in [1, 3, 5] {
            target.clear_prefix_cache();
            draft.clear_prefix_cache();
            let (out, st) = generate(&target, &draft, &prompt, &p, k, |_| true);
            assert_eq!(out, reference, "k={k}");
            assert!(st.target_passes <= reference.len());
        }

        // Taslak = hedef: her şey kabul edilir, geçiş sayısı ~ n/(k+1).
        target.clear_prefix_cache();
        let same = generator(21, 32, 2);
        let (out, st) = generate(&target, &same, &prompt, &p, 4, |_| true);
        assert_eq!(out, reference);
        assert_eq!(st.accepted, st.drafted, "{st:?}");
        assert!(st.target_passes * 4 <= reference.len() + 4, "{st:?}");
    }

    #[test]
    fn with_draft_is_transparent() {
        let reference = generator(21, 32, 2).complete("fn main", Some("a.rs"), &greedy(16), |_| {});
        let fast = generator(21, 32, 2)
            .with_draft(generator(99, 32, 1), 3)
            .unwrap();
        let out = fast.complete("fn main", Some("a.rs"), &greedy(16), |_| {});
        assert_eq!(out, reference);
        assert!(fast.last_spec.lock().unwrap().is_some());
    }

    #[test]
    fn sampling_mode_runs_and_respects_limit() {
        let target = generator(5, 32, 2);
        let draft = generator(6, 32, 1);
        let p = SamplingParams {
            temperature: 0.8,
            max_new_tokens: 20,
            ..Default::default()
        };
        let (out, st) = generate(
            &target,
            &draft,
            &(10..20).collect::<Vec<_>>(),
            &p,
            4,
            |_| true,
        );
        assert!(out.len() <= 20);
        assert!(st.accepted <= st.drafted);
    }
}
