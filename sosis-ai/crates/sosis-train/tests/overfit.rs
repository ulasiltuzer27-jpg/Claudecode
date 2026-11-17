//! Uçtan uca doğruluk testleri: küçük bir model, tekrarlanan tek bir kod parçasını ezberleyebilmeli.
//! Kayıp düşmüyorsa eğitim döngüsünde (gradyan, optimizer, veri kaydırma) bir hata vardır.
//! Aynı test doğrudan yol, optimizer offload + bf16, init_from ve damıtma için çalıştırılır.

use std::path::{Path, PathBuf};

use sosis_data::{CollectOptions, FilterOptions, PrepareOptions};
use sosis_model::{ModelConfig, Precision};
use sosis_tokenizer::BpeTrainer;
use sosis_train::{DistillConfig, RunConfig, TrainConfig};

const SNIPPET: &str = "fn fibonacci(n: u64) -> u64 {\n    match n {\n        0 => 0,\n        1 => 1,\n        _ => fibonacci(n - 1) + fibonacci(n - 2),\n    }\n}\n";

/// Geçici klasörde tokenizer + veri hazırlar.
fn setup(name: &str) -> PathBuf {
    let root = std::env::temp_dir().join(format!("sosis_{name}_{}", std::process::id()));
    let _ = std::fs::remove_dir_all(&root);
    let src = root.join("src");
    std::fs::create_dir_all(&src).unwrap();
    // Farklı dosyalar (dedup'a takılmasın diye küçük farklarla).
    for i in 0..40 {
        std::fs::write(
            src.join(format!("f{i}.rs")),
            format!("// dosya {i}\n{SNIPPET}"),
        )
        .unwrap();
    }
    let mut trainer = BpeTrainer::new();
    trainer.feed(SNIPPET);
    let tok = trainer.train(400, |_, _| {});
    tok.save(root.join("tok.json")).unwrap();
    sosis_data::prepare(
        &tok,
        &PrepareOptions {
            inputs: vec![src],
            out_dir: root.join("data"),
            fim_rate: 0.0,
            val_percent: 0.0,
            threads: 2,
            seed: 1,
            collect: CollectOptions::default(),
            filter: FilterOptions::default(),
        },
        |_, _| {},
    )
    .unwrap();
    root
}

fn config(root: &Path, run: &str, d_model: usize, kv: Option<usize>) -> RunConfig {
    RunConfig {
        model: ModelConfig {
            name: run.into(),
            vocab_size: 400,
            d_model,
            n_layers: 2,
            n_heads: 4,
            n_kv_heads: kv,
            ffn_hidden: 2 * d_model,
            max_seq_len: 64,
            rope_theta: 10_000.0,
            rope_scaling: 1.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        },
        train: TrainConfig {
            data_dir: root.join("data"),
            tokenizer: root.join("tok.json"),
            out_dir: root.join(run),
            backend: "cpu".into(),
            precision: Precision::F32,
            gradient_checkpointing: false,
            optimizer_offload: false,
            init_from: None,
            max_ram_gb: 28.0,
            prefetch_steps: 2,
            seq_len: 32,
            micro_batch: 8,
            grad_accum: 1,
            max_steps: 120,
            warmup_steps: 10,
            lr: 3e-3,
            min_lr: 3e-4,
            weight_decay: 0.0,
            beta1: 0.9,
            beta2: 0.95,
            grad_clip: 1.0,
            log_every: 10,
            eval_every: 60,
            eval_batches: 2,
            save_every: 60,
            keep_checkpoints: 1,
            seed: 7,
        },
        distill: None,
        inference: Default::default(),
    }
}

/// Eğitir; (ilk kayıp, sonuç) döndürür.
fn run(cfg: &RunConfig, resume: bool) -> (f32, sosis_train::TrainOutcome) {
    let mut first = None;
    let out = sosis_train::train(cfg, resume, |r| {
        first.get_or_insert(r.loss);
    })
    .unwrap();
    (first.unwrap(), out)
}

#[test]
fn tiny_model_memorizes_snippet() {
    let root = setup("overfit");
    let cfg = config(&root, "run", 64, None);
    let (first, out) = run(&cfg, false);
    eprintln!("doğrudan: kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(first > 4.5, "başlangıç kaybı ~ln(400)=6 olmalı, {first}");
    assert!(
        out.last_loss < 1.0,
        "model ezberleyemedi: son kayıp {}",
        out.last_loss
    );
    assert!(cfg.train.out_dir.join("best.bpk").exists());

    // Checkpoint'ten devam: adım sayısı korunmalı.
    let mut cfg2 = cfg.clone();
    cfg2.train.max_steps = 130;
    let (_, out2) = run(&cfg2, true);
    assert_eq!(out2.final_step, 130);
    assert!(
        out2.last_loss < 1.5,
        "devamda kayıp patladı: {}",
        out2.last_loss
    );

    // init_from: başka bir eğitimin ağırlıklarıyla başlayan model düşük kayıpla başlar.
    let mut warm = config(&root, "warm", 64, None);
    warm.train.init_from = Some(cfg.train.out_dir.clone());
    warm.train.max_steps = 5;
    warm.train.warmup_steps = 2;
    warm.train.lr = 1e-4;
    let (wfirst, _) = run(&warm, false);
    eprintln!("init_from ilk kayıp: {wfirst:.3}");
    assert!(wfirst < 1.5, "init_from çalışmadı: {wfirst}");

    // Damıtma: küçük GQA'lı öğrenci, ezberlemiş öğretmenden öğrenir.
    let mut student = config(&root, "student", 32, Some(2));
    student.distill = Some(DistillConfig {
        teacher: cfg.train.out_dir.clone(),
        alpha: 0.5,
        temperature: 2.0,
    });
    let (sfirst, sout) = run(&student, false);
    eprintln!("damıtma: kayıp {sfirst:.3} → {:.3}", sout.last_loss);
    assert!(
        sout.last_loss < sfirst * 0.4,
        "{sfirst} → {}",
        sout.last_loss
    );

    std::fs::remove_dir_all(&root).ok();
}

#[test]
fn offload_bf16_memorizes_and_resumes() {
    let root = setup("offload");
    let mut cfg = config(&root, "run", 64, Some(2));
    cfg.train.optimizer_offload = true;
    cfg.train.precision = Precision::Bf16;
    cfg.train.grad_accum = 2;
    cfg.train.micro_batch = 4;
    let (first, out) = run(&cfg, false);
    eprintln!("offload+bf16: kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(first > 4.5);
    assert!(
        out.last_loss < 1.0,
        "offload ezberleyemedi: {}",
        out.last_loss
    );

    let mut cfg2 = cfg.clone();
    cfg2.train.max_steps = 130;
    let (_, out2) = run(&cfg2, true);
    assert_eq!(out2.final_step, 130);
    assert!(
        out2.last_loss < 1.5,
        "offload devamında kayıp patladı: {}",
        out2.last_loss
    );

    std::fs::remove_dir_all(&root).ok();
}
