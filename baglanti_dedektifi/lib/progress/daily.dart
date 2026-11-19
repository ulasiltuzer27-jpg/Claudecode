// Gunun Delili: hangi gun hangi pano, seri nasil sayilir. Saf fonksiyonlar.

String dateKey(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-'
    '${d.month.toString().padLeft(2, '0')}-'
    '${d.day.toString().padLeft(2, '0')}';

DateTime? parseDateKey(String? key) {
  if (key == null) return null;
  final p = key.split('-');
  if (p.length != 3) return null;
  return DateTime(int.parse(p[0]), int.parse(p[1]), int.parse(p[2]));
}

/// Takvim gunu farki (yaz saati gecislerinden etkilenmez).
int daysBetween(DateTime a, DateTime b) {
  final ua = DateTime.utc(a.year, a.month, a.day);
  final ub = DateTime.utc(b.year, b.month, b.day);
  return ub.difference(ua).inDays;
}

final _epoch = DateTime(2026, 1, 1);

/// O gunun panosunun listedeki sirasi. Herkes ayni gun ayni panoyu gorur.
int dailyIndexFor(DateTime day, int boardCount) {
  if (boardCount <= 0) return 0;
  final n = daysBetween(_epoch, day);
  return ((n % boardCount) + boardCount) % boardCount;
}

/// Bugun cozulunce yeni seri degeri.
/// Dun cozulduyse +1, bugun zaten cozulduyse ayni, aksi halde 1.
int nextStreak({required String? lastDate, required int streak, required DateTime today}) {
  final last = parseDateKey(lastDate);
  if (last == null) return 1;
  final gap = daysBetween(last, today);
  if (gap == 0) return streak;
  if (gap == 1) return streak + 1;
  return 1;
}

/// Ekranda gosterilecek seri: dunden beri cozulmediyse seri kopmustur.
int visibleStreak({required String? lastDate, required int streak, required DateTime today}) {
  final last = parseDateKey(lastDate);
  if (last == null) return 0;
  return daysBetween(last, today) <= 1 ? streak : 0;
}
