namespace PixelSurvival.World;

/// <summary>
/// Tohumlanmış (seeded) gradyan gürültüsü — Perlin türevi, fraktal katmanlı.
///
/// Neden hazır kütüphane değil?
/// Multiplayer'da (madde 10/19) host ve client AYNI dünyayı üretmek zorunda.
/// Bu yüzden gürültünün her makinede bit düzeyinde aynı sonucu vermesi gerekiyor.
/// Kendi PRNG'imiz ve permütasyon tablomuz olduğu için üretim tamamen
/// tohumdan türer; platform veya .NET sürümü değişse de sonuç değişmez.
///
/// Tüm aritmetik <c>double</c> ile yapılır. <c>float</c> kullanılsaydı
/// biome eşiklerinin kenarındaki tile'lar makineden makineye kayabilirdi.
/// </summary>
public sealed class Noise
{
    private readonly int[] _permutation;

    /// <summary>8 yönlü birim gradyanlar. Klasik Perlin'in 2B karşılığı.</summary>
    private static readonly double[][] Gradients =
    [
        [1, 1], [-1, 1], [1, -1], [-1, -1],
        [1, 0], [-1, 0], [0, 1], [0, -1]
    ];

    public Noise(int seed)
    {
        _permutation = BuildPermutation(seed);
    }

    /// <summary>
    /// xorshift32 — kasten basit ve taşınabilir bir PRNG.
    /// <c>System.Random</c> KULLANILMAZ: .NET sürümleri arasında algoritması
    /// değişti, aynı tohum farklı dünyalar üretebilirdi.
    /// </summary>
    private static uint NextRandom(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    private static int[] BuildPermutation(int seed)
    {
        // Tohum 0 xorshift'i sonsuza kadar 0'da kilitler — sabit bir değerle karıştır.
        var state = (uint)seed ^ 0x9E3779B9u;
        if (state == 0)
        {
            state = 0x6D2B79F5u;
        }

        var table = new int[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = i;
        }

        // Fisher-Yates karıştırma
        for (var i = 255; i > 0; i--)
        {
            var j = (int)(NextRandom(ref state) % (uint)(i + 1));
            (table[i], table[j]) = (table[j], table[i]);
        }

        // Tabloyu ikiye katla: örnekleme sırasında modulo almaya gerek kalmaz.
        var doubled = new int[512];
        for (var i = 0; i < 512; i++)
        {
            doubled[i] = table[i & 255];
        }

        return doubled;
    }

    /// <summary>Perlin'in 6t^5-15t^4+10t^3 yumuşatma eğrisi.</summary>
    private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private double DotGradient(int hash, double x, double y)
    {
        var g = Gradients[hash & 7];
        return g[0] * x + g[1] * y;
    }

    /// <summary>Tek oktav gürültü. Çıktı kabaca [-1, 1].</summary>
    public double Sample(double x, double y)
    {
        var xi = (int)Math.Floor(x) & 255;
        var yi = (int)Math.Floor(y) & 255;

        var xf = x - Math.Floor(x);
        var yf = y - Math.Floor(y);

        var u = Fade(xf);
        var v = Fade(yf);

        var aa = _permutation[_permutation[xi] + yi];
        var ab = _permutation[_permutation[xi] + yi + 1];
        var ba = _permutation[_permutation[xi + 1] + yi];
        var bb = _permutation[_permutation[xi + 1] + yi + 1];

        var x1 = Lerp(DotGradient(aa, xf, yf), DotGradient(ba, xf - 1.0, yf), u);
        var x2 = Lerp(DotGradient(ab, xf, yf - 1.0), DotGradient(bb, xf - 1.0, yf - 1.0), u);

        // 2B Perlin'in teorik tepe değeri ~0.707; 1'e ölçekle.
        return Lerp(x1, x2, v) * 1.4142135623730951;
    }

    /// <summary>
    /// Fraktal Brown hareketi: farklı frekanslarda oktavları toplar.
    /// Tek oktav fazla düzgün görünür; oktavlar kıyı ve tepe detayını verir.
    /// Çıktı [0, 1] aralığına normalize edilir.
    /// </summary>
    public double Fractal01(double x, double y, int octaves, double lacunarity, double persistence)
    {
        var sum = 0.0;
        var amplitude = 1.0;
        var frequency = 1.0;
        var totalAmplitude = 0.0;

        for (var i = 0; i < octaves; i++)
        {
            sum += Sample(x * frequency, y * frequency) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        var normalized = (sum / totalAmplitude + 1.0) * 0.5;
        return Math.Clamp(normalized, 0.0, 1.0);
    }

    /// <summary>
    /// Koordinata bağlı deterministik 0-1 değeri. Ağaç saçılımı gibi
    /// "her tile için bağımsız zar" gereken yerlerde kullanılır.
    /// Gürültüden farklı: komşu tile'larla korelasyonu yoktur.
    /// </summary>
    public static double HashToUnit(int x, int y, int seed)
    {
        var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return h / 4294967296.0;
    }
}
