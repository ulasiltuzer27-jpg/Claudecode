import 'package:material_ui/material_ui.dart';

/// Noir paleti. Oyun her zaman koyu temada; acik tema bilerek yok.
abstract final class Noir {
  static const background = Color(0xFF15110E);
  static const surface = Color(0xFF221B16);
  static const surfaceHigh = Color(0xFF2E251E);
  static const outline = Color(0xFF4A3D32);
  static const paper = Color(0xFFF1E6D0);
  static const paperShade = Color(0xFFDCCBA9);
  static const ink = Color(0xFF2A2019);
  static const inkSoft = Color(0xFF6B5A4A);
  static const text = Color(0xFFEDE5D8);
  static const textMuted = Color(0xFFB3A693);
  static const thread = Color(0xFFC0392B);
  static const threadDark = Color(0xFF8E2A20);
  static const brass = Color(0xFFD9A441);
  static const cork = Color(0xFF9A7350);
  static const corkDark = Color(0xFF7A5A3E);
  static const success = Color(0xFF7FB069);

  /// Cozulen grup seritleri, zorluk sirasina gore (yapiskan not tonlari).
  static const groupColors = [
    Color(0xFFF2DE96),
    Color(0xFFC9DFB8),
    Color(0xFFB9D3E8),
    Color(0xFFE9C2D1),
    Color(0xFFD5C8EA),
  ];

  static Color groupColor(int index) => groupColors[index % groupColors.length];
}

const bodyFont = 'CourierPrime';
const displayFont = 'Playfair';

ThemeData buildTheme() {
  final scheme =
      ColorScheme.fromSeed(
        seedColor: Noir.brass,
        brightness: Brightness.dark,
      ).copyWith(
        primary: Noir.brass,
        onPrimary: Noir.ink,
        secondary: Noir.thread,
        onSecondary: Noir.paper,
        surface: Noir.surface,
        onSurface: Noir.text,
        error: Noir.thread,
        outline: Noir.outline,
      );
  final base = ThemeData(
    colorScheme: scheme,
    brightness: Brightness.dark,
    fontFamily: bodyFont,
    scaffoldBackgroundColor: Noir.background,
  );
  return base.copyWith(
    textTheme: base.textTheme
        .apply(bodyColor: Noir.text, displayColor: Noir.text)
        .copyWith(
          displaySmall: const TextStyle(
            fontFamily: displayFont,
            fontWeight: FontWeight.w700,
            fontSize: 34,
            color: Noir.text,
          ),
          headlineSmall: const TextStyle(
            fontFamily: displayFont,
            fontWeight: FontWeight.w700,
            fontSize: 24,
            color: Noir.text,
          ),
          titleLarge: const TextStyle(
            fontFamily: displayFont,
            fontWeight: FontWeight.w700,
            fontSize: 20,
            color: Noir.text,
          ),
        ),
    appBarTheme: const AppBarTheme(
      backgroundColor: Noir.background,
      foregroundColor: Noir.text,
      elevation: 0,
      centerTitle: false,
      titleTextStyle: TextStyle(
        fontFamily: displayFont,
        fontWeight: FontWeight.w700,
        fontSize: 20,
        color: Noir.text,
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: Noir.brass,
        foregroundColor: Noir.ink,
        disabledBackgroundColor: Noir.surfaceHigh,
        disabledForegroundColor: Noir.textMuted,
        minimumSize: const Size(48, 48),
        textStyle: const TextStyle(
          fontFamily: bodyFont,
          fontWeight: FontWeight.w700,
          fontSize: 16,
        ),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: Noir.text,
        side: const BorderSide(color: Noir.outline),
        minimumSize: const Size(48, 48),
        textStyle: const TextStyle(
          fontFamily: bodyFont,
          fontWeight: FontWeight.w700,
          fontSize: 15,
        ),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: Noir.brass,
        textStyle: const TextStyle(
          fontFamily: bodyFont,
          fontWeight: FontWeight.w700,
        ),
      ),
    ),
    cardTheme: CardThemeData(
      color: Noir.surface,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: const BorderSide(color: Noir.outline),
      ),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: Noir.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
    ),
    snackBarTheme: const SnackBarThemeData(
      backgroundColor: Noir.paper,
      contentTextStyle: TextStyle(
        fontFamily: bodyFont,
        color: Noir.ink,
        fontWeight: FontWeight.w700,
      ),
      behavior: SnackBarBehavior.floating,
    ),
    dividerTheme: const DividerThemeData(color: Noir.outline, space: 1),
  );
}
