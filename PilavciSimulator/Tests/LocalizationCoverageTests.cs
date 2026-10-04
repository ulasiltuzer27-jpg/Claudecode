using System.Text.RegularExpressions;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Data;
using Xunit;

namespace PilavciSimulator.Tests;

/// <summary>
/// Ekranda "[anahtar]" gorunmesin: koddaki sabit anahtarlar ve veriden
/// turetilen anahtarlarin hepsi ceviri tablosunda olmali.
/// </summary>
public class LocalizationCoverageTests
{
    private static readonly string[] Prefixes =
    [
        "act", "cash", "chat", "common", "coop", "credits", "day", "game", "hint", "hud", "info", "item", "key",
        "laptop", "menu", "net", "news", "order", "part", "pause", "phone", "plate", "pot", "presence", "price",
        "report", "say", "settings", "size", "station", "stats", "stock", "ticket", "toast", "topping", "tut", "upg",
    ];

    private static Dictionary<string, string> Table() =>
        Loc.LoadTable(Path.Combine(TestPaths.GameDir, "Data", "Localization", "tr.json"));

    [Fact]
    public void Every_literal_key_in_code_exists()
    {
        var table = Table();
        var pattern = new Regex("\"([a-z][a-z0-9_]*\\.[a-z0-9_.]*[a-z0-9_])\"", RegexOptions.Compiled);
        var missing = new SortedSet<string>();
        foreach (var file in Directory.EnumerateFiles(TestPaths.GameDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            foreach (Match m in pattern.Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[1].Value;
                var prefix = key[..key.IndexOf('.')];
                // "act.take_" gibi kesik onekler ve ".json" gibi dosya adlari anahtar degil.
                if (!Prefixes.Contains(prefix) || key.EndsWith('_') || key.EndsWith(".json", StringComparison.Ordinal))
                {
                    continue;
                }

                if (key is "say.order")
                {
                    continue; // StartsWith ile kullanilan onek
                }

                if (!table.ContainsKey(key))
                {
                    missing.Add($"{key} ({Path.GetFileName(file)})");
                }
            }
        }

        Assert.True(missing.Count == 0, "eksik anahtarlar: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_data_driven_key_exists()
    {
        TestPaths.UseSourceData();
        var table = Table();
        var data = GameData.Load(Path.Combine(TestPaths.GameDir, "Data"));
        var keys = new List<string>();
        keys.AddRange(data.Supplies.Select(s => s.NameKey));
        keys.AddRange(data.Menu.Select(m => m.NameKey));
        keys.AddRange(data.Upgrades.Select(u => u.NameKey));
        keys.AddRange(data.Upgrades.Where(u => u.DescKey.Length > 0).Select(u => u.DescKey));
        keys.AddRange(data.Spots.Select(s => s.NameKey));
        keys.AddRange(data.Customers.Select(c => c.NameKey));
        keys.AddRange(data.Customers.Select(c => "cust." + c.Id));
        keys.AddRange(data.Spots.Select(s => "spot." + s.Id));
        keys.AddRange(data.Achievements.Select(a => "ach." + a.Id));
        foreach (var voice in data.Customers.Select(c => c.Voice).Distinct())
        {
            keys.AddRange(Enumerable.Range(0, 4).Select(i => $"say.order.{voice}.{i}"));
        }

        keys.AddRange(Enum.GetValues<Food>().Select(f => "food." + f.ToString().ToLowerInvariant()));
        keys.AddRange(Enum.GetValues<WeatherKind>().Select(k => "weather." + k.ToString().ToLowerInvariant()));
        keys.AddRange(GameActions.All.Select(GameActions.LocKey));
        keys.AddRange(Enumerable.Range(0, Progression.TutorialSteps).Select(i => "tut." + i));
        keys.AddRange(Enumerable.Range(1, data.Balance.XpLevels.Length).Select(i => "level." + Math.Min(i, 12)));
        keys.AddRange(DayLogic.DayKeys);
        foreach (var stock in new[] { "pirinc", "bulgur", "nohut", "konserve_nohut", "fasulye", "tereyagi", "tavuk", "et" })
        {
            keys.Add("act.take_" + stock);
        }

        foreach (var tab in new[] { "graphics", "audio", "controls", "gameplay" })
        {
            keys.Add("settings.tab." + tab);
        }

        var missing = keys.Distinct().Where(k => !table.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "eksik anahtarlar: " + string.Join(", ", missing));
    }
}
