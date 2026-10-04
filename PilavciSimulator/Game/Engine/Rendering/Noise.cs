namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Doku uretimi icin periyodik (dikissiz dosenen) gurultu fonksiyonlari.
/// Hepsi deterministik: ayni tohum her makinede ayni dokuyu verir.
/// </summary>
public static class Noise
{
    public static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static int Wrap(int v, int period) => ((v % period) + period) % period;

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    /// <summary>Periyodik deger gurultusu. x,y [0, period) araliginda kafes biriminde.</summary>
    public static float Value(float x, float y, int period, int seed)
    {
        var xi = (int)MathF.Floor(x);
        var yi = (int)MathF.Floor(y);
        var fx = Smooth(x - xi);
        var fy = Smooth(y - yi);
        var x0 = Wrap(xi, period);
        var y0 = Wrap(yi, period);
        var x1 = Wrap(xi + 1, period);
        var y1 = Wrap(yi + 1, period);
        var a = Hash(x0, y0, seed);
        var b = Hash(x1, y0, seed);
        var c = Hash(x0, y1, seed);
        var d = Hash(x1, y1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /// <summary>
    /// Fraktal gurultu. u,v [0,1) doku koordinati; basePeriod kafes
    /// sayisi. Her oktavda periyot ikiye katlanir, dikissizlik korunur.
    /// </summary>
    public static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float gain = 0.5f)
    {
        var sum = 0f;
        var amp = 0.5f;
        var norm = 0f;
        var period = basePeriod;
        for (var o = 0; o < octaves; o++)
        {
            sum += Value(u * period, v * period, period, seed + o * 101) * amp;
            norm += amp;
            amp *= gain;
            period *= 2;
        }

        return sum / norm;
    }

    /// <summary>
    /// Periyodik hucresel gurultu: en yakin iki noktaya uzaklik (F1, F2)
    /// ve en yakin hucrenin kimligi. Arnavut kaldirimi ve tas desenleri.
    /// </summary>
    public static (float F1, float F2, int Cell) Cellular(float u, float v, int period, int seed, float jitter = 0.85f)
    {
        var x = u * period;
        var y = v * period;
        var xi = (int)MathF.Floor(x);
        var yi = (int)MathF.Floor(y);
        var f1 = float.MaxValue;
        var f2 = float.MaxValue;
        var cell = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var cx = xi + dx;
                var cy = yi + dy;
                var wx = Wrap(cx, period);
                var wy = Wrap(cy, period);
                var px = cx + 0.5f + (Hash(wx, wy, seed) - 0.5f) * jitter;
                var py = cy + 0.5f + (Hash(wx, wy, seed + 17) - 0.5f) * jitter;
                var d = MathF.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    cell = wy * period + wx;
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }
        }

        return (f1, f2, cell);
    }
}
