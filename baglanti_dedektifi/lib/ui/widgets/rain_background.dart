import 'dart:math';

import 'package:material_ui/material_ui.dart';

/// Arka planda hafif, egik yagmur cizgileri (noir atmosferi).
/// "Animasyonlari azalt" aciksa hareket etmez.
class RainBackground extends StatefulWidget {
  const RainBackground({super.key, this.drops = 70});

  final int drops;

  @override
  State<RainBackground> createState() => _RainBackgroundState();
}

class _RainBackgroundState extends State<RainBackground>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: const Duration(seconds: 2),
  );
  late final List<_Drop> _drops;

  @override
  void initState() {
    super.initState();
    final r = Random(3);
    _drops = List.generate(widget.drops, (_) => _Drop(r));
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final still = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    if (still) {
      _c.stop();
    } else if (!_c.isAnimating) {
      _c.repeat();
    }
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => IgnorePointer(
    child: RepaintBoundary(
      child: AnimatedBuilder(
        animation: _c,
        builder: (context, _) =>
            CustomPaint(painter: _RainPainter(_drops, _c.value)),
      ),
    ),
  );
}

class _Drop {
  _Drop(Random r)
    : x = r.nextDouble(),
      offset = r.nextDouble(),
      speed = 0.8 + r.nextDouble() * 0.8,
      length = 14 + r.nextDouble() * 22,
      alpha = 0.04 + r.nextDouble() * 0.08;

  final double x, offset, speed, length, alpha;
}

class _RainPainter extends CustomPainter {
  _RainPainter(this.drops, this.t);

  final List<_Drop> drops;
  final double t;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..strokeWidth = 1.2
      ..strokeCap = StrokeCap.round;
    for (final d in drops) {
      final p = (d.offset + t * d.speed) % 1.0;
      final y = p * (size.height + 60) - 30;
      final x = d.x * (size.width + 80) - 40 - p * 40;
      paint.color = const Color(0xFFB9C6D6).withValues(alpha: d.alpha);
      canvas.drawLine(
        Offset(x, y),
        Offset(x - d.length * 0.25, y + d.length),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(_RainPainter old) => old.t != t;
}
