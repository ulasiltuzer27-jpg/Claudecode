# Bağlantı Dedektifi

Android için bulmaca oyunu (Flutter). Oyuncu, mantar panodaki delil
kartlarından ortak bir bağlantısı olanları kırmızı iple bağlar. Çözülen her
pano dava için bir ipucu açar, davanın sonunda suçlu doğru kanıtlarla
gösterilir.

- **Gelir**: ödüllü + geçiş reklamları (AdMob), uygulama içi satın almalar
  (jeton, reklamsız, başlangıç paketi, premium dosyalar) ve "Dedektif
  Kulübü" aboneliği (Google Play Billing).
- **İçerik**: 8 dava (80 pano; 1–6 ücretsiz ana hikâye, 7–8 premium Özel
  Dosyalar), 60 günlük pano, 664 kart. TR + EN.
- **Bağlılık**: 7 günlük giriş ödülü, Günün Delili serisi, akşam hatırlatma
  bildirimi (izinle), 16 başarım, isteğe bağlı Google Play Games sıralaması.
- **Ses**: kodla üretilen efektler ve yağmurlu noir ambiyansı (`tool/audio`).
- **Yayın rehberi**: [docs/play_store.md](docs/play_store.md)

## Çalıştırma

Flutter 3.50 (stable) gerekir.

```bash
flutter pub get
flutter test                          # motor, kurallar, içerik, arayüz akışı
dart run tool/validate_content.dart   # tüm içerik denetimi
flutter run                           # bağlı telefonda (debug: sahte mağaza + AdMob test reklamları)
flutter build web --no-web-resources-cdn   # tarayıcıda hızlı önizleme (sahte reklam/mağaza)
```

`--dart-define=STORE=...` değerleri:

| Değer | Mağaza | Reklam | Ne için |
|---|---|---|---|
| `fake` | sahte | sahte | web, otomatik testler |
| `test` | sahte | AdMob test reklamları | Play Console'dan önce telefonda deneme |
| `real` | Google Play | AdMob | yayın |
| (boş) | debug'da `test`, release'te `real` | | |

GitHub'a her push'ta `.github/workflows/baglanti-dedektifi-android.yml`
testleri çalıştırır ve **Releases → "Test APK"** altına telefona kurulabilir
bir APK koyar.

## Klasörler

```
lib/
  data/          içerik modelleri, JSON yükleyici, içerik doğrulayıcı
  game/          saf Dart oyun motoru (pano, suçlama, ekonomi)
  progress/      kayıt, günlük pano ve seri
  monetization/  AdMob, Play Billing, sahte servisler
  audio/         ses servisi (audioplayers)
  notify/        hatırlatma planı ve bildirimler
  social/        Google Play Games (isteğe bağlı)
  app/           GameState, tema, TR/EN metinler
  ui/            ekranlar ve widget'lar
assets/
  content/       üretilen JSON (elle düzenleme!)
  icons/         Fluent Emoji Flat SVG'leri (MIT)
  fonts/         Courier Prime, Playfair Display (OFL)
tool/
  content/       içeriğin kaynağı: items.py, cases*.py, daily*.py, build_content.py
  audio/         ses sentezi
  validate_content.dart
```

## İçerik ekleme

İçeriğin tek kaynağı `tool/content/*.py` dosyaları. JSON ve ikonlar oradan üretilir:

```bash
npm pack @iconify-json/fluent-emoji-flat && tar -xzf iconify-json-fluent-emoji-flat-*.tgz
python3 tool/content/build_content.py --icons package/icons.json
dart run tool/validate_content.dart
```

Bulmaca kuralları:

- Her panoda 2+ grup, grup başına 3–5 kart, en fazla 3 sahte delil, en fazla 20 kart.
- Bir kart başka bir gruba da uyuyor gibi görünüyorsa, ait gibi göründüğü
  grubun `also` alanına nedenini yaz. Doğrulayıcı bu notların panodaki
  gerçek kartlara işaret ettiğini denetler.
- Mantık tek çözümlü olmalı: grup boyutları ve diğer gruplar hesaba
  katıldığında her kartın tek bir yeri kalmalı.
- Kelime oyunu yok: bağlantı TR ve EN'de aynı şekilde geçerli olmalı.

## Sesler

Tüm sesler kodla sentezlenir (lisans gerekmez). Değiştirmek için
`tool/audio/synth_sfx.py` dosyasını düzenleyip çalıştır (numpy ve ffmpeg gerekir):

```bash
python3 tool/audio/synth_sfx.py      # assets/audio/*.ogg
```

## Lisanslar

- İkonlar: [Microsoft Fluent Emoji](https://github.com/microsoft/fluentui-emoji) (MIT)
- Yazı tipleri: Courier Prime, Playfair Display (SIL Open Font License)

Lisans metinleri `assets/licenses/` altında ve oyunda Ayarlar → Lisanslar sayfasında.
