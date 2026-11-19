// Gunluk giris odulu: 7 gunluk takvim. Saf fonksiyonlar.
import 'daily.dart';

/// Takvimin her gunundeki jeton.
const loginRewards = [20, 30, 40, 50, 60, 80, 150];

/// Bugun odul alinabilir mi? (Gunde bir kez.)
bool isLoginRewardAvailable({
  required String? lastDate,
  required DateTime today,
}) => lastDate != dateKey(today);

/// Bugun alinacak odulun takvimdeki sirasi (0-6).
/// Dun alindiysa bir sonraki gun; bir gun kacirildiysa ya da hic alinmadiysa
/// takvim basa doner; 7. gunden sonra yeniden 1. gun.
int loginRewardIndexFor({
  required String? lastDate,
  required int lastIndex,
  required DateTime today,
}) {
  final last = parseDateKey(lastDate);
  if (last == null) return 0;
  final gap = daysBetween(last, today);
  if (gap == 0) return lastIndex;
  if (gap == 1) return (lastIndex + 1) % loginRewards.length;
  return 0;
}
