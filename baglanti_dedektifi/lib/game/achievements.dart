import '../data/models.dart';
import '../progress/progress.dart';

/// Oyun ici basarim. Play Games yapilandirildiysa ayrica oraya da
/// gonderilir (lib/social/play_games_ids.dart).
class AchievementDef {
  const AchievementDef({
    required this.id,
    required this.icon,
    required this.title,
    required this.description,
    required this.reward,
    required this.target,
    required this.measure,
  });

  final String id;
  final String icon;
  final LocalizedText title;
  final LocalizedText description;

  /// Acilinca verilen jeton.
  final int reward;
  final int target;

  /// Su anki ilerleme (0..target, fazlasi kirpilir).
  final int Function(Progress p, GameContent c) measure;

  String get assetPath => 'assets/icons/$icon.svg';

  int progressOf(Progress p, GameContent c) => measure(p, c).clamp(0, target);
}

LocalizedText _t(String tr, String en) => LocalizedText({'tr': tr, 'en': en});

int _solvedCases(Progress p, GameContent c) =>
    c.cases.where((x) => p.cases[x.id]?.solved ?? false).length;

int _bestCaseThreeStars(Progress p, GameContent c) {
  var best = 0;
  for (final cf in c.cases) {
    final n = cf.boards.where((b) => p.boardStars[b.id] == 3).length;
    if (n > best) best = n;
  }
  return best;
}

final achievements = <AchievementDef>[
  AchievementDef(
    id: 'first_board',
    icon: 'pushpin',
    title: _t('İlk ipucu', 'First clue'),
    description: _t('İlk panonu çöz.', 'Solve your first board.'),
    reward: 20,
    target: 1,
    measure: (p, c) => p.boardsCompleted,
  ),
  AchievementDef(
    id: 'boards_25',
    icon: 'card-index-dividers',
    title: _t('Pano avcısı', 'Board hunter'),
    description: _t('25 pano çöz.', 'Solve 25 boards.'),
    reward: 50,
    target: 25,
    measure: (p, c) => p.boardsCompleted,
  ),
  AchievementDef(
    id: 'boards_100',
    icon: 'file-folder',
    title: _t('Mantar pano ustası', 'Cork board master'),
    description: _t('100 pano çöz.', 'Solve 100 boards.'),
    reward: 150,
    target: 100,
    measure: (p, c) => p.boardsCompleted,
  ),
  AchievementDef(
    id: 'case_1',
    icon: 'detective',
    title: _t('İlk dosya', 'First case'),
    description: _t(
      'Bir davayı doğru suçlamayla kapat.',
      'Close a case with the right accusation.',
    ),
    reward: 50,
    target: 1,
    measure: _solvedCases,
  ),
  AchievementDef(
    id: 'case_3',
    icon: 'sports-medal',
    title: _t('Kıdemli dedektif', 'Senior detective'),
    description: _t('Üç davayı kapat.', 'Close three cases.'),
    reward: 80,
    target: 3,
    measure: _solvedCases,
  ),
  AchievementDef(
    id: 'season_finale',
    icon: 'bust-in-silhouette',
    title: _t('Gölge\'nin sonu', 'End of the Shadow'),
    description: _t('Gölge\'nin yüzünü ortaya çıkar.', "Unmask the Shadow."),
    reward: 200,
    target: 1,
    measure: (p, c) => (p.cases['case06']?.solved ?? false) ? 1 : 0,
  ),
  AchievementDef(
    id: 'special_files',
    icon: 'crown',
    title: _t('Özel dosyalar', 'Special files'),
    description: _t('İki özel dosyayı da kapat.', 'Close both special files.'),
    reward: 150,
    target: 2,
    measure: (p, c) => c.cases
        .where((x) => x.premium && (p.cases[x.id]?.solved ?? false))
        .length,
  ),
  AchievementDef(
    id: 'perfect_case',
    icon: 'gem-stone',
    title: _t('Kusursuz dosya', 'Flawless file'),
    description: _t(
      'Bir davanın 10 panosunun hepsinden 3 yıldız al.',
      'Earn 3 stars on all 10 boards of a case.',
    ),
    reward: 120,
    target: 10,
    measure: _bestCaseThreeStars,
  ),
  AchievementDef(
    id: 'one_shot',
    icon: 'bullseye',
    title: _t('Tek atış', 'One shot'),
    description: _t(
      'Bir davayı ilk suçlamada çöz.',
      'Solve a case with your first accusation.',
    ),
    reward: 60,
    target: 1,
    measure: (p, c) =>
        p.cases.values.where((r) => r.solved && r.stars == 3).length,
  ),
  AchievementDef(
    id: 'keen_10',
    icon: 'eye',
    title: _t('Keskin göz', 'Keen eye'),
    description: _t(
      '10 sahte delili pano bitmeden damgala.',
      'Stamp 10 fake clues before a board ends.',
    ),
    reward: 60,
    target: 10,
    measure: (p, c) => p.keenEyes,
  ),
  AchievementDef(
    id: 'keen_50',
    icon: 'magnifying-glass-tilted-left',
    title: _t('Şahin gözü', 'Hawk eye'),
    description: _t('50 sahte delil yakala.', 'Catch 50 fake clues.'),
    reward: 150,
    target: 50,
    measure: (p, c) => p.keenEyes,
  ),
  AchievementDef(
    id: 'streak_7',
    icon: 'fire',
    title: _t('Haftalık seri', 'Week streak'),
    description: _t(
      'Günün Delili\'ni 7 gün üst üste çöz.',
      'Solve the Daily Evidence 7 days in a row.',
    ),
    reward: 100,
    target: 7,
    measure: (p, c) => p.bestStreak,
  ),
  AchievementDef(
    id: 'streak_30',
    icon: 'alarm-clock',
    title: _t('Ayın dedektifi', 'Detective of the month'),
    description: _t(
      'Günün Delili\'ni 30 gün üst üste çöz.',
      'Solve the Daily Evidence 30 days in a row.',
    ),
    reward: 300,
    target: 30,
    measure: (p, c) => p.bestStreak,
  ),
  AchievementDef(
    id: 'stars_100',
    icon: 'glowing-star',
    title: _t('Yüz yıldız', 'A hundred stars'),
    description: _t('Toplam 100 yıldız topla.', 'Collect 100 stars in total.'),
    reward: 100,
    target: 100,
    measure: (p, c) => p.totalStars,
  ),
  AchievementDef(
    id: 'stars_250',
    icon: 'trophy',
    title: _t('Yıldız koleksiyoncusu', 'Star collector'),
    description: _t('Toplam 250 yıldız topla.', 'Collect 250 stars in total.'),
    reward: 250,
    target: 250,
    measure: (p, c) => p.totalStars,
  ),
  AchievementDef(
    id: 'club',
    icon: '1st-place-medal',
    title: _t('Kulüp üyesi', 'Club member'),
    description: _t('Dedektif Kulübü\'ne katıl.', 'Join the Detective Club.'),
    reward: 100,
    target: 1,
    measure: (p, c) => p.entitlements.clubActive ? 1 : 0,
  ),
];

/// Henuz acilmamis ama hedefi tutturulmus basarimlar.
List<AchievementDef> newlyEarned(Progress p, GameContent c) => [
  for (final a in achievements)
    if (!p.achievements.contains(a.id) && a.measure(p, c) >= a.target) a,
];
