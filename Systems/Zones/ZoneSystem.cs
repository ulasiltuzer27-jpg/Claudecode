using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Inventory;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Zones;

public sealed class RaidSettings
{
    /// <summary>Bir yapıyı kırmak için gereken vuruş sayısı.</summary>
    [JsonPropertyName("structureHealth")] public int StructureHealth { get; init; } = 5;

    [JsonPropertyName("damagePerHit")] public int DamagePerHit { get; init; } = 1;

    /// <summary>Kırılan yapıdan saldırgana dönen malzeme oranı (0..1).</summary>
    [JsonPropertyName("materialReturnRatio")] public float MaterialReturnRatio { get; init; } = 0.5f;

    [JsonPropertyName("cooldownSeconds")] public float CooldownSeconds { get; init; } = 0.5f;

    /// <summary>Baskın yarım bırakılırsa yapının canını geri kazanma süresi.</summary>
    [JsonPropertyName("decaySeconds")] public float DecaySeconds { get; init; } = 30f;
}

/// <summary><c>Content/World/zones.json</c> dosyasının kod karşılığı.</summary>
public sealed class ZoneTable
{
    [JsonPropertyName("safeRadiusTiles")] public int SafeRadiusTiles { get; init; } = 40;
    [JsonPropertyName("npcSafeRadiusTiles")] public int NpcSafeRadiusTiles { get; init; } = 14;
    [JsonPropertyName("safeZoneBlocksPlayerDamage")] public bool BlocksPlayerDamage { get; init; } = true;
    [JsonPropertyName("safeZoneBlocksRaid")] public bool BlocksRaid { get; init; } = true;
    [JsonPropertyName("safeZoneBlocksEnemySpawn")] public bool BlocksEnemySpawn { get; init; } = true;
    [JsonPropertyName("raid")] public RaidSettings Raid { get; init; } = new();
    [JsonPropertyName("raidableTiles")] public List<string> RaidableTiles { get; init; } = [];

    public static ZoneTable Load(ContentManager content, string assetName, Tileset tileset)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<ZoneTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        tileset.RequireTiles([.. table.RaidableTiles]);

        if (table.Raid.StructureHealth < 1 || table.Raid.DamagePerHit < 1)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': structureHealth ve damagePerHit en az 1 olmalı — " +
                $"sıfır hasar baskını sonsuza kadar uzatırdı.");
        }

        if (table.Raid.MaterialReturnRatio is < 0f or > 1f)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': materialReturnRatio 0 ile 1 arasında olmalı. " +
                $"1'in üstünde olsaydı baskın malzeme ÜRETİRDİ.");
        }

        if (table.NpcSafeRadiusTiles > table.SafeRadiusTiles)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': NPC güvenli yarıçapı ana güvenli bölgeden büyük — " +
                $"muhtemelen istenmeyen bir yapılandırma.");
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public enum ZoneKind
{
    Safe,
    Pvp
}

public enum RaidOutcome
{
    Damaged,
    Destroyed,
    NotRaidable,
    InSafeZone,
    Cooldown
}

/// <summary>
/// AŞAMA 2 / MADDE 18 — güvenli bölge / PvP bölgesi ve baskın.
///
/// ════════════════════════════════════════════════════════════════════════
/// KURAL HOST'TA UYGULANIR
/// ════════════════════════════════════════════════════════════════════════
/// Bölge kontrolü <see cref="Combat.CombatSystem"/> içinden, yani hasarın
/// zaten çözüldüğü otoriter noktadan geçer. İstemcinin "ben PvP bölgesindeyim"
/// demesine güvenilmez; konumu host biliyor, kararı host veriyor.
///
/// Bölge tamamen KONUMDAN türer — kaydedilen bir durum yok. Bu kasıtlı:
/// senkronlanacak fazladan veri olmuyor, host ve istemci aynı yarıçap
/// hesabından aynı sonuca varıyor.
/// ════════════════════════════════════════════════════════════════════════
///
/// KAPSAM DIŞI: oyuncunun kendi arazisini talep etmesi (claim), klan
/// toprağı, baskın saatleri/takvimi, yapı sahipliği kaydı. Şu an her yapı
/// sahipsiz: PvP bölgesindeki her yapı herkes tarafından kırılabilir.
/// Sahiplik kaydı madde 22'deki clan sistemiyle birlikte gelmeli.
/// </summary>
public sealed class ZoneSystem(ZoneTable table, Tileset tileset)
{
    /// <summary>Hasar görmüş yapılar: tile → (kalan can, son vuruştan bu yana geçen süre).</summary>
    private readonly Dictionary<Point, StructureDamage> _damaged = [];
    private readonly HashSet<int> _raidableIndices =
        table.RaidableTiles.Select(tileset.IndexOf).ToHashSet();

    private float _cooldown;

    /// <summary>
    /// Yarım kalan malzeme iadesi burada birikir.
    ///
    /// Doğrudan <c>floor(maliyet * oran)</c> yazmak, maliyeti 1 olan yapılarda
    /// <c>floor(0.5) = 0</c> verip iadeyi TAMAMEN sıfırlıyordu. Biriktirerek
    /// iki yapı kıran oyuncu 1 malzeme alıyor — yani gerçekten "yarısı".
    /// PRNG kullanılmadı: sonuç deterministik kalsın.
    /// </summary>
    private float _salvageCredit;

    private sealed class StructureDamage
    {
        public int Health;
        public float SecondsSinceHit;
    }

    /// <summary>Güvenli bölgenin merkezi (spawn). Dünya kurulunca ayarlanır.</summary>
    public Vector2 SafeCenter { get; set; }

    /// <summary>NPC konumları — çevreleri de güvenli sayılır.</summary>
    public IReadOnlyList<Vector2> SafePoints { get; set; } = [];

    public int DamagedStructureCount => _damaged.Count;

    /// <summary>Bir dünya konumu hangi bölgede.</summary>
    public ZoneKind ZoneAt(Vector2 position, int tileSize)
    {
        var safeRadius = table.SafeRadiusTiles * tileSize;

        if (Vector2.DistanceSquared(position, SafeCenter) <= safeRadius * safeRadius)
        {
            return ZoneKind.Safe;
        }

        var npcRadius = table.NpcSafeRadiusTiles * tileSize;
        foreach (var point in SafePoints)
        {
            if (Vector2.DistanceSquared(position, point) <= npcRadius * npcRadius)
            {
                return ZoneKind.Safe;
            }
        }

        return ZoneKind.Pvp;
    }

    /// <summary>
    /// Bu konumda oyuncular birbirine hasar verebilir mi.
    ///
    /// HEDEFİN konumuna bakılır, saldıranın değil: güvenli bölgedeki bir
    /// oyuncuya sınırın hemen dışından vurmak mümkün olmamalı.
    /// </summary>
    public bool AllowsPlayerDamage(Vector2 targetPosition, int tileSize) =>
        !table.BlocksPlayerDamage || ZoneAt(targetPosition, tileSize) == ZoneKind.Pvp;

    /// <summary>Bu konumda düşman doğabilir mi.</summary>
    public bool AllowsEnemySpawn(Vector2 position, int tileSize) =>
        !table.BlocksEnemySpawn || ZoneAt(position, tileSize) == ZoneKind.Pvp;

    /// <summary>Hasarlı yapıların canını zamanla geri verir.</summary>
    public void Update(float deltaSeconds)
    {
        if (_cooldown > 0f)
        {
            _cooldown -= deltaSeconds;
        }

        if (_damaged.Count == 0)
        {
            return;
        }

        foreach (var tile in _damaged.Keys.ToArray())
        {
            var entry = _damaged[tile];
            entry.SecondsSinceHit += deltaSeconds;

            // Yarım bırakılan baskın kalıcı bir yara bırakmasın: yapı iyileşir.
            if (entry.SecondsSinceHit >= table.Raid.DecaySeconds)
            {
                _damaged.Remove(tile);
            }
        }
    }

    /// <summary>Bir yapının kalan canı (0..1). Hasar görmemişse 1.</summary>
    public float HealthFraction(Point tile) =>
        _damaged.TryGetValue(tile, out var entry)
            ? entry.Health / (float)table.Raid.StructureHealth
            : 1f;

    public bool IsRaidable(int tileIndex) => _raidableIndices.Contains(tileIndex);

    /// <summary>
    /// Bir yapıya baskın vuruşu uygular.
    /// </summary>
    /// <returns>Sonuç; yıkıldıysa <see cref="RaidOutcome.Destroyed"/>.</returns>
    public RaidOutcome Raid(TileMap map, Point tile, WorldInventory attackerInventory,
                            string? materialItem)
    {
        var tileIndex = map.GetTileIndex(tile.X, tile.Y);

        if (!IsRaidable(tileIndex))
        {
            return RaidOutcome.NotRaidable;
        }

        var worldPosition = new Vector2(
            tile.X * map.TileSize + map.TileSize / 2f, (tile.Y + 1) * map.TileSize);

        if (table.BlocksRaid && ZoneAt(worldPosition, map.TileSize) == ZoneKind.Safe)
        {
            return RaidOutcome.InSafeZone;
        }

        if (_cooldown > 0f)
        {
            return RaidOutcome.Cooldown;
        }

        _cooldown = table.Raid.CooldownSeconds;

        if (!_damaged.TryGetValue(tile, out var entry))
        {
            entry = new StructureDamage { Health = table.Raid.StructureHealth };
            _damaged[tile] = entry;
        }

        entry.Health -= table.Raid.DamagePerHit;
        entry.SecondsSinceHit = 0f;

        if (entry.Health > 0)
        {
            return RaidOutcome.Damaged;
        }

        _damaged.Remove(tile);
        map.SetTile(tile.X, tile.Y, tileset.IndexOf("dirt"));

        // Malzemenin bir KISMI döner: baskın, sahibinin sökmesinden daha
        // verimsiz olmalı, yoksa yapı kurmak yerine başkasınınkini kırmak
        // her zaman daha kârlı olurdu.
        if (materialItem is not null)
        {
            _salvageCredit += table.Raid.MaterialReturnRatio;

            var returned = (int)MathF.Floor(_salvageCredit);
            if (returned > 0)
            {
                _salvageCredit -= returned;
                attackerInventory.TryAdd(materialItem, returned);
            }
        }

        return RaidOutcome.Destroyed;
    }

    /// <summary>Dünya yeniden kurulunca hasar kayıtları temizlenir.</summary>
    public void Reset()
    {
        _damaged.Clear();
        _salvageCredit = 0f;
    }
}
