import 'dart:math';

import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import '../../game/board_session.dart';
import '../../game/economy.dart';
import '../widgets/common.dart';
import '../widgets/evidence_board.dart';
import 'shop_screen.dart';

enum _ColdChoice { ad, coins, restart, quit }

/// Tek bir pano. [caseFile] yoksa Gunun Delili modudur.
/// Pano cozulurse `true` ile kapanir.
class BoardScreen extends StatefulWidget {
  const BoardScreen({super.key, required this.board, this.caseFile, this.boardIndex});

  final Board board;
  final CaseFile? caseFile;
  final int? boardIndex;

  bool get isDaily => caseFile == null;

  @override
  State<BoardScreen> createState() => _BoardScreenState();
}

class _BoardScreenState extends State<BoardScreen> with SingleTickerProviderStateMixin {
  late BoardSession session = _newSession();
  late final AnimationController _shake =
      AnimationController(vsync: this, duration: const Duration(milliseconds: 420));
  String? _message;
  bool _messageGood = false;
  bool _busy = false;

  BoardSession _newSession() =>
      BoardSession(widget.board, seed: Random().nextInt(1 << 31));

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _maybeTutorial());
  }

  @override
  void dispose() {
    _shake.dispose();
    super.dispose();
  }

  GameState get gs => context.read<GameState>();

  Future<void> _maybeTutorial() async {
    if (!mounted || gs.tutorialDone) return;
    final s = S.of(context);
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (context) => AlertDialog(
        title: Text(s.howToPlayTitle),
        content: Column(mainAxisSize: MainAxisSize.min, children: [
          for (final (i, t) in [s.howToPlay1, s.howToPlay2, s.howToPlay3].indexed)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                CircleAvatar(
                  radius: 12,
                  backgroundColor: Noir.thread,
                  child: Text('${i + 1}',
                      style: const TextStyle(color: Noir.paper, fontSize: 12, fontWeight: FontWeight.w700)),
                ),
                const SizedBox(width: 10),
                Expanded(child: Text(t)),
              ]),
            ),
        ]),
        actions: [
          FilledButton(onPressed: () => Navigator.pop(context), child: Text(s.gotIt)),
        ],
      ),
    );
    gs.finishTutorial();
  }

  void _say(String text, {bool good = false}) => setState(() {
        _message = text;
        _messageGood = good;
      });

  void _tap(String id) {
    if (_busy) return;
    if (session.toggle(id)) {
      haptic(context, HapticFeedback.selectionClick);
      setState(() => _message = null);
    }
  }

  void _longPress(String id) {
    if (_busy || widget.board.decoys.isEmpty) return;
    haptic(context, HapticFeedback.mediumImpact);
    setState(() => session.toggleFake(id));
  }

  Future<void> _submit() async {
    final s = S.of(context);
    final lang = s.lang;
    final result = session.submit();
    switch (result.outcome) {
      case SubmitOutcome.correct:
        haptic(context, HapticFeedback.mediumImpact);
        _say(s.fbCorrect(result.group!.theme.of(lang)), good: true);
        if (session.status == BoardStatus.solved) await _onSolved();
      case SubmitOutcome.oneAway:
        _wrongFeedback(s.fbOneAway);
      case SubmitOutcome.wrong:
        _wrongFeedback(s.fbWrong);
      case SubmitOutcome.repeated:
        _say(s.fbRepeated);
      case SubmitOutcome.invalidSize:
        break;
    }
    if (session.status == BoardStatus.failed) await _onFailed();
  }

  void _wrongFeedback(String text) {
    haptic(context, HapticFeedback.heavyImpact);
    _shake.forward(from: 0);
    _say(text);
  }

  Future<void> _hint() async {
    final s = S.of(context);
    if (!session.canHint) {
      showSnack(context, s.noMoreHints);
      return;
    }
    if (gs.payForHint()) {
      setState(() => session.revealHint());
      return;
    }
    final watch = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(s.hint),
        content: Text(s.hintAdOffer),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text(s.noThanks)),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(s.watchAd)),
        ],
      ),
    );
    if (watch != true || !mounted) return;
    setState(() => _busy = true);
    final earned = await gs.ads.showRewarded();
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (earned) session.revealHint();
    });
    if (!earned) showSnack(context, s.adNotReady);
  }

  Future<void> _onFailed() async {
    final s = S.of(context);
    while (mounted && session.status == BoardStatus.failed) {
      final choice = await showDialog<_ColdChoice>(
        context: context,
        barrierDismissible: false,
        builder: (context) => AlertDialog(
          title: Text(s.coldTitle),
          content: Text(s.coldBody),
          actionsOverflowDirection: VerticalDirection.down,
          actions: [
            FilledButton.icon(
              onPressed: () => Navigator.pop(context, _ColdChoice.ad),
              icon: const Icon(Icons.play_circle_outline),
              label: Text(s.watchAdContinue),
            ),
            OutlinedButton(
              onPressed: () => Navigator.pop(context, _ColdChoice.coins),
              child: Text(s.payContinue(Economy.continueCost)),
            ),
            TextButton(
              onPressed: () => Navigator.pop(context, _ColdChoice.restart),
              child: Text(s.restart),
            ),
            TextButton(
              onPressed: () => Navigator.pop(context, _ColdChoice.quit),
              child: Text(s.quit),
            ),
          ],
        ),
      );
      if (!mounted) return;
      switch (choice) {
        case _ColdChoice.ad:
          setState(() => _busy = true);
          final earned = await gs.ads.showRewarded();
          if (!mounted) return;
          setState(() {
            _busy = false;
            if (earned) session.forgive();
          });
          if (!earned) showSnack(context, s.adNotReady);
        case _ColdChoice.coins:
          if (gs.payForContinue()) {
            setState(() => session.forgive());
          } else {
            showSnack(context, s.notEnoughCoins);
          }
        case _ColdChoice.restart:
          setState(() {
            session = _newSession();
            _message = null;
          });
        case _ColdChoice.quit || null:
          Navigator.pop(context, false);
          return;
      }
    }
  }

  Future<void> _onSolved() async {
    final reward = widget.isDaily
        ? gs.completeDaily(session)
        : gs.completeCaseBoard(widget.caseFile!, session);
    await Future<void>.delayed(const Duration(milliseconds: 450));
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => BoardResultDialog(reward: reward, daily: widget.isDaily),
    );
    if (!mounted) return;
    if (gs.takeInterstitialSlot()) await gs.ads.showInterstitial();
    if (mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final lang = s.lang;
    final state = context.watch<GameState>();
    final caseFile = widget.caseFile;
    final title = caseFile == null
        ? s.daily
        : s.boardN((widget.boardIndex ?? 0) + 1, caseFile.boards.length);

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        actions: [
          CoinChip(
            onTap: () => Navigator.push(
                context, MaterialPageRoute<void>(builder: (_) => const ShopScreen())),
          ),
          const SizedBox(width: 12),
        ],
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            _StatusRow(
              session: session,
              freeHints: state.freeHintsLeft,
              onHint: _busy ? null : _hint,
            ),
            for (final g in session.activeLeads)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: _LeadChip(text: s.lead(g.theme.of(lang), g.size)),
              ),
            const SizedBox(height: 6),
            for (final g in session.solvedGroups)
              _SolvedStrip(
                group: g,
                colorIndex: widget.board.groups.indexOf(g),
                catalog: state.content.catalog,
                lang: lang,
              ),
            const SizedBox(height: 4),
            Expanded(
              child: AnimatedBuilder(
                animation: _shake,
                builder: (context, child) => Transform.translate(
                  offset: Offset(sin(_shake.value * pi * 6) * 8 * (1 - _shake.value), 0),
                  child: child,
                ),
                child: EvidenceBoard(
                  order: session.order,
                  selection: session.selection,
                  fakeMarks: session.fakeMarks,
                  catalog: state.content.catalog,
                  lang: lang,
                  onTap: _tap,
                  onLongPress: _longPress,
                ),
              ),
            ),
            SizedBox(
              height: 44,
              child: Center(
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 200),
                  child: Text(
                    _message ??
                        (widget.board.decoys.isNotEmpty && session.isPlaying
                            ? s.fakePresent(widget.board.decoys.length)
                            : ''),
                    key: ValueKey(_message),
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    style: TextStyle(
                      color: _message == null
                          ? Noir.textMuted
                          : (_messageGood ? Noir.success : Noir.paper),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
            ),
            Row(children: [
              IconButton.outlined(
                tooltip: s.shuffle,
                onPressed: _busy ? null : () => setState(session.shuffle),
                icon: const Icon(Icons.shuffle),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton(
                  style: OutlinedButton.styleFrom(padding: const EdgeInsets.symmetric(horizontal: 8)),
                  onPressed: session.selection.isEmpty || _busy
                      ? null
                      : () => setState(session.clearSelection),
                  child: FittedBox(fit: BoxFit.scaleDown, child: Text(s.clear, maxLines: 1)),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                flex: 2,
                child: FilledButton.icon(
                  onPressed: session.canSubmit && !_busy ? _submit : null,
                  icon: const Icon(Icons.link),
                  label: Text(s.link),
                ),
              ),
            ]),
          ]),
        ),
      ),
    );
  }
}

class _StatusRow extends StatelessWidget {
  const _StatusRow({required this.session, required this.freeHints, required this.onHint});

  final BoardSession session;
  final int freeHints;
  final VoidCallback? onHint;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Row(children: [
      Expanded(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Semantics(
            label: '${s.suspicion}: ${session.mistakes}/${session.maxMistakes}',
            child: Row(children: [
              Text('${s.suspicion} ', style: const TextStyle(color: Noir.textMuted, fontSize: 13)),
              for (var i = 0; i < session.maxMistakes; i++)
                Padding(
                  padding: const EdgeInsets.only(right: 4),
                  child: Icon(
                    i < session.mistakes ? Icons.visibility : Icons.visibility_outlined,
                    size: 18,
                    color: i < session.mistakes ? Noir.thread : Noir.outline,
                  ),
                ),
            ]),
          ),
          const SizedBox(height: 2),
          Text(
            '${s.groups}: ${session.remainingGroupSizes.join(' · ')}',
            style: const TextStyle(color: Noir.textMuted, fontSize: 13),
          ),
        ]),
      ),
      OutlinedButton.icon(
        onPressed: onHint,
        icon: const SvgIcon('assets/icons/magnifying-glass-tilted-left.svg', size: 20),
        label: Text(freeHints > 0 ? s.hintFree(freeHints) : '${Economy.hintCost}'),
      ),
    ]);
  }
}

class _LeadChip extends StatelessWidget {
  const _LeadChip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: Noir.surfaceHigh,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Noir.brass.withValues(alpha: 0.6)),
        ),
        child: Row(children: [
          const Icon(Icons.search, size: 16, color: Noir.brass),
          const SizedBox(width: 6),
          Expanded(child: Text(text, style: const TextStyle(color: Noir.brass, fontSize: 13))),
        ]),
      );
}

class _SolvedStrip extends StatelessWidget {
  const _SolvedStrip({
    required this.group,
    required this.colorIndex,
    required this.catalog,
    required this.lang,
  });

  final BoardGroup group;
  final int colorIndex;
  final Catalog catalog;
  final String lang;

  @override
  Widget build(BuildContext context) => TweenAnimationBuilder<double>(
        tween: Tween(begin: 0, end: 1),
        duration: const Duration(milliseconds: 350),
        builder: (context, t, child) => Opacity(
          opacity: t,
          child: Transform.translate(offset: Offset(0, (1 - t) * 12), child: child),
        ),
        child: Container(
          margin: const EdgeInsets.only(top: 6),
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Noir.groupColor(colorIndex),
            borderRadius: BorderRadius.circular(4),
            boxShadow: const [BoxShadow(color: Colors.black45, blurRadius: 3, offset: Offset(0, 2))],
          ),
          child: Row(children: [
            Expanded(
              child: Text(
                group.theme.of(lang),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(color: Noir.ink, fontWeight: FontWeight.w700),
              ),
            ),
            for (final id in group.items)
              Padding(
                padding: const EdgeInsets.only(left: 4),
                child: SvgIcon(catalog[id].assetPath, size: 20, semanticLabel: catalog[id].label.of(lang)),
              ),
          ]),
        ),
      );
}

/// Pano sonucu: yildizlar, jeton, ipucu karti, "reklamla ikiye katla".
class BoardResultDialog extends StatefulWidget {
  const BoardResultDialog({super.key, required this.reward, required this.daily});

  final BoardReward reward;
  final bool daily;

  @override
  State<BoardResultDialog> createState() => _BoardResultDialogState();
}

class _BoardResultDialogState extends State<BoardResultDialog> {
  bool _doubled = false;
  bool _busy = false;

  Future<void> _double() async {
    final gs = context.read<GameState>();
    setState(() => _busy = true);
    final earned = await gs.ads.showRewarded();
    if (!mounted) return;
    if (earned) gs.addCoins(widget.reward.coins);
    setState(() {
      _busy = false;
      _doubled = earned;
    });
    showSnack(context, earned ? S.of(context).doubled : S.of(context).adNotReady);
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final r = widget.reward;
    final clue = r.clue;
    return AlertDialog(
      title: Text(widget.daily ? s.dailySolvedTitle : s.solvedTitle, textAlign: TextAlign.center),
      content: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          StarRow(r.stars, size: 36),
          const SizedBox(height: 12),
          if (r.total > 0)
            Row(mainAxisAlignment: MainAxisAlignment.center, children: [
              const SvgIcon('assets/icons/coin.svg', size: 22),
              const SizedBox(width: 6),
              Text(s.plusCoins(_doubled ? r.coins * 2 + r.keenEyeBonus : r.total),
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
            ]),
          if (r.keenEyeBonus > 0)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(s.keenEye(r.keenEyeBonus),
                  textAlign: TextAlign.center, style: const TextStyle(color: Noir.success)),
            ),
          if (r.streak != null && r.total > 0)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                const SvgIcon('assets/icons/fire.svg', size: 18),
                const SizedBox(width: 6),
                Text(s.streak(r.streak!)),
              ]),
            ),
          if (!r.firstTime && !widget.daily)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(s.replayNote,
                  textAlign: TextAlign.center, style: const TextStyle(color: Noir.textMuted, fontSize: 13)),
            ),
          if (clue != null) ...[
            const SizedBox(height: 16),
            PaperCard(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(s.newClue,
                    style: const TextStyle(
                        color: Noir.thread, fontWeight: FontWeight.w700, fontSize: 12, letterSpacing: 1.5)),
                const SizedBox(height: 6),
                Text(clue.text.of(s.lang), style: const TextStyle(fontSize: 15)),
              ]),
            ),
          ],
        ]),
      ),
      actionsOverflowDirection: VerticalDirection.down,
      actions: [
        if (r.coins > 0 && !_doubled)
          OutlinedButton.icon(
            onPressed: _busy ? null : _double,
            icon: const Icon(Icons.play_circle_outline),
            label: Text(s.doubleCoins),
          ),
        FilledButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: Text(s.continueBtn),
        ),
      ],
    );
  }
}
