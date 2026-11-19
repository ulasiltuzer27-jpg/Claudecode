import 'dart:async';

import 'package:flutter/foundation.dart';

import '../audio/sound_service.dart';
import '../data/models.dart';
import '../game/accusation.dart';
import '../game/board_session.dart';
import '../game/achievements.dart';
import '../game/economy.dart';
import '../monetization/ads_service.dart';
import '../monetization/products.dart';
import '../monetization/purchase_service.dart';
import '../notify/reminder_plan.dart';
import '../notify/reminder_service.dart';
import '../progress/daily.dart';
import '../progress/login_reward.dart';
import '../progress/progress.dart';
import '../progress/progress_store.dart';
import '../social/play_games.dart';

/// Bir pano bitince oyuncuya gosterilenler.
class BoardReward {
  const BoardReward({
    required this.coins,
    required this.stars,
    required this.firstTime,
    this.keenEyeBonus = 0,
    this.clue,
    this.streak,
  });

  final int coins;
  final int stars;
  final bool firstTime;
  final int keenEyeBonus;
  final Clue? clue;

  /// Gunluk panoda yeni seri.
  final int? streak;

  int get total => coins + keenEyeBonus;
}

class AccusationResult {
  const AccusationResult(this.outcome, {this.stars = 0, this.coins = 0});

  final AccusationOutcome outcome;
  final int stars;
  final int coins;
}

/// Satin alma sonucunu arayuze iletmek icin.
enum StoreNotice { thanks, restored, pending, failed }

/// Ana ekrandaki "devam et" dugmesinin gidecegi yer.
class NextStep {
  const NextStep(this.caseFile, {this.boardIndex, this.accusation = false});

  final CaseFile caseFile;
  final int? boardIndex;
  final bool accusation;
}

/// Oyunun tek durum kaynagi: icerik + ilerleme + magaza + reklam.
class GameState extends ChangeNotifier {
  GameState({
    required this.content,
    required this.progress,
    required this.store,
    required this.ads,
    required this.purchases,
    SoundService? sound,
    ReminderService? reminders,
    PlayGamesService? playGames,
    this.deviceLanguage = 'tr',
    DateTime Function()? clock,
  }) : sound = sound ?? SilentSoundService(),
       reminders = reminders ?? NoopReminderService(),
       playGames = playGames ?? NoopPlayGames(),
       _clock = clock ?? DateTime.now {
    _purchaseSub = purchases.events.listen(_onPurchaseEvent);
  }

  final GameContent content;
  final Progress progress;
  final ProgressStore store;
  final AdsService ads;
  final PurchaseService purchases;
  final SoundService sound;
  final ReminderService reminders;
  final PlayGamesService playGames;

  /// Cihazin dili ('tr' ya da 'en'); bildirim metinleri icin.
  final String deviceLanguage;
  final DateTime Function() _clock;
  late final StreamSubscription<PurchaseEvent> _purchaseSub;

  final _notices = StreamController<StoreNotice>.broadcast();
  Stream<StoreNotice> get notices => _notices.stream;

  DateTime get now => _clock();
  String get todayKey => dateKey(now);

  final _achievementEvents = StreamController<AchievementDef>.broadcast();

  /// Yeni acilan basarimlar (arayuz bir bant gosterir).
  Stream<AchievementDef> get achievementUnlocks => _achievementEvents.stream;

  bool _checkingAchievements = false;

  /// Hedefi tutturulmus ama henuz acilmamis basarimlari acar ve odullerini
  /// verir. Ayni basarim iki kez odul vermez.
  void _checkAchievements() {
    if (_checkingAchievements) return;
    _checkingAchievements = true;
    try {
      for (final a in newlyEarned(progress, content)) {
        progress.achievements.add(a.id);
        progress.coins += a.reward;
        _achievementEvents.add(a);
        unawaited(playGames.unlock(a.id));
      }
    } finally {
      _checkingAchievements = false;
    }
  }

  void _changed() {
    _checkAchievements();
    notifyListeners();
    unawaited(store.save(progress));
    unawaited(
      playGames.submitScores(
        totalStars: totalStars,
        bestStreak: progress.bestStreak,
      ),
    );
  }

  @override
  void dispose() {
    _purchaseSub.cancel();
    _notices.close();
    _achievementEvents.close();
    super.dispose();
  }

  // --- Dil ve ayarlar ------------------------------------------------------

  /// Ayarlarda secilen dil; null ise cihaz dili kullanilir.
  String? get languageOverride => progress.settings.language;

  void setLanguage(String? lang) {
    progress.settings.language = lang;
    _changed();
  }

  bool get haptics => progress.settings.haptics;

  void setHaptics(bool on) {
    progress.settings.haptics = on;
    _changed();
  }

  bool get sfxOn => progress.settings.sfx;
  bool get musicOn => progress.settings.music;

  void setSfx(bool on) {
    progress.settings.sfx = on;
    sound.setSfxEnabled(on);
    _changed();
  }

  void setMusic(bool on) {
    progress.settings.music = on;
    sound.setMusicEnabled(on);
    _changed();
  }

  bool get tutorialDone => progress.tutorialDone;

  void finishTutorial() {
    progress.tutorialDone = true;
    _changed();
  }

  // --- Jeton, yildiz, rutbe ------------------------------------------------

  int get coins => progress.coins;
  int get totalStars => progress.totalStars;
  Rank get rank => Rank.forStars(totalStars);

  void addCoins(int n) {
    progress.coins += n;
    _changed();
  }

  bool spendCoins(int n) {
    if (progress.coins < n) return false;
    progress.coins -= n;
    _changed();
    return true;
  }

  // --- Erisim --------------------------------------------------------------

  Entitlements get entitlements => progress.entitlements;
  bool get clubActive => entitlements.clubActive;
  bool get noAds => entitlements.noAds;

  bool hasAccess(CaseFile c) =>
      !c.premium ||
      clubActive ||
      (c.packId != null && entitlements.ownedPacks.contains(c.packId));

  /// Ucretsiz davalar kendi aralarinda sirayla acilir. Premium "Ozel
  /// Dosyalar" ana hikayeyi beklemez: ilk dava kapaninca acilir (satin alan
  /// oyuncu sezon finalini beklemek zorunda kalmasin).
  bool isPreviousClosed(CaseFile c) {
    final free = content.cases.where((x) => !x.premium).toList();
    if (c.premium) {
      return free.isEmpty || record(free.first).solved;
    }
    final i = free.indexOf(c);
    if (i <= 0) return true;
    return record(free[i - 1]).solved;
  }

  bool isCasePlayable(CaseFile c) => hasAccess(c) && isPreviousClosed(c);

  CaseRecord record(CaseFile c) => progress.caseRecord(c.id);

  int? boardStars(Board b) => progress.boardStars[b.id];

  bool isBoardSolved(Board b) => progress.boardStars.containsKey(b.id);

  bool isBoardUnlocked(CaseFile c, int index) =>
      index == 0 || isBoardSolved(c.boards[index - 1]);

  int solvedBoardCount(CaseFile c) => c.boards.where(isBoardSolved).length;

  List<Clue> collectedClues(CaseFile c) => [
    for (final b in c.boards)
      if (isBoardSolved(b) && b.clueId != null) c.clue(b.clueId!),
  ];

  bool canAccuse(CaseFile c) => solvedBoardCount(c) == c.boards.length;

  void markIntroSeen(CaseFile c) {
    if (record(c).introSeen) return;
    record(c).introSeen = true;
    _changed();
  }

  NextStep? get nextStep {
    for (final c in content.cases) {
      if (record(c).solved) continue;
      if (!isCasePlayable(c)) return NextStep(c);
      for (final (i, b) in c.boards.indexed) {
        if (!isBoardSolved(b)) return NextStep(c, boardIndex: i);
      }
      return NextStep(c, accusation: true);
    }
    return null;
  }

  // --- Pano sonuclari ------------------------------------------------------

  BoardReward completeCaseBoard(CaseFile c, BoardSession session) {
    final board = session.board;
    final firstTime = !isBoardSolved(board);
    final stars = session.stars;
    final previous = progress.boardStars[board.id] ?? 0;
    if (stars > previous) progress.boardStars[board.id] = stars;

    final coins = firstTime ? Economy.boardReward(stars) : Economy.replayCoins;
    final keen = firstTime && session.keenEye ? Economy.keenEyeBonus : 0;
    if (keen > 0) progress.keenEyes++;
    progress.coins += coins + keen;
    _countBoard();
    _changed();
    return BoardReward(
      coins: coins,
      stars: stars,
      firstTime: firstTime,
      keenEyeBonus: keen,
      clue: firstTime && board.clueId != null ? c.clue(board.clueId!) : null,
    );
  }

  void _countBoard() {
    progress.boardsCompleted++;
    progress.boardsSinceInterstitial++;
  }

  /// Gecis reklami zamani geldi mi? Geldiyse sayaci sifirlar.
  bool takeInterstitialSlot() {
    if (noAds) return false;
    if (progress.boardsCompleted <= Economy.interstitialGracePeriodBoards) {
      return false;
    }
    if (progress.boardsSinceInterstitial < Economy.interstitialEveryBoards) {
      return false;
    }
    progress.boardsSinceInterstitial = 0;
    _changed();
    return true;
  }

  // --- Gunluk --------------------------------------------------------------

  Board get todaysBoard =>
      content.dailyBoards[dailyIndexFor(now, content.dailyBoards.length)];

  bool get dailyDoneToday => progress.lastDailyDate == todayKey;

  int get streak => visibleStreak(
    lastDate: progress.lastDailyDate,
    streak: progress.dailyStreak,
    today: now,
  );

  BoardReward completeDaily(BoardSession session) {
    if (dailyDoneToday) {
      // Ayni gun tekrar oynandi: odul yok.
      return BoardReward(
        coins: 0,
        stars: session.stars,
        firstTime: false,
        streak: streak,
      );
    }
    final newStreak = nextStreak(
      lastDate: progress.lastDailyDate,
      streak: progress.dailyStreak,
      today: now,
    );
    progress
      ..lastDailyDate = todayKey
      ..dailyStreak = newStreak;
    if (newStreak > progress.bestStreak) progress.bestStreak = newStreak;
    final coins = Economy.dailyReward(newStreak);
    final keen = session.keenEye ? Economy.keenEyeBonus : 0;
    progress.coins += coins + keen;
    _countBoard();
    _changed();
    unawaited(refreshReminders());
    return BoardReward(
      coins: coins,
      stars: session.stars,
      firstTime: true,
      keenEyeBonus: keen,
      streak: newStreak,
    );
  }

  // --- Buyutec ve devam ----------------------------------------------------

  int get freeHintsLeft {
    if (!clubActive) return 0;
    final used = progress.freeHintDay == todayKey ? progress.freeHintsUsed : 0;
    return (Economy.clubFreeHintsPerDay - used).clamp(
      0,
      Economy.clubFreeHintsPerDay,
    );
  }

  /// Buyutec bedelini oder: once kulubun bedava hakki, sonra jeton.
  bool payForHint() {
    if (freeHintsLeft > 0) {
      if (progress.freeHintDay != todayKey) {
        progress
          ..freeHintDay = todayKey
          ..freeHintsUsed = 0;
      }
      progress.freeHintsUsed++;
      _changed();
      return true;
    }
    return spendCoins(Economy.hintCost);
  }

  bool payForContinue() => spendCoins(Economy.continueCost);

  int get rewardedCoinsLeft {
    final used = progress.rewardedCoinsDay == todayKey
        ? progress.rewardedCoinsUsed
        : 0;
    return (Economy.rewardedCoinsPerDay - used).clamp(
      0,
      Economy.rewardedCoinsPerDay,
    );
  }

  /// Magazadaki odullu reklam. Reklam izlendiyse jeton verir.
  Future<bool> watchAdForCoins() async {
    if (rewardedCoinsLeft <= 0) return false;
    final earned = await showRewardedAd();
    if (!earned) return false;
    if (progress.rewardedCoinsDay != todayKey) {
      progress
        ..rewardedCoinsDay = todayKey
        ..rewardedCoinsUsed = 0;
    }
    progress.rewardedCoinsUsed++;
    progress.coins += Economy.rewardedCoins;
    _changed();
    return true;
  }

  // --- Suclama -------------------------------------------------------------

  AccusationResult accuse(CaseFile c, String suspectId, Set<String> clueIds) {
    final rec = record(c);
    final outcome = judgeAccusation(c, suspectId, clueIds);
    if (rec.solved) return AccusationResult(outcome, stars: rec.stars);
    rec.accusationAttempts++;
    if (outcome != AccusationOutcome.correct) {
      _changed();
      return AccusationResult(outcome);
    }
    rec
      ..solved = true
      ..stars = caseStarsForAttempts(rec.accusationAttempts);
    progress.coins += Economy.caseSolvedCoins;
    _changed();
    return AccusationResult(
      outcome,
      stars: rec.stars,
      coins: Economy.caseSolvedCoins,
    );
  }

  // --- Gunluk giris odulu --------------------------------------------------

  bool get loginRewardAvailable =>
      isLoginRewardAvailable(lastDate: progress.loginRewardDate, today: now);

  /// Bugun alinacak (ya da bugun alinmis) odulun takvimdeki sirasi.
  int get loginRewardIndex => loginRewardIndexFor(
    lastDate: progress.loginRewardDate,
    lastIndex: progress.loginRewardIndex,
    today: now,
  );

  /// Odulu verir; ayni gun ikinci kez cagrilirsa 0 doner.
  int claimLoginReward({bool doubled = false}) {
    if (!loginRewardAvailable) return 0;
    final index = loginRewardIndex;
    final coins = loginRewards[index] * (doubled ? 2 : 1);
    progress
      ..loginRewardIndex = index
      ..loginRewardDate = todayKey
      ..coins += coins;
    _changed();
    return coins;
  }

  // --- Hatirlatmalar --------------------------------------------------------

  bool get remindersOn => progress.settings.reminders;

  String get language => languageOverride ?? deviceLanguage;

  /// Yumusak soru zamani geldi mi: bir kez, ilk gunluk panodan ya da
  /// 3. panodan sonra.
  bool get shouldAskReminders =>
      !progress.settings.reminderAsked &&
      !remindersOn &&
      (progress.lastDailyDate != null || progress.boardsCompleted >= 3);

  void markRemindersAsked() {
    progress.settings.reminderAsked = true;
    _changed();
  }

  /// Sistem iznini ister; verildiyse hatirlatmalari acar.
  Future<bool> enableReminders() async {
    progress.settings.reminderAsked = true;
    final granted = await reminders.requestPermission();
    progress.settings.reminders = granted;
    _changed();
    await refreshReminders();
    return granted;
  }

  Future<void> disableReminders() async {
    progress.settings.reminders = false;
    _changed();
    await refreshReminders();
  }

  /// Onumuzdeki 7 gunun bildirimlerini yeniden kurar (ya da hepsini siler).
  Future<void> refreshReminders() async {
    try {
      if (!remindersOn) {
        await reminders.cancelAll();
        return;
      }
      await reminders.schedule(
        planReminders(
          now: now,
          doneToday: dailyDoneToday,
          streak: streak,
          lang: language,
        ),
      );
    } catch (e) {
      debugPrint('Hatirlatmalar kurulamadi: $e');
    }
  }

  // --- Reklam (muzik reklam boyunca susar) ----------------------------------

  Future<bool> showRewardedAd() async {
    sound.pauseMusic();
    try {
      return await ads.showRewarded();
    } finally {
      sound.resumeMusic();
    }
  }

  Future<void> showInterstitialAd() async {
    sound.pauseMusic();
    try {
      await ads.showInterstitial();
    } finally {
      sound.resumeMusic();
    }
  }

  // --- Servisler -----------------------------------------------------------

  /// Magaza ve reklamlari baslatir. Ilk kareden sonra cagrilir; hata olsa da
  /// oyun calismaya devam eder (cevrimdisi oynanabilir).
  Future<void> startServices() async {
    try {
      await sound.init(sfx: sfxOn, music: musicOn);
    } catch (e) {
      debugPrint('Ses baslatilamadi: $e');
    }
    try {
      await purchases.init();
      await syncEntitlements();
    } catch (e) {
      debugPrint('Magaza baslatilamadi: $e');
    }
    notifyListeners();
    try {
      await ads.init();
    } catch (e) {
      debugPrint('Reklamlar baslatilamadi: $e');
    }
    notifyListeners();
    if (remindersOn) {
      try {
        await reminders.init();
      } catch (e) {
        debugPrint('Bildirimler baslatilamadi: $e');
      }
      await refreshReminders();
    }
    // Eski kayitlar icin: guncellemeden once kazanilmis basarimlari ac.
    _changed();
    try {
      await playGames.init(progress.achievements);
      notifyListeners();
    } catch (e) {
      debugPrint('Play Games baslatilamadi: $e');
    }
  }

  // --- Magaza --------------------------------------------------------------

  Future<void> buy(String productId) => purchases.buy(productId);

  Future<void> restorePurchases() async {
    await purchases.restore();
    await syncEntitlements();
    _notices.add(StoreNotice.restored);
  }

  /// Acilista: magazadaki gercek durumu onbellege yansit.
  Future<void> syncEntitlements() async {
    final owned = await purchases.ownedProductIds();
    if (owned == null) return;
    final e = entitlements
      ..removeAds =
          owned.contains(ProductIds.removeAds) ||
          owned.contains(ProductIds.starterPack)
      ..starterBought = owned.contains(ProductIds.starterPack)
      ..clubActive = owned.any(ProductIds.subscriptions.contains);
    e.ownedPacks
      ..clear()
      ..addAll(owned.where(ProductIds.casePacks.contains));
    _changed();
  }

  void _onPurchaseEvent(PurchaseEvent event) {
    switch (event.kind) {
      case PurchaseEventKind.granted:
        _grant(event.productId, fresh: true);
        _notices.add(StoreNotice.thanks);
      case PurchaseEventKind.restored:
        _grant(event.productId, fresh: false);
      case PurchaseEventKind.pending:
        _notices.add(StoreNotice.pending);
      case PurchaseEventKind.error:
        _notices.add(StoreNotice.failed);
      case PurchaseEventKind.cancelled:
        break;
    }
  }

  void _grant(String? id, {required bool fresh}) {
    if (id == null) return;
    final e = entitlements;
    final coinPack = ProductIds.coinPacks[id];
    if (coinPack != null) {
      // Tuketilebilir urunler geri yuklenmez; yalnizca yeni satin alma.
      if (fresh) progress.coins += coinPack;
    } else if (id == ProductIds.removeAds) {
      e.removeAds = true;
    } else if (id == ProductIds.starterPack) {
      if (fresh && !e.starterBought) progress.coins += ProductIds.starterCoins;
      e
        ..starterBought = true
        ..removeAds = true;
    } else if (ProductIds.casePacks.contains(id)) {
      e.ownedPacks.add(id);
    } else if (ProductIds.subscriptions.contains(id)) {
      e.clubActive = true;
    }
    _changed();
  }

  List<CaseFile> casesInPack(String packId) =>
      content.cases.where((c) => c.packId == packId).toList();
}
