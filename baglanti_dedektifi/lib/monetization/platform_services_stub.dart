import 'ads_service.dart';
import 'purchase_service.dart';

AdsService createPlatformAds() => FakeAdsService();

PurchaseService createPlatformPurchases() => FakePurchaseService();
