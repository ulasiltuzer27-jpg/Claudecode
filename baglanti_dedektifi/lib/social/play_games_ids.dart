/// Google Play Games kimlikleri. Play Console > Play Games hizmetleri >
/// Basarimlar / Siralama tablolari bolumunde olusturulduktan sonra buraya
/// yazilir (docs/play_store.md). Bos kimlik = Play Games'e gonderilmez.
///
/// Anahtarlar lib/game/achievements.dart icindeki yerel kimliklerdir.
abstract final class PlayGamesIds {
  /// `--dart-define=PLAY_GAMES=true` ile derlenmedikce Play Games kapali.
  static const enabled = bool.fromEnvironment('PLAY_GAMES');

  static const totalStarsLeaderboard = '';
  static const bestStreakLeaderboard = '';

  static const achievements = <String, String>{
    'first_board': '',
    'boards_25': '',
    'boards_100': '',
    'case_1': '',
    'case_3': '',
    'season_finale': '',
    'special_files': '',
    'perfect_case': '',
    'one_shot': '',
    'keen_10': '',
    'keen_50': '',
    'streak_7': '',
    'streak_30': '',
    'stars_100': '',
    'stars_250': '',
    'club': '',
  };
}
