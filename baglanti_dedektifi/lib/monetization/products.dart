import 'package:flutter/foundation.dart';

/// Play Console'da acilacak urunlerin kimlikleri. Burada degisirse
/// Play Console'da da degismeli (docs/play_store.md).
abstract final class ProductIds {
  static const coinsSmall = 'coins_300';
  static const coinsMedium = 'coins_1000';
  static const coinsLarge = 'coins_2500';
  static const removeAds = 'remove_ads';
  static const starterPack = 'starter_pack';
  static const casePack1 = 'case_pack_1';
  static const clubWeekly = 'club_weekly';
  static const clubYearly = 'club_yearly';

  /// Tuketilebilir jeton paketleri -> jeton miktari.
  static const coinPacks = {
    coinsSmall: 300,
    coinsMedium: 1000,
    coinsLarge: 2500,
  };
  static const starterCoins = 1000;

  static const subscriptions = {clubWeekly, clubYearly};
  static const casePacks = {casePack1};
  static const nonConsumables = {removeAds, starterPack, casePack1};

  static Set<String> get all => {
    ...coinPacks.keys,
    ...nonConsumables,
    ...subscriptions,
  };

  static bool isConsumable(String id) => coinPacks.containsKey(id);
}

/// Derleme ayarlari. `--dart-define` ile verilir, varsayilanlar gelistirme icin.
abstract final class StoreConfig {
  /// `fake`: sahte magaza + sahte reklam (web, otomatik testler).
  /// `test`: sahte magaza + gercek AdMob test reklamlari (Play Console'dan
  ///         once telefonda denemek icin).
  /// `real`: Google Play Billing + AdMob.
  /// Bos: debug derlemede `test` gibi, release derlemede `real`.
  static const _store = String.fromEnvironment('STORE');

  static bool get fakeStore =>
      kIsWeb ||
      _store == 'fake' ||
      _store == 'test' ||
      (_store.isEmpty && kDebugMode);

  /// Web'de AdMob yok; diger yerlerde (test kimlikleriyle) gercek SDK.
  static bool get fakeAds => kIsWeb || _store == 'fake';

  /// Google'in herkese acik test reklam birimleri. Yayindan once
  /// `--dart-define=ADMOB_REWARDED_ID=...` ile gercekleri verilir.
  static const rewardedAdUnit = String.fromEnvironment(
    'ADMOB_REWARDED_ID',
    defaultValue: 'ca-app-pub-3940256099942544/5224354917',
  );
  static const interstitialAdUnit = String.fromEnvironment(
    'ADMOB_INTERSTITIAL_ID',
    defaultValue: 'ca-app-pub-3940256099942544/1033173712',
  );
}
