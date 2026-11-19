import 'package:material_ui/material_ui.dart';

/// Arayuz metinleri (TR/EN). Icerik metinleri JSON'dadir; bunlar yalnizca
/// ekran yazilari. Bilerek kod uretimi kullanilmiyor: iki dil, tek dosya.
class S {
  const S(this.lang);

  final String lang;

  static const supported = ['tr', 'en'];

  static S of(BuildContext context) =>
      S(Localizations.localeOf(context).languageCode == 'tr' ? 'tr' : 'en');

  String _(String tr, String en) => lang == 'tr' ? tr : en;

  // Genel
  String get appName => _('Bağlantı Dedektifi', 'Link Detective');
  String get tagline => _('Kanıtları bağla, gerçeği bul.', 'Connect the evidence. Find the truth.');
  String get loading => _('Yükleniyor...', 'Loading...');
  String get continueBtn => _('Devam', 'Continue');
  String get next => _('İleri', 'Next');
  String get close => _('Kapat', 'Close');
  String get quit => _('Çık', 'Quit');
  String coins(int n) => _('$n jeton', '$n coins');
  String plusCoins(int n) => _('+$n jeton', '+$n coins');
  String get notEnoughCoins => _('Yeterli jetonun yok.', 'Not enough coins.');
  String get adNotReady =>
      _('Şu an reklam yok, birazdan tekrar dene.', 'No ad available right now. Try again shortly.');

  // Ana ekran
  String get continueInvestigation => _('Soruşturmaya devam', 'Continue investigation');
  String get caseFiles => _('Dosyalar', 'Case files');
  String get daily => _('Günün Delili', 'Daily Evidence');
  String get dailyPlay => _('Bugünün panosunu çöz', "Solve today's board");
  String get dailyDone => _('Bugünün delili çözüldü. Yarın yenisi gelecek.',
      "Today's evidence is solved. A new one arrives tomorrow.");
  String streak(int n) => _('$n günlük seri', '$n-day streak');
  String get shop => _('Mağaza', 'Shop');
  String get settings => _('Ayarlar', 'Settings');
  String get allCasesClosed => _('Tüm dosyaları kapattın. Yeni dosyalar yolda!',
      'You have closed every case. New ones are on the way!');

  List<String> get rankNames => lang == 'tr'
      ? const ['Çaylak', 'Polis memuru', 'Komiser yardımcısı', 'Komiser', 'Başkomiser', 'Efsane dedektif']
      : const ['Rookie', 'Officer', 'Sergeant', 'Inspector', 'Chief Inspector', 'Legendary Detective'];
  String starsToNext(int n) => _('Sonraki rütbeye $n yıldız', '$n stars to the next rank');
  String get maxRank => _('En yüksek rütbedesin', 'You hold the highest rank');
  String totalStars(int n) => _('$n yıldız', '$n stars');

  // Dosyalar
  String caseN(int n) => _('Dosya $n', 'Case $n');
  String boardN(int n, int total) => _('Pano $n/$total', 'Board $n/$total');
  String boardsSolved(int n, int total) => _('$n/$total pano', '$n/$total boards');
  String get boards => _('Panolar', 'Boards');
  String get suspects => _('Şüpheliler', 'Suspects');
  String clues(int n, int total) => _('İpuçları ($n/$total)', 'Clues ($n/$total)');
  String get noCluesYet => _('Henüz ipucu yok. Panoları çözdükçe burada birikecek.',
      "No clues yet. They'll pile up here as you solve boards.");
  String get readStory => _('Hikâyeyi oku', 'Read the story');
  String get startInvestigation => _('Soruşturmaya başla', 'Start investigating');
  String get accuse => _('Suçla', 'Accuse');
  String get accuseLocked =>
      _('Suçlamak için önce tüm panoları çöz.', 'Solve every board before you accuse.');
  String get accuseReady =>
      _('Tüm ipuçları elinde. Suçluyu göster!', 'You have every clue. Name the culprit!');
  String get caseClosed => _('Dosya kapandı', 'Case closed');
  String get readEnding => _('Sonu yeniden oku', 'Read the ending again');
  String get lockedPrev =>
      _('Önceki dosyayı kapatınca açılır.', 'Opens once you close the previous case.');
  String get premiumLocked =>
      _('Premium dosya. Mağazadan açabilirsin.', 'Premium case. You can unlock it in the shop.');
  String get premium => _('Premium', 'Premium');

  // Pano
  String get link => _('Bağla', 'Link');
  String get clear => _('Temizle', 'Clear');
  String get shuffle => _('Karıştır', 'Shuffle');
  String get suspicion => _('Şüphe', 'Suspicion');
  String get groups => _('Gruplar', 'Groups');
  String fakePresent(int n) => n == 1
      ? _('Panoda 1 sahte delil var. Basılı tutup damgala.',
          "There's 1 fake clue on the board. Long-press to stamp it.")
      : _('Panoda $n sahte delil var. Basılı tutup damgala.',
          'There are $n fake clues on the board. Long-press to stamp them.');
  String get fakeStamp => _('SAHTE', 'FAKE');
  String fbCorrect(String theme) => _('Bağlantı bulundu: $theme', 'Connection found: $theme');
  String get fbOneAway => _('Çok yakın! Bir kart yanlış.', 'So close! One card is wrong.');
  String get fbWrong => _('Bu bağlantı tutmadı.', "That connection doesn't hold.");
  String get fbRepeated => _('Bunu zaten denedin.', 'You already tried that.');
  String get hint => _('Büyüteç', 'Magnifier');
  String hintFree(int n) => _('Ücretsiz ($n)', 'Free ($n)');
  String lead(String theme, int n) => _('İz: $theme ($n kart)', 'Lead: $theme ($n cards)');
  String get hintAdOffer => _('Yeterli jetonun yok. Kısa bir reklam izleyip büyüteci ücretsiz kullanmak ister misin?',
      "You don't have enough coins. Watch a short ad to use the magnifier for free?");
  String get watchAd => _('Reklam izle', 'Watch ad');
  String get noThanks => _('Hayır, teşekkürler', 'No thanks');
  String get noMoreHints => _('Bütün izler zaten açık.', 'Every lead is already revealed.');
  String get doubled => _('Jetonlar ikiye katlandı!', 'Coins doubled!');
  String get coldTitle => _('İz soğudu', 'The trail went cold');
  String get coldBody => _(
      'Çok fazla yanlış bağlantı kurdun. Devam etmek için şüpheni azalt ya da baştan başla.',
      'Too many wrong connections. Lower your suspicion to continue, or start over.');
  String get watchAdContinue => _('Reklam izle, devam et', 'Watch an ad to continue');
  String payContinue(int n) => _('$n jetonla devam et', 'Continue for $n coins');
  String get restart => _('Baştan başla', 'Start over');
  String get solvedTitle => _('Pano çözüldü!', 'Board solved!');
  String get dailySolvedTitle => _('Günün Delili çözüldü!', 'Daily Evidence solved!');
  String get newClue => _('Yeni ipucu', 'New clue');
  String keenEye(int n) =>
      _('Keskin göz! Sahte delilleri buldun: +$n', 'Keen eye! You spotted the fakes: +$n');
  String get doubleCoins => _('Reklam izle: jetonları ikiye katla', 'Watch an ad: double your coins');
  String get replayNote => _('Tekrar oynanan panolar daha az jeton verir.',
      'Replayed boards pay fewer coins.');
  String get howToPlayTitle => _('Nasıl oynanır?', 'How to play');
  String get howToPlay1 => _('Ortak bir noktası olan kartlara dokun. Kırmızı ip onları birbirine bağlar.',
      'Tap cards that share something in common. Red thread ties them together.');
  String get howToPlay2 => _('Bir grubun tüm kartlarını seçince Bağla\'ya bas. Üstteki sayılar grupların kaç kartlık olduğunu gösterir.',
      'Once you have picked a whole group, press Link. The numbers at the top show how many cards each group has.');
  String get howToPlay3 => _('Yanlış bağlantı şüpheni artırır. Takılırsan büyüteci kullan.',
      'Wrong links raise your suspicion. If you get stuck, use the magnifier.');
  String get gotIt => _('Anladım', 'Got it');

  // Suclama
  String get whoDidIt => _('Suçlu kim?', 'Who did it?');
  String get pickTwo => _('Bunu kanıtlayan iki ipucu seç', 'Pick two clues that prove it');
  String get accuseNow => _('Suçla!', 'Make the accusation!');
  String get accCorrectTitle => _('Dava çözüldü!', 'Case solved!');
  String get accWeakTitle => _('Kanıtlar zayıf', 'Weak evidence');
  String get accWeakBody => _(
      'Doğru kişiyi bulmuş olabilirsin ama bu ipuçları onu kanıtlamıyor. Başka kanıtlar seç.',
      "You may have the right person, but these clues don't prove it. Choose different evidence.");
  String get accWrongTitle => _('Yanlış kişi', 'Wrong suspect');
  String get accWrongBody => _('Bu kişinin sağlam bir mazereti olabilir. İpuçlarını yeniden oku.',
      'This person may have a solid alibi. Read your clues again.');
  String get tryAgain => _('Tekrar dene', 'Try again');
  String caseStars(int n) => _('Dava yıldızı: $n/3', 'Case stars: $n/3');
  String get closeFile => _('Dosyayı kapat', 'Close the file');
  String get toBeContinued => _('Devam edecek...', 'To be continued...');

  // Magaza
  String get clubTitle => _('Dedektif Kulübü', 'Detective Club');
  String get clubPerk1 => _('Reklamsız oyun', 'No ads');
  String get clubPerk2 => _('Her gün 5 ücretsiz büyüteç', '5 free magnifiers every day');
  String get clubPerk3 => _('Tüm premium dosyalar', 'Every premium case');
  String clubWeekly(String price) => _('Haftalık · $price', 'Weekly · $price');
  String clubYearly(String price) => _('Yıllık · $price', 'Yearly · $price');
  String get clubActive => _('Kulüp üyesisin', "You're a Club member");
  String get clubPromo => _('Reklamsız oyna, her gün ücretsiz büyüteç al.',
      'Play without ads and get free magnifiers every day.');
  String get clubCancelNote => _('Abonelik, iptal edilene kadar otomatik yenilenir. Google Play > Abonelikler bölümünden istediğin zaman iptal edebilirsin.',
      'The subscription renews automatically until cancelled. Cancel any time in Google Play > Subscriptions.');
  String get removeAdsTitle => _('Reklamları kaldır', 'Remove ads');
  String get removeAdsBody => _('Geçiş reklamları bir daha çıkmaz. Ödüllü reklamlar isteğe bağlı kalır.',
      'Interstitial ads never show again. Rewarded ads stay optional.');
  String get starterTitle => _('Başlangıç paketi', 'Starter pack');
  String get starterBody => _('1000 jeton + reklamsız oyun, tek seferlik fırsat.',
      '1,000 coins + no ads. A one-time offer.');
  String get casePackTitle => _('Premium dosya paketi', 'Premium case pack');
  String casePackBody(String titles) =>
      _('Kalıcı olarak açar: $titles', 'Unlocks for good: $titles');
  String get coinPacks => _('Jeton paketleri', 'Coin packs');
  String freeCoins(int n) => _('Reklam izle: +$n jeton', 'Watch an ad: +$n coins');
  String freeCoinsLeft(int n) => _('Bugün $n hakkın kaldı', '$n left today');
  String get restore => _('Satın alımları geri yükle', 'Restore purchases');
  String get owned => _('Sahipsin', 'Owned');
  String get testStore => _('Test modu: satın almalar sahte, para çekilmez.',
      "Test mode: purchases are simulated and you won't be charged.");
  String get storeUnavailable => _('Mağazaya şu an ulaşılamıyor.', 'The store is unavailable right now.');
  String get purchaseThanks => _('Teşekkürler! Satın alma tamamlandı.', 'Thank you! Purchase complete.');
  String get purchaseFailed => _('Satın alma tamamlanamadı.', "The purchase couldn't be completed.");
  String get purchasePending => _('Ödeme onay bekliyor.', 'Your payment is pending.');
  String get restored => _('Satın alımlar geri yüklendi.', 'Purchases restored.');

  // Ayarlar
  String get language => _('Dil', 'Language');
  String get systemLanguage => _('Cihaz dili', 'Device language');
  String get haptics => _('Titreşim', 'Haptics');
  String get privacyOptions => _('Reklam gizlilik tercihleri', 'Ad privacy choices');
  String get privacyPolicy => _('Gizlilik politikası', 'Privacy policy');
  String get licenses => _('Lisanslar', 'Licenses');
  String get privacyText => _(
      'Bağlantı Dedektifi hesap açtırmaz ve senden ad, e-posta ya da konum istemez. '
      'Oyun ilerlemen yalnızca bu cihazda saklanır.\n\n'
      'Reklamlar Google AdMob tarafından gösterilir. AdMob, reklamları göstermek ve ölçmek için '
      'cihazının reklam kimliğini ve temel cihaz bilgilerini kullanabilir. Avrupa Ekonomik Alanı ve '
      'Birleşik Krallık\'ta reklam izni ilk açılışta sorulur; bu tercihi Ayarlar > Reklam gizlilik '
      'tercihleri bölümünden değiştirebilirsin.\n\n'
      'Satın almalar Google Play üzerinden yapılır. Ödeme bilgilerin bize ulaşmaz.\n\n'
      'Oyun 18 yaşından küçükler için tasarlanmamıştır.',
      'Link Detective does not create accounts and never asks for your name, email or location. '
      'Your game progress is stored only on this device.\n\n'
      'Ads are served by Google AdMob. AdMob may use your device\'s advertising ID and basic device '
      'information to show and measure ads. In the European Economic Area and the UK you are asked '
      'for ad consent on first launch, and you can change that choice in Settings > Ad privacy choices.\n\n'
      'Purchases are handled by Google Play. Your payment details never reach us.\n\n'
      'The game is not designed for anyone under 18.');
}
