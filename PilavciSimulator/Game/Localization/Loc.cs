using System.Text.Json;
using PilavciSimulator.Core;

namespace PilavciSimulator.Localization;

/// <summary>
/// Ceviri erisiminin tek noktasi. Kalip PixelSurvival'daki
/// <c>Localization/Loc.cs</c>'den alindi.
///
/// Eksik anahtar GIZLENMEZ: secili dilde yoksa yedek dile (Ingilizce),
/// orada da yoksa anahtarin kendisi koseli parantezle gosterilir
/// (<c>[hud.money]</c>). Testler iki dilin anahtar kumelerinin esit
/// oldugunu ayrica dogruluyor.
/// </summary>
public static class Loc
{
    public const string FallbackCode = "en";
    public static readonly string[] Supported = ["tr", "en"];

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _current = new();
    private static Dictionary<string, string> _fallback = new();

    public static string CurrentCode { get; private set; } = "tr";

    public static event Action? LanguageChanged;

    public static void Load(string dataDir, string initial)
    {
        Tables.Clear();
        foreach (var code in Supported)
        {
            var path = Path.Combine(dataDir, "Localization", code + ".json");
            Tables[code] = LoadTable(path);
        }

        _fallback = Tables[FallbackCode];
        Use(initial);
    }

    public static Dictionary<string, string> LoadTable(string path)
    {
        using var stream = File.OpenRead(path);
        var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, JsonUtil.Options)
                  ?? new Dictionary<string, string>();
        return new Dictionary<string, string>(doc, StringComparer.Ordinal);
    }

    public static bool Use(string code)
    {
        if (!Tables.TryGetValue(code, out var t))
        {
            return false;
        }

        _current = t;
        CurrentCode = code;
        LanguageChanged?.Invoke();
        return true;
    }

    public static bool Has(string key) => _current.ContainsKey(key) || _fallback.ContainsKey(key);

    public static string T(string key)
    {
        if (_current.TryGetValue(key, out var v) || _fallback.TryGetValue(key, out v))
        {
            return v;
        }

        return "[" + key + "]";
    }

    public static string T(string key, params object[] args)
    {
        var format = T(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>Kayit/testler icin: yuklu bir tablonun anahtarlari.</summary>
    public static IReadOnlyCollection<string> KeysOf(string code) =>
        Tables.TryGetValue(code, out var t) ? t.Keys : Array.Empty<string>();
}
