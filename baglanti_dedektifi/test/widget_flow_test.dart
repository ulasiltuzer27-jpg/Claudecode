import 'dart:io';

import 'package:baglanti_dedektifi/app/app.dart';
import 'package:baglanti_dedektifi/app/game_state.dart';
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
      progress: (progress ?? Progress())..settings.language = lang,
      store: MemoryProgressStore(),
      ads: FakeAdsService(delay: Duration.zero),
      purchases: FakePurchaseService(delay: Duration.zero),
      clock: () => DateTime(2026, 11, 19, 10),
    );

Future<void> pumpApp(WidgetTester tester, GameState state) async {
  tester.view.physicalSize = const Size(412 * 3, 915 * 3);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(DetectiveApp(state: state));
  await tester.pumpAndSettle();
}

String label(String id, String lang) => content.catalog[id].label.of(lang);

Future<void> tapCards(WidgetTester tester, Iterable<String> ids, String lang) async {
  for (final id in ids) {
    await tester.tap(find.text(label(id, lang)));
    await tester.pump();
  }
}

void main() {
  setUpAll(() async {
    content = await loadContent((p) => File(p).readAsString());
  });

  testWidgets('ilk dava: hikaye -> ogretici -> pano cozulur -> ipucu acilir', (tester) async {
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
    final state = makeState(progress: Progress(tutorialDone: true));
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

  testWidgets('magaza: sahte satin alma jeton ekler, ingilizce arayuz', (tester) async {
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

  testWidgets('premium dosya kilitli gorunur ve magazaya yonlendirir', (tester) async {
    final state = makeState();
    await pumpApp(tester, state);
    await tester.tap(find.text('Dosyalar').last);
    await tester.pumpAndSettle();
    final premium = content.cases.firstWhere((c) => c.premium);
    expect(find.text(premium.title.of('tr')), findsOneWidget);
    expect(find.text('Premium dosya. Mağazadan açabilirsin.'), findsOneWidget);
  });
}
