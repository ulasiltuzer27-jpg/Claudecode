using System.Text.Json;
using System.Text.Json.Serialization;
using PixelSurvival.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Climate;
using PixelSurvival.World;

using PixelSurvival.Workshop;

namespace PixelSurvival.Systems.Farming;

public sealed class CropDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string RawName { get; init; } = "";
    [JsonPropertyName("nameKey")] public string NameKey { get; init; } = "";

    /// <summary>Ekranda gosterilecek ad — anahtar varsa cevrilir.</summary>
    [JsonIgnore] public string Name => DataName.Of(NameKey, RawName);

    /// <summary>crops_16.png içindeki satır indeksi.</summary>
    [JsonPropertyName("row")] public int Row { get; init; }

    [JsonPropertyName("seed")] public string Seed { get; init; } = "";
    [JsonPropertyName("produce")] public string Produce { get; init; } = "";
    [JsonPropertyName("produceAmount")] public int ProduceAmount { get; init; } = 1;

    /// <summary>Hasatta geri gelen tohum — tarımın kendini beslemesi için.</summary>
    [JsonPropertyName("seedReturn")] public int SeedReturn { get; init; } = 1;

    [JsonPropertyName("daysPerStage")] public float DaysPerStage { get; init; } = 1f;

    /// <summary>Bu ekinin büyüyebildiği mevsimler. Dışında ekilir ama ilerlemez.</summary>
    [JsonPropertyName("seasons")] public List<string> Seasons { get; init; } = [];
}

public sealed class CropTable
{
    [JsonPropertyName("tilledTile")] public string TilledTile { get; init; } = "soil_tilled";
    [JsonPropertyName("tillableGround")] public List<string> TillableGround { get; init; } = [];
    [JsonPropertyName("reachTiles")] public int ReachTiles { get; init; } = 1;
    [JsonPropertyName("rainGrowthMultiplier")] public float RainGrowthMultiplier { get; init; } = 1.5f;
    [JsonPropertyName("crops")] public List<CropDefinition> Crops { get; init; } = [];

    private readonly Dictionary<string, CropDefinition> _bySeed = [];
    private readonly HashSet<int> _tillableIndices = [];

    public int TilledTileIndex { get; private set; }

    public static CropTable Load(ContentManager content, string assetName,
                                 Tileset tileset, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var table = JsonSerializer.Deserialize<CropTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        table.TilledTileIndex = tileset.IndexOf(table.TilledTile);

        foreach (var ground in table.TillableGround)
        {
            table._tillableIndices.Add(tileset.IndexOf(ground));
        }

        foreach (var crop in table.Crops)
        {
            foreach (var itemId in new[] { crop.Seed, crop.Produce })
            {
                if (!items.Contains(itemId))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': '{crop.Id}' tanımsız '{itemId}' item'ını kullanıyor.");
                }
            }

            if (crop.DaysPerStage <= 0f)
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{crop.Id}' için daysPerStage pozitif olmalı — " +
                    $"sıfır olsaydı ekin ekildiği anda hasada hazır olurdu.");
            }

            if (!table._bySeed.TryAdd(crop.Seed, crop))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{crop.Seed}' tohumu iki ekine bağlı.");
            }
        }

        return table;
    }

    public bool IsTillable(int tileIndex) => _tillableIndices.Contains(tileIndex);
    public bool TryGetBySeed(string seedItem, out CropDefinition crop) =>
        _bySeed.TryGetValue(seedItem, out crop!);

    /// <summary>
    /// Kimliğe göre ekin tanımı — kayıttan geri yükleme için.
    ///
    /// Tohuma göre arama (<see cref="TryGetBySeed"/>) burada işe yaramaz:
    /// kayıt ekinin KENDİ kimliğini tutuyor, tohumunu değil. Tohumu
    /// yazsaydık bir veri düzenlemesinde iki ekinin tohumu takas edilince
    /// kayıtlı tarlalar sessizce başka ekine dönüşürdü.
    /// </summary>
    public bool TryGetById(string cropId, out CropDefinition crop)
    {
        crop = Crops.FirstOrDefault(c => c.Id == cropId)!;
        return crop is not null;
    }

    /// <summary>
    /// Ekinin listedeki indeksi — ağda kimlik yerine bu gidiyor.
    ///
    /// Sıra kablo sözleşmesinin parçası: <c>crops.json</c> içinde bir
    /// satırın yeri değişirse eski ve yeni istemci farklı ekin çizer.
    /// Yeni ekinler SONA eklenmeli.
    /// </summary>
    public int IndexOf(string cropId) => Crops.FindIndex(c => c.Id == cropId);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Ekilmiş tek bir ekin.</summary>
public sealed class CropInstance(CropDefinition definition, double plantedAtDay)
{
    public CropDefinition Definition { get; } = definition;

    /// <summary>Ekildiği andaki dünya günü (kesirli).</summary>
    public double PlantedAtDay { get; } = plantedAtDay;

    /// <summary>Doğru mevsimde ve yağmurda biriken büyüme ilerlemesi (gün).</summary>
    public double GrowthDays { get; set; }

    /// <summary>0'dan başlayan büyüme aşaması.</summary>
    public int Stage(int stageCount) =>
        Math.Clamp((int)(GrowthDays / Definition.DaysPerStage), 0, stageCount - 1);

    public bool IsRipe(int stageCount) => Stage(stageCount) >= stageCount - 1;
}

/// <summary>
/// Bağlamsal tarım eyleminin sonucu.
///
/// Değerler ağ üzerinden bayt olarak gidiyor (tarım host'ta çözülüyor),
/// bu yüzden SIRA PROTOKOL SABİTİ: yeni sonuçlar SONA eklenmeli.
/// </summary>
public enum FarmOutcome : byte
{
    Tilled = 0,
    Planted = 1,
    Harvested = 2,
    NotTillable = 3,
    NoSeed = 4,
    AlreadyPlanted = 5,
    NotRipe = 6,
    NothingHere = 7,
    InventoryFull = 8
}

/// <summary>
/// AŞAMA 2 / MADDE 13 — tarım.
///
/// Ekin verisi tile'da DEĞİL, ayrı bir sözlükte yaşar. Bir tile indeksi
/// "hangi ekin, ne zaman ekildi, ne kadar büyüdü" bilgisini taşıyamaz.
/// Toprak (soil_tilled) tile katmanında, ekin bu katmanda.
///
/// Büyüme dünya saatine bağlı (madde 12): yalnızca ekinin mevsiminde
/// ilerler, yağmurda hızlanır. Saat host otoriter olduğu için büyüme de
/// otomatik olarak host otoriter.
///
/// KAPSAM DIŞI: sulama, gübre, çok mevsimlik ekinler, sera, ekin hastalığı.
/// </summary>
public sealed class FarmingSystem(CropTable table, int stageCount)
{
    private readonly Dictionary<Point, CropInstance> _crops = [];
    private double _lastDay = -1;

    public int PlantedCount => _crops.Count;
    public IReadOnlyDictionary<Point, CropInstance> Crops => _crops;

    /// <summary>
    /// Büyümeyi ilerletir. Host çağırır.
    ///
    /// Geçen GERÇEK süre değil, geçen DÜNYA GÜNÜ kullanılıyor: gün uzunluğu
    /// climate.json'dan değişirse tarım dengesi kendiliğinden uyum sağlar.
    /// </summary>
    public void Update(ClimateSystem climate)
    {
        // Sürekli gün değeri: Day 1'den başlıyor, DayFraction gün içi kesir.
        var currentDay = climate.Day - 1 + climate.DayFraction;

        if (_lastDay < 0)
        {
            _lastDay = currentDay;
            return;
        }

        var elapsedDays = currentDay - _lastDay;
        _lastDay = currentDay;

        if (elapsedDays <= 0)
        {
            return;
        }

        // Season.KEY, Name DEGIL: crops.json mevsimleri sabit anahtarla
        // yaziyor. Cevrilen ad kullanilsaydi Ingilizce oynayan oyuncuda
        // karsilastirma hic tutmaz ve ekinler sessizce hic buyumezdi.
        var seasonKey = climate.Season.Key;
        var rainy = climate.Weather.Key == "rain";

        foreach (var crop in _crops.Values)
        {
            // Yanlış mevsimde ekin durur — çürümez, sadece beklemeye geçer.
            if (!crop.Definition.Seasons.Contains(seasonKey))
            {
                continue;
            }

            crop.GrowthDays += elapsedDays * (rainy ? table.RainGrowthMultiplier : 1f);
        }
    }

    /// <summary>Hedef tile'daki ekin (varsa) — çizim için.</summary>
    public CropInstance? At(Point tile) => _crops.GetValueOrDefault(tile);

    /// <summary>
    /// Kayıttan gelen tarlaları geri kurar.
    ///
    /// Sürülmüş toprak tile'ları zaten override katmanından geliyor
    /// (kesilen ağaçla aynı yolla); burada geri kurulan yalnızca o
    /// toprağın ÜSTÜNDEKİ ekin. İkisi ayrı katman olduğu için ayrı
    /// kaydediliyorlar — bu, tarımın en baştaki tasarım kararının doğal
    /// sonucu.
    ///
    /// Tanımsız ekin kimlikleri atlanıyor: bir ekin veriden kaldırıldığında
    /// ya da bir mod onu değiştirdiğinde yüklemeyi reddetmek, oyuncunun
    /// bütün kaydını çöpe atmak olurdu.
    /// </summary>
    /// <returns>Geri kurulan tarla sayısı.</returns>
    public int Restore(IEnumerable<(Point Tile, string CropId, double PlantedAtDay,
                                    double GrowthDays)> saved)
    {
        _crops.Clear();

        var restored = 0;

        foreach (var (tile, cropId, plantedAtDay, growthDays) in saved)
        {
            if (!table.TryGetById(cropId, out var definition)) continue;

            _crops[tile] = new CropInstance(definition, plantedAtDay)
            {
                // Negatif buyume elle duzenlenmis bir kayittan gelebilir;
                // asama hesabi negatifte ters calisir.
                GrowthDays = Math.Max(0, growthDays)
            };

            restored++;
        }

        // Buyume gecen DUNYA GUNUNDEN hesaplaniyor ve _lastDay yuklemeden
        // once kalmis olabilir; sifirlanmazsa yuklemeden sonraki ilk
        // Update bir anda gunler kadar buyume eklerdi.
        _lastDay = -1;

        return restored;
    }

    /// <summary>
    /// Tek tuşla bağlamsal tarım eylemi: boş zemini sürer, sürülmüş toprağa
    /// eker, olgun ekini hasat eder.
    ///
    /// Üç ayrı tuş yerine tek tuş: oyuncu hangi modda olduğunu takip etmek
    /// zorunda kalmıyor, hedefe bakıp basıyor.
    /// </summary>
    public FarmOutcome Interact(Player player, TileMap map, WorldInventory inventory,
                                ClimateSystem climate, string? selectedSeed)
    {
        var tile = Building.BuildingSystem.AimTile(player, map, table.ReachTiles);
        var tileIndex = map.GetTileIndex(tile.X, tile.Y);

        // 1) Olgun ekin varsa hasat et.
        if (_crops.TryGetValue(tile, out var existing))
        {
            if (!existing.IsRipe(stageCount))
            {
                return FarmOutcome.NotRipe;
            }

            var definition = existing.Definition;

            // Ürün ve tohum envantere sığmazsa ekin SÖKÜLMEZ — oyuncu
            // envanterini boşaltıp geri gelebilsin.
            if (inventory.TryAdd(definition.Produce, definition.ProduceAmount) > 0)
            {
                return FarmOutcome.InventoryFull;
            }

            inventory.TryAdd(definition.Seed, definition.SeedReturn);
            _crops.Remove(tile);
            return FarmOutcome.Harvested;
        }

        // 2) Sürülmüş toprak varsa tohum ek.
        if (tileIndex == table.TilledTileIndex)
        {
            if (selectedSeed is null || !table.TryGetBySeed(selectedSeed, out var crop))
            {
                return FarmOutcome.NoSeed;
            }

            if (!inventory.TryRemove(crop.Seed, 1))
            {
                return FarmOutcome.NoSeed;
            }

            _crops[tile] = new CropInstance(crop, climate.Day + climate.DayFraction);
            return FarmOutcome.Planted;
        }

        // 3) Sürülebilir zemin varsa sür.
        if (table.IsTillable(tileIndex))
        {
            map.SetTile(tile.X, tile.Y, table.TilledTileIndex);
            return FarmOutcome.Tilled;
        }

        return FarmOutcome.NotTillable;
    }

    /// <summary>
    /// Sürülmüş toprak başka bir şeye dönüştüyse (inşa, sökme) ekini düşürür.
    /// Aksi halde ekin havada asılı kalırdı.
    /// </summary>
    public void DropOrphans(TileMap map)
    {
        foreach (var tile in _crops.Keys.ToArray())
        {
            if (map.GetTileIndex(tile.X, tile.Y) != table.TilledTileIndex)
            {
                _crops.Remove(tile);
            }
        }
    }

    /// <summary>Görünür alandaki ekinleri toprağın üstüne çizer.</summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D cropSheet, int tileSize,
                     Rectangle visibleWorldArea)
    {
        foreach (var (tile, crop) in _crops)
        {
            var x = tile.X * tileSize;
            var y = tile.Y * tileSize;

            if (x + tileSize < visibleWorldArea.Left || x > visibleWorldArea.Right ||
                y + tileSize < visibleWorldArea.Top || y > visibleWorldArea.Bottom)
            {
                continue;
            }

            var source = new Rectangle(
                crop.Stage(stageCount) * tileSize,
                crop.Definition.Row * tileSize,
                tileSize, tileSize);

            spriteBatch.Draw(cropSheet, new Rectangle(x, y, tileSize, tileSize),
                             source, Color.White);
        }
    }
}
