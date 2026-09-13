# Veteriner Simülatörü — Roblox

Hayvan sahibiyle gelir → sıraya girer → teşhis edersin → tedavi edersin →
taburcu edersin → para, bahşiş ve XP kazanırsın. Klinik büyür; yeni aletler,
türler ve odalar açılır.

**Yalnızca birinci şahıs** — ellerini ve elindeki aleti görüyorsun.
Gündüz/gece döngüsü, gece nöbeti zammı, alet mini-oyunları, kendi
dekorasyonunu seçtiğin bir klinik.

Luau · Rojo uyumlu · sunucu otoriter · DataStore kayıtlı · **hiçbir dış
asset'e bağımlı değil** (tek bir doku, mesh ya da animasyon ID'si yok)

---

## Nasıl oynanır

1. `dist/VeterinerSimulatoru.rbxlx` dosyasına **çift tıkla** — Roblox Studio açılır.
2. **Play**'e bas.
3. **Lobide** başlarsın: dört portal — Tek / İki / Üç / Dört kişilik.
   Birini seç, sıraya gir; kadro dolunca kliniğe birlikte gidilir.
4. Output penceresinde şu satırı gör:
   `[SELF-TEST] n/n geçti — veri tabloları ve oyun mantığı tutarlı.`
   (Bir denetim düşerse hangisi olduğunu tek tek yazar.)

> **Studio'da ışınlanma çalışmaz** — Roblox Studio `TeleportService`'i
> desteklemez ve yayınlanmamış bir yerin `PlaceId`'si 0'dır. Bu yüzden mod
> seçince klinik **aynı sunucuda** açılır ve Output'a
> `Işınlanma yapılamadı — klinik AYNI sunucuda açılıyor` yazar. Oyunu
> yayınladığında gerçek ışınlanma devreye girer; bunun için ekstra bir ayar
> ya da ikinci bir place gerekmez.

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
| **Shift** (basılı) | Koş |
| **Boşluk** | Ameliyat mini-oyununda kes · kan testinde ibreyi durdur |
| Sahnede tıkla | Röntgende kırığı işaretle · ultrasonda probu onayla |

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

Üç alet (röntgen, kan testi, ultrason) doğrudan çalışmaz, **kendi
mini-oyununu** açar: görüntüde kırığı bulmak, süzülen ibreyi doğru aralıkta
durdurmak, probu sıcak/soğuk geri bildirimiyle hedefe götürmek. Başaramazsan
belirti açılmaz — aleti tekrar deneyebilirsin ama her deneme hayvanı yeniden
gerer.

Kırık, yutulan yabancı cisim ve mesane taşı **ameliyat** ister; ameliyat
yalnızca ameliyat masasında yapılır, yani hastayı oraya taşımak gerekir.

Kuyrukta arada **rutin işlemler** de var (aşı, kontrol, tımar, parazit
ilacı): belirti ve teşhis yok, tek adımlık iş, hızlı para. Kuyruğun tamamı
bulmaca olunca oyun tek ritimli kalıyordu.

---

## Lobi ve eşleştirme

Oyun **tek bir yer dosyası**. Ayrım `game.PrivateServerId` ile yapılıyor:

```
PrivateServerId == ""   →  LOBİ    (herkese açık sunucu)
PrivateServerId ~= ""   →  KLİNİK  (ayrılmış sunucu)
```

Bu yüzden iki ayrı place yayınlamak ve PlaceId yapıştırmak gerekmiyor —
oyun yayınlandığı anda lobiden kliniğe ışınlanma çalışır.

- **Tek kişilik** anında gider.
- **İki/Üç/Dört kişilik** kadro dolana kadar bekler; dolunca seçilen
  oyuncular `TeleportService:ReserveServer` ile açılan **yeni bir sunucuya
  birlikte** ışınlanır.
- Kimse gelmezse **30 saniye** sonra eldeki oyuncularla başlar
  (`data/lobby.json: soloFallbackSeconds`) — oyun yeniyken kimse sırada
  kilitli kalmaz.
- Portalların üstünde sırada kaç kişi olduğu **canlı** yazar.

## Ses ve müzik

Ses sistemi tamamen kurulu: arayüz tıkları, alet başına ayrı ses, tedavi
sesleri, ameliyat adım notları, taburcu, acil vaka sireni, klinik ortam
uğultusu, tür başına hayvan sesi ve **çapraz geçişli müzik** (lobi · klinik ·
acil vaka). Ayarlardan ana ses / müzik / efekt seviyeleri ayrı ayrı
ayarlanır ve profile kaydedilir.

**Efektler ilk açılışta duyulur.** Kullanılan sesler Roblox istemcisiyle
birlikte gelen `rbxasset://` dosyaları — ID gerektirmezler, indirilmezler.

**Müzik için ID gerekiyor.** Roblox hazır müzik parçası ile gelmiyor;
`data/audio.json` içindeki `music` yuvaları boş. Creator Store'dan bir müzik
ID'si bulup yapıştır, `python3 build.py` çalıştır — çalar:

```json
"music": { "lobby": { "assetId": 123456789, "volume": 0.5 }, ... }
```

**Kendi kendini teşhis eder.** Bu depoyu üreten ortamda Roblox'a erişim
yoktu, yani `rbxasset://` yollarının hepsinin geçerli olduğu
doğrulanamadı. `Audio.preload()` açılışta hepsini yükleyip **tutmayanları
adıyla Output'a yazar**:

```
[SES] 49 ses hazır.
[SES] 2/49 ses yüklenemedi - data/audio.json'dan değiştirilebilir:
  toolXray, treatWash
```

Sessiz bir gizem değil, tek satırlık bir düzeltme.

## İçindekiler

| | |
|---|---|
| **10 tür** | Köpek, kedi, tavşan, hamster, papağan, kaplumbağa, kirpi, yılan, iguana, at |
| **31 hastalık** | Köpek öksürüğünden kolik ve metabolik kemik hastalığına |
| **4 rutin işlem** | Aşı, rutin kontrol, tımar, parazit ilacı — teşhissiz, hızlı |
| **33 belirti** | 8 farklı aletle açılıyor |
| **8 tedavi** | İğne, ilaç, sargı, damla, yıkama, besleme, su, ameliyat |
| **9 yükseltme** | Bekleme odası, reklam, eczane, premium bakım… |
| **18 başarım** | İlk hastadan "Başhekim"e |
| **10 günlük görev** | Her gün havuzdan 3 tanesi seçilir |
| **15 seviye** | Stajyer → Efsane Veteriner |
| **7 oda** | Resepsiyon, eczane, 2 muayene, ameliyathane, koğuş, yıkama |
| **3 alet mini-oyunu** | Röntgen (kırığı bul), kan testi (ibreyi durdur), ultrason (sıcak/soğuk) |
| **4 tema + 4 süs** | Mağazanın dekorasyon sekmesi — klinik oyuncunun eline geçiyor |
| **Gündüz/gece** | 12 dakikalık gün; gece az hasta, zamlı ücret, çok acil |

### Görünüm

Klinik ve 26 eşya, **eşya başına en az 8 parçadan** kuruluyor: pahlı
kenarlar, iki tonlu paneller, kulplar, konik ayaklar, sarkan kablolar,
tepside sıralı aletler, rafta şişeler, kafeste mandal ve etiket. Ortak
detaylar `src/shared/Detail.luau` içinde tek yerde — 26 eşya aynı kalite
dilinden konuşuyor.

Bina artık kutu değil: **dış duvarlarda 11 pencere** (cam + çerçeve +
denizlik), odalarda **dama desenli zemin**, duvarlarda süpürgelik ve
tavan kornişi, kapılarda söve.

Her tür kendi iskeletiyle kuruluyor: yılanın **eklemli gövdesi** (9 halka,
kuyruğa doğru incelen, sürünme dalgasıyla kıvrılan), kirpinin **dikenleri**,
iguananın **sırt yelesi**, atın **yelesi**. Animasyonlar prosedürel —
nefes, kuyruk, kafa, yürüyüş, kanat çırpma ve hastada topallama. Yüzlerinde
göz bebeği ve parıltı, kulak içi, burun üstü, pati altı yastıkları ve
bıyıklar var; gövdelerinde **desen** (benek, şerit, çorap, alın akıtması,
maske, kabuk plakası, yılan halkası) — hepsi `data/animals.json`'dan.

Yılan masaya **kıvrılarak** konuyor: 7 stud'luk gövdesi 3 stud'luk bir
daireye toplanıyor. Bu yalnızca görsel değil, veri de bunu biliyor
(`tableFootprint`) — ve **büyük bir hayvan küçük bir masaya konamıyor**.

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

## HUD ve arayüz

Arayüzün tamamı `src/client/Theme.luau` içindeki tek bir tasarım sisteminden
çıkıyor: boşluk (4/8/12/16/24), köşe yarıçapı (6/10/14) ve yazı boyu
(11/13/15/18/22/34) ölçekleri, koyu cam yüzey, tek noktadan animasyon.
Paneller ayrı ayrı kurulsaydı her birinde farklı bir gri ve farklı bir köşe
yarıçapı olurdu.

**Koyu cam** dört parçadan oluşuyor ve tek başına hiçbiri yetmiyor: `UIGradient`
ile geçişli zemin, `UIStroke` ile 1 piksel kenar, üstte ince ışık çizgisi,
altta koyu rim. Dördü birlikte yükseklik hissi veriyor.

**İkonların hiçbiri doku değil.** Görsel asset yüklenemediği için her ikon
`Frame`'lerden kuruluyor (dikdörtgen + daire + döndürme). Alet ve tedavi
ikonları veri kimlikleriyle **aynı adı** taşıyor, yani `data/tools.json`'a
yeni bir alet eklenince ikonu da adıyla bulunuyor.

| Panel | |
|---|---|
| HUD | Para/seviye/itibar ayrı kartlar; sayılar **sayarak** artıyor, XP çubuğu 10 bölmeli |
| Hedef bandı | "Şimdi ne yapmalıyım?" sorusunun cevabı — sunucudan gelen durumun tek cümleye çevrilmesi |
| Kazanç yazıları | +120 TL ekranda yükselip soluyor |
| Stres vinyeti | Hayvanın stresi %50'yi geçince ekran kenarında turuncu ışık |
| Hasta kartı | Sahibin şikâyeti alıntı balonunda; vitaller bölmeli ve eşiğe göre renk değiştiriyor |
| Teşhis | Adaylar **bulunan belirtilere göre eşleşme yüzdesiyle** sıralı |
| Tedavi | İkon ızgarası, fiyat rozeti, uygulanınca kare doluyor |
| Alet çubuğu | Tuş kapağı görünümü, seçili alet yükseliyor, bekleme süresi süpürmesi |
| Ameliyat | Tik işaretli ray, nabız atan mükemmel bölge, adım noktaları |
| Mağaza | Kategori sekmeleri: aletler · yükseltmeler · **dekorasyon** |
| Lobi | Kapasite noktaları ve geri sayım halkası |
| Bildirim | Soldan kayarak giriyor, ikonlu, renk şeritli |

**Eşleşme yüzdesi sunucuda hesaplanıyor** (`PatientFlow.chartSnapshot`).
İstemciye hesaplatmak, hastalık tablosunun tamamını istemciye yollamak
demekti; sunucuda hesaplanan oran ise zaten oyuncunun elindeki bilgiden
(bulunmuş belirtiler) çıkıyor ve doğru cevabı söylemiyor — iki hastalık aynı
yüzdeyi rahatlıkla paylaşabiliyor.

Geri sayım **halkaları** da dokusuz: bir çember üzerine dizilmiş noktaların
kaçının yandığı oranı gösteriyor.

---

## Alet mini-oyunları

Üç alet artık doğrudan çalışmıyor, bir mini-oyun açıyor. Hepsi
`src/server/game/ToolGame.luau` içinde, **`Surgery.luau` ile aynı kalıpta**:
hedef sunucuda üretiliyor, sonuç sunucuda doğrulanıyor, süre sunucuda
doluyor — istemcinin "bitti" demesi beklenmiyor.

| Alet | Mini-oyun | Ne gizli, ne değil |
|---|---|---|
| **Röntgen** | Görüntüdeki çatlağı bul ve tıkla | Kırığın yeri istemciye gidiyor çünkü **çizilmesi** gerekiyor; tıklamanın menzili ve süresi sunucuda ölçülüyor. Sahte işaretler de sunucuda üretiliyor. |
| **Kan testi** | Süzülen ibreyi yeşil aralıkta durdur | İbrenin yeri **zamandan** çıkıyor (senkron sunucu saati). İstemcinin bildirdiği konum kullanılmıyor: sunucu tuşa basıldığı anı kendi saatinde okuyor. |
| **Ultrason** | Probu gezdir, kızardıkça yaklaş | Hedef istemciye **hiç gitmiyor**; yalnızca "ne kadar yakınsın" sayısı dönüyor. |

Hiçbir mini-oyunda **hastalığın kimliği sızmıyor** — oyunun sırrı bu ve
korunuyor. Başarısızlık aleti kilitlemiyor, tekrar denenebiliyor; ama her
deneme hayvanı yeniden geriyor, yani bedava değil.

---

## Rutin işlemler

31 hastalığın hepsi bulmaca olunca oyun tek ritimli kalıyordu. Kuyruğa
`kind: "routine"` olan dört vaka karıştı: **aşı, rutin kontrol, tımar,
parazit ilacı**. Belirti yok, teşhis yok — hasta zaten teşhisli geliyor,
tek adımlık işlemi uygulayıp taburcu ediyorsun. Hızlı para, az XP.

Bunlar teşhis panelinde **görünmüyor** (`Diagnosis.conditionsFor` onları
atıyor): listede "Aşı" seçeneği görmek bulmacayı anlamsızlaştırırdı.
Oranı `data/economy.json` → `routineChance` (varsayılan %22). Acil vaka
önce değerlendiriliyor: bir vaka hem acil hem rutin olamaz.

---

## Kliniği kendin döşüyorsun

Mağazanın **Dekorasyon** sekmesinde 4 duvar/zemin teması (Klasik Beyaz,
Sıcak Ahşap, Orman Yeşili, Gece Mavisi) ve 4 süs eşyası (akvaryum, duvar
saati, diploma çerçevesi, çiçek saksısı) var.

Tema satın alınca **aktif oluyor**; ayrıca bir seçim ekranı yok. Klinik
ortak olduğu için çok oyunculuda **online oyuncuların en yüksek sıralı
teması** uygulanıyor — yükseltmelerdeki "klinik geneli" kuralının aynısı.

Süs eşyaları **kuruluşta görünmez olarak inşa ediliyor** ve satın alınınca
yalnızca görünürlüğü açılıyor. Çalışma anında yeni parça yaratmak, yerleşim
denetiminden (`verify_layout.py`) kaçan bir model demek olurdu: o eşya hiçbir
çakışma kontrolünden geçmezdi. Bedeli birkaç yüz görünmez parça; kazancı,
dekorun da bütün denetimlerden geçmesi.

---

## Gündüz, gece ve nöbet

Bir gün **12 dakika** sürüyor. `Lighting.ClockTime` **sunucuda** yazılıyor ve
Roblox onu istemcilere kendiliğinden replike ediyor. İstemcinin kendi saatini
işletmesi, bir oyuncuda gece diğerinde gündüz olması demek olurdu — ve ücret
çarpanı geceye bağlı olduğu için bu yalnızca görsel değil, **oynanış** hatası
olurdu.

Gece "boş zaman" değil, **başka bir oyun**:

| | gündüz → gece |
|---|---|
| Hasta aralığı | ×1.0 → **×1.55** (daha seyrek) |
| Ücret | ×1.0 → **×1.35** (nöbet zammı) |
| XP | ×1.0 → **×1.2** |
| Acil vaka oranı | +0 → **+0.12** |

Sokak lambaları geceyle birlikte yanıyor (fenerin camı da gündüz sönüyor),
yıldızlar çıkıyor, sis ve ortam ışığı kararıyor. Geçiş `transitionHours`
boyunca yumuşak: bir karede gündüzden geceye atlamak göze çarpıyordu.
Bütün sayılar `data/daycycle.json` içinde. HUD'da saat ve vardiya rozeti
var; gece nöbetinde rozet ücret çarpanını yazıyor.

---

## Canlı klinik

- **Hasta tepkileri**: iğnede irkiliyor, yıkamada gevşiyor, doğru teşhiste
  kuyruk sallıyor, ameliyatta derin nefes alıyor, damlada kafa sallıyor,
  alet değince başını kaldırıyor. Tepki katmanı mevcut sinüs katmanını
  durdurmuyor, **üstüne biniyor** — hayvan hem nefes almaya devam ediyor
  hem irkiliyor.
- **Sahip jestleri**: yerine varınca hayvanını gösteriyor, doğru teşhiste
  rahatlıyor, acil vakada endişeleniyor. Kalabalık bir bekleme odasında
  hangi sahibin hangi hayvana ait olduğu bu jestle okunuyor.
- **Tedavi efektleri**: damla, buhar, ışıltı, halka.
  `ParticleEmitter` **kullanılmıyor** — dokusu bir asset ve bu ortamda
  doğrulanamaz; yüklenmeyen bir doku emitter'ı sessizce boş bırakır.
  Tweenlenen küçük parçalar hiçbir asset istemiyor.
- **Kapılar**: her kapı boşluğunda yaklaşınca açılan çift kanat. Kanatlar
  **çarpışmıyor**: kapalı bir kanat çarpışan bir duvar olsaydı, açılma
  gecikmesinde oyuncu kapıda sıkışırdı.

---

## Kamera ve konfor

Oyun **yalnızca birinci şahıs**. Üçüncü şahıs seçeneği bilerek yok: muayene
masasına eğilip hayvana bakmak oyunun merkezinde ve o an kameranın omuz
üstünde olması işi uzaklaştırıyor.

**O** tuşundaki ayarlar paneli (ayarlar profile kaydediliyor, başka bir
oturumda da aynı gelir):

| Ayar | |
|---|---|
| Ana ses · Müzik · Ses efektleri | üçü ayrı ayrı, %0 – %100 |
| Kamera sallanması | 0 = tamamen kapalı, 2.0 = en yüksek |
| Görüş açısı (FOV) | 55 – 100 |
| Koşma (Shift) | hız + görüş açısının açılması |
| Kare sayacı | FPS, **en düşük kare** ve gecikme (ms) |
| Ellerini göster | birinci şahıs el/alet modeli (kapatılabilir) |

### Ellerini görüyorsun

Oyun zaten yalnızca birinci şahıstı ama aşağı bakınca hiçbir şey yoktu.
`src/client/ViewModel.luau` ekranın altına **iki el ve tuttuğun aleti**
koyuyor:

- **Savrulma (sway)**: fareyi çevirince el gecikmeli savruluyor — kamera
  anında dönüyor, el arkasından yetişiyor.
- **Sallanma**: yürürken elin ritmi `Camera.motion()`den geliyor, yani
  kamerayla **aynı fazda**. İki ayrı sayaç tutulsaydı el birkaç saniyede
  kameradan ayrılır ve "yüzen el" gibi görünürdü.
- **Kullanım animasyonu**: alete göre değişiyor — steteskop *bastırıyor*,
  otoskop *uzanıyor*, idrar tahlili *çeviriyor*, röntgen *süpürüyor*.
  Hangisinin hangisi olduğu `data/tools.json` → `viewModel.motion`.
- Boştayken nefes alıyor.
- **Yerel oyuncunun gerçek gövdesi gizleniyor**: Roblox birinci şahısta
  yalnızca kafayı gizliyor, aşağı bakınca havada yüzen bir gövde görmek
  birinci şahsı bozuyor.

Parçalar `Workspace.CurrentCamera` altında duruyor — kameranın çocukları
sunucuya **replike olmuyor**, yani başka oyuncular senin ellerini görmüyor
ve sunucuda hiçbir şey yaratılmıyor. Motor6D + `Animation` ile kurmak da
mümkündü ama bir `Animation` bir asset'tir ve bu ortamda doğrulanamazdı;
yüklenemeyen bir animasyon ID'si **sessizce hiçbir şey oynatmaz**.

Ekranın ortasında **bağlama duyarlı nişangâh** var: boşken küçük bir nokta,
etkileşim menzilinde açılan bir halka ve eylemin adı. Menzili
`ProximityPromptService` söylüyor — kendi raycast'imizi yazsaydık menzil
tanımı ikiye bölünür ve ikisi birbirinden kayabilirdi.

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

## Parlama neden kesildi

İlk sürümde sahne göz yoruyordu. Suçlu tek bir sayıydı: `Bloom.Threshold`
**1.05**. Bloom, eşiğin üstündeki her pikseli parlatır; 1.0 zaten "beyaz"
demek olduğu için o eşikte beyaz duvar, beyaz dolap, beyaz önlük — hepsi
parlıyordu.

| | eski → yeni |
|---|---|
| `Bloom.Threshold` / `Intensity` | 1.05 / 0.55 → **2.0 / 0.12** |
| `Lighting.Brightness` | 2.4 → 1.7 |
| `ExposureCompensation` | 0.12 → 0 |
| `Atmosphere.Glare` | 0.18 → 0.04 |
| Tavan lambası | 1.6 → 0.75 |
| Ameliyat lambası | 4.0 → 1.5 |
| Sokak lambası | 2.2 → 1.1 |

`Material.Neon` artık yalnızca **gerçekten ışık yayan** üç yerde: tavan
lambası difüzörü, ameliyat lambası merceği, tabeladaki haç. Monitör ve
röntgen **ekranları** artık parlamıyor — gerçek bir ekran karanlık odada
etrafını aydınlatmaz.

Bütün bu sayılar `data/lighting.json` içinde; hâlâ parlak geliyorsa oradan
kısılır. `verify_data.py` eşiğin bir daha 1.8'in altına düşmemesini
denetliyor.

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
denetim** yazıldı. Toplam **9.797 denetim**:

```bash
python3 build.py
python3 tools/verify_place.py       #   303  yer dosyası şeması + tazelik
python3 tools/verify_data.py        # 2.260  veri + ses + ışık + dekor + gündüz/gece + çeviri
python3 tools/verify_luau.py        # 1.025  Luau statik denetimleri
python3 tools/verify_layout.py      # 5.991  3B çakışma, bekleyen hayvan, sahip, dekor, lobi
python3 tools/simulate_economy.py   #   218  ilerleme/ekonomi eğrisi
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

### Çakışan modeller: ölçülüp düzeltildi ve bir daha olamaz

"İç içe giren modeller var" şikâyeti ölçüldü: **19 gerçek kesişme** vardı.
Röntgen cihazı kuzey duvarından koğuşa taşıyordu, lavabo ile çöp kovası üst
üsteydi, ağaçlar çalıların içindeydi, koğuşta bir bekleme noktası mama
istasyonunun içinde kalıyordu.

Çözüm yalnızca "düzelttim" değil, **sözleşme**:

- `data/props.json` her eşyanın **yer bütçesini** bildiriyor ("bu eşya en
  fazla bu kadar yer kaplar").
- `tools/verify_layout.py` bütçelerin birbiriyle, duvarlarla, **kapı ve
  pencere açıklıklarıyla** ve bekleme noktalarıyla çakışmadığını 3B olarak
  denetliyor (Y ekseni dahil: tavan lambası masanın üstünde, çakışma değil).
- `SelfTest.server.luau` eşyayı **gerçekten kurup** ölçüsünün bütçeye
  sığdığını ve en az 8 parçadan oluştuğunu denetliyor.

İkinci turda denetim **hareket eden şeyleri de** kapsayacak şekilde
genişletildi — eskiden yalnızca eşya-eşya bakıyordu:

- **Bekleyen hayvan**: her sıra noktasına en büyük hayvanın bekleme ayak izi
  konuyor. Ayak izi **dairesel** sayılıyor, çünkü hayvanın hangi yöne dönük
  duracağı belli değil. Bu denetim gerçek bir hatayı yakaladı: sıra noktaları
  7 stud arayla diziliydi ve en uzun hayvan 7,2 studdu — hayvanlar üst üste
  biniyordu.
- **Sahip NPC'si**: her sıra noktasının yanındaki NPC yeri boş ve odanın
  içinde olmalı. Bu da bir hata ortaya çıkardı: NPC'nin durduğu nokta kodda
  "sıra noktasının 3 stud sağı" diye hesaplanıyordu, oysa denetim
  `data/clinic.json`'daki `ownerPoints`'i ölçüyordu — yani **denetlenen yer
  ile durulan yer birbirinden kayabiliyordu**. Artık tek kaynak var.
- **Dekorasyon**: süs eşyaları da (satın alınmamış olsalar bile) kuruluşta
  inşa edildikleri için aynı çakışma denetiminden geçiyor.
- **Lobi**: kliniğin denetlendiği her şey — eşya-eşya, sınırlar, portal
  pedleri, doğma alanı — lobide de denetleniyor. Eskiden lobi hiç
  denetlenmiyordu ve şans eseri temizdi.

Yani veri ile model birbirinden kayamıyor — 10 kat detay eklerken en büyük
risk buydu. Şu an: **0 kesişme**.

**Bu denetimler işe yaradı.** Yazılırken yedi gerçek hata yakaladılar:

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
6. **19 model iç içe giriyordu** (yukarıda).
7. `KNOWN_MARKINGS` tablosu, onu okuyan fonksiyondan **sonra** tanımlanmıştı —
   4. maddedeki hatanın aynısı. `verify_luau.py`'nin sıra denetimi bu turda
   yerel tabloları da kapsayacak şekilde genişletildi ve hatayı yakaladı.
8. Sahip NPC'lerinin durduğu nokta koddan hesaplanıyor, denetim ise
   veriden okuyordu — ikisi ayrı olduğu için yerleşim denetimi aslında
   **hiçbir şey garanti etmiyordu**. Tek kaynağa indirildi.
9. Çeviri anahtarı denetimi yalnızca 18 ad alanını tanıyordu; sonradan
   eklenen `toolgame.`, `decor.`, `set.`, `lobby.`, `stat.`, `progress.`,
   `hint.`, `ach.`, `task.`, `fps.`, `board.` **denetlenmiyordu**. Listede
   olmayan bir ad alanı, eksik anahtarın ancak Studio'da görülmesi demekti.
   Liste tamamlandı: şimdi 29 ad alanının hepsi denetleniyor.

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
│   ├── settings.json                 kamera, koşma, ses, birinci şahıs el modeli
│   ├── audio.json                    49 ses ipucu + 3 müzik yuvası
│   ├── lighting.json                 ışık ve son-işlem (parlama buradan kısılır)
│   ├── props.json                    her eşyanın yer bütçesi ve masa yüzeyi
│   ├── lobby.json                    lobi salonu, 4 portal, eşleştirme ayarları
│   ├── economy.json                  XP eğrisi, ücret, itibar, bahşiş, acil vaka, stres
│   ├── clinic.json                   oda planı, duvarlar, kapılar, eşya yerleşimi
│   ├── decor.json                    4 tema paleti + 4 süs eşyası ve yerleri
│   ├── daycycle.json                 gün uzunluğu, gece eşikleri, nöbet çarpanları
│   ├── assetIds.json                 Creator Store yuvaları (boş)
│   └── locale/tr.json                425 satır Türkçe metin
│
├── src/shared/                       Net · Validate · Loc · Assets · Build · Detail · Audio
├── src/server/
│   ├── init.server.luau              tek giriş noktası, kurulum sırası
│   ├── SelfTest.server.luau          açılış denetimleri → Output
│   ├── world/                        Clinic · Props · Lighting · AnimalFactory
│   │                                 AnimalAnimator · OwnerFactory · Lobby
│   │                                 Decor · Doors · Effects · DayCycle
│   └── game/                         PatientFlow · Diagnosis · Treatment · Surgery
│                                     ToolGame · Economy · Upgrades · Profile
│                                     Achievements · Tasks · Leaderboard · Matchmaking
├── src/client/                       Hud · Chart · DiagnosisPanel · TreatmentPanel
│                                     SurgeryPanel · ToolGamePanel · ShopPanel
│                                     ProgressPanel · SettingsPanel · LobbyPanel
│                                     Camera · ViewModel · Crosshair · Fps · ToolBar
│                                     Notify · Theme
└── tools/                            rbxlx yazıcısı + 5 doğrulayıcı
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
- **Kamera sallanmasının şiddeti** kişiye göre değişir; birinci şahısta
  daha güçlü hissettirdiği için varsayılan 0.8'e düşürüldü ve panelden
  0'a kadar indirilebiliyor.
- **Işınlanma yayında denenmedi.** Studio bunu desteklemiyor; kod
  `pcall` içinde ve başarısız olunca kliniği aynı sunucuda açıyor.
  Gerçek davranışı ilk yayınladığında göreceksin.
- **`rbxasset://` ses yolları doğrulanamadı.** Tutmayanlar sessiz kalır ve
  açılışta adıyla Output'a yazılır.
- **Müzik yok** — Roblox hazır müzik parçasıyla gelmiyor; `data/audio.json`
  içindeki `music` yuvalarına bir ID yapıştırman gerekiyor.
- **Hayvanlar ve sahip NPC'leri çarpışmasız yürüyor.** Yolları düz hatlar
  olduğu için duvarlardan geçmiyorlar, ama kalabalıkta birbirlerinin
  içinden geçebilirler.
- **Arayüz yerleşimi** masaüstü çözünürlüğüne göre kuruldu. Sağ sütun
  (kuyruk → vardiya → hasta kartı → teşhis/tedavi) yukarıdan aşağı
  ~790 piksel yer kaplıyor; 768 piksel yüksekliğindeki bir ekranda alttaki
  panel alet çubuğuna değebilir. Panellerin konumu kendi dosyalarının
  `create` fonksiyonlarında tek satır.
- **Birinci şahıs el modeli ekranı kapatabilir.** Ölçüler
  `data/settings.json` → `viewModel` içinde ayarlanabilir; ayar panelinden
  ("Ellerini göster") tamamen kapatılabiliyor.
- **Mini-oyunların pencereleri gecikmede dar gelebilir.** Süreler ve
  yarıçaplar `data/tools.json` → `minigames` içinde; ameliyatta olduğu gibi
  cömert tutuldu ama yüksek gecikmede genişletilmesi gerekebilir.
- **Röntgen mini-oyununda hedefin ekrandaki yeri istemciye gidiyor** —
  çizilmesi gerektiği için. Doğrulama (menzil ve süre) sunucuda; hastalığın
  kimliği hâlâ sızmıyor. Tek oyunculu/işbirlikçi bir PvE oyununda bunun
  bedeli, bir hile istemcisinin mini-oyunu atlayabilmesi.
- **DataStore** yayında denenmedi. Studio'da "Studio Access to API Services"
  kapalıysa oyun bellekte çalışır ve HUD'da Türkçe uyarı gösterir — sessizce
  ilerleme kaybetmez.
- **Ameliyat zamanlaması** senkron sunucu saati (`GetServerTimeNow`) üzerinden
  yürüyor; yüksek gecikmede pencereler (0,18 sn / 0,34 sn) dar gelebilir.
  `data/treatments.json` → `surgery` bölümünden genişletilir.
- **Süre.** Simülasyona göre 15. seviye ~472 hastada, yaklaşık **3,0 saatte**
  geliyor. Daha uzun bir oyun istersen `data/economy.json` içindeki `levels`
  eşiklerini büyüt; `python3 tools/simulate_economy.py` yeni eğriyi anında
  gösterir.
