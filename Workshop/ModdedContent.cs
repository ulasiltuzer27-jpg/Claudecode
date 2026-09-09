using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.Workshop;

/// <summary>
/// AŞAMA 2 / MADDE 25 — mod içerik bindirmesi.
///
/// ── Neden gerekti ───────────────────────────────────────────────────────
/// <see cref="ModRegistry"/> bütün mod dosyalarını keşfediyor ve parmak
/// izine katıyordu, ama bindirme YALNIZCA dil tablolarında çalışıyordu.
/// Yani bir mod yeni bir item ya da tarif ekleyemiyor; keşfedilmiş ama
/// etkisiz kalıyordu.
///
/// ── Nasıl çalışır ───────────────────────────────────────────────────────
/// Mod klasöründeki yol, Content klasöründeki yolun AYNISI. Bir modun
/// <c>Items/items.json</c> dosyası, temel <c>Content/Items/items.json</c>
/// üstüne bindirilir. (Dil tabloları zaten bu kuralı kullanıyordu:
/// <c>Localization/tr.json</c>.)
///
/// Birleştirme JSON DÜĞÜM düzeyinde, tabloların kendisini tanımadan
/// yapılıyor. Alternatif her tablo için ayrı bir birleştirme kodu
/// yazmaktı — on yedi tablo, on yedi ayrı hata kaynağı.
///
/// ── Birleştirme kuralları ───────────────────────────────────────────────
///   • Nesne + nesne  → alan alan birleşir (mod yalnızca değiştirdiği
///                       alanı yazar; gerisi temel dosyadan gelir).
///   • Dizi + dizi    → <c>id</c>/<c>key</c> alanı eşleşen öğeler
///                       birleşir, eşleşmeyenler EKLENİR.
///   • Diğer her şey  → mod'un değeri kazanır.
///
/// Öğe SİLME bilinçli olarak yok: bir modun temel bir item'ı silmesi,
/// o item'a atıfta bulunan bütün tarifleri ve düşman ganimetlerini
/// geçersiz kılar ve oyun açılışta patlar. Silmek isteyen mod, item'ı
/// erişilemez hale getirebilir (örn. tarifini değiştirerek).
///
/// ── Steam yolları bindirilemez ──────────────────────────────────────────
/// <c>Steam/</c> altındaki dosyalar bindirmeye KAPALI ve bu iki dosyanın
/// ikisi de bilinçli olarak kapsam dışında:
///
///   • <c>Steam/itemdefs.json</c> — bir Workshop paketi buraya
///     yazabilseydi, oyuncunun makinesinde pazarlanabilir item tanımları
///     uydurabilirdi. Bu tek başına bir grant üretmez (o yol
///     <c>ISteamItemGrantAuthority</c>'den geçiyor) ama arayüzde sahte bir
///     envanter göstermek bile kabul edilemez.
///   • <c>Steam/achievements.json</c> — eşikler buradan geliyor. Modun
///     eşiği 1'e çekmesi, Steam profilinde gerçek değeri olan
///     achievement'ları bedavaya açmak demek olurdu.
/// </summary>
public static class ModdedContent
{
    /// <summary>
    /// Bindirmeye kapalı yol önekleri.
    ///
    /// Beyaz liste değil kara liste olmasının sebebi: kapatılan şey
    /// DAR ve iyi tanımlı (Steam ekonomisi), açılan ise bütün oyun
    /// verisi. Beyaz liste her yeni içerik dosyasında güncellenmeyi
    /// unutmaya açık olurdu ve unutulduğunda mod sessizce etkisiz kalırdı.
    /// </summary>
    private static readonly string[] BlockedPrefixes = ["Steam/"];

    private static ModRegistry? _registry;

    /// <summary>Son <see cref="Open"/> çağrılarında uygulanan bindirme sayısı.</summary>
    public static int AppliedOverlays { get; private set; }

    /// <summary>
    /// Bindirmenin kaynağı olan mod kümesini tanıtır.
    ///
    /// Statik durum, <see cref="Localization.Loc"/> ile aynı gerekçeyle:
    /// aktif mod kümesi oyun boyunca TEK bir küresel gerçek ve her tablo
    /// yükleyicisine parametre olarak geçirmek, imzaları kirletip bir
    /// yerde unutulduğunda o tablonun sessizce bindirilmemesine yol açardı.
    /// </summary>
    public static void Use(ModRegistry? registry)
    {
        _registry = registry;
        AppliedOverlays = 0;
    }

    /// <summary>
    /// Bir içerik JSON'unu, mod bindirmeleri uygulanmış hâlde açar.
    /// </summary>
    /// <param name="assetName">Uzantısız asset adı, örn. <c>Items/items</c>.</param>
    public static Stream Open(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";

        // Mod yoksa dosya OLDUGU GIBI aciliyor: ayristirip yeniden
        // seri hale getirmek, modsuz oynayan herkese bedava is yuklerdi.
        if (_registry is null || !HasOverlay(assetName, out var overlays))
        {
            return TitleContainer.OpenStream(relativePath);
        }

        JsonNode? merged;

        using (var stream = TitleContainer.OpenStream(relativePath))
        {
            merged = JsonNode.Parse(stream);
        }

        if (merged is not JsonObject baseObject)
        {
            // Kok bir nesne degilse birlestirilecek bir sey yok; temel
            // dosya oldugu gibi kullaniliyor.
            return TitleContainer.OpenStream(relativePath);
        }

        foreach (var (mod, path) in overlays)
        {
            try
            {
                using var stream = File.OpenRead(path);

                if (JsonNode.Parse(stream) is JsonObject overlay)
                {
                    Merge(baseObject, overlay);
                    AppliedOverlays++;
                }
            }
            catch (Exception ex)
            {
                // Bozuk bir bindirme modu tumden devre disi BIRAKMAZ ve
                // oyunu cokertmez: temel dosya gecerli kaliyor.
                _registry.AddWarning(
                    $"'{mod.Manifest.Id}': {assetName}.json bindirilemedi ({ex.Message}).");
            }
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(baseObject.ToJsonString()));
    }

    /// <summary>
    /// Bu asset bindirmeye açık mı.
    ///
    /// Ayrı ve genel bir metot: kuralın kendisi sınanabilir olmalı, yoksa
    /// "Steam yolları kapalı" iddiası ancak bir mod yazıp denenerek
    /// doğrulanabilirdi.
    /// </summary>
    public static bool IsOverlayAllowed(string assetName)
    {
        foreach (var prefix in BlockedPrefixes)
        {
            if (assetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    /// <summary>Bu asset için bindirme sağlayan modlar (yükleme sırasıyla).</summary>
    private static bool HasOverlay(string assetName,
                                   out List<(LoadedMod Mod, string Path)> overlays)
    {
        overlays = [];

        if (!IsOverlayAllowed(assetName)) return false;

        var wanted = assetName + ".json";

        foreach (var mod in _registry!.Active)
        {
            foreach (var relative in mod.Files)
            {
                if (!relative.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;

                overlays.Add((mod, Path.Combine(
                    mod.RootDirectory, relative.Replace('/', Path.DirectorySeparatorChar))));
            }
        }

        return overlays.Count > 0;
    }

    /// <summary>
    /// Nesneyi alan alan birleştirir; mod'un yazdığı alan kazanır.
    ///
    /// Bindirmenin KURALI bu metotta. Herkese açık olmasının sebebi
    /// sınanabilirlik: kural ancak burada, tek başına koşturularak
    /// doğrulanabiliyor.
    /// </summary>
    public static void Merge(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, value) in overlay)
        {
            // Yorum alanlari (_comment gibi) da normal alan gibi ele
            // aliniyor: veri dosyalarinda onlarin da uzerine yazilabilmeli.
            if (value is JsonObject childOverlay && target[key] is JsonObject childTarget)
            {
                Merge(childTarget, childOverlay);
            }
            else if (value is JsonArray arrayOverlay && target[key] is JsonArray arrayTarget)
            {
                MergeArray(arrayTarget, arrayOverlay);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Diziyi birleştirir: kimliği eşleşen öğeler birleşir, kalanlar eklenir.
    ///
    /// Diziyi tümden değiştirmek daha basit olurdu ama bir modun tek bir
    /// item'ın fiyatını değiştirmek için BÜTÜN item listesini kopyalaması
    /// gerekirdi — ve o kopya, temel oyun bir item eklediğinde eskimiş
    /// olurdu.
    /// </summary>
    private static void MergeArray(JsonArray target, JsonArray overlay)
    {
        foreach (var item in overlay)
        {
            if (item is JsonObject overlayObject && IdentityOf(overlayObject) is { } id)
            {
                var existing = target.OfType<JsonObject>()
                    .FirstOrDefault(e => IdentityOf(e) == id);

                if (existing is not null)
                {
                    Merge(existing, overlayObject);
                    continue;
                }
            }

            // Kimliksiz oge (orn. bir sayi listesi) ya da yeni kimlik:
            // eklenir. DeepClone sart -- bir dugum iki agaca birden
            // baglanamaz.
            target.Add(item?.DeepClone());
        }
    }

    /// <summary>
    /// Bir dizi öğesinin kimliği. <c>id</c> yoksa <c>key</c>'e bakılır
    /// (istatistikler ve hava türleri <c>key</c> kullanıyor).
    /// </summary>
    private static string? IdentityOf(JsonObject node)
    {
        foreach (var field in (string[])["id", "key"])
        {
            if (node.TryGetPropertyValue(field, out var value) &&
                value is JsonValue scalar &&
                scalar.TryGetValue<string>(out var text))
            {
                return text;
            }
        }

        return null;
    }
}
