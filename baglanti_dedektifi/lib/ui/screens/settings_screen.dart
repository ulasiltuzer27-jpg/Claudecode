import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../widgets/common.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    final current = gs.languageOverride;

    return Scaffold(
      appBar: AppBar(title: Text(s.settings)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: 8),
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
              child: Text(
                s.language,
                style: const TextStyle(
                  color: Noir.brass,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            RadioGroup<String?>(
              groupValue: current,
              onChanged: gs.setLanguage,
              child: Column(
                children: [
                  RadioListTile<String?>(
                    value: null,
                    title: Text(s.systemLanguage),
                  ),
                  const RadioListTile<String?>(
                    value: 'tr',
                    title: Text('Türkçe'),
                  ),
                  const RadioListTile<String?>(
                    value: 'en',
                    title: Text('English'),
                  ),
                ],
              ),
            ),
            const Divider(),
            SwitchListTile(
              value: gs.sfxOn,
              onChanged: gs.setSfx,
              title: Text(s.soundEffects),
            ),
            SwitchListTile(
              value: gs.musicOn,
              onChanged: gs.setMusic,
              title: Text(s.musicAmbience),
            ),
            SwitchListTile(
              value: gs.haptics,
              onChanged: gs.setHaptics,
              title: Text(s.haptics),
            ),
            SwitchListTile(
              value: gs.remindersOn,
              onChanged: (on) async {
                if (!on) {
                  await gs.disableReminders();
                  return;
                }
                final granted = await gs.enableReminders();
                if (!granted && context.mounted) {
                  showSnack(context, s.remindersDenied);
                }
              },
              title: Text(s.remindersSetting),
            ),
            const Divider(),
            if (gs.ads.privacyOptionsRequired)
              ListTile(
                leading: const Icon(Icons.privacy_tip_outlined),
                title: Text(s.privacyOptions),
                onTap: gs.ads.showPrivacyOptions,
              ),
            ListTile(
              leading: const Icon(Icons.policy_outlined),
              title: Text(s.privacyPolicy),
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute<void>(builder: (_) => const PrivacyScreen()),
              ),
            ),
            ListTile(
              leading: const Icon(Icons.description_outlined),
              title: Text(s.licenses),
              onTap: () =>
                  showLicensePage(context: context, applicationName: s.appName),
            ),
          ],
        ),
      ),
    );
  }
}

class PrivacyScreen extends StatelessWidget {
  const PrivacyScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(s.privacyPolicy)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            PaperCard(
              child: Text(s.privacyText, style: const TextStyle(fontSize: 15)),
            ),
          ],
        ),
      ),
    );
  }
}
