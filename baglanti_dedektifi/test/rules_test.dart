import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/accusation.dart';
import 'package:baglanti_dedektifi/game/economy.dart';
import 'package:baglanti_dedektifi/progress/daily.dart';
import 'package:baglanti_dedektifi/progress/progress.dart';
import 'package:flutter_test/flutter_test.dart';

LocalizedText t(String s) => LocalizedText({'tr': s, 'en': s});

CaseFile sampleCase() => CaseFile(
  id: 'c',
  order: 1,
  premium: false,
  icon: 'gem-stone',
  title: t('t'),
  intro: const [],
  suspects: [
    for (final id in ['a', 'b', 'c'])
      Suspect(id: id, icon: 'x', name: t(id), role: t(id), bio: t(id)),
  ],
  clues: [
    for (final id in ['k1', 'k2', 'k3', 'k4']) Clue(id: id, text: t(id)),
  ],
  boards: const [],
  culpritId: 'b',
  proofClueIds: const ['k2', 'k3', 'k4'],
  ending: t('e'),
  teaser: t('n'),
);

void main() {
  group('suclama', () {
    test('dogru kisi + iki gecerli kanit', () {
      expect(
        judgeAccusation(sampleCase(), 'b', {'k2', 'k4'}),
        AccusationOutcome.correct,
      );
    });
    test('dogru kisi ama zayif kanit', () {
      expect(
        judgeAccusation(sampleCase(), 'b', {'k1', 'k2'}),
        AccusationOutcome.weakEvidence,
      );
      expect(
        judgeAccusation(sampleCase(), 'b', {'k2'}),
        AccusationOutcome.weakEvidence,
      );
    });
    test('yanlis kisi', () {
      expect(
        judgeAccusation(sampleCase(), 'a', {'k2', 'k3'}),
        AccusationOutcome.wrongSuspect,
      );
    });
    test('dava yildizi denemeye gore', () {
      expect(caseStarsForAttempts(1), 3);
      expect(caseStarsForAttempts(2), 2);
      expect(caseStarsForAttempts(5), 1);
    });
  });

  group('gunluk', () {
    test('ayni gun ayni pano, ertesi gun siradaki', () {
      final d = DateTime(2026, 11, 19);
      final i = dailyIndexFor(d, 14);
      expect(dailyIndexFor(DateTime(2026, 11, 19, 23, 59), 14), i);
      expect(dailyIndexFor(DateTime(2026, 11, 20), 14), (i + 1) % 14);
      expect(dailyIndexFor(DateTime(2025, 3, 1), 14), inInclusiveRange(0, 13));
    });

    test('seri: dun +1, bugun ayni, bosluk 1', () {
      final today = DateTime(2026, 11, 19);
      expect(nextStreak(lastDate: null, streak: 0, today: today), 1);
      expect(nextStreak(lastDate: '2026-11-18', streak: 4, today: today), 5);
      expect(nextStreak(lastDate: '2026-11-19', streak: 4, today: today), 4);
      expect(nextStreak(lastDate: '2026-11-16', streak: 4, today: today), 1);
      expect(visibleStreak(lastDate: '2026-11-16', streak: 4, today: today), 0);
      expect(visibleStreak(lastDate: '2026-11-18', streak: 4, today: today), 4);
    });

    test('yaz saati gecisi gun farkini bozmaz', () {
      expect(daysBetween(DateTime(2026, 3, 28), DateTime(2026, 3, 30)), 2);
      expect(daysBetween(DateTime(2026, 10, 24), DateTime(2026, 10, 26)), 2);
    });

    test('tarih anahtari gidis-donus', () {
      final d = DateTime(2026, 1, 5);
      expect(dateKey(d), '2026-01-05');
      expect(parseDateKey(dateKey(d)), d);
    });
  });

  group('ekonomi ve rutbe', () {
    test('odul formulleri', () {
      expect(Economy.boardReward(3), 20);
      expect(Economy.dailyReward(1), 25);
      expect(Economy.dailyReward(30), Economy.dailyReward(7));
    });
    test('rutbe esikleri', () {
      expect(Rank.forStars(0).index, 0);
      expect(Rank.forStars(15).index, 1);
      expect(Rank.forStars(999).index, Rank.all.length - 1);
      expect(Rank.forStars(999).next, isNull);
    });
  });

  test('ilerleme JSON gidis-donus', () {
    final p = Progress(coins: 77)
      ..boardStars['x'] = 3
      ..caseRecord('c').solved = true
      ..caseRecord('c').stars = 2
      ..entitlements.ownedPacks.add('pack1')
      ..entitlements.clubActive = true
      ..settings.language = 'en'
      ..dailyStreak = 4
      ..lastDailyDate = '2026-11-18';
    final q = Progress.fromJson(p.toJson());
    expect(q.coins, 77);
    expect(q.boardStars, {'x': 3});
    expect(q.cases['c']!.solved, isTrue);
    expect(q.totalStars, 5);
    expect(q.entitlements.ownedPacks, {'pack1'});
    expect(q.entitlements.noAds, isTrue);
    expect(q.settings.language, 'en');
    expect(q.dailyStreak, 4);
    expect(q.lastDailyDate, '2026-11-18');
  });

  test('bos JSON varsayilanlara duser', () {
    final p = Progress.fromJson({});
    expect(p.coins, Economy.startingCoins);
    expect(p.entitlements.noAds, isFalse);
  });
}
