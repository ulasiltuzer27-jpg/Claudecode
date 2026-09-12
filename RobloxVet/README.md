# Veteriner Simülatörü — Roblox

Hayvan sahibiyle gelir → sıraya girer → teşhis edersin → tedavi edersin →
taburcu edersin → para, bahşiş ve XP kazanırsın. Klinik büyür; yeni aletler,
türler ve odalar açılır.

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
| **1 – 8** | Aleti kullan |
| Panelden tıkla | Teşhis koy · Tedavi uygula |
| **T** | Hastayı taburcu et (gereken bütün tedaviler bitince) |
| **B** | Mağaza — aletler ve klinik yükseltmeleri |
| **G** | İlerleme — günlük görevler · başarımlar · istatistikler |
| **O** | Ayarlar — kamera, sallanma, FOV, koşma, kare sayacı |
| **V** | Birinci ↔ üçüncü şahıs kamera |
| **Shift** (basılı) | Koş |
| **Boşluk** | Ameliyat mini-oyununda kes |

Masaların yanında ayrıca **"Hastayı masaya al"** yazan bir ProximityPrompt çıkar.
**Tab** tuşundaki oyuncu listesinde Para / Seviye / Hasta sütunları var.

### Oynanışın özeti

Hayvanın hastalığı **sunucuda gizlidir**. Üç ipucu kaynağın var:

1. **Sahibinin şikâyeti** — hayvanla birlikte gelen NPC'nin başındaki
   balonda ve hasta kartında. Teşhisi ele vermez, yönlendirir:
   *"Merdivenden düştü, ayağını hiç yere koyamıyor."*
2. **Gözle görünen belirtiler** — hasta hayvan **topallar**, stresliyse
   kulakları yatar ve kuyruğu düşer.
3. **Aletler** — her biri farklı bir semptom kümesini açar:

| Alet | Açtığı belirtiler |
|---|---|
| Gözle Muayene | topallama, şişlik, kızarıklık, kilo kaybı, akıntı, halsizlik, diken dökülmesi, sıyrılmamış deri, donuk tüy |
| Steteskop | üfürüm, hızlı/düzensiz kalp atışı, hırıltı, bağırsak sessizliği |
| Termometre | ateş, hipotermi |
| Otoskop | kulak uyuzu/iltihabı, boğaz kızarıklığı |
| Röntgen | kırık, yabancı cisim, eklem aşınması, akciğer gölgesi |
| Kan Testi | yüksek akyuvar, kansızlık, böbrek değerleri, parazit yumurtası |
| İdrar Testi | idrarda kan, yüksek şeker, idrar yolu enfeksiyonu |
| Ultrason | gebelik, organ büyümesi, mesane taşı |

İkinci katman **stres yönetimi**: her alet kullanımı ve her acı veren tedavi
hayvanı gerer; stres 100'e ulaşırsa hayvan **direnir** ve tedavi boşa gider
(sarf malzemesi yine gider). Arada yıkama/besleme/su ile sakinleştirmek
gerekir.

Kırık, yutulan yabancı cisim ve mesane taşı **ameliyat** ister; ameliyat
yalnızca ameliyat masasında yapılır, yani hastayı oraya taşımak gerekir.

---

## İçindekiler

| | |
|---|---|
| **10 tür** | Köpek, kedi, tavşan, hamster, papağan, kaplumbağa, kirpi, yılan, iguana, at |
| **31 hastalık** | Köpek öksürüğünden kolik ve metabolik kemik hastalığına |
| **33 belirti** | 8 farklı aletle açılıyor |
| **8 tedavi** | İğne, ilaç, sargı, damla, yıkama, besleme, su, ameliyat |
| **9 yükseltme** | Bekleme odası, reklam, eczane, premium bakım… |
| **18 başarım** | İlk hastadan "Başhekim"e |
| **10 günlük görev** | Her gün havuzdan 3 tanesi seçilir |
| **15 seviye** | Stajyer → Efsane Veteriner |
| **7 oda** | Resepsiyon, eczane, 2 muayene, ameliyathane, koğuş, yıkama |

Her tür kendi iskeletiyle kuruluyor: yılanın **eklemli gövdesi** (9 halka,
kuyruğa doğru incelen, sürünme dalgasıyla kıvrılan), kirpinin **dikenleri**,
iguananın **sırt yelesi**, atın **yelesi**. Animasyonlar prosedürel —
nefes, kuyruk, kafa, yürüyüş, kanat çırpma ve hastada topallama.

---

## Klinikte kimler var

### Hayvan sahipleri

Her hasta bir NPC ile geliyor. Sahip bekleme odasında duruyor, hayvanına
bakıyor, sıkılınca kollarını kavuşturuyor.

- **Şikâyeti ipucudur.** 31 hastalığın her biri için ayrı bir replik var.
- **Memnuniyeti bahşiştir.** Beklerken düşer, doğru teşhiste ve sağlıklı
  taburcuda yükselir. Eşiğin üstündeyse ücretin üstüne bahşiş bırakır.
- Yanlış teşhis memnuniyeti sertçe düşürür — ve sahip bunu söyler.

### Acil vakalar

3. seviyeden sonra hastaların bir kısmı **ambulansla** geliyor: kırmızı
dönen ışık, daha düşük başlangıç sağlığı ve **geri sayım**. Ekranın
ortasında kalan süre yazıyor.

- Süresinde bitirirsen ücret ve XP belirgin şekilde yüksek.
- Süre dolarsa hasta **kaybedilir** — oyunun kaybedilebilir tek durumu, o
  yüzden itibar cezası da ağır.

### Günlük görevler, başarımlar, istatistik

**G** tuşu üç sekme açıyor:

- **Günlük görevler** — havuzdan 3 tanesi. Hangi görevlerin geleceği günün
  numarası ve oyuncunun kimliğinden türeyen **sabit** bir tohumla
  belirleniyor: aynı gün hangi sunucuya girersen gir aynı görevleri
  görürsün, sunucu değiştirerek görev "çevirmek" mümkün değil.
- **Başarımlar** — 18 tane, ilerleme çubuklarıyla. Ödülleri sunucu veriyor.
- **İstatistikler** — isabet oranı, en uzun doğru teşhis serisi, toplam
  bahşiş, ameliyat ve kusursuz ameliyat sayısı, kurtarılan acil vaka…

Hepsi profile kaydediliyor.

---

## Kamera ve konfor

**O** tuşundaki ayarlar paneli (ayarlar profile kaydediliyor, başka bir
oturumda da aynı gelir):

| Ayar | |
|---|---|
| Birinci şahıs kamera | **V** ile de anında değişir |
| Kamera sallanması | 0 = tamamen kapalı, 2.0 = en yüksek |
| Görüş açısı (FOV) | 55 – 100 |
| Koşma (Shift) | hız + görüş açısının açılması |
| Kare sayacı | FPS, **en düşük kare** ve gecikme (ms) |

### Sallanma neden böyle yazıldı

Yürüyen bir insanın gövdesi **her adımda bir kez** inip kalkar, ama ağırlığı
bir sağa bir sola geçer. Yani dikey bileşen adım frekansının **iki katı**,
yatay bileşen adım frekansındadır. Bu 2:1 oranı bozulduğunda yürüyüş
"kayıyor" gibi hissettirir — camera bobbing'i kötü yapan şey neredeyse her
zaman budur. Sallanma ayrıca **hıza bağlı**: dururken tamamen kesiliyor,
çünkü dururken devam eden bir sallanma oyuncuyu birkaç dakikada rahatsız
eder. Yere inişte kısa bir çökme var.

Kamera Roblox'un kendi kamera betiğini **devralmıyor**; o yazdıktan sonra
(`RenderPriority.Camera + 1`) sonucun üzerine bir ofset çarpıyor. Devralmak,
zoom ve çarpışma davranışını da yeniden yazmak demek olurdu.

Kare sayacı ortalama FPS'in yanında **son saniyenin en uzun karesini** de
gösteriyor: ortalama takılmaları gizler, 60 ortalamalı bir oyun arada 12'ye
düşebilir.

---

## Neden her şey kodda — Creator Store meselesi

Bu depo, **Roblox'a ağ erişimi olmayan** bir ortamda üretildi. Doğrulandı:
`apis.roblox.com`, `www.roblox.com` ve `assetdelivery.roblox.com` isteklerinin
üçü de ağ politikası tarafından reddediliyor.

Sonucu:

- **Creator Store'dan model indirilemedi.**
- Bir asset ID'sinin gerçekten var olduğu bile **doğrulanamazdı**. Ezberden
  bir ID yazmak, oyunda gri/kırık bir model demek olurdu — bu yüzden yazılmadı.

Onun yerine klinik, 26 eşya, 10 hayvan rigi ve sahip NPC'leri **kod ile,
temel parçalardan** (Part / WedgePart / silindir / küre + malzeme, ışık,
parçacık) kuruluyor. Hiçbir dış dosyaya bağımlı değil; ilk açılışta
eksiksiz görünüyor.

### Kendi modelini takmak istersen

`data/assetIds.json` **bilerek boş** geliyor. Creator Store'da beğendiğin
modelin sayfasını aç, adresteki `.../library/SAYI/...` kısmındaki sayıyı
ilgili yuvaya yaz:

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
ücretler, XP eğrisi, itibar, bahşiş, acil vaka sıklığı ve süresi, hasta gelme
aralığı, stres, tedavi süreleri, oda planı, eşya yerleşimi, hayvan oranları
ve renkleri, kamera sallanmasının frekans ve genlikleri, başarım ve görev
hedefleri.

---

## Doğrulama

Studio'ya erişimimiz olmadığı için "çalışıyor" demek yerine **çalıştırılabilir
denetim** yazıldı. Toplam **2.786 denetim**:

```bash
python3 build.py
python3 tools/verify_place.py       #   227  yer dosyası şeması + tazelik
python3 tools/verify_data.py        # 1.710  veri tutarlılığı + çeviri anahtarları
python3 tools/verify_luau.py        #   671  Luau statik denetimleri
python3 tools/simulate_economy.py   #   178  ilerleme/ekonomi eğrisi
```

Hepsi hata yoksa `0`, varsa `1` döner (mevcut deponun `Tools/verify_*.py`
sözleşmesiyle aynı). Ayrıca oyun içinde `SelfTest.server.luau` Play'e basınca
veri tablolarına ve saf mantık modüllerine karşı yüzlerce `assert` çalıştırıp
Output'a tek satırlık sonuç basar — Studio'yu açmadan göremediğim tek katman
bu, o yüzden sonucu ilk Play'de kontrol etmeye değer.

Denetimler birbirini de kontrol ediyor: `verify_data`, veri dosyalarındaki
sayaç/etki adlarını **kodun gerçekten okuduğu** adlarla karşılaştırıyor;
`verify_luau`'nun sıra denetimi ise bilerek bozulmuş bir örnek üzerinde
kendini doğruluyor.

**Bu denetimler işe yaradı.** Yazılırken beş gerçek hata yakaladılar:

1. `fleas` 1. seviyede geliyordu ama tedavisi `drops` 2. seviyede açılıyordu —
   yeni oyuncu o hastayı **iyileştiremezdi**.
2. Oyuncu bütün parasını bir alete yatırıp sarf malzemesi alamaz hale
   gelebiliyordu: tedavi edemez → para kazanamaz → **oyun kilitlenir**. Mağazaya
   kasa rezervi kuralı eklendi (`economy.json: minimumReserve`).
3. Hamsterın kırığında ameliyat sarf gideri (155 TL) alınan ücreti (97 TL)
   aşıyordu — o vakayı yapan oyuncu **zarar ediyordu**.
4. `PatientFlow.pushState`, kendisinden **sonra** tanımlanan `pushProgress`
   fonksiyonunu çağırıyordu. Lua'da `local function` yalnızca kendinden
   sonraki koda görünür; öncesinde ad `nil` kalır. Bu, **ilk oyuncu
   girdiğinde sunucu hatası** demekti. Düzeltildi ve bu hata sınıfı
   `verify_luau.py`'ye kalıcı bir denetim olarak eklendi.
5. Sahibin şikâyeti hasta kartında **ancak teşhisten sonra** görünüyordu —
   oysa bütün amacı teşhisten önce ipucu vermek.

---

## Dosya haritası

```
RobloxVet/
├── dist/VeterinerSimulatoru.rbxlx   ← AÇACAĞIN DOSYA (üretilen, işlenmiş)
├── build.py                          JSON→Luau + tek dosya paketleme
├── default.project.json              Rojo eşlemesi
│
├── data/                             (VERİ) ayarlanabilir her şey
│   ├── animals.json                  10 türün oranları, renkleri, ücretleri
│   ├── symptoms.json                 belirti → hangi aletle görünür
│   ├── conditions.json               hastalık → belirti kümesi + tedavi zinciri + sahip repliği
│   ├── tools.json · treatments.json · upgrades.json
│   ├── achievements.json             18 başarım, sayaç adları
│   ├── tasks.json                    10 günlük görev havuzu
│   ├── settings.json                 kamera sallanması, FOV, koşma; sınırlar ve varsayılanlar
│   ├── economy.json                  XP eğrisi, ücret, itibar, bahşiş, acil vaka, stres
│   ├── clinic.json                   oda planı, duvarlar, kapılar, eşya yerleşimi
│   ├── assetIds.json                 Creator Store yuvaları (boş)
│   └── locale/tr.json                341 satır Türkçe metin
│
├── src/shared/                       Net · Validate · Loc · Assets · Build
├── src/server/
│   ├── init.server.luau              tek giriş noktası, kurulum sırası
│   ├── SelfTest.server.luau          açılış denetimleri → Output
│   ├── world/                        Clinic · Props · Lighting · AnimalFactory
│   │                                 AnimalAnimator · OwnerFactory
│   └── game/                         PatientFlow · Diagnosis · Treatment · Surgery
│                                     Economy · Upgrades · Profile
│                                     Achievements · Tasks · Leaderboard
├── src/client/                       Hud · Chart · DiagnosisPanel · TreatmentPanel
│                                     SurgeryPanel · ShopPanel · ProgressPanel
│                                     SettingsPanel · Camera · Fps · ToolBar
│                                     Notify · Theme
└── tools/                            rbxlx yazıcısı + 4 doğrulayıcı
```

### Mimarinin üç kuralı

1. **Sunucu otoriter.** İstemci yalnızca niyet bildirir ("şu aleti kullan");
   para, seviye, teşhis, tedavi, başarım ve görev sonucunu sunucu hesaplar.
   Hastalığın kimliği istemciye **ancak doğru teşhisten sonra** gider.
   Tek istisna kamera: sunucunun oyuncunun ne gördüğü hakkında söyleyecek bir
   şeyi yok — ama ayarlar yine sunucuda saklanıyor.
2. **Tek tanım noktası.** Bütün remote'lar `src/shared/Net.luau` içindeki tek
   tabloda; asset ID'leri yalnızca `Assets.luau`'dan; leaderstats sözleşmesi
   yalnızca `Leaderboard.luau`'dan okunuyor. `verify_luau.py` iki tarafı da
   bu tablolara karşı denetliyor.
3. **Eksik çeviri gizlenmez.** Tabloda olmayan bir anahtar ekranda
   `[anahtar.adi]` diye **görünür** — sessizce boş bırakılmaz.

---

## Bilinen boşluklar

Studio'ya erişimim olmadığı için şunları **ben doğrulayamadım**; ilk Play'de
bakılacak liste:

- **Görsel ölçüler.** Oda boyları, eşya yerleşimi ve hayvan boyutları
  ölçülerek hesaplandı ama gözle görülmedi. Özellikle at (en büyük tür) ve
  yılan (eklemli gövde) bakılmaya değer. Bir şey büyük/küçük durursa
  `data/clinic.json` ve `data/animals.json` içinden ayarlanır.
- **Kamera sallanmasının şiddeti** kişiye göre değişir; varsayılan orta
  seviyede bırakıldı ve panelden 0'a kadar indirilebiliyor.
- **Hayvanlar ve sahip NPC'leri çarpışmasız yürüyor.** Yolları düz hatlar
  olduğu için duvarlardan geçmiyorlar, ama kalabalıkta birbirlerinin
  içinden geçebilirler.
- **Arayüz yerleşimi** masaüstü çözünürlüğüne göre kuruldu; telefon
  ekranında sağdaki paneller dar kalabilir.
- **DataStore** yayında denenmedi. Studio'da "Studio Access to API Services"
  kapalıysa oyun bellekte çalışır ve HUD'da Türkçe uyarı gösterir — sessizce
  ilerleme kaybetmez.
- **Ameliyat zamanlaması** senkron sunucu saati (`GetServerTimeNow`) üzerinden
  yürüyor; yüksek gecikmede pencereler (0,18 sn / 0,34 sn) dar gelebilir.
  `data/treatments.json` → `surgery` bölümünden genişletilir.
- **Süre.** Simülasyona göre 15. seviye ~412 hastada, yaklaşık **2,8 saatte**
  geliyor. Daha uzun bir oyun istersen `data/economy.json` içindeki `levels`
  eşiklerini büyüt; `python3 tools/simulate_economy.py` yeni eğriyi anında
  gösterir.
- **Ses yok.** `assetIds.json`'daki ses yuvaları boş; doldurulunca çalışır.
