// Web derlemesi AdMob ve Play Billing eklentilerini hic ice aktarmasin diye
// kosullu ice aktarma: dart:io olan platformlarda (Android) gercek servisler.
export 'platform_services_stub.dart'
    if (dart.library.io) 'platform_services_mobile.dart';
