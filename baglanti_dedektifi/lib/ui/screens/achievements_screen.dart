import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../game/achievements.dart';
import '../widgets/common.dart';

class AchievementsScreen extends StatelessWidget {
  const AchievementsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    final unlocked = gs.progress.achievements;
    // Acilanlar once, sonra en cok ilerlenenler.
    final sorted = [...achievements]
      ..sort((a, b) {
        final ua = unlocked.contains(a.id), ub = unlocked.contains(b.id);
        if (ua != ub) return ua ? -1 : 1;
        final ra = a.progressOf(gs.progress, gs.content) / a.target;
        final rb = b.progressOf(gs.progress, gs.content) / b.target;
        return rb.compareTo(ra);
      });

    return Scaffold(
      appBar: AppBar(
        title: Text(s.achievementsTitle),
        actions: [
          if (gs.playGames.enabled)
            IconButton(
              tooltip: s.leaderboards,
              onPressed: gs.playGames.showAchievements,
              icon: const Icon(Icons.sports_esports_outlined),
            ),
          const SizedBox(width: 8),
        ],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
          children: [
            Text(
              s.achievementsCount(unlocked.length, achievements.length),
              style: const TextStyle(
                color: Noir.brass,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 12),
            for (final a in sorted)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _AchievementTile(
                  def: a,
                  lang: s.lang,
                  unlocked: unlocked.contains(a.id),
                  value: a.progressOf(gs.progress, gs.content),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _AchievementTile extends StatelessWidget {
  const _AchievementTile({
    required this.def,
    required this.lang,
    required this.unlocked,
    required this.value,
  });

  final AchievementDef def;
  final String lang;
  final bool unlocked;
  final int value;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Semantics(
      label:
          '${def.title.of(lang)}. ${def.description.of(lang)} '
          '${unlocked ? '✓' : '$value/${def.target}'}',
      excludeSemantics: true,
      child: Card(
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: unlocked ? Noir.brass : Noir.outline),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Opacity(
                opacity: unlocked ? 1 : 0.4,
                child: SvgIcon(def.assetPath, size: 44),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      def.title.of(lang),
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 16,
                        color: unlocked ? Noir.brass : Noir.text,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      def.description.of(lang),
                      style: const TextStyle(
                        color: Noir.textMuted,
                        fontSize: 13,
                      ),
                    ),
                    const SizedBox(height: 6),
                    if (unlocked)
                      Text(
                        s.reward(def.reward),
                        style: const TextStyle(
                          color: Noir.success,
                          fontSize: 12,
                        ),
                      )
                    else
                      Row(
                        children: [
                          Expanded(
                            child: ClipRRect(
                              borderRadius: BorderRadius.circular(4),
                              child: LinearProgressIndicator(
                                value: value / def.target,
                                minHeight: 6,
                                backgroundColor: Noir.surfaceHigh,
                                color: Noir.brass,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            '$value/${def.target}',
                            style: const TextStyle(
                              color: Noir.textMuted,
                              fontSize: 12,
                            ),
                          ),
                        ],
                      ),
                  ],
                ),
              ),
              if (unlocked) const Icon(Icons.verified, color: Noir.brass),
            ],
          ),
        ),
      ),
    );
  }
}
