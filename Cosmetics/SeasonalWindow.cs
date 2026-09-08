using System.Text.Json.Serialization;

namespace PixelSurvival.Cosmetics;

/// <summary>
/// Bir kozmetiğin ELDE EDİLEBİLDİĞİ zaman aralığı (madde 20 — sezonluk item altyapısı).
///
/// ── Kritik ayrım: elde etme ≠ kullanma ──────────────────────────────────
/// Pencere yalnızca item'ın <b>alınabilirliğini</b> kapatır. Bir kez sahip
/// olunduktan sonra kozmetik SONSUZA KADAR kuşanılabilir kalır.
///
/// Tersi (pencere kapanınca kuşanılamaz olması) oyuncunun satın aldığı
/// şeyi elinden almak olurdu; Steam'de bu, item marketable/tradable
/// olduğu için gerçek bir mülkiyet sorunu yaratır. Bu yüzden
/// <see cref="CosmeticLoadout"/> kuşanırken pencereye DEĞİL, yalnızca
/// sahipliğe bakar.
///
/// İki tür kısıt birlikte kullanılabilir:
///   • <see cref="From"/>/<see cref="Until"/> — gerçek takvim (örn. bir
///     etkinlik dönemi). Steam ItemDef'iyle aynı tarihleri taşımalı.
///   • <see cref="WorldSeason"/> — oyun içi mevsim (climate.json'daki ad).
///     Yalnızca dünyada o mevsim yaşanırken düşer/satılır.
/// </summary>
public sealed class SeasonalWindow
{
    /// <summary>ISO-8601 UTC. Boş = başlangıç kısıtı yok.</summary>
    [JsonPropertyName("from")] public string From { get; init; } = "";

    /// <summary>ISO-8601 UTC. Boş = bitiş kısıtı yok.</summary>
    [JsonPropertyName("until")] public string Until { get; init; } = "";

    /// <summary>
    /// Oyun içi mevsim adı (<c>climate.json</c>: Ilkbahar/Yaz/Sonbahar/Kis).
    /// Boş = mevsim kısıtı yok.
    /// </summary>
    [JsonPropertyName("worldSeason")] public string WorldSeason { get; init; } = "";

    /// <summary>Hiçbir kısıtı olmayan pencere — her zaman açık.</summary>
    public static readonly SeasonalWindow Always = new();

    /// <summary>Herhangi bir kısıt var mı (arayüzde "sezonluk" rozeti için).</summary>
    [JsonIgnore]
    public bool IsSeasonal =>
        From.Length > 0 || Until.Length > 0 || WorldSeason.Length > 0;

    /// <summary>
    /// Şu anda elde edilebilir mi.
    /// </summary>
    /// <param name="utcNow">Gerçek takvim zamanı.</param>
    /// <param name="currentWorldSeason">
    /// Dünyanın o anki mevsimi. Boş geçilirse mevsim kısıtı DEĞERLENDİRİLMEZ
    /// (örn. henüz dünya kurulmadan katalog listelenirken).
    /// </param>
    public bool IsObtainable(DateTime utcNow, string currentWorldSeason = "")
    {
        if (From.Length > 0)
        {
            // Tarih ayrıştırılamıyorsa pencere AÇIK sayılmaz: bozuk veri
            // yüzünden sezonluk bir item'ın sürekli düşmesi, hiç düşmemesinden
            // daha kötü (geri alınamaz).
            if (!TryParseUtc(From, out var from) || utcNow < from) return false;
        }

        if (Until.Length > 0)
        {
            if (!TryParseUtc(Until, out var until) || utcNow > until) return false;
        }

        if (WorldSeason.Length > 0 && currentWorldSeason.Length > 0 &&
            !string.Equals(WorldSeason, currentWorldSeason, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>Arayüzde gösterilecek kısa açıklama.</summary>
    public string Describe()
    {
        if (!IsSeasonal) return "";
        if (WorldSeason.Length > 0 && From.Length == 0) return $"Sezon: {WorldSeason}";

        var from = From.Length > 0 ? From[..Math.Min(10, From.Length)] : "?";
        var until = Until.Length > 0 ? Until[..Math.Min(10, Until.Length)] : "?";
        return $"{from} - {until}";
    }

    private static bool TryParseUtc(string text, out DateTime value) =>
        DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                          System.Globalization.DateTimeStyles.AdjustToUniversal |
                          System.Globalization.DateTimeStyles.AssumeUniversal,
                          out value);
}
