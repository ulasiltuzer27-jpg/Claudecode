import 'package:material_ui/material_ui.dart';

import '../../app/strings.dart';
import '../../audio/sound_service.dart';
import '../../app/theme.dart';
import '../../data/models.dart';
import '../widgets/common.dart';
import '../widgets/confetti.dart';

/// Davanin giris hikayesi (sahneler + supheliler) ya da kapanis sahnesi.
class StoryScreen extends StatefulWidget {
  const StoryScreen.intro({super.key, required this.caseFile})
    : ending = false,
      stars = null;

  const StoryScreen.ending({super.key, required this.caseFile, this.stars})
    : ending = true;

  final CaseFile caseFile;
  final bool ending;
  final int? stars;

  @override
  State<StoryScreen> createState() => _StoryScreenState();
}

class _StoryScreenState extends State<StoryScreen> {
  final _pages = PageController();
  int _page = 0;

  @override
  void initState() {
    super.initState();
    if (widget.ending && widget.stars != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        sfx(context, Sfx.caseSolved);
        showConfetti(context, count: 140);
      });
    }
  }

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = S.of(context);
    final lang = s.lang;
    final c = widget.caseFile;
    final pages = <Widget>[
      if (!widget.ending) ...[
        for (final p in c.intro)
          _Scene(icon: p.assetPath, text: p.text.of(lang)),
        _SuspectsPage(caseFile: c, lang: lang),
      ] else ...[
        _Scene(
          icon: c.suspect(c.culpritId).assetPath,
          title: s.accCorrectTitle,
          text: c.ending.of(lang),
          stars: widget.stars,
          stamp: s.caseClosedStamp,
        ),
        _Scene(
          icon: 'assets/icons/detective.svg',
          title: s.toBeContinued,
          text: c.teaser.of(lang),
        ),
      ],
    ];
    final last = _page == pages.length - 1;
    return Scaffold(
      appBar: AppBar(title: Text(c.title.of(lang))),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: PageView(
                controller: _pages,
                onPageChanged: (i) {
                  sfx(context, Sfx.page);
                  setState(() => _page = i);
                },
                children: pages,
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      for (var i = 0; i < pages.length; i++)
                        Container(
                          width: 8,
                          height: 8,
                          margin: const EdgeInsets.symmetric(horizontal: 3),
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: i == _page ? Noir.brass : Noir.outline,
                          ),
                        ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: () {
                        if (last) {
                          Navigator.pop(context, true);
                        } else {
                          _pages.nextPage(
                            duration: const Duration(milliseconds: 300),
                            curve: Curves.easeOut,
                          );
                        }
                      },
                      child: Text(
                        last
                            ? (widget.ending
                                  ? s.closeFile
                                  : s.startInvestigation)
                            : s.next,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Scene extends StatelessWidget {
  const _Scene({
    required this.icon,
    required this.text,
    this.title,
    this.stars,
    this.stamp,
  });

  final String icon;
  final String text;
  final String? title;
  final int? stars;

  /// Sahnenin ustune basilan lastik damga ("DOSYA KAPANDI").
  final String? stamp;

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Column(
      children: [
        const SizedBox(height: 16),
        Stack(
          alignment: Alignment.center,
          clipBehavior: Clip.none,
          children: [
            Container(
              padding: const EdgeInsets.all(28),
              decoration: const BoxDecoration(
                shape: BoxShape.circle,
                color: Noir.surfaceHigh,
              ),
              child: SvgIcon(icon, size: 96),
            ),
            if (stamp != null)
              TweenAnimationBuilder<double>(
                tween: Tween(begin: 0, end: 1),
                duration: const Duration(milliseconds: 650),
                curve: Curves.easeOutBack,
                builder: (context, t, child) => Opacity(
                  opacity: t.clamp(0, 1),
                  child: Transform.scale(scale: 2.4 - 1.4 * t, child: child),
                ),
                child: Transform.rotate(
                  angle: -0.22,
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 6,
                    ),
                    decoration: BoxDecoration(
                      border: Border.all(color: Noir.thread, width: 4),
                      borderRadius: BorderRadius.circular(6),
                      color: Noir.background.withValues(alpha: 0.55),
                    ),
                    child: Text(
                      stamp!,
                      style: const TextStyle(
                        color: Noir.thread,
                        fontWeight: FontWeight.w700,
                        fontSize: 24,
                        letterSpacing: 2,
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 24),
        if (title != null) ...[
          Text(
            title!,
            style: Theme.of(context).textTheme.headlineSmall,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 8),
        ],
        if (stars != null) ...[
          StarRow(stars!, size: 32),
          const SizedBox(height: 16),
        ],
        PaperCard(
          padding: const EdgeInsets.all(20),
          child: Text(text, style: const TextStyle(fontSize: 17, height: 1.5)),
        ),
      ],
    ),
  );
}

class _SuspectsPage extends StatelessWidget {
  const _SuspectsPage({required this.caseFile, required this.lang});

  final CaseFile caseFile;
  final String lang;

  @override
  Widget build(BuildContext context) => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      Text(
        S.of(context).suspects,
        style: Theme.of(context).textTheme.headlineSmall,
      ),
      const SizedBox(height: 12),
      for (final sp in caseFile.suspects) ...[
        SuspectTile(suspect: sp, lang: lang),
        const SizedBox(height: 10),
      ],
    ],
  );
}

class SuspectTile extends StatelessWidget {
  const SuspectTile({
    super.key,
    required this.suspect,
    required this.lang,
    this.selected = false,
    this.onTap,
  });

  final Suspect suspect;
  final String lang;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Card(
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(14),
      side: BorderSide(
        color: selected ? Noir.thread : Noir.outline,
        width: selected ? 2.5 : 1,
      ),
    ),
    child: InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SvgIcon(suspect.assetPath, size: 52),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    suspect.name.of(lang),
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 17,
                    ),
                  ),
                  Text(
                    suspect.role.of(lang),
                    style: const TextStyle(color: Noir.brass),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    suspect.bio.of(lang),
                    style: const TextStyle(color: Noir.textMuted, height: 1.3),
                  ),
                ],
              ),
            ),
            if (selected) const Icon(Icons.gavel, color: Noir.thread),
          ],
        ),
      ),
    ),
  );
}
