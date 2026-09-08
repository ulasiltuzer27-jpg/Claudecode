using Microsoft.Xna.Framework;
using PixelSurvival.Accessibility;

namespace PixelSurvival.Cosmetics;

/// <summary>
/// Kozmetik nadirlik kademesi.
///
/// ════════════════════════════════════════════════════════════════════════
/// NADIRLIK YALNIZCA GÖRSEL BİR ETİKETTİR
/// ════════════════════════════════════════════════════════════════════════
/// Bir kademe oyuncuya hasar, can, hız, zırh veya başka HİÇBİR oynanış
/// avantajı VERMEZ. Verseydi oyun pay-to-win olurdu ve PvP (madde 18) ile
/// ekonomi (madde 22) dengesi anlamını yitirirdi.
///
/// Bu kural yorum satırıyla korunmuyor — yapısal savunmalar:
///   1. <see cref="CosmeticDefinition"/> tipinde istatistik alanı YOK.
///      Nadirliğe güç bağlamak için önce yeni bir alan açmak gerekir.
///   2. Bu enum'un tek davranışı <see cref="Color"/> döndürmek. Sayısal
///      bir çarpan üretmiyor, bir yerde çarpan olarak kullanılamıyor.
///   3. <c>Tools/verify_content.py</c> cosmetics.json içinde damage/health/
///      speed/armor benzeri bir alan görürse hata veriyor.
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public enum CosmeticRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public static class CosmeticRarityExtensions
{
    /// <summary>
    /// Arayüzdeki çerçeve/yazı rengi. Nadirliğin oyundaki TEK etkisi budur.
    ///
    /// MADDE 23: renk artık erişilebilirlik paletinden geliyor. Varsayılan
    /// palette yaygın→gri, az bulunur→yeşil, nadir→mavi sırası, kırmızı-yeşil
    /// körlüğünde iki kademeyi birbirine yaklaştırıyordu; renk körlüğü
    /// paletlerinde kademeler mavi-turuncu ekseninde ayrışıyor.
    ///
    /// Nadirliğin renk DIŞINDA bir etkisi hâlâ yok: bu metot bir Color
    /// döndürür, sayı değil.
    /// </summary>
    public static Color FrameColor(this CosmeticRarity rarity, UiPalette palette) => rarity switch
    {
        CosmeticRarity.Common => palette.RarityCommon,
        CosmeticRarity.Uncommon => palette.RarityUncommon,
        CosmeticRarity.Rare => palette.RarityRare,
        CosmeticRarity.Epic => palette.RarityEpic,
        CosmeticRarity.Legendary => palette.RarityLegendary,
        _ => palette.Text
    };

    /// <summary>Ekranda gösterilecek kısa ad (madde 23: çeviri tablosundan).</summary>
    public static string Label(this CosmeticRarity rarity) => rarity switch
    {
        CosmeticRarity.Common => Localization.Loc.T("rarity.common"),
        CosmeticRarity.Uncommon => Localization.Loc.T("rarity.uncommon"),
        CosmeticRarity.Rare => Localization.Loc.T("rarity.rare"),
        CosmeticRarity.Epic => Localization.Loc.T("rarity.epic"),
        CosmeticRarity.Legendary => Localization.Loc.T("rarity.legendary"),
        _ => "?"
    };

    /// <summary>
    /// JSON'daki metni kademeye çevirir. Tanınmayan değer sessizce
    /// <see cref="CosmeticRarity.Common"/>'a düşmez — veri hatası
    /// gizlenmemeli, yükleme sırasında patlamalı.
    /// </summary>
    public static CosmeticRarity Parse(string text)
    {
        if (Enum.TryParse<CosmeticRarity>(text, ignoreCase: true, out var rarity))
        {
            return rarity;
        }

        throw new InvalidOperationException(
            $"Bilinmeyen nadirlik: '{text}'. Gecerli degerler: " +
            $"{string.Join(", ", Enum.GetNames<CosmeticRarity>())}");
    }
}
