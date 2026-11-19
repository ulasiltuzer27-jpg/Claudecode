// Gunun Delili hatirlatmalari: hangi gun, saat kacta, hangi metinle.
// Saf fonksiyon; bildirim eklentisi yalnizca bu listeyi zamanlar.

class ReminderSlot {
  const ReminderSlot({
    required this.id,
    required this.at,
    required this.title,
    required this.body,
  });

  final int id;

  /// Yerel saat.
  final DateTime at;
  final String title;
  final String body;
}

const reminderHour = 19;
const reminderDays = 7;
const _baseId = 1000;

/// Onumuzdeki [days] gun icin aksam [hour]'da birer bildirim.
/// Bugunun panosu cozulduyse ya da saat gectiyse bugun atlanir.
List<ReminderSlot> planReminders({
  required DateTime now,
  required bool doneToday,
  required int streak,
  required String lang,
  int days = reminderDays,
  int hour = reminderHour,
}) {
  final tr = lang == 'tr';
  final generic = [
    (
      tr ? 'Günün Delili hazır 🔎' : "Today's evidence is ready 🔎",
      tr
          ? 'Yeni bir pano seni bekliyor. Kaç yıldız alacaksın?'
          : 'A new board is waiting. How many stars will you get?',
    ),
    (
      tr ? 'Dedektif, şehir seni bekliyor' : 'Detective, the city needs you',
      tr
          ? "Liman Şehri'nde yeni bir delil bulundu."
          : 'New evidence has turned up in Harbor City.',
    ),
    (
      tr
          ? 'Bugünün bağlantısını bulabilir misin?'
          : "Can you find today's connection?",
      tr
          ? 'On altı kart, dört grup. Kırmızı ip hazır.'
          : 'Sixteen cards, four groups. The red thread is ready.',
    ),
  ];

  final slots = <ReminderSlot>[];
  for (var d = 0; d < days; d++) {
    final at = DateTime(now.year, now.month, now.day + d, hour);
    if (d == 0 && (doneToday || !now.isBefore(at))) continue;
    // Seri yalnizca bugun ve yarin icin anlamli; sonrasi oyuncuya bagli.
    final streakForDay = d == 0 ? streak : (d == 1 && doneToday ? streak : 0);
    final (title, body) = streakForDay > 0
        ? (
            tr
                ? '🔥 $streakForDay günlük serin var!'
                : '🔥 You have a $streakForDay-day streak!',
            tr
                ? 'Bugünün panosunu çöz, serin bozulmasın.'
                : "Solve today's board to keep it going.",
          )
        : generic[(at.day + at.month) % generic.length];
    slots.add(ReminderSlot(id: _baseId + d, at: at, title: title, body: body));
  }
  return slots;
}
