using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Engine.Ui;

/// <summary>Arayuz ikonlari (vektor; doku yok, her olcekte keskin).</summary>
public enum Icon
{
    None,
    Money,
    Clock,
    Star,
    Xp,
    Sun,
    Cloud,
    Rain,
    Snow,
    Moon,
    Plate,
    Kazan,
    Cart,
    Phone,
    Laptop,
    Box,
    Person,
    Group,
    Lock,
    Check,
    Cross,
    Warning,
    ArrowRight,
    ArrowLeft,
    Gear,
    Save,
    Exit,
    Music,
    Pin,
    Trophy,
    Calendar,
    Task,
    Heart,
    Flame,
    Drop,
    Thermo,
    Scooter,
    Gift,
    Home,
    Chart,
    Cup,
    Play,
    Sound,
    Store,
    Info,
    Plus,
    Minus,
    Ladle,
    Package,
    Map,
    News,
    Paint,
}

/// <summary>
/// raylib sekil cizimleriyle ikonlar. Her ikon merkez + boyut kutusuna
/// cizilir; <c>accent</c> iki renkli ikonlarda ikincil renktir.
/// </summary>
public static class Icons
{
    public static IEnumerable<Icon> All => Enum.GetValues<Icon>().Where(i => i != Icon.None);

    public static void Draw(Icon icon, Vector2 c, float size, Color col, Color? accent = null)
    {
        var s = size * 0.5f;
        var t = MathF.Max(1.5f, size * 0.085f);
        // Ikincil renk verilmezse ayrintilar ana rengin koyu tonuyla cizilir (beyaz ikonda kaybolmasin)
        var a = accent ?? Darken(col, 0.5f);
        Vector2 P(float x, float y) => c + new Vector2(x * s, y * s);
        void L(float x0, float y0, float x1, float y1, float th = -1) => Raylib.DrawLineEx(P(x0, y0), P(x1, y1), th < 0 ? t : th, col);
        void LA(float x0, float y0, float x1, float y1, Color k, float th = -1) => Raylib.DrawLineEx(P(x0, y0), P(x1, y1), th < 0 ? t : th, k);
        void Circ(float x, float y, float r, Color k) => Raylib.DrawCircleV(P(x, y), r * s, k);
        void Ring(float x, float y, float r, Color k, float th = -1) => Raylib.DrawRing(P(x, y), r * s - (th < 0 ? t : th) / 2, r * s + (th < 0 ? t : th) / 2, 0, 360, 36, k);
        void Rect(float x0, float y0, float x1, float y1, Color k, float round = 0.25f) =>
            Raylib.DrawRectangleRounded(new Rectangle(P(x0, y0), (x1 - x0) * s, (y1 - y0) * s), round, 6, k);
        void RectLine(float x0, float y0, float x1, float y1, Color k, float round = 0.25f) =>
            Raylib.DrawRectangleRoundedLinesEx(new Rectangle(P(x0, y0), (x1 - x0) * s, (y1 - y0) * s), round, 6, t, k);
        void Tri(Vector2 p0, Vector2 p1, Vector2 p2, Color k)
        {
            var cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            if (cross > 0)
            {
                (p1, p2) = (p2, p1);
            }

            Raylib.DrawTriangle(p0, p1, p2, k);
        }

        void Poly(Color k, params (float X, float Y)[] pts)
        {
            // Disbukey cokgen: yelpaze
            for (var i = 1; i < pts.Length - 1; i++)
            {
                Tri(P(pts[0].X, pts[0].Y), P(pts[i].X, pts[i].Y), P(pts[i + 1].X, pts[i + 1].Y), k);
            }
        }

        void StarShape(float x, float y, float r, Color k)
        {
            var outer = new Vector2[5];
            var inner = new Vector2[5];
            for (var i = 0; i < 5; i++)
            {
                var ao = -MathF.PI / 2 + i * MathF.Tau / 5;
                var ai = ao + MathF.PI / 5;
                outer[i] = P(x + MathF.Cos(ao) * r, y + MathF.Sin(ao) * r);
                inner[i] = P(x + MathF.Cos(ai) * r * 0.45f, y + MathF.Sin(ai) * r * 0.45f);
            }

            var center = P(x, y);
            for (var i = 0; i < 5; i++)
            {
                Tri(center, outer[i], inner[i], k);
                Tri(center, inner[i], outer[(i + 1) % 5], k);
            }
        }

        void Person(float x, float y, float k, Color colr)
        {
            Circ(x, y - 0.38f * k, 0.26f * k, colr);
            Raylib.DrawCircleSector(P(x, y + 0.55f * k), 0.55f * k * s, 180, 360, 20, colr);
        }

        void CloudShape(float x, float y, Color k)
        {
            Circ(x - 0.35f, y + 0.05f, 0.3f, k);
            Circ(x + 0.05f, y - 0.15f, 0.42f, k);
            Circ(x + 0.42f, y + 0.08f, 0.28f, k);
            Rect(x - 0.62f, y + 0.02f, x + 0.68f, y + 0.36f, k, 1f);
        }

        switch (icon)
        {
            case Icon.Money:
            {
                Circ(0, 0, 0.9f, col);
                Ring(0, 0, 0.72f, a.WithAlpha(0.5f), t * 0.5f);
                // ₺: dikey govde, iki egik cizgi, alttan saga kivrilan kuyruk
                var ink = accent ?? Darken(col, 0.45f);
                var th = t * 0.75f;
                LA(-0.16f, -0.5f, -0.16f, 0.12f, ink, th);
                LA(-0.42f, -0.04f, 0.18f, -0.26f, ink, th);
                LA(-0.42f, 0.18f, 0.18f, -0.04f, ink, th);
                Raylib.DrawRing(P(0.22f, 0.1f), 0.38f * s - th / 2, 0.38f * s + th / 2, 10, 180, 16, ink);
                break;
            }

            case Icon.Clock:
                Ring(0, 0, 0.8f, col);
                L(0, 0, 0, -0.5f);
                L(0, 0, 0.36f, 0.18f);
                Circ(0, 0, 0.1f, a);
                break;
            case Icon.Star:
                StarShape(0, 0.05f, 0.95f, col);
                break;
            case Icon.Xp:
                Poly(col, (0.15f, -0.95f), (-0.55f, 0.15f), (-0.02f, 0.15f));
                Poly(col, (-0.15f, 0.95f), (0.55f, -0.15f), (0.02f, -0.15f));
                Poly(col, (-0.02f, 0.15f), (0.2f, -0.15f), (0.02f, -0.15f), (-0.2f, 0.15f));
                break;
            case Icon.Sun:
                Circ(0, 0, 0.45f, col);
                for (var i = 0; i < 8; i++)
                {
                    var an = i * MathF.PI / 4;
                    L(MathF.Cos(an) * 0.62f, MathF.Sin(an) * 0.62f, MathF.Cos(an) * 0.9f, MathF.Sin(an) * 0.9f);
                }

                break;
            case Icon.Cloud:
                CloudShape(0, 0.05f, col);
                break;
            case Icon.Rain:
                CloudShape(0, -0.25f, col);
                foreach (var x in new[] { -0.4f, 0f, 0.4f })
                {
                    LA(x + 0.08f, 0.32f, x - 0.08f, 0.82f, a);
                }

                break;
            case Icon.Snow:
                for (var i = 0; i < 3; i++)
                {
                    var an = i * MathF.PI / 3 + MathF.PI / 2;
                    L(MathF.Cos(an) * 0.85f, MathF.Sin(an) * 0.85f, -MathF.Cos(an) * 0.85f, -MathF.Sin(an) * 0.85f);
                    for (var e = -1; e <= 1; e += 2)
                    {
                        var ex = MathF.Cos(an) * 0.55f * e;
                        var ey = MathF.Sin(an) * 0.55f * e;
                        var px = -MathF.Sin(an) * 0.2f;
                        var py = MathF.Cos(an) * 0.2f;
                        L(ex, ey, ex * 1.35f + px, ey * 1.35f + py, t * 0.8f);
                        L(ex, ey, ex * 1.35f - px, ey * 1.35f - py, t * 0.8f);
                    }
                }

                break;
            case Icon.Moon:
                Crescent(c, s, col);
                break;
            case Icon.Plate:
                Raylib.DrawEllipse((int)c.X, (int)(c.Y + 0.38f * s), 0.95f * s, 0.36f * s, col);
                Raylib.DrawEllipse((int)c.X, (int)(c.Y + 0.33f * s), 0.66f * s, 0.2f * s, Darken(col, 0.82f));
                Raylib.DrawCircleSector(P(0, 0.33f), 0.58f * s, 180, 360, 20, accent ?? Lighten(col, 0.6f));
                Circ(-0.2f, 0.0f, 0.06f, Darken(col, 0.7f));
                Circ(0.15f, -0.08f, 0.06f, Darken(col, 0.7f));
                Circ(0.0f, -0.2f, 0.06f, Darken(col, 0.7f));
                break;
            case Icon.Kazan:
                Rect(-0.72f, -0.25f, 0.72f, 0.7f, col, 0.35f);
                Rect(-0.85f, -0.38f, 0.85f, -0.22f, a, 1f);
                Rect(-1f, -0.05f, -0.68f, 0.12f, col, 1f);
                Rect(0.68f, -0.05f, 1f, 0.12f, col, 1f);
                foreach (var x in new[] { -0.3f, 0.05f, 0.4f })
                {
                    LA(x, -0.55f, x + 0.1f, -0.85f, a, t * 0.7f);
                }

                break;
            case Icon.Cart:
                Rect(-0.8f, -0.2f, 0.65f, 0.45f, col, 0.2f);
                Poly(a, (-0.9f, -0.35f), (0.75f, -0.35f), (0.6f, -0.7f), (-0.75f, -0.7f));
                Circ(-0.45f, 0.62f, 0.2f, col);
                Circ(0.35f, 0.62f, 0.2f, col);
                L(0.65f, -0.05f, 0.95f, -0.3f);
                break;
            case Icon.Phone:
                Rect(-0.48f, -0.92f, 0.48f, 0.92f, col, 0.3f);
                Rect(-0.36f, -0.7f, 0.36f, 0.55f, a, 0.1f);
                Circ(0, 0.74f, 0.08f, a);
                break;
            case Icon.Laptop:
                RectLine(-0.7f, -0.7f, 0.7f, 0.3f, col, 0.1f);
                Rect(-0.55f, -0.56f, 0.55f, 0.16f, a, 0.05f);
                Poly(col, (-0.85f, 0.42f), (0.85f, 0.42f), (1f, 0.62f), (-1f, 0.62f));
                break;
            case Icon.Box:
            case Icon.Package:
                Poly(col, (0f, -0.85f), (0.82f, -0.42f), (0f, 0f), (-0.82f, -0.42f));
                Poly(Darken(col, 0.8f), (-0.82f, -0.42f), (0f, 0f), (0f, 0.9f), (-0.82f, 0.45f));
                Poly(Darken(col, 0.65f), (0f, 0f), (0.82f, -0.42f), (0.82f, 0.45f), (0f, 0.9f));
                if (icon == Icon.Package)
                {
                    LA(-0.41f, -0.64f, 0.41f, -0.21f, a, t * 0.9f);
                }

                break;
            case Icon.Person:
                Person(0, 0, 1f, col);
                break;
            case Icon.Group:
                Person(0.32f, -0.05f, 0.8f, Darken(col, 0.75f));
                Person(-0.22f, 0.08f, 0.9f, col);
                break;
            case Icon.Lock:
                Raylib.DrawRing(P(0, -0.2f), 0.35f * s, 0.35f * s + t, 180, 360, 20, col);
                L(-0.35f - t / s / 2, -0.2f, -0.35f - t / s / 2, 0.05f);
                L(0.35f + t / s / 2, -0.2f, 0.35f + t / s / 2, 0.05f);
                Rect(-0.65f, 0f, 0.65f, 0.85f, col, 0.25f);
                Circ(0, 0.38f, 0.12f, a);
                break;
            case Icon.Check:
                L(-0.7f, 0.05f, -0.2f, 0.55f, t * 1.6f);
                L(-0.2f, 0.55f, 0.75f, -0.5f, t * 1.6f);
                break;
            case Icon.Cross:
                L(-0.6f, -0.6f, 0.6f, 0.6f, t * 1.6f);
                L(0.6f, -0.6f, -0.6f, 0.6f, t * 1.6f);
                break;
            case Icon.Warning:
                Poly(col, (0f, -0.9f), (0.95f, 0.8f), (-0.95f, 0.8f));
                LA(0, -0.35f, 0, 0.25f, accent ?? Darken(col, 0.3f), t * 1.3f);
                Circ(0, 0.52f, 0.1f, accent ?? Darken(col, 0.3f));
                break;
            case Icon.ArrowRight:
            case Icon.ArrowLeft:
            {
                var d = icon == Icon.ArrowRight ? 1 : -1;
                L(-0.75f * d, 0, 0.4f * d, 0, t * 1.4f);
                Poly(col, (0.85f * d, 0f), (0.25f * d, -0.55f), (0.25f * d, 0.55f));
                break;
            }

            case Icon.Gear:
                for (var i = 0; i < 8; i++)
                {
                    var an = i * 45f;
                    var p = P(MathF.Cos(an * MathF.PI / 180) * 0.68f, MathF.Sin(an * MathF.PI / 180) * 0.68f);
                    Raylib.DrawRectanglePro(new Rectangle(p, 0.36f * s, 0.32f * s), new Vector2(0.18f * s, 0.16f * s), an, col);
                }

                Circ(0, 0, 0.66f, col);
                Circ(0, 0, 0.27f, accent ?? Darken(col, 0.35f));
                break;
            case Icon.Save:
                Rect(-0.8f, -0.8f, 0.8f, 0.8f, col, 0.15f);
                Rect(-0.45f, -0.8f, 0.4f, -0.3f, a, 0.1f);
                Rect(-0.55f, 0.1f, 0.55f, 0.65f, a, 0.1f);
                break;
            case Icon.Exit:
                RectLine(-0.8f, -0.85f, 0.15f, 0.85f, col, 0.1f);
                LA(-0.2f, 0, 0.6f, 0, a, t * 1.3f);
                Poly(a, (0.95f, 0f), (0.5f, -0.38f), (0.5f, 0.38f));
                break;
            case Icon.Music:
                Raylib.DrawEllipse((int)P(-0.45f, 0.6f).X, (int)P(-0.45f, 0.6f).Y, 0.27f * s, 0.2f * s, col);
                Raylib.DrawEllipse((int)P(0.5f, 0.45f).X, (int)P(0.5f, 0.45f).Y, 0.27f * s, 0.2f * s, col);
                L(-0.2f, 0.6f, -0.2f, -0.6f);
                L(0.75f, 0.45f, 0.75f, -0.8f);
                Poly(col, (-0.24f, -0.6f), (0.79f, -0.85f), (0.79f, -0.6f), (-0.24f, -0.35f));
                break;
            case Icon.Pin:
                Circ(0, -0.3f, 0.6f, col);
                Poly(col, (-0.52f, -0.05f), (0.52f, -0.05f), (0f, 0.95f));
                Circ(0, -0.3f, 0.24f, accent ?? Color.White);
                break;
            case Icon.Trophy:
                Raylib.DrawCircleSector(P(0, -0.35f), 0.62f * s, 0, 180, 20, col);
                Rect(-0.62f, -0.8f, 0.62f, -0.35f, col, 0.1f);
                Raylib.DrawRing(P(-0.62f, -0.45f), 0.2f * s, 0.2f * s + t, 90, 270, 12, col);
                Raylib.DrawRing(P(0.62f, -0.45f), 0.2f * s, 0.2f * s + t, -90, 90, 12, col);
                L(0, 0.25f, 0, 0.6f, t * 1.4f);
                Rect(-0.45f, 0.6f, 0.45f, 0.85f, a, 0.3f);
                break;
            case Icon.Calendar:
                Rect(-0.82f, -0.62f, 0.82f, 0.85f, col, 0.15f);
                Rect(-0.82f, -0.62f, 0.82f, -0.25f, a, 0.2f);
                L(-0.42f, -0.85f, -0.42f, -0.48f);
                L(0.42f, -0.85f, 0.42f, -0.48f);
                for (var r = 0; r < 2; r++)
                {
                    for (var k = 0; k < 3; k++)
                    {
                        Circ(-0.45f + k * 0.45f, 0.1f + r * 0.38f, 0.1f, a);
                    }
                }

                break;
            case Icon.Task:
                Rect(-0.68f, -0.75f, 0.68f, 0.92f, col, 0.15f);
                Rect(-0.3f, -0.92f, 0.3f, -0.62f, a, 0.4f);
                for (var i = 0; i < 3; i++)
                {
                    var y = -0.3f + i * 0.4f;
                    LA(-0.45f, y, -0.3f, y + 0.12f, a, t * 0.8f);
                    LA(-0.3f, y + 0.12f, -0.12f, y - 0.1f, a, t * 0.8f);
                    LA(0.02f, y, 0.48f, y, a, t * 0.8f);
                }

                break;
            case Icon.Heart:
                Circ(-0.4f, -0.25f, 0.45f, col);
                Circ(0.4f, -0.25f, 0.45f, col);
                Poly(col, (-0.82f, -0.1f), (0.82f, -0.1f), (0f, 0.85f));
                break;
            case Icon.Flame:
                Circ(0, 0.35f, 0.55f, col);
                Poly(col, (-0.52f, 0.2f), (0.52f, 0.2f), (0.1f, -0.95f));
                Circ(0, 0.48f, 0.28f, accent ?? Lighten(col, 0.5f));
                Poly(accent ?? Lighten(col, 0.5f), (-0.26f, 0.42f), (0.26f, 0.42f), (0.05f, -0.15f));
                break;
            case Icon.Drop:
                Circ(0, 0.3f, 0.55f, col);
                Poly(col, (-0.52f, 0.15f), (0.52f, 0.15f), (0f, -0.95f));
                break;
            case Icon.Thermo:
                Rect(-0.2f, -0.9f, 0.2f, 0.45f, col, 1f);
                Circ(0, 0.55f, 0.35f, col);
                Rect(-0.08f, -0.3f, 0.08f, 0.5f, a, 1f);
                Circ(0, 0.55f, 0.22f, a);
                break;
            case Icon.Scooter:
                Ring(-0.6f, 0.55f, 0.25f, col);
                Ring(0.62f, 0.55f, 0.25f, col);
                Poly(col, (-0.6f, 0.25f), (0.35f, 0.25f), (0.45f, 0.5f), (-0.75f, 0.5f));
                Rect(-0.65f, -0.15f, -0.05f, 0.25f, a, 0.4f);
                L(0.4f, 0.4f, 0.55f, -0.65f);
                L(0.35f, -0.65f, 0.8f, -0.65f);
                break;
            case Icon.Gift:
                Rect(-0.78f, -0.25f, 0.78f, 0.85f, col, 0.1f);
                Rect(-0.88f, -0.45f, 0.88f, -0.15f, col, 0.2f);
                Rect(-0.12f, -0.45f, 0.12f, 0.85f, a, 0f);
                Raylib.DrawRing(P(-0.25f, -0.62f), 0.12f * s, 0.12f * s + t, 0, 360, 16, a);
                Raylib.DrawRing(P(0.25f, -0.62f), 0.12f * s, 0.12f * s + t, 0, 360, 16, a);
                break;
            case Icon.Home:
                Poly(col, (0f, -0.95f), (0.95f, -0.05f), (-0.95f, -0.05f));
                Rect(-0.68f, -0.15f, 0.68f, 0.85f, col, 0.05f);
                Rect(-0.2f, 0.3f, 0.2f, 0.85f, a, 0.1f);
                break;
            case Icon.Chart:
                Rect(-0.8f, 0.1f, -0.38f, 0.85f, col, 0.15f);
                Rect(-0.22f, -0.35f, 0.2f, 0.85f, a, 0.15f);
                Rect(0.38f, -0.8f, 0.8f, 0.85f, col, 0.15f);
                break;
            case Icon.Cup:
                Poly(col, (-0.55f, -0.8f), (0.55f, -0.8f), (0.42f, 0.85f), (-0.42f, 0.85f));
                Poly(accent ?? Lighten(col, 0.6f), (-0.5f, -0.35f), (0.5f, -0.35f), (0.42f, 0.78f), (-0.42f, 0.78f));
                break;
            case Icon.Play:
                Poly(col, (-0.55f, -0.8f), (0.85f, 0f), (-0.55f, 0.8f));
                break;
            case Icon.Sound:
                Poly(col, (-0.85f, -0.3f), (-0.45f, -0.3f), (-0.45f, 0.3f), (-0.85f, 0.3f));
                Poly(col, (-0.45f, -0.3f), (0.05f, -0.8f), (0.05f, 0.8f), (-0.45f, 0.3f));
                Raylib.DrawRing(P(0.1f, 0), 0.35f * s, 0.35f * s + t, -50, 50, 12, a);
                Raylib.DrawRing(P(0.1f, 0), 0.65f * s, 0.65f * s + t, -50, 50, 12, a);
                break;
            case Icon.Store:
                Rect(-0.8f, -0.1f, 0.8f, 0.85f, col, 0.05f);
                for (var i = 0; i < 5; i++)
                {
                    var x0 = -0.9f + i * 0.36f;
                    Poly(i % 2 == 0 ? a : Lighten(a, 0.7f), (x0, -0.75f), (x0 + 0.36f, -0.75f), (x0 + 0.36f, -0.1f), (x0, -0.1f));
                }

                Rect(-0.22f, 0.3f, 0.22f, 0.85f, Darken(col, 0.6f), 0.1f);
                break;
            case Icon.Info:
                Circ(0, 0, 0.9f, col);
                LA(0, -0.1f, 0, 0.5f, a, t * 1.4f);
                Circ(0, -0.42f, 0.11f, a);
                break;
            case Icon.Plus:
                L(-0.7f, 0, 0.7f, 0, t * 1.6f);
                L(0, -0.7f, 0, 0.7f, t * 1.6f);
                break;
            case Icon.Minus:
                L(-0.7f, 0, 0.7f, 0, t * 1.6f);
                break;
            case Icon.Ladle:
                Raylib.DrawCircleSector(P(-0.3f, 0.25f), 0.48f * s, 0, 180, 18, col);
                L(-0.3f, 0.25f, 0.8f, -0.85f, t * 1.2f);
                Rect(-0.8f, 0.2f, 0.2f, 0.3f, col, 1f);
                break;
            case Icon.Map:
                Poly(col, (-0.9f, -0.6f), (-0.3f, -0.85f), (-0.3f, 0.6f), (-0.9f, 0.85f));
                Poly(Darken(col, 0.8f), (-0.3f, -0.85f), (0.3f, -0.6f), (0.3f, 0.85f), (-0.3f, 0.6f));
                Poly(col, (0.3f, -0.6f), (0.9f, -0.85f), (0.9f, 0.6f), (0.3f, 0.85f));
                Circ(0.05f, -0.1f, 0.2f, a);
                break;
            case Icon.News:
                Rect(-0.85f, -0.75f, 0.85f, 0.8f, col, 0.1f);
                Rect(-0.65f, -0.55f, -0.05f, 0.05f, a, 0.05f);
                for (var i = 0; i < 4; i++)
                {
                    LA(0.08f, -0.5f + i * 0.17f, 0.65f, -0.5f + i * 0.17f, a, t * 0.7f);
                }

                LA(-0.65f, 0.3f, 0.65f, 0.3f, a, t * 0.7f);
                LA(-0.65f, 0.55f, 0.4f, 0.55f, a, t * 0.7f);
                break;
            case Icon.Paint:
                Rect(-0.85f, -0.85f, 0.45f, -0.2f, col, 0.3f);
                L(0.45f, -0.52f, 0.75f, -0.52f);
                L(0.75f, -0.52f, 0.75f, 0.0f);
                L(0.75f, 0.0f, 0f, 0.1f);
                Rect(-0.12f, 0.1f, 0.12f, 0.9f, a, 0.4f);
                break;
        }
    }

    /// <summary>Hilal: iki daire arasinda kalan bolge (serit ucgenlerle).</summary>
    private static void Crescent(Vector2 c, float s, Color col)
    {
        float R = 0.85f, r = 0.7f, d = 0.45f;
        var x = (d * d + R * R - r * r) / (2 * d);
        var y = MathF.Sqrt(MathF.Max(0, R * R - x * x));
        var t1 = MathF.Atan2(y, x);
        var t2 = MathF.Atan2(y, x - d);
        const int n = 20;
        Vector2 O(int i) => c + new Vector2(MathF.Cos(t1 + i / (float)n * (MathF.Tau - 2 * t1)), MathF.Sin(t1 + i / (float)n * (MathF.Tau - 2 * t1))) * R * s - new Vector2(0.15f * s, 0);
        Vector2 I(int i) => c + (new Vector2(d, 0) + new Vector2(MathF.Cos(t2 + i / (float)n * (MathF.Tau - 2 * t2)), MathF.Sin(t2 + i / (float)n * (MathF.Tau - 2 * t2))) * r) * s - new Vector2(0.15f * s, 0);
        for (var i = 0; i < n; i++)
        {
            Quad(O(i), O(i + 1), I(i + 1), I(i), col);
        }
    }

    private static void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
    {
        DrawTri(a, b, c, col);
        DrawTri(a, c, d, col);
    }

    private static void DrawTri(Vector2 p0, Vector2 p1, Vector2 p2, Color k)
    {
        var cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
        if (cross > 0)
        {
            (p1, p2) = (p2, p1);
        }

        Raylib.DrawTriangle(p0, p1, p2, k);
    }

    public static Color Darken(Color c, float k) => new((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k), c.A);

    public static Color Lighten(Color c, float k) =>
        new((byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k), c.A);
}
