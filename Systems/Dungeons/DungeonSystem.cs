using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Dungeons;

/// <summary><c>Content/World/dungeons.json</c> dosyasının kod karşılığı.</summary>
public sealed class DungeonTable
{
    [JsonPropertyName("entranceTile")] public string EntranceTile { get; init; } = "dungeon_entrance";
    [JsonPropertyName("exitTile")] public string ExitTile { get; init; } = "dungeon_exit";
    [JsonPropertyName("entranceChancePerTile")] public double EntranceChancePerTile { get; init; } = 0.0009;
    [JsonPropertyName("entranceBiomeTiles")] public List<string> EntranceBiomeTiles { get; init; } = [];
    [JsonPropertyName("columns")] public int Columns { get; init; } = 64;
    [JsonPropertyName("rows")] public int Rows { get; init; } = 48;
    [JsonPropertyName("roomCount")] public int RoomCount { get; init; } = 9;
    [JsonPropertyName("minRoomSize")] public int MinRoomSize { get; init; } = 5;
    [JsonPropertyName("maxRoomSize")] public int MaxRoomSize { get; init; } = 11;
    [JsonPropertyName("clearRewardItem")] public string ClearRewardItem { get; init; } = "coin";
    [JsonPropertyName("clearRewardAmount")] public int ClearRewardAmount { get; init; } = 40;

    public static DungeonTable Load(ContentManager content, string assetName,
                                    Tileset tileset, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<DungeonTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        tileset.RequireTiles(table.EntranceTile, table.ExitTile);
        tileset.RequireTiles([.. table.EntranceBiomeTiles]);

        if (!items.Contains(table.ClearRewardItem))
        {
            throw new InvalidOperationException(
                $"'{relativePath}': '{table.ClearRewardItem}' item'ı tanımlı değil.");
        }

        // Oda haritaya sığmalı: kenarlarda 2 tile pay bırakılıyor.
        if (table.MaxRoomSize + 4 > Math.Min(table.Columns, table.Rows))
        {
            throw new InvalidOperationException(
                $"'{relativePath}': maxRoomSize ({table.MaxRoomSize}) harita boyutuna " +
                $"({table.Columns}x{table.Rows}) göre çok büyük — hiç oda yerleşemez.");
        }

        if (table.MinRoomSize > table.MaxRoomSize)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': minRoomSize maxRoomSize'dan büyük olamaz.");
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Oyuncunun şu an hangi haritada olduğu.</summary>
public enum WorldLocation
{
    Overworld,
    Dungeon
}

/// <summary>
/// AŞAMA 2 / MADDE 16 — zindan/instance alanları.
///
/// ════════════════════════════════════════════════════════════════════════
/// INSTANCE NE DEMEK
/// ════════════════════════════════════════════════════════════════════════
/// Zindan, üst dünyanın bir parçası DEĞİL: ayrı bir <see cref="TileMap"/>.
/// Girişte üst dünya haritası ve oyuncunun konumu saklanır, zindan haritası
/// devreye girer; çıkışta geri yüklenir.
///
/// Bunun mümkün olması, madde 5'te <c>Camera2D</c>'nin sınırını
/// <c>Rectangle?</c> yapmamız ve şimdi <c>ITileGenerator</c> soyutlamasını
/// çıkarmamız sayesinde: zindan sınırlı bir harita ve kamera kendini ona
/// göre kısıtlıyor, tek satır kamera kodu değişmeden.
///
/// Zindan tohumu GİRİŞİN DÜNYA KOORDİNATINDAN türer — aynı girişe tekrar
/// girildiğinde aynı zindan çıkar. Oyuncu haritayı öğrenebilmeli.
/// ════════════════════════════════════════════════════════════════════════
///
/// KAPSAM DIŞI: çok katlı zindan, kilitli kapı/anahtar, tuzak, zindan içi
/// ganimet sandığı, çok oyunculu instance paylaşımı (şu an zindan YEREL —
/// istemci girerse kendi kopyasını görür).
/// </summary>
public sealed class DungeonSystem(DungeonTable table, Tileset tileset)
{
    private TileMap? _overworld;
    private Vector2 _returnPosition;

    public WorldLocation Location { get; private set; } = WorldLocation.Overworld;
    public DungeonGenerator? Generator { get; private set; }
    public bool IsInside => Location == WorldLocation.Dungeon;

    /// <summary>Zindanda ve boss yenildi — çıkış açık.</summary>
    public bool ExitOpen { get; private set; }

    /// <summary>
    /// Bu tile bir zindan girişi mi. Üretim tarafında değil BURADA
    /// belirleniyor: giriş, dünya tohumundan türeyen seyrek bir hash
    /// kontrolüyle bulunur, böylece biome üreticisine dokunmaya gerek kalmıyor.
    /// </summary>
    public bool IsEntrance(TileMap map, Point tile)
    {
        var index = map.GetTileIndex(tile.X, tile.Y);

        // Zindan içindeyken giriş tile'ı çıkış anlamına gelir.
        if (IsInside)
        {
            return false;
        }

        if (!table.EntranceBiomeTiles.Contains(tileset[index].Key))
        {
            return false;
        }

        return Noise.HashToUnit(tile.X, tile.Y, map.Seed ^ 0x0D15EA5E) < table.EntranceChancePerTile;
    }

    /// <summary>Zindan içindeki çıkış tile'ı mı.</summary>
    public bool IsExit(TileMap map, Point tile) =>
        IsInside && tileset[map.GetTileIndex(tile.X, tile.Y)].Key == table.ExitTile;

    /// <summary>
    /// Zindana girer. Üst dünya haritası saklanır, yeni bir instance kurulur.
    /// </summary>
    /// <returns>Yeni harita ve oyuncunun konumu.</returns>
    public (TileMap Map, Vector2 Spawn) Enter(TileMap overworld, Player player, Point entranceTile)
    {
        _overworld = overworld;
        _returnPosition = player.Position;

        // Tohum girişin koordinatından: aynı giriş = aynı zindan.
        var seed = HashCode.Combine(overworld.Seed, entranceTile.X, entranceTile.Y);

        Generator = new DungeonGenerator(tileset, seed,
            table.Columns, table.Rows, table.RoomCount, table.MinRoomSize, table.MaxRoomSize);

        var map = new TileMap(Generator, tileset);

        Location = WorldLocation.Dungeon;
        ExitOpen = false;

        return (map, Generator.WorldPositionOf(Generator.EntranceTile));
    }

    /// <summary>Boss yenilince çıkışı açar ve ödülü verir.</summary>
    public void OnBossDefeated(TileMap dungeonMap, WorldInventory inventory)
    {
        if (!IsInside || ExitOpen || Generator is null)
        {
            return;
        }

        ExitOpen = true;

        // Çıkış girişin yerine açılır: oyuncu geldiği yoldan döner, aramaz.
        dungeonMap.SetTile(Generator.EntranceTile.X, Generator.EntranceTile.Y,
            tileset.IndexOf(table.ExitTile));

        inventory.TryAdd(table.ClearRewardItem, table.ClearRewardAmount);
    }

    /// <summary>Üst dünyaya döner.</summary>
    public (TileMap Map, Vector2 Spawn) Leave()
    {
        if (_overworld is null)
        {
            throw new InvalidOperationException("Zindanda değilken çıkış çağrıldı.");
        }

        var map = _overworld;
        var position = _returnPosition;

        _overworld = null;
        Generator = null;
        Location = WorldLocation.Overworld;
        ExitOpen = false;

        return (map, position);
    }

    /// <summary>Dünya yeniden kurulunca durum sıfırlanır.</summary>
    public void Reset()
    {
        _overworld = null;
        Generator = null;
        Location = WorldLocation.Overworld;
        ExitOpen = false;
    }
}
