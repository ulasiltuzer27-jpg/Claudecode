using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

using PixelSurvival.Workshop;

namespace PixelSurvival.World;

/// <summary>
/// <c>Content/World/biomes.json</c> dosyasının kod karşılığı.
///
/// Eşikler, saçılım oranları ve gürültü ölçekleri koda GÖMÜLÜ DEĞİL.
/// Dünyanın hissini ayarlamak (daha çok su, daha sık orman, daha dağlık)
/// yeniden derleme gerektirmez — JSON'u değiştirip oyunu yeniden başlatmak yeter.
/// </summary>
public sealed class BiomeTable
{
    public NoiseSettings Noise { get; init; } = new();
    public List<BiomeRule> Rules { get; init; } = [];
    public List<ScatterRule> Scatter { get; init; } = [];
    public SpawnSettings Spawn { get; init; } = new();

    public static BiomeTable Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var table = JsonSerializer.Deserialize<BiomeTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.Rules.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': en az bir kural gerekli.");
        }

        // Son kural koşulsuz olmalı, aksi halde hiçbir kuralın eşleşmediği
        // koordinatlarda ne çizileceği belirsiz kalır.
        var last = table.Rules[^1];
        if (last.ElevationBelow is not null || last.MoistureAbove is not null ||
            last.ElevationAbove is not null || last.MoistureBelow is not null)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': son kural ('{last.Biome}') koşulsuz olmalı — " +
                $"varsayılan biome görevi görüyor.");
        }

        return table;
    }

    /// <summary>
    /// Kuralları sırayla değerlendirir, İLK eşleşen kazanır.
    /// </summary>
    public BiomeRule Resolve(double elevation, double moisture)
    {
        foreach (var rule in Rules)
        {
            if (rule.Matches(elevation, moisture))
            {
                return rule;
            }
        }

        // Load() son kuralın koşulsuz olduğunu garanti ediyor, buraya düşülmemeli.
        return Rules[^1];
    }

    /// <summary>Tabloda geçen tüm tile anahtarları (doğrulama için).</summary>
    public IEnumerable<string> AllTileKeys =>
        Rules.Select(r => r.Tile).Concat(Scatter.Select(s => s.Tile)).Distinct();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class NoiseSettings
{
    [JsonPropertyName("elevationScale")] public double ElevationScale { get; init; } = 0.011;
    [JsonPropertyName("moistureScale")] public double MoistureScale { get; init; } = 0.019;
    [JsonPropertyName("octaves")] public int Octaves { get; init; } = 4;
    [JsonPropertyName("lacunarity")] public double Lacunarity { get; init; } = 2.0;
    [JsonPropertyName("persistence")] public double Persistence { get; init; } = 0.5;

    /// <summary>
    /// Nem gürültüsü, yükseklik gürültüsünden FARKLI bir tohum kullanır.
    /// Aynı tohum kullanılsaydı nem ve yükseklik birebir aynı desen olurdu ve
    /// biome çeşitliliği çökerdi.
    /// </summary>
    [JsonPropertyName("moistureSeedOffset")] public int MoistureSeedOffset { get; init; } = 7777;
}

public sealed class BiomeRule
{
    [JsonPropertyName("biome")] public string Biome { get; init; } = "";
    [JsonPropertyName("tile")] public string Tile { get; init; } = "";
    [JsonPropertyName("elevationBelow")] public double? ElevationBelow { get; init; }
    [JsonPropertyName("elevationAbove")] public double? ElevationAbove { get; init; }
    [JsonPropertyName("moistureBelow")] public double? MoistureBelow { get; init; }
    [JsonPropertyName("moistureAbove")] public double? MoistureAbove { get; init; }

    public bool Matches(double elevation, double moisture) =>
        (ElevationBelow is null || elevation < ElevationBelow) &&
        (ElevationAbove is null || elevation >= ElevationAbove) &&
        (MoistureBelow is null || moisture < MoistureBelow) &&
        (MoistureAbove is null || moisture >= MoistureAbove);
}

/// <summary>Zemin seçildikten SONRA uygulanan tekil tile serpiştirmesi.</summary>
public sealed class ScatterRule
{
    [JsonPropertyName("tile")] public string Tile { get; init; } = "";
    [JsonPropertyName("onBiome")] public string OnBiome { get; init; } = "";
    [JsonPropertyName("moistureAbove")] public double? MoistureAbove { get; init; }
    [JsonPropertyName("chance")] public double Chance { get; init; }

    /// <summary>
    /// Bu kurala özel hash tuzu.
    ///
    /// ── Neden gerekli ───────────────────────────────────────────────────
    /// Saçılım kararı <c>Noise.HashToUnit(x, y, seed)</c> ile veriliyordu
    /// ve bu değer KURAL BAŞINA DEĞİŞMİYORDU. Sonuç: aynı biome'daki
    /// ikinci kural, birincinin alt kümesi oluyordu.
    ///
    /// Somut hâli: %22'lik ağaç kuralı hash &lt; 0.22 olan her tile'ı
    /// kapıp <c>break</c> ediyor. %10'luk çalı kuralı ancak hash &lt; 0.10
    /// iken eşleşebilir — ama orada zaten ağaç kazanmış oluyor. Yani çalı
    /// HİÇ ÇIKMIYORDU ve hata sessizdi: biomes.json'a kural yazılıyor,
    /// hiçbir uyarı çıkmıyor, dünyada o bitki hiç görünmüyor.
    ///
    /// Tuz her kuralın hash akışını ayırıyor; kurallar artık BAĞIMSIZ
    /// olaylar.
    ///
    /// ── Neden satır sırasından değil, kuralın KİMLİĞİNDEN türüyor ──────
    /// Tuz olarak listedeki indeks kullanılabilirdi ama o zaman
    /// biomes.json'da iki satırın yerini değiştirmek bütün dünyayı
    /// kaydırırdı: aynı tohumla açılan kayıtlı dünyalarda ağaçlar bir
    /// anda başka yerlere taşınırdı. Biome+tile çiftinden türetilince
    /// sıra önemsiz kalıyor.
    /// </summary>
    public int Salt => _salt ??= ComputeSalt();

    private int? _salt;

    /// <summary>FNV-1a — kısa dizeler için ucuz ve iyi dağılan bir karma.</summary>
    private int ComputeSalt()
    {
        var hash = 2166136261u;

        foreach (var c in $"{OnBiome}/{Tile}")
        {
            hash = (hash ^ c) * 16777619u;
        }

        // İşaret bitini at: negatif tuz da çalışırdı ama Python
        // karşılığıyla (Tools/verify_worldgen.py) birebir aynı sayıyı
        // üretmek, işaretsiz kalınca çok daha az tuzaklı.
        return (int)(hash & 0x7FFFFFFF);
    }
}

public sealed class SpawnSettings
{
    /// <summary>Başlangıç noktasının bağlı olduğu açık alan en az bu kadar tile olmalı.</summary>
    [JsonPropertyName("minimumOpenArea")] public int MinimumOpenArea { get; init; } = 120;

    [JsonPropertyName("searchRadius")] public int SearchRadius { get; init; } = 400;
}
