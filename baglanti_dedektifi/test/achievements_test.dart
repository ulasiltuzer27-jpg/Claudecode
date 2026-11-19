import 'dart:io';

import 'package:baglanti_dedektifi/app/game_state.dart';
import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/achievements.dart';
import 'package:baglanti_dedektifi/game/board_session.dart';
import 'package:baglanti_dedektifi/monetization/ads_service.dart';
import 'package:baglanti_dedektifi/monetization/products.dart';
import 'package:baglanti_dedektifi/monetization/purchase_service.dart';
import 'package:baglanti_dedektifi/progress/progress.dart';
import 'package:baglanti_dedektifi/progress/progress_store.dart';
import 'package:baglanti_dedektifi/social/play_games.dart';
import 'package:flutter_test/flutter_test.dart';

late GameContent content;

class RecordingPlayGames extends NoopPlayGames {
  final unlocked = <String>[];
  final stars = <int>[];
  List<String> initWith = [];

  @override
  Future<void> init(Iterable<String> unlockedAchievements) async =>
      initWith = unlockedAchievements.toList();

  @override
  Future<void> unlock(String achievementId) async =>
      unlocked.add(achievementId);

  @override
  Future<void> submitScores({
    required int totalStars,
    required int bestStreak,
  }) async => stars.add(totalStars);
}

BoardSession solved(Board b) {
  final s = BoardSession(b, seed: 1);
  for (final g in b.groups) {
    g.items.forEach(s.toggle);
    s.submit();
  }
  return s;
}

AchievementDef def(String id) => achievements.firstWhere((a) => a.id == id);

void main() {
  setUpAll(() async {
    content = await loadContent((p) => File(p).readAsString());
  });

  late RecordingPlayGames play;

  GameState make([Progress? progress]) {
    play = RecordingPlayGames();
    return GameState(
      content: content,
      progress: progress ?? Progress(),
      store: MemoryProgressStore(),
      ads: FakeAdsService(delay: Duration.zero),
      purchases: FakePurchaseService(delay: Duration.zero),
      playGames: play,
      clock: () => DateTime(2026, 11, 19, 10),
    );
  }

  test('kimlikler benzersiz, ikon ve TR/EN metin var, hedefler pozitif', () {
    expect(achievements.map((a) => a.id).toSet().length, achievements.length);
    for (final a in achievements) {
      expect(
        File('assets/icons/${a.icon}.svg').existsSync(),
        isTrue,
        reason: a.id,
      );
      expect(a.title.hasAll(['tr', 'en']), isTrue, reason: a.id);
      expect(a.description.hasAll(['tr', 'en']), isTrue, reason: a.id);
      expect(a.target, greaterThan(0));
      expect(a.reward, greaterThan(0));
    }
  });

  test('ilk pano: basarim acilir, odul bir kez, olay yayinlanir, Play Games bilgilenir', () async {
    final gs = make();
    final events = <String>[];
    gs.achievementUnlocks.listen((a) => events.add(a.id));
    final c = content.cases.first;
    final start = gs.coins;
    final r = gs.completeCaseBoard(c, solved(c.boards[0]));
    await pumpEventQueue();
    expect(gs.progress.achievements, contains('first_board'));
    expect(events, ['first_board']);
    expect(gs.coins, start + r.coins + def('first_board').reward);
    expect(play.unlocked, ['first_board']);
    expect(play.stars.last, gs.totalStars);

    // Tekrar oynamak ayni basarimi yeniden vermez.
    final before = gs.coins;
    final r2 = gs.completeCaseBoard(c, solved(c.boards[0]));
    await pumpEventQueue();
    expect(events, ['first_board']);
    expect(gs.coins, before + r2.coins);
  });

  test('dava kapaninca ve ilk suclamada iki basarim birden', () {
    final gs = make();
    final c = content.cases.first;
    for (final b in c.boards) {
      gs.completeCaseBoard(c, solved(b));
    }
    gs.accuse(c, c.culpritId, c.proofClueIds.take(2).toSet());
    expect(
      gs.progress.achievements,
      containsAll(['case_1', 'one_shot', 'perfect_case']),
    );
  });

  test('kulube katilinca kulup basarimi', () async {
    final gs = make();
    await gs.buy(ProductIds.clubWeekly);
    await pumpEventQueue();
    expect(gs.progress.achievements, contains('club'));
  });

  test('eski kayit: guncellemeden once kazanilanlar acilista yakalanir ve Play Games ile esitlenir', () async {
    final gs = make(Progress(boardsCompleted: 30, keenEyes: 12, bestStreak: 8));
    await gs.startServices();
    expect(
      gs.progress.achievements,
      containsAll(['first_board', 'boards_25', 'keen_10', 'streak_7']),
    );
    expect(gs.progress.achievements, isNot(contains('boards_100')));
    expect(play.initWith, containsAll(['first_board', 'boards_25']));
  });

  test('ilerleme hedefle sinirlanir', () {
    final p = Progress(boardsCompleted: 500);
    expect(def('boards_25').progressOf(p, content), 25);
  });
}
