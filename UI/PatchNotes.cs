using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.UI;

/// <summary>Tek bir sürümün notları.</summary>
public sealed class PatchRelease
{
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("date")] public string Date { get; init; } = "";
    [JsonPropertyName("title")] public string Title { get; init; } = "";
    [JsonPropertyName("changes")] public List<string> Changes { get; init; } = [];
}

/// <summary>
/// AŞAMA 2 / MADDE 24 — yama notları.
///
/// Notlar <c>Content/UI/patchnotes.json</c>'dan okunur; sürüm çıkarken
/// veri dosyasına bir giriş eklenir, KOD DEĞİŞMEZ. Notları koda gömmek,
/// her yamada bir derleme gerektirirdi.
///
/// Sıralama da veriden: liste dosyadaki sırayla çizilir. Kod tarih
/// ayrıştırıp sıralasaydı, biçimi bozuk tek bir tarih tüm listeyi
/// karıştırırdı.
/// </summary>
public sealed class PatchNotes
{
    [JsonPropertyName("releases")] public List<PatchRelease> Releases { get; init; } = [];

    /// <summary>En yeni sürüm — başlık çubuğunda gösterilir.</summary>
    public string LatestVersion => Releases.Count > 0 ? Releases[0].Version : "?";

    public static PatchNotes Load(ContentManager content, string assetName)
    {
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var notes = JsonSerializer.Deserialize<PatchNotes>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadi.");

        if (notes.Releases.Count == 0)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': hic surum yok. Bos bir yama notu ekrani, " +
                "ekranin hic acilmadigi izlenimi verir.");
        }

        return notes;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
