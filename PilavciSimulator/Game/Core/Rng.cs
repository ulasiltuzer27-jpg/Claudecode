namespace PilavciSimulator.Core;

/// <summary>
/// Kucuk, hizli, tohumlanabilir rastgele sayi ureteci (xorshift32).
///
/// <see cref="System.Random"/> yerine kendi uretecimiz: durumu tek bir
/// <c>uint</c>, kayda yazilip geri okunabiliyor ve .NET surumleri arasinda
/// ayni diziyi uretmesi garanti.
/// </summary>
public struct Rng
{
    public uint State;

    public Rng(int seed) => State = seed == 0 ? 0x9E3779B9u : (uint)seed;
    public Rng(uint seed) => State = seed == 0 ? 0x9E3779B9u : seed;

    public uint NextUInt()
    {
        var x = State;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        State = x;
        return x;
    }

    /// <summary>[0, 1)</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>[min, max)</summary>
    public int Range(int min, int max) => max <= min ? min : min + (int)(NextUInt() % (uint)(max - min));

    public bool Chance(float p) => NextFloat() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

    /// <summary>Konumdan/kimlikten turetilen kararli bir karma (gorsel cesitlilik icin).</summary>
    public static uint Hash(uint a, uint b = 0)
    {
        var h = a * 0x27d4eb2du ^ (b + 0x165667b1u);
        h ^= h >> 15;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        h *= 0xc2b2ae35u;
        h ^= h >> 16;
        return h;
    }
}
