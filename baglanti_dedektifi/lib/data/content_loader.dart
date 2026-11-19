import 'dart:convert';

import 'models.dart';

const contentRoot = 'assets/content';
const caseIndexPath = '$contentRoot/cases/index.json';
const itemsPath = '$contentRoot/items.json';
const dailyPath = '$contentRoot/daily.json';

String casePath(String id) => '$contentRoot/cases/$id.json';

/// Dosya okuma soyutlamasi: uygulamada `rootBundle`, testte `File`.
typedef TextReader = Future<String> Function(String path);

Map<String, Object?> _obj(String s) => (jsonDecode(s) as Map).cast<String, Object?>();

Future<GameContent> loadContent(TextReader read) async {
  final catalog = Catalog.fromJson(_obj(await read(itemsPath)));
  final index = (jsonDecode(await read(caseIndexPath)) as List).cast<String>();
  final cases = <CaseFile>[
    for (final id in index) CaseFile.fromJson(_obj(await read(casePath(id)))),
  ]..sort((a, b) => a.order.compareTo(b.order));
  final daily = _obj(await read(dailyPath));
  final dailyBoards = [
    for (final b in (daily['boards'] as List))
      Board.fromJson((b as Map).cast<String, Object?>()),
  ];
  return GameContent(catalog: catalog, cases: cases, dailyBoards: dailyBoards);
}
