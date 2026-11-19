import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import 'app/app.dart';
import 'app/game_state.dart';
import 'data/content_loader.dart';
import 'monetization/platform_services.dart';
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
  );

  runApp(DetectiveApp(state: state));

  // Reklam onay formu bir Activity ister; ilk kare cizildikten sonra baslat.
  WidgetsBinding.instance.addPostFrameCallback((_) => state.startServices());
}
