/// Reklam soyutlamasi. Oyun mantigi yalnizca bunu bilir; AdMob ayrintilari
/// `platform_services_mobile.dart` icindedir.
abstract class AdsService {
  /// Onay formu (gerekiyorsa) + SDK baslatma + ilk reklamlari yukleme.
  Future<void> init();

  bool get rewardedReady;

  /// Odullu reklam gosterir. Oyuncu odulu kazandiysa true.
  Future<bool> showRewarded();

  /// Gecis reklami gosterir (hazir degilse sessizce gecer).
  Future<void> showInterstitial();

  /// AB/BK gibi yerlerde ayarlarda "gizlilik tercihleri" dugmesi zorunlu.
  bool get privacyOptionsRequired;

  Future<void> showPrivacyOptions();
}

/// Web, testler ve `STORE=fake` icin: reklam yerine kisa bir bekleme.
class FakeAdsService implements AdsService {
  FakeAdsService({
    this.delay = const Duration(milliseconds: 700),
    this.alwaysReady = true,
  });

  final Duration delay;
  final bool alwaysReady;
  int rewardedShown = 0;
  int interstitialsShown = 0;

  @override
  Future<void> init() async {}

  @override
  bool get rewardedReady => alwaysReady;

  @override
  Future<bool> showRewarded() async {
    if (!alwaysReady) return false;
    await Future<void>.delayed(delay);
    rewardedShown++;
    return true;
  }

  @override
  Future<void> showInterstitial() async {
    interstitialsShown++;
  }

  @override
  bool get privacyOptionsRequired => false;

  @override
  Future<void> showPrivacyOptions() async {}
}
