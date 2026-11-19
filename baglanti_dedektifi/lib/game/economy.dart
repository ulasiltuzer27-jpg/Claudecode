/// Oyun ekonomisinin tum sayilari tek yerde. Denge ayari burada yapilir.
abstract final class Economy {
  static const startingCoins = 120;

  /// Pano odulu: taban + yildiz basina.
  static const boardBaseCoins = 5;
  static const boardCoinsPerStar = 5;

  /// Sahte delillerin hepsini dogru damgalama bonusu.
  static const keenEyeBonus = 15;

  /// Dava ilk kez dogru suclamayla kapaninca.
  static const caseSolvedCoins = 60;

  static const dailyBaseCoins = 20;
  static const dailyStreakBonusPerDay = 5;
  static const dailyStreakBonusCap = 7;

  /// Buyutec (bir grubun temasini acar).
  static const hintCost = 30;

  /// "Iz soguk" ekranindan jetonla devam.
  static const continueCost = 50;

  /// Kulup uyelerine her gun bedava verilen buyutec.
  static const clubFreeHintsPerDay = 5;

  /// Magazadaki "reklam izle, jeton kazan" odulu ve gunluk siniri.
  static const rewardedCoins = 25;
  static const rewardedCoinsPerDay = 5;

  /// Daha once cozulmus bir pano tekrar oynaninca verilen jeton.
  static const replayCoins = 3;

  /// Gecis reklami: bu kadar pano bitmeden hic gosterilmez...
  static const interstitialGracePeriodBoards = 5;

  /// ...sonra her bu kadar panoda bir.
  static const interstitialEveryBoards = 3;

  static int boardReward(int stars) => boardBaseCoins + boardCoinsPerStar * stars;

  static int dailyReward(int streak) {
    final bonusDays = streak.clamp(0, dailyStreakBonusCap);
    return dailyBaseCoins + dailyStreakBonusPerDay * bonusDays;
  }
}

/// Toplam yildiza gore dedektif rutbesi.
class Rank {
  const Rank(this.index, this.minStars);

  final int index;
  final int minStars;

  static const all = [
    Rank(0, 0),
    Rank(1, 15),
    Rank(2, 40),
    Rank(3, 80),
    Rank(4, 130),
    Rank(5, 200),
  ];

  static Rank forStars(int stars) =>
      all.lastWhere((r) => stars >= r.minStars, orElse: () => all.first);

  Rank? get next => index + 1 < all.length ? all[index + 1] : null;
}
