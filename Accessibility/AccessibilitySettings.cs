using Microsoft.Xna.Framework;

namespace PixelSurvival.Accessibility;

/// <summary>
/// Renk körlüğü paleti.
///
/// Oyun anlamı RENKLE taşıyan üç yerde riskli: üretilebilir tarif (yeşil) /
/// üretilemez (gri), güvenli bölge (yeşil) / PvP (kırmızı), ve nadirlik
/// kademeleri. Kırmızı-yeşil ayrımı, en yaygın renk körlüğü türlerinde
/// (protanopi + döteranopi, erkeklerin ~%8'i) çalışmaz.
/// </summary>
public enum ColorVisionMode
{
    /// <summary>Varsayılan palet.</summary>
    Default,

    /// <summary>Kırmızı zayıflığı — kırmızılar koyu/kahverengi algılanır.</summary>
    Protanopia,

    /// <summary>Yeşil zayıflığı — en yaygın tür.</summary>
    Deuteranopia,

    /// <summary>Mavi-sarı zayıflığı.</summary>
    Tritanopia
}

/// <summary>
/// Arayüzün anlam taşıyan renkleri.
///
/// Sabit renkler kodun içine dağılmış olsaydı, renk körlüğü paletini
/// eklemek her çizim çağrısını tek tek bulmayı gerektirirdi. Tek bir
/// palet nesnesi, "bu renk ne ANLAMA geliyor" sorusunu isimlendiriyor.
/// </summary>
public sealed record UiPalette(
    Color Text,
    Color DimText,
    Color Positive,
    Color Negative,
    Color Warning,
    Color RarityCommon,
    Color RarityUncommon,
    Color RarityRare,
    Color RarityEpic,
    Color RarityLegendary);

/// <summary>
/// AŞAMA 2 / MADDE 23 — erişilebilirlik ayarları.
///
/// ── Renk TEK BAŞINA anlam taşımamalı ────────────────────────────────────
/// Palet değiştirmek tek başına yetmez; bu yüzden arayüz kritik bilgiyi
/// renge ek olarak METİNLE de veriyor (başarım listesinde <c>[X]</c>/<c>[ ]</c>,
/// bölge göstergesinde "GUVENLI BOLGE"/"PvP BOLGESI", nadirlikte
/// "Nadir"/"Efsanevi"). Palet, renk körü oyuncunun bilgiyi hızlı
/// AYIRT ETMESİNİ kolaylaştırır; bilginin TEK taşıyıcısı değildir.
///
/// ── Yazı boyutu ─────────────────────────────────────────────────────────
/// Bitmap font tam sayı katlarla ölçeklenir. 1.5x gibi bir kat, pixel
/// art yazıyı bulanıklaştırır ve okunurluğu ARTIRMAK yerine düşürür;
/// bu yüzden ölçek tam sayı.
/// </summary>
public sealed class AccessibilitySettings
{
    /// <summary>Yazı ölçeği alt/üst sınırı (tam sayı kat).</summary>
    public const int MinTextScale = 1;
    public const int MaxTextScale = 4;

    private int _textScale = 2;
    private ColorVisionMode _mode = ColorVisionMode.Default;

    /// <summary>Ayar değişti — arayüz yeniden ölçülmeli.</summary>
    public event Action? Changed;

    /// <summary>Arayüz yazı ölçeği (tam sayı kat).</summary>
    public int TextScale
    {
        get => _textScale;
        set
        {
            var clamped = Math.Clamp(value, MinTextScale, MaxTextScale);
            if (clamped == _textScale) return;

            _textScale = clamped;
            Changed?.Invoke();
        }
    }

    public ColorVisionMode ColorVision
    {
        get => _mode;
        set
        {
            if (value == _mode) return;

            _mode = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Yazı ölçeğini sırayla artırır, üst sınırda başa döner.</summary>
    public void CycleTextScale() =>
        TextScale = _textScale >= MaxTextScale ? MinTextScale : _textScale + 1;

    /// <summary>Renk paletini sırayla değiştirir.</summary>
    public void CycleColorVision()
    {
        var values = Enum.GetValues<ColorVisionMode>();
        ColorVision = values[(Array.IndexOf(values, _mode) + 1) % values.Length];
    }

    /// <summary>Seçili moda karşılık gelen palet.</summary>
    public UiPalette Palette => _mode switch
    {
        // Protanopi: kirmizi koyu ve kahverengimsi algilanir. "Olumsuz"
        // rengi kirmizidan MAGENTAya kaydiriliyor, "olumlu" ise yesilden
        // aciik maviye — ikisi de kirmizi kanalindan bagimsiz ayrisiyor.
        ColorVisionMode.Protanopia => new UiPalette(
            Text: new Color(232, 236, 244),
            DimText: new Color(130, 136, 152),
            Positive: new Color(102, 178, 236),
            Negative: new Color(226, 118, 206),
            Warning: new Color(238, 202, 92),
            RarityCommon: new Color(178, 184, 196),
            RarityUncommon: new Color(126, 196, 232),
            RarityRare: new Color(96, 146, 226),
            RarityEpic: new Color(196, 128, 236),
            RarityLegendary: new Color(240, 202, 96)),

        // Doteranopi (en yaygin): yesil-kirmizi ayrimi kayboluyor.
        // Mavi-turuncu ekseni bu turde en guvenli kontrasttir.
        ColorVisionMode.Deuteranopia => new UiPalette(
            Text: new Color(232, 236, 244),
            DimText: new Color(130, 136, 152),
            Positive: new Color(108, 186, 240),
            Negative: new Color(240, 148, 62),
            Warning: new Color(242, 208, 104),
            RarityCommon: new Color(178, 184, 196),
            RarityUncommon: new Color(130, 200, 236),
            RarityRare: new Color(92, 150, 230),
            RarityEpic: new Color(186, 134, 238),
            RarityLegendary: new Color(244, 186, 74)),

        // Tritanopi: mavi-sari ayrimi kayboluyor. Kirmizi-yesil ekseni
        // burada calisir, ama mavi tonlar birbirinden ayrilmali.
        ColorVisionMode.Tritanopia => new UiPalette(
            Text: new Color(238, 234, 234),
            DimText: new Color(146, 138, 138),
            Positive: new Color(118, 222, 138),
            Negative: new Color(236, 96, 96),
            Warning: new Color(232, 130, 168),
            RarityCommon: new Color(184, 178, 178),
            RarityUncommon: new Color(126, 216, 146),
            RarityRare: new Color(96, 200, 196),
            RarityEpic: new Color(214, 108, 172),
            RarityLegendary: new Color(232, 106, 106)),

        _ => new UiPalette(
            Text: new Color(228, 232, 240),
            DimText: new Color(128, 134, 150),
            Positive: new Color(120, 230, 140),
            Negative: new Color(235, 110, 110),
            Warning: new Color(240, 186, 74),
            RarityCommon: new Color(178, 184, 196),
            RarityUncommon: new Color(118, 198, 122),
            RarityRare: new Color(96, 158, 226),
            RarityEpic: new Color(176, 118, 224),
            RarityLegendary: new Color(240, 186, 74))
    };

    /// <summary>
    /// Seçili modun ekranda gösterilecek adı.
    ///
    /// Dört isim de çeviri tablosundan gelir; biri sabit metin kalsaydı
    /// ayar ekranı dil değişince yarı Türkçe görünürdü.
    /// </summary>
    public string ColorVisionLabel => _mode switch
    {
        ColorVisionMode.Protanopia => Localization.Loc.T("settings.colorvision.protanopia"),
        ColorVisionMode.Deuteranopia => Localization.Loc.T("settings.colorvision.deuteranopia"),
        ColorVisionMode.Tritanopia => Localization.Loc.T("settings.colorvision.tritanopia"),
        _ => Localization.Loc.T("settings.colorvision.default")
    };
}
