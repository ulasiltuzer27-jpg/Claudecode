using Starfall.Core;

namespace Starfall.World;

/// <summary>
/// Tohumlu 2B simplex gurultu. JS surumuyle ayni permutasyon ve ayni
/// formuller (double): ayni tohum ayni adayi uretir.
/// </summary>
public sealed class Noise2D
{
    private static readonly int[,] Grad =
    {
        { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 },
        { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 },
    };

    private static readonly double F2 = 0.5 * (Math.Sqrt(3) - 1);
    private static readonly double G2 = (3 - Math.Sqrt(3)) / 6;
    private readonly byte[] _perm = new byte[512];

    public Noise2D(uint seed)
    {
        var rng = new Rng(seed);
        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;
        for (int i = 255; i > 0; i--)
        {
            int j = (int)Math.Floor(rng.NextD() * (i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    public double Get(double x, double y)
    {
        double s = (x + y) * F2;
        int i = (int)Math.Floor(x + s);
        int j = (int)Math.Floor(y + s);
        double t = (i + j) * G2;
        double x0 = x - (i - t);
        double y0 = y - (j - t);
        int i1 = x0 > y0 ? 1 : 0;
        int j1 = x0 > y0 ? 0 : 1;
        double x1 = x0 - i1 + G2;
        double y1 = y0 - j1 + G2;
        double x2 = x0 - 1 + 2 * G2;
        double y2 = y0 - 1 + 2 * G2;
        int ii = i & 255;
        int jj = j & 255;
        double n = 0;
        double t0 = 0.5 - x0 * x0 - y0 * y0;
        if (t0 > 0)
        {
            int g = _perm[ii + _perm[jj]] & 7;
            t0 *= t0;
            n += t0 * t0 * (Grad[g, 0] * x0 + Grad[g, 1] * y0);
        }
        double t1 = 0.5 - x1 * x1 - y1 * y1;
        if (t1 > 0)
        {
            int g = _perm[ii + i1 + _perm[jj + j1]] & 7;
            t1 *= t1;
            n += t1 * t1 * (Grad[g, 0] * x1 + Grad[g, 1] * y1);
        }
        double t2 = 0.5 - x2 * x2 - y2 * y2;
        if (t2 > 0)
        {
            int g = _perm[ii + 1 + _perm[jj + 1]] & 7;
            t2 *= t2;
            n += t2 * t2 * (Grad[g, 0] * x2 + Grad[g, 1] * y2);
        }
        return 70 * n;
    }

    public double Fbm(double x, double y, int octaves = 4, double lacunarity = 2, double gain = 0.5)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Get(x * freq, y * freq);
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return sum / norm;
    }
}

public static class NoiseUtil
{
    public static double Smoothstep(double e0, double e1, double x)
    {
        double t = Math.Min(1, Math.Max(0, (x - e0) / (e1 - e0)));
        return t * t * (3 - 2 * t);
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Clamp(double x, double lo, double hi) => x < lo ? lo : x > hi ? hi : x;

    public static uint HashString(string str)
    {
        uint h = 2166136261;
        foreach (char c in str)
        {
            h ^= c;
            h = unchecked(h * 16777619);
        }
        return h;
    }
}
