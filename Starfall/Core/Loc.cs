using System.Text.Json;
using System.Text.RegularExpressions;
using Starfall.Render;

namespace Starfall.Core;

/// <summary>
/// Ceviri. Anahtarlar duz ("menu.continue"); deger dizi olabilir (diyaloglar).
/// Parametreler {ad} bicimindedir: Loc.T("toast.shard", ("n", 3), ("total", 30)).
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, Dictionary<string, JsonElement>> Dicts = new();
    public static string Lang { get; private set; } = "tr";
    public static readonly string[] Langs = { "tr", "en" };
    public static event Action<string>? Changed;
    private static readonly Regex Param = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private static Dictionary<string, JsonElement> Dict(string lang)
    {
        if (Dicts.TryGetValue(lang, out var d)) return d;
        var json = Gfx.ReadResource($"i18n/{lang}.json");
        d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        Dicts[lang] = d;
        return d;
    }

    public static IReadOnlyCollection<string> Keys(string lang) => Dict(lang).Keys;

    public static string Detect()
    {
        var c = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var env = Environment.GetEnvironmentVariable("LANG") ?? "";
        return c.StartsWith("tr", StringComparison.OrdinalIgnoreCase) || env.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
    }

    public static void SetLang(string? lang)
    {
        Lang = lang == "tr" || lang == "en" ? lang : "en";
        Changed?.Invoke(Lang);
    }

    public static bool Has(string key) => Dict(Lang).ContainsKey(key) || Dict("en").ContainsKey(key);

    private static JsonElement? Raw(string key)
    {
        if (Dict(Lang).TryGetValue(key, out var v)) return v;
        if (Dict("en").TryGetValue(key, out v)) return v;
        return null;
    }

    private static string Fill(string s, (string k, object v)[] p)
    {
        if (p.Length == 0) return s;
        return Param.Replace(s, m =>
        {
            foreach (var (k, v) in p) if (k == m.Groups[1].Value) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            return m.Value;
        });
    }

    public static string T(string key, params (string k, object v)[] p)
    {
        var v = Raw(key);
        if (v == null) return key;
        if (v.Value.ValueKind == JsonValueKind.Array) return string.Join("\n", Lines(key, p));
        return Fill(v.Value.GetString() ?? key, p);
    }

    public static string[] Lines(string key, params (string k, object v)[] p)
    {
        var v = Raw(key);
        if (v == null) return new[] { key };
        if (v.Value.ValueKind != JsonValueKind.Array) return new[] { Fill(v.Value.GetString() ?? key, p) };
        return v.Value.EnumerateArray().Select(e => Fill(e.GetString() ?? "", p)).ToArray();
    }
}
