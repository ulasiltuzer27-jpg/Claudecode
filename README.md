# PixelSurvival

Top-down, pixel-art, gerçek zamanlı çok oyunculu survival oyunu.
MonoGame **3.8.5.1** (DesktopGL) · .NET 8 · C#

> `PixelSurvival` çalışma adıdır. Değiştirmek için: `.sln` / `.csproj` dosya adları,
> `.csproj` içindeki `RootNamespace` + `AssemblyName`, `.cs` dosyalarındaki `namespace`,
> ve `app.manifest` içindeki `assemblyIdentity name`.

> ⚠ **Bu proje henüz BİR KEZ BİLE derlenmedi.** 8900+ satır C# yazıldı, algoritmalar
> Python'da bağımsız olarak doğrulandı, ama `dotnet build` hiç çalıştırılmadı.
> İlk iş bu olmalı.

**Durum: Aşama 1 tamam (madde 1–11) + Aşama 2 / madde 12–19.** Sprite üretici, hareket,
çarpışma, kamera, chunk tabanlı prosedürel sonsuz dünya, kaynak toplama,
WorldInventory, crafting, grid tabanlı inşa, ağ oturumu (host/istemci) ve dövüş
çalışıyor.

Mevsim/hava, tarım+balıkçılık, evcil hayvan/binek, düşman+boss rotasyonu,
zindanlar, NPC tüccar/görev, güvenli bölge/PvP ayrımı + baskın ve Steam
envanteri + Steam taşıma katmanı eklendi.

Henüz yok: Steamworks tam entegrasyonu (20–21), clan/trade (22),
localization (23), photo mode (24), Workshop (25), kaydetme/yükleme.

## Kontroller

| Girdi | Eylem |
|-------|-------|
| WASD / yön tuşları | hareket |
| Gamepad D-Pad / sol analog stick | hareket |
| Space / E / gamepad A (**basılı tut**) | bakılan tile'ı topla |
| 1 – 4 | üretim panelindeki tarifi uygula |
| Tab | üretim panelini aç/kapat |
| Q / Z | inşa edilecek yapıyı değiştir |
| R / gamepad B | seçili yapıyı hedefe koy |
| F / sol Ctrl / gamepad X | saldır |
| C | tarım: sür / ek / hasat (bağlamsal) |
| X | ekilecek tohumu değiştir |
| V (**basılı tut**) | suya bak, oltayı at, yeşil bölgede bırak |
| G | yaratığı besle / bin / in |
| T | yakındaki NPC ile konuş (sat / görev teslim et) |
| B | zindan girişine/çıkışına bas |
| F (yapıya bakarken) | PvP bölgesindeki yapıya baskın |
| F9 | sunucu aç (port 7777) |
| F10 | localhost'a bağlan |
| F11 | oturumdan ayrıl |
| F1 | asset denetim görünümü (ham sprite sheet'ler) |
| F2 | debug görünümü (katı tile kırmızı, collider yeşil, chunk sınırı sarı) |
| F5 | rastgele yeni tohumla dünyayı yeniden üret |
| Escape / gamepad Back | çıkış |

Hareket 8 yönlü, animasyon 4 yönlü (Stardew Valley yaklaşımı). Katı 4 yön isteniyorsa
`Entities/Player.cs` içindeki `SnapToFourDirections` sabiti `true` yapılır.

---

## Hızlı başlangıç

```bash
pip install -r Tools/requirements.txt

./run.sh          # Linux / macOS
run.bat           # Windows
```

Script sırayla: assetleri üretir → içerik doğrulaması yapar → MGCB araçlarını ve
NuGet paketlerini indirir → oyunu çalıştırır. Herhangi bir adım patlarsa durur.

Elle yapmak isterseniz:

```bash
python3 Tools/generate_placeholders.py --clean
python3 Tools/verify_content.py
python3 Tools/verify_worldgen.py
dotnet tool restore     # MGCB + MGCB Editor
dotnet restore
dotnet run
```

Linux'ta Content Pipeline'ın native bağımlılıkları gerekebilir:

```bash
sudo apt install -y libfreetype6-dev libgdiplus
```

Content dosyalarını görsel editörle düzenlemek için:
`dotnet mgcb-editor Content/Content.mgcb`

---

## Doğrulama

### 1. Statik kontrol (oyunu açmadan, saniyeler içinde)

```bash
python3 Tools/verify_content.py
```

Content Pipeline'ın en sinsi hatasını yakalar: `Content.Load<T>("X")` ile
`Content.mgcb` içindeki `/build:` girdisinin uyuşmaması. Bu hata **derlemede
görünmez**, sadece oyun açılırken `ContentLoadException` olarak patlar.

Ayrıca baktıkları: mgcb'de listelenip diskte olmayan dosyalar, diskte olup mgcb'ye
eklenmemiş dosyalar, PNG'lerin geçerli ve RGBA olması, sheet boyutlarının yanındaki
JSON metadata ile tutarlı olması.

Hata varsa çıkış kodu 1 → pre-commit hook veya CI'a doğrudan bağlanabilir.

### 2. Görsel kontrol

`dotnet run` sonrası 1280x720 pencere açılmalı:

| # | Beklenen | Anlamı |
|---|----------|--------|
| 1 | Çim/toprak desenli zemin | Tileset pipeline'dan yüklendi (Madde 1–2) |
| 2 | Ortada duran karakter | Karakter sheet'i + JSON metadata yüklendi |
| 3 | Pixel kenarları **keskin** | `SamplerState.PointClamp` doğru |
| 4 | Karakterin çevresinde gri hale/kutu yok | Alfa ve `PremultiplyAlpha` uyumlu |
| 5 | WASD ile akıcı hareket, çapraz daha hızlı **değil** | Normalize + delta-time doğru (Madde 3) |
| 6 | Yön değişince sprite yönü değişiyor | 4 yönlü facing çözümlemesi |
| 7 | Yürürken bacaklar oynuyor, durunca idle | Animator state geçişleri |
| 8 | Gamepad stick'i yarım ittirince yavaş yürüyor | Analog kısmi eğim korunuyor |
| 9 | Kamera karakteri takip ediyor | Yumuşatılmış takip (Madde 4) |
| 10 | Taşa/suya/ağaca girilemiyor | Tile çarpışması |
| 11 | Duvara çapraz bastırınca duvar boyunca **kayıyor** | Eksen ayrık çözüm |
| 12 | Tile'lar arasında 1px boşluk çizgisi **yok** | Kamera kaydırması tam sayıya yuvarlanıyor |
| 13 | Yürüdükçe çim/orman/kumsal/dağ geçişleri çıkıyor | Biome üretimi (Madde 5) |
| 14 | Ne kadar yürürsen yürü dünya bitmiyor, dikiş yok | Chunk akışı |
| 15 | F2'de sarı chunk kareleri kenarda belirip arkada kayboluyor | Chunk yükleme/atma |
| 16 | F5 tamamen farklı bir dünya üretiyor | Tohum ayrımı |
| 17 | Konsoldaki sağlama `verify_worldgen.py` ile aynı | Üretim algoritması doğrulandı |
| 18 | Bakılan tile'da çerçeve var; ağaç/taşta beyaz, boş zeminde soluk | Hedefleme (Madde 6) |
| 19 | Space basılı tutunca çubuk doluyor, ağaç kayboluyor | Toplama |
| 20 | Kesilen ağacın yerinden geçilebiliyor | Tile gerçekten değişti |
| 21 | Uzaklaşıp geri dönünce ağaç geri **gelmiyor** | Override katmanı chunk'tan bağımsız |
| 22 | Yarım kesip yön değiştirince ilerleme sıfırlanıyor | Bedava kesim istismarı kapalı |
| 23 | Konsolda `[toplama] +3 wood ...` satırları | Toplama sonucu |
| 24 | Alt ortada envanter çubuğu, ağaç kesince odun ikonu + sayı | WorldInventory (Madde 7) |
| 25 | 99'u geçen odun ikinci slota taşıyor | Yığın limiti |
| 26 | Sağ üstte üretim paneli, Türkçe karakterler doğru | Bitmap font + crafting (Madde 8) |
| 27 | Malzemesi olan tarif yeşil, olmayan soluk | Tarif uygunluk kontrolü |
| 28 | `1` tuşu odunu tahtaya çeviriyor, sayılar güncelleniyor | Üretim |
| 29 | Malzeme yokken `3` "Malzeme yetersiz" diyor, envanter değişmiyor | İşlem bütünlüğü |
| 30 | Hedefte yeşil/kırmızı inşa hayaleti var | İnşa önizlemesi (Madde 9) |
| 31 | `R` yapıyı koyuyor, taş duvardan geçilemiyor | Grid tabanlı inşa |
| 32 | Konulan yapı Space ile sökülüp envantere dönüyor | Sökme (toplama sistemi) |
| 33 | Uzaklaşıp dönünce yapı yerinde duruyor | Override katmanı |
| 34 | İki örnek: biri F9, diğeri F10 → karakterler birbirini görüyor | Ağ oturumu (Madde 10) |
| 35 | Diğer oyuncu akıcı hareket ediyor, kekelemiyor | Snapshot interpolasyonu |
| 36 | Kendi hareketin anında tepki veriyor | Client-side prediction |
| 37 | `F` ile diğer oyuncuya vurunca can çubuğu düşüyor | Dövüş (Madde 11) |
| 38 | 5 vuruşta ölüyor, 3 saniye sonra doğuyor | Hasar ve yeniden doğma |
| 39 | Sağ üstte gün/saat/mevsim/hava yazıyor, saat akıyor | Dünya saati (Madde 12) |
| 40 | Gece ekran maviye kararıyor, şafakta turuncuya dönüyor | Gündüz-gece döngüsü |
| 41 | Yağmurda çizgiler, karda salınan taneler düşüyor | Hava parçacıkları |
| 42 | `C` çimi sürüyor, ikinci `C` tohum ekiyor | Tarım (Madde 13) |
| 43 | Ekin günler geçtikçe büyüyor, olgununca `C` hasat ediyor | Ekin döngüsü |
| 44 | Balkabağı ilkbaharda ekiliyor ama **büyümüyor** | Mevsim kilidi |
| 45 | Suya bakıp `V` tutunca çubuk çıkıyor, yeşilde bırakınca balık | Balıkçılık |
| 46 | Yağmurda yeşil bölge geniş, gece dar | Hava/saat oynanışa dokunuyor |
| 47 | Çevrede dört ayaklı yaratıklar dolaşıyor | Yaratık üretimi (Madde 14) |
| 48 | `G` ile 3 kez yem verince evcilleşip peşinden geliyor | Evcilleştirme |
| 49 | Tekrar `G` ile biniyorsun, hareket belirgin hızlanıyor | Binek |
| 50 | Gece düşmanlar daha sık doğuyor, uzakta beliriyor | Düşman üretimi (Madde 15) |
| 51 | `F` ile düşmana vurunca can çubuğu düşüyor, ölünce ganimet | Düşman dövüşü |
| 52 | Sağ üstteki bilgide aktif boss 3 günde bir değişiyor | Boss rotasyonu |
| 53 | Taşlık alanda seyrek mor tile'lar var, `B` içeri sokuyor | Zindan girişi (Madde 16) |
| 54 | Zindanda kamera duvarların dışını **göstermiyor** | Sınırlı harita clamp'i |
| 55 | En uzak odada boss bekliyor, yenilince çıkış açılıyor | Zindan döngüsü |
| 56 | Aynı girişe tekrar girince **aynı** zindan çıkıyor | Konumdan türeyen tohum |
| 57 | Spawn çevresinde iki NPC duruyor | NPC yerleşimi (Madde 17) |
| 58 | Tüccarda `T` item satıyor, altın geliyor | Ticaret |
| 59 | İhtiyarda `T` görev durumunu söylüyor, malzeme varsa tamamlıyor | Görev zinciri |
| 60 | Sol altta "GUVENLI BOLGE" yazıyor, uzaklaşınca "PvP BOLGESI" oluyor | Bölge ayrımı (Madde 18) |
| 61 | Güvenli bölgede yapı kırılamıyor, uyarı çıkıyor | Bölge kuralı |
| 62 | PvP bölgesinde `F` ile yapıya 5 vuruş = yıkım | Baskın |
| 63 | Yarım bırakılan baskın 30 sn sonra iyileşiyor | Yapı onarımı |
| 64 | Sol altta "Steam yok (gelistirme derlemesi)" yazıyor | Steam katmanı (Madde 19) |
| 65 | Escape pencereyi kapatıyor | — |

Referans kare üretip ekran görüntünüzle yan yana koyun:

```bash
python3 Tools/preview_expected_frame.py   # -> expected_frame.png
```

`Game1.Draw()` ile aynı kaynak PNG'leri ve aynı yerleşim matematiğini kullanır.
Aradaki her fark bir hata işaretidir.

### Sorun giderme

| Belirti | Sebep |
|---------|-------|
| Ekran boş | Content derlenmemiş ya da asset adları tutmuyor → `verify_content.py` |
| Sprite'lar bulanık | `SpriteBatch.Begin(samplerState: SamplerState.PointClamp)` eksik |
| Sprite'larda gri hale | `PremultiplyAlpha` ile `BlendState` uyuşmuyor |
| `ContentLoadException` | Önce `generate_placeholders.py` çalıştırılmamış |
| `NU1102 dotnet-mgcb-editor-*` | `.config/dotnet-tools.json` sürümü paket sürümüyle uyuşmuyor |

---

## Dosya yapısı

```
.config/dotnet-tools.json   MGCB + MGCB Editor arac manifesti (dotnet tool restore)
app.manifest                Windows DPI awareness — pixel art'in ezilmemesi icin
Icon.ico / Icon.bmp         pencere ikonu (gomulu kaynak)
PixelSurvival.csproj        MonoGame 3.8.5.1 DesktopGL, net8.0
Program.cs                  giris noktasi
Game1.cs                    oyun dongusu, gecici test zemini, F1 denetim gorunumu
run.sh / run.bat            uret -> dogrula -> restore -> calistir

Content/                    Content.mgcb + derlenen sprite/tile assetleri
Tools/                      sprite ureteci + dogrulama araclari

Entities/       Player.cs (hareket + facing + collider) · NPC/boss/item sonra
Systems/Camera2D.cs   takip eden kamera, zoom, istege bagli sinir clamp'i
Systems/Input/        PlayerInput.cs, InputReader.cs
Systems/Animation/    SpriteSheet.cs, SpriteAnimator.cs
Systems/Collision/    TileCollider.cs (float AABB + eksen ayrik cozum)
Systems/Gathering/    GatheringSystem.cs (hedefleme + basili tutmali toplama)
Systems/Crafting/     CraftingSystem.cs (RecipeBook + islem butunluklu uretim)
Systems/Building/     BuildingSystem.cs (grid yerlestirme + dogrulama)
Systems/Combat/       CombatSystem.cs (HOST OTORITER hasar, olum, dirilme)
Systems/Climate/      ClimateSystem.cs (dunya saati, mevsim, hava)
Systems/Farming/      FarmingSystem.cs (surme, ekme, buyume, hasat)
Systems/Fishing/      FishingSystem.cs (zamanlama mini oyunu)
Systems/Taming/       TamingSystem.cs (yaratik, evcillestirme, binek)
Systems/Hostiles/     Enemy.cs, EnemySystem.cs (dusman AI + boss rotasyonu)
Systems/Dungeons/     DungeonSystem.cs (instance giris/cikis)
Systems/Npcs/         NpcSystem.cs (ticaret + gorev zinciri)
Systems/Zones/        ZoneSystem.cs (guvenli/PvP + baskin)
Inventory/Steam/      SteamInventory.cs (STEAM OTORITER - grant metodu YOK)
                      ISteamItemGrantAuthority.cs (grant yolu, backend arkasinda)
                      SteamSession.cs (Steam tarafinin tek giris noktasi)
Networking/SteamNetworkingTransport.cs  ISteamNetworkingSockets uzerinden
Networking/TransportFactory.cs          tasima seciminin TEK noktasi
World/ITileGenerator.cs  tile uretimi soyutlamasi
World/DungeonGenerator.cs oda-koridor uretimi (sinirli harita)
Networking/           INetworkTransport.cs (soyutlama - gameplay bunu gorur)
                      LiteNetLibTransport.cs (LiteNetLib'e dokunan TEK dosya)
                      NetworkProtocol.cs (elle yazilmis little-endian kablo duzeni)
                      NetworkSession.cs (host/istemci, 20 tick/sn dongu)
Inventory/            WorldInventory.cs (HOST AUTHORITATIVE - Steam ile karistirilmaz)
UI/BitmapFont.cs      atlas tabanli yazi, SpriteFont bagimliligi YOK
UI/HudRenderer.cs     envanter cubugu + uretim paneli
World/Noise.cs        tohumlanmis Perlin + fBm (kendi PRNG'si, tasinabilir)
World/BiomeTable.cs   biomes.json'un kod karsiligi
World/WorldGenerator.cs  gurultu -> biome -> tile, spawn bulma, saglama
World/TileMap.cs      chunk onbellegi, yukleme/atma, cizim
World/Tileset.cs      tile dokusu + solid bayraklari
World/ResourceTable.cs   resources.json'un kod karsiligi

Content/World/biomes.json     biome esikleri, sacilim, spawn ayarlari (VERI)
Content/World/resources.json  hangi tile ne verir, ne kadar surer (VERI)
Content/Items/items.json      item tanimlari, slot sayisi, yigin limitleri (VERI)
Content/Items/recipes.json    crafting tarifleri (VERI)
Content/World/buildables.json hangi item hangi tile'i insa eder (VERI)
Content/World/climate.json    gun uzunlugu, fazlar, mevsimler, hava (VERI)
Content/World/crops.json      ekinler, mevsimleri, buyume hizlari (VERI)
Content/World/fishing.json    mini oyun zorlugu (VERI)
Content/Entities/creatures.json yaratik, evcillestirme, binek (VERI)
Content/Entities/enemies.json dusman/boss istatistikleri, rotasyon (VERI)
Content/Entities/npcs.json    NPC ticareti ve gorevler (VERI)
Content/World/dungeons.json   zindan boyutu, oda sayisi, odul (VERI)
Content/World/zones.json      guvenli yaricap, baskin ayarlari (VERI)
Content/Steam/itemdefs.json   Steamworks ItemDef AYNASI (sahiplik kaniti DEGIL)
Content/Items/icons_16.png    item ikonlari
Content/UI/font_ascii.png     pisirilmis bitmap font atlasi
World/          chunk, terrain generation, biome, PvP/guvenli bolge metadata
Networking/     INetworkTransport + LiteNetLib / SteamNetworking implementasyonlari
Inventory/      WorldInventory ve SteamInventory (AYRI siniflar)
UI/             HUD, envanter, photo mode, achievement, trade, leaderboard, patch notes
Localization/   EN/TR string tablolari
Accessibility/  renk koru paleti, yazi boyutu
Achievements/   Steam achievement tanimlari
```

`Networking/`, `Inventory/`, `UI/`, `Localization/`, `Accessibility/`,
`Achievements/` **bilerek boş**. Her birinde hangi maddede doldurulacağını yazan bir
`README.md` var.

### Dünyayı ayarlamak

Biome eşikleri, saçılım oranları ve gürültü ölçekleri `Content/World/biomes.json`
içinde. Daha çok su, daha sık orman, daha dağlık bir dünya için **yeniden derlemeye
gerek yok** — JSON'u değiştirip oyunu yeniden başlatmak yeter.

Eşik seçerken dikkat: fBm çıktısı 0..1 arasına düz dağılmaz, **0.5 etrafında
yığılır** (standart sapma ~0.10). `0.30` gibi bir eşik sezgisel olarak "alt %30"
gibi durur ama pratikte alt %1'e denk gelir. Mevcut değerler ölçülen yüzdeliklerden
alındı. Eşik değiştirdikten sonra dağılımı kontrol edin:

```bash
python3 Tools/verify_worldgen.py --minimap harita.png
```

Hangi tile'ın katı olduğu da koda gömülü değil — `tileset_16.json` içindeki `solid`
alanından okunur.

### Dünya değişiklikleri (override katmanı)

Chunk'lar bellekten atılıp geri geldiklerinde gürültüden **yeniden üretilir**.
Kesilen ağaç chunk verisine yazılsaydı, oyuncu uzaklaşıp döndüğünde ağaç geri gelirdi.

Bu yüzden oyuncunun yaptığı kalıcı değişiklikler `TileMap` içinde ayrı bir sözlükte
tutulur ve **her okuma yolunda üretimin önüne geçer** — chunk yüklü olsun olmasın.
Madde 9'daki inşa sistemi de bu katmanı kullanacak.

Kalıcılık (save) geldiğinde diske yazılması gereken tek dünya verisi budur; gerisi
tohumdan yeniden üretilebilir.

Toplanabilir kaynaklar **yeniden doğmaz**. Dünya sonsuz olduğu için tükenen bölgeden
uzaklaşılır. Yeniden doğma mekaniği kapsam dışı bırakıldı.

### İki envanter asla karıştırılmaz

`Inventory/WorldInventory.cs` **host authoritative**: odun, taş, maden, yiyecek,
alet, crafting materyalleri. Dünyayı açan oyuncunun makinesi bu veriye tam yetkilidir.

Karakter lisansları, satılabilir kozmetikler ve pazar değeri taşıyan varlıklar
**buraya girmez**. Onlar madde 19'da ayrı bir `PixelSurvival.Inventory.Steam`
namespace'inde yaşayacak ve sahiplikleri Steam Inventory Service tarafından
doğrulanacak.

**Kural:** WorldInventory verisi tek başına marketable/tradable bir Steam item'ı
grant edemez. Oyun içi crafting sonucu Steam item'ı üretilecekse bu, Steam'in
doğrulayabildiği bir exchange recipe veya güvenilir backend üzerinden yapılır —
asla doğrudan WorldInventory → SteamInventory geçişi olarak kodlanmaz.
Publisher/Economy API key hiçbir koşulda client'a gömülmez.

Pratik sonuç: `WorldInventory.cs` dosyasına Steam'e ait bir tip, alan veya `using`
eklenmez. Böyle bir ihtiyaç doğuyorsa tasarım yanlıştır.

> Namespace neden `Inventory.World` değil? C#'ta `PixelSurvival.World` harita
> namespace'i ile çakışıp nitelenmiş isimleri belirsiz hale getiriyordu. Ayrım
> namespace adında değil, sınıf ve dosya düzeyinde net tutuldu.

### İki kişilik test nasıl yapılır

```bash
# 1. terminal — sunucu
dotnet run          # oyunda F9

# 2. terminal — istemci (AYNI makinede olabilir)
dotnet run          # oyunda F10
```

İstemci bağlanınca host'un tohumunu alır ve dünyayı yeniden üretir. Harita verisi
ağdan **gönderilmez** — iki taraf aynı tohumdan aynı dünyayı üretir. Sadece
oyuncuların yaptığı değişiklikler (kesilen ağaç, konan duvar) tile mesajıyla gider.

### Ağda ne otoriter, ne değil

`Networking/NetworkSession.cs` başındaki nota bakın. Özetle:

**Host otoriter:** oyuncu konumları, can/hasar/ölüm/dirilme, dünya tile'ları,
istemcilerin toplayarak kazandığı item'lar.

**Henüz otoriter DEĞİL — kapatılacak açıklar:**
* Crafting istemcide yerel çalışıyor, host doğrulamıyor.
* İnşa yalnızca host'ta çalışıyor; istemci inşa edemiyor.
* İstemci envanteri host'ta ayna tutulmuyor.

Bunlar PvP ve ekonomi anlam kazanmadan **önce** kapatılmalı. Madde 18 (PvP bölgesi)
ve madde 22 (oyuncular arası trade) bu açıklar açıkken yapılamaz.

`Send()` ileride teslim modu (Reliable/Unreliable) parametresi alacak şekilde
tasarlandı — pozisyon unreliable, saldırı reliable gitmeli. **Bu aşamada
kodlanmadı**, arayüz genişletilebilir bırakıldı.

Madde 19'da `SteamNetworkingTransport` eklenecek (ISteamNetworkingSockets üzerinden;
eski ISteamNetworking API'si deprecated, kullanılmayacak). Gameplay kodunda tek
satır değişmeyecek — sadece hangi `INetworkTransport` örneğinin kurulduğu değişecek.

### Aşama 2 sistemleri birbirine bağlı

Madde 12-14 kasten iç içe geçirildi; üç ayrı ada değiller:

* **Ekin büyümesi dünya saatine bağlı.** Gerçek saniye değil, geçen *dünya günü*
  sayılıyor — `climate.json`'da gün uzunluğunu değiştirirsen tarım dengesi
  kendiliğinden uyum sağlar.
* **Ekinler mevsim kilitli.** Balkabağı sonbahar dışında ekilebilir ama ilerlemez.
  Yağmurda büyüme %50 hızlanır.
* **Balık zorluğu hava ve saate bağlı.** Yağmurda yeşil bölge %20'den %30'a çıkar,
  gece %15'e düşer. Hava böylece sadece görsel olmaktan çıkıyor.
* **Yem zinciri tarımdan geliyor:** buğday → yem → evcilleştirme → binek.

### Zindan neden ayrı bir harita sınıfı gerektirmedi

`TileMap` zaten üreticiden yalnızca iki şey istiyordu: *"şu koordinatta hangi tile"*
ve *"katı mı"*. Bu sözleşmeyi `ITileGenerator` olarak dışarı çıkarınca chunk'lı
sonsuz dünya ile sınırlı zindan **aynı TileMap sınıfını** kullanabildi.

Alternatifi ikinci bir harita sınıfı yazmaktı; o zaman `TileCollider`, `Player`,
`GatheringSystem`, `BuildingSystem`, `FarmingSystem` ve `Creature`'ın hepsinin
imzasını değiştirmek gerekirdi.

Kamera tarafında da tek satır değişmedi: madde 5'te `Camera2D`'nin sınırını
`Rectangle?` yapmıştık, zindan sınırlı bir harita olduğu için clamp kendiliğinden
devreye giriyor.

Zindan tohumu **girişin dünya koordinatından** türer — aynı girişe tekrar girince
aynı zindan çıkar. Rastgele olsaydı oyuncu haritayı hiç öğrenemezdi.

### Boss dengesi — bilinçli bir tercih

Ölçülen sayılar:

| Boss | DPS | Oyuncu öldürme süresi | Oyuncunun ölme süresi |
|------|-----|----------------------|----------------------|
| Yosun Devi | 12.2 | 9.6 sn | 8.2 sn |
| Buz Çenesi | 21.4 | 7.8 sn | 4.7 sn |

**İki boss da düz dövüşte kazanıyor.** Bu kaza değil: oyuncu her ikisinden de hızlı
(90 vs 44/58 px/sn), yani vur-kaç yapmak zorunda. Boss yürüyerek vakit kaybederken
oyuncu vurmaya devam edebiliyor.

Ama düşmanlar **yol bulmuyor** — düz çizgide yürüyorlar. Bu yüzden vur-kaç şu an
gereğinden kolay ve duvar arkasına geçince düşman takılıyor. Yol bulma eklenene
kadar boss dengesi gerçek bir test sayılmaz.

### Madde 19: kritik kural yorum satırıyla korunmuyor

Spec'in en sert kuralı: **WorldInventory verisi tek başına marketable/tradable
bir Steam item'ı grant edemez.** Bunu bir yorum satırına yazmak yeterli değil —
ileride biri (biz de olabiliriz) "kısa yoldan şunu verelim" der, derleme geçer,
testler geçer, ekonomi sessizce açılır.

Dört yapısal savunma:

1. **Tip ayrımı.** Steam item kimliği `SteamItemDefId` (int sarmalayıcı), dünya
   item kimliği `string`. Biri diğerinin yerine geçemez — derleme zamanında yakalanır.
2. **`SteamInventory`'de Grant metodu YOK.** Item üretmek (`GenerateItems`) bir Web
   API çağrısıdır ve publisher key ister. O anahtar client'a gömülmez; grant yolu
   `ISteamItemGrantAuthority` arkasında ve tek meşru implementasyonu güvenilir
   bir backend. Şu anki implementasyon (`UnconfiguredGrantAuthority`) **kasten
   hiçbir şey yapmıyor** — "şimdilik client'tan verelim" yolu bilerek kapalı.
3. **Sınır katmanı.** `Game1` `SteamInventory` tipini hiç görmüyor, yalnızca
   `SteamSession`'ı görüyor. İki envanter aynı dosyada buluştuğu anda aralarında
   köprü kurmak bir satırlık iş haline gelirdi.
4. **Statik denetim.** `verify_content.py` her çalıştığında kontrol ediyor:
   `Inventory/Steam/` altında `WorldInventory` geçemez, hiçbir dosyada iki envanter
   birlikte geçemez, depoda 32 haneli hex (Web API anahtarı) veya `GenerateItems`
   /`publisherKey` gibi ifadeler bulunamaz. Üçünü de kasten bozup test ettim.

Ayrıca: ItemDef'lerde `damage`/`health`/`speed` gibi alan bulunması hata sayılıyor —
**nadirlik yalnızca kozmetik farktır**, gameplay gücü pay-to-win yaratır.

### Steam derlemesi ayrı bir yapılandırma

```bash
dotnet build                    # LiteNetLib, Steamworks.NET YOK, Steam istemcisi gerekmez
dotnet build -c SteamRelease    # Steamworks.NET + STEAM_BUILD + SteamNetworkingTransport
```

Varsayılan derleme Steam'e hiç bağımlı değil. `SteamNetworkingTransport`
`ISteamNetworkingSockets` üzerinden yazıldı — eski `ISteamNetworking` API'si
deprecated ve kullanılmıyor. P2P adresleme SteamID üzerinden: NAT traversal ve
Valve relay ağı bedavaya geliyor, oyuncular birbirinin IP'sini görmüyor.

Taşıma seçimi tek bir yerde (`TransportFactory`). `NetworkSession` artık hiçbir
somut kütüphane adı geçirmiyor — gameplay hangi taşımanın aktif olduğundan habersiz.

### Madde 18: bölge kuralı hedefin konumuna bakar

Güvenli bölgedeki bir oyuncuya sınırın hemen dışından vurulamaz — kontrol
**hedefin** konumuna göre yapılır, saldıranınkine göre değil.

Bölge tamamen konumdan türer, kaydedilen bir durum yok: senkronlanacak fazladan
veri olmuyor, host ve istemci aynı yarıçap hesabından aynı sonuca varıyor.

Baskın sökmekten **verimsiz**: malzemenin %50'si dönüyor. Yoksa yapı kurmak yerine
başkasınınkini kırmak her zaman daha kârlı olurdu. Yarım bırakılan baskın 30 saniye
sonra iyileşiyor.

### Bilinen boşluklar (Aşama 2)

* **Yaratıklar, düşmanlar, NPC'ler ve zindanlar ağda senkronize DEĞİL.** Her istemci
  kendi kopyasını görür. Protokole varlık senkronizasyonu mesajı gerekiyor —
  şu an yalnızca oyuncular, tile'lar ve dünya saati senkron.
* **Düşman yol bulma yok.** Düz çizgide yürüyorlar, duvar arkasına geçince takılırlar.
* **Görev ilerlemesi kaydedilmiyor** (save sistemi henüz yok).
* **Yapı sahipliği kaydı yok.** PvP bölgesindeki her yapı herkes tarafından
  kırılabilir; kimin kurduğu tutulmuyor. Sahiplik madde 22'deki clan sistemiyle
  birlikte gelmeli.
* **Steam entegrasyonu hiç çalıştırılmadı.** `SteamNetworkingTransport` ve
  `SteamInventory` gerçek bir Steam istemcisine karşı denenmedi. Steamworks.NET
  sürümü de doğrulanmadı.
* **Tarım istemcide çalışmıyor** — `C` tuşu istemci modunda uyarı veriyor.
  Madde 10'daki yetki notlarıyla birlikte kapatılmalı.
* Dünya saati ve hava **host otoriter** ve senkronize (saniyede bir `WorldTime`
  mesajı). Ekin büyümesi de otomatik olarak host'ta kalıyor.

### Yazı neden SpriteFont değil?

MonoGame'in `SpriteFont`'u Content Pipeline'da `FontDescriptionProcessor` kullanır ve
**derleyen makinede kurulu bir font** ister. Bu, projeyi "benim makinemde çalışıyor"
durumuna açık hale getirir ve CI'da kırılır.

Bunun yerine `Tools/generate_placeholders.py` bir bitmap font atlası pişirip repoya
koyar; oyun sadece bir doku yükler, font bağımlılığı sıfırdır. Atlas Türkçe harfleri
de içerir — aksanlar temel ASCII glifinin üstüne bindirilerek kuruluyor, çünkü
Pillow'un varsayılan fontunda bu glifler yok.

### Üretim doğrulaması

Oyun açılışta konsola şunu basar:

```
[worldgen] tohum=20260908 spawn=(0,0) chunk(0,0) sağlama=0x49C29A30
```

`Tools/verify_worldgen.py` aynı sağlamayı **bağımsız bir implementasyonla** hesaplar.
İki değer tutuyorsa C# üretici, test edilmiş algoritmayla aynı sonucu veriyor demektir.

Tutmuyorsa: önce biome dağılımlarına bakın. Bir iki tile farkı, biome eşiğinin tam
kenarındaki koordinatta oluşan zararsız float yuvarlama farkıdır. Dağılımlar tamamen
farklıysa gerçek bir hata var (permütasyon tablosu, PRNG veya oktav toplama sırası
ayrılmış olabilir).

---

## Asset sözleşmesi (sanatçı için)

Placeholder'lar gerçek Aseprite sanatıyla **1:1** değiştirilebilir:
aynı dosya adı, aynı frame boyutu, aynı grid düzeni.

**Karakter sheet'i** — `Content/Characters/<key>.png`, frame 32x32, 4 sütun x 11 satır.
Her animasyon state'i bir satır, frame'ler soldan sağa, artan hücreler tamamen şeffaf:

```
satir  0-3   idle_down / idle_up / idle_left / idle_right   (2 frame)
satir  4-7   walk_down / walk_up / walk_left / walk_right   (4 frame)
satir  8     harvest                                        (3 frame)
satir  9     hurt                                           (2 frame)
satir 10     death                                          (4 frame)
```

**Tile atlası** — `Content/Tiles/tileset_16.png`, 16x16.
**Satır = tile türü, sütun = varyant** (şu an 3 varyant). Satır sırası
`tileset_16.json` içindeki `tiles` listesidir; kod ve veri dosyaları bu
indeksi kullanır. Varyant sayısını değiştirirseniz JSON'daki `variants`
alanını da güncelleyin — C# tarafı oradan okuyor.

Her PNG'nin yanında aynı adla `.json` var; frame boyutu ve satır→state eşlemesi
orada. Oyun kodu bu JSON'u okuyacak, düzen koda gömülmeyecek. Grid değişirse JSON
da güncellenmeli — `verify_content.py` uyuşmazlığı yakalar.

Üretici **deterministik**: aynı script tekrar çalıştırıldığında byte-identical PNG
üretir, git diff'i temiz kalır.

Yeni karakter eklemek: `Tools/generate_placeholders.py` içindeki `CHARACTERS`
listesine bir `CharacterSpec` ekleyin, sonra o PNG'yi `Content.mgcb`'ye kaydedin
(veya `mgcb-editor` ile sürükleyin). Şu an sadece 2 ücretsiz karakter var; 8 rarity
karakteri Aşama 2 / madde 20'de gelecek.

---

## Sıradaki adımlar (Aşama 1 vertical slice)

~~3. Top-down karakter hareketi (4 yön, klavye + gamepad)~~ ✔
~~4. Basit collision + kamera~~ ✔
~~5. Chunk tabanlı prosedürel harita (noise + biome)~~ ✔
~~6. Kaynak toplama (odun, taş)~~ ✔
~~7. WorldInventory~~ ✔
~~8. Basit crafting~~ ✔
~~9. Grid tabanlı inşa~~ ✔
~~10. `INetworkTransport` + `LiteNetLibTransport` ile 2 kişilik local test~~ ✔
~~11. Basit combat~~ ✔

## Aşama 2 (devam)

~~12. Mevsim/gündüz-gece/hava döngüsü~~ ✔
~~13. Balıkçılık/tarım mini oyunu~~ ✔
~~14. Evcil hayvan/binek sistemi~~ ✔
~~15. Boss/düşman rotasyon sistemi~~ ✔
~~16. Zindan/instance alan sistemi~~ ✔
~~17. NPC tüccar/görev sistemi~~ ✔
~~18. Güvenli bölge/PvP bölge ayrımı + raid~~ ✔
~~19. SteamInventory + SteamNetworkingTransport~~ ✔
20. Karakter kozmetik/rarity/layered sprite + sezonluk item
21. Steamworks.NET tam entegrasyonu
22. Clan/guild + oyuncular arası trade
23. Localization + erişilebilirlik
24. Photo mode + emote + ping + patch notes
25. Steam Workshop

## İlk yapılacak iş

Kod yazmak değil: **`dotnet build` çalıştırmak.**

8900 satır hiç derlenmedi. Algoritmaları Python'da doğruladım (dünya üretimi,
çarpışma, protokol, envanter, dövüş, iklim, tarım, balıkçılık, zindan üretimi,
düşman dengesi, ticaret ekonomisi, bölge kuralları, Steam ayrım denetimi) ama bu
derlemenin yerine geçmez. LiteNetLib ve Steamworks.NET sürümleri, MonoGame
Content Pipeline ve 42 dosyalık C# hiç sınanmadı.

Derleme geçtikten sonra ikinci iş: arkadaşınla iki örnek açıp oynamak.
