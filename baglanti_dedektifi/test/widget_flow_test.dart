import 'dart:io';

import 'package:baglanti_dedektifi/app/app.dart';
import 'package:baglanti_dedektifi/app/game_state.dart';
import 'package:baglanti_dedektifi/audio/sound_service.dart';
import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/monetization/ads_service.dart';
import 'package:baglanti_dedektifi/monetization/products.dart';
import 'package:baglanti_dedektifi/monetization/purchase_service.dart';
import 'package:baglanti_dedektifi/progress/progress.dart';
import 'package:baglanti_dedektifi/progress/progress_store.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';

late GameContent content;

GameState makeState({String lang = 'tr', Progress? progress}) => GameState(
  content: content,
  progress: (progress ?? Progress(loginRewardDate: '2026-11-19'))
    ..settings.language = lang,
  store: MemoryProgressStore(),
  ads: FakeAdsService(delay: Duration.zero),
  purchases: FakePurchaseService(delay: Duration.zero),
  clock: () => DateTime(2026, 11, 19, 10),
);

Future<void> pumpApp(WidgetTester tester, GameState state) async {
  tester.view.physicalSize = const Size(412 * 3, 915 * 3);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  // "Animasyonlari azalt": yagmur durur, konfeti ve kart ucusu atlanir;
  // boylece pumpAndSettle sonsuz animasyonda takilmaz.
  tester.platformDispatcher.accessibilityFeaturesTestValue =
      const FakeAccessibilityFeatures(disableAnimations: true);
  addTearDown(tester.platformDispatcher.clearAccessibilityFeaturesTestValue);
  await tester.pumpWidget(DetectiveApp(state: state));
  await tester.pumpAndSettle();
}

String label(String id, String lang) => content.catalog[id].label.of(lang);

Future<void> tapCards(
  WidgetTester tester,
  Iterable<String> ids,
  String lang,
) async {
  for (final id in ids) {
    await tester.tap(find.text(label(id, lang)));
    await tester.pump();
  }
}

Future<void> step(WidgetTester tester, [int ms = 400]) async {
  for (var i = 0; i < ms ~/ 50; i++) {
    await tester.pump(const Duration(milliseconds: 50));
  }
}

void main() {
  setUpAll(() async {
    content = await loadContent((p) => File(p).readAsString());
  });

  testWidgets('ilk dava: hikaye -> ogretici -> pano cozulur -> ipucu acilir', (
    tester,
  ) async {
    final state = makeState();
    await pumpApp(tester, state);
    final c = content.cases.first;

    expect(find.text('Bağlantı Dedektifi'), findsOneWidget);
    await tester.tap(find.text('Soruşturmaya devam'));
    await tester.pumpAndSettle();

    // Giris hikayesi: sahneler + supheliler sayfasi.
    for (var i = 0; i < c.intro.length; i++) {
      await tester.tap(find.text('İleri'));
      await tester.pumpAndSettle();
    }
    expect(find.text('Şüpheliler'), findsOneWidget);
    await tester.tap(find.text('Soruşturmaya başla'));
    await tester.pumpAndSettle();
    expect(state.record(c).introSeen, isTrue);

    // Ilk pano dogrudan acilir; ogretici gosterilir.
    expect(find.text('Nasıl oynanır?'), findsOneWidget);
    await tester.tap(find.text('Anladım'));
    await tester.pumpAndSettle();
    expect(state.tutorialDone, isTrue);

    final board = c.boards.first;
    for (final g in board.groups) {
      await tapCards(tester, g.items, 'tr');
      await tester.tap(find.text('Bağla'));
      await tester.pump(const Duration(milliseconds: 600));
    }
    await tester.pumpAndSettle();

    expect(find.text('Pano çözüldü!'), findsOneWidget);
    expect(find.text(c.clue(board.clueId!).text.of('tr')), findsOneWidget);
    await tester.tap(find.text('Devam'));
    await tester.pumpAndSettle();

    expect(state.boardStars(board), 3);
    // Dosya merkezine donuldu; ipucu listede.
    expect(find.text('İpuçları (1/10)'), findsOneWidget);
  });

  testWidgets('yanlis bag: supheyi artirir, mesaj gosterir', (tester) async {
    final state = makeState(
      progress: Progress(tutorialDone: true, loginRewardDate: '2026-11-19'),
    );
    final c = content.cases.first;
    state.record(c).introSeen = true;
    await pumpApp(tester, state);
    await tester.tap(find.text('Soruşturmaya devam'));
    await tester.pumpAndSettle();

    final g = c.boards.first.groups;
    await tapCards(tester, [g[0].items[0], g[0].items[1], g[1].items[0]], 'tr');
    await tester.tap(find.text('Bağla'));
    await tester.pumpAndSettle();
    expect(find.text('Çok yakın! Bir kart yanlış.'), findsOneWidget);
  });

  testWidgets('magaza: sahte satin alma jeton ekler, ingilizce arayuz', (
    tester,
  ) async {
    final state = makeState(lang: 'en');
    await pumpApp(tester, state);
    final before = state.coins;
    await tester.tap(find.byTooltip('Shop'));
    await tester.pumpAndSettle();
    expect(find.text('Detective Club'), findsOneWidget);
    await tester.scrollUntilVisible(find.text('1000'), 300);
    await tester.ensureVisible(find.text('1000'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('1000'));
    await tester.pumpAndSettle();
    expect(state.coins, before + ProductIds.coinPacks[ProductIds.coinsMedium]!);
    expect(find.text('Thank you! Purchase complete.'), findsOneWidget);
  });

  testWidgets('premium dosya kilitli gorunur ve magazaya yonlendirir', (
    tester,
  ) async {
    final state = makeState();
    await pumpApp(tester, state);
    await tester.tap(find.text('Dosyalar').last);
    await tester.pumpAndSettle();
    final premium = content.cases.firstWhere((c) => c.premium);
    expect(find.text(premium.title.of('tr')), findsOneWidget);
    expect(find.text('Premium dosya. Mağazadan açabilirsin.'), findsOneWidget);
  });

  testWidgets(
    'animasyonlar acikken: kart ucusu, konfeti, yildizlar ve sesler',
    (tester) async {
      final sound = SilentSoundService();
      final c = content.cases.first;
      final state = GameState(
        content: content,
        progress: Progress(tutorialDone: true, loginRewardDate: '2026-11-19')
          ..settings.language = 'tr',
        store: MemoryProgressStore(),
        ads: FakeAdsService(delay: Duration.zero),
        purchases: FakePurchaseService(delay: Duration.zero),
        sound: sound,
        clock: () => DateTime(2026, 11, 19, 10),
      );
      state.record(c).introSeen = true;
      tester.view.physicalSize = const Size(412 * 3, 915 * 3);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(DetectiveApp(state: state));
      await step(tester);
      await tester.tap(find.text('Soruşturmaya devam'));
      await step(tester, 1200);

      final board = c.boards.first;
      for (final g in board.groups) {
        await tapCards(tester, g.items, 'tr');
        await tester.tap(find.text('Bağla'));
        // Hayalet kartlar ucarken bir sonraki gruba gecilir.
        await step(tester, 700);
      }
      await step(tester, 1600);
      expect(find.text('Pano çözüldü!'), findsOneWidget);
      await step(tester, 1500);
      await tester.tap(find.text('Devam'));
      await step(tester, 1200);

      expect(state.boardStars(board), 3);
      expect(
        sound.played,
        containsAllInOrder([Sfx.pick, Sfx.thread, Sfx.correct]),
      );
      expect(
        sound.played.where((s) => s == Sfx.correct).length,
        board.groups.length,
      );
      expect(sound.played.where((s) => s == Sfx.star).length, 3);
      // Ana ekrandaki sonsuz yagmur animasyonu testin sonunda durdurulsun.
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'gunluk odul penceresi: ilk acilista cikar, alinca jeton eklenir',
    (tester) async {
      final state = makeState(progress: Progress());
      final start = state.coins;
      await pumpApp(tester, state);
      expect(find.text('Günlük ödül'), findsOneWidget);
      expect(find.text('1. gün'), findsOneWidget);
      await tester.tap(find.textContaining('Al ·'));
      await tester.pumpAndSettle();
      expect(state.coins, start + 20);
      expect(find.text('Günlük ödül'), findsNothing);
      expect(state.loginRewardAvailable, isFalse);
    },
  );
}
