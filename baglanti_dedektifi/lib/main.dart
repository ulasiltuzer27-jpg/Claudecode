import 'dart:ui' show PlatformDispatcher;

import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import 'app/app.dart';
import 'app/game_state.dart';
import 'audio/sound_service.dart';
import 'data/content_loader.dart';
import 'monetization/platform_services.dart';
import 'notify/platform_reminders.dart';
import 'progress/progress_store.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  registerAssetLicenses();

  final store = PrefsProgressStore();
  final progress = await store.load();
  final content = await loadContent(rootBundle.loadString);

  final state = GameState(
    content: content,
    progress: progress,
    store: store,
    ads: createPlatformAds(),
    purchases: createPlatformPurchases(),
    sound: AudioplayersSoundService(),
    reminders: createPlatformReminders(),
    deviceLanguage: PlatformDispatcher.instance.locale.languageCode == 'tr'
        ? 'tr'
        : 'en',
  );

  runApp(DetectiveApp(state: state));

  // Reklam onay formu bir Activity ister; ilk kare cizildikten sonra baslat.
  WidgetsBinding.instance.addPostFrameCallback((_) => state.startServices());
}
