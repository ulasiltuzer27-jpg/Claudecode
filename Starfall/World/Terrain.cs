using System.Numerics;
using Starfall.Core;
using static Starfall.World.NoiseUtil;

namespace Starfall.World;

/// <summary>Tek bir adanin yukseklik izgarasi (2 m hucre). Sorgular izgaradan enterpole edilir.</summary>
public sealed class Terrain
{
    public readonly string Id;
    public readonly float CenterX, CenterZ, Size, Cell;
    public readonly int Cells, Verts;
    public readonly float[] Heights;
    public readonly float[] PathDist;
    public readonly Noise2D N1, N2, N3;
    public readonly IIslandShape Shape;

    public float MinX => CenterX - Size / 2;
    public float MinZ => CenterZ - Size / 2;

    public Terrain(string id, IIslandShape shape, uint seed, float cx, float cz, float size, int cells)
    {
        Id = id;
        Shape = shape;
        CenterX = cx; CenterZ = cz; Size = size; Cells = cells;
        Cell = size / cells;
        Verts = cells + 1;
        N1 = new Noise2D(seed);
        N2 = new Noise2D(seed + 101);
        N3 = new Noise2D(seed + 202);
        Heights = new float[Verts * Verts];
        PathDist = new float[Verts * Verts];
        Parallel.For(0, Verts, iz =>
        {
            for (int ix = 0; ix < Verts; ix++)
            {
                double x = MinX + ix * Cell;
                double z = MinZ + iz * Cell;
                Heights[iz * Verts + ix] = (float)shape.RawHeight(this, x, z);
                PathDist[iz * Verts + ix] = (float)shape.DistToPaths(x, z);
            }
        });
    }

    public bool Contains(float x, float z) =>
        x >= MinX && z >= MinZ && x < MinX + Size && z < MinZ + Size;

    /// <summary>Izgara ustunde bilinear enterpolasyon (JS surumuyle ayni).</summary>
    public float Height(float x, float z)
    {
        float fx = (x - MinX) / Cell;
        float fz = (z - MinZ) / Cell;
        if (fx < 0 || fz < 0 || fx >= Cells || fz >= Cells) return -9f;
        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        float h00 = Heights[iz * Verts + ix];
        float h10 = Heights[iz * Verts + ix + 1];
        float h01 = Heights[(iz + 1) * Verts + ix];
        float h11 = Heights[(iz + 1) * Verts + ix + 1];
        float a = h00 + (h10 - h00) * tx;
        float b = h01 + (h11 - h01) * tx;
        return a + (b - a) * tz;
    }

    /// <summary>Ucgen hassasiyetinde yukseklik (fizik icin: mesh'in cizdigi yuzeyin ayni).</summary>
    public float HeightTri(float x, float z)
    {
        float fx = (x - MinX) / Cell;
        float fz = (z - MinZ) / Cell;
        if (fx < 0 || fz < 0 || fx >= Cells || fz >= Cells) return -9f;
        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        float h00 = Heights[iz * Verts + ix];
        float h10 = Heights[iz * Verts + ix + 1];
        float h01 = Heights[(iz + 1) * Verts + ix];
        float h11 = Heights[(iz + 1) * Verts + ix + 1];
        // mesh ucgenleri: (a, d, b) ve (b, d, e) -> kosegen b(1,0)-d(0,1)
        if (tx + tz <= 1) return h00 + (h10 - h00) * tx + (h01 - h00) * tz;
        return h11 + (h01 - h11) * (1 - tx) + (h10 - h11) * (1 - tz);
    }

    public Vector3 Normal(float x, float z)
    {
        const float e = 1f;
        float hl = Height(x - e, z), hr = Height(x + e, z);
        float hd = Height(x, z - e), hu = Height(x, z + e);
        return Vector3.Normalize(new Vector3(hl - hr, 2 * e, hd - hu));
    }

    public float PathDistance(float x, float z)
    {
        int fx = (int)MathX.Clamp(MathF.Round((x - MinX) / Cell), 0, Cells);
        int fz = (int)MathX.Clamp(MathF.Round((z - MinZ) / Cell), 0, Cells);
        return PathDist[fz * Verts + fx];
    }

    public float VertexX(int ix) => MinX + ix * Cell;
    public float VertexZ(int iz) => MinZ + iz * Cell;
}

public interface IIslandShape
{
    double RawHeight(Terrain t, double x, double z);
    double DistToPaths(double x, double z);
}

internal static class Seg
{
    public static double Dist(double px, double pz, double ax, double az, double bx, double bz)
    {
        double dx = bx - ax, dz = bz - az;
        double len2 = dx * dx + dz * dz;
        double t = len2 > 0 ? ((px - ax) * dx + (pz - az) * dz) / len2 : 0;
        t = Clamp(t, 0, 1);
        double cx = ax + dx * t - px, cz = az + dz * t - pz;
        return Math.Sqrt(cx * cx + cz * cz);
    }

    public static double DistToPaths(double[][][] paths, double x, double z)
    {
        double best = double.MaxValue;
        foreach (var path in paths)
            for (int i = 0; i < path.Length - 1; i++)
                best = Math.Min(best, Dist(x, z, path[i][0], path[i][1], path[i + 1][0], path[i + 1][1]));
        return best;
    }
}

/// <summary>
/// Ada 1 (Yildiz Adasi): JS surumundeki Terrain.rawHeight'in birebir karsiligi.
/// Bolgeler, gorevler ve yerlesim bu koordinatlara bagli.
/// </summary>
public sealed class Isle1Shape : IIslandShape
{
    public static readonly Isle1Shape Instance = new();

    public static class L
    {
        public static readonly Vector2 Peak = new(6, -112);
        public const float PeakMesa1 = 31, PeakMesa2 = 17, PeakCliff1 = 2.7f, PeakCliff2 = 6.8f;
        public static readonly Vector2 Windy = new(104, -58);
        public const float WindyR = 32, WindyCliff = 4.0f;
        public static readonly Vector2 Hollow = new(-86, -80);
        public const float HollowR = 40;
        public static readonly Vector2 Lake = new(30, 2);
        public const float LakeR = 23, LakeLevel = 5.2f;
        public static readonly Vector2 Village = new(8, 108);
        public const float VillageR = 40, VillageH = 3.0f;
        public static readonly Vector2 Meadow = new(-58, 50);
        public static readonly Vector2 Forest = new(-120, -6);
        public static readonly Vector2 Cove = new(136, 50);
        public static readonly double[] Inlet = { 88, 164, 86, 84, 7.5 }; // ax, az, bx, bz, w
        public static readonly Vector2 Islet = new(-182, -108);
        public const float IsletR = 16;
    }

    public static readonly double[][][] Paths =
    {
        new[] { new double[] { 8, 100 }, new double[] { -14, 86 }, new double[] { -40, 62 }, new double[] { -62, 46 }, new double[] { -92, 22 }, new double[] { -116, -4 }, new double[] { -110, -40 }, new double[] { -92, -66 } },
        new[] { new double[] { 14, 96 }, new double[] { 22, 70 }, new double[] { 26, 40 }, new double[] { 40, 24 } },
        new[] { new double[] { 30, -22 }, new double[] { 24, -46 }, new double[] { 14, -70 }, new double[] { 8, -84 } },
        new[] { new double[] { 50, -10 }, new double[] { 74, -30 }, new double[] { 90, -42 } },
        new[] { new double[] { 24, 108 }, new double[] { 52, 108 }, new double[] { 80, 108 } },
        new[] { new double[] { 104, 108 }, new double[] { 120, 86 }, new double[] { 132, 62 } },
    };

    public double DistToPaths(double x, double z) => Seg.DistToPaths(Paths, x, z);

    private static double Plateau(double d, double r, double w = 1.1) => 1 - Smoothstep(r - w, r + w, d);

    public double CoastRadius(Terrain t, double x, double z)
    {
        double a = Math.Atan2(z, x);
        double c = Math.Cos(a), s = Math.Sin(a);
        return 160 + 14 * t.N1.Get(c * 1.4 + 7, s * 1.4 + 3) + 7 * t.N2.Get(c * 3.1, s * 3.1);
    }

    public double RawHeight(Terrain t, double x, double z)
    {
        double r = Math.Sqrt(x * x + z * z);
        double R = CoastRadius(t, x, z);
        double land = Smoothstep(R + 26, R - 30, r);
        double h = Lerp(-9, 5.5, land);

        double dP = Hyp(x - L.Peak.X, z - L.Peak.Y);
        double aP = Math.Atan2(z - L.Peak.Y, x - L.Peak.X);
        double peakCalm = 1 - Smoothstep(18, 46, dP);
        double dW = Hyp(x - L.Windy.X, z - L.Windy.Y);
        double windyCalm = 1 - Smoothstep(L.WindyR - 6, L.WindyR + 10, dW);
        double dV = Hyp(x - L.Village.X, z - L.Village.Y);
        double villageCalm = 1 - Smoothstep(L.VillageR - 14, L.VillageR + 6, dV);

        double calm = Math.Max(peakCalm, Math.Max(windyCalm, villageCalm));
        double hills = t.N1.Fbm(x * 0.011, z * 0.011, 4) * 5.5 + t.N2.Fbm(x * 0.045, z * 0.045, 3) * 0.7;
        h += hills * land * land * (1 - calm);

        double wobble1 = 2.2 * t.N3.Get(Math.Cos(aP) * 2 + 11, Math.Sin(aP) * 2 + 5);
        double wobble2 = 1.4 * t.N3.Get(Math.Cos(aP) * 2.5 + 31, Math.Sin(aP) * 2.5 + 17);
        h += 24 * Math.Exp(-Math.Pow(dP / 64, 2)) * land;
        h += L.PeakCliff1 * Plateau(dP, L.PeakMesa1 + wobble1);
        h += L.PeakCliff2 * Plateau(dP, L.PeakMesa2 + wobble2);

        double aW = Math.Atan2(z - L.Windy.Y, x - L.Windy.X);
        double wobbleW = 2.5 * t.N3.Get(Math.Cos(aW) * 2 + 51, Math.Sin(aW) * 2 + 9);
        h += 8 * Math.Exp(-Math.Pow(dW / 52, 2)) * land;
        h += L.WindyCliff * Plateau(dW, L.WindyR + wobbleW);

        double dH = Hyp(x - L.Hollow.X, z - L.Hollow.Y);
        h -= 3.6 * Math.Exp(-Math.Pow(dH / L.HollowR, 2)) * land;

        h = Lerp(h, L.VillageH + t.N2.Fbm(x * 0.05, z * 0.05, 2) * 0.25, villageCalm * land);

        double dL = Hyp(x - L.Lake.X, z - L.Lake.Y);
        if (dL < L.LakeR + 16)
        {
            double rim = L.LakeLevel + 0.55 + Math.Max(0, dL - L.LakeR) * 0.08;
            double bowl = L.LakeLevel - 2.8 * (1 - Math.Pow(Clamp(dL / L.LakeR, 0, 1), 2)) - 0.25;
            double inner = Smoothstep(L.LakeR + 1.5, L.LakeR - 2.5, dL);
            double rimBand = Smoothstep(L.LakeR + 16, L.LakeR + 4, dL);
            h = Lerp(h, Math.Max(h, rim), rimBand * (1 - inner));
            h = Lerp(h, bowl, inner);
            h += 3.4 * Math.Exp(-Math.Pow(dL / 4.5, 2));
        }

        var I = L.Inlet;
        double dI = Seg.Dist(x, z, I[0], I[1], I[2], I[3]);
        double inletT = Smoothstep(I[4] + 7, I[4] - 2, dI) * Smoothstep(I[3] - 6, I[3] + 10, z);
        h = Math.Min(h, Lerp(h, -3.5, inletT));

        double dS = Hyp(x - L.Islet.X, z - L.Islet.Y);
        h = Math.Max(h, -9 + 15.5 * Math.Exp(-Math.Pow(dS / L.IsletR, 2)));
        return h;
    }

    private static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);
}
