# Play Store Meta Verileri ve Yayın Hazırlığı

Bu belge, Unity projesinin (`Scripts/`) Google Play'de yayınlanması için
mağaza metinlerini, Play Console "Veri güvenliği" formunun AdMob özetini
ve yayın öncesi kontrol listesini içerir. Metinler oyunda **gerçekten var
olan** özelliklere dayanır; oyuna özellik eklemeden açıklamaya yenisini
yazmayın (Google Play meta veri politikası yanıltıcı açıklamayı yasaklar).

Karakter sayıları bu belgedeki metinlerin birebir ölçümüdür. Google Play
sınırları: başlık 30, kısa açıklama 80, uzun açıklama 4000 karakter.

---

## 1. Oyun başlığı önerileri (30 karakter altı)

> ⚠️ **"Idle Restaurant Tycoon" adını kullanmayın.** Bu adla (Codigames'in
> "Idle Restaurant Tycoon - Empire") çok indirilen bir oyun zaten var.
> Aynı veya çok benzer ad marka ihlali/taklit şikâyetine ve düşük ASO'ya
> yol açar. Proje içindeki çalışma adı olarak kalabilir.

Önerilerin hepsi "ayırt edici marka kelimesi + aranan anahtar kelime"
kalıbında: marka kısmı sizi benzersiz yapar, "Idle / Restaurant / Tycoon /
Chef / Food" kısmı aramalarda eşleşir.

| Başlık | Karakter | Not |
|--------|:-------:|-----|
| Chef Street: Idle Restaurant | 28 | En dengeli; iki güçlü anahtar kelime |
| Grill Town: Idle Food Tycoon | 28 | "food tycoon" aramaları için |
| Food Court Tycoon: Idle Chef | 28 | Üç anahtar kelime, doğal okunuyor |
| Burger Lane: Idle Cafe Empire | 29 | "cafe", "empire" aramaları için |
| Tiny Kitchen: Idle Tycoon | 25 | Kısa, marka ağırlıklı |
| Idle Diner: Chef Tycoon | 23 | En kısa; küçük ekranda tam görünür |

Türkçe mağaza sayfası için (yerelleştirilmiş başlık):

| Başlık | Karakter |
|--------|:-------:|
| Lezzet Sokağı: Restoran Oyunu | 29 |
| Mutfak Patronu: Idle Restoran | 29 |
| Lezzet Durağı: Restoran Kur | 27 |

Seçmeden önce:

- Play Store'da ve marka veri tabanlarında (TÜRKPATENT, EUIPO, USPTO) adı aratın.
- Başlıkta emoji, TAMAMI BÜYÜK HARF, "#1", "en iyi", "ücretsiz", "yeni" gibi
  ifadeler kullanmayın; Google Play meta veri politikası bunları yasaklar.

---

## 2. Kısa açıklama (80 karakter altı)

| Dil | Metin | Karakter |
|-----|-------|:-------:|
| EN | Upgrade your kitchen, serve hungry customers and earn even while you're away. | 77 |
| EN (alternatif) | Build a food empire: upgrade stations, serve customers and earn offline! | 72 |
| TR | Mutfağını geliştir, müşterilere hizmet et, oyunda değilken bile kazan! | 70 |
| TR (alternatif) | Restoranını büyüt, istasyonları geliştir, çevrimdışıyken bile para kazan! | 73 |

---

## 3. Uzun açıklama

### English (1.692 karakter)

```text
Start with a single cooking station and grow it into a busy restaurant that keeps cooking even when you put your phone down.

Upgrade your kitchen stations, unlock new ones and watch your income climb from a few coins to millions, billions and beyond. Customers walk in, take a seat at your tables and pay as soon as their order is ready, so every upgrade makes your restaurant feel faster and livelier.

HOW IT WORKS
• Tap to upgrade stations. Every level raises what a station earns each cycle.
• Unlock new stations to serve more customers and multiply your income.
• Your kitchen keeps working while you are away. Come back to collect your offline earnings.
• Complete an endless chain of goals: earn money, upgrade stations and reset your restaurant for bonus rewards.

PRESTIGE AND PERMANENT BOOSTS
When progress slows down, reset your restaurant to earn Gems. Spend Gems on permanent boosts that survive every reset:
• Faster production for every station
• Higher income from every order
Each run is quicker than the last, so you always have a reason to push a little further.

PLAY YOUR WAY
• Relaxing idle gameplay: short sessions or long ones, your restaurant grows either way.
• Optional rewarded ads: double your offline earnings or speed up all stations for a short time. Watching ads is never required to progress.
• Designed for one-handed play in portrait mode.
• Satisfying sounds and haptic feedback, with separate on/off switches in settings.
• No account and no sign-up. The game runs without an internet connection; ads need a connection.

Big numbers, steady progress and a kitchen that never sleeps. Open your restaurant today and see how far your food empire can grow!
```

### Türkçe (1.754 karakter)

```text
Tek bir mutfak istasyonuyla başla ve onu, telefonunu bıraktığında bile yemek pişirmeye devam eden kalabalık bir restorana dönüştür.

Mutfak istasyonlarını geliştir, yenilerini aç ve gelirinin birkaç bozukluktan milyonlara, milyarlara ve ötesine tırmanışını izle. Müşteriler kapıdan girer, masalarına oturur ve siparişleri hazır olur olmaz ödemesini yapar; her geliştirme restoranını daha hızlı ve daha canlı hissettirir.

NASIL OYNANIR
• İstasyonları geliştirmek için dokun. Her seviye, istasyonun her döngüde kazandığını artırır.
• Daha çok müşteriye hizmet etmek ve gelirini katlamak için yeni istasyonlar aç.
• Sen yokken de mutfağın çalışır. Geri döndüğünde çevrimdışı kazancını topla.
• Bitmeyen görev zincirini tamamla: para kazan, istasyonları geliştir, ödüller için restoranını sıfırla.

PRESTİJ VE KALICI GÜÇLENDİRMELER
İlerleme yavaşladığında restoranını sıfırla ve Gem kazan. Gem'leri her sıfırlamada korunan kalıcı güçlendirmelere harca:
• Tüm istasyonlarda daha hızlı üretim
• Her siparişten daha yüksek gelir
Her tur bir öncekinden hızlı geçer; biraz daha ileri gitmek için hep bir nedenin olur.

İSTEDİĞİN GİBİ OYNA
• Rahatlatıcı idle oynanış: kısa ya da uzun oturumlar, restoranın her durumda büyür.
• İsteğe bağlı ödüllü reklamlar: çevrimdışı kazancını ikiye katla veya kısa süreliğine tüm istasyonları hızlandır. İlerlemek için reklam izlemek asla zorunlu değildir.
• Dikey ekranda tek elle oynamak için tasarlandı.
• Keyifli sesler ve dokunsal titreşim; ayarlardan ayrı ayrı açılıp kapatılabilir.
• Hesap yok, kayıt yok. Oyun internet bağlantısı olmadan çalışır; reklamlar bağlantı gerektirir.

Büyük sayılar, istikrarlı ilerleme ve hiç uyumayan bir mutfak. Restoranını bugün aç ve yemek imparatorluğunun ne kadar büyüyebileceğini gör!
```

Açıklama hakkında notlar:

- Anahtar kelimeler (idle, restaurant, tycoon, kitchen, chef, food, upgrade,
  offline, prestige) cümle içinde doğal geçiyor. Açıklamanın sonuna anahtar
  kelime listesi eklemeyin; Google bunu "keyword stuffing" sayar.
- Açıklamada oyuncu yorumu, sıralama veya fiyat iddiası kullanmayın.
- Oyunun varsayılan ayarları değişirse (ör. hızlandırıcının süresi) metni
  güncelleyin; bu sürüm bilerek sayı vermiyor.

---

## 4. Veri güvenliği (Data safety) formu — AdMob özeti

Uygulamanın **kendisi** cihaz dışına veri göndermez: hesap, sunucu ve
analiz aracı yok; oyun kaydı yalnızca cihazda. Google'ın tanımına göre
yalnızca cihazda işlenen veri "toplanan veri" sayılmaz. Bu yüzden formda
beyan edilecek verilerin tamamı **Google Mobile Ads SDK (AdMob)** kaynaklıdır.

> Aşağıdaki özet, Google'ın AdMob için yayımladığı Play veri açıklaması
> rehberine dayanır. Google bu rehberi SDK sürümleriyle günceller; formu
> doldurmadan önce güncel sayfayı kontrol edin:
> https://developers.google.com/admob/android/privacy/play-data-disclosure

### 4.1 Genel sorular

| Soru | Cevap |
|------|-------|
| Uygulamanız zorunlu kullanıcı veri türlerinden herhangi birini topluyor veya paylaşıyor mu? | **Evet** (AdMob nedeniyle) |
| Toplanan tüm kullanıcı verileri aktarım sırasında şifreleniyor mu? | **Evet** (SDK HTTPS kullanır) |
| Kullanıcıların verilerinin silinmesini isteyebilecekleri bir yol sunuyor musunuz? | Hesap sistemi olmadığı için genellikle **Hayır**. Oyun içi "İlerlemeyi Sıfırla" yalnızca cihazdaki kaydı siler; reklam verileri Google'a aittir. Durumunuza göre karar verin. |

### 4.2 İşaretlenecek veri türleri

Her satır için: **Toplanıyor: Evet · Paylaşılıyor: Evet · Geçici işleme: Hayır ·
Zorunlu (kullanıcı kapatamaz)**.

| Kategori → Veri türü | Kaynağı | Amaçlar |
|----------------------|---------|---------|
| Konum → **Yaklaşık konum** | IP adresinden tahmin | Reklamcılık veya pazarlama · Analiz · Sahtekârlığı önleme, güvenlik ve uyumluluk |
| Uygulama etkinliği → **Uygulama etkileşimleri** | Uygulama açılışı, reklam görüntüleme/dokunma | Reklamcılık veya pazarlama · Analiz · Sahtekârlığı önleme, güvenlik ve uyumluluk |
| Uygulama bilgileri ve performansı → **Kilitlenme günlükleri** | SDK tanılama verisi | Analiz · Sahtekârlığı önleme, güvenlik ve uyumluluk |
| Uygulama bilgileri ve performansı → **Teşhis** | SDK performans verisi | Analiz · Sahtekârlığı önleme, güvenlik ve uyumluluk |
| Cihaz veya diğer kimlikler → **Cihaz veya diğer kimlikler** | Reklam kimliği, uygulama grubu kimliği | Reklamcılık veya pazarlama · Analiz · Sahtekârlığı önleme, güvenlik ve uyumluluk |

**İşaretlenmeyecekler:** kişisel bilgiler (ad, e-posta, telefon), finansal
bilgiler, sağlık, mesajlar, fotoğraf/video, ses, dosyalar, takvim, rehber,
hassas konum, web geçmişi. Uygulama bunların hiçbirine erişmez.

### 4.3 Bu cevapları değiştiren durumlar

- **Analiz/çökme raporlama SDK'sı eklerseniz** (Firebase Analytics,
  Crashlytics, Unity Analytics), onun veri açıklamasını da ekleyin.
- **Uygulama içi satın alma eklerseniz**, "Finansal bilgiler → Satın alma
  geçmişi" gerekebilir.
- **Hesap/giriş eklerseniz**, veri silme bağlantısı zorunlu hale gelir.

---

## 5. Diğer "Uygulama içeriği" beyanları

| Beyan | Önerilen cevap |
|-------|----------------|
| Reklamlar | **Evet, uygulamam reklam içeriyor** |
| Reklam kimliği | **Evet**; amaçlar: Reklamcılık veya pazarlama, Analiz, Sahtekârlığı önleme. Google Mobile Ads SDK `com.google.android.gms.permission.AD_ID` iznini manifest'e kendisi ekler. |
| Uygulama erişimi | Tüm işlevler giriş yapmadan kullanılabilir |
| Hedef kitle | **13 yaş ve üzeri** önerilir. 13 yaş altını seçmek Aileler Politikası'nı devreye sokar (çocuklara yönelik reklam ayarları, kişiselleştirilmiş reklam yasağı); mevcut AdMob kurulumu buna göre yapılandırılmadı. |
| İçerik derecelendirmesi | IARC anketi: şiddet, kumar, sohbet yok; kullanıcılar arası etkileşim yok. Oyun içi para gerçek parayla satın alınmıyor. |
| Kategori | Oyun → **Simülasyon** (alternatif: Gündelik) |
| Gizlilik politikası | `PRIVACY_POLICY.md`'nin yayındaki adresi (Bölüm 7) |

---

## 6. Yayın öncesi kontrol listesi

**Engelleyici (bunlar olmadan yayınlamayın):**

- [ ] **AB/AEA/Birleşik Krallık rızası (UMP).** Google, bu bölgelerde reklam
      göstermeden önce Google onaylı bir rıza platformuyla rıza alınmasını
      zorunlu tutar. Mevcut `AdMobAdService` rıza akışı içermiyor; Google
      Mobile Ads Unity eklentisindeki User Messaging Platform
      (`GoogleMobileAds.Ump`) ile rıza formu ve Ayarlar'a "Gizlilik
      seçenekleri" girişi eklenmeli, AdMob konsolunda "Gizlilik ve
      mesajlaşma" bölümünden GDPR mesajı oluşturulmalı.
      `PRIVACY_POLICY.md` Bölüm 5 bu entegrasyonu varsayar.
- [ ] **Gerçek reklam kimlikleri.** `AdManager`'daki Android/iOS ödüllü reklam
      birimleri Google'ın test kimlikleri. Kendi birimlerinizle değiştirin;
      AdMob App ID'yi Assets → Google Mobile Ads → Settings'e girin. Geliştirme
      sırasında kendi reklamlarınıza tıklamayın (test cihazı tanımlayın).
- [ ] **`ADMOB_ENABLED`** Android için Scripting Define Symbols'ta tanımlı
      olmalı; yoksa sürüm derlemesi mock reklam gösterir.
- [ ] **Gizlilik politikası adresi.** `PRIVACY_POLICY.md`'deki köşeli parantezli
      alanları doldurun, herkese açık bir adreste yayınlayın; aynı adresi Play
      Console'a ve `SettingsPanelUI.privacyPolicyUrl` alanına girin.
- [ ] **Hedef API seviyesi.** Google Play her yıl yeni uygulamalar ve
      güncellemeler için hedef API seviyesini yükseltir; yüklemeden önce Play
      Console'daki güncel gereksinimi kontrol edin. Unity 2021.3 LTS bu
      gereksinimi ve 16 KB bellek sayfası uyumluluğunu karşılamayabilir; sürüm
      derlemesini Unity 6 LTS veya 2022.3 LTS'in son sürümüyle alın. Kod,
      Unity 2023.1+ API'leri için ayrı dallar içeriyor (`UNITY_2023_1_OR_NEWER`);
      seçtiğiniz sürümde bir kez derleyip uyarıları kontrol edin.

**Gerekli:**

- [ ] Android App Bundle (**.aab**), **IL2CPP**, **ARM64** açık (Player Settings).
- [ ] Nihai manifest'te `android.permission.VIBRATE` ve `AD_ID` izinleri var.
      Derlenen .aab/.apk'yı Android Studio → Build → Analyze APK ile açıp
      AndroidManifest.xml'e bakın.
- [ ] `iOS` derlemesi yapılacaksa `IdleHaptics.mm` yalnızca iOS için işaretli.
- [ ] Sahne File → Build Settings'e ekli (İlerlemeyi Sıfırla sahneyi yeniden yükler).
- [ ] Geliştirici web sitesine **app-ads.txt** (AdMob → Uygulamalar → app-ads.txt)
      ve bu sitenin Play Console'daki "Web sitesi" alanına girilmesi.
- [ ] Önce **Dahili test** kanalında deneyin: reklam yükleme, çevrimdışı kazanç,
      ayarlar paneli, titreşim, İlerlemeyi Sıfırla.
- [ ] Mağaza görselleri: 512×512 simge, 1024×500 öne çıkan grafik, en az 2
      dikey ekran görüntüsü (oyunun gerçek görüntüleri).

---

## 7. Gizlilik politikasını yayınlama (GitHub Pages ile)

Play Console herkese açık, giriş istemeyen, PDF olmayan bir web sayfası ister.
Depo herkese açıksa en kısa yol:

1. Depoda Settings → Pages → "Deploy from a branch" → yayın dalını ve kök
   klasörü seçin.
2. Birkaç dakika sonra politika şu biçimde bir adreste yayında olur:
   `https://<kullanıcı-adı>.github.io/<depo-adı>/PRIVACY_POLICY`
3. Adresi tarayıcıda gizli pencerede açıp giriş istemediğini doğrulayın.
4. Aynı adresi Play Console → Uygulama içeriği → Gizlilik politikası alanına
   ve Unity'de `SettingsPanelUI` → **Privacy Policy Url** alanına girin.

Depo gizliyse politikayı ayrı, herkese açık bir depoda veya kendi alan
adınızda yayınlayın.
