import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import '../../game/accusation.dart';
import '../widgets/common.dart';
import 'story_screen.dart';

/// Suclu + iki kanit secimi. Dogru suclamada dava yildiziyla kapanir.
class AccusationScreen extends StatefulWidget {
  const AccusationScreen({super.key, required this.caseFile});

  final CaseFile caseFile;

  @override
  State<AccusationScreen> createState() => _AccusationScreenState();
}

class _AccusationScreenState extends State<AccusationScreen> {
  String? _suspect;
  final Set<String> _clues = {};

  void _toggleClue(String id) => setState(() {
    if (!_clues.remove(id) && _clues.length < requiredProofCount) {
      _clues.add(id);
    }
  });

  Future<void> _accuse() async {
    final s = S.of(context);
    final gs = context.read<GameState>();
    final result = gs.accuse(widget.caseFile, _suspect!, _clues);
    if (result.outcome == AccusationOutcome.correct) {
      haptic(context, HapticFeedback.mediumImpact);
      Navigator.pop(context, result.stars);
      return;
    }
    haptic(context, HapticFeedback.heavyImpact);
    final weak = result.outcome == AccusationOutcome.weakEvidence;
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(weak ? s.accWeakTitle : s.accWrongTitle),
        content: Text(weak ? s.accWeakBody : s.accWrongBody),
        actions: [
          FilledButton(
            onPressed: () => Navigator.pop(context),
            child: Text(s.tryAgain),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final lang = s.lang;
    final c = widget.caseFile;
    final clues = context.watch<GameState>().collectedClues(c);
    final ready = _suspect != null && _clues.length == requiredProofCount;

    return Scaffold(
      appBar: AppBar(title: Text(s.accuse)),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text(
                    s.whoDidIt,
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 12),
                  for (final sp in c.suspects)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: SuspectTile(
                        suspect: sp,
                        lang: lang,
                        selected: _suspect == sp.id,
                        onTap: () => setState(() => _suspect = sp.id),
                      ),
                    ),
                  const SizedBox(height: 12),
                  Text(
                    '${s.pickTwo} (${_clues.length}/$requiredProofCount)',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 12),
                  for (final clue in clues)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _ClueChoice(
                        text: clue.text.of(lang),
                        selected: _clues.contains(clue.id),
                        onTap: () => _toggleClue(clue.id),
                      ),
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: Noir.thread,
                    foregroundColor: Noir.paper,
                  ),
                  onPressed: ready ? _accuse : null,
                  icon: const Icon(Icons.gavel),
                  label: Text(s.accuseNow),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ClueChoice extends StatelessWidget {
  const _ClueChoice({
    required this.text,
    required this.selected,
    required this.onTap,
  });

  final String text;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Semantics(
    checked: selected,
    child: GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: selected ? const Color(0xFFFFF6E3) : Noir.paper,
          borderRadius: BorderRadius.circular(6),
          border: Border.all(
            color: selected ? Noir.thread : Noir.paperShade,
            width: selected ? 2.5 : 1,
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              selected ? Icons.push_pin : Icons.push_pin_outlined,
              color: selected ? Noir.thread : Noir.inkSoft,
              size: 20,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                text,
                style: const TextStyle(color: Noir.ink, height: 1.35),
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
