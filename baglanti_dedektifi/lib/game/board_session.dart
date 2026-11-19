import 'dart:math';

import '../data/models.dart';

enum SubmitOutcome {
  /// Secim kalan bir grupla birebir ayni.
  correct,

  /// Bir kart disinda hepsi ayni gruptan.
  oneAway,

  /// Yanlis bag.
  wrong,

  /// Ayni yanlis secim daha once denenmisti; ceza yok.
  repeated,

  /// Secim boyutu kalan hicbir grubun boyutuna uymuyor.
  invalidSize,
}

class SubmitResult {
  const SubmitResult(this.outcome, [this.group]);

  final SubmitOutcome outcome;

  /// [SubmitOutcome.correct] ise cozulen grup.
  final BoardGroup? group;
}

enum BoardStatus { playing, solved, failed }

/// Tek bir panonun oyun durumu. Saf Dart; arayuz bu nesneyi okur ve
/// yalnizca kamuya acik metotlarla degistirir.
class BoardSession {
  BoardSession(this.board, {int? seed, this.maxMistakes = defaultMaxMistakes})
    : _random = Random(seed) {
    order = board.allItems.toList()..shuffle(_random);
  }

  static const defaultMaxMistakes = 4;

  final Board board;
  final int maxMistakes;
  final Random _random;

  /// Kalan kartlarin ekrandaki sirasi.
  late List<String> order;

  /// Cozulme sirasina gore grup kimlikleri.
  final List<String> solvedGroupIds = [];

  /// Secilen kartlar, secilme sirasiyla (ip bu sirayla cekilir).
  final List<String> selection = [];

  /// Oyuncunun "sahte" diye damgaladigi kartlar.
  final Set<String> fakeMarks = {};

  /// Buyutecle temasi acilan gruplar.
  final List<String> revealedGroupIds = [];

  final Set<String> _tried = {};

  /// Su anki hata sayisi; [maxMistakes]'a ulasinca iz soguk.
  int mistakes = 0;

  /// Toplam hata; [forgive] bunu silmez, yildizlar buna gore hesaplanir.
  int totalMistakes = 0;
  int hintsUsed = 0;

  BoardStatus get status {
    if (solvedGroupIds.length == board.groups.length) return BoardStatus.solved;
    if (mistakes >= maxMistakes) return BoardStatus.failed;
    return BoardStatus.playing;
  }

  bool get isPlaying => status == BoardStatus.playing;

  List<BoardGroup> get solvedGroups => [
    for (final id in solvedGroupIds) board.groups.firstWhere((g) => g.id == id),
  ];

  List<BoardGroup> get unsolvedGroups =>
      board.groups.where((g) => !solvedGroupIds.contains(g.id)).toList();

  /// Kalan grup boyutlari, buyukten kucuge.
  List<int> get remainingGroupSizes =>
      unsolvedGroups.map((g) => g.size).toList()
        ..sort((a, b) => b.compareTo(a));

  int get maxSelectable =>
      unsolvedGroups.fold(0, (m, g) => g.size > m ? g.size : m);

  bool isSelected(String itemId) => selection.contains(itemId);

  /// Karti secer ya da secimi kaldirir. Secim yapilabildiyse true doner.
  bool toggle(String itemId) {
    if (!isPlaying || !order.contains(itemId)) return false;
    if (selection.remove(itemId)) return true;
    if (selection.length >= maxSelectable) return false;
    selection.add(itemId);
    return true;
  }

  void clearSelection() => selection.clear();

  bool get canSubmit =>
      isPlaying && remainingGroupSizes.contains(selection.length);

  SubmitResult submit() {
    if (!canSubmit) return const SubmitResult(SubmitOutcome.invalidSize);
    final picked = selection.toSet();

    for (final g in unsolvedGroups) {
      if (g.size == picked.length && g.items.toSet().containsAll(picked)) {
        solvedGroupIds.add(g.id);
        if (solvedGroupIds.length == board.groups.length) {
          _keenEye =
              board.decoys.isNotEmpty &&
              fakeMarks.length == board.decoys.length &&
              fakeMarks.containsAll(board.decoys);
        }
        order.removeWhere(picked.contains);
        fakeMarks.removeAll(picked);
        selection.clear();
        return SubmitResult(SubmitOutcome.correct, g);
      }
    }

    final key = (picked.toList()..sort()).join('|');
    if (!_tried.add(key)) return const SubmitResult(SubmitOutcome.repeated);

    mistakes++;
    totalMistakes++;
    final oneAway = unsolvedGroups.any(
      (g) =>
          g.size == picked.length &&
          picked.where(g.items.contains).length == picked.length - 1,
    );
    return SubmitResult(oneAway ? SubmitOutcome.oneAway : SubmitOutcome.wrong);
  }

  /// En kolay, henuz acilmamis ve cozulmemis grubun temasini acar.
  BoardGroup? revealHint() {
    if (!isPlaying) return null;
    for (final g in unsolvedGroups) {
      if (!revealedGroupIds.contains(g.id)) {
        revealedGroupIds.add(g.id);
        hintsUsed++;
        return g;
      }
    }
    return null;
  }

  bool get canHint =>
      isPlaying && unsolvedGroups.any((g) => !revealedGroupIds.contains(g.id));

  /// Acik ama henuz cozulmemis ipucu temalari.
  List<BoardGroup> get activeLeads => [
    for (final id in revealedGroupIds)
      if (!solvedGroupIds.contains(id))
        board.groups.firstWhere((g) => g.id == id),
  ];

  void toggleFake(String itemId) {
    if (!isPlaying || !order.contains(itemId)) return;
    if (!fakeMarks.remove(itemId)) fakeMarks.add(itemId);
  }

  /// "Iz soguk" ekranindan devam: hatalardan [count] tanesini siler.
  void forgive([int count = 2]) {
    mistakes = max(0, mistakes - count);
  }

  void shuffle() => order.shuffle(_random);

  /// 3: hatasiz ve ipucusuz, 2: en fazla 1 hata ya da ipucu kullanildi,
  /// 1: geri kalan her durum.
  int get stars {
    if (totalMistakes == 0 && hintsUsed == 0) return 3;
    if (totalMistakes <= 1) return 2;
    return 1;
  }

  /// Sahte delillerin hepsi, baska hicbir kart damgalanmadan bulundu mu?
  /// Son grup cozuldugu anda hesaplanir; o andan sonra kalan kartlar zaten
  /// sahte oldugu icin damganin ondan once vurulmus olmasi gerekir.
  bool get keenEye => _keenEye;
  bool _keenEye = false;
}
