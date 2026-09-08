using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.Systems.Animation;

/// <summary>Tek bir animasyon satırı: hangi satır, kaç frame, ne hızda, döngülü mü.</summary>
public sealed record AnimationClip(string State, int Row, int Frames, float Fps, bool Loop);

/// <summary>
/// Bir sprite sheet PNG'si + yanındaki .json metadata'sı.
///
/// Grid düzeni KODA GÖMÜLÜ DEĞİL. Sanatçı satır sayısını veya bir state'in
/// frame sayısını değiştirdiğinde sadece JSON güncellenir, C# tarafı aynı kalır.
/// (Tutarsızlığı `python3 Tools/verify_content.py` yakalar.)
/// </summary>
public sealed class SpriteSheet
{
    private readonly Dictionary<string, AnimationClip> _clips;

    public Texture2D Texture { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }

    private SpriteSheet(Texture2D texture, SheetMetadata meta)
    {
        Texture = texture;
        FrameWidth = meta.FrameWidth;
        FrameHeight = meta.FrameHeight;
        _clips = meta.Animations.ToDictionary(
            a => a.State,
            a => new AnimationClip(a.State, a.Row, a.Frames, a.Fps, a.Loop));
    }

    /// <summary>
    /// PNG'yi Content Pipeline'dan, metadata'yı ham .json olarak yükler.
    /// </summary>
    /// <param name="assetName">Uzantısız asset adı, örn. "Characters/char_free_male".</param>
    public static SpriteSheet Load(ContentManager content, string assetName)
    {
        var texture = content.Load<Texture2D>(assetName);

        // JSON, Content.mgcb'de /copy: ile işaretli olduğu için çıktı klasörüne
        // ham haliyle kopyalanır — derlenmiş .xnb değil.
        //
        // TitleContainer.OpenStream kullanılıyor, File.OpenRead DEĞİL:
        // `dotnet run` çalışma dizinini proje klasörü yapar, exe'nin bulunduğu
        // klasörü değil. TitleContainer her platformda doğru köke göre çözer.
        var relativePath = $"{content.RootDirectory}/{assetName}.json";
        using var stream = TitleContainer.OpenStream(relativePath);

        var meta = JsonSerializer.Deserialize<SheetMetadata>(stream, JsonOptions)
                   ?? throw new InvalidOperationException(
                       $"'{relativePath}' okunamadı veya boş.");

        if (meta.FrameWidth <= 0 || meta.FrameHeight <= 0)
        {
            throw new InvalidOperationException(
                $"'{relativePath}': frameWidth/frameHeight geçersiz.");
        }

        return new SpriteSheet(texture, meta);
    }

    /// <summary>
    /// Bir state'in clip'ini döndürür. Yoksa mevcut state'leri listeleyerek fırlatır —
    /// sessizce boş kare çizmek yerine hatanın hemen görünmesi istenir.
    /// </summary>
    public AnimationClip GetClip(string state)
    {
        if (_clips.TryGetValue(state, out var clip))
        {
            return clip;
        }

        throw new KeyNotFoundException(
            $"'{state}' animasyon state'i sheet'te yok. " +
            $"Mevcut state'ler: {string.Join(", ", _clips.Keys.Order())}");
    }

    /// <summary>
    /// Beklenen state'lerin hepsinin var olduğunu açılışta doğrular.
    /// Oyunun ortasında değil, LoadContent sırasında patlaması için.
    /// </summary>
    public void RequireStates(params string[] states)
    {
        var missing = states.Where(s => !_clips.ContainsKey(s)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Sheet'te eksik animasyon state'leri: {string.Join(", ", missing)}. " +
                $"Metadata JSON'u ile kod uyuşmuyor.");
        }
    }

    /// <summary>Verilen clip ve frame için sheet üzerindeki kaynak dikdörtgeni.</summary>
    public Rectangle GetSourceRectangle(AnimationClip clip, int frameIndex)
    {
        var column = Math.Clamp(frameIndex, 0, clip.Frames - 1);
        return new Rectangle(
            column * FrameWidth,
            clip.Row * FrameHeight,
            FrameWidth,
            FrameHeight);
    }

    // ---------------- JSON şeması ----------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SheetMetadata
    {
        [JsonPropertyName("frameWidth")] public int FrameWidth { get; set; }
        [JsonPropertyName("frameHeight")] public int FrameHeight { get; set; }
        [JsonPropertyName("columns")] public int Columns { get; set; }
        [JsonPropertyName("rows")] public int Rows { get; set; }
        [JsonPropertyName("animations")] public List<AnimationMetadata> Animations { get; set; } = [];
    }

    private sealed class AnimationMetadata
    {
        [JsonPropertyName("state")] public string State { get; set; } = "";
        [JsonPropertyName("row")] public int Row { get; set; }
        [JsonPropertyName("frames")] public int Frames { get; set; }
        [JsonPropertyName("fps")] public float Fps { get; set; } = 8f;
        [JsonPropertyName("loop")] public bool Loop { get; set; } = true;
    }
}
