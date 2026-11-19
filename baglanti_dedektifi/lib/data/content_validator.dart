import 'models.dart';

const supportedLanguages = ['tr', 'en'];

/// Icerik denetimi. Bos liste = sorun yok.
///
/// [availableIcons]: `assets/icons/` altindaki dosya adlari (uzantisiz).
List<String> validateContent(GameContent content, Set<String> availableIcons) {
  final errors = <String>[];
  final catalog = content.catalog;

  void text(String where, LocalizedText t) {
    if (!t.hasAll(supportedLanguages)) errors.add('$where: TR/EN metni eksik');
  }

  void icon(String where, String name) {
    if (!availableIcons.contains(name)) errors.add('$where: ikon dosyasi yok ($name)');
  }

  for (final item in catalog.items.values) {
    icon('kart ${item.id}', item.icon);
    text('kart ${item.id}', item.label);
  }

  final boardIds = <String>{};

  void board(String where, Board b, {required bool needsClue, Set<String>? clueIds}) {
    if (!boardIds.add(b.id)) errors.add('$where: tekrar eden pano kimligi ${b.id}');
    if (b.groups.length < 2) errors.add('$where: en az 2 grup olmali');
    final seen = <String>{};
    for (final id in b.allItems) {
      if (!catalog.items.containsKey(id)) errors.add('$where: katalogda yok: $id');
      if (!seen.add(id)) errors.add('$where: ayni kart iki kez: $id');
    }
    // Ayni gorunen iki kart (ayni ikon) bir panoda olmamali.
    final icons = <String, String>{};
    for (final id in seen) {
      final it = catalog.items[id];
      if (it == null) continue;
      final other = icons[it.icon];
      if (other != null) errors.add('$where: $other ve $id ayni ikonu kullaniyor');
      icons[it.icon] = id;
    }
    // Ayni etiketli iki kart da oyuncuyu yaniltir (ornegin TR'de "Kupa").
    for (final lang in supportedLanguages) {
      final labels = <String, String>{};
      for (final id in seen) {
        final it = catalog.items[id];
        if (it == null) continue;
        final label = it.label.of(lang).toLowerCase();
        final other = labels[label];
        if (other != null) errors.add('$where: $other ve $id ayni etiket ($lang: $label)');
        labels[label] = id;
      }
    }
    final groupIds = <String>{};
    for (final g in b.groups) {
      if (!groupIds.add(g.id)) errors.add('$where: tekrar eden grup ${g.id}');
      if (g.size < 3 || g.size > 5) errors.add('$where/${g.id}: grup 3-5 kart olmali');
      text('$where/${g.id} temasi', g.theme);
      for (final e in g.alsoFits.entries) {
        if (g.items.contains(e.key)) {
          errors.add('$where/${g.id}: alsoFits kendi kartini gosteriyor (${e.key})');
        } else if (!seen.contains(e.key)) {
          errors.add('$where/${g.id}: alsoFits panoda olmayan kart (${e.key})');
        }
        if (e.value.trim().isEmpty) errors.add('$where/${g.id}: alsoFits aciklamasi bos');
      }
    }
    if (b.decoys.length > 3) errors.add('$where: en fazla 3 sahte delil');
    if (b.cardCount > 20) errors.add('$where: en fazla 20 kart');
    if (needsClue) {
      if (b.clueId == null) {
        errors.add('$where: ipucu karti yok');
      } else if (clueIds != null && !clueIds.contains(b.clueId)) {
        errors.add('$where: tanimsiz ipucu ${b.clueId}');
      }
    }
  }

  final orders = <int>{};
  for (final c in content.cases) {
    final w = 'dava ${c.id}';
    if (!orders.add(c.order)) errors.add('$w: tekrar eden sira ${c.order}');
    icon(w, c.icon);
    text('$w basligi', c.title);
    text('$w sonu', c.ending);
    text('$w devami', c.teaser);
    if (c.premium && c.packId == null) errors.add('$w: premium ama paket yok');
    if (c.intro.isEmpty) errors.add('$w: giris hikayesi yok');
    for (final (i, p) in c.intro.indexed) {
      icon('$w giris $i', p.icon);
      text('$w giris $i', p.text);
    }
    if (c.suspects.length < 3 || c.suspects.length > 4) {
      errors.add('$w: 3-4 supheli olmali');
    }
    final suspectIds = <String>{};
    for (final s in c.suspects) {
      if (!suspectIds.add(s.id)) errors.add('$w: tekrar eden supheli ${s.id}');
      icon('$w/${s.id}', s.icon);
      text('$w/${s.id} adi', s.name);
      text('$w/${s.id} rolu', s.role);
      text('$w/${s.id} biyografi', s.bio);
    }
    if (!suspectIds.contains(c.culpritId)) errors.add('$w: suclu supheliler arasinda yok');

    final clueIds = <String>{};
    for (final cl in c.clues) {
      if (!clueIds.add(cl.id)) errors.add('$w: tekrar eden ipucu ${cl.id}');
      text('$w/${cl.id}', cl.text);
    }
    if (c.proofClueIds.length < 2) errors.add('$w: en az 2 kanit ipucu olmali');
    for (final p in c.proofClueIds) {
      if (!clueIds.contains(p)) errors.add('$w: kanit ipucu tanimsiz: $p');
    }

    if (c.boards.length != 10) errors.add('$w: 10 pano olmali (${c.boards.length})');
    final usedClues = <String>{};
    for (final b in c.boards) {
      board('$w/${b.id}', b, needsClue: true, clueIds: clueIds);
      if (b.clueId != null && !usedClues.add(b.clueId!)) {
        errors.add('$w/${b.id}: ipucu ${b.clueId} iki panoda');
      }
    }
    for (final id in clueIds.difference(usedClues)) {
      errors.add('$w: ipucu $id hicbir panodan acilmiyor');
    }
  }

  if (content.dailyBoards.isEmpty) errors.add('gunluk pano yok');
  for (final b in content.dailyBoards) {
    board('gunluk/${b.id}', b, needsClue: false);
  }
  return errors;
}
