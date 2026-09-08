using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Building;

public sealed class BuildableDefinition
{
    /// <summary>Harcanan envanter item'ı.</summary>
    [JsonPropertyName("item")] public string Item { get; init; } = "";

    /// <summary>Yerine konan tile anahtarı.</summary>
    [JsonPropertyName("tile")] public string Tile { get; init; } = "";

    [JsonPropertyName("cost")] public int Cost { get; init; } = 1;
}

/// <summary><c>Content/World/buildables.json</c> dosyasının kod karşılığı.</summary>
public sealed class BuildableTable
{
    [JsonPropertyName("reachTiles")] public int ReachTiles { get; init; } = 1;

    /// <summary>Üzerine inşa edilmesine izin verilen zemin tile'ları.</summary>
    [JsonPropertyName("buildableGround")] public List<string> BuildableGround { get; init; } = [];

    [JsonPropertyName("buildables")] public List<BuildableDefinition> Buildables { get; init; } = [];

    private readonly HashSet<int> _groundIndices = [];
    private readonly Dictionary<string, int> _tileIndices = [];

    public static BuildableTable Load(ContentManager content, string assetName,
                                      Tileset tileset, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<BuildableTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (table.Buildables.Count == 0)
        {
            throw new InvalidOperationException($"'{relativePath}': hiç inşa tanımı yok.");
        }

        foreach (var ground in table.BuildableGround)
        {
            var index = tileset.IndexOf(ground);

            if (tileset.IsSolid(index))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{ground}' katı bir tile ama inşa zemini olarak " +
                    $"listelenmiş. Katı zemine inşa, oyuncunun duvarın içine yapı " +
                    $"koymasına izin verirdi.");
            }

            table._groundIndices.Add(index);
        }

        foreach (var buildable in table.Buildables)
        {
            if (!items.Contains(buildable.Item))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{buildable.Item}' item'ı tanımlı değil.");
            }

            if (buildable.Cost < 1)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{buildable.Item}' için cost en az 1 olmalı — " +
                    $"sıfır olsaydı sonsuz yapı kurulurdu.");
            }

            table._tileIndices[buildable.Item] = tileset.IndexOf(buildable.Tile);
        }

        return table;
    }

    public bool IsBuildableGround(int tileIndex) => _groundIndices.Contains(tileIndex);
    public int TileIndexFor(BuildableDefinition buildable) => _tileIndices[buildable.Item];

    public IEnumerable<string> AllTileKeys =>
        BuildableGround.Concat(Buildables.Select(b => b.Tile)).Distinct();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public enum BuildOutcome
{
    Success,
    NotBuildableGround,
    MissingMaterial,
    BlockedByPlayer
}

/// <summary>
/// AŞAMA 1 / MADDE 9 — grid tabanlı inşa.
///
/// Yerleştirme, madde 6'da kurulan <c>TileMap</c> override katmanını kullanır:
/// yapılar chunk yeniden üretiminden bağımsız olarak kalıcıdır.
///
/// Sökme AYRI BİR SİSTEM DEĞİL — inşa edilen tile'lar resources.json'a
/// toplanabilir olarak eklendi, mevcut GatheringSystem onları söküyor.
/// İki ayrı kod yolu yazmak yerine var olanı yeniden kullanmak, "yapı
/// söküldü ama envantere girmedi" türü tutarsızlıkları baştan engelliyor.
///
/// KAPSAM DIŞI: çok tile'lı yapılar, yapı sağlığı/hasar, snap/rotasyon,
/// blueprint. Hepsi Aşama 2 işi.
/// </summary>
public sealed class BuildingSystem(BuildableTable table)
{
    private int _selectedIndex;

    public IReadOnlyList<BuildableDefinition> Buildables => table.Buildables;
    public BuildableDefinition Selected => table.Buildables[_selectedIndex];

    /// <summary>Hedef tile — inşa çerçevesi burada çizilir.</summary>
    public Point AimedTile { get; private set; }

    public void SelectNext() =>
        _selectedIndex = (_selectedIndex + 1) % table.Buildables.Count;

    public void SelectPrevious() =>
        _selectedIndex = (_selectedIndex - 1 + table.Buildables.Count) % table.Buildables.Count;

    /// <summary>Her karede hedefi tazeler (çizim ve geçerlilik göstergesi için).</summary>
    public void UpdateAim(Player player, TileMap map)
    {
        AimedTile = AimTile(player, map, table.ReachTiles);
    }

    /// <summary>Şu anki hedefe seçili yapı konabilir mi.</summary>
    public bool CanPlaceAtAim(Player player, TileMap map, WorldInventory inventory) =>
        Evaluate(player, map, inventory, out _) == BuildOutcome.Success;

    /// <summary>Seçili yapıyı hedefe koyar ve malzemeyi düşer.</summary>
    public BuildOutcome TryPlace(Player player, TileMap map, WorldInventory inventory)
    {
        var outcome = Evaluate(player, map, inventory, out var tileIndex);
        if (outcome != BuildOutcome.Success)
        {
            return outcome;
        }

        // Malzeme önce düşülür: TryRemove bütünsel, yetmezse hiçbir şey almaz.
        if (!inventory.TryRemove(Selected.Item, Selected.Cost))
        {
            return BuildOutcome.MissingMaterial;
        }

        map.SetTile(AimedTile.X, AimedTile.Y, tileIndex);
        return BuildOutcome.Success;
    }

    private BuildOutcome Evaluate(Player player, TileMap map, WorldInventory inventory,
                                  out int tileIndex)
    {
        tileIndex = table.TileIndexFor(Selected);

        if (!table.IsBuildableGround(map.GetTileIndex(AimedTile.X, AimedTile.Y)))
        {
            return BuildOutcome.NotBuildableGround;
        }

        if (!inventory.Has(Selected.Item, Selected.Cost))
        {
            return BuildOutcome.MissingMaterial;
        }

        // Oyuncunun çarpışma kutusu hedef tile'a değiyorsa katı bir yapı
        // koymak oyuncuyu duvarın içine hapsederdi.
        if (OverlapsPlayer(player, map, AimedTile))
        {
            return BuildOutcome.BlockedByPlayer;
        }

        return BuildOutcome.Success;
    }

    private static bool OverlapsPlayer(Player player, TileMap map, Point tile)
    {
        var box = player.Collider;
        var left = tile.X * map.TileSize;
        var top = tile.Y * map.TileSize;

        return box.Right > left && box.Left < left + map.TileSize &&
               box.Bottom > top && box.Top < top + map.TileSize;
    }

    /// <summary>
    /// Karakterin baktığı yöndeki tile.
    /// GatheringSystem ile aynı hesap — referans nokta collider merkezi,
    /// ayak konumu tile sınırında durduğu için hedef bir kare titrer.
    /// </summary>
    internal static Point AimTile(Player player, TileMap map, int reach)
    {
        var collider = player.Collider;
        var tileX = (int)MathF.Floor((collider.Left + collider.Width / 2f) / map.TileSize);
        var tileY = (int)MathF.Floor((collider.Top + collider.Height / 2f) / map.TileSize);

        var (dx, dy) = player.Facing switch
        {
            Facing.Left => (-1, 0),
            Facing.Right => (1, 0),
            Facing.Up => (0, -1),
            _ => (0, 1)
        };

        return new Point(tileX + dx * reach, tileY + dy * reach);
    }
}
