import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../app/theme.dart';

/// Ekranin ustunden kisa bir konfeti patlamasi. Kendi kendini kaldirir.
/// Sistemde "animasyonlari azalt" aciksa hicbir sey yapmaz.
void showConfetti(BuildContext context, {int count = 90}) {
  if (MediaQuery.maybeDisableAnimationsOf(context) ?? false) return;
  final overlay = Overlay.maybeOf(context, rootOverlay: true);
  if (overlay == null) return;
  late final OverlayEntry entry;
  entry = OverlayEntry(
    builder: (_) => _ConfettiLayer(count: count, onDone: () => entry.remove()),
  );
  overlay.insert(entry);
}

class _Particle {
  _Particle(Random r)
    : x = 0.5 + (r.nextDouble() - 0.5) * 0.3,
      vx = (r.nextDouble() - 0.5) * 1.4,
      vy = -(0.9 + r.nextDouble() * 0.9),
      spin = (r.nextDouble() - 0.5) * 18,
      size = 6 + r.nextDouble() * 6,
      color = _colors[r.nextInt(_colors.length)];

  static final _colors = [
    ...Noir.groupColors,
    Noir.brass,
    Noir.thread,
    Noir.paper,
  ];

  final double x, vx, vy, spin, size;
  final Color color;
}

class _ConfettiLayer extends StatefulWidget {
  const _ConfettiLayer({required this.count, required this.onDone});

  final int count;
  final VoidCallback onDone;

  @override
  State<_ConfettiLayer> createState() => _ConfettiLayerState();
}

class _ConfettiLayerState extends State<_ConfettiLayer>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1900),
  );
  late final List<_Particle> _particles;

  @override
  void initState() {
    super.initState();
    final r = Random();
    _particles = List.generate(widget.count, (_) => _Particle(r));
    _c.forward().whenComplete(widget.onDone);
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => IgnorePointer(
    child: AnimatedBuilder(
      animation: _c,
      builder: (context, _) => CustomPaint(
        size: MediaQuery.sizeOf(context),
        painter: _ConfettiPainter(_particles, _c.value),
      ),
    ),
  );
}

class _ConfettiPainter extends CustomPainter {
  _ConfettiPainter(this.particles, this.t);

  final List<_Particle> particles;
  final double t;

  @override
  void paint(Canvas canvas, Size size) {
    const gravity = 2.2;
    final paint = Paint();
    final fadeOut = t > 0.7 ? (1 - t) / 0.3 : 1.0;
    for (final p in particles) {
      final secs = t * 1.9;
      final x = (p.x + p.vx * secs * 0.5) * size.width;
      final y =
          size.height * 0.32 +
          (p.vy * secs + 0.5 * gravity * secs * secs) * size.height * 0.35;
      paint.color = p.color.withValues(alpha: fadeOut.clamp(0, 1));
      canvas.save();
      canvas.translate(x, y);
      canvas.rotate(p.spin * secs);
      canvas.drawRect(
        Rect.fromCenter(
          center: Offset.zero,
          width: p.size,
          height: p.size * 0.55,
        ),
        paint,
      );
      canvas.restore();
    }
  }

  @override
  bool shouldRepaint(_ConfettiPainter old) => old.t != t;
}
