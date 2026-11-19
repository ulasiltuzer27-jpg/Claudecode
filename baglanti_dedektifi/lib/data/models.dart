// Icerik modelleri. Hepsi degismez (immutable) ve saf Dart: Flutter'a
// bagimli degiller, boylece motor testleri VM'de dogrudan calisir.

/// Iki dilli metin. Eksik dilde Ingilizceye, o da yoksa ilk degere duser.
class LocalizedText {
  const LocalizedText(this.values);

  final Map<String, String> values;

  factory LocalizedText.fromJson(Object? json) {
    if (json is String) return LocalizedText({'en': json, 'tr': json});
    final map = (json as Map).cast<String, Object?>();
    return LocalizedText({
      for (final e in map.entries) e.key: e.value as String,
    });
  }

  String of(String lang) =>
      values[lang] ??
      values['en'] ??
      (values.isEmpty ? '' : values.values.first);

  bool hasAll(Iterable<String> langs) =>
      langs.every((l) => (values[l] ?? '').trim().isNotEmpty);
}

/// Panoda gorunen delil karti.
class Item {
  const Item({required this.id, required this.icon, required this.label});

  final String id;

  /// `assets/icons/<icon>.svg`
  final String icon;
  final LocalizedText label;

  String get assetPath => 'assets/icons/$icon.svg';
}

class Catalog {
  const Catalog(this.items);

  final Map<String, Item> items;

  factory Catalog.fromJson(Map<String, Object?> json) {
    final raw = (json['items'] as Map).cast<String, Object?>();
    return Catalog({
      for (final e in raw.entries)
        e.key: () {
          final m = (e.value as Map).cast<String, Object?>();
          return Item(
            id: e.key,
            icon: (m['icon'] as String?) ?? e.key,
            label: LocalizedText({
              'tr': m['tr'] as String,
              'en': m['en'] as String,
            }),
          );
        }(),
    });
  }

  Item operator [](String id) {
    final item = items[id];
    if (item == null) throw StateError('Katalogda olmayan kart: $id');
    return item;
  }
}

/// Bir panodaki gizli grup.
class BoardGroup {
  const BoardGroup({
    required this.id,
    required this.theme,
    required this.items,
    this.alsoFits = const {},
  });

  final String id;
  final LocalizedText theme;
  final List<String> items;

  /// Baska gruptan olup bu gruba aitmis gibi gorunen kartlar ve nedenleri.
  /// Yalnizca icerik dogrulamasi ve icerik yazarlari icin; oyunda kullanilmaz.
  final Map<String, String> alsoFits;

  int get size => items.length;

  factory BoardGroup.fromJson(Map<String, Object?> json, int index) =>
      BoardGroup(
        id: (json['id'] as String?) ?? 'g${index + 1}',
        theme: LocalizedText.fromJson(json['theme']),
        items: (json['items'] as List).cast<String>(),
        alsoFits: ((json['alsoFits'] as Map?) ?? const {})
            .cast<String, Object?>()
            .map((k, v) => MapEntry(k, v as String)),
      );
}

class Board {
  const Board({
    required this.id,
    required this.groups,
    this.decoys = const [],
    this.clueId,
  });

  final String id;

  /// Sira zorluk sirasidir: ilk grup en kolay.
  final List<BoardGroup> groups;

  /// Hicbir gruba ait olmayan sahte deliller.
  final List<String> decoys;

  /// Cozulunce acilan ipucu karti (yalnizca dava panolarinda).
  final String? clueId;

  Iterable<String> get allItems sync* {
    for (final g in groups) {
      yield* g.items;
    }
    yield* decoys;
  }

  int get cardCount => groups.fold(0, (s, g) => s + g.size) + decoys.length;

  BoardGroup? groupOf(String itemId) {
    for (final g in groups) {
      if (g.items.contains(itemId)) return g;
    }
    return null;
  }

  factory Board.fromJson(Map<String, Object?> json) => Board(
    id: json['id'] as String,
    groups: [
      for (final (i, g) in (json['groups'] as List).indexed)
        BoardGroup.fromJson((g as Map).cast<String, Object?>(), i),
    ],
    decoys: ((json['decoys'] as List?) ?? const []).cast<String>(),
    clueId: json['clue'] as String?,
  );
}

class Suspect {
  const Suspect({
    required this.id,
    required this.icon,
    required this.name,
    required this.role,
    required this.bio,
  });

  final String id;
  final String icon;
  final LocalizedText name;
  final LocalizedText role;
  final LocalizedText bio;

  String get assetPath => 'assets/icons/$icon.svg';

  factory Suspect.fromJson(Map<String, Object?> json) => Suspect(
    id: json['id'] as String,
    icon: json['icon'] as String,
    name: LocalizedText.fromJson(json['name']),
    role: LocalizedText.fromJson(json['role']),
    bio: LocalizedText.fromJson(json['bio']),
  );
}

class Clue {
  const Clue({required this.id, required this.text});

  final String id;
  final LocalizedText text;

  factory Clue.fromJson(Map<String, Object?> json) => Clue(
    id: json['id'] as String,
    text: LocalizedText.fromJson(json['text']),
  );
}

/// Hikaye sahnesi: buyuk bir ikon ve bir paragraf.
class StoryPanel {
  const StoryPanel({required this.icon, required this.text});

  final String icon;
  final LocalizedText text;

  String get assetPath => 'assets/icons/$icon.svg';

  factory StoryPanel.fromJson(Map<String, Object?> json) => StoryPanel(
    icon: json['icon'] as String,
    text: LocalizedText.fromJson(json['text']),
  );
}

class CaseFile {
  const CaseFile({
    required this.id,
    required this.order,
    required this.premium,
    required this.icon,
    required this.title,
    required this.intro,
    required this.suspects,
    required this.clues,
    required this.boards,
    required this.culpritId,
    required this.proofClueIds,
    required this.ending,
    required this.teaser,
    this.packId,
  });

  final String id;
  final int order;
  final bool premium;

  /// Premium davanin hangi satin alinabilir pakette oldugu.
  final String? packId;
  final String icon;
  final LocalizedText title;
  final List<StoryPanel> intro;
  final List<Suspect> suspects;
  final List<Clue> clues;
  final List<Board> boards;
  final String culpritId;

  /// Sucluyu kanitlayan ipuclari. Suclamada bunlardan herhangi ikisi yeter.
  final List<String> proofClueIds;
  final LocalizedText ending;
  final LocalizedText teaser;

  String get assetPath => 'assets/icons/$icon.svg';

  Suspect suspect(String id) => suspects.firstWhere((s) => s.id == id);
  Clue clue(String id) => clues.firstWhere((c) => c.id == id);

  factory CaseFile.fromJson(Map<String, Object?> json) {
    List<Map<String, Object?>> list(String key) => [
      for (final e in (json[key] as List)) (e as Map).cast<String, Object?>(),
    ];
    return CaseFile(
      id: json['id'] as String,
      order: json['order'] as int,
      premium: (json['premium'] as bool?) ?? false,
      packId: json['pack'] as String?,
      icon: json['icon'] as String,
      title: LocalizedText.fromJson(json['title']),
      intro: list('intro').map(StoryPanel.fromJson).toList(),
      suspects: list('suspects').map(Suspect.fromJson).toList(),
      clues: list('clues').map(Clue.fromJson).toList(),
      boards: list('boards').map(Board.fromJson).toList(),
      culpritId: json['culprit'] as String,
      proofClueIds: (json['proof'] as List).cast<String>(),
      ending: LocalizedText.fromJson(json['ending']),
      teaser: LocalizedText.fromJson(json['teaser']),
    );
  }
}

/// Tum oyun icerigi.
class GameContent {
  const GameContent({
    required this.catalog,
    required this.cases,
    required this.dailyBoards,
  });

  final Catalog catalog;

  /// `order` alanina gore sirali.
  final List<CaseFile> cases;
  final List<Board> dailyBoards;

  CaseFile caseById(String id) => cases.firstWhere((c) => c.id == id);
}
