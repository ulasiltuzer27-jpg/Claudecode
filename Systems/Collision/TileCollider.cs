using Microsoft.Xna.Framework;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Collision;

/// <summary>
/// Float hassasiyetli eksen hizalı kutu. MonoGame'in <see cref="Rectangle"/>'ı
/// int tabanlı; hareket alt-pixel hızlarla yapıldığı için burada float gerekiyor.
/// </summary>
public readonly struct Aabb(float x, float y, float width, float height)
{
    public readonly float X = x;
    public readonly float Y = y;
    public readonly float Width = width;
    public readonly float Height = height;

    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    public Aabb Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);
}

/// <summary>
/// Tile ızgarasına karşı çarpışma çözümü.
///
/// Yöntem: EKSEN AYRIK hareket. Önce X ekseninde ilerlenir ve çakışma varsa
/// geri itilir, sonra Y ekseninde aynısı yapılır.
///
/// Neden tek seferde değil de iki adımda?
/// Tek adımda çözülürse duvara çapraz bastığında karakter takılıp kalır.
/// Eksen ayrık çözümde X bloklanır ama Y serbest kalır — karakter duvar boyunca
/// KAYAR. Top-down oyunlarda beklenen his budur.
/// </summary>
public static class TileCollider
{
    /// <summary>
    /// Çakışmayı çözmek için uygulanan minik pay. Kutu duvara tam yapışırsa
    /// bir sonraki karede yuvarlama hatası yüzünden içeri sızabilir.
    /// </summary>
    private const float Skin = 0.001f;

    /// <summary>
    /// <paramref name="box"/>'ı <paramref name="delta"/> kadar hareket ettirmeye
    /// çalışır, katı tile'lara çarpınca durdurur.
    /// </summary>
    /// <returns>Gerçekte uygulanabilen hareket vektörü.</returns>
    public static Vector2 Move(TileMap map, Aabb box, Vector2 delta)
    {
        var applied = Vector2.Zero;

        // --- X ekseni ---
        if (delta.X != 0f)
        {
            var moved = box.Offset(delta.X, 0f);

            if (Overlaps(map, moved))
            {
                // Duvarın kenarına tam hizala.
                var tileSize = map.TileSize;
                if (delta.X > 0f)
                {
                    var wall = MathF.Floor(moved.Right / tileSize) * tileSize;
                    applied.X = wall - box.Right - Skin;
                }
                else
                {
                    var wall = (MathF.Floor(moved.Left / tileSize) + 1) * tileSize;
                    applied.X = wall - box.Left + Skin;
                }

                // Hizalama ters yöne itiyorsa hiç hareket etme (zaten içerideydi).
                if (Math.Sign(applied.X) != Math.Sign(delta.X))
                {
                    applied.X = 0f;
                }
            }
            else
            {
                applied.X = delta.X;
            }
        }

        box = box.Offset(applied.X, 0f);

        // --- Y ekseni ---
        if (delta.Y != 0f)
        {
            var moved = box.Offset(0f, delta.Y);

            if (Overlaps(map, moved))
            {
                var tileSize = map.TileSize;
                if (delta.Y > 0f)
                {
                    var wall = MathF.Floor(moved.Bottom / tileSize) * tileSize;
                    applied.Y = wall - box.Bottom - Skin;
                }
                else
                {
                    var wall = (MathF.Floor(moved.Top / tileSize) + 1) * tileSize;
                    applied.Y = wall - box.Top + Skin;
                }

                if (Math.Sign(applied.Y) != Math.Sign(delta.Y))
                {
                    applied.Y = 0f;
                }
            }
            else
            {
                applied.Y = delta.Y;
            }
        }

        return applied;
    }

    /// <summary>Kutu herhangi bir katı tile ile kesişiyor mu.</summary>
    public static bool Overlaps(TileMap map, Aabb box)
    {
        var tileSize = map.TileSize;

        var minX = (int)MathF.Floor(box.Left / tileSize);
        var maxX = (int)MathF.Floor((box.Right - Skin) / tileSize);
        var minY = (int)MathF.Floor(box.Top / tileSize);
        var maxY = (int)MathF.Floor((box.Bottom - Skin) / tileSize);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (map.IsSolidTile(x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
