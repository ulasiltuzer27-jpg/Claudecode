using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.World;

/// <summary>Bir tile türünün tanımı. <c>Solid</c> çarpışma için kullanılır.</summary>
public sealed record TileDefinition(int Index, string Key, bool Solid);

/// <summary>
/// Tile şeridi PNG'si + yanındaki .json metadata'sı.
///
/// Hangi tile'ın katı olduğu KODA GÖMÜLÜ DEĞİL — <c>tileset_16.json</c> içindeki
/// <c>solid</c> alanından okunur. Taşı geçilebilir yapmak istersen sprite
/// üreticisindeki <c>TileSpec</c> satırını değiştirmen yeterli, C# aynı kalır.
/// </summary>
public sealed class Tileset
{
    private readonly TileDefinition[] _tiles;
    private readonly Dictionary<string, TileDefinition> _byKey;

    public Texture2D Texture { get; }
    public int TileSize { get; }
    public int Count => _tiles.Length;

    /// <summary>
    /// Her tile türünün kaç görsel varyantı olduğu.
    ///
    /// Tek varyantla döşenen zemin ekranda hemen fark edilen bir şahmat
    /// deseni yaratıyordu. Varyantlar bunu kırıyor; hangi varyantın
    /// kullanılacağını <see cref="TileMap"/> konumdan türetiyor.
    /// </summary>
    public int Variants { get; }

    private Tileset(Texture2D texture, TilesetMetadata meta)
    {
        Texture = texture;
        TileSize = meta.TileSize;
        // Varyant sayisi atlasin SUTUN eksenidir; satirlar tile turleridir.
        // 'variants' alani yoksa 'columns'a dusulur, o da yoksa tek varyant.
        Variants = Math.Max(1, meta.Variants > 0 ? meta.Variants : meta.Columns);
        _tiles = meta.Tiles
            .Select(t => new TileDefinition(t.Index, t.Key, t.Solid))
            .OrderBy(t => t.Index)
            .ToArray();
        _byKey = _tiles.ToDictionary(t => t.Key);
    }

    public static Tileset Load(ContentManager content, string assetName)
    {
        var texture = content.Load<Texture2D>(assetName);

        // TitleContainer: `dotnet run` calisma dizinini proje klasoru yapar,
        // exe'nin klasorunu degil. File.OpenRead burada yanlis yere bakardi.
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var meta = JsonSerializer.Deserialize<TilesetMetadata>(stream, JsonOptions)
                   ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (meta.TileSize <= 0 || meta.Tiles.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': tileSize/tiles geçersiz.");
        }

        return new Tileset(texture, meta);
    }

    public TileDefinition this[int index] => _tiles[index];

    public bool IsSolid(int index) =>
        index >= 0 && index < _tiles.Length && _tiles[index].Solid;

    /// <summary>Anahtardan tile indeksi. Harita legend'ı bunu kullanır.</summary>
    public int IndexOf(string key) => _byKey.TryGetValue(key, out var tile)
        ? tile.Index
        : throw new KeyNotFoundException(
            $"'{key}' tile'ı tileset'te yok. Mevcut: {string.Join(", ", _byKey.Keys)}");

    /// <summary>
    /// Beklenen tile anahtarlarının hepsinin var olduğunu açılışta doğrular.
    /// Sprite üreticisinde bir tile yeniden adlandırılırsa oyun ilk karede
    /// değil, LoadContent'te anlaşılır şekilde patlar.
    /// </summary>
    public void RequireTiles(params string[] keys)
    {
        var missing = keys.Where(k => !_byKey.ContainsKey(k)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Tileset'te eksik tile'lar: {string.Join(", ", missing)}. " +
                $"Metadata JSON'u ile kod uyuşmuyor.");
        }
    }

    /// <summary>Tile'ın atlas üzerindeki kaynak dikdörtgeni (varsayılan varyant).</summary>
    public Rectangle GetSourceRectangle(int index) => GetSourceRectangle(index, 0);

    /// <summary>
    /// Belirli bir varyantın kaynak dikdörtgeni.
    /// Atlas düzeni: SÜTUN = tile türü, SATIR = varyant.
    /// </summary>
    public Rectangle GetSourceRectangle(int index, int variant) => new(
        index * TileSize,
        Math.Abs(variant) % Variants * TileSize,
        TileSize,
        TileSize);

    // ---------------- JSON şeması ----------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class TilesetMetadata
    {
        [JsonPropertyName("tileSize")] public int TileSize { get; set; }
        [JsonPropertyName("columns")] public int Columns { get; set; } = 1;
        [JsonPropertyName("rows")] public int Rows { get; set; } = 1;
        [JsonPropertyName("variants")] public int Variants { get; set; } = 1;
        [JsonPropertyName("tiles")] public List<TileMetadata> Tiles { get; set; } = [];
    }

    private sealed class TileMetadata
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("solid")] public bool Solid { get; set; }
    }
}
