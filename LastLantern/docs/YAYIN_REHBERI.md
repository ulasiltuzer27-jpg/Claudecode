# Son Fener (Last Lantern): Google Play Yayın Rehberi

Bu rehber, oyunu Google Play'e çıkarmak için yapmanız gereken her şeyi sırayla anlatır.
Kodla ilgili kısımlar hazır; burada kalanlar yalnızca sizin hesaplarınızla yapılabilecek işler.

> **Önemli tarih kuralı:** Yeni kişisel geliştirici hesaplarında Google, uygulamayı herkese
> açmadan önce **en az 12 test kullanıcısıyla 14 gün kesintisiz kapalı test** ister. Bu süreyi
> erkenden başlatın (aşağıda 8. adım).

---

## 0. Hızlı kontrol listesi

- [ ] 1. Uygulama kimliğini (`APP_ID`) kendi adınıza göre değiştirdiniz
- [ ] 2. Upload anahtarını oluşturup GitHub secret'larına eklediniz
- [ ] 3. Gizlilik politikasındaki e-postayı değiştirip sayfayı yayınladınız
- [ ] 4. Play Console geliştirici hesabını açtınız
- [ ] 5. AdMob uygulamasını ve ödüllü reklam birimini oluşturup kimlikleri girdiniz
- [ ] 6. Play Console'da uygulamayı oluşturup "Uygulama içeriği" formlarını doldurdunuz
- [ ] 7. Mağaza girişini (metinler + görseller) yüklediniz
- [ ] 8. İç test, ardından 12 kişilik ve 14 günlük kapalı test
- [ ] 9. `supporter_pack` ürününü oluşturdunuz
- [ ] 10. Üretime (herkese açık) başvurdunuz

---

## 1. Uygulama kimliği (yayından sonra DEĞİŞTİRİLEMEZ)

`LastLantern/gradle.properties` dosyasındaki satırı açın:

```
APP_ID=com.lastlantern.game
```

Bunu size ait, dünyada eşsiz bir kimlikle değiştirin. Örnek: `com.ulasstudio.sonfener`. Yalnızca
küçük harf, rakam ve nokta kullanın. Play'e ilk yüklemeden sonra bu değer bir daha değişemez.

## 2. Upload anahtarı (imza)

Play, uygulamanızı kendi anahtarıyla imzalar (Play App Signing). Sizin yalnızca bir **upload
anahtarına** ihtiyacınız var. Bilgisayarınızda Java kurulu olmalı:

```bash
keytool -genkeypair -v -keystore upload.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

Sorulan şifreyi ve bilgileri not edin. **Bu dosyayı ve şifreyi kaybetmeyin; depoya asla koymayın.**
(`.gitignore` zaten `*.jks` dosyalarını dışarıda tutuyor.)

Dosyayı Base64'e çevirin:

- Linux / macOS: `base64 -w0 upload.jks > upload.b64` (macOS: `base64 -i upload.jks -o upload.b64`)
- Windows (PowerShell): `[Convert]::ToBase64String([IO.File]::ReadAllBytes("upload.jks")) > upload.b64`

GitHub'da depo → **Settings → Secrets and variables → Actions → New repository secret** ile
dört secret ekleyin:

| Ad | Değer |
|---|---|
| `LL_UPLOAD_KEYSTORE_B64` | `upload.b64` dosyasının içeriği |
| `LL_KEYSTORE_PASSWORD` | keystore şifresi |
| `LL_KEY_ALIAS` | `upload` |
| `LL_KEY_PASSWORD` | anahtar şifresi (genelde keystore şifresiyle aynı) |

Bundan sonra her push'ta GitHub Actions → **Last Lantern (Android)** çalışmasının
**Artifacts** bölümünde imzalı `last-lantern-release-aab` dosyası oluşur. Play'e yükleyeceğiniz
dosya budur. Sürüm kodu (`versionCode`) her derlemede otomatik artar.

## 3. Gizlilik politikası

1. `LastLantern/docs/privacy-policy.html` içindeki `destek@ornek.com` adresini (iki yerde)
   kendi destek e-postanızla değiştirin.
2. Değişiklikleri `main` dalına birleştirin (Pages yalnızca varsayılan daldan yayın yapar).
3. GitHub'da depo → **Settings → Pages → Source: GitHub Actions** seçin.
4. **Actions → Last Lantern (gizlilik politikasi sayfasi) → Run workflow** ile yayınlayın.
5. Adres: `https://ulasiltuzer27-jpg.github.io/Claudecode/privacy-policy.html`
   (Oyundaki "Gizlilik politikası" düğmesi de bu adresi açar. Adres değişirse
   `core/src/main/kotlin/com/lastlantern/Links.kt` dosyasını da güncelleyin.)

## 4. Play Console hesabı

- <https://play.google.com/console> adresinden geliştirici hesabı açın (tek seferlik 25 USD).
- Kimlik doğrulamasını tamamlayın.
- Satın alma satacağınız için **Ödeme profili** (Payments profile) oluşturun.

## 5. AdMob (ödüllü reklam)

Kod şu an Google'ın **test** reklam kimliklerini kullanıyor. Gerçek reklamlar için:

1. <https://admob.google.com> adresinde hesap açın ve **Uygulamalar → Uygulama ekle → Android**
   seçin. Uygulama henüz mağazada olmadığı için "listelenmemiş" seçeneğiyle ekleyin; yayından
   sonra mağaza kaydına bağlarsınız.
2. **Reklam birimi ekle → Ödüllü** seçin. Ad: `revive_and_double`. Ödül: 1 adet.
3. İki kimliği `LastLantern/gradle.properties` dosyasına yazın:
   ```
   ADMOB_APP_ID=ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY
   ADMOB_REWARDED_ID=ca-app-pub-XXXXXXXXXXXXXXXX/ZZZZZZZZZZ
   ```
4. **Gizlilik ve mesajlaşma → AB yönetmelikleri (GDPR)** bölümünde bu uygulama için bir onay
   mesajı oluşturup yayınlayın. Oyundaki UMP entegrasyonu bu mesajı AB/İngiltere'deki
   oyunculara otomatik gösterir; "Ayarlar → Gizlilik seçenekleri" düğmesi de buna bağlıdır.
5. **Kendi gerçek reklamlarınıza asla tıklamayın.** Test için telefonunuzu AdMob'da "test cihazı"
   olarak ekleyin.
6. *(Önerilen)* **app-ads.txt:** AdMob, geliştirici web sitenizin kökünde `app-ads.txt` ister.
   GitHub'da `ulasiltuzer27-jpg.github.io` adlı yeni bir depo açıp içine AdMob'un verdiği tek
   satırlık `app-ads.txt` dosyasını koyun. Play Console'da web sitesi olarak
   `https://ulasiltuzer27-jpg.github.io` girin.

## 6. Play Console'da uygulama ve "Uygulama içeriği"

**Uygulama oluştur:** Ad: *Son Fener: Geceyi Atlat* (ya da *Last Lantern: Night Survivor*),
varsayılan dil: Türkçe, tür: **Oyun**, **Ücretsiz**.

**Politika → Uygulama içeriği** bölümündeki formlar:

| Form | Cevap |
|---|---|
| Gizlilik politikası | 3. adımdaki adres |
| Uygulama erişimi | Tüm işlevler kısıtlama olmadan kullanılabilir |
| Reklamlar | **Evet, reklam içeriyor** |
| İçerik derecelendirmesi | Anket: Oyun. Şiddet: fantastik/çizgi film tarzı, kan yok. Kumar, cinsellik, küfür, uyuşturucu: yok. Kullanıcı etkileşimi/paylaşım: yok. Dijital satın alma: **var**. Beklenen sonuç: PEGI 7 / ESRB Everyone 10+ |
| Hedef kitle | **13-15, 16-17 ve 18+** (13 yaş altını seçmeyin; aksi halde Aile Politikası ve ek reklam kısıtlamaları gerekir) |
| Haber uygulaması | Hayır |
| Veri güvenliği | Aşağıya bakın |
| Devlet / finans / sağlık uygulaması | Hayır |

### Veri güvenliği formu

Oyunun kendisi sunucuya veri göndermez; veri toplayan kısım Google Mobile Ads SDK'sıdır
(ve Play Billing). Önerilen beyan:

- **Veri toplanıyor veya paylaşılıyor mu?** Evet.
- **Aktarımda şifreleniyor mu?** Evet.
- **Kullanıcılar verilerinin silinmesini isteyebilir mi?** Evet. Cihazdaki tüm veriler "Ayarlar →
  İlerlemeyi sıfırla" ile ya da uygulamayı kaldırarak silinir.

| Veri türü | Toplanıyor | Paylaşılıyor | Amaç | Zorunlu mu |
|---|---|---|---|---|
| Cihaz veya diğer kimlikler (reklam kimliği) | Evet | Evet (Google AdMob) | Reklam veya pazarlama, Analiz, Dolandırıcılığı önleme | Zorunlu değil (onaya bağlı) |
| Yaklaşık konum (IP'den) | Evet | Evet (Google AdMob) | Reklam, Dolandırıcılığı önleme | Zorunlu değil |
| Uygulama etkileşimleri | Evet | Evet (Google AdMob) | Reklam, Analiz | Zorunlu değil |
| Kilitlenme günlükleri / Teşhis | Evet | Evet (Google AdMob) | Analiz | Zorunlu değil |
| Satın alma geçmişi | Evet | Hayır | Uygulama işlevselliği | Zorunlu değil |

Bu tablo, Google'ın Mobile Ads SDK için yayımladığı veri beyanı rehberine göre hazırlandı. Form
dolduğunda Play, mağaza sayfanızda bir özet gösterir.

## 7. Mağaza girişi

**Büyüme → Mağazadaki varlık → Ana mağaza girişi** bölümünde Türkçe ve İngilizce (çeviri ekle)
için şu dosyaları kullanın:

| Alan | Türkçe | İngilizce |
|---|---|---|
| Uygulama adı | `store/tr/title.txt` | `store/en/title.txt` |
| Kısa açıklama | `store/tr/short-description.txt` | `store/en/short-description.txt` |
| Tam açıklama | `store/tr/full-description.txt` | `store/en/full-description.txt` |
| Uygulama simgesi (512x512) | `store/icon-512.png` | aynı |
| Öne çıkan grafik (1024x500) | `store/tr/feature-graphic.png` | `store/en/feature-graphic.png` |
| Telefon ekran görüntüleri | `store/tr/screenshots/*.png` (8 adet) | `store/en/screenshots/*.png` |

Kategori: **Oyun → Aksiyon** (ya da Rol Yapma). Etiketler: *Roguelite, Hayatta kalma, Piksel,
Çevrimdışı*. İletişim e-postası zorunludur.

Ekran görüntüleri oyundan otomatik alınır; içerik değişirse yeniden üretmek için:

```bash
cd LastLantern
tools/capture.sh 1080x1700 store/raw/en game_crowd levelup evolve boss game_early drowned camp stages
CAPTURE_LANG=tr tools/capture.sh 1080x1700 store/raw/tr game_crowd levelup evolve boss game_early drowned camp stages
python3 tools/gen_store_art.py
```

## 8. Test sürümleri

1. **Test → İç test → Yeni sürüm oluştur.** İmzalı AAB'yi (2. adım) yükleyin. Sürüm notu
   yazın, kendinizi test kullanıcısı olarak ekleyin, bağlantıdan yükleyip oynayın.
2. Play Console'daki **Ön lansman raporu**nu inceleyin (otomatik cihaz testleri).
3. **Test → Kapalı test** için bir kanal oluşturun, en az **12** kişinin e-postasını ekleyin
   (Google grubu da olur) ve sürümü bu kanala yükseltin. Testçilerin bağlantıdan katılıp
   oyunu **14 gün boyunca** yüklü tutması gerekir.
4. 14 gün dolunca **Üretim erişimi için başvur** düğmesi açılır. Formda testçilerin geri
   bildirimlerini ve yaptığınız düzeltmeleri kısaca anlatın.

Hızlı deneme: Her push sonrası GitHub Actions'taki `last-lantern-debug-apk` dosyasını
telefonunuza indirip doğrudan yükleyebilirsiniz ("bilinmeyen kaynaklar" izni gerekir). Debug
sürüm ayrı bir uygulama olarak kurulur (`.debug` son ekiyle).

## 9. Destekçi Paketi (uygulama içi satın alma)

İç teste en az bir AAB yükledikten sonra **Para kazanma → Ürünler → Uygulama içi ürünler →
Ürün oluştur**:

| Alan | Değer |
|---|---|
| Ürün kimliği | `supporter_pack` (kodla aynı olmalı, değiştirmeyin) |
| Ad | Destekçi Paketi / Supporter Pack |
| Açıklama | Reklam izlemeden ödüller, kalıcı +%25 altın ve altın fener |
| Fiyat | Örnek: 49,99 TL / 2,99 USD (Play diğer ülkeler için dönüştürür) |

Ürünü **Etkinleştir**in. Satın almayı test etmek için **Ayarlar → Lisans testi** bölümüne
kendi Google hesabınızı ekleyin; böylece gerçek ödeme yapılmaz.

## 10. Üretim

Kapalı test şartı tamamlanınca **Üretim → Yeni sürüm**, AAB'yi seçin, ülkeleri belirleyin ve
incelemeye gönderin. İlk inceleme birkaç gün sürebilir.

---

## Sürüm güncelleme

1. `gradle.properties` içindeki `VERSION_NAME` değerini artırın (örn. `1.0.1`). Ayrıca
   `core/src/main/kotlin/com/lastlantern/Links.kt` içindeki `VERSION` değerini de aynı yapın.
2. Push edin; CI imzalı AAB üretir (`versionCode` otomatik artar).
3. Play Console'da önce iç teste, sonra üretime yükleyin.

## Yerelde Android derlemesi (isteğe bağlı)

Android SDK kurulu bir bilgisayarda:

```bash
cd LastLantern
./gradlew :android:assembleDebug        # APK: android/build/outputs/apk/debug/
./gradlew :android:bundleRelease        # AAB (LL_KEYSTORE_* ortam değişkenleriyle imzalı)
```

Masaüstünde oynamak için: `./gradlew :lwjgl3:run` (WASD ile hareket, Esc ile duraklat).
