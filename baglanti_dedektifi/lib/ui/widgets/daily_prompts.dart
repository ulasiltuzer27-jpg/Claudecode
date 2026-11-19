import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../audio/sound_service.dart';
import '../../progress/login_reward.dart';
import 'common.dart';
import 'confetti.dart';

/// Gunun ilk acilisinda giris odulunu gosterir (alinabiliyorsa).
Future<void> maybeShowLoginReward(BuildContext context) async {
  final gs = context.read<GameState>();
  if (!gs.loginRewardAvailable) return;
  await showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) => const LoginRewardDialog(),
  );
}

/// Uygun anda, bir kez: "Gunun Delili icin hatirlatalim mi?"
/// Sistem izni yalnizca oyuncu "evet" derse istenir.
Future<void> maybeAskReminders(BuildContext context) async {
  final gs = context.read<GameState>();
  if (!gs.shouldAskReminders) return;
  final s = S.of(context);
  final yes = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      icon: const SvgIcon('assets/icons/alarm-clock.svg', size: 48),
      title: Text(s.remindersTitle),
      content: Text(s.remindersAsk),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: Text(s.notNow),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, true),
          child: Text(s.remindYes),
        ),
      ],
    ),
  );
  if (!context.mounted) return;
  if (yes != true) {
    gs.markRemindersAsked();
    return;
  }
  final granted = await gs.enableReminders();
  if (!granted && context.mounted) showSnack(context, s.remindersDenied);
}

class LoginRewardDialog extends StatefulWidget {
  const LoginRewardDialog({super.key});

  @override
  State<LoginRewardDialog> createState() => _LoginRewardDialogState();
}

class _LoginRewardDialogState extends State<LoginRewardDialog> {
  bool _busy = false;

  Future<void> _claim({required bool doubled}) async {
    final gs = context.read<GameState>();
    final s = S.of(context);
    setState(() => _busy = true);
    var double = false;
    if (doubled) {
      double = await gs.showRewardedAd();
      if (!mounted) return;
      if (!double) showSnack(context, s.adNotReady);
    }
    final bigDay = gs.loginRewardIndex == loginRewards.length - 1;
    final coins = gs.claimLoginReward(doubled: double);
    gs.sound.play(Sfx.coin);
    if (!mounted) return;
    if (bigDay) showConfetti(context);
    Navigator.pop(context);
    showSnack(context, s.plusCoins(coins));
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    final today = gs.loginRewardIndex;
    return AlertDialog(
      title: Text(s.loginTitle, textAlign: TextAlign.center),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 8,
              runSpacing: 8,
              children: [
                for (var i = 0; i < loginRewards.length; i++)
                  _DayBox(
                    day: i + 1,
                    coins: loginRewards[i],
                    past: i < today,
                    today: i == today,
                    big: i == loginRewards.length - 1,
                  ),
              ],
            ),
            const SizedBox(height: 14),
            Text(
              s.loginNote,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Noir.textMuted, fontSize: 13),
            ),
          ],
        ),
      ),
      actionsOverflowDirection: VerticalDirection.down,
      actions: [
        OutlinedButton.icon(
          onPressed: _busy ? null : () => _claim(doubled: true),
          icon: const Icon(Icons.play_circle_outline),
          label: Text(s.claimDouble),
        ),
        FilledButton(
          onPressed: _busy ? null : () => _claim(doubled: false),
          child: Text('${s.claim} · ${s.plusCoins(loginRewards[today])}'),
        ),
      ],
    );
  }
}

class _DayBox extends StatelessWidget {
  const _DayBox({
    required this.day,
    required this.coins,
    required this.past,
    required this.today,
    required this.big,
  });

  final int day;
  final int coins;
  final bool past;
  final bool today;
  final bool big;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Semantics(
      label: '${s.loginDay(day)}: ${s.coins(coins)}',
      selected: today,
      child: Container(
        width: big ? 132 : 62,
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 4),
        decoration: BoxDecoration(
          color: today ? Noir.paper : (past ? Noir.surfaceHigh : Noir.surface),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: today ? Noir.brass : Noir.outline,
            width: today ? 2.5 : 1,
          ),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              s.loginDay(day),
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: today ? Noir.ink : Noir.textMuted,
              ),
            ),
            const SizedBox(height: 4),
            if (past)
              const Icon(Icons.check_circle, color: Noir.success, size: 26)
            else
              SvgIcon(
                big ? 'assets/icons/money-bag.svg' : 'assets/icons/coin.svg',
                size: 26,
              ),
            const SizedBox(height: 2),
            Text(
              '$coins',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: today ? Noir.ink : Noir.text,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
