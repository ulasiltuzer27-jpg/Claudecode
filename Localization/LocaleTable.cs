using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PixelSurvival.Localization;

/// <summary>
/// Tek bir dilin string tablosu — <c>Content/Localization/&lt;kod&gt;.json</c>.
/// </summary>
public sealed class LocaleTable
{
    /// <summary>ISO dil kodu: <c>en</c>, <c>tr</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; init; } = "";

    /// <summary>Dilin kendi dilindeki adı ("English", "Türkçe").</summary>
    [JsonPropertyName("name")] public string Name { get; init; } = "";

    [JsonPropertyName("strings")]
    public Dictionary<string, string> Strings { get; init; } = new(StringComparer.Ordinal);

    public static LocaleTable Load(ContentManager content, string code)
    {
        var relativePath = $"{content.RootDirectory}/Localization/{code}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var table = JsonSerializer.Deserialize<LocaleTable>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"'{relativePath}' okunamadi.");

        if (table.Code != code)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': dosya adi '{code}' ama icindeki kod '{table.Code}'.");
        }

        return table;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
