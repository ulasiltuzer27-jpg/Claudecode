using System.Text.Json;
using Starfall.Core;
using Starfall.Render;

namespace Starfall.Achievements;

public sealed record StatDef(string Key, string Kind);
public sealed record AchIcon(string Glyph, string Color);
public sealed record AchievementDef(string Id, string Stat, double Threshold, AchIcon Icon, bool Hidden = false);

/// <summary>achievements.json: esikler veriden gelir; yeni basarim eklemek icin koda dokunmak gerekmez.</summary>
public static class AchievementData
{
    private sealed class FileShape
    {
        public List<StatDef> Stats { get; set; } = new();
        public List<AchievementDef> Achievements { get; set; } = new();
    }

    private static FileShape? _d;
    private static FileShape D => _d ??= JsonSerializer.Deserialize<FileShape>(Gfx.ReadResource("achievements.json"),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    public static IReadOnlyList<StatDef> Stats => D.Stats;
    public static IReadOnlyList<AchievementDef> All => D.Achievements;
}

/// <summary>Profil duzeyindeki istatistikler ('sum' birikir, 'max' yalnizca rekor kirilinca degisir).</summary>
public sealed class Stats
{
    private readonly Dictionary<string, double> _v = new();
    private readonly HashSet<string> _known;
    private readonly Events? _events;

    public Stats(Dictionary<string, double>? values, Events? events)
    {
        _known = AchievementData.Stats.Select(s => s.Key).ToHashSet();
        foreach (var k in _known) _v[k] = values != null && values.TryGetValue(k, out var x) ? x : 0;
        _events = events;
    }

    public IReadOnlyDictionary<string, double> Values => _v;

    public double Get(string key) => _v.TryGetValue(key, out var v) ? v : 0;

    public void Add(string key, double amount = 1)
    {
        if (!_known.Contains(key)) throw new ArgumentException("Bilinmeyen istatistik: " + key);
        _v[key] = Get(key) + amount;
        _events?.Emit("stat", a: (float)_v[key], key: key);
    }

    public void Max(string key, double value)
    {
        if (!_known.Contains(key)) throw new ArgumentException("Bilinmeyen istatistik: " + key);
        if (value > Get(key))
        {
            _v[key] = value;
            _events?.Emit("stat", a: (float)value, key: key);
        }
    }

    public Dictionary<string, double> ToDict() => new(_v);
}
