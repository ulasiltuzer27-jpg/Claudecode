import 'dart:async';

import 'package:cupertino_ui/cupertino_ui.dart' show CupertinoLocalizations, DefaultCupertinoLocalizations;
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../ui/screens/home_screen.dart';
import 'game_state.dart';
import 'strings.dart';
import 'theme.dart';

class DetectiveApp extends StatefulWidget {
  const DetectiveApp({super.key, required this.state});

  final GameState state;

  @override
  State<DetectiveApp> createState() => _DetectiveAppState();
}

class _DetectiveAppState extends State<DetectiveApp> {
  final _messenger = GlobalKey<ScaffoldMessengerState>();
  late final StreamSubscription<StoreNotice> _sub;

  @override
  void initState() {
    super.initState();
    _sub = widget.state.notices.listen(_showNotice);
  }

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }

  void _showNotice(StoreNotice notice) {
    final messenger = _messenger.currentState;
    final ctx = _messenger.currentContext;
    if (messenger == null || ctx == null) return;
    final s = S.of(ctx);
    final text = switch (notice) {
      StoreNotice.thanks => s.purchaseThanks,
      StoreNotice.restored => s.restored,
      StoreNotice.pending => s.purchasePending,
      StoreNotice.failed => s.purchaseFailed,
    };
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(text)));
  }

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider<GameState>.value(
      value: widget.state,
      child: Selector<GameState, String?>(
        selector: (_, g) => g.languageOverride,
        builder: (context, lang, _) => MaterialApp(
          title: 'Bağlantı Dedektifi',
          debugShowCheckedModeBanner: false,
          theme: buildTheme(),
          scaffoldMessengerKey: _messenger,
          locale: lang == null ? null : Locale(lang),
          supportedLocales: [for (final l in S.supported) Locale(l)],
          localeResolutionCallback: (locale, _) =>
              Locale(locale?.languageCode == 'tr' ? 'tr' : 'en'),
          localizationsDelegates: const [
            _AnyLocaleMaterialLocalizations(),
            _AnyLocaleWidgetsLocalizations(),
            _AnyLocaleCupertinoLocalizations(),
          ],
          home: const HomeScreen(),
        ),
      ),
    );
  }
}

// material_ui kendi MaterialLocalizations sinifini kullaniyor ve henuz Turkce
// ceviri paketi yok. Oyunun tum metinleri `S` icinde; Material'in kendi
// birkac ipucu metni (geri tusu vb.) Ingilizce kalir ama Turkce yerel ayarda
// "MaterialLocalizations bulunamadi" hatasi olusmaz.
class _AnyLocaleMaterialLocalizations extends LocalizationsDelegate<MaterialLocalizations> {
  const _AnyLocaleMaterialLocalizations();

  @override
  bool isSupported(Locale locale) => true;

  @override
  Future<MaterialLocalizations> load(Locale locale) => DefaultMaterialLocalizations.load(locale);

  @override
  bool shouldReload(_AnyLocaleMaterialLocalizations old) => false;
}

class _AnyLocaleWidgetsLocalizations extends LocalizationsDelegate<WidgetsLocalizations> {
  const _AnyLocaleWidgetsLocalizations();

  @override
  bool isSupported(Locale locale) => true;

  @override
  Future<WidgetsLocalizations> load(Locale locale) => DefaultWidgetsLocalizations.load(locale);

  @override
  bool shouldReload(_AnyLocaleWidgetsLocalizations old) => false;
}

class _AnyLocaleCupertinoLocalizations extends LocalizationsDelegate<CupertinoLocalizations> {
  const _AnyLocaleCupertinoLocalizations();

  @override
  bool isSupported(Locale locale) => true;

  @override
  Future<CupertinoLocalizations> load(Locale locale) => DefaultCupertinoLocalizations.load(locale);

  @override
  bool shouldReload(_AnyLocaleCupertinoLocalizations old) => false;
}

/// Ucuncu taraf varliklarin lisanslari "Lisanslar" sayfasinda gorunur.
void registerAssetLicenses() {
  LicenseRegistry.addLicense(() async* {
    yield LicenseEntryWithLineBreaks(
      const ['Fluent Emoji (Microsoft)'],
      await rootBundle.loadString('assets/licenses/fluent_emoji_MIT.txt'),
    );
    yield LicenseEntryWithLineBreaks(
      const ['Courier Prime'],
      await rootBundle.loadString('assets/licenses/courier_prime_OFL.txt'),
    );
    yield LicenseEntryWithLineBreaks(
      const ['Playfair Display'],
      await rootBundle.loadString('assets/licenses/playfair_display_OFL.txt'),
    );
  });
}
