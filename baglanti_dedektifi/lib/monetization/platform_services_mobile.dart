import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:google_mobile_ads/google_mobile_ads.dart';
import 'package:in_app_purchase/in_app_purchase.dart';
import 'package:in_app_purchase_android/in_app_purchase_android.dart';

import 'ads_service.dart';
import 'products.dart';
import 'purchase_service.dart';

AdsService createPlatformAds() => StoreConfig.fakeAds ? FakeAdsService() : MobileAdsService();

PurchaseService createPlatformPurchases() =>
    StoreConfig.fakeStore ? FakePurchaseService() : PlayPurchaseService();

/// Google AdMob + UMP onay formu.
class MobileAdsService implements AdsService {
  RewardedAd? _rewarded;
  InterstitialAd? _interstitial;
  bool _canRequestAds = false;
  bool _privacyRequired = false;
  bool _loadingRewarded = false;
  bool _loadingInterstitial = false;
  int _rewardedFailures = 0;
  int _interstitialFailures = 0;

  @override
  Future<void> init() async {
    final consentDone = Completer<void>();
    void finish() {
      if (!consentDone.isCompleted) consentDone.complete();
    }

    ConsentInformation.instance.requestConsentInfoUpdate(
      ConsentRequestParameters(),
      () async {
        await ConsentForm.loadAndShowConsentFormIfRequired((_) => finish());
        finish();
      },
      (_) => finish(),
    );
    await consentDone.future.timeout(const Duration(seconds: 20), onTimeout: () {});

    _canRequestAds = await ConsentInformation.instance.canRequestAds();
    _privacyRequired = await ConsentInformation.instance.getPrivacyOptionsRequirementStatus() ==
        PrivacyOptionsRequirementStatus.required;
    if (!_canRequestAds) return;

    await MobileAds.instance.initialize();
    _loadRewarded();
    _loadInterstitial();
  }

  Duration _backoff(int failures) => Duration(seconds: min(120, 5 * (1 << min(failures, 5))));

  void _loadRewarded() {
    if (!_canRequestAds || _loadingRewarded || _rewarded != null) return;
    _loadingRewarded = true;
    RewardedAd.load(
      adUnitId: StoreConfig.rewardedAdUnit,
      request: const AdRequest(),
      rewardedAdLoadCallback: RewardedAdLoadCallback(
        onAdLoaded: (ad) {
          _loadingRewarded = false;
          _rewardedFailures = 0;
          _rewarded = ad;
        },
        onAdFailedToLoad: (error) {
          _loadingRewarded = false;
          debugPrint('Odullu reklam yuklenemedi: $error');
          Future<void>.delayed(_backoff(_rewardedFailures++), _loadRewarded);
        },
      ),
    );
  }

  void _loadInterstitial() {
    if (!_canRequestAds || _loadingInterstitial || _interstitial != null) return;
    _loadingInterstitial = true;
    InterstitialAd.load(
      adUnitId: StoreConfig.interstitialAdUnit,
      request: const AdRequest(),
      adLoadCallback: InterstitialAdLoadCallback(
        onAdLoaded: (ad) {
          _loadingInterstitial = false;
          _interstitialFailures = 0;
          _interstitial = ad;
        },
        onAdFailedToLoad: (error) {
          _loadingInterstitial = false;
          debugPrint('Gecis reklami yuklenemedi: $error');
          Future<void>.delayed(_backoff(_interstitialFailures++), _loadInterstitial);
        },
      ),
    );
  }

  @override
  bool get rewardedReady => _rewarded != null;

  @override
  Future<bool> showRewarded() async {
    final ad = _rewarded;
    if (ad == null) {
      _loadRewarded();
      return false;
    }
    _rewarded = null;
    final done = Completer<bool>();
    var earned = false;
    ad.fullScreenContentCallback = FullScreenContentCallback(
      onAdDismissedFullScreenContent: (a) {
        a.dispose();
        if (!done.isCompleted) done.complete(earned);
        _loadRewarded();
      },
      onAdFailedToShowFullScreenContent: (a, error) {
        a.dispose();
        if (!done.isCompleted) done.complete(false);
        _loadRewarded();
      },
    );
    await ad.show(onUserEarnedReward: (_, _) => earned = true);
    return done.future;
  }

  @override
  Future<void> showInterstitial() async {
    final ad = _interstitial;
    if (ad == null) {
      _loadInterstitial();
      return;
    }
    _interstitial = null;
    final done = Completer<void>();
    ad.fullScreenContentCallback = FullScreenContentCallback(
      onAdDismissedFullScreenContent: (a) {
        a.dispose();
        if (!done.isCompleted) done.complete();
        _loadInterstitial();
      },
      onAdFailedToShowFullScreenContent: (a, error) {
        a.dispose();
        if (!done.isCompleted) done.complete();
        _loadInterstitial();
      },
    );
    await ad.show();
    return done.future;
  }

  @override
  bool get privacyOptionsRequired => _privacyRequired;

  @override
  Future<void> showPrivacyOptions() async {
    final done = Completer<void>();
    await ConsentForm.showPrivacyOptionsForm((_) {
      if (!done.isCompleted) done.complete();
    });
    await done.future;
    _canRequestAds = await ConsentInformation.instance.canRequestAds();
  }
}

/// Google Play Billing.
///
/// Not: makbuzlar sunucuda dogrulanmiyor (ilk surum). Haklar yalnizca
/// cihazdaki Play istemcisinin bildirdigine gore veriliyor.
class PlayPurchaseService implements PurchaseService {
  final _iap = InAppPurchase.instance;
  final _events = StreamController<PurchaseEvent>.broadcast();
  final Map<String, ProductDetails> _details = {};
  final Set<String> _handled = {};
  StreamSubscription<List<PurchaseDetails>>? _sub;
  bool _available = false;

  @override
  Future<void> init() async {
    _sub = _iap.purchaseStream.listen(
      _onPurchases,
      onError: (Object _) => _events.add(const PurchaseEvent(PurchaseEventKind.error)),
    );
    _available = await _iap.isAvailable();
    if (!_available) return;
    final response = await _iap.queryProductDetails(ProductIds.all);
    for (final d in response.productDetails) {
      if (!_details.containsKey(d.id) || _isBasePlan(d)) _details[d.id] = d;
    }
    if (response.notFoundIDs.isNotEmpty) {
      debugPrint('Play Console\'da bulunamayan urunler: ${response.notFoundIDs}');
    }
  }

  /// Bir abonelik birden fazla teklif dondurebilir; temel plani tercih et.
  bool _isBasePlan(ProductDetails d) {
    if (d is! GooglePlayProductDetails) return true;
    final index = d.subscriptionIndex;
    final offers = d.productDetails.subscriptionOfferDetails;
    if (index == null || offers == null) return true;
    return offers[index].offerId == null;
  }

  @override
  bool get available => _available;

  @override
  Map<String, StoreProduct> get products => {
        for (final d in _details.values)
          d.id: StoreProduct(
            id: d.id,
            price: d.price,
            isSubscription: ProductIds.subscriptions.contains(d.id),
          ),
      };

  @override
  Stream<PurchaseEvent> get events => _events.stream;

  @override
  Future<void> buy(String productId) async {
    final details = _details[productId];
    if (details == null) {
      _events.add(PurchaseEvent(PurchaseEventKind.error, productId));
      return;
    }
    final param = PurchaseParam(productDetails: details);
    try {
      if (ProductIds.isConsumable(productId)) {
        await _iap.buyConsumable(purchaseParam: param);
      } else {
        await _iap.buyNonConsumable(purchaseParam: param);
      }
    } catch (e) {
      debugPrint('Satin alma baslatilamadi: $e');
      _events.add(PurchaseEvent(PurchaseEventKind.error, productId));
    }
  }

  Future<void> _onPurchases(List<PurchaseDetails> purchases) async {
    for (final p in purchases) {
      switch (p.status) {
        case PurchaseStatus.pending:
          _events.add(PurchaseEvent(PurchaseEventKind.pending, p.productID));
        case PurchaseStatus.purchased:
        case PurchaseStatus.restored:
          final key = p.purchaseID ?? '${p.productID}@${p.transactionDate}';
          if (_handled.add(key)) {
            _events.add(PurchaseEvent(
              p.status == PurchaseStatus.restored
                  ? PurchaseEventKind.restored
                  : PurchaseEventKind.granted,
              p.productID,
            ));
          }
        case PurchaseStatus.error:
          _events.add(PurchaseEvent(PurchaseEventKind.error, p.productID));
        case PurchaseStatus.canceled:
          _events.add(PurchaseEvent(PurchaseEventKind.cancelled, p.productID));
      }
      if (p.pendingCompletePurchase) await _iap.completePurchase(p);
    }
  }

  @override
  Future<void> restore() => _iap.restorePurchases();

  @override
  Future<Set<String>?> ownedProductIds() async {
    if (!_available) return null;
    try {
      final addition = _iap.getPlatformAddition<InAppPurchaseAndroidPlatformAddition>();
      final result = await addition.queryPastPurchases();
      if (result.error != null) return null;
      final owned = <String>{};
      for (final p in result.pastPurchases) {
        if (p.status == PurchaseStatus.purchased || p.status == PurchaseStatus.restored) {
          owned.add(p.productID);
        }
        // Onaylanmayan satin almalar 3 gun sonra Google tarafindan iade edilir.
        if (p.pendingCompletePurchase) await _iap.completePurchase(p);
      }
      return owned;
    } catch (e) {
      debugPrint('Gecmis satin almalar okunamadi: $e');
      return null;
    }
  }

  @override
  void dispose() {
    _sub?.cancel();
    _events.close();
  }
}
