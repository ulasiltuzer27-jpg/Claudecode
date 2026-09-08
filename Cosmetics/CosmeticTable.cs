using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using PixelSurvival.Systems.Animation;

namespace PixelSurvival.Cosmetics;

/// <summary>
/// Kozmetik kimliği. Dünya item'ı (<c>string</c>) ve Steam ItemDef'i
/// (<c>SteamItemDefId</c>) ile karışmasın diye AYRI bir tip.
/// </summary>
public readonly record struct CosmeticId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Tek bir kozmetiğin tanımı — <c>Content/Cosmetics/cosmetics.json</c>.
///
/// ── Burada NE YOK ───────────────────────────────────────────────────────
/// Hasar, can, hız, zırh, kritik şansı… hiçbir oynanış istatistiği yok ve
/// eklenmeyecek. Nadirlik yalnızca <see cref="CosmeticRarity"/> etiketidir,
/// sayısal bir güç değil. Bkz. <see cref="CosmeticRarity"/> notu.
/// </summary>
public sealed class CosmeticDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>Uzantısız asset adı, örn. <c>Cosmetics/cos_hat_crown</c>.</summary>
    [JsonPropertyName("asset")] public string Asset { get; init; } = "";

    [JsonPropertyName("slot")] public string SlotName { get; init; } = "";
    [JsonPropertyName("rarity")] public string RarityName { get; init; } = "common";

    /// <summary>
    /// Sahipliği kanıtlayan Steam ItemDef numarası. <c>0</c> = ücretsiz
    /// (herkesin sahip olduğu başlangıç kozmetiği).
    ///
    /// Burada yalnızca NUMARA duruyor; sahiplik kanıtı DEĞİL. Oyuncunun bu
    /// item'a sahip olup olmadığı yalnızca Steam Inventory'den okunur —
    /// bkz. <see cref="ICosmeticOwnership"/>.
    /// </summary>
    [JsonPropertyName("steamDefId")] public int SteamDefId { get; init; }

    [JsonPropertyName("season")] public SeasonalWindow Season { get; init; } = SeasonalWindow.Always;

    [JsonIgnore] public CosmeticId CosmeticId => new(Id);
    [JsonIgnore] public CosmeticSlot Slot { get; private set; }
    [JsonIgnore] public CosmeticRarity Rarity { get; private set; }

    /// <summary>Steam sahipliği gerektirmeyen başlangıç kozmetiği mi.</summary>
    [JsonIgnore] public bool IsFree => SteamDefId == 0;

    /// <summary>Yüklendikten sonra metin alanlarını enum'lara çevirir ve doğrular.</summary>
    internal void Bind(string sourcePath)
    {
        if (Id.Length == 0 || Asset.Length == 0)
        {
            throw new InvalidOperationException($"'{sourcePath}': id/asset bos olamaz.");
        }

        if (!Enum.TryParse<CosmeticSlot>(SlotName, ignoreCase: true, out var slot))
        {
            throw new InvalidOperationException(
                $"'{sourcePath}': '{Id}' icin bilinmeyen slot '{SlotName}'. " +
                $"Gecerli: {string.Join(", ", CosmeticSlotExtensions.Equippable)}");
        }

        // Body bir kozmetik slotu DEGIL: beden degisimi karakter lisansidir.
        // Veri dosyasindan gelen bir "body" kozmetigi, katman yiginini
        // sessizce bozardi.
        if (slot == CosmeticSlot.Body)
        {
            throw new InvalidOperationException(
                $"'{sourcePath}': '{Id}' slot olarak 'body' kullanamaz — " +
                "beden degisimi kozmetik degil, karakter lisansidir.");
        }

        Slot = slot;
        Rarity = CosmeticRarityExtensions.Parse(RarityName);
    }
}

/// <summary>
/// AŞAMA 2 / MADDE 20 — kozmetik kataloğu ve yüklenen katman sheet'leri.
///
/// Her kozmetik, temel beden sheet'iyle AYNI grid'i kullanan bir sprite
/// sheet'tir (aynı frame boyutu, aynı satır→state eşlemesi). Bu yüzden
/// tek bir <see cref="AnimationClip"/> kaynak dikdörtgeni bütün katmanlara
/// uyar ve animasyon senkronu kendiliğinden korunur.
///
/// Yükleme sırasında grid uyuşmazlığı HATA verir: bir katmanın frame
/// boyutu bedenden farklıysa şapka kafadan kayardı ve bu, oyunun ortasında
/// fark edilen sinsi bir hata olurdu.
/// </summary>
public sealed class CosmeticTable
{
    [JsonPropertyName("cosmetics")]
    public List<CosmeticDefinition> Cosmetics { get; init; } = [];

    private readonly Dictionary<string, CosmeticDefinition> _byId = [];
    private readonly Dictionary<string, SpriteSheet> _sheets = [];

    /// <summary>Kimlikten tanıma.</summary>
    public bool TryGet(CosmeticId id, out CosmeticDefinition definition) =>
        _byId.TryGetValue(id.Value, out definition!);

    /// <summary>Bir kozmetiğin katman sheet'i.</summary>
    public SpriteSheet SheetFor(CosmeticDefinition definition) => _sheets[definition.Id];

    /// <summary>Belirli bir slottaki tüm kozmetikler (arayüzde listelemek için).</summary>
    public IEnumerable<CosmeticDefinition> InSlot(CosmeticSlot slot) =>
        Cosmetics.Where(c => c.Slot == slot);

    /// <summary>
    /// Katalogu ve tüm katman sheet'lerini yükler.
    /// </summary>
    /// <param name="reference">
    /// Temel beden sheet'i. Katmanların grid'i buna UYMAK ZORUNDA.
    /// </param>
    public static CosmeticTable Load(ContentManager content, string assetName, SpriteSheet reference)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<CosmeticTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadi.");

        foreach (var cosmetic in table.Cosmetics)
        {
            cosmetic.Bind(relativePath);

            if (!table._byId.TryAdd(cosmetic.Id, cosmetic))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': '{cosmetic.Id}' iki kez tanimlanmis.");
            }

            var sheet = SpriteSheet.Load(content, cosmetic.Asset);

            // Grid sozlesmesi: katman bedenle ayni olcude olmali.
            if (sheet.FrameWidth != reference.FrameWidth ||
                sheet.FrameHeight != reference.FrameHeight)
            {
                throw new InvalidOperationException(
                    $"'{cosmetic.Asset}' katmani {sheet.FrameWidth}x{sheet.FrameHeight}, " +
                    $"beden {reference.FrameWidth}x{reference.FrameHeight}. " +
                    "Katmanli sprite icin grid AYNI olmali.");
            }

            table._sheets[cosmetic.Id] = sheet;
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// "Bu oyuncu bu kozmetiğe sahip mi?" sorusunun TEK cevap noktası.
///
/// Neden arayüz? Sahiplik kaynağı derlemeye göre değişiyor: geliştirme
/// derlemesinde yalnızca ücretsiz kozmetikler, Steam derlemesinde Steam
/// Inventory. Kuşanma kodu hangisinin aktif olduğunu bilmemeli.
///
/// KURAL: Bu arayüzün hiçbir implementasyonu sahiplik ÜRETMEZ. Yalnızca
/// mevcut sahipliği OKUR. Item vermek madde 19'daki
/// <c>ISteamItemGrantAuthority</c> yolundan geçer.
/// </summary>
public interface ICosmeticOwnership
{
    bool Owns(CosmeticDefinition cosmetic);
}

/// <summary>
/// Geliştirme derlemesinin sahiplik kaynağı: yalnızca ücretsiz kozmetikler.
///
/// Steam istemcisi olmadan da oyunun açılıp katmanlı sprite sisteminin
/// görülebilmesi için var. Ücretli bir kozmetiği "şimdilik açalım" demez —
/// o yol bilerek kapalı.
/// </summary>
public sealed class FreeCosmeticsOnly : ICosmeticOwnership
{
    public bool Owns(CosmeticDefinition cosmetic) => cosmetic.IsFree;
}
