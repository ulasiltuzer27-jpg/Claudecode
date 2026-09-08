using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.Achievements;

/// <summary>
/// Bir oyun istatistiğinin anahtarı. <c>string</c> yerine sarmalayıcı tip:
/// achievement eşiği yanlış anahtara bağlanınca derleme değil, sessiz bir
/// "hiç açılmayan achievement" hatası olurdu.
/// </summary>
public readonly record struct StatKey(string Value)
{
    public override string ToString() => Value;
}

public sealed class StatDefinition
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
}

/// <summary>
/// Tek bir achievement tanımı — <c>Content/Steam/achievements.json</c>.
///
/// Eşik VERİDEN gelir, koda gömülmez: yeni bir achievement eklemek için
/// C# dosyasına dokunmak gerekmiyor.
/// </summary>
public sealed class AchievementDefinition
{
    /// <summary>Steamworks'teki API adı (örn. <c>ACH_FIRST_WOOD</c>).</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("description")] public string Description { get; init; } = "";

    /// <summary>Hangi istatistiği izlediği.</summary>
    [JsonPropertyName("stat")] public string Stat { get; init; } = "";

    /// <summary>Açılması için istatistiğin ulaşması gereken değer.</summary>
    [JsonPropertyName("threshold")] public int Threshold { get; init; } = 1;

    /// <summary>Açılana kadar listede gizli mi (sürpriz achievement'lar).</summary>
    [JsonPropertyName("hidden")] public bool Hidden { get; init; }

    [JsonIgnore] public StatKey StatKey => new(Stat);
}

/// <summary>Steam leaderboard tanımı — hangi istatistiği hangi sırayla yazdığı.</summary>
public sealed class LeaderboardDefinition
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("stat")] public string Stat { get; init; } = "";

    /// <summary><c>descending</c> = büyük skor daha iyi.</summary>
    [JsonPropertyName("sort")] public string Sort { get; init; } = "descending";

    [JsonIgnore] public bool HigherIsBetter =>
        Sort.Equals("descending", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore] public StatKey StatKey => new(Stat);
}

/// <summary>
/// AŞAMA 2 / MADDE 21 — achievement, istatistik ve leaderboard tanımları.
///
/// Bu dosya Steamworks partner sitesindeki tanımların YEREL AYNASIDIR.
/// Buradaki bir kaydın varlığı, achievement'in Steam tarafında tanımlı
/// olduğunu KANITLAMAZ; oyun yalnızca adı/açıklamayı gösterebilsin ve
/// eşiği bilsin diye var.
/// </summary>
public sealed class AchievementCatalog
{
    [JsonPropertyName("appId")] public uint AppId { get; init; }
    [JsonPropertyName("stats")] public List<StatDefinition> Stats { get; init; } = [];
    [JsonPropertyName("achievements")] public List<AchievementDefinition> Achievements { get; init; } = [];
    [JsonPropertyName("leaderboards")] public List<LeaderboardDefinition> Leaderboards { get; init; } = [];

    public static AchievementCatalog Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var catalog = JsonSerializer.Deserialize<AchievementCatalog>(stream, JsonOptions)
                      ?? throw new InvalidOperationException($"'{relativePath}' okunamadi.");

        var statKeys = catalog.Stats.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var achievement in catalog.Achievements)
        {
            if (!seen.Add(achievement.Id))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{achievement.Id}' iki kez tanimlanmis.");
            }

            // Tanimsiz bir istatistige bagli achievement HIC acilmaz ve bu,
            // calisma zamaninda fark edilmesi cok zor bir hatadir: kimse
            // "acilmayan achievement" sikayeti getirene kadar sessiz kalir.
            if (!statKeys.Contains(achievement.Stat))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{achievement.Id}' tanimsiz istatistige bagli " +
                    $"('{achievement.Stat}'). Gecerli: {string.Join(", ", statKeys)}");
            }

            if (achievement.Threshold < 1)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{achievement.Id}' esigi {achievement.Threshold} — " +
                    "sifir veya negatif esik oyun baslar baslamaz acilirdi.");
            }
        }

        foreach (var leaderboard in catalog.Leaderboards)
        {
            if (!statKeys.Contains(leaderboard.Stat))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{leaderboard.Key}' tanimsiz istatistige bagli " +
                    $"('{leaderboard.Stat}').");
            }
        }

        return catalog;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
