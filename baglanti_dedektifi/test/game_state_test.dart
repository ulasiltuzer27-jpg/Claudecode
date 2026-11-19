import 'dart:io';

import 'package:baglanti_dedektifi/app/game_state.dart';
import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/accusation.dart';
import 'package:baglanti_dedektifi/game/board_session.dart';
import 'package:baglanti_dedektifi/game/economy.dart';
import 'package:baglanti_dedektifi/monetization/ads_service.dart';
import 'package:baglanti_dedektifi/monetization/products.dart';
import 'package:baglanti_dedektifi/monetization/purchase_service.dart';
import 'package:baglanti_dedektifi/progress/progress.dart';
import 'package:baglanti_dedektifi/progress/progress_store.dart';
import 'package:flutter_test/flutter_test.dart';

late GameContent content;

BoardSession solved(Board b, {bool withMistake = false}) {
  final s = BoardSession(b, seed: 1);
  if (withMistake) {
    // Iki farkli gruptan kart secip yanlis gonder.
    final g0 = b.groups[0], g1 = b.groups[1];
    final pick = [...g0.items.take(g0.size - 1), g1.items.first];
    pick.forEach(s.toggle);
    s.submit();
    s.clearSelection();
  }
  for (final g in b.groups) {
    g.items.forEach(s.toggle);
    s.submit();
  }
  return s;
}

class Harness {
  Harness({Progress? progress, DateTime? now}) {
    clock = now ?? DateTime(2026, 11, 19, 10);
    state = GameState(
      content: content,
      progress: progress ?? Progress(),
      store: store,
      ads: ads,
      purchases: purchases,
      clock: () => clock,
    );
  }

  final store = MemoryProgressStore();
  final ads = FakeAdsService(delay: Duration.zero);
  final purchases = FakePurchaseService(delay: Duration.zero);
  late DateTime clock;
  late GameState state;
}

void main() {
  setUpAll(() async {
    content = await loadContent((p) => File(p).readAsString());
  });

  test('ilk cozum tam odul + ipucu; tekrar oynama az jeton, ipucu yok', () {
    final h = Harness();
    final c = content.cases.first;
    final start = h.state.coins;
    final r1 = h.state.completeCaseBoard(c, solved(c.boards[0]));
    expect(r1.firstTime, isTrue);
    expect(r1.stars, 3);
    expect(r1.coins, Economy.boardReward(3));
    expect(r1.clue!.id, c.boards[0].clueId);
    expect(h.state.coins, start + r1.coins);
    expect(h.state.isBoardUnlocked(c, 1), isTrue);
    expect(h.state.isBoardUnlocked(c, 2), isFalse);

    final r2 = h.state.completeCaseBoard(c, solved(c.boards[0], withMistake: true));
    expect(r2.firstTime, isFalse);
    expect(r2.coins, Economy.replayCoins);
    expect(r2.clue, isNull);
    // En iyi yildiz korunur.
    expect(h.state.boardStars(c.boards[0]), 3);
    expect(h.store.saves, greaterThan(0));
  });

  test('suclama yalnizca tum panolar bitince; dogru suclama dosyayi kapatir', () {
    final h = Harness();
    final c = content.cases.first;
    expect(h.state.canAccuse(c), isFalse);
    for (final b in c.boards) {
      h.state.completeCaseBoard(c, solved(b));
    }
    expect(h.state.canAccuse(c), isTrue);
    expect(h.state.collectedClues(c).length, c.boards.length);

    final wrong = c.suspects.firstWhere((s) => s.id != c.culpritId).id;
    expect(h.state.accuse(c, wrong, c.proofClueIds.take(2).toSet()).outcome,
        AccusationOutcome.wrongSuspect);
    final r = h.state.accuse(c, c.culpritId, c.proofClueIds.take(2).toSet());
    expect(r.outcome, AccusationOutcome.correct);
    expect(r.stars, 2);
    expect(h.state.record(c).solved, isTrue);
    expect(h.state.isPreviousClosed(content.cases[1]), isTrue);
  });

  test('kilit: ucretsiz davalar sirayla; premium ilk dava kapaninca + satin alinca', () async {
    final h = Harness();
    final free = content.cases.where((c) => !c.premium).toList();
    final premium = content.cases.where((c) => c.premium).toList();
    expect(free.length, 6);
    expect(premium.length, 2);
    expect(h.state.isCasePlayable(free[0]), isTrue);
    expect(h.state.isCasePlayable(free[1]), isFalse);
    expect(h.state.isPreviousClosed(premium.first), isFalse);

    h.state.record(free[0]).solved = true;
    expect(h.state.isCasePlayable(free[1]), isTrue);
    expect(h.state.isCasePlayable(free[2]), isFalse);
    // Ilk dava kapali ama paket yok: hala kapali.
    expect(h.state.isPreviousClosed(premium.first), isTrue);
    expect(h.state.isCasePlayable(premium.first), isFalse);

    await h.state.buy(premium.first.packId!);
    await pumpEventQueue();
    // Ana hikayenin geri kalanini beklemeden oynanabilir.
    for (final p in premium) {
      expect(h.state.isCasePlayable(p), isTrue, reason: p.id);
    }
  });

  test('premium dosya: paket ya da kulup olmadan kapali', () async {
    final h = Harness();
    final premium = content.cases.firstWhere((c) => c.premium);
    expect(h.state.hasAccess(premium), isFalse);
    await h.state.buy(premium.packId!);
    await pumpEventQueue();
    expect(h.state.hasAccess(premium), isTrue);
  });

  test('satin almalar: jeton, reklamsiz, baslangic paketi, kulup', () async {
    final h = Harness();
    final start = h.state.coins;
    await h.state.buy(ProductIds.coinsMedium);
    await pumpEventQueue();
    expect(h.state.coins, start + 1000);

    await h.state.buy(ProductIds.starterPack);
    await pumpEventQueue();
    expect(h.state.coins, start + 1000 + ProductIds.starterCoins);
    expect(h.state.noAds, isTrue);

    await h.state.buy(ProductIds.clubYearly);
    await pumpEventQueue();
    expect(h.state.clubActive, isTrue);
    expect(h.state.freeHintsLeft, Economy.clubFreeHintsPerDay);
  });

  test('geri yukleme jeton paketini tekrar vermez', () async {
    final h = Harness();
    final notices = <StoreNotice>[];
    h.state.notices.listen(notices.add);
    await h.state.buy(ProductIds.removeAds);
    await pumpEventQueue();
    final coins = h.state.coins;
    await h.state.restorePurchases();
    await pumpEventQueue();
    expect(h.state.coins, coins);
    expect(h.state.noAds, isTrue);
    expect(notices, containsAllInOrder([StoreNotice.thanks, StoreNotice.restored]));
  });

  test('gecis reklami: ilk panolarda yok, sonra her 3 panoda bir, reklamsizda hic', () {
    final h = Harness();
    final c = content.cases.first;
    final slots = <bool>[];
    for (var i = 0; i < 10; i++) {
      h.state.completeCaseBoard(c, solved(c.boards[i]));
      slots.add(h.state.takeInterstitialSlot());
    }
    // 5 panoluk muhlet; 6. panoda sayac 6 >= 3 -> reklam; sonra 9. panoda.
    expect(slots, [false, false, false, false, false, true, false, false, true, false]);

    final noAds = Harness()..state.entitlements.removeAds = true;
    for (var i = 0; i < 10; i++) {
      noAds.state.completeCaseBoard(c, solved(c.boards[i]));
      expect(noAds.state.takeInterstitialSlot(), isFalse);
    }
  });

  test('buyutec: once kulubun bedava hakki, sonra jeton', () {
    final h = Harness()..state.entitlements.clubActive = true;
    final coins = h.state.coins;
    for (var i = 0; i < Economy.clubFreeHintsPerDay; i++) {
      expect(h.state.payForHint(), isTrue);
    }
    expect(h.state.coins, coins);
    expect(h.state.freeHintsLeft, 0);
    expect(h.state.payForHint(), isTrue);
    expect(h.state.coins, coins - Economy.hintCost);

    // Ertesi gun haklar yenilenir.
    h.clock = h.clock.add(const Duration(days: 1));
    expect(h.state.freeHintsLeft, Economy.clubFreeHintsPerDay);
  });

  test('jeton yetmezse buyutec alinamaz', () {
    final h = Harness(progress: Progress(coins: 10));
    expect(h.state.payForHint(), isFalse);
    expect(h.state.coins, 10);
  });

  test('gunluk: ayni gun ikinci odul yok, ertesi gun seri artar', () {
    final h = Harness();
    final r1 = h.state.completeDaily(solved(h.state.todaysBoard));
    expect(r1.streak, 1);
    expect(h.state.dailyDoneToday, isTrue);
    final again = h.state.completeDaily(solved(h.state.todaysBoard));
    expect(again.total, 0);

    h.clock = h.clock.add(const Duration(days: 1));
    expect(h.state.dailyDoneToday, isFalse);
    final r2 = h.state.completeDaily(solved(h.state.todaysBoard));
    expect(r2.streak, 2);
    expect(r2.coins, Economy.dailyReward(2));

    h.clock = h.clock.add(const Duration(days: 3));
    expect(h.state.streak, 0);
  });

  test('odullu reklam jetonu gunluk sinirli', () async {
    final h = Harness();
    final coins = h.state.coins;
    for (var i = 0; i < Economy.rewardedCoinsPerDay; i++) {
      expect(await h.state.watchAdForCoins(), isTrue);
    }
    expect(await h.state.watchAdForCoins(), isFalse);
    expect(h.state.coins, coins + Economy.rewardedCoins * Economy.rewardedCoinsPerDay);
  });

  test('devam et dugmesi siradaki adimi gosterir', () {
    final h = Harness();
    final c = content.cases.first;
    expect(h.state.nextStep!.boardIndex, 0);
    h.state.completeCaseBoard(c, solved(c.boards[0]));
    expect(h.state.nextStep!.boardIndex, 1);
  });
}
