using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.Localization;

/// <summary>
/// AŞAMA 2 / MADDE 23 — çeviri erişiminin TEK noktası.
///
/// ── Neden statik ────────────────────────────────────────────────────────
/// Metin, arayüzün her köşesinden isteniyor. Her sınıfa bir
/// <c>LocaleTable</c> parametresi geçirmek, imzaları çeviri uğruna
/// kirletirdi ve bir yerde unutulduğunda o metin sessizce çevrilmeden
/// kalırdı. Dil, oyun boyunca TEK bir küresel durumdur; statik erişim
/// bu gerçeği doğrudan yansıtıyor.
///
/// ── Eksik anahtar GİZLENMEZ ─────────────────────────────────────────────
/// Bir anahtar seçili dilde yoksa önce yedek dile (İngilizce) düşülür.
/// Orada da yoksa anahtarın KENDİSİ köşeli parantez içinde gösterilir:
/// <c>[hud.inventory]</c>. Sessizce boş string döndürmek, eksik çevirinin
/// ekranda hiç fark edilmemesine yol açardı.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Yedek dil. Bir anahtar seçili dilde yoksa buradan okunur.
    /// İngilizce seçildi çünkü tablonun tamamı her zaman burada tutuluyor
    /// (<c>verify_content.py</c> diğer dillerin eksiklerini raporluyor).
    /// </summary>
    public const string FallbackCode = "en";

    private static readonly Dictionary<string, LocaleTable> Tables = new(StringComparer.Ordinal);

    private static LocaleTable? _current;
    private static LocaleTable? _fallback;

    /// <summary>Yüklenmiş dillerin kodları — ayar ekranında listelemek için.</summary>
    public static IReadOnlyCollection<string> Available => Tables.Keys;

    /// <summary>Seçili dilin kodu.</summary>
    public static string CurrentCode => _current?.Code ?? FallbackCode;

    /// <summary>Seçili dilin kendi dilindeki adı.</summary>
    public static string CurrentName => _current?.Name ?? "English";

    /// <summary>Dil değişti — arayüzün önbelleğe aldığı metinler yenilenmeli.</summary>
    public static event Action? LanguageChanged;

    /// <summary>Verilen dilleri yükler ve ilkini seçer.</summary>
    public static void Load(ContentManager content, params string[] codes)
    {
        Tables.Clear();

        foreach (var code in codes)
        {
            Tables[code] = LocaleTable.Load(content, code);
        }

        if (!Tables.TryGetValue(FallbackCode, out _fallback))
        {
            throw new InvalidOperationException(
                $"Yedek dil '{FallbackCode}' yuklenmedi. Eksik anahtarlar icin gerekli.");
        }

        _current = Tables[codes[0]];
    }

    /// <summary>Dili değiştirir. Bilinmeyen kod yok sayılır.</summary>
    public static bool Use(string code)
    {
        if (!Tables.TryGetValue(code, out var table)) return false;

        _current = table;
        LanguageChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Yüklü diller arasında sırayla geçer — ayar ekranında tek tuşla
    /// dil değiştirmek için.
    /// </summary>
    public static void Cycle()
    {
        if (Tables.Count == 0) return;

        var codes = Tables.Keys.Order().ToArray();
        var index = Array.IndexOf(codes, CurrentCode);

        Use(codes[(index + 1) % codes.Length]);
    }

    /// <summary>Anahtarın çevirisi.</summary>
    public static string T(string key)
    {
        if (_current is not null && _current.Strings.TryGetValue(key, out var text)) return text;
        if (_fallback is not null && _fallback.Strings.TryGetValue(key, out var backup)) return backup;

        // Eksik anahtari gizlemek yerine gorunur kilmak: ekranda
        // [key] gorulur ve hata hemen fark edilir.
        return $"[{key}]";
    }

    /// <summary>
    /// Biçimlendirilmiş çeviri: <c>Loc.T("hud.count", 3)</c> →
    /// tabloda <c>"Adet: {0}"</c>.
    /// </summary>
    public static string T(string key, params object[] args)
    {
        var format = T(key);

        // Bozuk bir bicim dizesi (yanlis {0}) oyunu cokertmemeli:
        // ham metni gostermek, FormatException ile kapanmaktan iyidir.
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}
