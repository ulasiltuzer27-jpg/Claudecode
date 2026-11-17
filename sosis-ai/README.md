# 🌭 Sosis AI

Kendi ekran kartınızda **sıfırdan** eğitilen, kod üreten bir dil modeli. Tamamen Rust.
Ollama yok, Python yok, harici API yok — tokenizer, veri hattı, model, eğitim ve çıkarım bu depoda.

| Model | Parametre | Katman / d / kafa | Bağlam | Amaç |
|---|---|---|---|---|
| **Sosy 1.0** | 97.5M | 12 / 768 / 12 | 1024 | Kalite |
| **Sosy-Flash 1.0** | 20.5M | 8 / 384 / 6 | 512 | Hız |

Hedef donanım: **NVIDIA RTX 4060 Ti (8 GB)**.

> **Gerçekçi beklenti:** Bu boyutta sıfırdan eğitilen bir model kısa fonksiyonları, sık kalıpları,
> sözdizimini ve kod tamamlamayı öğrenir. ChatGPT gibi sohbet etmez ve uzun programları doğru yazamaz.
> Kalite en çok **verinin miktarı ve temizliğine** bağlıdır.

## Mimari

Llama tarzı, yalnızca-decoder Transformer:

- **RMSNorm** (pre-norm), **RoPE** konum kodlaması, **SwiGLU** ileri besleme
- Giriş embedding'i ile çıkış katmanı **ağırlık paylaşımlı**
- **KV-cache** ile hızlı üretim, bağlam dolunca kayan pencere
- **FIM (fill-in-the-middle)**: belgelerin %50'si `önek / sonek → orta` biçiminde eğitilir;
  böylece model imlecin ortasındaki kodu da tamamlayabilir
- Kendi **byte-level BPE** tokenizer'ımız: girinti (`\n    `), `::`, `->` gibi kod parçaları tek token olabilir;
  her UTF-8 metin (Türkçe yorumlar dahil) kayıpsız kodlanır

Derin öğrenme çerçevesi: [Burn](https://burn.dev) 0.22 (saf Rust; GPU için CUDA, test için CPU backend'i).

```
sosis-ai/
  configs/                 sosy-1.0.toml, sosy-flash-1.0.toml, sosy-tiny.toml (CPU demo)
  crates/
    sosis-tokenizer/       BPE eğitimi, encode/decode, özel token'lar
    sosis-data/            kod toplama, filtreleme, dedup, FIM, u16 token shard'ları
    sosis-model/           Transformer, KV-cache, backend seçimi
    sosis-train/           eğitim döngüsü, LR planı, checkpoint/devam
    sosis-infer/           örnekleme, tamamlama, FIM, perplexity
    sosis-cli/             `sosis` komutu
```

## Kurulum (Windows veya Linux)

1. **NVIDIA sürücüsü** (güncel) ve **CUDA Toolkit 12.x** — `nvcc --version` çalışmalı.
2. **Rust** (stable): <https://rustup.rs>
3. Derleme:

```bash
cd sosis-ai
cargo build --release --features cuda
# ikili dosya: target/release/sosis   (Windows'ta sosis.exe)
```

İlk CUDA derlemesi, Burn'ün GPU derleyicisi için ~45 MB'lık bir LLVM paketini bir kez indirir
(`~/.cache/tracel`); sonraki derlemeler çevrimdışı çalışır.

CUDA olmadan (yalnızca CPU, deneme amaçlı): `cargo build --release`.

`--backend auto` (varsayılan) GPU varsa onu, yoksa CPU'yu kullanır.

## Kullanım

### 1) Veri toplayın

Eğitim verisi = **kod dosyaları**. Kaynaklar:

- Kendi projeleriniz ve bilgisayarınızdaki açık kaynak depolar (`git clone ...`)
- İndirilmiş veri setleri `.jsonl` biçiminde (her satır `{"content": "...", "path": "..."}`).
  Örn. The Stack'in izinli lisanslı alt kümeleri. (İndirme bir kez yapılır; eğitim ve kullanım tamamen çevrimdışıdır.)

Sosy 1.0 için hedef: **birkaç GB** temiz kod (~1–3 milyar token). Flash için birkaç yüz MB bile iş görür.

`target/`, `node_modules/`, `.git/` gibi klasörler, 1 MB'tan büyük, küçültülmüş (minified) veya
otomatik üretilmiş dosyalar ve birebir kopyalar otomatik elenir.

### 2) Tokenizer eğitin

```bash
sosis tokenizer-train -i ~/kod -i ~/indirilenler/stack.jsonl --vocab-size 16384 -o tokenizer.json
```

### 3) Veriyi hazırlayın

```bash
sosis prepare -i ~/kod -i ~/indirilenler/stack.jsonl -t tokenizer.json -o data
# yalnızca belirli diller: --ext rs --ext py
```

Çıktı: `data/train.bin`, `data/val.bin`, `data/meta.json`.

### 4) Eğitin

```bash
sosis info -c configs/sosy-flash-1.0.toml configs/sosy-1.0.toml   # parametre + VRAM tahmini
sosis train -c configs/sosy-flash-1.0.toml                         # önce Flash ile başlayın
sosis train -c configs/sosy-1.0.toml
```

- İstediğiniz zaman `Ctrl+C` ile durdurup `--resume` ile kaldığınız yerden sürdürün.
- Kayıt: `runs/<model>/` — `best.bpk` (en iyi doğrulama), `checkpoints/`, `log.csv`.
- **VRAM yetmezse** (`out of memory`): `micro_batch`'i yarıya indirip `grad_accum`'u iki katına çıkarın
  (adım başına token aynı kalır). Sosy 1.0'da `gradient_checkpointing = true` açıktır.
- `precision = "bf16"` daha hızlı ve daha az bellek kullanır ama **deneyseldir** (ağırlıklar da bf16 tutulur);
  kayıp NaN olursa `f32`'ye dönün.

Tahmini süre (4060 Ti): Flash ~1.3B token → birkaç saat – 1 gün; Sosy ~2.6B token → birkaç gün.
İlk dakikalarda `nvidia-smi` ile VRAM'i ve `token/s` değerini kontrol edin.

### 5) Kullanın

```bash
# Kod tamamlama (--file dil ipucu verir)
sosis generate -r runs/sosy-flash-1.0 --file main.rs -p "fn fibonacci(n: u64) -> u64 {"

# Ortayı doldurma
sosis fim -r runs/sosy-1.0 --file util.py --prefix "def is_prime(n):\n" --suffix "\n    return True"

# Etkileşimli
sosis repl -r runs/sosy-1.0 --file main.rs

# Perplexity (düşük = model bu kodu daha iyi tanıyor)
sosis eval -r runs/sosy-1.0 -i benim_projem/src
```

Örnekleme: `--temperature` (0 = deterministik), `--top-k`, `--top-p`, `--repetition-penalty`,
`--max-tokens`, `--stop "\n\n"`, `--seed`.

## CPU'da 3 dakikalık deneme

GPU'suz bir makinede de tüm akışı görmek için:

```bash
cargo build --release
S=target/release/sosis
$S tokenizer-train -i .. --vocab-size 2048 -o demo/tokenizer.json
$S prepare -i .. -t demo/tokenizer.json -o demo/data --val-percent 5
$S train -c configs/sosy-tiny.toml
$S generate -r demo/runs/sosy-tiny --backend cpu --file Player.cs -p "public class Player"
```

## Testler

```bash
cargo test --release
```

Tokenizer gidiş-dönüş, FIM biçimi, filtreler, KV-cache'in tam ileri geçişle birebir aynı sonucu
vermesi, nedensellik (gelecek token sızıntısı yok), örnekleme ve küçük bir modelin bir kod parçasını
ezberleyip checkpoint'ten devam edebilmesi test edilir.

## Yol haritası

- [x] Tokenizer, veri hattı, model, eğitim, çıkarım, CLI
- [ ] Derle/çalıştır tabanlı değerlendirme seti (fonksiyon tamamla → `rustc`/`python` ile kontrol)
- [ ] Sosy-Flash'ı Sosy 1.0'dan damıtma (distillation)
- [ ] Soru → kod çiftleriyle talimat ince ayarı (`<|user|>` / `<|assistant|>` token'ları ayrılmış durumda)
- [ ] Parquet veri seti okuma
