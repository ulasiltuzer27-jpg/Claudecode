import 'package:baglanti_dedektifi/data/content_validator.dart';
import 'package:baglanti_dedektifi/data/models.dart';
import 'package:flutter_test/flutter_test.dart';

LocalizedText t(String tr, [String? en]) => LocalizedText({'tr': tr, 'en': en ?? tr});

Item item(String id, {String? label, String? icon}) =>
    Item(id: id, icon: icon ?? id, label: t(label ?? id));

GameContent contentWith(Board daily, {List<Item> extra = const []}) {
  final ids = ['a', 'b', 'c', 'd', 'e', 'f', 'g'];
  return GameContent(
    catalog: Catalog({
      for (final id in ids) id: item(id),
      for (final i in extra) i.id: i,
    }),
    cases: const [],
    dailyBoards: [daily],
  );
}

Board board(List<List<String>> groups, {List<String> decoys = const []}) => Board(
      id: 'd1',
      decoys: decoys,
      groups: [
        for (final (i, g) in groups.indexed)
          BoardGroup(id: 'g$i', theme: t('tema $i'), items: g),
      ],
    );

Set<String> icons(GameContent c) => c.catalog.items.values.map((i) => i.icon).toSet();

void main() {
  test('temiz pano sorunsuz gecer', () {
    final c = contentWith(board([['a', 'b', 'c'], ['d', 'e', 'f']]));
    expect(validateContent(c, icons(c)), isEmpty);
  });

  test('ayni kart iki kez', () {
    final c = contentWith(board([['a', 'b', 'c'], ['c', 'e', 'f']]));
    expect(validateContent(c, icons(c)), contains(contains('ayni kart iki kez: c')));
  });

  test('katalogda olmayan kart', () {
    final c = contentWith(board([['a', 'b', 'c'], ['d', 'e', 'zz']]));
    expect(validateContent(c, icons(c)), contains(contains('katalogda yok: zz')));
  });

  test('ikon dosyasi eksik', () {
    final c = contentWith(board([['a', 'b', 'c'], ['d', 'e', 'f']]));
    expect(validateContent(c, icons(c)..remove('a')), contains(contains('ikon dosyasi yok (a)')));
  });

  test('cok kucuk grup', () {
    final c = contentWith(board([['a', 'b'], ['d', 'e', 'f']]));
    expect(validateContent(c, icons(c)), contains(contains('grup 3-5 kart olmali')));
  });

  test('ayni etiketli iki kart', () {
    final c = contentWith(
      board([['a', 'b', 'c'], ['d', 'e', 'kupa2']]),
      extra: [item('kupa2', label: 'a')],
    );
    expect(validateContent(c, icons(c)), contains(contains('ayni etiket')));
  });

  test('ayni ikonlu iki kart', () {
    final c = contentWith(
      board([['a', 'b', 'c'], ['d', 'e', 'a2']]),
      extra: [item('a2', label: 'baska', icon: 'a')],
    );
    expect(validateContent(c, icons(c)), contains(contains('ayni ikonu')));
  });

  test('eksik ceviri', () {
    final c = GameContent(
      catalog: Catalog({
        for (final id in ['a', 'b', 'c', 'd', 'e', 'f']) id: item(id),
        'x': Item(id: 'x', icon: 'x', label: LocalizedText({'tr': 'x'})),
      }),
      cases: const [],
      dailyBoards: [board([['a', 'b', 'c'], ['d', 'e', 'f']])],
    );
    expect(validateContent(c, {...icons(c)}), contains(contains('kart x: TR/EN')));
  });

  test('alsoFits panoda olmayan karti gosteriyor', () {
    final c = contentWith(Board(id: 'd1', groups: [
      BoardGroup(id: 'g1', theme: t('x'), items: const ['a', 'b', 'c'], alsoFits: const {'g': 'neden'}),
      BoardGroup(id: 'g2', theme: t('y'), items: const ['d', 'e', 'f']),
    ]));
    expect(validateContent(c, icons(c)), contains(contains('alsoFits panoda olmayan kart (g)')));
  });
}
