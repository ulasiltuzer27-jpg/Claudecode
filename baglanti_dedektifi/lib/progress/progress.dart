import '../game/economy.dart';

/// Bir davanin kalici durumu.
class CaseRecord {
  CaseRecord({
    this.introSeen = false,
    this.accusationAttempts = 0,
    this.solved = false,
    this.stars = 0,
  });

  bool introSeen;
  int accusationAttempts;
  bool solved;
  int stars;

  Map<String, Object?> toJson() => {
    'introSeen': introSeen,
    'attempts': accusationAttempts,
    'solved': solved,
    'stars': stars,
  };

  factory CaseRecord.fromJson(Map<String, Object?> j) => CaseRecord(
    introSeen: (j['introSeen'] as bool?) ?? false,
    accusationAttempts: (j['attempts'] as int?) ?? 0,
    solved: (j['solved'] as bool?) ?? false,
    stars: (j['stars'] as int?) ?? 0,
  );
}

/// Satin alinan haklar. Kaynak her zaman magazadir; bu yalnizca onbellek.
class Entitlements {
  Entitlements({
    this.removeAds = false,
    this.clubActive = false,
    this.starterBought = false,
    Set<String>? ownedPacks,
  }) : ownedPacks = ownedPacks ?? {};

  bool removeAds;
  bool clubActive;
  bool starterBought;
  final Set<String> ownedPacks;

  bool get noAds => removeAds || clubActive;

  Map<String, Object?> toJson() => {
    'removeAds': removeAds,
    'club': clubActive,
    'starter': starterBought,
    'packs': ownedPacks.toList()..sort(),
  };

  factory Entitlements.fromJson(Map<String, Object?> j) => Entitlements(
    removeAds: (j['removeAds'] as bool?) ?? false,
    clubActive: (j['club'] as bool?) ?? false,
    starterBought: (j['starter'] as bool?) ?? false,
    ownedPacks: ((j['packs'] as List?) ?? const []).cast<String>().toSet(),
  );
}

class Settings {
  Settings({
    this.language,
    this.haptics = true,
    this.sfx = true,
    this.music = true,
    this.reminders = false,
    this.reminderAsked = false,
  });

  /// null: cihaz dili.
  String? language;
  bool haptics;
  bool sfx;
  bool music;

  /// Gunun Delili hatirlatmasi. Oyuncu "evet" demeden acilmaz.
  bool reminders;

  /// Yumusak izin sorusu bir kez soruldu mu?
  bool reminderAsked;

  Map<String, Object?> toJson() => {
    'language': language,
    'haptics': haptics,
    'sfx': sfx,
    'music': music,
    'reminders': reminders,
    'reminderAsked': reminderAsked,
  };

  factory Settings.fromJson(Map<String, Object?> j) => Settings(
    language: j['language'] as String?,
    haptics: (j['haptics'] as bool?) ?? true,
    sfx: (j['sfx'] as bool?) ?? true,
    music: (j['music'] as bool?) ?? true,
    reminders: (j['reminders'] as bool?) ?? false,
    reminderAsked: (j['reminderAsked'] as bool?) ?? false,
  );
}

/// Kaydedilen tum oyuncu durumu. Tek bir JSON olarak saklanir.
class Progress {
  Progress({
    this.coins = Economy.startingCoins,
    Map<String, int>? boardStars,
    Map<String, CaseRecord>? cases,
    this.boardsCompleted = 0,
    this.boardsSinceInterstitial = 0,
    this.keenEyes = 0,
    this.lastDailyDate,
    this.dailyStreak = 0,
    this.bestStreak = 0,
    this.tutorialDone = false,
    this.freeHintDay,
    this.freeHintsUsed = 0,
    this.rewardedCoinsDay,
    this.rewardedCoinsUsed = 0,
    this.loginRewardDate,
    this.loginRewardIndex = 0,
    Set<String>? achievements,
    Entitlements? entitlements,
    Settings? settings,
  }) : achievements = achievements ?? {},
       boardStars = boardStars ?? {},
       cases = cases ?? {},
       entitlements = entitlements ?? Entitlements(),
       settings = settings ?? Settings();

  static const schemaVersion = 1;

  int coins;

  /// Pano kimligi -> en iyi yildiz.
  final Map<String, int> boardStars;
  final Map<String, CaseRecord> cases;

  /// Bitirilen toplam pano (dava + gunluk). Reklam sikligi icin.
  int boardsCompleted;
  int boardsSinceInterstitial;
  int keenEyes;

  /// `yyyy-mm-dd`, yerel saat.
  String? lastDailyDate;
  int dailyStreak;
  int bestStreak;
  bool tutorialDone;

  /// Kulup bedava buyutecinin sayildigi gun.
  String? freeHintDay;
  int freeHintsUsed;

  /// Magazadaki odullu reklam jetonunun sayildigi gun.
  String? rewardedCoinsDay;
  int rewardedCoinsUsed;

  /// Gunluk giris odulunun en son alindigi gun ve takvimdeki sirasi (0-6).
  String? loginRewardDate;
  int loginRewardIndex;

  /// Acilan basarimlarin kimlikleri.
  final Set<String> achievements;

  final Entitlements entitlements;
  final Settings settings;

  CaseRecord caseRecord(String caseId) =>
      cases.putIfAbsent(caseId, CaseRecord.new);

  int get totalStars =>
      boardStars.values.fold(0, (a, b) => a + b) +
      cases.values.fold(0, (a, c) => a + c.stars);

  Map<String, Object?> toJson() => {
    'v': schemaVersion,
    'coins': coins,
    'boardStars': boardStars,
    'cases': cases.map((k, v) => MapEntry(k, v.toJson())),
    'boardsCompleted': boardsCompleted,
    'boardsSinceAd': boardsSinceInterstitial,
    'keenEyes': keenEyes,
    'lastDaily': lastDailyDate,
    'streak': dailyStreak,
    'bestStreak': bestStreak,
    'tutorialDone': tutorialDone,
    'freeHintDay': freeHintDay,
    'freeHintsUsed': freeHintsUsed,
    'adCoinsDay': rewardedCoinsDay,
    'adCoinsUsed': rewardedCoinsUsed,
    'loginDate': loginRewardDate,
    'loginIndex': loginRewardIndex,
    'achievements': achievements.toList()..sort(),
    'ent': entitlements.toJson(),
    'settings': settings.toJson(),
  };

  factory Progress.fromJson(Map<String, Object?> j) {
    Map<String, Object?> obj(String k) =>
        ((j[k] as Map?) ?? const {}).cast<String, Object?>();
    return Progress(
      coins: (j['coins'] as int?) ?? Economy.startingCoins,
      boardStars: obj('boardStars').map((k, v) => MapEntry(k, v as int)),
      cases: obj('cases').map(
        (k, v) => MapEntry(
          k,
          CaseRecord.fromJson((v as Map).cast<String, Object?>()),
        ),
      ),
      boardsCompleted: (j['boardsCompleted'] as int?) ?? 0,
      boardsSinceInterstitial: (j['boardsSinceAd'] as int?) ?? 0,
      keenEyes: (j['keenEyes'] as int?) ?? 0,
      lastDailyDate: j['lastDaily'] as String?,
      dailyStreak: (j['streak'] as int?) ?? 0,
      bestStreak: (j['bestStreak'] as int?) ?? 0,
      tutorialDone: (j['tutorialDone'] as bool?) ?? false,
      freeHintDay: j['freeHintDay'] as String?,
      freeHintsUsed: (j['freeHintsUsed'] as int?) ?? 0,
      rewardedCoinsDay: j['adCoinsDay'] as String?,
      rewardedCoinsUsed: (j['adCoinsUsed'] as int?) ?? 0,
      loginRewardDate: j['loginDate'] as String?,
      loginRewardIndex: (j['loginIndex'] as int?) ?? 0,
      achievements: ((j['achievements'] as List?) ?? const [])
          .cast<String>()
          .toSet(),
      entitlements: Entitlements.fromJson(obj('ent')),
      settings: Settings.fromJson(obj('settings')),
    );
  }
}
