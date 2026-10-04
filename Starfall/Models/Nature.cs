using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>
/// Doga modelleri: agaclar, kayalar, calilar, cicekler, mantarlar. Hepsi tek geometri
/// dondurur; sahnede InstanceBatch ile yuzlerce kez cizilir (tek draw call).
/// </summary>
public static class Nature
{
    private static Geo CanopyBall(string color, float r, Vector3 pos, uint seed, int detail = 1)
    {
        var g = Icosahedron(r, detail).Color(color);
        g.Jitter(r * 0.28f, seed);
        return g.Translate(pos.X, pos.Y, pos.Z);
    }

    private static Geo Trunk(string color, float rTop, float rBottom, float h, float lean = 0)
    {
        var g = Part(CylinderGeo(rTop, rBottom, h, 7, 2), color, V(0, h / 2, 0), V(0, 0, lean));
        return g.GradientY(P.BarkDark, color, 0, h * 0.6f);
    }

    public static Geo RoundTree(uint seed = 1, bool blossom = false)
    {
        string c1 = blossom ? P.Blossom : P.Canopy;
        string c2 = blossom ? P.BlossomLight : P.CanopyLight;
        var g = Merge(
            Trunk(P.Bark, 0.18f, 0.3f, 2.6f),
            Part(CylinderGeo(0.06f, 0.1f, 1.1f, 5), P.Bark, V(0.45f, 2.2f, 0), V(0, 0, -0.9f)),
            CanopyBall(c1, 1.5f, V(0, 3.4f, 0), seed),
            CanopyBall(c2, 1.05f, V(0.9f, 3.0f, 0.4f), seed + 1),
            CanopyBall(c1, 1.1f, V(-0.8f, 3.1f, -0.4f), seed + 2),
            CanopyBall(c2, 0.95f, V(0.1f, 4.3f, 0.2f), seed + 3));
        return g.FaceTint(0.12f, seed);
    }

    public static Geo PineTree(uint seed = 1, bool snowy = false)
    {
        var parts = new List<Geo> { Trunk(P.BarkDark, 0.12f, 0.25f, 1.6f) };
        for (int i = 0; i < 4; i++)
        {
            float r = 1.55f - i * 0.32f;
            float h = 1.5f - i * 0.12f;
            float y = 1.2f + i * 0.95f;
            var c = Part(CylinderGeo(0, r, h, 8, 1), i % 2 == 1 ? P.PineLight : P.Pine, V(0, y + h / 2, 0), V(0, i * 0.4f, 0));
            c.Jitter(0.18f, seed + (uint)i);
            if (snowy)
            {
                // kar ortusu: yukari bakan yuzlerin ust yarisi beyaz
                var snow = MathX.Hex("#f4f8ff");
                for (int k = 0; k + 2 < c.Count; k += 3)
                {
                    float ny = c.N[k].Y;
                    float yy = (c.P[k].Y + c.P[k + 1].Y + c.P[k + 2].Y) / 3f;
                    if (ny > 0.35f && yy > y + h * 0.18f)
                        c.C[k] = c.C[k + 1] = c.C[k + 2] = snow;
                }
            }
            parts.Add(c);
        }
        return Merge(parts).FaceTint(0.12f, seed);
    }

    public static Geo BirchTree(uint seed = 1)
    {
        var t = Part(CylinderGeo(0.11f, 0.17f, 3.4f, 7, 4), P.Birch, V(0, 1.7f, 0));
        // kayin govdesindeki koyu lekeler
        var rng = new Rng(seed);
        var marks = new double[6];
        for (int i = 0; i < 6; i++) marks[i] = rng.NextD() * 3.2;
        var dark = new Vector3(0.2f, 0.18f, 0.17f);
        for (int i = 0; i + 2 < t.Count; i += 3)
        {
            float y = (t.P[i].Y + t.P[i + 1].Y + t.P[i + 2].Y) / 3;
            bool near = false;
            foreach (var m in marks) if (Math.Abs(m - y) < 0.07) { near = true; break; }
            if (near && rng.NextD() > 0.3)
                t.C[i] = t.C[i + 1] = t.C[i + 2] = dark;
        }
        var g = Merge(
            t,
            CanopyBall("#a6d65a", 1.0f, V(0, 3.7f, 0), seed),
            CanopyBall("#c3e36d", 0.8f, V(0.6f, 3.2f, 0.2f), seed + 1),
            CanopyBall("#a6d65a", 0.75f, V(-0.55f, 3.3f, -0.2f), seed + 2),
            CanopyBall("#c3e36d", 0.65f, V(0, 4.4f, 0.3f), seed + 3));
        return g.FaceTint(0.1f, seed);
    }

    public static Geo PalmTree(uint seed = 1)
    {
        var parts = new List<Geo>();
        float x = 0, y = 0;
        for (int i = 0; i < 7; i++)
        {
            const float h = 0.6f;
            parts.Add(Part(CylinderGeo(0.15f - i * 0.008f, 0.18f - i * 0.008f, h, 7), i % 2 == 1 ? "#b08a5a" : "#9c774a",
                V(x, y + h / 2, 0), V(0, 0, -0.06f * i)));
            x += MathF.Sin(0.06f * i) * h;
            y += MathF.Cos(0.06f * i) * h * 0.98f;
        }
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * MathX.TwoPi;
            parts.Add(Part(CylinderGeo(0, 0.32f, 2.1f, 4, 1), k % 2 == 1 ? P.Palm : "#3e9a48",
                V(x + MathF.Cos(a) * 0.8f, y - 0.05f, MathF.Sin(a) * 0.8f),
                V(0, -a, MathX.Pi / 2 + 0.35f), V(1, 1, 0.25f)));
        }
        parts.Add(Sphere("#7a5a3a", 0.13f, V(x + 0.12f, y - 0.18f, 0.08f), null, 6, 4));
        parts.Add(Sphere("#7a5a3a", 0.13f, V(x - 0.1f, y - 0.2f, -0.1f), null, 6, 4));
        return Merge(parts).FaceTint(0.1f, seed);
    }

    public static Geo Rock(uint seed = 1, bool mossy = true, string? baseColor = null, string? topColor = null)
    {
        var g = Dodecahedron(1, 1).Color(P.Rock);
        g.Jitter(0.45f, seed);
        g.Scale(1, 0.72f, 1).FlatNormals();
        var moss = MathX.Hex(topColor ?? P.GrassMoss);
        var bas = MathX.Hex(baseColor ?? P.Rock);
        var dark = MathX.Hex(P.RockDark);
        for (int i = 0; i + 2 < g.Count; i += 3)
        {
            float ny = g.N[i].Y;
            float y = (g.P[i].Y + g.P[i + 1].Y + g.P[i + 2].Y) / 3;
            var c = (mossy && ny > 0.75f && y > 0.2f) ? moss : Vector3.Lerp(bas, dark, MathF.Max(0, -y * 0.8f));
            g.C[i] = g.C[i + 1] = g.C[i + 2] = c;
        }
        g.Translate(0, 0.35f, 0);
        return g.FaceTint(0.14f, seed);
    }

    /// <summary>Kar Adasi kayasi: ustu karli, gri-mavi.</summary>
    public static Geo SnowRock(uint seed = 1) => Rock(seed, true, "#8d97a6", "#f2f6fc");

    public static Geo Bush(uint seed = 1, bool snowy = false)
    {
        var g = Merge(
            CanopyBall(snowy ? "#3f7f58" : "#4fa64a", 0.6f, V(0, 0.45f, 0), seed),
            CanopyBall(snowy ? "#4c8c62" : "#62b856", 0.45f, V(0.45f, 0.35f, 0.1f), seed + 1),
            CanopyBall(snowy ? "#3f7f58" : "#4fa64a", 0.42f, V(-0.4f, 0.33f, -0.15f), seed + 2));
        if (snowy)
        {
            var snow = MathX.Hex("#f4f8ff");
            for (int i = 0; i + 2 < g.Count; i += 3)
                if (g.N[i].Y > 0.55f) g.C[i] = g.C[i + 1] = g.C[i + 2] = snow;
        }
        return g.FaceTint(0.15f, seed);
    }

    /// <summary>Petal rengi ornek rengiyle verilir; petaller beyaz birakilir.</summary>
    public static Geo Flower()
    {
        var parts = new List<Geo> { Cyl("#4f9e3a", 0.012f, 0.012f, 0.32f, V(0, 0.16f, 0), null, 4) };
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * MathX.TwoPi;
            parts.Add(Sphere("#ffffff", 0.05f, V(MathF.Cos(a) * 0.055f, 0.33f, MathF.Sin(a) * 0.055f), V(1, 0.35f, 1), 5, 3));
        }
        parts.Add(Sphere("#ffd84a", 0.035f, V(0, 0.345f, 0), null, 5, 3));
        parts.Add(Part(CylinderGeo(0, 0.04f, 0.12f, 3), "#5fb04a", V(0.04f, 0.1f, 0), V(0, 0, -0.9f), V(1, 1, 0.3f)));
        return Merge(parts);
    }

    public static Geo Mushroom(string? color = null, uint seed = 1)
    {
        var cap = Part(SphereGeo(0.22f, 10, 6, 0, MathX.TwoPi, 0, MathX.Pi / 2), color ?? P.MushroomRed, V(0, 0.22f, 0), null, V(1, 0.75f, 1));
        var parts = new List<Geo> { Cyl(P.MushroomStem, 0.07f, 0.09f, 0.24f, V(0, 0.12f, 0), null, 7), cap };
        var rng = new Rng(seed);
        for (int i = 0; i < 5; i++)
        {
            float a = (float)(rng.NextD() * Math.PI * 2);
            float r = 0.08f + (float)rng.NextD() * 0.08f;
            float yy = 0.22f + 0.16f * MathF.Sqrt(1 - (r / 0.22f) * (r / 0.22f)) * 0.75f;
            parts.Add(Sphere("#fffaf0", 0.025f, V(MathF.Cos(a) * r, yy, MathF.Sin(a) * r), V(1, 0.4f, 1), 5, 3));
        }
        return Merge(parts);
    }

    /// <summary>Ziplatan dev mantar (Mantar Vadisi): sapka ustu "boing".</summary>
    public static Geo GiantMushroom(string color, float capR, float height, uint seed = 1)
    {
        var stem = Part(CylinderGeo(capR * 0.22f, capR * 0.3f, height, 10, 3), P.MushroomStem, V(0, height / 2, 0));
        stem.Jitter(0.08f, seed);
        var cap = Part(SphereGeo(capR, 16, 8, 0, MathX.TwoPi, 0, MathX.Pi / 2), color, V(0, height - 0.1f, 0), null, V(1, 0.5f, 1));
        var under = Part(CircleGeo(capR * 0.98f, 16), "#f3e3c8", V(0, height - 0.09f, 0), V(MathX.Pi / 2, 0, 0));
        var parts = new List<Geo> { stem, cap, under };
        var rng = new Rng(seed);
        for (int i = 0; i < 9; i++)
        {
            float a = (float)(rng.NextD() * Math.PI * 2);
            float rr = capR * (0.25f + (float)rng.NextD() * 0.6f);
            float yy = height - 0.1f + capR * 0.5f * MathF.Sqrt(MathF.Max(0, 1 - (rr / capR) * (rr / capR)));
            parts.Add(Sphere("#fffaf0", capR * 0.11f, V(MathF.Cos(a) * rr, yy, MathF.Sin(a) * rr), V(1, 0.35f, 1), 6, 3));
        }
        return Merge(parts).FaceTint(0.06f, seed);
    }

    public static Geo Cloud(uint seed = 1)
    {
        var rng = new Rng(seed);
        var parts = new List<Geo>();
        int n = 4 + (int)Math.Floor(rng.NextD() * 4);
        for (int i = 0; i < n; i++)
        {
            float r = 3 + (float)rng.NextD() * 4;
            var g = Icosahedron(r, 1).Color("#ffffff");
            g.Jitter(r * 0.2f, seed * 10 + (uint)i);
            g.Scale(1, 0.6f, 1);
            float tx = (i - n / 2f) * 4.5f + (float)rng.NextD() * 2;
            float ty = (float)rng.NextD() * 2;
            float tz = ((float)rng.NextD() - 0.5f) * 6;
            g.Translate(tx, ty, tz);
            parts.Add(g);
        }
        return Merge(parts);
    }

    public static Geo Log(float len = 1.6f) => Merge(
        Cyl(P.Bark, 0.16f, 0.16f, len, null, V(0, 0, MathX.Pi / 2), 7),
        Cyl("#d9b98a", 0.13f, 0.13f, 0.01f, V(len / 2 + 0.005f, 0, 0), V(0, 0, MathX.Pi / 2), 7),
        Cyl("#d9b98a", 0.13f, 0.13f, 0.01f, V(-len / 2 - 0.005f, 0, 0), V(0, 0, MathX.Pi / 2), 7));

    public static Geo Crystal(uint seed = 1, string? c1 = null, string? c2 = null)
    {
        var rng = new Rng(seed);
        var parts = new List<Geo>();
        for (int i = 0; i < 5; i++)
        {
            float h = 0.6f + (float)rng.NextD() * 1.2f;
            float px = ((float)rng.NextD() - 0.5f) * 0.8f;
            float pz = ((float)rng.NextD() - 0.5f) * 0.8f;
            float rx = ((float)rng.NextD() - 0.5f) * 0.6f;
            float ry = (float)rng.NextD() * 3;
            float rz = ((float)rng.NextD() - 0.5f) * 0.6f;
            parts.Add(Part(Octahedron(0.25f), i % 2 == 1 ? (c1 ?? P.Crystal) : (c2 ?? "#b9a6ff"),
                V(px, h * 0.45f, pz), V(rx, ry, rz), V(1, h * 2, 1)));
        }
        return Merge(parts);
    }

    /// <summary>Buz kutlesi (Kar Adasi): yari saydam gorunumlu acik mavi kristal kaya.</summary>
    public static Geo IceChunk(uint seed = 1)
    {
        var g = Icosahedron(1, 0).Color("#bfe9ff");
        g.Jitter(0.35f, seed);
        g.Scale(1, 0.8f, 1).FlatNormals();
        var top = MathX.Hex("#f2fbff");
        var side = MathX.Hex("#8fd0f0");
        for (int i = 0; i + 2 < g.Count; i += 3)
        {
            var c = g.N[i].Y > 0.6f ? top : Vector3.Lerp(MathX.Hex("#bfe9ff"), side, MathF.Max(0, -g.N[i].Y));
            g.C[i] = g.C[i + 1] = g.C[i + 2] = c;
        }
        g.Translate(0, 0.3f, 0);
        return g.FaceTint(0.1f, seed);
    }
}
