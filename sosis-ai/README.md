# 🌭 Sosis AI

Kendi ekran kartınızda **sıfırdan** eğitilen, kod yazan bir dil modeli ailesi. Tamamen Rust.
Ollama yok, Python yok, harici API yok: tokenizer, veri hattı, model, eğitim, değerlendirme,
sohbet ve web arayüzü bu depoda ve tamamen kendi bilgisayarınızda çalışır.

| Model | Parametre | Katman / d / kafa (KV) | Eğitim bağlamı | Çıkarım bağlamı | Token | VRAM | RAM |
|---|---|---|---|---|---|---|---|
| **Sosy-Flash 1.0** | 27M | 8 / 384 / 6 | 512 | 1024 | 1.3B | ~4 GB | ~2 GB |
| **Sosy 1.0** | 110M | 12 / 768 / 12 | 1024 | 2048 | 2.6B | ~5 GB | ~2 GB |
| **Sosy-Pro 1.0** | 281M | 22 / 1024 / 16 (4) | 1024 → 2048 | 4096 | 4.5B | ~4.5–6 GB | ~6 GB |

VRAM ve RAM değerleri `sosis info` ile hesaplanan kaba tahminlerdir. Hedef donanım: **RTX 4060 Ti 8 GB**, 32 GB RAM.

> **Gerçekçi beklenti:** Bu boyutlarda sıfırdan eğitilen modeller kısa fonksiyonları, sık kalıpları,
> sözdizimini, kod tamamlamayı ve basit "şunu yaz" isteklerini öğrenir. Büyük ticari modeller gibi
> uzun programları doğru yazamazlar. Kalite en çok **verinin miktarı ve temizliğine** bağlıdır.
> Sosy-Pro için ~15–20 GB temiz kod ve 4060 Ti'de **1–2 hafta** sürekli eğitim gerekir.

## Neler var?

- **Mimari:** Llama tarzı yalnızca-decoder Transformer: RMSNorm, RoPE, SwiGLU, bağlı embedding.
  Pro'da **GQA** (4 KV kafası) var; KV-cache 4 kat küçük.
- **8 GB'a büyük model sığdırma:** **bf16** çalışma kopyası, **optimizer offload** (f32 ana ağırlıklar
  ve AdamW durumu RAM'de, gradyanlar CPU'da f32 biriktirilir) ve **gradient checkpointing**.
  Eğitim, `max_ram_gb = 28` sınırını izler.
- **Uzun bağlam:** 1024 ile eğitim, ardından 2048'e genişletme aşaması. Çıkarımda NTK-uyumlu RoPE
  ölçekleme ile 4096. Burn'ün autodiff'inde bellek-verimli dikkat geri yayılımı yok; 4096 ile eğitim
  8 GB'a sığmadığı için bu yol seçildi.
- **32k byte-level BPE tokenizer:** Kod için ön-bölümleme kullanır; girinti, `::` ve `->` tek token
  olabilir. Türkçe dahil her metni kayıpsız kodlar.
- **FIM (ortayı doldurma):** Belgelerin %50'si önek / sonek → orta biçiminde eğitilir.
- **Veri:** Klasörler, `.jsonl` ve `.parquet` (ör. The Stack) okunur. Filtre, tekilleştirme, aynı
  klasörün dosyalarını ardışık yazma ve arka planda batch ön-yükleme de var.
- **Değerlendirme (`sosis bench`):** 40 görev (20 Rust + 20 Python); üretilen kod gerçekten
  derlenip test edilir, sonuç **pass@k** olarak raporlanır.
- **Talimat ince ayarı (SFT):** Sohbet şablonu kullanılır ve kayıp yalnızca cevaplarda hesaplanır.
  Veri **API'siz** üretilebilir: kodunuzdaki belgelenmiş fonksiyonlar (`///`, C# `<summary>`,
  Python docstring) soru → kod örneklerine dönüşür.
- **Damıtma:** Flash, Pro'nun olasılık dağılımından da öğrenebilir.
- **Hafıza:**
  - *Sohbet hafızası:* konuşma geçmişi hatırlanır, oturumlar diske kaydedilir.
  - *Proje hafızası (RAG):* kod tabanınız indekslenir ve ilgili kod bağlama eklenir.
- **Yerel web arayüzü (`sosis serve`):** Sohbet, tamamlama ve FIM sekmeleri; cevap token token akar.
  Yalnızca `127.0.0.1`'den erişilir, internete bağlanmaz.

```
sosis-ai/
  configs/      sosy-{flash-1.0,1.0,pro-1.0,pro-1.0-ctx2048}.toml, *-chat.toml, sosy-tiny*.toml
  benchmarks/   rust.toml, python.toml (derle-çalıştır görevleri)
  crates/
    sosis-tokenizer/  BPE, sohbet şablonu
    sosis-data/       toplama, filtre, FIM, parquet, SFT verisi, maskeli shard'lar, RAM bekçisi
    sosis-model/      Transformer, GQA, RoPE ölçekleme, KV-cache, backend seçimi
    sosis-train/      eğitim döngüsü, optimizer offload, damıtma, checkpoint/devam
    sosis-infer/      örnekleme, tamamlama, FIM, sohbet hafızası, RAG
    sosis-bench/      pass@k değerlendirmesi
    sosis-cli/        `sosis` komutu + web arayüzü
```

## Kurulum (Windows veya Linux)

1. Güncel **NVIDIA sürücüsü** ve **CUDA Toolkit 12.x** (`nvcc --version` çalışmalı).
2. **Rust** (stable): <https://rustup.rs>. Windows'ta Visual Studio Build Tools (C++) da gerekir.
3. Derleme:

```bash
cd sosis-ai
cargo build --release --features cuda
# ikili dosya: target/release/sosis   (Windows'ta sosis.exe)
```

İlk CUDA derlemesi, Burn'ün GPU derleyicisi için ~45 MB'lık bir LLVM paketini bir kez indirir
(`~/.cache/tracel`); sonraki derlemeler çevrimdışı çalışır. `--backend auto` (varsayılan) GPU varsa
onu, yoksa CPU'yu kullanır.

## Adım adım: Sosy-Pro 1.0

### 1) Veri toplayın

- Kendi projeleriniz ve klonladığınız açık kaynak depolar.
- İndirilmiş veri setleri: `.parquet` veya `.jsonl`. Sütunlar `content` / `text` ve isteğe bağlı
  `path` olmalı; ör. The Stack'in izinli lisanslı alt kümeleri. İndirme bir kez yapılır, sonrası çevrimdışıdır.

`target/`, `node_modules/`, `.git/`, küçültülmüş (minified) veya otomatik üretilmiş dosyalar ve kopyalar
otomatik elenir.

### 2) Tokenizer ve veri

```bash
sosis tokenizer-train -i ~/kod -i ~/indirilenler/stack -o tokenizer.json   # 32k sözlük, RAM sınırlı
sosis prepare -i ~/kod -i ~/indirilenler/stack -t tokenizer.json -o data
```

Tüm modeller aynı `tokenizer.json`'u kullanır; damıtma için bu şarttır.

### 3) Eğitim

```bash
sosis info -c configs/sosy-pro-1.0.toml configs/sosy-pro-1.0-ctx2048.toml
sosis train -c configs/sosy-pro-1.0.toml           # 1. aşama, 1024 bağlam (~4B token)
sosis train -c configs/sosy-pro-1.0-ctx2048.toml   # 2. aşama, 2048 bağlam (~0.5B token)
```

- `Ctrl+C` ile durdurup `--resume` ile kaldığınız yerden sürdürebilirsiniz; bilgisayar sürekli açık kalabilir.
- Log satırlarında kayıp, `token/s`, VRAM ve RAM görünür; ayrıca `runs/<model>/log.csv` dosyasına yazılır.
- **VRAM yetmezse:** `micro_batch`'i yarıya indirip `grad_accum`'u iki katına çıkarın.
- Hız için ilk dakikalarda `nvidia-smi` ile kartın dolu çalıştığını kontrol edin.

Diğer modeller: `configs/sosy-flash-1.0.toml`, `configs/sosy-1.0.toml`.

### 4) Flash'ı damıtma (isteğe bağlı)

```bash
sosis train -c configs/sosy-flash-1.0.toml --teacher runs/sosy-pro-1.0-ctx2048
```

### 5) Sohbet için ince ayar (SFT)

```bash
sosis sft-data -i ~/kod -o sft.jsonl                  # belgelenmiş fonksiyonlardan otomatik örnekler
sosis prepare-sft -i sft.jsonl kendi_sorularim.jsonl -t tokenizer.json -o data-sft
sosis train -c configs/sosy-pro-1.0-chat.toml
```

Kendi `.jsonl` dosyanızda her satır şu biçimlerden biri olabilir:
`{"instruction": "...", "response": "..."}` ya da
`{"messages": [{"role": "user", "content": "..."}, {"role": "assistant", "content": "..."}]}`.
Elle yazılmış birkaç yüz kaliteli örnek, sohbet davranışını belirgin şekilde iyileştirir.

### 6) Değerlendirme

```bash
sosis bench -r runs/sosy-pro-1.0-ctx2048                                    # pass@1
sosis bench -r runs/sosy-pro-1.0-ctx2048 --samples 10 --temperature 0.8     # pass@1/5/10
sosis bench --check-solutions                                               # düzeneğin kendini testi
sosis eval  -r runs/sosy-pro-1.0-ctx2048 -i benim_projem/src                # perplexity
```

> `bench`, üretilen kodu bilgisayarınızda geçici bir klasörde, süre sınırıyla çalıştırır. Görevler saf
> fonksiyonlardır ama bu bir güvenlik kum havuzu değildir.

### 7) Kullanım

```bash
sosis index -i ~/projem -o .sosis-index                                  # proje hafızası
sosis serve -r runs/sosy-pro-1.0-chat --index .sosis-index               # → http://127.0.0.1:8080
sosis chat  -r runs/sosy-pro-1.0-chat --index .sosis-index               # terminalde sohbet
sosis generate -r runs/sosy-pro-1.0-ctx2048 --file main.rs -p "fn parse_config("
sosis fim -r runs/sosy-pro-1.0-ctx2048 --file util.py --prefix "def slug(s):\n" --suffix "\n"
```

- **Örnekleme seçenekleri:** `--temperature` (0 = deterministik), `--top-k`, `--top-p`,
  `--repetition-penalty`, `--max-tokens`, `--stop`, `--seed`.
- **Bağlam:** `--context 4096` ile uzatılır (varsayılan değer config'deki `[inference] context`).
- **Sohbet komutları:** `/yeni`, `/liste`, `/yükle <id>`, `/çık`. Oturumlar `sessions/` klasöründe saklanır.

## CPU'da birkaç dakikalık deneme

```bash
cargo build --release
S=target/release/sosis
$S tokenizer-train -i .. --vocab-size 2048 -o demo/tokenizer.json
$S prepare -i .. -t demo/tokenizer.json -o demo/data --val-percent 5
$S train -c configs/sosy-tiny.toml                    # GQA + bf16 + offload, Pro ile aynı yol
$S sft-data -i .. -o demo/sft.jsonl
$S prepare-sft -i demo/sft.jsonl -t demo/tokenizer.json -o demo/data-sft
$S train -c configs/sosy-tiny-chat.toml
$S index -i .. -o demo/.sosis-index
$S serve -r demo/runs/sosy-tiny-chat --backend cpu --index demo/.sosis-index
```

1M parametrelik bu model yalnızca akışı göstermek içindir: cevap biçimini öğrenir, anlamlı kod yazmaz.

## Testler

```bash
cargo test --release
```

Testlerin kapsadıkları:
- **Tokenizer ve veri:** tokenizer gidiş-dönüş, FIM biçimi, parquet okuma.
- **Model:** GQA'lı KV-cache'in tam ileri geçişle aynı sonucu vermesi, gelecek token sızıntısı
  olmaması, RoPE'nin göreli olması ve ölçeklenmesi.
- **Eğitim yolları:**
  - optimizer offload adımının doğrudan adımla birebir aynı olması;
  - offload + bf16, init_from, damıtma ve SFT ile küçük modellerin ezberleyip devam edebilmesi.
- **Değerlendirme:** 40 referans çözümün gerçekten derlenip geçmesi, pass@k tahmincisi.
- **Hafıza:** BM25 sıralaması, oturum kaydı ve kimlik güvenliği.

## Sınırlar ve notlar

- GPU'da gerçek eğitim süresi ve bellek kullanımı ancak sizin kartınızda ölçülebilir; tahminler kabadır.
- `precision = "bf16"` offload olmadan da kullanılabilir, ancak o durumda ağırlıklar da bf16 tutulur.
  Bu kararsız olabilir; Pro yapılandırması bu yüzden offload kullanır.
- Gradyan kırpma, Burn'de parametre tensörü başına uygulanır (tüm model üzerinden tek norm değil).
