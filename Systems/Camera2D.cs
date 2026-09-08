using Microsoft.Xna.Framework;

namespace PixelSurvival.Systems;

/// <summary>
/// Top-down 2D kamera. Dünya koordinatlarını ekran koordinatlarına çeviren
/// bir matris üretir; çizim kodu artık ölçek/kaydırma hesabı yapmaz.
///
/// Madde 3'teki <c>PixelScale</c> sabiti buraya <see cref="Zoom"/> olarak taşındı.
/// </summary>
public sealed class Camera2D(int viewportWidth, int viewportHeight, float zoom)
{
    /// <summary>
    /// Takip yumuşatması. 1'e yaklaştıkça kamera karaktere daha sıkı yapışır.
    /// Değer kare süresine göre üstel olarak uygulanır (aşağıya bak), bu yüzden
    /// FPS değişince his değişmez.
    /// </summary>
    private const float FollowSharpness = 12f;

    /// <summary>Kameranın dünya koordinatlarında baktığı merkez nokta.</summary>
    public Vector2 Position { get; private set; }

    public float Zoom { get; } = zoom;

    /// <summary>Görüntülenen dünya alanının pixel cinsinden boyutu.</summary>
    public Vector2 ViewSize => new(viewportWidth / Zoom, viewportHeight / Zoom);

    /// <summary>
    /// Kamerayı hedefe anında oturtur (başlangıçta veya ışınlanmada).
    /// <paramref name="worldBounds"/> null ise sınır uygulanmaz — madde 5'ten
    /// itibaren dünya sonsuz olduğu için normal durum budur. Sınırlı alanlar
    /// (madde 16'daki zindan/instance) bir dikdörtgen geçirir.
    /// </summary>
    public void SnapTo(Vector2 target, Rectangle? worldBounds = null)
    {
        Position = ClampToWorld(target, worldBounds);
    }

    /// <summary>Kamerayı hedefe doğru yumuşakça hareket ettirir.</summary>
    public void Follow(Vector2 target, GameTime gameTime, Rectangle? worldBounds = null)
    {
        var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Üstel yumuşatma: sabit bir lerp katsayısı FPS'e bağımlı olurdu
        // (144 FPS'te kamera 2.4 kat hızlı yakalardı). Bu formül bağımsız.
        var t = 1f - MathF.Exp(-FollowSharpness * delta);

        Position = Vector2.Lerp(Position, ClampToWorld(target, worldBounds), t);
        Position = ClampToWorld(Position, worldBounds);
    }

    /// <summary>
    /// Kamerayı harita sınırları içinde tutar — kenarda boşluk görünmez.
    /// Harita görüntüden küçükse eksen ortalanır.
    /// </summary>
    private Vector2 ClampToWorld(Vector2 target, Rectangle? bounds)
    {
        if (bounds is not { } worldBounds)
        {
            return target;
        }

        var half = ViewSize / 2f;

        var x = worldBounds.Width <= ViewSize.X
            ? worldBounds.Center.X
            : Math.Clamp(target.X, worldBounds.Left + half.X, worldBounds.Right - half.X);

        var y = worldBounds.Height <= ViewSize.Y
            ? worldBounds.Center.Y
            : Math.Clamp(target.Y, worldBounds.Top + half.Y, worldBounds.Bottom - half.Y);

        return new Vector2(x, y);
    }

    /// <summary>
    /// SpriteBatch.Begin(transformMatrix: ...) için görünüm matrisi.
    ///
    /// Kaydırma TAM SAYI ekran pixel'ine yuvarlanır. Yuvarlanmazsa kamera
    /// kesirli konumdayken tile'ların kenarında titreme (shimmer) ve
    /// 1 pixel'lik boşluk çizgileri oluşur — pixel art'ta hemen göze batar.
    /// </summary>
    public Matrix GetViewMatrix()
    {
        var translation = -Position * Zoom + new Vector2(viewportWidth, viewportHeight) / 2f;
        translation = new Vector2(MathF.Round(translation.X), MathF.Round(translation.Y));

        return Matrix.CreateScale(Zoom, Zoom, 1f) *
               Matrix.CreateTranslation(translation.X, translation.Y, 0f);
    }

    /// <summary>
    /// Görünür dünya alanı. Tile culling bunu kullanır.
    /// Kenarlarda 1 tile'lık pay bırakılır ki kesirli kamera konumunda
    /// kenardaki tile yarım kalmasın.
    /// </summary>
    public Rectangle GetVisibleWorldArea(int padding = 16)
    {
        var size = ViewSize;
        return new Rectangle(
            (int)MathF.Floor(Position.X - size.X / 2f) - padding,
            (int)MathF.Floor(Position.Y - size.Y / 2f) - padding,
            (int)MathF.Ceiling(size.X) + padding * 2,
            (int)MathF.Ceiling(size.Y) + padding * 2);
    }
}
