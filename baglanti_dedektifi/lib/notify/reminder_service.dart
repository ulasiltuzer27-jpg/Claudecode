import 'reminder_plan.dart';

/// Hatirlatma bildirimleri. Gercegi Android'de
/// `reminder_service_mobile.dart`; web ve testlerde [NoopReminderService].
abstract class ReminderService {
  Future<void> init();

  /// Android 13+ sistem izni. Verildiyse true.
  Future<bool> requestPermission();

  /// Oncekileri siler, verilenleri zamanlar.
  Future<void> schedule(List<ReminderSlot> slots);

  Future<void> cancelAll();
}

class NoopReminderService implements ReminderService {
  NoopReminderService({this.grant = true});

  final bool grant;
  List<ReminderSlot> scheduled = const [];
  int cancels = 0;

  @override
  Future<void> init() async {}

  @override
  Future<bool> requestPermission() async => grant;

  @override
  Future<void> schedule(List<ReminderSlot> slots) async =>
      scheduled = List.of(slots);

  @override
  Future<void> cancelAll() async {
    cancels++;
    scheduled = const [];
  }
}
