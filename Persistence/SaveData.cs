using System.Text.Json.Serialization;

namespace PixelSurvival.Persistence;

/// <summary>Kaydedilmiş bir tile değişikliği.</summary>
public sealed class SavedTile
{
    [JsonPropertyName("x")] public int X { get; init; }
    [JsonPropertyName("y")] public int Y { get; init; }
    [JsonPropertyName("tile")] public int Tile { get; init; }
}

/// <summary>Envanterdeki tek bir slot.</summary>
public sealed class SavedSlot
{
    [JsonPropertyName("slot")] public int Slot { get; init; }
    [JsonPropertyName("item")] public string Item { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}

/// <summary>Klan üyesi.</summary>
public sealed class SavedClanMember
{
    [JsonPropertyName("playerId")] public byte PlayerId { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("rank")] public string Rank { get; init; } = "Member";
}

/// <summary>Klan ve sahip olduğu yapılar.</summary>
public sealed class SavedClan
{
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("tag")] public string Tag { get; init; } = "";
    [JsonPropertyName("members")] public List<SavedClanMember> Members { get; init; } = [];

    /// <summary>Bu klana ait yapıların tile koordinatları.</summary>
    [JsonPropertyName("structures")] public List<SavedTile> Structures { get; init; } = [];
}

/// <summary>
/// Diske yazılan oyun durumu.
///
/// ── Ne kaydediliyor, ne kaydedilmiyor ───────────────────────────────────
/// Dünyanın kendisi kaydedilmiyor: tohumdan yeniden üretiliyor. Diske
/// yazılması gereken TEK dünya verisi oyuncunun yaptığı değişiklikler
/// (<see cref="Tiles"/> — kesilen ağaç, konan duvar). Bu, projenin
/// başından beri override katmanının var oluş sebebi.
///
/// Kaydedilmeyenler bilinçli: düşmanlar, yaratıklar ve NPC'ler her
/// açılışta tohumdan yeniden doğuyor. Onları kaydetmek, kayıt dosyasını
/// dünya büyüklüğünde şişirir ve kazandırdığı tek şey "aynı tavşan aynı
/// yerde" olurdu.
/// </summary>
public sealed class SaveData
{
    /// <summary>
    /// Kayıt biçimi sürümü.
    ///
    /// Uyuşmayan sürüm SESSİZCE okunmaz. Eski bir kaydı yeni alan
    /// düzeniyle okumak, alanların yanlış yerlere oturup oyuncunun
    /// envanterini bozması demektir; açıkça reddetmek daha iyidir.
    /// </summary>
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")] public int Version { get; init; } = CurrentVersion;

    /// <summary>Kaydın alındığı gerçek zaman — menüde gösterilir.</summary>
    [JsonPropertyName("savedAtUtc")] public string SavedAtUtc { get; init; } = "";

    /// <summary>
    /// Aktif mod kümesinin parmak izi (madde 25).
    ///
    /// Modlar dünya üretimini besleyen veriyi değiştirebiliyor; farklı mod
    /// kümesiyle açılan bir kayıt, aynı tohumdan FARKLI bir dünya üretir
    /// ve kaydedilen tile değişiklikleri artık başka şeylerin üstüne
    /// oturur. Uyuşmazlık yüklemeyi engellemiyor ama UYARIYOR.
    /// </summary>
    [JsonPropertyName("modFingerprint")] public string ModFingerprint { get; init; } = "";

    // --- Dünya ---
    [JsonPropertyName("seed")] public int Seed { get; init; }
    [JsonPropertyName("worldSeconds")] public double WorldSeconds { get; init; }
    [JsonPropertyName("weather")] public string Weather { get; init; } = "";

    /// <summary>Oyuncunun yaptığı tile değişiklikleri — override katmanı.</summary>
    [JsonPropertyName("tiles")] public List<SavedTile> Tiles { get; init; } = [];

    // --- Oyuncu ---
    [JsonPropertyName("playerX")] public float PlayerX { get; init; }
    [JsonPropertyName("playerY")] public float PlayerY { get; init; }
    [JsonPropertyName("playerHealth")] public int PlayerHealth { get; init; }

    [JsonPropertyName("inventory")] public List<SavedSlot> Inventory { get; init; } = [];

    /// <summary>Kuşanılan kozmetikler: slot adı → kozmetik kimliği.</summary>
    [JsonPropertyName("cosmetics")]
    public Dictionary<string, string> Cosmetics { get; init; } = new(StringComparer.Ordinal);

    // --- İlerleme ---
    [JsonPropertyName("stats")]
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.Ordinal);

    [JsonPropertyName("unlocked")] public List<string> Unlocked { get; init; } = [];

    [JsonPropertyName("clan")] public SavedClan? Clan { get; init; }
}
