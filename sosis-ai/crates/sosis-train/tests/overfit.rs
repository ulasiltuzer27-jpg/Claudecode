//! Uçtan uca doğruluk testi: küçük bir model, tekrarlanan tek bir kod parçasını ezberleyebilmeli.
//! Kayıp düşmüyorsa eğitim döngüsünde (gradyan, optimizer, veri kaydırma) bir hata vardır.

use std::path::PathBuf;

use sosis_data::{CollectOptions, FilterOptions, PrepareOptions};
use sosis_model::ModelConfig;
use sosis_tokenizer::BpeTrainer;
use sosis_train::{RunConfig, TrainConfig};

const SNIPPET: &str = "fn fibonacci(n: u64) -> u64 {\n    match n {\n        0 => 0,\n        1 => 1,\n        _ => fibonacci(n - 1) + fibonacci(n - 2),\n    }\n}\n";

#[test]
fn tiny_model_memorizes_snippet() {
    let root = std::env::temp_dir().join(format!("sosis_overfit_{}", std::process::id()));
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
    let tok_path = root.join("tok.json");
    tok.save(&tok_path).unwrap();

    let data = root.join("data");
    sosis_data::prepare(
        &tok,
        &PrepareOptions {
            inputs: vec![src],
            out_dir: data.clone(),
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

    let cfg = RunConfig {
        model: ModelConfig {
            name: "overfit".into(),
            vocab_size: 400,
            d_model: 64,
            n_layers: 2,
            n_heads: 4,
            ffn_hidden: 128,
            max_seq_len: 64,
            rope_theta: 10_000.0,
            norm_eps: 1e-5,
            init_std: 0.02,
        },
        train: TrainConfig {
            data_dir: data,
            tokenizer: tok_path,
            out_dir: root.join("run"),
            backend: "cpu".into(),
            precision: Default::default(),
            gradient_checkpointing: false,
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
    };

    let mut first = None;
    let out = sosis_train::train(&cfg, false, |r| {
        first.get_or_insert(r.loss);
    })
    .unwrap();
    let first = first.unwrap();
    eprintln!("kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(first > 4.5, "başlangıç kaybı ~ln(400)=6 olmalı, {first}");
    assert!(
        out.last_loss < 1.0,
        "model ezberleyemedi: son kayıp {}",
        out.last_loss
    );
    assert!(PathBuf::from(&cfg.train.out_dir).join("best.bpk").exists());

    // Checkpoint'ten devam: adım sayısı korunmalı.
    let mut cfg2 = cfg.clone();
    cfg2.train.max_steps = 130;
    let out2 = sosis_train::train(&cfg2, true, |_| {}).unwrap();
    assert_eq!(out2.final_step, 130);
    assert!(
        out2.last_loss < 1.5,
        "devamda kayıp patladı: {}",
        out2.last_loss
    );

    std::fs::remove_dir_all(&root).ok();
}
