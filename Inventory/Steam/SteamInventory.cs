using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.Inventory.Steam;

/// <summary>
/// Steam ItemDef kimliği.
///
/// Kasten <c>string</c> DEĞİL: dünya item'ları string kimlik kullanıyor
/// (<c>"wood"</c>, <c>"plank"</c>). Farklı tip kullanmak, bir dünya item
/// kimliğinin yanlışlıkla Steam tarafına geçmesini DERLEME ZAMANINDA
/// imkânsız kılıyor. Bu, madde 19'un kritik kuralının ilk savunma hattı.
/// </summary>
public readonly record struct SteamItemDefId(int Value)
{
    public override string ToString() => $"itemdef:{Value}";
}

public sealed class SteamItemDefinition
{
    /// <summary>Steamworks'teki ItemDef numarası.</summary>
    [JsonPropertyName("defId")] public int DefId { get; init; }

    [JsonPropertyName("name")] public string Name { get; init; } = "";

    /// <summary>character_license, cosmetic, seasonal ...</summary>
    [JsonPropertyName("kind")] public string Kind { get; init; } = "cosmetic";

    /// <summary>common / rare / epic / legendary — YALNIZCA görsel fark demektir.</summary>
    [JsonPropertyName("rarity")] public string Rarity { get; init; } = "common";

    /// <summary>Topluluk pazarında satılabilir mi (Steam tarafında tanımlı).</summary>
    [JsonPropertyName("marketable")] public bool Marketable { get; init; }

    [JsonPropertyName("tradable")] public bool Tradable { get; init; }

    /// <summary>Sezonluk item'lar için erişim penceresi (ISO tarih, boş = sınırsız).</summary>
    [JsonPropertyName("availableFrom")] public string AvailableFrom { get; init; } = "";
    [JsonPropertyName("availableUntil")] public string AvailableUntil { get; init; } = "";

    public SteamItemDefId Id => new(DefId);

    public bool IsAvailable(DateTime utcNow)
    {
        if (!string.IsNullOrEmpty(AvailableFrom) &&
            DateTime.TryParse(AvailableFrom, out var from) && utcNow < from)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(AvailableUntil) &&
            DateTime.TryParse(AvailableUntil, out var until) && utcNow > until)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Steam'in doğrulayabildiği bir takas tarifi.
///
/// GİRDİLERİ DE STEAM ITEM'IDIR. Dünya kaynağı (odun, taş) girdi olarak
/// KABUL EDİLMEZ — tip sistemi bunu zaten engelliyor. Oyun içi malzemeden
/// Steam item'ı üretilecekse yol <see cref="ISteamItemGrantAuthority"/>
/// üzerinden, güvenilir backend ile geçer.
/// </summary>
public sealed class SteamExchangeRecipe
{
    [JsonPropertyName("outputDefId")] public int OutputDefId { get; init; }

    /// <summary>Girdi ItemDef'leri ve adetleri — hepsi Steam item'ı.</summary>
    [JsonPropertyName("inputs")] public List<SteamExchangeInput> Inputs { get; init; } = [];
}

public sealed class SteamExchangeInput
{
    [JsonPropertyName("defId")] public int DefId { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; } = 1;
}

/// <summary>Oyuncunun sahip olduğu tek bir Steam item örneği.</summary>
public readonly record struct SteamItemInstance(ulong InstanceId, SteamItemDefId DefId, int Quantity);

/// <summary><c>Content/Steam/itemdefs.json</c> — Steamworks ItemDef'lerinin yerel aynası.</summary>
public sealed class SteamItemCatalog
{
    [JsonPropertyName("appId")] public uint AppId { get; init; }
    [JsonPropertyName("items")] public List<SteamItemDefinition> Items { get; init; } = [];
    [JsonPropertyName("exchanges")] public List<SteamExchangeRecipe> Exchanges { get; init; } = [];

    private readonly Dictionary<int, SteamItemDefinition> _byId = [];

    public static SteamItemCatalog Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var catalog = JsonSerializer.Deserialize<SteamItemCatalog>(stream, JsonOptions)
                      ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        foreach (var item in catalog.Items)
        {
            if (!catalog._byId.TryAdd(item.DefId, item))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': ItemDef {item.DefId} iki kez tanımlanmış.");
            }
        }

        foreach (var exchange in catalog.Exchanges)
        {
            if (!catalog._byId.ContainsKey(exchange.OutputDefId))
            {
                throw new InvalidOperationException(
                    $"'{relativePath}': takas çıktısı {exchange.OutputDefId} tanımlı değil.");
            }

            foreach (var input in exchange.Inputs)
            {
                if (!catalog._byId.ContainsKey(input.DefId))
                {
                    throw new InvalidOperationException(
                        $"'{relativePath}': takas girdisi {input.DefId} tanımlı değil.");
                }
            }
        }

        return catalog;
    }

    public bool TryGet(SteamItemDefId id, out SteamItemDefinition definition) =>
        _byId.TryGetValue(id.Value, out definition!);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// AŞAMA 2 / MADDE 19 — Steam envanteri.
///
/// ════════════════════════════════════════════════════════════════════════
/// BU SINIF HOST AUTHORITATIVE DEĞİLDİR
/// ════════════════════════════════════════════════════════════════════════
/// Sahiplik Steam Inventory Service tarafından doğrulanır. Dünyayı açan
/// oyuncunun makinesi (player-host) bu item'ların sahipliğini yerel save
/// verisine dayanarak OLUŞTURAMAZ ve keyfi olarak GRANT EDEMEZ.
///
/// Bu sınıfın yaptığı tek şey Steam'in söylediğini OKUMAK ve Steam'in
/// istemci tarafında izin verdiği işlemleri (consume, exchange) çağırmaktır.
///
/// ── Neden burada bir "Grant" metodu YOK ─────────────────────────────────
/// Steamworks'te item üretmek (GenerateItems) bir Web API çağrısıdır ve
/// PUBLISHER/ECONOMY API KEY ister. O anahtar hiçbir koşulda oyun
/// client'ına veya player-host'a gömülmez. Bu yüzden grant yolu bu sınıfta
/// değil, <see cref="ISteamItemGrantAuthority"/> arkasında — ve o arayüzün
/// tek meşru implementasyonu güvenilir bir backend'dir.
///
/// ── KRİTİK KURAL ────────────────────────────────────────────────────────
/// WorldInventory verisi TEK BAŞINA marketable/tradable bir Steam item'ı
/// grant EDEMEZ. Oyun içi crafting sonucu Steam item'ı üretilecekse bu,
/// Steam'in doğrulayabildiği bir exchange recipe (girdileri de Steam item'ı)
/// veya güvenilir backend üzerinden yapılır. Asla doğrudan
/// WorldInventory → SteamInventory geçişi olarak kodlanmaz.
///
/// YAPISAL SAVUNMALAR:
///   1. Steam item kimliği <see cref="SteamItemDefId"/> (int sarmalayıcı),
///      dünya item kimliği <c>string</c> — biri diğerinin yerine geçemez.
///   2. Bu dosyada <c>WorldInventory</c> tipine referans YOKTUR ve olmamalı.
///      <c>Tools/verify_content.py</c> bunu her çalıştığında denetler.
///   3. Depoda publisher/Web API anahtarına benzeyen bir dize aranır.
/// ════════════════════════════════════════════════════════════════════════
///
/// KAPSAM DIŞI: gerçek Steamworks çağrıları yalnızca <c>STEAM_BUILD</c>
/// derleme sembolüyle etkin. Sembol yokken bu sınıf boş bir envanter
/// döndürür; geliştirme derlemesi Steam istemcisi olmadan çalışır.
/// </summary>
public sealed class SteamInventory(SteamItemCatalog catalog)
{
    private readonly List<SteamItemInstance> _items = [];

    /// <summary>Steam'in bildirdiği son sahiplik listesi. SALT OKUNUR.</summary>
    public IReadOnlyList<SteamItemInstance> Items => _items;

    /// <summary>Steam bağlantısı kurulabildi mi.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Son durumun ÇEVİRİ ANAHTARI — çevrilmiş metin değil.
    ///
    /// Metin saklansaydı, dil değiştiğinde (madde 23) bu satır eski dilde
    /// donup kalırdı: çeviri bir kez, <see cref="Refresh"/> anında
    /// yapılırdı. Anahtar saklanıp <see cref="Status"/> her okumada
    /// çevrildiği için dil değişimi anında yansıyor.
    /// </summary>
    private string _statusKey = "steam.absent";

    /// <summary>Son hata/durum açıklaması — HUD'da gösterilir.</summary>
    public string Status => Localization.Loc.T(_statusKey);

    /// <summary>
    /// Steam'den sahiplik listesini tazeler.
    ///
    /// Sonuç Steam'den gelir; oyun bu listeyi ÜRETMEZ, yalnızca alır.
    /// </summary>
    public void Refresh()
    {
#if STEAM_BUILD
        // Steamworks.NET: SteamInventory.GetAllItems + sonucun
        // GetResultItems ile okunması. Sonuç asenkron gelir; gerçek
        // uygulamada SteamInventoryResultReady_t callback'i beklenir.
        // Burada yalnızca çağrı noktası işaretleniyor.
        IsAvailable = Steamworks.SteamAPI.IsSteamRunning();
        _statusKey = IsAvailable ? "steam.inventoryRead" : "steam.notRunning";
#else
        IsAvailable = false;
        _statusKey = "steam.absent";
        _items.Clear();
#endif
    }

    /// <summary>
    /// Bir item'ı tüketir. Steam'in istemci tarafında İZİN VERDİĞİ işlem.
    /// </summary>
    public bool TryConsume(ulong instanceId, int quantity)
    {
#if STEAM_BUILD
        return Steamworks.SteamInventory.ConsumeItem(out _, new Steamworks.SteamItemInstanceID_t(instanceId), (uint)quantity);
#else
        _ = instanceId;
        _ = quantity;
        return false;
#endif
    }

    /// <summary>
    /// Steam'in doğrulayabildiği bir takas tarifini uygular.
    ///
    /// Girdiler oyuncunun SAHİP OLDUĞU Steam item'larıdır. Dünya kaynağı
    /// girdi olarak verilemez — imza buna izin vermiyor.
    /// </summary>
    public bool TryExchange(SteamExchangeRecipe recipe, IReadOnlyList<ulong> inputInstanceIds)
    {
        if (recipe.Inputs.Count == 0)
        {
            // Girdisiz takas bedava item demek: Steam tarafında da
            // reddedilir ama buraya hiç gelmesin.
            return false;
        }

#if STEAM_BUILD
        var ids = inputInstanceIds
            .Select(id => new Steamworks.SteamItemInstanceID_t(id)).ToArray();
        var counts = recipe.Inputs.Select(i => (uint)i.Count).ToArray();

        return Steamworks.SteamInventory.ExchangeItems(
            out _,
            [new Steamworks.SteamItemDef_t(recipe.OutputDefId)], [1u], 1,
            ids, counts, (uint)ids.Length);
#else
        _ = inputInstanceIds;
        return false;
#endif
    }

    /// <summary>
    /// Steam'in oynanış süresine bağlı düşürme mekanizmasını tetikler.
    /// Hız sınırı Steam tarafındadır; oyun bunu zorlayamaz.
    /// </summary>
    public bool RequestPlaytimeDrop(SteamItemDefId defId)
    {
        if (!catalog.TryGet(defId, out var definition) ||
            !definition.IsAvailable(DateTime.UtcNow))
        {
            return false;
        }

#if STEAM_BUILD
        return Steamworks.SteamInventory.TriggerItemDrop(
            out _, new Steamworks.SteamItemDef_t(defId.Value));
#else
        return false;
#endif
    }
}
