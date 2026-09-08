using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelSurvival.Workshop;

/// <summary>
/// Bir modun ne tür içerik sağladığı.
///
/// ════════════════════════════════════════════════════════════════════════
/// MODLAR KOD ÇALIŞTIRMAZ — VERİ VE ASSET SAĞLAR
/// ════════════════════════════════════════════════════════════════════════
/// Workshop'tan indirilen bir paketin kod çalıştırabilmesi, oyuncunun
/// makinesinde rastgele kod çalıştırılması demektir. Bu oyunda böyle bir
/// yol YOK ve açılmayacak: mod'lar yalnızca JSON veri dosyaları ve PNG
/// asset'leri sağlar.
///
/// Bu bir kısıtlama değil, bir güvenlik sınırı. Oyunun bütün dengesi
/// zaten veriden okunuyor (biome eşikleri, tarifler, item'lar, düşman
/// istatistikleri); veri modlanabilirse oyunun neredeyse tamamı
/// modlanabilir hale geliyor — kod yürütmeye gerek kalmadan.
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
[Flags]
public enum ModContentKind
{
    None = 0,

    /// <summary>Tile, biome, kaynak tabloları.</summary>
    World = 1 << 0,

    /// <summary>Item, tarif, inşa tanımları.</summary>
    Items = 1 << 1,

    /// <summary>Yaratık, düşman, NPC tanımları.</summary>
    Entities = 1 << 2,

    /// <summary>Kozmetik katmanlar (madde 20).</summary>
    Cosmetics = 1 << 3,

    /// <summary>Dil tabloları (madde 23).</summary>
    Localization = 1 << 4
}

/// <summary>
/// <c>mod.json</c> — bir Workshop paketinin kimliği ve içeriği.
///
/// Dosya adı sabit: klasörde <c>mod.json</c> yoksa o klasör mod değildir.
/// Esnek bir arama (herhangi bir .json'u manifest saymak) rastgele veri
/// dosyalarını mod olarak yüklemeye çalışırdı.
/// </summary>
public sealed class ModManifest
{
    /// <summary>Dosya adı sabiti — tek yerde.</summary>
    public const string FileName = "mod.json";

    /// <summary>
    /// Benzersiz kimlik. Klasör adından BAĞIMSIZ: Steam Workshop
    /// klasörleri sayısal id ile adlandırılır, oyuncunun yerel klasörü
    /// ise insan okunur olabilir. İkisinin aynı modu göstermesi mümkün.
    /// </summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("author")] public string Author { get; init; } = "";
    [JsonPropertyName("version")] public string Version { get; init; } = "1.0.0";
    [JsonPropertyName("description")] public string Description { get; init; } = "";

    /// <summary>
    /// Sağladığı içerik türleri (<c>world</c>, <c>items</c>, …).
    /// Bildirilmemiş bir türde dosya sağlamak yükleme hatasıdır: manifest
    /// ile içeriğin ayrışması, "neden çalışmıyor" sorusunun en sık cevabı.
    /// </summary>
    [JsonPropertyName("provides")] public List<string> Provides { get; init; } = [];

    /// <summary>
    /// Yükleme sırası. Küçük önce; aynı dosyayı iki mod sağlarsa BÜYÜK
    /// olan kazanır. Belirsiz sıra, aynı mod setinin iki makinede farklı
    /// sonuç vermesi demek olurdu.
    /// </summary>
    [JsonPropertyName("loadOrder")] public int LoadOrder { get; init; }

    /// <summary>
    /// Oyunun bu modun test edildiği sürümü. Uyuşmazlık ENGELLEMİYOR,
    /// yalnızca uyarı üretiyor — mod yazarını sürekli sürüm güncellemeye
    /// zorlamak, çalışan modları gereksiz yere devre dışı bırakırdı.
    /// </summary>
    [JsonPropertyName("gameVersion")] public string GameVersion { get; init; } = "";

    /// <summary>Steam Workshop'taki dosya kimliği; yerel modlarda 0.</summary>
    [JsonPropertyName("publishedFileId")] public ulong PublishedFileId { get; init; }

    [JsonIgnore] public bool IsPublished => PublishedFileId != 0;

    /// <summary>Bildirilen içerik türlerinin bayrak karşılığı.</summary>
    [JsonIgnore]
    public ModContentKind Kinds
    {
        get
        {
            var kinds = ModContentKind.None;

            foreach (var name in Provides)
            {
                if (Enum.TryParse<ModContentKind>(name, ignoreCase: true, out var kind))
                {
                    kinds |= kind;
                }
            }

            return kinds;
        }
    }

    /// <summary>Yüklendikten sonra doğrular; bozuk manifest sessizce geçmemeli.</summary>
    public void Validate(string sourcePath)
    {
        if (Id.Length == 0)
        {
            throw new InvalidOperationException($"'{sourcePath}': mod 'id' alani bos.");
        }

        // Kimlik dosya yolu olarak da kullanilabiliyor; ayirici veya ust
        // klasor gecisi iceren bir kimlik, mod klasorunun disina yazmaya
        // acilan bir kapi olurdu.
        if (Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')))
        {
            throw new InvalidOperationException(
                $"'{sourcePath}': mod kimligi '{Id}' yalnizca harf, rakam, _ - . " +
                "icerebilir.");
        }

        if (Name.Length == 0)
        {
            throw new InvalidOperationException($"'{sourcePath}': mod 'name' alani bos.");
        }

        foreach (var name in Provides)
        {
            if (!Enum.TryParse<ModContentKind>(name, ignoreCase: true, out var kind) ||
                kind == ModContentKind.None)
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}': bilinmeyen icerik turu '{name}'. Gecerli: " +
                    $"{string.Join(", ", Enum.GetNames<ModContentKind>().Where(n => n != "None"))}");
            }
        }
    }

    public static ModManifest Load(string path)
    {
        using var stream = File.OpenRead(path);

        var manifest = JsonSerializer.Deserialize<ModManifest>(stream, JsonOptions)
                       ?? throw new InvalidOperationException($"'{path}' okunamadi.");

        manifest.Validate(path);
        return manifest;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
