import 'package:flutter/foundation.dart';
import 'package:games_services/games_services.dart';

import 'play_games.dart';
import 'play_games_ids.dart';

/// Play Games yalnizca `PLAY_GAMES=true` ile derlendiyse kullanilir.
/// Aksi halde eklentiye hic dokunulmaz (SDK baslatilmaz, oturum acilmaz).
PlayGamesService createPlatformPlayGames() =>
    PlayGamesIds.enabled ? GamesServicesPlayGames() : NoopPlayGames();

class GamesServicesPlayGames implements PlayGamesService {
  bool _signedIn = false;
  int _lastStars = -1;
  int _lastStreak = -1;

  @override
  bool get enabled => _signedIn;

  @override
  Future<void> init(Iterable<String> unlockedAchievements) async {
    try {
      await GamesServices.signIn();
      _signedIn = await GamesServices.isSignedIn;
    } catch (e) {
      debugPrint('Play Games oturumu acilamadi: $e');
      _signedIn = false;
    }
    if (!_signedIn) return;
    for (final id in unlockedAchievements) {
      await unlock(id);
    }
  }

  @override
  Future<void> unlock(String achievementId) async {
    final androidId = PlayGamesIds.achievements[achievementId] ?? '';
    if (!_signedIn || androidId.isEmpty) return;
    try {
      await GamesServices.unlock(
        achievement: Achievement(androidID: androidId),
      );
    } catch (e) {
      debugPrint('Basarim gonderilemedi ($achievementId): $e');
    }
  }

  @override
  Future<void> submitScores({
    required int totalStars,
    required int bestStreak,
  }) async {
    if (!_signedIn) return;
    if (totalStars > _lastStars &&
        PlayGamesIds.totalStarsLeaderboard.isNotEmpty) {
      _lastStars = totalStars;
      await _submit(PlayGamesIds.totalStarsLeaderboard, totalStars);
    }
    if (bestStreak > _lastStreak &&
        PlayGamesIds.bestStreakLeaderboard.isNotEmpty) {
      _lastStreak = bestStreak;
      await _submit(PlayGamesIds.bestStreakLeaderboard, bestStreak);
    }
  }

  Future<void> _submit(String leaderboard, int value) async {
    try {
      await GamesServices.submitScore(
        score: Score(androidLeaderboardID: leaderboard, value: value),
      );
    } catch (e) {
      debugPrint('Skor gonderilemedi: $e');
    }
  }

  @override
  Future<void> showLeaderboards() async {
    if (!_signedIn) return;
    try {
      await GamesServices.showLeaderboards();
    } catch (e) {
      debugPrint('Siralama acilamadi: $e');
    }
  }

  @override
  Future<void> showAchievements() async {
    if (!_signedIn) return;
    try {
      await GamesServices.showAchievements();
    } catch (e) {
      debugPrint('Basarimlar acilamadi: $e');
    }
  }
}
