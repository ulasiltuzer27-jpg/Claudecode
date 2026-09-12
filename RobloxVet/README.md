# Veteriner Simülatörü — Roblox

Hayvan gelir → sıraya girer → teşhis edersin → tedavi edersin → taburcu
edersin → para ve XP kazanırsın. Klinik büyür, yeni aletler ve türler açılır.

Luau · Rojo uyumlu · sunucu otoriter · DataStore kayıtlı · **hiçbir dış
asset'e bağımlı değil**

---

## Nasıl oynanır

1. `dist/VeterinerSimulatoru.rbxlx` dosyasına **çift tıkla** — Roblox Studio açılır.
2. **Play**'e bas.
3. Output penceresinde şu satırı gör:
   `[SELF-TEST] n/n geçti — veri tabloları ve oyun mantığı tutarlı.`
   (Bir denetim düşerse hangisi olduğunu tek tek yazar.)

Klinik, eşyalar, hayvanlar ve ışıklandırma Play'e basınca kurulur. Bu yüzden
Studio'nun Explorer'ında Workspace **Play'e basana kadar boş görünür** —
normal, bozuk değil (neden böyle: aşağıda "Neden her şey kodda").

> **İsteğe bağlı, tek tık:** Studio'da `Lighting` → `Technology` → `Future`.
> Yer dosyası bunu zaten `Future` olarak yazıyor, ama bu `.rbxlx` içindeki
> tek sayısal enum değeri ve Studio'suz doğrulanamadı. Işıklandırma düz
> görünüyorsa buradan düzeltilir.

### Kontroller

| Tuş | Eylem |
|-----|-------|
| **E** | Sıradaki hastayı en yakın muayene masasına al (ya da hastanı o masaya taşı) |
| **1 – 6** | Aleti kullan (gözle muayene, steteskop, termometre, otoskop, röntgen, kan testi) |
| Panelden tıkla | Teşhis koy · Tedavi uygula |
| **T** | Hastayı taburcu et (bütün gereken tedaviler bitince) |
| **B** | Mağaza (alet ve klinik yükseltmeleri) |
| **Boşluk** | Ameliyat mini-oyununda kes |

Masaların yanında ayrıca **"Hastayı masaya al"** yazan bir ProximityPrompt çıkar.

### Oynanışın özeti

Hayvanın hastalığı **sunucuda gizlidir**. Aletler semptom açar, semptomlar
hastalığı ele verir:

- **Gözle muayene** → topallama, şişlik, kızarıklık, kilo kaybı, akıntı, halsizlik
- **Steteskop** → üfürüm, hızlı kalp atışı, hırıltı, bağırsak sessizliği
- **Termometre** → ateş, hipotermi
- **Otoskop** → kulak uyuzu/iltihabı, boğaz kızarıklığı
- **Röntgen** → kırık, yabancı cisim, eklem aşınması, akciğer gölgesi
- **Kan testi** → yüksek akyuvar, kansızlık, böbrek değerleri, parazit yumurtası

İki katman var. Birincisi bulmaca: 20 hastalığın hangisi olduğunu semptomlardan
çıkarmak. İkincisi **stres yönetimi**: her alet kullanımı ve her acı veren tedavi
hayvanı gerer; stres 100'e ulaşırsa hayvan **direnir** ve tedavi boşa gider —
arada yıkama/besleme/su ile sakinleştirmek gerekir.

Kırık ve yutulan yabancı cisim **ameliyat** ister; ameliyat yalnızca ameliyat
masasında yapılır, yani hastayı oraya taşımak gerekir.

---

## İçindekiler

| | |
|---|---|
| **6 tür** | Köpek, kedi, tavşan, hamster, papağan, kaplumbağa |
| **20 hastalık** | Köpek öksürüğünden böbrek yetmezliğine |
| **23 semptom** | 6 farklı aletle açılıyor |
| **8 tedavi** | İğne, ilaç, sargı, damla, yıkama, besleme, su, ameliyat |
| **9 yükseltme** | Bekleme odası, reklam, eczane, premium bakım… |
| **10 seviye** | Stajyer → Efsane Veteriner |
| **7 oda** | Resepsiyon, eczane, 2 muayene, ameliyathane, koğuş, yıkama |

Hayvanların animasyonu **prosedürel**: nefes, kuyruk sallama, kafa oynatma,
yürüyüş — ve hasta hayvanda **topallama**. Yani oyuncu daha aleti eline
almadan "bu hayvanın bacağında bir şey var" diyebiliyor.

---

## Neden her şey kodda — Creator Store meselesi

Bu depo, **Roblox'a ağ erişimi olmayan** bir ortamda üretildi. Doğrulandı:
`apis.roblox.com`, `www.roblox.com` ve `assetdelivery.roblox.com` isteklerinin
üçü de ağ politikası tarafından reddediliyor.

Sonucu:

- **Creator Store'dan model indirilemedi.**
- Bir asset ID'nin gerçekten var olduğu bile **doğrulanamazdı**. Ezberden bir ID
  yazmak, oyunda gri/kırık bir model demek olurdu — bu yüzden yazılmadı.

Onun yerine klinik, 26 eşya ve 6 hayvan rigi **kod ile, temel parçalardan**
(Part / WedgePart / silindir / küre + malzeme, ışık, parçacık) kuruluyor.
Hiçbir dış dosyaya bağımlı değil; ilk açılışta eksiksiz görünüyor.

### Kendi modelini takmak istersen

`data/assetIds.json` **bilerek boş** geliyor. Creator Store'da beğendiğin
modelin sayfasını aç, adresteki `.../library/SAYI/...` kısmındaki sayıyı ilgili
yuvaya yaz:

```json
"meshes": { "dog": 123456789, "examTable": 0, ... }
```

Sonra `python3 build.py`. `src/shared/Assets.luau` o yuvayı okuyup prosedürel
modelin yerine mesh'i koyar. Yuva `0` ise (varsayılan) hiçbir şey değişmez;
yazdığın ID yüklenemezse kod sessizce prosedürel modele döner — **oyun hiçbir
durumda bozulmaz**. Aynı şey sesler ve görseller için de geçerli.

### `.rbxlx` neden yalnızca script taşıyor

Yer dosyasının XML'i sadece şunları içeriyor: servisler + `Folder` / `Script` /
`LocalScript` / `ModuleScript`. Görsel ve geometrik hiçbir şey yok.

Sebebi: `.rbxlx` içinde `Material` gibi enum'lar **sayısal token** olarak
yazılmak zorunda (`<token name="Material">272</token>`) ve bu sayılar Studio
olmadan doğrulanamıyor — yanlış token, sessizce yanlış görünüm demek. Aynı şey
Luau'da `Enum.Material.SmoothPlastic` diye yazılıyor: okunur, diff'i anlamlı,
yanlışsa Output'ta hata veriyor.

Yan faydası: **tek `.rbxlx` yolu ile Rojo yolu birebir aynı kodu çalıştırıyor.**
İki ayrı doğruluk kaynağı yok.

---

## Geliştirme

### Rojo ile

```bash
python3 build.py --data-only   # data/*.json -> build/data/*.luau
rojo serve                     # sonra Studio'da Rojo eklentisinden Connect
```

`default.project.json` ağacı `src/` ve `build/data/` klasörlerine bağlıyor.

### Tek dosyayı yeniden üretmek

```bash
python3 build.py               # + dist/VeterinerSimulatoru.rbxlx
```

`dist/` **depoya işleniyor**: oyunu açmak için kimsenin python çalıştırması
gerekmesin diye. `verify_place.py` dosyanın kaynaklarla senkron olduğunu
denetliyor, yani bayat bir artefakt sessizce kalamıyor.

### Dengeyi değiştirmek

Kod değiştirmeye gerek yok — ayarlanabilir **her şey** `data/*.json` içinde:
ücretler, XP eğrisi, itibar, hasta gelme aralığı, stres, tedavi süreleri,
oda planı, eşya yerleşimi, hayvan oranları ve renkleri.

Örneğin oyun şu an **~1,6 saatte** 10. seviyeye çıkıyor (aşağıdaki simülasyona
göre). Daha uzun istersen `data/economy.json` içindeki `levels` eşiklerini
büyütmen yeterli; `python3 tools/simulate_economy.py` yeni eğriyi hemen gösterir.

---

## Doğrulama

Studio'ya erişimimiz olmadığı için "çalışıyor" demek yerine **çalıştırılabilir
denetim** yazıldı. Toplam **1.763 denetim**:

```bash
python3 build.py
python3 tools/verify_place.py       # 183  yer dosyası şeması + tazelik
python3 tools/verify_data.py        # 975  veri tutarlılığı + çeviri anahtarları
python3 tools/verify_luau.py        # 503  Luau statik denetimleri
python3 tools/simulate_economy.py   # 102  ilerleme/ekonomi eğrisi
```

Hepsi hata yoksa `0`, varsa `1` döner (mevcut deponun `Tools/verify_*.py`
sözleşmesiyle aynı). Ayrıca oyun içinde `SelfTest.server.luau` Play'e basınca
veri tablolarına ve saf mantık modüllerine karşı yüzlerce `assert` çalıştırıp
Output'a tek satırlık sonuç basar — Studio'yu açmadan göremediğim tek katman
bu, o yüzden sonucu ilk Play'de kontrol etmeye değer.

**Bu denetimler işe yaradı.** Yazılırken üç gerçek hata yakaladılar:

1. `fleas` 1. seviyede geliyordu ama tedavisi `drops` 2. seviyede açılıyordu —
   yeni oyuncu o hastayı **iyileştiremezdi**.
2. Oyuncu bütün parasını bir alete yatırıp sarf malzemesi alamaz hale
   gelebiliyordu: tedavi edemez → para kazanamaz → **oyun kilitlenir**. Mağazaya
   kasa rezervi kuralı eklendi (`economy.json: minimumReserve`).
3. Hamsterın kırığında ameliyat sarf gideri (155 TL) alınan ücreti (97 TL)
   aşıyordu — o vakayı yapan oyuncu **zarar ediyordu**.

---

## Dosya haritası

```
RobloxVet/
├── dist/VeterinerSimulatoru.rbxlx   ← AÇACAĞIN DOSYA (üretilen, işlenmiş)
├── build.py                          JSON→Luau + tek dosya paketleme
├── default.project.json              Rojo eşlemesi
│
├── data/                             (VERİ) ayarlanabilir her şey
│   ├── animals.json                  tür oranları, renkleri, ücretleri
│   ├── symptoms.json                 semptom → hangi aletle görünür
│   ├── conditions.json               hastalık → semptom kümesi + tedavi zinciri
│   ├── tools.json · treatments.json · upgrades.json
│   ├── economy.json                  XP eğrisi, ücret formülü, itibar, stres
│   ├── clinic.json                   oda planı, duvarlar, kapılar, eşya yerleşimi
│   ├── assetIds.json                 Creator Store yuvaları (boş)
│   └── locale/tr.json                178 satır Türkçe metin
│
├── src/shared/                       Net · Validate · Loc · Assets · Build
├── src/server/
│   ├── init.server.luau              tek giriş noktası, kurulum sırası
│   ├── SelfTest.server.luau          açılış denetimleri → Output
│   ├── world/                        Clinic · Props · Lighting · AnimalFactory · AnimalAnimator
│   └── game/                         PatientFlow · Diagnosis · Treatment · Surgery
│                                     Economy · Upgrades · Profile
├── src/client/                       Hud · Chart · DiagnosisPanel · TreatmentPanel
│                                     SurgeryPanel · ShopPanel · ToolBar · Notify · Theme
└── tools/                            rbxlx yazıcısı + 4 doğrulayıcı
```

### Mimarinin üç kuralı

1. **Sunucu otoriter.** İstemci yalnızca niyet bildirir ("şu aleti kullan");
   para, seviye, teşhis ve tedavi sonucunu sunucu hesaplar. Hastalığın kimliği
   istemciye **ancak doğru teşhisten sonra** gider.
2. **Tek tanım noktası.** Bütün remote'lar `src/shared/Net.luau` içindeki tek
   tabloda; asset ID'leri yalnızca `Assets.luau`'dan okunuyor.
   `verify_luau.py` iki tarafı da bu tablolara karşı denetliyor.
3. **Eksik çeviri gizlenmez.** Tabloda olmayan bir anahtar ekranda
   `[anahtar.adi]` diye **görünür** — sessizce boş bırakılmaz.

---

## Bilinen boşluklar

Studio'ya erişimim olmadığı için şunları **ben doğrulayamadım**; ilk Play'de
bakılacak liste:

- **Görsel ölçüler.** Oda boyları, eşya yerleşimi ve hayvan boyutları ölçülerek
  hesaplandı ama gözle görülmedi. Bir şey büyük/küçük durursa
  `data/clinic.json` ve `data/animals.json` içinden ayarlanır.
- **Animasyonların akıcılığı.** Prosedürel animasyon matematiği doğru ama
  hızları/genlikleri elle ayar isteyebilir (`AnimalAnimator.luau`).
- **Arayüz yerleşimi** masaüstü çözünürlüğüne göre kuruldu; telefon ekranında
  sağdaki paneller dar kalabilir.
- **DataStore** yayında denenmedi. Studio'da "Studio Access to API Services"
  kapalıysa oyun bellekte çalışır ve HUD'da Türkçe uyarı gösterir — sessizce
  ilerleme kaybetmez.
- **Ameliyat zamanlaması** senkron sunucu saati (`GetServerTimeNow`) üzerinden
  yürüyor; yüksek gecikmede pencereler (0,18 sn / 0,34 sn) dar gelebilir.
  `data/treatments.json` → `surgery` bölümünden genişletilir.
- **Ses yok.** `assetIds.json`'daki ses yuvaları boş; doldurulunca çalışır.
