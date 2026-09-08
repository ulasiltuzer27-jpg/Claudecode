using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.UI;

/// <summary>
/// Atlas tabanlı bitmap font.
///
/// NEDEN SpriteFont değil?
/// MonoGame'in SpriteFont'u Content Pipeline'da FontDescriptionProcessor
/// kullanır ve DERLEYEN MAKİNEDE kurulu bir font ister. Bu, projeyi
/// "benim makinemde çalışıyor" durumuna açık hale getirir ve CI'da kırılır.
/// Atlas <c>Tools/generate_placeholders.py</c> tarafından bir kez pişirilip
/// repoya konur; oyun sadece bir doku yükler, font bağımlılığı sıfırdır.
///
/// Atlas Türkçe harfleri de içerir (ç, ğ, ı, ö, ş, ü ve büyükleri).
/// </summary>
public sealed class BitmapFont
{
    private readonly Texture2D _texture;
    private readonly Dictionary<char, Glyph> _glyphs = [];
    private readonly int _fallbackAdvance;

    public int LineHeight { get; }

    /// <summary>
    /// Mürekkebin hücre içindeki üst kenarı ve yüksekliği.
    ///
    /// Hücre 16 pixel ama gliflerin mürekkebi 13 satır; üstte ve altta boş
    /// pay var. Bir yazının arkasına kutu çizen kod (madde 24'teki emote
    /// balonu, ping işareti) satır aralığını kullanırsa kutu gözle görülür
    /// biçimde yazıyı aşkın çıkar. Bu iki değer veriden okunuyor ki
    /// sanatçı fontu değiştirdiğinde C# tarafı aynı kalsın.
    /// </summary>
    public int InkTop { get; }
    public int InkHeight { get; }

    private readonly record struct Glyph(Rectangle Source, int Advance);

    private BitmapFont(Texture2D texture, FontMetadata meta)
    {
        _texture = texture;
        LineHeight = meta.LineSpacing > 0 ? meta.LineSpacing : meta.CellHeight + 1;

        // Metadata'da yoksa tum hucre murekkep sayilir: eski bir font
        // dosyasi yuklendiginde davranis en azindan bozulmaz.
        InkTop = meta.InkHeight > 0 ? meta.InkTop : 0;
        InkHeight = meta.InkHeight > 0 ? meta.InkHeight : meta.CellHeight;

        for (var i = 0; i < meta.Glyphs.Count; i++)
        {
            var entry = meta.Glyphs[i];
            var source = new Rectangle(
                i % meta.Columns * meta.CellWidth,
                i / meta.Columns * meta.CellHeight,
                meta.CellWidth,
                meta.CellHeight);

            _glyphs[(char)entry.Code] = new Glyph(source, entry.Advance);
        }

        // Bilinmeyen karakterler için boşluk kadar yer bırakılır; çizim yapılmaz.
        _fallbackAdvance = _glyphs.TryGetValue(' ', out var space)
            ? space.Advance
            : meta.CellWidth / 2;
    }

    public static BitmapFont Load(ContentManager content, string assetName)
    {
        var texture = content.Load<Texture2D>(assetName);

        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var meta = JsonSerializer.Deserialize<FontMetadata>(stream, JsonOptions)
                   ?? throw new InvalidOperationException($"'{relativePath}' okunamadı.");

        if (meta.Glyphs.Count == 0 || meta.CellWidth <= 0 || meta.CellHeight <= 0)
        {
            throw new InvalidOperationException($"'{relativePath}': font metadata eksik.");
        }

        return new BitmapFont(texture, meta);
    }

    /// <summary>Metnin ölçekli genişliği (pixel).</summary>
    public int Measure(string text, int scale = 1)
    {
        var width = 0;
        foreach (var c in text)
        {
            width += _glyphs.TryGetValue(c, out var glyph) ? glyph.Advance : _fallbackAdvance;
        }

        return width * scale;
    }

    /// <summary>
    /// Metni çizer. Atlas beyaz + alfa olduğu için renk doğrudan tint olarak uygulanır.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, string text, Vector2 position,
                     Color color, int scale = 1)
    {
        var x = position.X;

        foreach (var c in text)
        {
            if (!_glyphs.TryGetValue(c, out var glyph))
            {
                x += _fallbackAdvance * scale;
                continue;
            }

            spriteBatch.Draw(
                _texture,
                new Vector2(x, position.Y),
                glyph.Source,
                color,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 0f);

            x += glyph.Advance * scale;
        }
    }

    private sealed class FontMetadata
    {
        [JsonPropertyName("columns")] public int Columns { get; set; }
        [JsonPropertyName("cellWidth")] public int CellWidth { get; set; }
        [JsonPropertyName("cellHeight")] public int CellHeight { get; set; }
        [JsonPropertyName("lineSpacing")] public int LineSpacing { get; set; }
        [JsonPropertyName("inkTop")] public int InkTop { get; set; }
        [JsonPropertyName("inkHeight")] public int InkHeight { get; set; }
        [JsonPropertyName("glyphs")] public List<GlyphMetadata> Glyphs { get; set; } = [];
    }

    private sealed class GlyphMetadata
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("advance")] public int Advance { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
