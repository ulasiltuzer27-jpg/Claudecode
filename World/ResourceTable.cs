using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.World;

/// <summary>Tek bir toplanabilir kaynağın tanımı.</summary>
public sealed class ResourceDefinition
{
    /// <summary>Toplanabilen tile'ın anahtarı (örn. "wood").</summary>
    [JsonPropertyName("tile")] public string Tile { get; init; } = "";

    /// <summary>Envantere yazılacak kaynak adı. Madde 7'de WorldInventory bunu kullanacak.</summary>
    [JsonPropertyName("resource")] public string Resource { get; init; } = "";

    [JsonPropertyName("amount")] public int Amount { get; init; } = 1;

    /// <summary>Tuşa basılı tutulup toplamanın tamamlanması için gereken süre.</summary>
    [JsonPropertyName("harvestSeconds")] public float HarvestSeconds { get; init; } = 1f;

    /// <summary>Toplandıktan sonra tile'ın dönüşeceği anahtar.</summary>
    [JsonPropertyName("becomesTile")] public string BecomesTile { get; init; } = "";
}

/// <summary>
/// <c>Content/World/resources.json</c> dosyasının kod karşılığı.
///
/// Hangi tile'ın toplanabilir olduğu, ne kadar sürdüğü ve ne verdiği koda
/// GÖMÜLÜ DEĞİL. Odunu 3 yerine 5 yapmak için yeniden derleme gerekmez.
/// </summary>
public sealed class ResourceTable
{
    /// <summary>Karakterin baktığı yönde kaç tile uzağa uzanabildiği.</summary>
    [JsonPropertyName("reachTiles")] public int ReachTiles { get; init; } = 1;

    [JsonPropertyName("resources")] public List<ResourceDefinition> Resources { get; init; } = [];

    /// <summary>Tile indeksinden tanıma hızlı erişim. <see cref="Bind"/> ile doldurulur.</summary>
    private readonly Dictionary<int, ResourceDefinition> _byTileIndex = [];
    private readonly Dictionary<string, int> _becomesIndex = [];

    public static ResourceTable Load(ContentManager content, string assetName, Tileset tileset)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<ResourceTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.ReachTiles < 1)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': reachTiles en az 1 olmalı.");
        }

        table.Bind(tileset, relativePath);
        return table;
    }

    /// <summary>
    /// Tile anahtarlarını indekslere çözer ve tutarlılığı doğrular.
    /// Hatalı bir anahtar oyunun ortasında değil, açılışta patlar.
    /// </summary>
    private void Bind(Tileset tileset, string sourcePath)
    {
        foreach (var definition in Resources)
        {
            if (definition.Amount < 1)
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}': '{definition.Tile}' için amount en az 1 olmalı.");
            }

            if (definition.HarvestSeconds <= 0f)
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}': '{definition.Tile}' için harvestSeconds pozitif olmalı. " +
                    $"Sıfır olsaydı toplama anında biterdi ve tuşa bir kez basmak " +
                    $"tüm ekranı silerdi.");
            }

            // IndexOf bulunamayan anahtarda mevcut listeyle birlikte fırlatır.
            var tileIndex = tileset.IndexOf(definition.Tile);
            var becomesIndex = tileset.IndexOf(definition.BecomesTile);

            if (tileset.IsSolid(becomesIndex) && becomesIndex == tileIndex)
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}': '{definition.Tile}' kendine dönüşüyor — " +
                    $"toplama sonsuz döngüye girer.");
            }

            _byTileIndex[tileIndex] = definition;
            _becomesIndex[definition.Tile] = becomesIndex;
        }
    }

    /// <summary>Bu tile toplanabilir mi; öyleyse tanımı.</summary>
    public bool TryGet(int tileIndex, out ResourceDefinition definition) =>
        _byTileIndex.TryGetValue(tileIndex, out definition!);

    /// <summary>Toplandıktan sonra yerine konacak tile indeksi.</summary>
    public int GetReplacementIndex(ResourceDefinition definition) =>
        _becomesIndex[definition.Tile];

    /// <summary>Tabloda geçen tüm tile anahtarları (doğrulama için).</summary>
    public IEnumerable<string> AllTileKeys =>
        Resources.Select(r => r.Tile).Concat(Resources.Select(r => r.BecomesTile)).Distinct();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
