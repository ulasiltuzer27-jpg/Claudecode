# Play Store'a çıkış rehberi

Bu adımları **sen** yapacaksın; hesaplar ve ödemeler senin adına olmalı.
Kod tarafı hazır. Bu rehber hangi değerin nereye gideceğini gösteriyor.

## 1. Hesaplar

1. **Google Play Console**: <https://play.google.com/console> → kişisel hesap,
   25 USD tek seferlik ücret, kimlik doğrulama (birkaç gün sürebilir).
2. **AdMob**: <https://admob.google.com> → aynı Google hesabıyla.
   - "Uygulama ekle" → Android → henüz yayınlanmadı.
   - İki reklam birimi oluştur: **Ödüllü** ve **Geçiş (interstitial)**.
   - Uygulama kimliği `ca-app-pub-XXXX~YYYY`, birim kimlikleri `ca-app-pub-XXXX/ZZZZ` biçimindedir.
   - AdMob → Gizlilik ve mesajlaşma → **GDPR mesajı** oluştur (AB/BK kullanıcıları için onay formu).
     Oyun bu formu açılışta kendisi gösterir.

> Kendi gerçek reklamlarına **asla** kendin tıklama; AdMob hesabı kapatılır.
> Denemeler için test kimlikleri (varsayılanlar) kullanılır.

## 2. Yükleme anahtarı (bir kez)

```bash
keytool -genkey -v -keystore ~/baglanti-upload.jks -keyalg RSA -keysize 2048 \
  -validity 10000 -alias upload
```

`baglanti_dedektifi/android/key.properties` dosyası oluştur (**depoya girmez**, `.gitignore`'da):

```properties
storePassword=...
keyPassword=...
keyAlias=upload
storeFile=/tam/yol/baglanti-upload.jks
```

Keystore'u ve şifreleri güvenli bir yerde yedekle. Kaybedersen Play App Signing
sayesinde yeni yükleme anahtarı istenebilir, ama süreç uzun sürer.

## 3. Yayın derlemesi

```bash
cd baglanti_dedektifi
flutter build appbundle --release \
  --dart-define=STORE=real \
  --dart-define=ADMOB_REWARDED_ID=ca-app-pub-XXXX/ODULLU \
  --dart-define=ADMOB_INTERSTITIAL_ID=ca-app-pub-XXXX/GECIS \
  -PadmobAppId=ca-app-pub-XXXX~UYGULAMA
```

Çıktı: `build/app/outputs/bundle/release/app-release.aab`. Her yeni sürümde
`pubspec.yaml` içindeki `version: 1.0.0+1` satırının `+` sonrasını artır.

Uygulama kimliği `com.baglantidedektifi.app`. İlk yüklemeden sonra
**değiştirilemez**; değiştirmek istersen ilk yüklemeden önce
`android/app/build.gradle.kts` içinde değiştir.

## 4. Play Console'da uygulama içi ürünler

Play Console → uygulama → Para kazanma → Ürünler. Kimlikler kodla
(`lib/monetization/products.dart`) **birebir** aynı olmalı. Fiyatlar öneri.

| Kimlik | Tür | Öneri (TR) | Ne verir |
|---|---|---|---|
| `coins_300` | Uygulama içi, tüketilebilir | ₺39,99 | 300 jeton |
| `coins_1000` | Uygulama içi, tüketilebilir | ₺99,99 | 1000 jeton |
| `coins_2500` | Uygulama içi, tüketilebilir | ₺199,99 | 2500 jeton |
| `starter_pack` | Uygulama içi | ₺79,99 | 1000 jeton + reklamsız (tek sefer) |
| `remove_ads` | Uygulama içi | ₺119,99 | Geçiş reklamları kalkar |
| `case_pack_1` | Uygulama içi | ₺59,99 | Premium dosyalar (şu an: Sisli Fener) |
| `club_weekly` | Abonelik, haftalık temel plan | ₺49,99 | Dedektif Kulübü |
| `club_yearly` | Abonelik, yıllık temel plan | ₺799,99 | Dedektif Kulübü |

Kulüp: reklamsız + günde 5 ücretsiz büyüteç + tüm premium dosyalar. Haftalık
plana 3 günlük ücretsiz deneme eklemek dönüşümü artırır (Play Console'da
"teklif" olarak).

Ürünler ancak uygulamanın bir AAB'si en az bir test kanalına yüklendikten
sonra oluşturulabilir. Gerçek satın almayı denemek için Play Console →
Ayarlar → **Lisans testi** listesine kendi Gmail adresini ekle; böylece ödeme
alınmaz.

## 5. Mağaza kaydı ve formlar

- **Uygulama simgesi**: `store/icon_512.png`
- **Öne çıkan görsel** (1024×500) ve **ekran görüntüleri** (en az 2, telefon):
  telefonda oyundan alınabilir.
- **Gizlilik politikası URL'si** zorunlu. Metin oyunda: Ayarlar → Gizlilik
  politikası. Aynı metni herkese açık bir sayfada yayınla (GitHub Pages ya
  da Google Sites ücretsiz) ve URL'yi buraya yaz.
- **Uygulama içeriği** formları:
  - Reklam içeriyor mu: **Evet**
  - Hedef kitle: **18 ve üzeri** (Families politikasına girmemek için)
  - İçerik derecelendirmesi (IARC anketi): şiddet yok, kumar yok (jetonlar
    gerçek paraya çevrilmiyor), kullanıcı etkileşimi yok
  - **Veri güvenliği**: Uygulama veri toplamıyor, ama **AdMob** cihaz
    kimliği/reklam kimliği ve tanılama verisi topluyor. AdMob'un resmi
    "Data safety" rehberine göre doldur: Cihaz veya diğer kimlikler →
    toplanıyor, paylaşılıyor (reklam amaçlı). Satın alma geçmişi → Google
    Play üzerinden.
- **AdMob `app-ads.txt`**: geliştirici web siteni Play kaydına ekle ve
  AdMob'un verdiği satırı `https://siten/app-ads.txt` adresine koy.

## 6. Test ve yayın sırası

1. **Dahili test**: AAB'yi yükle, kendin indir, satın almaları lisans test
   hesabıyla dene.
2. **Kapalı test** (zorunlu): 13 Kasım 2023'ten sonra açılan kişisel
   hesaplarda üretime geçmeden önce **en az 12 test kullanıcısı 14 gün
   kesintisiz** katılmış olmalı. Arkadaş, aile, dedektif/bulmaca toplulukları.
   Test kullanıcılarının oyunu gerçekten oynaması önemli.
3. **Üretim erişimi başvurusu** → onaydan sonra üretime yayın.

## 7. Yayından sonra

- Play Console → İstatistikler: 1. gün / 7. gün tutma oranı, dönüşüm.
- AdMob → eCPM, gösterim oranı. Geçiş reklamı sıklığı
  `lib/game/economy.dart` içinde (`interstitialEveryBoards`).
- Yeni dosyalar: `tool/content/cases.py` → `build_content.py` →
  `dart run tool/validate_content.dart`.

---

## Mağaza metinleri

### Türkçe

**Uygulama adı**: Bağlantı Dedektifi: Kanıt Panosu

**Kısa açıklama** (80): Kanıtları kırmızı iple bağla, ipuçlarını topla, suçluyu bul!

**Tam açıklama**:

Mantar panoya asılmış kanıtlara bak: bir elma, bir gül, bir itfaiye arabası...
Ortak noktaları ne? Kırmızı ipi çek, bağlantıyı kur, yeni bir ipucu kazan!

Bağlantı Dedektifi; ilişki kurma bulmacalarını sürükleyici dedektif
hikâyeleriyle birleştiriyor. Her dosyada 10 pano çöz, ipuçlarını topla,
şüphelileri sorgula ve sonunda suçluyu doğru kanıtlarla göster.

• Özenle hazırlanmış bağlantı bulmacaları
• Her dosyada yeni bir gizem: müzede kaybolan elmas, gece treninde soygun,
  sisli fenerdeki batık...
• Sahte delillere dikkat: hepsini bulursan "Keskin Göz" bonusu!
• Günün Delili: her gün yeni bir pano ve seri ödülleri
• Takıldığında büyüteç her zaman yanında
• Türkçe ve İngilizce

Dedektif Kulübü ile reklamsız oyna, her gün ücretsiz büyüteç al ve tüm
premium dosyaları aç.

### English

**App name**: Link Detective: Evidence Board

**Short description** (80): Tie the evidence with red thread, collect clues and catch the culprit!

**Full description**:

Look at the evidence pinned to the cork board: an apple, a rose, a fire
engine... What do they have in common? Pull the red thread, make the
connection, and earn a new clue!

Link Detective blends association puzzles with gripping detective stories.
Solve 10 boards in every case, collect clues, size up the suspects, and
finally name the culprit with the right evidence.

• Handcrafted connection puzzles
• A new mystery in every case: a vanished diamond, a heist on the night
  train, a shipwreck at the foggy lighthouse...
• Watch out for fake evidence: spot them all for a Keen Eye bonus!
• Daily Evidence: a fresh board every day, with streak rewards
• Stuck? Your magnifier is always at hand
• English and Turkish

Join the Detective Club to play ad-free, get free magnifiers every day and
unlock every premium case.
