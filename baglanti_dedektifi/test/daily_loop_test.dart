import 'dart:io';

import 'package:baglanti_dedektifi/app/game_state.dart';
import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/board_session.dart';
import 'package:baglanti_dedektifi/monetization/ads_service.dart';
import 'package:baglanti_dedektifi/monetization/purchase_service.dart';
import 'package:baglanti_dedektifi/notify/reminder_plan.dart';
import 'package:baglanti_dedektifi/notify/reminder_service.dart';
import 'package:baglanti_dedektifi/progress/login_reward.dart';
import 'package:baglanti_dedektifi/progress/progress.dart';
import 'package:baglanti_dedektifi/progress/progress_store.dart';
import 'package:flutter_test/flutter_test.dart';

late GameContent content;

BoardSession solved(Board b) {
  final s = BoardSession(b, seed: 1);
  for (final g in b.groups) {
    g.items.forEach(s.toggle);
    s.submit();
  }
  return s;
}

void main() {
  setUpAll(() async {
    content = await loadContent((p) => File(p).readAsString());
  });

  group('hatirlatma plani', () {
    test('7 gun, aksam 19.00; bugun saat gecmediyse bugun dahil', () {
      final slots = planReminders(
        now: DateTime(2026, 11, 19, 10),
        doneToday: false,
        streak: 0,
        lang: 'tr',
      );
      expect(slots.length, 7);
      expect(slots.first.at, DateTime(2026, 11, 19, 19));
      expect(slots.last.at, DateTime(2026, 11, 25, 19));
      expect(slots.map((s) => s.id).toSet().length, 7);
    });

    test('bugun cozulduyse ya da saat gectiyse bugun atlanir', () {
      final done = planReminders(
        now: DateTime(2026, 11, 19, 10),
        doneToday: true,
        streak: 3,
        lang: 'tr',
      );
      expect(done.first.at, DateTime(2026, 11, 20, 19));
      final late = planReminders(
        now: DateTime(2026, 11, 19, 21),
        doneToday: false,
        streak: 0,
        lang: 'tr',
      );
      expect(late.first.at.day, 20);
      expect(late.length, 6);
    });

    test('seri varsa metin seriden bahseder; dil uyar', () {
      final tr = planReminders(
        now: DateTime(2026, 11, 19, 10),
        doneToday: false,
        streak: 5,
        lang: 'tr',
      );
      expect(tr.first.title, contains('5 günlük'));
      final en = planReminders(
        now: DateTime(2026, 11, 19, 10),
        doneToday: false,
        streak: 0,
        lang: 'en',
      );
      expect(en.first.title, isNot(contains('ü')));
    });

    test('ay sonundan sonraki gunler dogru tarihe duser', () {
      final slots = planReminders(
        now: DateTime(2026, 12, 30, 8),
        doneToday: false,
        streak: 0,
        lang: 'tr',
      );
      expect(slots[2].at, DateTime(2027, 1, 1, 19));
    });
  });

  group('giris odulu takvimi', () {
    final today = DateTime(2026, 11, 19);
    test('ilk kez: 1. gun; dun alindi: bir sonraki gun; kacirildi: basa', () {
      expect(
        loginRewardIndexFor(lastDate: null, lastIndex: 0, today: today),
        0,
      );
      expect(
        loginRewardIndexFor(lastDate: '2026-11-18', lastIndex: 2, today: today),
        3,
      );
      expect(
        loginRewardIndexFor(lastDate: '2026-11-16', lastIndex: 4, today: today),
        0,
      );
    });
    test('7. gunden sonra yeniden 1. gun', () {
      expect(
        loginRewardIndexFor(lastDate: '2026-11-18', lastIndex: 6, today: today),
        0,
      );
    });
    test('gunde bir kez', () {
      expect(
        isLoginRewardAvailable(lastDate: '2026-11-19', today: today),
        isFalse,
      );
      expect(
        isLoginRewardAvailable(lastDate: '2026-11-18', today: today),
        isTrue,
      );
    });
  });

  group('GameState: odul ve hatirlatma', () {
    late DateTime clock;
    late NoopReminderService reminders;

    GameState make({bool grant = true}) {
      clock = DateTime(2026, 11, 19, 10);
      reminders = NoopReminderService(grant: grant);
      return GameState(
        content: content,
        progress: Progress(),
        store: MemoryProgressStore(),
        ads: FakeAdsService(delay: Duration.zero),
        purchases: FakePurchaseService(delay: Duration.zero),
        reminders: reminders,
        clock: () => clock,
      );
    }

    test(
      'giris odulu: gunde bir kez, art arda gunlerde artar, ikiye katlanir',
      () {
        final gs = make();
        final start = gs.coins;
        expect(gs.loginRewardAvailable, isTrue);
        expect(gs.claimLoginReward(), loginRewards[0]);
        expect(gs.loginRewardAvailable, isFalse);
        expect(gs.claimLoginReward(), 0);
        clock = clock.add(const Duration(days: 1));
        expect(gs.loginRewardIndex, 1);
        expect(gs.claimLoginReward(doubled: true), loginRewards[1] * 2);
        expect(gs.coins, start + loginRewards[0] + loginRewards[1] * 2);
      },
    );

    test('hatirlatma sorusu: 3. panodan sonra bir kez', () {
      final gs = make();
      final c = content.cases.first;
      expect(gs.shouldAskReminders, isFalse);
      for (var i = 0; i < 3; i++) {
        gs.completeCaseBoard(c, solved(c.boards[i]));
      }
      expect(gs.shouldAskReminders, isTrue);
      gs.markRemindersAsked();
      expect(gs.shouldAskReminders, isFalse);
    });

    test(
      'izin verilince 7 gunluk plan kurulur; gunluk cozulunce bugun duser',
      () async {
        final gs = make();
        expect(await gs.enableReminders(), isTrue);
        expect(gs.remindersOn, isTrue);
        expect(reminders.scheduled.length, 7);
        gs.completeDaily(solved(gs.todaysBoard));
        await pumpEventQueue();
        expect(reminders.scheduled.length, 6);
        expect(reminders.scheduled.first.at.day, 20);
        await gs.disableReminders();
        expect(reminders.scheduled, isEmpty);
      },
    );

    test('izin reddedilirse hatirlatmalar kapali kalir', () async {
      final gs = make(grant: false);
      expect(await gs.enableReminders(), isFalse);
      expect(gs.remindersOn, isFalse);
      expect(reminders.scheduled, isEmpty);
      expect(gs.shouldAskReminders, isFalse);
    });
  });

  test('eski kayit yeni alanlar olmadan yuklenir', () {
    final p = Progress.fromJson({
      'coins': 50,
      'settings': {'haptics': false},
    });
    expect(p.coins, 50);
    expect(p.loginRewardDate, isNull);
    expect(p.settings.sfx, isTrue);
    expect(p.settings.reminders, isFalse);
    expect(p.settings.haptics, isFalse);
  });
}
