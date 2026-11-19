import 'dart:io';

import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/content_validator.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:baglanti_dedektifi/game/board_session.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late GameContent content;

  setUpAll(() async {
    content = await loadContent((path) => File(path).readAsString());
  });

  test('tum icerik dogrulayicidan gecer', () {
    final icons = Directory('assets/icons')
        .listSync()
        .map((f) => f.uri.pathSegments.last.replaceAll('.svg', ''))
        .toSet();
    expect(validateContent(content, icons), isEmpty);
  });

  test('ilk dava ucretsiz ve kolay baslar', () {
    final first = content.cases.first;
    expect(first.premium, isFalse);
    expect(first.boards.first.cardCount, lessThanOrEqualTo(9));
    expect(first.boards.first.decoys, isEmpty);
  });

  test('her pano cozulebilir: gruplari sirayla secmek panoyu bitirir', () {
    final boards = [
      for (final c in content.cases) ...c.boards,
      ...content.dailyBoards,
    ];
    for (final b in boards) {
      final s = BoardSession(b, seed: 7);
      for (final g in b.groups) {
        g.items.forEach(s.toggle);
        expect(s.submit().outcome, SubmitOutcome.correct, reason: b.id);
      }
      expect(s.status, BoardStatus.solved, reason: b.id);
      expect(s.order.toSet(), b.decoys.toSet(), reason: b.id);
    }
  });

  test('suclu, kanit ipuclarinin hepsi dava panolarindan acilir', () {
    for (final c in content.cases) {
      final opened = c.boards.map((b) => b.clueId).toSet();
      expect(opened.containsAll(c.proofClueIds), isTrue, reason: c.id);
    }
  });
}
