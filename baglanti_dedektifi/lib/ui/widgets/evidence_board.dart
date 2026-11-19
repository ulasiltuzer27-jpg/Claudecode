import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import 'common.dart';

/// Mantar pano: kalan kartlar izgarada, secilenler kirmizi iple bagli.
///
/// Konumlar izgaradan hesaplanir; kartlar [AnimatedPositioned] ile kayar.
/// Bir grup cozulup kartlar panodan kalkinca, eski yerlerinde "hayalet"
/// kopyalari parlayip kuculerek yukaridaki seride dogru ucar.
class EvidenceBoard extends StatefulWidget {
  const EvidenceBoard({
    super.key,
    required this.order,
    required this.selection,
    required this.fakeMarks,
    required this.catalog,
    required this.lang,
    required this.onTap,
    required this.onLongPress,
  });

  final List<String> order;
  final List<String> selection;
  final Set<String> fakeMarks;
  final Catalog catalog;
  final String lang;
  final ValueChanged<String> onTap;
  final ValueChanged<String> onLongPress;

  static int columnsFor(int count) => count <= 9 ? 3 : 4;

  /// Her kart kendi kimligine gore hep ayni hafif egimde durur.
  static double tiltFor(String id) {
    final h = id.codeUnits.fold<int>(7, (a, c) => (a * 31 + c) & 0xFFFF);
    return ((h % 7) - 3) * 0.012;
  }

  @override
  State<EvidenceBoard> createState() => _EvidenceBoardState();
}

class _Ghost {
  _Ghost(this.cards, this.controller);

  final List<(String, Rect)> cards;
  final AnimationController controller;
}

class _EvidenceBoardState extends State<EvidenceBoard>
    with TickerProviderStateMixin {
  final List<_Ghost> _ghosts = [];
  Size _lastSize = Size.zero;

  @override
  void didUpdateWidget(EvidenceBoard old) {
    super.didUpdateWidget(old);
    final removed = old.order
        .where((id) => !widget.order.contains(id))
        .toList();
    if (removed.isEmpty || _lastSize == Size.zero) return;
    if (MediaQuery.maybeDisableAnimationsOf(context) ?? false) return;
    final layout = _GridLayout(old.order.length, _lastSize);
    final controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 560),
    );
    final ghost = _Ghost([
      for (final id in removed) (id, layout.rect(old.order.indexOf(id))),
    ], controller);
    _ghosts.add(ghost);
    controller.forward().whenComplete(() {
      if (mounted) setState(() => _ghosts.remove(ghost));
      controller.dispose();
    });
  }

  @override
  void dispose() {
    for (final g in _ghosts) {
      g.controller.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final w = widget;
    return Container(
      decoration: BoxDecoration(
        color: Noir.cork,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFF5C432E), width: 6),
        boxShadow: const [BoxShadow(color: Colors.black87, blurRadius: 10)],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(6),
        child: CustomPaint(
          painter: _CorkPainter(),
          child: LayoutBuilder(
            builder: (context, box) {
              _lastSize = box.biggest;
              final layout = _GridLayout(w.order.length, box.biggest);
              final pinFor = <String, Offset>{
                for (final (i, id) in w.order.indexed) id: layout.pin(i),
              };
              return Stack(
                clipBehavior: Clip.none,
                children: [
                  for (final (i, id) in w.order.indexed)
                    AnimatedPositioned(
                      key: ValueKey(id),
                      duration: const Duration(milliseconds: 320),
                      curve: Curves.easeOutCubic,
                      left: layout.rect(i).left,
                      top: layout.rect(i).top,
                      width: layout.rect(i).width,
                      height: layout.rect(i).height,
                      child: EvidenceCard(
                        item: w.catalog[id],
                        lang: w.lang,
                        selected: w.selection.contains(id),
                        fake: w.fakeMarks.contains(id),
                        tilt: EvidenceBoard.tiltFor(id),
                        onTap: () => w.onTap(id),
                        onLongPress: () => w.onLongPress(id),
                      ),
                    ),
                  Positioned.fill(
                    child: IgnorePointer(
                      child: CustomPaint(
                        painter: ThreadPainter([
                          for (final id in w.selection)
                            if (pinFor[id] != null) pinFor[id]!,
                        ]),
                      ),
                    ),
                  ),
                  for (final g in _ghosts)
                    for (final (id, rect) in g.cards)
                      _GhostCard(
                        key: ValueKey('ghost-$id'),
                        animation: g.controller,
                        rect: rect,
                        child: EvidenceCard(
                          item: w.catalog[id],
                          lang: w.lang,
                          selected: false,
                          solved: true,
                          fake: false,
                          tilt: 0,
                          onTap: () {},
                          onLongPress: () {},
                        ),
                      ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

/// Cozulen kartin hayaleti: kisa bir parlama, sonra kuculup yukari ucar.
class _GhostCard extends AnimatedWidget {
  const _GhostCard({
    super.key,
    required Animation<double> animation,
    required this.rect,
    required this.child,
  }) : super(listenable: animation);

  final Rect rect;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final t = (listenable as Animation<double>).value;
    final fly = Curves.easeInCubic.transform(((t - 0.25) / 0.75).clamp(0, 1));
    final pop = t < 0.25 ? 1 + 0.08 * sin(t / 0.25 * pi) : 1.0;
    final dy = -fly * (rect.top + rect.height);
    return Positioned(
      left: rect.left,
      top: rect.top + dy,
      width: rect.width,
      height: rect.height,
      child: IgnorePointer(
        child: Opacity(
          opacity: (1 - fly).clamp(0, 1),
          child: Transform.scale(scale: pop * (1 - 0.45 * fly), child: child),
        ),
      ),
    );
  }
}

class _GridLayout {
  _GridLayout(int count, Size size) {
    cols = EvidenceBoard.columnsFor(count);
    rows = max(1, (count / cols).ceil());
    const pad = 10.0;
    final cellW = (size.width - pad * 2) / cols;
    final cellH = min(cellW * 1.4, (size.height - pad * 2) / rows);
    final gridH = cellH * rows;
    origin = Offset(pad, pad + max(0, (size.height - pad * 2 - gridH) / 2));
    cell = Size(cellW, cellH);
  }

  late final int cols;
  late final int rows;
  late final Offset origin;
  late final Size cell;
  static const gap = 6.0;

  Rect rect(int i) {
    final c = i % cols, r = i ~/ cols;
    return Rect.fromLTWH(
      origin.dx + c * cell.width + gap / 2,
      origin.dy + r * cell.height + gap / 2,
      cell.width - gap,
      cell.height - gap,
    );
  }

  /// Iplik kartin ust ortasindaki raptiyeye baglanir.
  Offset pin(int i) {
    final r = rect(i);
    return Offset(r.center.dx, r.top + 9);
  }
}

/// Raptiyeler arasinda hafif sarkan kirmizi ip.
class ThreadPainter extends CustomPainter {
  ThreadPainter(this.points);

  final List<Offset> points;

  @override
  void paint(Canvas canvas, Size size) {
    if (points.length < 2) return;
    final shadow = Paint()
      ..color = Colors.black38
      ..strokeWidth = 3.4
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    final thread = Paint()
      ..color = Noir.thread
      ..strokeWidth = 2.6
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    final path = Path()..moveTo(points.first.dx, points.first.dy);
    for (var i = 1; i < points.length; i++) {
      final a = points[i - 1], b = points[i];
      final sag = min(22.0, (b - a).distance * 0.12);
      final mid = Offset((a.dx + b.dx) / 2, (a.dy + b.dy) / 2 + sag);
      path.quadraticBezierTo(mid.dx, mid.dy, b.dx, b.dy);
    }
    canvas.drawPath(path.shift(const Offset(1.5, 2.5)), shadow);
    canvas.drawPath(path, thread);
  }

  @override
  bool shouldRepaint(ThreadPainter old) =>
      old.points.length != points.length ||
      Iterable.generate(points.length).any((i) => old.points[i] != points[i]);
}

/// Mantar dokusu: koyu ve acik minik noktalar (sabit tohumla, titremez).
class _CorkPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final rnd = Random(11);
    final dark = Paint()..color = Noir.corkDark.withValues(alpha: 0.55);
    final light = Paint()
      ..color = const Color(0xFFB58C64).withValues(alpha: 0.5);
    final n = (size.width * size.height / 90).clamp(0, 6000).toInt();
    for (var i = 0; i < n; i++) {
      final p = Offset(
        rnd.nextDouble() * size.width,
        rnd.nextDouble() * size.height,
      );
      canvas.drawCircle(
        p,
        rnd.nextDouble() * 1.4 + 0.3,
        i.isEven ? dark : light,
      );
    }
  }

  @override
  bool shouldRepaint(_CorkPainter oldDelegate) => false;
}

/// Panodaki tek delil karti.
class EvidenceCard extends StatelessWidget {
  const EvidenceCard({
    super.key,
    required this.item,
    required this.lang,
    required this.selected,
    required this.fake,
    required this.tilt,
    required this.onTap,
    required this.onLongPress,
    this.solved = false,
  });

  final Item item;
  final String lang;
  final bool selected;

  /// Cozulen grubun karti (hayalet animasyonunda yesil cerceve).
  final bool solved;
  final bool fake;
  final double tilt;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  @override
  Widget build(BuildContext context) {
    final label = item.label.of(lang);
    final s = S.of(context);
    return Semantics(
      button: true,
      selected: selected,
      excludeSemantics: true,
      label: fake ? '$label, ${s.fakeStamp}' : label,
      onTap: onTap,
      onLongPress: onLongPress,
      child: GestureDetector(
        onTap: onTap,
        onLongPress: onLongPress,
        child: AnimatedScale(
          scale: selected ? 1.05 : 1,
          duration: const Duration(milliseconds: 140),
          child: Transform.rotate(
            angle: selected ? 0 : tilt,
            child: LayoutBuilder(
              builder: (context, box) {
                final iconSize = min(box.maxWidth * 0.52, box.maxHeight * 0.4);
                return Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Positioned.fill(
                      child: AnimatedContainer(
                        duration: const Duration(milliseconds: 140),
                        padding: const EdgeInsets.fromLTRB(4, 14, 4, 4),
                        decoration: BoxDecoration(
                          color: selected
                              ? const Color(0xFFFFF6E3)
                              : Noir.paper,
                          borderRadius: BorderRadius.circular(4),
                          border: Border.all(
                            color: solved
                                ? Noir.success
                                : (selected ? Noir.thread : Noir.paperShade),
                            width: selected || solved ? 2.5 : 1,
                          ),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(
                                alpha: selected ? 0.55 : 0.35,
                              ),
                              blurRadius: selected ? 9 : 4,
                              offset: Offset(0, selected ? 5 : 2),
                            ),
                          ],
                        ),
                        child: Column(
                          children: [
                            Expanded(
                              child: Center(
                                child: SvgIcon(item.assetPath, size: iconSize),
                              ),
                            ),
                            SizedBox(
                              height: box.maxHeight * 0.36,
                              child: Center(
                                child: Text(
                                  label,
                                  textAlign: TextAlign.center,
                                  maxLines: 3,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(
                                    color: Noir.ink,
                                    fontWeight: FontWeight.w700,
                                    fontSize: (box.maxWidth / 7.2).clamp(
                                      10.0,
                                      14.0,
                                    ),
                                    height: 1.05,
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    // Raptiye
                    Positioned(
                      top: 3,
                      left: 0,
                      right: 0,
                      child: Center(
                        child: Container(
                          width: 12,
                          height: 12,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: selected
                                ? Noir.thread
                                : const Color(0xFF8C8C8C),
                            boxShadow: const [
                              BoxShadow(
                                color: Colors.black45,
                                blurRadius: 2,
                                offset: Offset(1, 1),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                    if (fake)
                      Positioned.fill(
                        child: IgnorePointer(
                          child: Center(
                            child: TweenAnimationBuilder<double>(
                              tween: Tween(begin: 1.9, end: 1),
                              duration: const Duration(milliseconds: 230),
                              curve: Curves.easeOutBack,
                              builder: (context, scale, child) =>
                                  Transform.scale(scale: scale, child: child),
                              child: Transform.rotate(
                                angle: -0.35,
                                child: Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 6,
                                    vertical: 2,
                                  ),
                                  decoration: BoxDecoration(
                                    border: Border.all(
                                      color: Noir.thread,
                                      width: 2.5,
                                    ),
                                    borderRadius: BorderRadius.circular(4),
                                    color: Noir.paper.withValues(alpha: 0.7),
                                  ),
                                  child: Text(
                                    s.fakeStamp,
                                    style: const TextStyle(
                                      color: Noir.thread,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 13,
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}
