using Microsoft.Xna.Framework;

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
    /// </summary>
    public static Color FrameColor(this CosmeticRarity rarity) => rarity switch
    {
        CosmeticRarity.Common => new Color(178, 184, 196),
        CosmeticRarity.Uncommon => new Color(118, 198, 122),
        CosmeticRarity.Rare => new Color(96, 158, 226),
        CosmeticRarity.Epic => new Color(176, 118, 224),
        CosmeticRarity.Legendary => new Color(240, 186, 74),
        _ => Color.White
    };

    /// <summary>Ekranda gösterilecek kısa ad.</summary>
    public static string Label(this CosmeticRarity rarity) => rarity switch
    {
        CosmeticRarity.Common => "Yaygin",
        CosmeticRarity.Uncommon => "Az Bulunur",
        CosmeticRarity.Rare => "Nadir",
        CosmeticRarity.Epic => "Destansi",
        CosmeticRarity.Legendary => "Efsanevi",
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
