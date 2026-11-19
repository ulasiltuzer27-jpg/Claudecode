import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/board_session.dart';
import 'package:flutter_test/flutter_test.dart';

LocalizedText t(String s) => LocalizedText({'tr': s, 'en': s});

Board sampleBoard({List<String> decoys = const []}) => Board(
  id: 'b',
  clueId: 'c1',
  decoys: decoys,
  groups: [
    BoardGroup(
      id: 'cold',
      theme: t('cold'),
      items: const ['ice', 'snow', 'penguin'],
    ),
    BoardGroup(
      id: 'red',
      theme: t('red'),
      items: const ['apple', 'rose', 'fire-engine'],
    ),
    BoardGroup(
      id: 'time',
      theme: t('time'),
      items: const ['clock', 'hourglass', 'calendar', 'stopwatch'],
    ),
  ],
);

void main() {
  group('BoardSession', () {
    test('kartlari karistirir, hepsi yerinde', () {
      final s = BoardSession(sampleBoard(decoys: ['hat']), seed: 1);
      expect(s.order.toSet(), sampleBoard(decoys: ['hat']).allItems.toSet());
      expect(s.order.length, 11);
      expect(s.remainingGroupSizes, [4, 3, 3]);
    });

    test('dogru grup cozulur ve kartlar panodan kalkar', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      for (final id in ['ice', 'snow', 'penguin']) {
        expect(s.toggle(id), isTrue);
      }
      expect(s.canSubmit, isTrue);
      final r = s.submit();
      expect(r.outcome, SubmitOutcome.correct);
      expect(r.group!.id, 'cold');
      expect(s.order, isNot(contains('ice')));
      expect(s.selection, isEmpty);
      expect(s.mistakes, 0);
      expect(s.remainingGroupSizes, [4, 3]);
    });

    test('secim sirasi korunur ve ikinci dokunus secimi kaldirir', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      s.toggle('rose');
      s.toggle('ice');
      expect(s.selection, ['rose', 'ice']);
      s.toggle('rose');
      expect(s.selection, ['ice']);
    });

    test('en buyuk kalan gruptan fazla kart secilemez', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      for (final id in ['clock', 'hourglass', 'calendar', 'stopwatch']) {
        s.toggle(id);
      }
      expect(s.toggle('ice'), isFalse);
      expect(s.selection.length, 4);
    });

    test('boyutu hicbir gruba uymayan secim gonderilemez', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      s.toggle('ice');
      s.toggle('snow');
      expect(s.canSubmit, isFalse);
      expect(s.submit().outcome, SubmitOutcome.invalidSize);
      expect(s.mistakes, 0);
    });

    test('bir eksik: uyari verir ve hata sayar', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      for (final id in ['ice', 'snow', 'apple']) {
        s.toggle(id);
      }
      expect(s.submit().outcome, SubmitOutcome.oneAway);
      expect(s.mistakes, 1);
      // Secim korunur ki oyuncu bir karti degistirebilsin.
      expect(s.selection.length, 3);
    });

    test('ayni yanlis secim ikinci kez ceza yemez', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      for (final id in ['ice', 'rose', 'clock']) {
        s.toggle(id);
      }
      expect(s.submit().outcome, SubmitOutcome.wrong);
      expect(s.submit().outcome, SubmitOutcome.repeated);
      expect(s.mistakes, 1);
    });

    test(
      'hata siniri dolunca iz soguk; forgive devam ettirir ama yildizi korumaz',
      () {
        final s = BoardSession(sampleBoard(), seed: 1, maxMistakes: 2);
        s.toggle('ice');
        s.toggle('rose');
        s.toggle('clock');
        s.submit();
        s.toggle('clock');
        s.toggle('apple');
        s.submit();
        expect(s.status, BoardStatus.failed);
        expect(s.toggle('snow'), isFalse);
        s.forgive();
        expect(s.status, BoardStatus.playing);
        expect(s.totalMistakes, 2);
        expect(s.stars, 1);
      },
    );

    test('buyutec en kolay cozulmemis grubun temasini acar', () {
      final s = BoardSession(sampleBoard(), seed: 1);
      expect(s.revealHint()!.id, 'cold');
      expect(s.revealHint()!.id, 'red');
      expect(s.activeLeads.map((g) => g.id), ['cold', 'red']);
      for (final id in ['ice', 'snow', 'penguin']) {
        s.toggle(id);
      }
      s.submit();
      expect(s.activeLeads.map((g) => g.id), ['red']);
      expect(s.revealHint()!.id, 'time');
      expect(s.canHint, isFalse);
      expect(s.revealHint(), isNull);
    });

    test('yildizlar: hatasiz 3, ipucu ya da 1 hata 2, fazlasi 1', () {
      final perfect = BoardSession(sampleBoard(), seed: 1);
      expect(perfect.stars, 3);
      final hinted = BoardSession(sampleBoard(), seed: 1)..revealHint();
      expect(hinted.stars, 2);
    });

    test('tum gruplar cozulunce pano biter; sahte deliller panoda kalir', () {
      final s = BoardSession(sampleBoard(decoys: ['hat']), seed: 3);
      for (final g in sampleBoard().groups) {
        g.items.forEach(s.toggle);
        expect(s.submit().outcome, SubmitOutcome.correct);
      }
      expect(s.status, BoardStatus.solved);
      expect(s.order, ['hat']);
      expect(s.keenEye, isFalse);
    });

    test('keskin goz: sahte delil once damgalanirsa bonus', () {
      final s = BoardSession(sampleBoard(decoys: ['hat']), seed: 3);
      s.toggleFake('hat');
      for (final g in sampleBoard().groups) {
        g.items.forEach(s.toggle);
        s.submit();
      }
      expect(s.keenEye, isTrue);
    });

    test('keskin goz: gercek bir kart da damgalandiysa bonus yok', () {
      final s = BoardSession(sampleBoard(decoys: ['hat']), seed: 3);
      s.toggleFake('hat');
      s.toggleFake('clock');
      for (final g in sampleBoard().groups) {
        g.items.forEach(s.toggle);
        s.submit();
      }
      expect(s.keenEye, isFalse);
    });

    test('ayni tohum ayni dizilim', () {
      expect(
        BoardSession(sampleBoard(), seed: 42).order,
        BoardSession(sampleBoard(), seed: 42).order,
      );
    });
  });
}
