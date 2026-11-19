import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import '../widgets/common.dart';
import 'case_hub_screen.dart';
import 'shop_screen.dart';

class CaseListScreen extends StatelessWidget {
  const CaseListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    return Scaffold(
      appBar: AppBar(
        title: Text(s.caseFiles),
        actions: const [CoinChip(), SizedBox(width: 12)],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            for (final c in gs.content.cases)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: CaseFolderCard(caseFile: c),
              ),
          ],
        ),
      ),
    );
  }
}

/// Dosya klasoru gorunumlu dava karti.
class CaseFolderCard extends StatelessWidget {
  const CaseFolderCard({super.key, required this.caseFile});

  final CaseFile caseFile;

  void _open(BuildContext context, GameState gs) {
    final s = S.of(context);
    if (!gs.hasAccess(caseFile)) {
      Navigator.push(
        context,
        MaterialPageRoute<void>(builder: (_) => const ShopScreen()),
      );
      return;
    }
    if (!gs.isPreviousClosed(caseFile)) {
      showSnack(context, caseFile.premium ? s.lockedFirstCase : s.lockedPrev);
      return;
    }
    Navigator.push(
      context,
      MaterialPageRoute<void>(
        builder: (_) => CaseHubScreen(caseFile: caseFile),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final gs = context.watch<GameState>();
    final c = caseFile;
    final rec = gs.record(c);
    final access = gs.hasAccess(c);
    final playable = access && gs.isPreviousClosed(c);
    final subtitle = !access
        ? s.premiumLocked
        : !playable
        ? (c.premium ? s.lockedFirstCase : s.lockedPrev)
        : rec.solved
        ? s.caseClosed
        : s.boardsSolved(gs.solvedBoardCount(c), c.boards.length);

    return InkWell(
      onTap: () => _open(context, gs),
      borderRadius: BorderRadius.circular(10),
      child: Container(
        decoration: BoxDecoration(
          color: playable ? const Color(0xFFC9A36B) : const Color(0xFF6E5A40),
          borderRadius: const BorderRadius.only(
            topRight: Radius.circular(10),
            bottomLeft: Radius.circular(10),
            bottomRight: Radius.circular(10),
          ),
          boxShadow: const [
            BoxShadow(
              color: Colors.black54,
              blurRadius: 6,
              offset: Offset(0, 3),
            ),
          ],
        ),
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Opacity(
              opacity: playable ? 1 : 0.55,
              child: SvgIcon(c.assetPath, size: 52),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(
                        s.caseN(c.order),
                        style: const TextStyle(
                          color: Noir.ink,
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                        ),
                      ),
                      if (c.premium) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 1,
                          ),
                          decoration: BoxDecoration(
                            color: Noir.ink,
                            borderRadius: BorderRadius.circular(4),
                          ),
                          child: Text(
                            s.premium,
                            style: const TextStyle(
                              color: Noir.brass,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  Text(
                    c.title.of(s.lang),
                    style: const TextStyle(
                      fontFamily: displayFont,
                      fontSize: 21,
                      fontWeight: FontWeight.w700,
                      color: Noir.ink,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(color: Noir.ink, fontSize: 13),
                  ),
                ],
              ),
            ),
            if (!playable)
              const Icon(Icons.lock, color: Noir.ink)
            else if (rec.solved)
              StarRow(rec.stars)
            else
              const Icon(Icons.chevron_right, color: Noir.ink),
          ],
        ),
      ),
    );
  }
}
