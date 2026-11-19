import 'dart:async';

import 'products.dart';

class StoreProduct {
  const StoreProduct({
    required this.id,
    required this.price,
    this.isSubscription = false,
  });

  final String id;

  /// Magazanin yerellestirdigi fiyat metni ("₺49,99").
  final String price;
  final bool isSubscription;
}

enum PurchaseEventKind { granted, restored, pending, cancelled, error }

class PurchaseEvent {
  const PurchaseEvent(this.kind, [this.productId]);

  final PurchaseEventKind kind;
  final String? productId;
}

/// Magaza soyutlamasi. Haklar magazadan gelir; oyun yalnizca olaylari dinler.
abstract class PurchaseService {
  Future<void> init();

  bool get available;

  Map<String, StoreProduct> get products;

  Stream<PurchaseEvent> get events;

  Future<void> buy(String productId);

  Future<void> restore();

  /// Sahip olunan kalici urunler ve aktif abonelikler.
  /// Ogrenilemezse (cevrimdisi, sahte magaza) null: onbellekteki haklar korunur.
  Future<Set<String>?> ownedProductIds();

  void dispose();
}

/// Play Console hesabi acilmadan once denemek icin sahte magaza.
class FakePurchaseService implements PurchaseService {
  FakePurchaseService({this.delay = const Duration(milliseconds: 500)});

  final Duration delay;
  final _events = StreamController<PurchaseEvent>.broadcast();
  final Set<String> _owned = {};

  static const _prices = {
    ProductIds.coinsSmall: '₺39,99',
    ProductIds.coinsMedium: '₺99,99',
    ProductIds.coinsLarge: '₺199,99',
    ProductIds.removeAds: '₺119,99',
    ProductIds.starterPack: '₺79,99',
    ProductIds.casePack1: '₺59,99',
    ProductIds.clubWeekly: '₺49,99',
    ProductIds.clubYearly: '₺799,99',
  };

  @override
  Future<void> init() async {}

  @override
  bool get available => true;

  @override
  Map<String, StoreProduct> get products => {
    for (final e in _prices.entries)
      e.key: StoreProduct(
        id: e.key,
        price: e.value,
        isSubscription: ProductIds.subscriptions.contains(e.key),
      ),
  };

  @override
  Stream<PurchaseEvent> get events => _events.stream;

  @override
  Future<void> buy(String productId) async {
    await Future<void>.delayed(delay);
    if (!ProductIds.isConsumable(productId)) _owned.add(productId);
    _events.add(PurchaseEvent(PurchaseEventKind.granted, productId));
  }

  @override
  Future<void> restore() async {
    for (final id in _owned) {
      _events.add(PurchaseEvent(PurchaseEventKind.restored, id));
    }
  }

  @override
  Future<Set<String>?> ownedProductIds() async => null;

  @override
  void dispose() => _events.close();
}
