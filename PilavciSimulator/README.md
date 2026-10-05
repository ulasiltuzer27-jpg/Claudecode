# Pilavcı Simülatörü

Birinci şahıs bir sokak pilavcısı işletme simülasyonu. Depo mutfağında pilavını
pişiriyorsun, arabanı mahallenin işlek köşelerine itiyorsun, müşterilere servis
yapıp para üstü veriyorsun. Zabıtadan kaçıyor, martılara ve kedilere karşı
tabağını koruyorsun. Arkadaşlarınla 2–4 kişilik co-op oynanabilir.

- **Motor:** [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) 7.0.2 (raylib 5.5), .NET 8.
- **Çok oyunculu:** host otoriteli. Steam derlemesinde Steam lobisi ve relay kullanılır, geliştirme derlemesinde doğrudan IP (LiteNetLib).
- **İçerik:** hiçbir hazır varlık gerekmez. Modeller, dokular ve sesler çalışma anında kodla üretilir; istenirse dosyayla değiştirilebilir (aşağıda).

Bu klasör depodaki PixelSurvival oyunundan bağımsızdır. Kökteki
`PixelSurvival.csproj` bu klasörü derlemeye katmaz.

## Derleme ve çalıştırma

Gerekenler: .NET 8 SDK.

```bash
cd PilavciSimulator
dotnet build                       # Debug, Steam'siz; Windows/Linux/macOS'ta çalışır
dotnet run --project Game          # oyunu başlatır
dotnet build -c Release
dotnet build -c SteamRelease       # Facepunch.Steamworks + STEAM_BUILD (yalnızca Windows x64'te Steam'e bağlanır)
dotnet test                        # mantık testleri (pencere açmaz)
```

Windows'ta çıktı `Game/bin/<Yapılandırma>/net8.0/PilavciSimulator.exe` olur. `Data/` ve
`Assets/` klasörleri exe'nin yanına kopyalanır. Dengeleme JSON'la yapılır ve
derleme gerektirmez.

### Komut satırı

| Argüman | Ne yapar |
|---|---|
| `--new-game` | Menüyü atlayıp yeni oyun açar (kayıt yuvasına yazmaz) |
| `--host [--port N]` | Yeni oyun açar ve co-op'a açar (varsayılan port 27015) |
| `--join ADRES [--port N]` | Doğrudan bir host'a bağlanır |
| `--name AD` | Oyuncu adı (Steam'siz) |
| `--skip-tutorial` | Rehberli ilk günü kapatır |
| `--windowed 1280x720` | Pencere boyutu |
| `--data-dir KLASÖR` | Kayıt/ayar/log klasörünü değiştirir (testler için) |
| `--seed N`, `--no-audio` | Sabit tohum, sessiz çalışma |
| `--capture-script F --capture-out D` | Senaryo oynatıp ekran görüntüsü alır (aşağıda) |

## Kontroller

Varsayılan tuşlar. Hepsi Ayarlar → Kontroller'den değiştirilebilir. Gamepad menülerde çalışır.

| Tuş | Eylem |
|---|---|
| W A S D | Yürü |
| Fare | Bak |
| Shift / Space / Ctrl | Koş / zıpla / eğil |
| E | Al, yerleştir, düğme çevir. Basılı tutunca yıka veya su doldur |
| R | İkincil eylem: kapak, ateşi kıs, siparişi reddet |
| Sol tık | Elindekini kullan: dök, ekle, kepçele, servis et. Basılı tutunca karıştır |
| G | Bırak. Basılı tutunca fırlat |
| Tab | Telefon: harita, satış noktaları, hava, haberler |
| T / Enter | Sohbet (co-op) |
| Esc | Duraklat / geri |
| F12 | Ekran görüntüsü |
| Kasa ekranında 1–7, Backspace, Enter | Banknot seç, geri al, ver |

## Oynanış özeti

- **Pilav:** pirinci süzgeçle al, lavaboda yıka, kazan ocağında tereyağında kavur,
  ölçü kabıyla su ekle (1 kg pirince 1,5 L), tuzla, kapağı kapat. Su çekilince
  ateşi kapat ve demlendir. Kalite (0–100) yıkama, yağ, kavurma, su oranı, ateş,
  demleme ve tuza göre hesaplanır. Ateşte unutursan dibi tutar.
- **Nohut, tavuk, fasulye, et:** tencerede haşlanır. Kuru nohut önceden ıslatılır
  ya da hazır kavanoz alınır. Tavuk kesme tahtasında didiklenir.
- **Satış:** kazanı arabaya yükle, arabayı it, bir satış noktasında aç. Müşteriler
  kuyruğa girer. Tabak al, kepçele, üstüne ekleme yap, müşteriye uzat, kasada para
  üstünü ver.
- **Noktalar:** okul önü, sanayi, hastane önü, stadyum (maç günleri) ve iskele.
  Ruhsatsız noktaya zabıta gelebilir; arabayı zamanında kaçır.
- **Ekonomi:** fiyat tabelası, laptoptan toptancı siparişi (kamyonet gelir,
  kolileri rafa dizersin), haftalık zam, yükseltmeler, çırak ve masalı dükkan.
- **Gün döngüsü:** 06:00'da başlar. Gerçek zamanda yaklaşık 21 dakikada kapanış
  saatine (22:00) gelinir. Akşam depodaki yatakta uyuyunca gün raporu çıkar ve
  otomatik kayıt yapılır. Gece yarısını sokakta geçirirsen sızarsın.
- **Co-op:** para, stok ve itibar ortaktır; kayıt host'ta kalır. Oyun ortasında
  katılınabilir.

## Kayıtlar, ayarlar, loglar

`%LOCALAPPDATA%\PilavciSimulator\` (Linux'ta `~/.local/share/PilavciSimulator/`) altında:

- `saves/`: 3 kayıt yuvası (`slotN.pilav` + `slotN.json`). Yazma atomiktir ve
  `.bak` yedeği tutulur; bozuk kayıtta yedek açılır.
- `config/settings.json`: ayarlar ve tuş atamaları.
- `logs/`: günlük ve çökme raporları (`crash-*.txt`).
- `screenshots/`: F12 ile alınan ekran görüntüleri.

## Modelleri ve sesleri değiştirmek

- `Game/Assets/Models/<anahtar>.glb` (ya da `.gltf`/`.obj`): prosedürel modelin
  yerine yüklenir. Anahtar listesi ve kurallar `Game/Assets/Models/README.md`
  içinde.
- `Game/Assets/Audio/Sfx/<id>.wav`: efekt ya da ortam döngüsü yerine çalınır.
- `Game/Assets/Audio/Music/*.ogg`: arka planda sırayla çalınan müzik.
- Çeviriler: `Tools/make_localization.py` TR ve EN tablolarını tek kaynaktan üretir
  (`Game/Data/Localization/*.json`).

## Doğrulama araçları

Pencere ve GPU gerekmez. Xvfb ve Mesa (yazılım GL) yeterli.

```bash
# Senaryo oynat, ekran görüntüleri al
LIBGL_ALWAYS_SOFTWARE=1 xvfb-run -a --server-args="-screen 0 1600x900x24" \
  Game/bin/Debug/net8.0/PilavciSimulator --data-dir /tmp/pilav --windowed 1280x720 \
  --capture-script Tools/captures/serve.txt --capture-out /tmp/pilav-shots

Tools/run_captures.sh     # tüm senaryolar (menu, gameplay, serve, tutorial)
Tools/verify_coop.sh      # host + istemci: istemcinin servisi host'un kasasına yansıyor mu
```

Senaryo komutları (`wait`, `waitfor`, `tap`, `down`/`up`, `click`, `look`, `mouse`,
`type`, `shot`, `annotate`, `expect`, `cmd …`) `Game/Diagnostics/CaptureHarness.cs`
içinde açıklanır. Geliştirici komutları (`tp`, `aim`, `act`, `time`, `cook`, `cart`,
`customers`, `endday` …) `Game/Screens/GameplayScreen.cs` içindedir.

## Steam'e çıkış kontrol listesi

1. **AppId:** Steamworks'ten aldığın AppId'yi `Game/PilavciSimulator.csproj` içindeki
   `<SteamAppId>` satırına yaz. Tek kaynak budur. Varsayılan 480, Valve'in test
   uygulamasıdır (Spacewar).
2. **Başarımlar:** Steamworks → Stats & Achievements'a `Game/Data/achievements.json`
   içindeki 27 kimliği aynen gir (`ilk_tabak`, `yuz_tabak` … `pilav_sultani`).
   İstatistikler: `served`, `days_played`, `correct_change`, `kazan_cooked`.
   Liderlik tablosu: `en_iyi_gun`.
3. **Zengin durum:** `status` anahtarı kullanılıyor (`#Status` yerelleştirme dosyası
   isteğe bağlı).
4. **Steam Cloud (Auto-Cloud):** kök `WinAppDataLocal`, alt klasör
   `PilavciSimulator/saves`, desen `*`.
5. **Derleme:** Windows'ta `Tools\publish-steam.bat`, Linux/macOS'ta
   `Tools/publish-steam.sh`. Çıktı `Tools/out/steam/content`. Kendi kendine yeten
   win-x64 derlemesidir ve .NET kurulumu gerektirmez. `steam_appid.txt` çıktıya
   konmaz.
6. **Yükleme:** `Tools/steam/app_build.vdf` ve `depot_build.vdf` içindeki AppID ve
   DepotID'yi değiştir, sonra
   `steamcmd +login <kullanıcı> +run_app_build <yol>/Tools/steam/app_build.vdf +quit`.
7. **Mağaza:** kapsül görselleri, ekran görüntüleri (F12 ya da capture
   senaryoları), açıklama ve yaş derecelendirmesi Steamworks panelinden girilir.

## Bilinen boşluklar

- Steam lobisi, relay ağı, başarımlar ve liderlik tablosu kodda var ve derleniyor,
  ama gerçek bir Steam istemcisine karşı denenmedi. Steam açılamazsa oyun
  Steam'siz (yerel) moda düşer. İlk iş olarak Spacewar (480) ile iki Steam
  hesabında denenmeli.
- Modeller ve sesler prosedürel yer tutuculardır: low-poly ve sentezlenmiş.
  Yayın kalitesi için `.glb` ve ses dosyalarıyla değiştirilmeleri önerilir.
- Karakterler parça parça canlandırılıyor; kemikli tek parça model desteği yok.
- Co-op'ta istemci kendi hareketini anında uygular, dünyanın geri kalanı host'tan
  gelir. Yüksek gecikmede eşyalar kısa süre geride görünebilir.
- Yalnızca Windows için Steam derlemesi var: Facepunch.Steamworks 2.3.3 yalnızca
  win-x64 taşıyor. Linux/macOS'ta oyun Steam'siz çalışır.
- Raylib-cs 8.x (raylib 6) yükseltmesi denenmedi.

## Klasör yapısı

```
PilavciSimulator/
  PilavciSimulator.sln
  Game/                  oyun (WinExe)
    Core/                yollar, log, ayarlar, JSON, RNG, çökme raporu
    Engine/              render (shader, gölge, dokular, mesh), girdi, arayüz, ses
    Sim/                 motordan bağımsız simülasyon (pişirme, müşteri, ekonomi, olaylar, fizik, yol bulma)
    World/               mahalle yerleşimi ve geometri
    Net/                 protokol, host/istemci oturumları, LiteNetLib ve Steam taşımaları
    Client/              dünya çizimi, karakter, HUD, yerel oyuncu
    Screens/             menüler ve oyun ekranları
    Platform/            Steam / yerel platform
    Persistence/         kayıt yuvaları
    Diagnostics/         senaryo oynatıcı
    Data/                denge JSON'ları ve çeviriler
    Assets/              yazı tipi, ikon, isteğe bağlı model/ses değiştirmeleri
  Tests/                 xUnit mantık testleri
  Tools/                 senaryolar, co-op doğrulaması, yayın betikleri, Steam şablonları
```
