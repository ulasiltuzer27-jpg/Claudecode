import 'package:flutter_svg/flutter_svg.dart';
import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';

/// `assets/icons/` altindaki bir Fluent Emoji ikonu.
class SvgIcon extends StatelessWidget {
  const SvgIcon(this.assetPath, {super.key, this.size = 32, this.semanticLabel});

  final String assetPath;
  final double size;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) => SvgPicture.asset(
        assetPath,
        width: size,
        height: size,
        semanticsLabel: semanticLabel,
      );
}

/// Ust cubuktaki jeton gostergesi. Dokununca magazaya gider.
class CoinChip extends StatelessWidget {
  const CoinChip({super.key, this.onTap});

  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final coins = context.select<GameState, int>((g) => g.coins);
    return Semantics(
      button: onTap != null,
      label: S.of(context).coins(coins),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(20),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: Noir.surfaceHigh,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: Noir.outline),
          ),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            const SvgIcon('assets/icons/coin.svg', size: 18),
            const SizedBox(width: 6),
            Text('$coins',
                style: const TextStyle(fontWeight: FontWeight.w700, color: Noir.text)),
          ]),
        ),
      ),
    );
  }
}

class StarRow extends StatelessWidget {
  const StarRow(this.stars, {super.key, this.size = 16, this.max = 3});

  final int stars;
  final int max;
  final double size;

  @override
  Widget build(BuildContext context) => Semantics(
        label: '$stars/$max',
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < max; i++)
              Icon(i < stars ? Icons.star_rounded : Icons.star_outline_rounded,
                  size: size, color: i < stars ? Noir.brass : Noir.outline),
          ],
        ),
      );
}

class SectionTitle extends StatelessWidget {
  const SectionTitle(this.text, {super.key, this.trailing});

  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(0, 20, 0, 10),
        child: Row(children: [
          Expanded(child: Text(text, style: Theme.of(context).textTheme.titleLarge)),
          ?trailing,
        ]),
      );
}

/// Eski kagit gorunumlu kart (ipucu kartlari, hikaye).
class PaperCard extends StatelessWidget {
  const PaperCard({super.key, required this.child, this.padding = const EdgeInsets.all(14)});

  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) => Container(
        padding: padding,
        decoration: BoxDecoration(
          color: Noir.paper,
          borderRadius: BorderRadius.circular(6),
          boxShadow: const [BoxShadow(color: Colors.black54, blurRadius: 6, offset: Offset(0, 3))],
        ),
        child: DefaultTextStyle.merge(
          style: const TextStyle(color: Noir.ink, height: 1.35),
          child: child,
        ),
      );
}

void showSnack(BuildContext context, String text) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(text), duration: const Duration(seconds: 2)));
}

/// Ayarlarda acik ise hafif titresim.
void haptic(BuildContext context, Future<void> Function() feedback) {
  if (context.read<GameState>().haptics) feedback();
}
