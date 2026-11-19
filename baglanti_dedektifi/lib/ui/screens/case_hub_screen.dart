import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import '../widgets/common.dart';
import 'accusation_screen.dart';
import 'board_screen.dart';
import 'story_screen.dart';

/// Bir davanin merkezi: panolar, supheliler, toplanan ipuclari, suclama.
class CaseHubScreen extends StatefulWidget {
  const CaseHubScreen({super.key, required this.caseFile, this.openBoard});

  final CaseFile caseFile;

  /// Ana ekrandaki "devam et" dogrudan bu panoyu acar.
  final int? openBoard;

  @override
  State<CaseHubScreen> createState() => _CaseHubScreenState();
}

class _CaseHubScreenState extends State<CaseHubScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      final gs = context.read<GameState>();
      if (!gs.record(widget.caseFile).introSeen) {
        await _readIntro();
      }
      if (!mounted) return;
      final open = widget.openBoard;
      if (open != null && gs.isBoardUnlocked(widget.caseFile, open)) await _openBoard(open);
    });
  }

  Future<void> _readIntro() async {
    await Navigator.push(
      context,
      MaterialPageRoute<bool>(builder: (_) => StoryScreen.intro(caseFile: widget.caseFile)),
    );
    if (mounted) context.read<GameState>().markIntroSeen(widget.caseFile);
  }

  Future<void> _openBoard(int index) async {
    final c = widget.caseFile;
    final solved = await Navigator.push(
      context,
      MaterialPageRoute<bool>(
        builder: (_) => BoardScreen(board: c.boards[index], caseFile: c, boardIndex: index),
      ),
    );
    if (!mounted || solved != true) return;
    final gs = context.read<GameState>();
    if (gs.canAccuse(c) && !gs.record(c).solved) showSnack(context, S.of(context).accuseReady);
  }

  Future<void> _accuse() async {
    final c = widget.caseFile;
    final stars = await Navigator.push(
      context,
      MaterialPageRoute<int>(builder: (_) => AccusationScreen(caseFile: c)),
    );
    if (!mounted || stars == null) return;
    await Navigator.push(
      context,
      MaterialPageRoute<bool>(builder: (_) => StoryScreen.ending(caseFile: c, stars: stars)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final lang = s.lang;
    final gs = context.watch<GameState>();
    final c = widget.caseFile;
    final rec = gs.record(c);
    final clues = gs.collectedClues(c);

    return Scaffold(
      appBar: AppBar(
        title: Text(c.title.of(lang)),
        actions: const [CoinChip(), SizedBox(width: 12)],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
          children: [
            Row(children: [
              SvgIcon(c.assetPath, size: 56),
              const SizedBox(width: 14),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(s.caseN(c.order), style: const TextStyle(color: Noir.brass)),
                  Text(s.boardsSolved(gs.solvedBoardCount(c), c.boards.length),
                      style: const TextStyle(color: Noir.textMuted)),
                  if (rec.solved) StarRow(rec.stars),
                ]),
              ),
              TextButton.icon(
                onPressed: _readIntro,
                icon: const Icon(Icons.menu_book_outlined),
                label: Text(s.readStory),
              ),
            ]),
            SectionTitle(s.boards),
            GridView.count(
              crossAxisCount: 5,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
              childAspectRatio: 0.8,
              children: [
                for (final (i, b) in c.boards.indexed)
                  _BoardTile(
                    number: i + 1,
                    stars: gs.boardStars(b),
                    unlocked: gs.isBoardUnlocked(c, i),
                    onTap: () => _openBoard(i),
                  ),
              ],
            ),
            const SizedBox(height: 16),
            if (rec.solved) ...[
              FilledButton.icon(
                onPressed: () => Navigator.push(
                  context,
                  MaterialPageRoute<bool>(
                      builder: (_) => StoryScreen.ending(caseFile: c, stars: rec.stars)),
                ),
                icon: const Icon(Icons.task_alt),
                label: Text('${s.caseClosed} · ${s.readEnding}'),
              ),
            ] else ...[
              FilledButton.icon(
                style: FilledButton.styleFrom(backgroundColor: Noir.thread, foregroundColor: Noir.paper),
                onPressed: gs.canAccuse(c) ? _accuse : null,
                icon: const Icon(Icons.gavel),
                label: Text(s.accuse),
              ),
              if (!gs.canAccuse(c))
                Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text(s.accuseLocked,
                      textAlign: TextAlign.center, style: const TextStyle(color: Noir.textMuted, fontSize: 13)),
                ),
            ],
            SectionTitle(s.clues(clues.length, c.clues.length)),
            if (clues.isEmpty)
              Text(s.noCluesYet, style: const TextStyle(color: Noir.textMuted))
            else
              for (final (i, clue) in clues.indexed)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: PaperCard(
                    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text('${i + 1}.',
                          style: const TextStyle(fontWeight: FontWeight.w700, color: Noir.thread)),
                      const SizedBox(width: 8),
                      Expanded(child: Text(clue.text.of(lang))),
                    ]),
                  ),
                ),
            SectionTitle(s.suspects),
            for (final sp in c.suspects)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: SuspectTile(suspect: sp, lang: lang),
              ),
          ],
        ),
      ),
    );
  }
}

class _BoardTile extends StatelessWidget {
  const _BoardTile({required this.number, required this.stars, required this.unlocked, required this.onTap});

  final int number;
  final int? stars;
  final bool unlocked;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final solved = stars != null;
    return Semantics(
      button: unlocked,
      label: '$number${solved ? ', $stars/3' : ''}',
      child: InkWell(
        onTap: unlocked ? onTap : null,
        borderRadius: BorderRadius.circular(8),
        child: Container(
          decoration: BoxDecoration(
            color: solved ? Noir.paper : (unlocked ? Noir.surfaceHigh : Noir.surface),
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: unlocked && !solved ? Noir.brass : Noir.outline, width: unlocked && !solved ? 2 : 1),
          ),
          child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
            if (!unlocked)
              const Icon(Icons.lock_outline, color: Noir.outline)
            else
              Text('$number',
                  style: TextStyle(
                      fontFamily: displayFont,
                      fontSize: 22,
                      fontWeight: FontWeight.w700,
                      color: solved ? Noir.ink : Noir.text)),
            if (solved) StarRow(stars!, size: 11),
          ]),
        ),
      ),
    );
  }
}
