import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../widgets/common.dart';
import '../widgets/daily_prompts.dart';
import '../widgets/rain_background.dart';
import 'board_screen.dart';
import 'case_hub_screen.dart';
import 'case_list_screen.dart';
import 'settings_screen.dart';
import 'shop_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) maybeShowLoginReward(context);
    });
  }

  void _push(BuildContext context, Widget screen) =>
      Navigator.push(context, MaterialPageRoute<void>(builder: (_) => screen));

  void _continue(BuildContext context, GameState gs) {
    final step = gs.nextStep;
    if (step == null) {
      _push(context, const CaseListScreen());
      return;
    }
    if (!gs.hasAccess(step.caseFile)) {
      _push(context, const ShopScreen());
      return;
    }
    _push(
      context,
      CaseHubScreen(caseFile: step.caseFile, openBoard: step.boardIndex),
    );
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    final rank = gs.rank;
    final next = rank.next;
    final step = gs.nextStep;

    return Scaffold(
      body: Stack(
        children: [
          const Positioned.fill(child: RainBackground()),
          SafeArea(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              children: [
                Row(
                  children: [
                    const CoinChip(),
                    const Spacer(),
                    IconButton(
                      tooltip: s.shop,
                      onPressed: () => _push(context, const ShopScreen()),
                      icon: const Icon(Icons.storefront_outlined),
                    ),
                    IconButton(
                      tooltip: s.settings,
                      onPressed: () => _push(context, const SettingsScreen()),
                      icon: const Icon(Icons.settings_outlined),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                const Center(
                  child: SvgIcon('assets/icons/detective.svg', size: 88),
                ),
                const SizedBox(height: 12),
                Text(
                  s.appName,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.displaySmall,
                ),
                const SizedBox(height: 4),
                Text(
                  s.tagline,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Noir.textMuted),
                ),
                const SizedBox(height: 20),
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(14),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.military_tech,
                          color: Noir.brass,
                          size: 36,
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                s.rankNames[rank.index],
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                  fontSize: 17,
                                ),
                              ),
                              const SizedBox(height: 4),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(4),
                                child: LinearProgressIndicator(
                                  value: next == null
                                      ? 1
                                      : (gs.totalStars - rank.minStars) /
                                            (next.minStars - rank.minStars),
                                  minHeight: 6,
                                  backgroundColor: Noir.surfaceHigh,
                                  color: Noir.brass,
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                next == null
                                    ? s.maxRank
                                    : s.starsToNext(
                                        next.minStars - gs.totalStars,
                                      ),
                                style: const TextStyle(
                                  color: Noir.textMuted,
                                  fontSize: 13,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          s.totalStars(gs.totalStars),
                          style: const TextStyle(color: Noir.brass),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  style: FilledButton.styleFrom(
                    minimumSize: const Size.fromHeight(60),
                  ),
                  onPressed: () => _continue(context, gs),
                  icon: const Icon(Icons.play_arrow_rounded, size: 28),
                  label: Column(
                    children: [
                      Text(
                        step == null ? s.caseFiles : s.continueInvestigation,
                        style: const TextStyle(fontSize: 18),
                      ),
                      if (step != null)
                        Text(
                          step.caseFile.title.of(s.lang) +
                              (step.boardIndex != null
                                  ? ' · ${s.boardN(step.boardIndex! + 1, step.caseFile.boards.length)}'
                                  : step.accusation
                                  ? ' · ${s.accuse}'
                                  : ''),
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w400,
                          ),
                        ),
                    ],
                  ),
                ),
                if (step == null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      s.allCasesClosed,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Noir.textMuted),
                    ),
                  ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: () => _push(context, const CaseListScreen()),
                  icon: const Icon(Icons.folder_open_outlined),
                  label: Text(s.caseFiles),
                ),
                const SizedBox(height: 16),
                _DailyCard(gs: gs),
                if (!gs.clubActive) ...[
                  const SizedBox(height: 16),
                  _ClubPromo(onTap: () => _push(context, const ShopScreen())),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _DailyCard extends StatelessWidget {
  const _DailyCard({required this.gs});

  final GameState gs;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final done = gs.dailyDoneToday;
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () async {
          final solved = await Navigator.push(
            context,
            MaterialPageRoute<bool>(
              builder: (_) => BoardScreen(board: gs.todaysBoard),
            ),
          );
          if (solved == true && context.mounted) {
            await maybeAskReminders(context);
          }
        },
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              const SvgIcon('assets/icons/calendar.svg', size: 44),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      s.daily,
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    Text(
                      done ? s.dailyDone : s.dailyPlay,
                      style: const TextStyle(color: Noir.textMuted),
                    ),
                  ],
                ),
              ),
              if (gs.streak > 0)
                Row(
                  children: [
                    const SvgIcon('assets/icons/fire.svg', size: 22),
                    const SizedBox(width: 2),
                    Text(
                      '${gs.streak}',
                      semanticsLabel: s.streak(gs.streak),
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 18,
                      ),
                    ),
                  ],
                )
              else if (done)
                const Icon(Icons.check_circle, color: Noir.success),
            ],
          ),
        ),
      ),
    );
  }
}

class _ClubPromo extends StatelessWidget {
  const _ClubPromo({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(14),
          gradient: const LinearGradient(
            colors: [Color(0xFF3B2A12), Color(0xFF5A3F17)],
          ),
          border: Border.all(color: Noir.brass),
        ),
        child: Row(
          children: [
            const SvgIcon('assets/icons/crown.svg', size: 40),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    s.clubTitle,
                    style: const TextStyle(
                      fontFamily: displayFont,
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: Noir.brass,
                    ),
                  ),
                  Text(
                    s.clubPromo,
                    style: const TextStyle(color: Noir.text, fontSize: 13),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: Noir.brass),
          ],
        ),
      ),
    );
  }
}
