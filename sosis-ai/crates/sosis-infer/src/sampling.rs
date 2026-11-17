//! Token örnekleme: tekrar cezası, temperature, top-k, top-p (nucleus).

use rand::Rng;

#[derive(Debug, Clone)]
pub struct SamplingParams {
    /// 0 → açgözlü (her zaman en olası token).
    pub temperature: f32,
    /// 0 → kapalı.
    pub top_k: usize,
    /// 1.0 → kapalı.
    pub top_p: f32,
    /// 1.0 → kapalı. Son `repetition_window` token'a uygulanır.
    pub repetition_penalty: f32,
    pub repetition_window: usize,
    pub max_new_tokens: usize,
    /// Çıktıda görüldüğünde üretimi durduran diziler.
    pub stop: Vec<String>,
    pub seed: u64,
}

impl Default for SamplingParams {
    fn default() -> Self {
        Self {
            temperature: 0.2,
            top_k: 40,
            top_p: 0.95,
            repetition_penalty: 1.1,
            repetition_window: 64,
            max_new_tokens: 256,
            stop: vec![],
            seed: 42,
        }
    }
}

/// `logits` üzerinde yerinde işlem yaparak bir sonraki token'ı seçer.
pub fn sample(logits: &mut [f32], history: &[u32], p: &SamplingParams, rng: &mut impl Rng) -> u32 {
    if p.repetition_penalty != 1.0 {
        let from = history.len().saturating_sub(p.repetition_window);
        for &t in &history[from..] {
            if let Some(l) = logits.get_mut(t as usize) {
                *l = if *l > 0.0 {
                    *l / p.repetition_penalty
                } else {
                    *l * p.repetition_penalty
                };
            }
        }
    }
    if p.temperature <= 0.0 {
        return argmax(logits);
    }

    let mut idx: Vec<usize> = (0..logits.len()).collect();
    let k = if p.top_k == 0 {
        logits.len()
    } else {
        p.top_k.min(logits.len())
    };
    if k < logits.len() {
        idx.select_nth_unstable_by(k - 1, |&a, &b| logits[b].total_cmp(&logits[a]));
        idx.truncate(k);
    }
    idx.sort_unstable_by(|&a, &b| logits[b].total_cmp(&logits[a]));

    let max = logits[idx[0]];
    let mut probs: Vec<f32> = idx
        .iter()
        .map(|&i| ((logits[i] - max) / p.temperature).exp())
        .collect();
    let sum: f32 = probs.iter().sum();
    probs.iter_mut().for_each(|x| *x /= sum);

    if p.top_p < 1.0 {
        let mut cum = 0.0;
        let mut cut = probs.len();
        for (i, &pr) in probs.iter().enumerate() {
            cum += pr;
            if cum >= p.top_p {
                cut = i + 1;
                break;
            }
        }
        probs.truncate(cut);
        idx.truncate(cut);
    }

    let total: f32 = probs.iter().sum();
    let mut r = rng.gen::<f32>() * total;
    for (i, &pr) in probs.iter().enumerate() {
        r -= pr;
        if r <= 0.0 {
            return idx[i] as u32;
        }
    }
    idx[idx.len() - 1] as u32
}

fn argmax(v: &[f32]) -> u32 {
    v.iter()
        .enumerate()
        .max_by(|a, b| a.1.total_cmp(b.1))
        .map(|(i, _)| i as u32)
        .unwrap_or(0)
}

#[cfg(test)]
mod tests {
    use super::*;
    use rand::SeedableRng;

    #[test]
    fn greedy_and_topk() {
        let mut rng = rand::rngs::StdRng::seed_from_u64(1);
        let base = vec![0.1, 3.0, 0.5, 2.9];
        let p = SamplingParams {
            temperature: 0.0,
            repetition_penalty: 1.0,
            ..Default::default()
        };
        assert_eq!(sample(&mut base.clone(), &[], &p, &mut rng), 1);
        let p = SamplingParams {
            temperature: 1.0,
            top_k: 2,
            top_p: 1.0,
            repetition_penalty: 1.0,
            ..Default::default()
        };
        for _ in 0..100 {
            let t = sample(&mut base.clone(), &[], &p, &mut rng);
            assert!(t == 1 || t == 3);
        }
    }

    #[test]
    fn repetition_penalty_changes_choice() {
        let mut rng = rand::rngs::StdRng::seed_from_u64(1);
        let p = SamplingParams {
            temperature: 0.0,
            repetition_penalty: 2.0,
            ..Default::default()
        };
        assert_eq!(sample(&mut [1.0, 3.0, 2.0], &[1], &p, &mut rng), 2);
    }
}
