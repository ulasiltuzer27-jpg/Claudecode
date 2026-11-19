import 'reminder_service.dart';

ReminderService createPlatformReminders() => NoopReminderService(grant: false);
