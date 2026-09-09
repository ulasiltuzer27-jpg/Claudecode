using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Climate;
using PixelSurvival.World;

using PixelSurvival.Workshop;

namespace PixelSurvival.Systems.Fishing;

/// <summary><c>Content/World/fishing.json</c> dosyasının kod karşılığı.</summary>
public sealed class FishingTable
{
    [JsonPropertyName("castSeconds")] public float CastSeconds { get; init; } = 0.8f;

    /// <summary>İşaretin çubukta saniyede kaç tam tur attığı.</summary>
    [JsonPropertyName("markerSpeed")] public float MarkerSpeed { get; init; } = 1.35f;

    /// <summary>Yakalama bölgesinin çubuğa oranı (0..1).</summary>
    [JsonPropertyName("catchZoneSize")] public float CatchZoneSize { get; init; } = 0.20f;

    [JsonPropertyName("rainZoneBonus")] public float RainZoneBonus { get; init; } = 0.10f;
    [JsonPropertyName("nightZonePenalty")] public float NightZonePenalty { get; init; } = 0.05f;
    [JsonPropertyName("catchItem")] public string CatchItem { get; init; } = "fish";
    [JsonPropertyName("catchAmount")] public int CatchAmount { get; init; } = 1;
    [JsonPropertyName("failCooldownSeconds")] public float FailCooldownSeconds { get; init; } = 0.6f;

    public static FishingTable Load(ContentManager content, string assetName, ItemDatabase items)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        // Mod bindirmesinden GECIYOR: bir mod bu tabloyu degistirebilir
        // ya da yeni satir ekleyebilir (bkz. ModdedContent).
        using var stream = ModdedContent.Open(content, assetName);

        var table = JsonSerializer.Deserialize<FishingTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (!items.Contains(table.CatchItem))
        {
            throw new InvalidOperationException(
                $"'{relativePath}': '{table.CatchItem}' item'ı tanımlı değil.");
        }

        if (table.CatchZoneSize is <= 0f or >= 1f)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': catchZoneSize 0 ile 1 arasında olmalı. " +
                $"1'e eşit olsaydı her deneme başarılı olurdu.");
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public enum FishingState
{
    Idle,
    Casting,
    Waiting,
    Success,
    Failed
}

/// <summary>
/// AŞAMA 2 / MADDE 13 — balıkçılık mini oyunu.
///
/// Akış: suya bak → tuşa BAS (olta atılır) → kısa bekleme → çubukta bir
/// işaret gidip gelmeye başlar → tuşu yeşil bölgede BIRAK → balık.
///
/// Zorluk madde 12'ye bağlı: yağmurda yeşil bölge genişler, gece daralır.
/// Böylece hava ve saat sadece görsel olmaktan çıkıp oynanışa dokunuyor.
///
/// KAPSAM DIŞI: balık türleri/nadirlik, olta kalitesi, yem, balık boyu,
/// koleksiyon. Şu an tek tür balık var.
/// </summary>
public sealed class FishingSystem(FishingTable table)
{
    private float _timer;
    private float _marker;
    private float _zoneStart;
    private float _zoneSize;
    private float _cooldown;

    public FishingState State { get; private set; } = FishingState.Idle;

    /// <summary>İşaretin çubuktaki konumu (0..1) — HUD çizimi için.</summary>
    public float Marker => _marker;

    public float ZoneStart => _zoneStart;
    public float ZoneSize => _zoneSize;
    public bool IsActive => State is FishingState.Casting or FishingState.Waiting;

    /// <summary>Karakter suya bakıyor mu — balığa başlanabilir mi.</summary>
    public static bool IsFacingWater(Player player, TileMap map, Tileset tileset)
    {
        var tile = Building.BuildingSystem.AimTile(player, map, 1);
        return tileset[map.GetTileIndex(tile.X, tile.Y)].Key == "water";
    }

    /// <summary>Bir karelik mini oyun mantığı.</summary>
    /// <param name="held">Balık tuşu basılı mı.</param>
    /// <returns>Yakalanan item ve miktar; yoksa null.</returns>
    public (string Item, int Amount)? Update(float deltaSeconds, bool held, bool facingWater,
                                             ClimateSystem climate, WorldInventory inventory)
    {
        if (_cooldown > 0f)
        {
            _cooldown -= deltaSeconds;
            return null;
        }

        // Sudan uzaklaşmak denemeyi iptal eder.
        if (!facingWater)
        {
            State = FishingState.Idle;
            return null;
        }

        switch (State)
        {
            case FishingState.Idle or FishingState.Success or FishingState.Failed:
                if (held)
                {
                    StartCast(climate);
                }

                return null;

            case FishingState.Casting:
                if (!held)
                {
                    State = FishingState.Idle;
                    return null;
                }

                _timer -= deltaSeconds;
                if (_timer <= 0f)
                {
                    State = FishingState.Waiting;
                    _timer = 0f;
                    _marker = 0f;
                }

                return null;

            case FishingState.Waiting:
                // İşaret 0-1 arasında gidip gelir. Testere dişi yerine üçgen
                // dalga: uçlarda ani sıçrama olmaz, göz takip edebilir.
                _timer += deltaSeconds * table.MarkerSpeed;
                var cycle = _timer % 1f;
                _marker = cycle < 0.5f ? cycle * 2f : (1f - cycle) * 2f;

                if (held)
                {
                    return null;
                }

                // Tuş bırakıldı — işaret bölgede mi?
                if (_marker < _zoneStart || _marker > _zoneStart + _zoneSize)
                {
                    State = FishingState.Failed;
                    _cooldown = table.FailCooldownSeconds;
                    return null;
                }

                // Envanter dolu: balık sessizce kaybolmasın, denemeyi iptal et.
                if (inventory.TryAdd(table.CatchItem, table.CatchAmount) > 0)
                {
                    State = FishingState.Failed;
                    _cooldown = table.FailCooldownSeconds;
                    return null;
                }

                State = FishingState.Success;
                _cooldown = table.FailCooldownSeconds;
                return (table.CatchItem, table.CatchAmount);

            default:
                return null;
        }
    }

    private void StartCast(ClimateSystem climate)
    {
        State = FishingState.Casting;
        _timer = table.CastSeconds;

        // Zorluk hava ve saate göre: yağmurda kolay, gece zor.
        _zoneSize = table.CatchZoneSize
                    + (climate.Weather.Key == "rain" ? table.RainZoneBonus : 0f)
                    - (climate.IsDaytime ? 0f : table.NightZonePenalty);

        _zoneSize = Math.Clamp(_zoneSize, 0.05f, 0.9f);

        // Bölge konumu dünya saatinden türeyen deterministik bir değerden:
        // ayrı bir PRNG taşımaya gerek yok, yine de her seferinde farklı.
        var pseudo = (float)(climate.WorldSeconds * 7.13 % 1.0);
        _zoneStart = pseudo * (1f - _zoneSize);
    }

    public void Cancel()
    {
        State = FishingState.Idle;
        _timer = 0f;
    }
}
