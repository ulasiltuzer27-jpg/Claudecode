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
            near_dedup: None,
            decontaminate: vec![],
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
            lora: None,
            init_from: None,
            max_ram_gb: 28.0,
            prefetch_steps: 2,
            bench_every: None,
            bench_suite: "benchmarks".into(),
            bench_samples: 1,
            bench_backend: "cpu".into(),
            bench_max_tokens: 64,
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
        dpo: None,
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

#[test]
fn sft_learns_answers_only() {
    use sosis_data::sft::{prepare_sft, SftExample};
    let root = setup("sft");
    let tok = sosis_tokenizer::Tokenizer::load(root.join("tok.json")).unwrap();
    let examples: Vec<SftExample> = (0..30)
        .map(|i| SftExample::qa(format!("soru {i}: fibonacci yaz"), SNIPPET))
        .collect();
    prepare_sft(&tok, &examples, &root.join("data"), 0.0, None).unwrap();
    let mut cfg = config(&root, "sft", 64, Some(2));
    cfg.train.max_steps = 100;
    let (first, out) = run(&cfg, false);
    eprintln!("sft: kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(out.last_loss.is_finite());
    assert!(
        out.last_loss < 0.5,
        "cevap ezberlenemedi: {}",
        out.last_loss
    );
    std::fs::remove_dir_all(&root).ok();
}

#[test]
fn lora_adapts_frozen_base_to_new_code() {
    use sosis_model::lora::LoraConfig;
    let root = setup("lora");
    // 1) Taban: fibonacci'yi ezberler.
    let base = config(&root, "base", 64, Some(2));
    let (_, bout) = run(&base, false);
    assert!(bout.last_loss < 1.0);
    let base_file = base.train.out_dir.join("best.bpk");
    let base_bytes = std::fs::read(&base_file).unwrap();

    // 2) Yeni veri: farklı bir kod parçası.
    let other = "fn gcd(a: u64, b: u64) -> u64 {\n    if b == 0 {\n        a\n    } else {\n        gcd(b, a % b)\n    }\n}\n";
    let tok = sosis_tokenizer::Tokenizer::load(root.join("tok.json")).unwrap();
    let src2 = root.join("src2");
    std::fs::create_dir_all(&src2).unwrap();
    for i in 0..40 {
        std::fs::write(
            src2.join(format!("g{i}.rs")),
            format!("// gcd {i}\n{other}"),
        )
        .unwrap();
    }
    sosis_data::prepare(
        &tok,
        &PrepareOptions {
            inputs: vec![src2],
            out_dir: root.join("data2"),
            fim_rate: 0.0,
            val_percent: 0.0,
            threads: 2,
            seed: 1,
            collect: CollectOptions::default(),
            filter: FilterOptions::default(),
            near_dedup: None,
            decontaminate: vec![],
        },
        |_, _| {},
    )
    .unwrap();

    // 3) LoRA (gradient checkpointing açık): taban donuk, yalnızca adaptör öğrenir.
    let mut l = config(&root, "lora", 64, Some(2));
    l.train.data_dir = root.join("data2");
    l.train.init_from = Some(base.train.out_dir.clone());
    l.train.lora = Some(LoraConfig {
        rank: 8,
        alpha: 16.0,
        qlora: false,
    });
    l.train.gradient_checkpointing = true;
    l.train.lr = 1e-2;
    l.train.max_steps = 150;
    let (first, out) = run(&l, false);
    eprintln!("lora: kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(out.last_loss < first * 0.5, "{first} → {}", out.last_loss);
    // Taban dosyası değişmedi; adaptör tabandan çok küçük.
    assert_eq!(std::fs::read(&base_file).unwrap(), base_bytes);
    let adapter = std::fs::metadata(l.train.out_dir.join("best.bpk"))
        .unwrap()
        .len();
    assert!(
        adapter * 2 < base_bytes.len() as u64,
        "adaptör {adapter} B, taban {} B",
        base_bytes.len()
    );

    // 4) Devam: checkpoint'ten LoRA ile sürer.
    let mut l2 = l.clone();
    l2.train.max_steps = 160;
    let (_, out2) = run(&l2, true);
    assert_eq!(out2.final_step, 160);
    assert!(out2.last_loss < first * 0.6);
    std::fs::remove_dir_all(&root).ok();
}

#[test]
fn dpo_learns_preference() {
    use sosis_data::sft::{write_dpo_jsonl, DpoPair};
    use sosis_tokenizer::chat::{Message, Role};
    let root = setup("dpo");
    let base = config(&root, "base", 64, Some(2));
    run(&base, false);

    let pairs: Vec<DpoPair> = (0..24)
        .map(|i| DpoPair {
            context: vec![Message::new(Role::User, format!("soru {i}: fibonacci yaz"))],
            chosen: "fn fibonacci(n: u64) -> u64 { n }".into(),
            rejected: "olmaz, yazamam".into(),
        })
        .collect();
    let data = root.join("dpo.jsonl");
    write_dpo_jsonl(&data, &pairs).unwrap();

    let mut cfg = config(&root, "dpo", 64, Some(2));
    cfg.train.init_from = Some(base.train.out_dir.clone());
    cfg.train.max_steps = 40;
    cfg.train.warmup_steps = 5;
    cfg.train.lr = 1e-3;
    cfg.train.micro_batch = 4;
    cfg.train.log_every = 1;
    cfg.dpo = Some(sosis_train::dpo::DpoConfig { data, beta: 0.5 });
    let (first, out) = run(&cfg, false);
    eprintln!("dpo: kayıp {first:.3} → {:.3}", out.last_loss);
    assert!(
        (first - std::f32::consts::LN_2).abs() < 0.05,
        "başlangıç ln2 olmalı: {first}"
    );
    assert!(
        out.last_loss < 0.2,
        "tercih öğrenilemedi: {}",
        out.last_loss
    );
    assert!(cfg.train.out_dir.join("best.bpk").exists());
    std::fs::remove_dir_all(&root).ok();
}
