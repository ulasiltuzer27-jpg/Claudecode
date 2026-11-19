/// Google Play Games (basarim + siralama). Yapilandirilmadiysa [NoopPlayGames]:
/// oyun ici basarimlar yine calisir, yalnizca Play Games'e gonderilmez.
abstract class PlayGamesService {
  /// Play Games derlemede acik ve oturum acilabildi mi?
  bool get enabled;

  /// Sessiz oturum acma + yerelde acik basarimlari esitleme.
  Future<void> init(Iterable<String> unlockedAchievements);

  Future<void> unlock(String achievementId);

  /// Yalnizca deger arttiysa gonderir.
  Future<void> submitScores({required int totalStars, required int bestStreak});

  Future<void> showLeaderboards();

  Future<void> showAchievements();
}

class NoopPlayGames implements PlayGamesService {
  @override
  bool get enabled => false;

  @override
  Future<void> init(Iterable<String> unlockedAchievements) async {}

  @override
  Future<void> unlock(String achievementId) async {}

  @override
  Future<void> submitScores({
    required int totalStars,
    required int bestStreak,
  }) async {}

  @override
  Future<void> showLeaderboards() async {}

  @override
  Future<void> showAchievements() async {}
}
