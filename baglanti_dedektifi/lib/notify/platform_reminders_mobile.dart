import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_timezone/flutter_timezone.dart';
import 'package:timezone/data/latest.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import 'reminder_plan.dart';
import 'reminder_service.dart';

ReminderService createPlatformReminders() => LocalReminderService();

/// flutter_local_notifications ile yerel, zamanlanmis bildirimler.
/// Kesin alarm izni istenmez; "yaklasik" zamanlama yeterli.
class LocalReminderService implements ReminderService {
  final _plugin = FlutterLocalNotificationsPlugin();
  bool _ready = false;

  static const _details = NotificationDetails(
    android: AndroidNotificationDetails(
      'daily_evidence',
      'Günün Delili',
      channelDescription: 'Günün Delili hazır olduğunda hatırlatır.',
    ),
  );

  @override
  Future<void> init() async {
    tzdata.initializeTimeZones();
    try {
      final info = await FlutterTimezone.getLocalTimezone();
      tz.setLocalLocation(tz.getLocation(info.identifier));
    } catch (e) {
      debugPrint('Saat dilimi okunamadi, UTC kullaniliyor: $e');
    }
    await _plugin.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('ic_notification'),
      ),
    );
    _ready = true;
  }

  AndroidFlutterLocalNotificationsPlugin? get _android => _plugin
      .resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin
      >();

  @override
  Future<bool> requestPermission() async {
    if (!_ready) await init();
    final android = _android;
    if (android == null) return false;
    final granted = await android.requestNotificationsPermission();
    // Android 12 ve oncesinde izin gerekmez: null doner.
    return granted ?? (await android.areNotificationsEnabled() ?? true);
  }

  @override
  Future<void> schedule(List<ReminderSlot> slots) async {
    if (!_ready) await init();
    await _plugin.cancelAll();
    for (final s in slots) {
      try {
        await _plugin.zonedSchedule(
          id: s.id,
          scheduledDate: tz.TZDateTime.from(s.at, tz.local),
          notificationDetails: _details,
          androidScheduleMode: AndroidScheduleMode.inexactAllowWhileIdle,
          title: s.title,
          body: s.body,
        );
      } catch (e) {
        debugPrint('Hatirlatma kurulamadi (${s.id}): $e');
      }
    }
  }

  @override
  Future<void> cancelAll() async {
    if (!_ready) await init();
    await _plugin.cancelAll();
  }
}
