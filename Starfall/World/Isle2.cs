using System.Numerics;
using Starfall.Core;
using static Starfall.World.NoiseUtil;

namespace Starfall.World;

/// <summary>
/// Kar Adasi yerlesimi (dunya koordinatlari). Ada merkezi Ada 1'in kuzeydogusunda; liman
/// guneybatiya (Ada 1'e) bakar, rasathane kuzeydeki ucurumlu zirvede.
/// </summary>
public static class L2
{
    public static readonly Vector2 C = new(640, -560);
    public const float Size = 448;
    public static Vector2 W(float u, float v) => new(C.X + u, C.Y + v);

    public static readonly Vector2 Harbor = W(-95, 95);
    public const float HarborR = 34, HarborH = 2.6f;
    public static readonly Vector2 Summit = W(30, -95);
    public const float SummitR = 20, SummitCliff = 15f;
    public static readonly Vector2 Lake = W(75, 45);
    public const float LakeR = 26, LakeLevel = 6f;
    public static readonly Vector2 Spring = W(-105, -40);
    public const float SpringR = 22, SpringH = 4.0f, SpringLevel = 3.45f;
    public static readonly Vector2[] Pools = { W(-112, -46), W(-98, -32), W(-99, -50) };
    public const float PoolR = 4.2f;
    public static readonly Vector2 Cave = W(118, -70);
    public static readonly Vector2 SledTop = W(12, -30), SledBottom = W(58, 22);
    public static readonly Vector2 SlopeMid = W(-30, 10);
    public static readonly Vector2 Training = W(-58, 58);
    public static readonly Vector2 DockStart = W(-104, 104), DockDir = Vector2.Normalize(new Vector2(-0.72f, 0.69f));
    public const float DockLength = 24;
}

/// <summary>Kar Adasi yukseklik fonksiyonu (JS adasiyla ayni yaklasim: gauss tepeler + platolar).</summary>
public sealed class Isle2Shape : IIslandShape
{
    public static readonly Isle2Shape Instance = new();

    public static readonly double[][][] Paths =
    {
        // liman -> yamac -> kizak tepesi
        new[] { P(-95, 95), P(-70, 70), P(-45, 40), P(-20, 5), P(5, -22) },
        // liman -> sicak su
        new[] { P(-95, 95), P(-112, 50), P(-110, 0), P(-105, -28) },
        // yamac -> rasathane ucurumu eteği
        new[] { P(-20, 5), P(-5, -40), P(12, -68) },
        // kizak tepesi -> donmus gol (kizak yolu)
        new[] { P(12, -30), P(30, -8), P(48, 10), P(58, 22) },
    };

    private static double[] P(double u, double v) => new[] { L2.C.X + u, L2.C.Y + v };

    public double DistToPaths(double x, double z) => Seg.DistToPaths(Paths, x, z);

    private static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);
    private static double Plateau(double d, double r, double w = 1.0) => 1 - Smoothstep(r - w, r + w, d);

    public double CoastRadius(Terrain t, double u, double v)
    {
        double a = Math.Atan2(v, u);
        double c = Math.Cos(a), s = Math.Sin(a);
        return 150 + 12 * t.N1.Get(c * 1.3 + 3, s * 1.3 + 9) + 6 * t.N2.Get(c * 3.3 + 1, s * 3.3);
    }

    public double RawHeight(Terrain t, double x, double z)
    {
        double u = x - L2.C.X, v = z - L2.C.Y;
        double r = Hyp(u, v);
        double R = CoastRadius(t, u, v);
        double land = Smoothstep(R + 24, R - 28, r);
        double h = Lerp(-9, 5, land);

        double dH = Hyp(x - L2.Harbor.X, z - L2.Harbor.Y);
        double harborCalm = 1 - Smoothstep(L2.HarborR - 12, L2.HarborR + 6, dH);
        double dS = Hyp(x - L2.Summit.X, z - L2.Summit.Y);
        double summitCalm = 1 - Smoothstep(L2.SummitR + 4, L2.SummitR + 26, dS);
        double dSp = Hyp(x - L2.Spring.X, z - L2.Spring.Y);
        double springCalm = 1 - Smoothstep(L2.SpringR - 6, L2.SpringR + 6, dSp);
        double calm = Math.Max(harborCalm, Math.Max(summitCalm, springCalm));

        double hills = t.N1.Fbm(x * 0.012, z * 0.012, 4) * 4.5 + t.N2.Fbm(x * 0.05, z * 0.05, 3) * 0.6;
        h += hills * land * land * (1 - calm);

        // dag: genis gauss + zirvede tek basamakli ucurum (tirmanma gerekir)
        h += 30 * Math.Exp(-Math.Pow(dS / 78, 2)) * land;
        double aS = Math.Atan2(z - L2.Summit.Y, x - L2.Summit.X);
        double wob = 1.6 * t.N3.Get(Math.Cos(aS) * 2 + 41, Math.Sin(aS) * 2 + 3);
        h += L2.SummitCliff * Plateau(dS, L2.SummitR + wob);
        // yamac: liman ile zirve arasinda yumusak rampa
        double dM = Hyp(x - L2.SlopeMid.X, z - L2.SlopeMid.Y);
        h += 7 * Math.Exp(-Math.Pow(dM / 60, 2)) * land * (1 - harborCalm);

        // liman: duz
        h = Lerp(h, L2.HarborH + t.N2.Fbm(x * 0.05, z * 0.05, 2) * 0.2, harborCalm * land);

        // sicak su: duz havza + uc kucuk havuz
        h = Lerp(h, L2.SpringH + t.N2.Fbm(x * 0.07, z * 0.07, 2) * 0.25, springCalm * land);
        foreach (var p in L2.Pools)
        {
            double dp = Hyp(x - p.X, z - p.Y);
            double inner = Smoothstep(L2.PoolR + 1.2, L2.PoolR - 1.5, dp);
            double bowl = L2.SpringLevel - 1.1 * (1 - Math.Pow(Clamp(dp / L2.PoolR, 0, 1), 2)) - 0.2;
            h = Lerp(h, Math.Min(h, bowl), inner);
        }

        // donmus gol: kase + kenar
        double dL = Hyp(x - L2.Lake.X, z - L2.Lake.Y);
        if (dL < L2.LakeR + 18)
        {
            double rim = L2.LakeLevel + 0.6 + Math.Max(0, dL - L2.LakeR) * 0.1;
            double bowl = L2.LakeLevel - 2.6 * (1 - Math.Pow(Clamp(dL / L2.LakeR, 0, 1), 2)) - 0.3;
            double inner = Smoothstep(L2.LakeR + 1.5, L2.LakeR - 2.5, dL);
            double rimBand = Smoothstep(L2.LakeR + 18, L2.LakeR + 4, dL);
            h = Lerp(h, Math.Max(h, rim), rimBand * (1 - inner));
            h = Lerp(h, bowl, inner);
            h += 2.6 * Math.Exp(-Math.Pow(Hyp(x - (L2.Lake.X + 6), z - (L2.Lake.Y - 4)) / 3.5, 2)); // golun ortasinda kaya adacik
        }

        // kizak yolu: duzgun, surekli egim (tepeden gole)
        double dT = Seg.Dist(x, z, L2.SledTop.X, L2.SledTop.Y, L2.SledBottom.X, L2.SledBottom.Y);
        if (dT < 12)
        {
            var ab = L2.SledBottom - L2.SledTop;
            double tt = Clamp(((x - L2.SledTop.X) * ab.X + (z - L2.SledTop.Y) * ab.Y) / ab.LengthSquared(), 0, 1);
            double top = 22, bottom = L2.LakeLevel + 0.9;
            double target = Lerp(top, bottom, Smoothstep(0, 1, tt));
            double k = Smoothstep(12, 5, dT) * land;
            h = Lerp(h, target, k);
        }

        // buz magarasi icin teras
        double dC = Hyp(x - L2.Cave.X, z - L2.Cave.Y);
        h = Lerp(h, 8.5, (1 - Smoothstep(8, 14, dC)) * land);
        return h;
    }
}

/// <summary>Kar Adasi boyasi: kar, mavi golgeli kaya, buz, gri cakil kumsal.</summary>
public sealed class Isle2Paint : IIslandPaint
{
    public static readonly Isle2Paint Instance = new();
    private static readonly Vector3 Snow = MathX.Hex("#f3f7fc"), SnowShade = MathX.Hex("#dfe9f5"), Pebble = MathX.Hex("#b9b6ae"),
        PebbleWet = MathX.Hex("#8f8c86"), Rock = MathX.Hex("#8d97a6"), RockDark = MathX.Hex("#6e7887"), PathC = MathX.Hex("#d6dbe2"),
        Deep = MathX.Hex("#5f6f80"), Grass = MathX.Hex("#8fb08a"), SpringC = MathX.Hex("#b7c8a8"), Ice = MathX.Hex("#cfe9f6");

    public Vector3 Color(Terrain t, float x, float z, float h, Vector3 n, float pd, float wl, out Vector4 mask)
    {
        float vary = (float)(t.N3.Get(x * 0.08, z * 0.08) * 0.5 + t.N3.Get(x * 0.3, z * 0.3) * 0.25);
        var c = Vector3.Lerp(Snow, SnowShade, MathX.Smoothstep(-0.4f, 0.7f, vary));
        float spring = Tint.Gauss(x, z, L2.Spring, 26);
        float harbor = Tint.Gauss(x, z, L2.Harbor, 30);
        // sicak su cevresinde kar erimis: cimen ve yosun
        c = Vector3.Lerp(c, SpringC, spring * 0.85f);
        c = Vector3.Lerp(c, Vector3.Lerp(c, Grass, 0.35f), harbor * 0.4f);
        float beach = 1 - MathX.Smoothstep(1.6f, 2.6f, h);
        c = Vector3.Lerp(c, Pebble, beach);
        if (h < wl) c = Vector3.Lerp(PebbleWet, Deep, MathX.Smoothstep(0, 6, wl - h));
        float rock = MathX.Smoothstep(0.8f, 0.64f, n.Y);
        c = Vector3.Lerp(c, Rock, rock);
        c = Vector3.Lerp(c, RockDark, rock * MathX.Smoothstep(0, 1, vary + 0.3f) * 0.5f);
        float path = (1 - MathX.Smoothstep(1.1f, 2.1f, pd)) * (h > wl + 0.3f ? 1 : 0) * (1 - rock);
        c = Vector3.Lerp(c, PathC, path * 0.8f);
        float dL = MathX.Hypot(x - L2.Lake.X, z - L2.Lake.Y);
        if (dL < L2.LakeR + 1) c = Vector3.Lerp(c, Ice, 0.6f);
        c *= 1 + vary * 0.05f;

        // cimen yalnizca sicak suyun cevresinde (kar erimis); geri kalan her yer temiz kar
        float dens = (1 - beach) * (1 - rock) * (pd > 2.4f ? 1 : 0) * (h > wl + 0.25f ? 1 : 0);
        dens *= MathF.Min(1, spring * 1.6f);
        mask = new Vector4(dens, spring * 0.6f, 0, MathF.Min(1, spring * 1.2f));
        return c;
    }
}
