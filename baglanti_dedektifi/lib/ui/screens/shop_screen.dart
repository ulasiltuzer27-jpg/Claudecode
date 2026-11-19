import 'package:material_ui/material_ui.dart';
import 'package:provider/provider.dart';

import '../../app/game_state.dart';
import '../../app/strings.dart';
import '../../app/theme.dart';
import '../../game/economy.dart';
import '../../monetization/products.dart';
import '../widgets/common.dart';

class ShopScreen extends StatefulWidget {
  const ShopScreen({super.key});

  @override
  State<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends State<ShopScreen> {
  bool _adBusy = false;

  Future<void> _watchForCoins() async {
    final gs = context.read<GameState>();
    final s = S.of(context);
    setState(() => _adBusy = true);
    final ok = await gs.watchAdForCoins();
    if (!mounted) return;
    setState(() => _adBusy = false);
    showSnack(context, ok ? s.plusCoins(Economy.rewardedCoins) : s.adNotReady);
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final lang = s.lang;
    final gs = context.watch<GameState>();
    final products = gs.purchases.products;
    final e = gs.entitlements;
    String? price(String id) => products[id]?.price;
    final packTitles = gs
        .casesInPack(ProductIds.casePack1)
        .map((c) => c.title.of(lang))
        .join(', ');

    return Scaffold(
      appBar: AppBar(
        title: Text(s.shop),
        actions: const [CoinChip(), SizedBox(width: 12)],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
          children: [
            if (StoreConfig.fakeStore)
              Container(
                margin: const EdgeInsets.only(bottom: 12),
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: Noir.surfaceHigh,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Noir.brass.withValues(alpha: 0.5)),
                ),
                child: Text(
                  s.testStore,
                  style: const TextStyle(color: Noir.brass, fontSize: 13),
                ),
              ),
            if (!gs.purchases.available)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Text(
                  s.storeUnavailable,
                  style: const TextStyle(color: Noir.thread),
                ),
              ),
            _ClubCard(
              active: gs.clubActive,
              weekly: price(ProductIds.clubWeekly),
              yearly: price(ProductIds.clubYearly),
              onBuy: gs.buy,
            ),
            const SizedBox(height: 16),
            if (!e.starterBought && !e.removeAds)
              _OfferTile(
                icon: 'assets/icons/wrapped-gift.svg',
                title: s.starterTitle,
                body: s.starterBody,
                price: price(ProductIds.starterPack),
                onBuy: () => gs.buy(ProductIds.starterPack),
                highlight: true,
              ),
            _OfferTile(
              icon: 'assets/icons/prohibited.svg',
              title: s.removeAdsTitle,
              body: s.removeAdsBody,
              price: price(ProductIds.removeAds),
              owned: e.noAds,
              onBuy: () => gs.buy(ProductIds.removeAds),
            ),
            if (packTitles.isNotEmpty)
              _OfferTile(
                icon: 'assets/icons/card-index-dividers.svg',
                title: s.casePackTitle,
                body: s.casePackBody(packTitles),
                price: price(ProductIds.casePack1),
                owned:
                    gs.clubActive ||
                    e.ownedPacks.contains(ProductIds.casePack1),
                onBuy: () => gs.buy(ProductIds.casePack1),
              ),
            SectionTitle(s.coinPacks),
            Card(
              child: ListTile(
                leading: const Icon(
                  Icons.play_circle_outline,
                  color: Noir.brass,
                  size: 32,
                ),
                title: Text(s.freeCoins(Economy.rewardedCoins)),
                subtitle: Text(s.freeCoinsLeft(gs.rewardedCoinsLeft)),
                trailing: _adBusy
                    ? const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.chevron_right),
                enabled: gs.rewardedCoinsLeft > 0 && !_adBusy,
                onTap: _watchForCoins,
              ),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                for (final (i, entry)
                    in ProductIds.coinPacks.entries.indexed) ...[
                  if (i > 0) const SizedBox(width: 10),
                  Expanded(
                    child: _CoinPack(
                      coins: entry.value,
                      price: price(entry.key),
                      onBuy: () => gs.buy(entry.key),
                    ),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 20),
            Center(
              child: TextButton(
                onPressed: gs.restorePurchases,
                child: Text(s.restore),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ClubCard extends StatelessWidget {
  const _ClubCard({
    required this.active,
    required this.weekly,
    required this.yearly,
    required this.onBuy,
  });

  final bool active;
  final String? weekly;
  final String? yearly;
  final Future<void> Function(String) onBuy;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF3B2A12), Color(0xFF1E160C)],
        ),
        border: Border.all(color: Noir.brass, width: 1.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const SvgIcon('assets/icons/crown.svg', size: 36),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  s.clubTitle,
                  style: const TextStyle(
                    fontFamily: displayFont,
                    fontSize: 22,
                    fontWeight: FontWeight.w700,
                    color: Noir.brass,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          for (final perk in [s.clubPerk1, s.clubPerk2, s.clubPerk3])
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Row(
                children: [
                  const Icon(Icons.check, color: Noir.brass, size: 18),
                  const SizedBox(width: 8),
                  Expanded(child: Text(perk)),
                ],
              ),
            ),
          const SizedBox(height: 12),
          if (active)
            Row(
              children: [
                const Icon(Icons.verified, color: Noir.success),
                const SizedBox(width: 8),
                Text(
                  s.clubActive,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
              ],
            )
          else ...[
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: yearly == null
                    ? null
                    : () => onBuy(ProductIds.clubYearly),
                child: Text(s.clubYearly(yearly ?? '—')),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton(
                onPressed: weekly == null
                    ? null
                    : () => onBuy(ProductIds.clubWeekly),
                child: Text(s.clubWeekly(weekly ?? '—')),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              s.clubCancelNote,
              style: const TextStyle(color: Noir.textMuted, fontSize: 12),
            ),
          ],
        ],
      ),
    );
  }
}

class _OfferTile extends StatelessWidget {
  const _OfferTile({
    required this.icon,
    required this.title,
    required this.body,
    required this.price,
    required this.onBuy,
    this.owned = false,
    this.highlight = false,
  });

  final String icon;
  final String title;
  final String body;
  final String? price;
  final VoidCallback onBuy;
  final bool owned;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Card(
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: highlight ? Noir.brass : Noir.outline),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              SvgIcon(icon, size: 40),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 16,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      body,
                      style: const TextStyle(
                        color: Noir.textMuted,
                        fontSize: 13,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              if (owned)
                Text(
                  s.owned,
                  style: const TextStyle(
                    color: Noir.success,
                    fontWeight: FontWeight.w700,
                  ),
                )
              else
                FilledButton(
                  onPressed: price == null ? null : onBuy,
                  child: Text(price ?? '—'),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CoinPack extends StatelessWidget {
  const _CoinPack({
    required this.coins,
    required this.price,
    required this.onBuy,
  });

  final int coins;
  final String? price;
  final VoidCallback onBuy;

  @override
  Widget build(BuildContext context) => Card(
    child: InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: price == null ? null : onBuy,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 6),
        child: Column(
          children: [
            SvgIcon(
              coins >= 2500
                  ? 'assets/icons/money-bag.svg'
                  : 'assets/icons/coin.svg',
              size: 36,
            ),
            const SizedBox(height: 6),
            Text(
              '$coins',
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18),
            ),
            const SizedBox(height: 4),
            Text(
              price ?? '—',
              style: const TextStyle(
                color: Noir.brass,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
