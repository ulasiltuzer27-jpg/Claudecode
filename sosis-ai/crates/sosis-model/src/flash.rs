//! Bellek-verimli dikkat (FlashAttention-2 algoritması, blok blok).
//!
//! Standart dikkat, geri yayılım için `[b, h, t, t]` boyutlu skor matrisini saklar (O(t²) bellek).
//! Burada skorlar `blok × blok` parçalar halinde hesaplanır; ileri geçişte yalnızca çıktı ve satır
//! başına logsumexp saklanır, geri geçişte skorlar parça parça yeniden hesaplanır. Bellek O(t·d).
//!
//! Burn'ün "backend extension" mekanizmasıyla özel bir işlem olarak kaydedilir; `Autodiff`
//! uygulaması geri geçişi elle tanımlar. Algoritma backend'in ilkel işlemleriyle yazıldığı için
//! hem CPU (Flex) hem GPU (CubeCL/CUDA) üzerinde çalışır; ancak bu tek bir birleşik GPU çekirdeği
//! değildir — hız değil **bellek** kazancı amaçlanır.

use burn::backend::autodiff::checkpoint::strategy::CheckpointStrategy;
use burn::backend::autodiff::grads::Gradients;
use burn::backend::autodiff::ops::{Backward, Ops, OpsKind};
use burn::backend::autodiff::Autodiff;
use burn::backend::tensor::FloatTensor;
use burn::backend::{backend_extension, Backend, TensorMetadata};
use burn::tensor::{Shape, Slice, TensorData};

use std::sync::atomic::{AtomicUsize, Ordering};

static BLOCK_SIZE: AtomicUsize = AtomicUsize::new(128);

/// Blok boyu (sorgu ve anahtar). Küçük blok = daha az bellek, daha çok işlem çağrısı.
pub fn set_block_size(n: usize) {
    BLOCK_SIZE.store(n.max(8), Ordering::Relaxed);
}

fn block() -> usize {
    BLOCK_SIZE.load(Ordering::Relaxed)
}
const NEG: f32 = -1.0e30;

fn slice_t<B: Backend>(x: FloatTensor<B>, start: usize, end: usize) -> FloatTensor<B> {
    let dims = x.shape().dims::<4>();
    B::float_slice(
        x,
        &[
            Slice::new(0, Some(dims[0] as isize), 1),
            Slice::new(0, Some(dims[1] as isize), 1),
            Slice::new(start as isize, Some(end as isize), 1),
            Slice::new(0, Some(dims[3] as isize), 1),
        ],
    )
}

/// Köşegen bloğunda nedensel maske: anahtar konumu > sorgu konumu olanlara −1e30.
fn causal_bias<B: Backend>(
    like: &FloatTensor<B>,
    qs: usize,
    lq: usize,
    ks: usize,
    lk: usize,
) -> Option<FloatTensor<B>> {
    if ks + lk <= qs + 1 {
        return None; // tümüyle geçmişte
    }
    let mut v = Vec::with_capacity(lq * lk);
    for i in 0..lq {
        for j in 0..lk {
            v.push(if ks + j > qs + i { NEG } else { 0.0 });
        }
    }
    let dims = like.shape().dims::<4>();
    let bias = B::float_from_data(TensorData::new(v, [1, 1, lq, lk]), &like.device());
    let bias = B::float_cast(bias, like.dtype().into());
    Some(B::float_expand(
        bias,
        Shape::new([dims[0], dims[1], lq, lk]),
    ))
}

/// İleri geçiş: (çıktı `[b,h,t,d]`, logsumexp `[b,h,t,1]`).
pub fn forward<B: Backend>(
    q: FloatTensor<B>,
    k: FloatTensor<B>,
    v: FloatTensor<B>,
    causal: bool,
    block: usize,
) -> (FloatTensor<B>, FloatTensor<B>) {
    let [b, h, t, d] = q.shape().dims::<4>();
    let tk = k.shape().dims::<4>()[2];
    let scale = 1.0 / (d as f32).sqrt();
    let device = q.device();
    let dtype = q.dtype().into();
    let (mut outs, mut lses) = (Vec::new(), Vec::new());
    let mut qs = 0;
    while qs < t {
        let qe = (qs + block).min(t);
        let lq = qe - qs;
        let qi = B::float_mul_scalar(slice_t::<B>(q.clone(), qs, qe), scale.into());
        let mut m = B::float_full(Shape::new([b, h, lq, 1]), NEG.into(), &device, dtype);
        let mut l = B::float_zeros(Shape::new([b, h, lq, 1]), &device, dtype);
        let mut acc = B::float_zeros(Shape::new([b, h, lq, d]), &device, dtype);
        // Nedensel: sorgu i, anahtar ≤ (tk − t) + i görür (önbellekli ofset için genel).
        let offset = tk - t;
        let kend = if causal { (qe + offset).min(tk) } else { tk };
        let mut ks = 0;
        while ks < kend {
            let ke = (ks + block).min(kend);
            let kj = slice_t::<B>(k.clone(), ks, ke);
            let vj = slice_t::<B>(v.clone(), ks, ke);
            let mut s = B::float_matmul(qi.clone(), B::float_transpose(kj));
            if causal {
                if let Some(bias) = causal_bias::<B>(&s, qs + offset, lq, ks, ke - ks) {
                    s = B::float_add(s, bias);
                }
            }
            let mnew = B::float_max_dim(
                B::float_cat(vec![m.clone(), B::float_max_dim(s.clone(), 3)], 3),
                3,
            );
            let p = B::float_exp(B::float_sub(s, mnew.clone()));
            let corr = B::float_exp(B::float_sub(m, mnew.clone()));
            l = B::float_add(
                B::float_mul(l, corr.clone()),
                B::float_sum_dim(p.clone(), 3),
            );
            acc = B::float_add(B::float_mul(acc, corr), B::float_matmul(p, vj));
            m = mnew;
            ks = ke;
        }
        outs.push(B::float_div(acc, l.clone()));
        lses.push(B::float_add(m, B::float_log(l)));
        qs = qe;
    }
    (B::float_cat(outs, 2), B::float_cat(lses, 2))
}

/// Geri geçiş: (dq, dk, dv).
#[allow(clippy::too_many_arguments)]
pub fn backward<B: Backend>(
    q: FloatTensor<B>,
    k: FloatTensor<B>,
    v: FloatTensor<B>,
    o: FloatTensor<B>,
    lse: FloatTensor<B>,
    d_o: FloatTensor<B>,
    causal: bool,
    block: usize,
) -> (FloatTensor<B>, FloatTensor<B>, FloatTensor<B>) {
    let [_, _, t, d] = q.shape().dims::<4>();
    let tk = k.shape().dims::<4>()[2];
    let offset = tk - t;
    let scale = 1.0 / (d as f32).sqrt();
    // D_i = Σ_j dO_ij · O_ij
    let dsum = B::float_sum_dim(B::float_mul(d_o.clone(), o), 3);
    let qblocks: Vec<(usize, usize)> = (0..t)
        .step_by(block)
        .map(|s| (s, (s + block).min(t)))
        .collect();
    let mut dq: Vec<Option<FloatTensor<B>>> = vec![None; qblocks.len()];
    let (mut dks, mut dvs) = (Vec::new(), Vec::new());
    let mut ks = 0;
    while ks < tk {
        let ke = (ks + block).min(tk);
        let kj = slice_t::<B>(k.clone(), ks, ke);
        let vj = slice_t::<B>(v.clone(), ks, ke);
        let (mut dk_j, mut dv_j): (Option<FloatTensor<B>>, Option<FloatTensor<B>>) = (None, None);
        for (bi, &(qs, qe)) in qblocks.iter().enumerate() {
            if causal && qe + offset <= ks {
                continue; // bu sorgu bloğu bu anahtarları görmez
            }
            let qi = slice_t::<B>(q.clone(), qs, qe);
            let doi = slice_t::<B>(d_o.clone(), qs, qe);
            let lse_i = slice_t::<B>(lse.clone(), qs, qe);
            let d_i = slice_t::<B>(dsum.clone(), qs, qe);
            let mut s = B::float_matmul(
                B::float_mul_scalar(qi.clone(), scale.into()),
                B::float_transpose(kj.clone()),
            );
            if causal {
                if let Some(bias) = causal_bias::<B>(&s, qs + offset, qe - qs, ks, ke - ks) {
                    s = B::float_add(s, bias);
                }
            }
            let p = B::float_exp(B::float_sub(s, lse_i));
            let dv_part = B::float_matmul(B::float_transpose(p.clone()), doi.clone());
            dv_j = Some(match dv_j {
                Some(x) => B::float_add(x, dv_part),
                None => dv_part,
            });
            let dp = B::float_matmul(doi, B::float_transpose(vj.clone()));
            let ds = B::float_mul(p, B::float_sub(dp, d_i));
            let dq_part =
                B::float_mul_scalar(B::float_matmul(ds.clone(), kj.clone()), scale.into());
            dq[bi] = Some(match dq[bi].take() {
                Some(x) => B::float_add(x, dq_part),
                None => dq_part,
            });
            let dk_part =
                B::float_mul_scalar(B::float_matmul(B::float_transpose(ds), qi), scale.into());
            dk_j = Some(match dk_j {
                Some(x) => B::float_add(x, dk_part),
                None => dk_part,
            });
        }
        let zeros = || B::float_zeros(kj.shape(), &kj.device(), kj.dtype().into());
        dks.push(dk_j.unwrap_or_else(zeros));
        dvs.push(dv_j.unwrap_or_else(zeros));
        ks = ke;
    }
    let dq: Vec<FloatTensor<B>> = qblocks
        .iter()
        .zip(dq)
        .map(|(&(qs, qe), g)| {
            g.unwrap_or_else(|| {
                let x = slice_t::<B>(q.clone(), qs, qe);
                B::float_zeros(x.shape(), &x.device(), x.dtype().into())
            })
        })
        .collect();
    (
        B::float_cat(dq, 2),
        B::float_cat(dks, 2),
        B::float_cat(dvs, 2),
    )
}

/// Özel işlem: `q, k, v: [b, h, t, d]` → `[b, h, t, d]`.
#[backend_extension(Flex: cfg(feature = "cpu"), Cube: cfg(feature = "cuda"), Autodiff)]
pub trait FlashOps: Backend {
    fn flash_attention(
        q: FloatTensor<Self>,
        k: FloatTensor<Self>,
        v: FloatTensor<Self>,
        causal: bool,
    ) -> FloatTensor<Self> {
        forward::<Self>(q, k, v, causal, block()).0
    }
}

#[cfg(feature = "cpu")]
impl FlashOps for burn::backend::Flex {}
#[cfg(feature = "cuda")]
impl FlashOps for burn::backend::Cube {}

#[derive(Debug)]
struct FlashBackward;

impl<B: Backend> Backward<B, 3> for FlashBackward {
    type State = (
        FloatTensor<B>,
        FloatTensor<B>,
        FloatTensor<B>,
        FloatTensor<B>,
        FloatTensor<B>,
        bool,
    );

    fn backward(
        self,
        ops: Ops<Self::State, 3>,
        grads: &mut Gradients,
        _: &mut burn::backend::autodiff::checkpoint::base::Checkpointer,
    ) {
        let (q, k, v, o, lse, causal) = ops.state;
        let d_o = grads.consume::<B>(&ops.node);
        let (dq, dk, dv) = backward::<B>(q, k, v, o, lse, d_o, causal, block());
        let [pq, pk, pv] = ops.parents;
        if let Some(n) = pq {
            grads.register::<B>(n.id, dq);
        }
        if let Some(n) = pk {
            grads.register::<B>(n.id, dk);
        }
        if let Some(n) = pv {
            grads.register::<B>(n.id, dv);
        }
    }
}

impl<B: FlashOps, C: CheckpointStrategy> FlashOps for Autodiff<B, C> {
    fn flash_attention(
        q: FloatTensor<Self>,
        k: FloatTensor<Self>,
        v: FloatTensor<Self>,
        causal: bool,
    ) -> FloatTensor<Self> {
        let (qp, kp, vp) = (
            q.primitive().clone(),
            k.primitive().clone(),
            v.primitive().clone(),
        );
        match FlashBackward
            .prepare::<C>([q.node(), k.node(), v.node()])
            .compute_bound()
            .stateful()
        {
            OpsKind::Tracked(prep) => {
                let (o, lse) = forward::<B>(qp.clone(), kp.clone(), vp.clone(), causal, block());
                prep.finish((qp, kp, vp, o.clone(), lse, causal), o)
            }
            OpsKind::UnTracked(prep) => prep.finish(B::flash_attention(qp, kp, vp, causal)),
        }
    }
}

/// Yüksek seviye sarmalayıcı.
pub fn flash_attention(
    q: burn::tensor::Tensor<4>,
    k: burn::tensor::Tensor<4>,
    v: burn::tensor::Tensor<4>,
    causal: bool,
) -> burn::tensor::Tensor<4> {
    use burn::backend::Dispatch;
    burn::tensor::Tensor::from_dispatch(Dispatch::flash_attention(
        q.into_dispatch(),
        k.into_dispatch(),
        v.into_dispatch(),
        causal,
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    use burn::tensor::module::attention;
    use burn::tensor::ops::AttentionModuleOptions;
    use burn::tensor::{Device, Distribution, Tensor};

    fn close(a: &Tensor<4>, b: &Tensor<4>, tol: f32, what: &str) {
        let a: Vec<f32> = a.to_data().convert::<f32>().try_to_vec().unwrap();
        let b: Vec<f32> = b.to_data().convert::<f32>().try_to_vec().unwrap();
        let m = a
            .iter()
            .zip(&b)
            .map(|(x, y)| (x - y).abs())
            .fold(0f32, f32::max);
        assert!(m < tol, "{what}: en büyük fark {m}");
    }

    #[test]
    fn matches_standard_attention_forward_and_backward() {
        let dev = Device::flex().autodiff();
        set_block_size(8); // 37 token → 5 blok, köşegen ve tam bloklar birlikte
        for causal in [true, false] {
            let mk = || Tensor::<4>::random([2, 3, 37, 16], Distribution::Normal(0.0, 1.0), &dev);
            let (q0, k0, v0) = (mk(), mk(), mk());
            let w =
                Tensor::<4>::random([2, 3, 37, 16], Distribution::Normal(0.0, 1.0), &dev).detach();
            let run = |flash: bool| {
                let (q, k, v) = (
                    q0.clone().detach().require_grad(),
                    k0.clone().detach().require_grad(),
                    v0.clone().detach().require_grad(),
                );
                let out = if flash {
                    flash_attention(q.clone(), k.clone(), v.clone(), causal)
                } else {
                    attention(
                        q.clone(),
                        k.clone(),
                        v.clone(),
                        None,
                        None,
                        AttentionModuleOptions {
                            scale: None,
                            softcap: None,
                            is_causal: causal,
                        },
                    )
                };
                let g = (out.clone() * w.clone()).sum().backward();
                (
                    out.detach(),
                    q.grad(&g).unwrap(),
                    k.grad(&g).unwrap(),
                    v.grad(&g).unwrap(),
                )
            };
            let (of, dqf, dkf, dvf) = run(true);
            let (os, dqs, dks, dvs) = run(false);
            close(&of, &os, 1e-4, "çıktı");
            close(&dqf, &dqs, 1e-3, "dq");
            close(&dkf, &dks, 1e-3, "dk");
            close(&dvf, &dvs, 1e-3, "dv");
        }
        set_block_size(128);
    }

    /// Bellek ölçümü (elle çalıştırılır):
    /// `SOSIS_ATTN=flash cargo test --release -p sosis-model mem_probe -- --ignored --nocapture`
    #[test]
    #[ignore]
    fn mem_probe() {
        let mode = std::env::var("SOSIS_ATTN").unwrap_or_else(|_| "standard".into());
        let t: usize = std::env::var("SOSIS_T")
            .ok()
            .and_then(|v| v.parse().ok())
            .unwrap_or(4096);
        let dev = Device::flex().autodiff();
        let mk = || {
            Tensor::<4>::random([1, 8, t, 64], Distribution::Normal(0.0, 1.0), &dev).require_grad()
        };
        let (q, k, v) = (mk(), mk(), mk());
        let start = std::time::Instant::now();
        let o = if mode == "flash" {
            flash_attention(q.clone(), k.clone(), v.clone(), true)
        } else {
            attention(
                q.clone(),
                k.clone(),
                v.clone(),
                None,
                None,
                AttentionModuleOptions {
                    scale: None,
                    softcap: None,
                    is_causal: true,
                },
            )
        };
        let g = o.sum().backward();
        let _ = q.grad(&g).unwrap();
        let hwm = std::fs::read_to_string("/proc/self/status")
            .unwrap_or_default()
            .lines()
            .find(|l| l.starts_with("VmHWM"))
            .unwrap_or("VmHWM: ?")
            .to_string();
        eprintln!(
            "{mode} t={t}: {:.2}s, tepe {hwm}",
            start.elapsed().as_secs_f32()
        );
    }
}
