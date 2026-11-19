// Tum oyun icerigini denetler.
//
//     dart run tool/validate_content.dart
//
// Sorun yoksa 0, varsa 1 ile cikar ve her sorunu bir satira yazar.
import 'dart:io';

import 'package:baglanti_dedektifi/data/content_loader.dart';
import 'package:baglanti_dedektifi/data/content_validator.dart';

Future<void> main() async {
  final content = await loadContent((path) => File(path).readAsString());
  final icons = Directory('assets/icons')
      .listSync()
      .whereType<File>()
      .where((f) => f.path.endsWith('.svg'))
      .map((f) => f.uri.pathSegments.last.replaceAll('.svg', ''))
      .toSet();

  final errors = validateContent(content, icons);
  final boards = content.cases.fold(0, (n, c) => n + c.boards.length);
  if (errors.isEmpty) {
    stdout.writeln('Icerik temiz: ${content.catalog.items.length} kart, '
        '${content.cases.length} dava ($boards pano), '
        '${content.dailyBoards.length} gunluk pano.');
    return;
  }
  for (final e in errors) {
    stderr.writeln(e);
  }
  stderr.writeln('${errors.length} sorun.');
  exitCode = 1;
}
