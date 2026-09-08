using Microsoft.Xna.Framework;

namespace PixelSurvival.World;

/// <summary>
/// Tohumdan tile üreten saf fonksiyon topluluğu.
///
/// KONUM TABANLI, chunk tabanlı DEĞİL. <see cref="GetTileIndex"/> yalnızca
/// dünya koordinatına ve tohuma bakar; hangi chunk'ın parçası olduğunu bilmez.
/// Bunun sonucu: chunk sınırlarında dikiş OLUŞMAZ ve chunk'lar herhangi bir
/// sırada, herhangi bir sayıda üretilebilir — sonuç hep aynıdır.
///
/// Bu özellik multiplayer için de kritik: host ve client aynı tohumla aynı
/// dünyayı üretir, harita verisi ağdan gönderilmek zorunda kalmaz.
/// </summary>
public sealed class WorldGenerator : ITileGenerator
{
    private readonly BiomeTable _table;
    private readonly Tileset _tileset;
    private readonly Noise _elevationNoise;
    private readonly Noise _moistureNoise;
    private readonly Dictionary<string, int> _tileIndexCache = [];

    public int Seed { get; }

    /// <summary>Sonsuz dünya — sınır yok.</summary>
    public Rectangle? Bounds => null;

    public WorldGenerator(BiomeTable table, Tileset tileset, int seed)
    {
        _table = table;
        _tileset = tileset;
        Seed = seed;

        _elevationNoise = new Noise(seed);
        _moistureNoise = new Noise(seed + table.Noise.MoistureSeedOffset);

        // Tablodaki her tile anahtarı tileset'te var mı — açılışta doğrula.
        _tileset.RequireTiles(table.AllTileKeys.ToArray());

        foreach (var key in table.AllTileKeys)
        {
            _tileIndexCache[key] = _tileset.IndexOf(key);
        }
    }

    public double Elevation(int tileX, int tileY) =>
        _elevationNoise.Fractal01(
            tileX * _table.Noise.ElevationScale,
            tileY * _table.Noise.ElevationScale,
            _table.Noise.Octaves, _table.Noise.Lacunarity, _table.Noise.Persistence);

    public double Moisture(int tileX, int tileY) =>
        _moistureNoise.Fractal01(
            tileX * _table.Noise.MoistureScale,
            tileY * _table.Noise.MoistureScale,
            _table.Noise.Octaves, _table.Noise.Lacunarity, _table.Noise.Persistence);

    /// <summary>Dünya koordinatındaki tile'ın tileset indeksi.</summary>
    public int GetTileIndex(int tileX, int tileY)
    {
        var elevation = Elevation(tileX, tileY);
        var moisture = Moisture(tileX, tileY);

        var rule = _table.Resolve(elevation, moisture);
        var tileKey = rule.Tile;

        // Saçılım: zemin seçildikten sonra tekil engeller serpiştirilir.
        foreach (var scatter in _table.Scatter)
        {
            if (scatter.OnBiome != rule.Biome)
            {
                continue;
            }

            if (scatter.MoistureAbove is not null && moisture < scatter.MoistureAbove)
            {
                continue;
            }

            // Gürültü değil hash: komşu tile'larla korelasyon olmasın, ağaçlar
            // blok blok değil tek tek dağılsın.
            if (Noise.HashToUnit(tileX, tileY, Seed) < scatter.Chance)
            {
                tileKey = scatter.Tile;
                break;
            }
        }

        return _tileIndexCache[tileKey];
    }

    public bool IsSolid(int tileX, int tileY) => _tileset.IsSolid(GetTileIndex(tileX, tileY));

    /// <summary>
    /// Oyuncu için güvenli bir başlangıç tile'ı bulur.
    ///
    /// Sadece "katı değil" yetmez: prosedürel dünyada karakter denizin
    /// ortasındaki 3 tile'lık bir adaya veya kayaların arasındaki cebe
    /// doğabilir. Bu yüzden bulunan noktadan flood fill yapılıp bağlı açık
    /// alanın yeterince büyük olduğu doğrulanır.
    /// </summary>
    public Point FindSpawnTile()
    {
        var settings = _table.Spawn;

        // Merkezden dışa doğru kare halkalar halinde tara.
        for (var radius = 0; radius <= settings.SearchRadius; radius++)
        {
            foreach (var candidate in RingTiles(radius))
            {
                if (IsSolid(candidate.X, candidate.Y))
                {
                    continue;
                }

                if (CountConnectedOpenTiles(candidate, settings.MinimumOpenArea)
                    >= settings.MinimumOpenArea)
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException(
            $"Tohum {Seed}: {settings.SearchRadius} tile yarıçapında " +
            $"{settings.MinimumOpenArea} tile'lık açık alan bulunamadı. " +
            $"biomes.json eşikleri dünyayı fazla kapalı yapıyor olabilir.");
    }

    /// <summary>Merkezden <paramref name="radius"/> uzaklıktaki kare halkanın tile'ları.</summary>
    private static IEnumerable<Point> RingTiles(int radius)
    {
        if (radius == 0)
        {
            yield return Point.Zero;
            yield break;
        }

        for (var x = -radius; x <= radius; x++)
        {
            yield return new Point(x, -radius);
            yield return new Point(x, radius);
        }

        for (var y = -radius + 1; y <= radius - 1; y++)
        {
            yield return new Point(-radius, y);
            yield return new Point(radius, y);
        }
    }

    /// <summary>
    /// Başlangıç noktasına bağlı açık tile sayısı.
    /// <paramref name="limit"/>'e ulaşınca durur — sonsuz kıtayı baştan sona
    /// taramanın anlamı yok, "yeterince büyük mü" sorusuna cevap arıyoruz.
    /// </summary>
    private int CountConnectedOpenTiles(Point start, int limit)
    {
        var visited = new HashSet<Point> { start };
        var queue = new Queue<Point>();
        queue.Enqueue(start);
        var count = 0;

        while (queue.Count > 0 && count < limit)
        {
            var current = queue.Dequeue();
            count++;

            foreach (var offset in Neighbours)
            {
                var next = new Point(current.X + offset.X, current.Y + offset.Y);
                if (!visited.Add(next))
                {
                    continue;
                }

                if (!IsSolid(next.X, next.Y))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return count;
    }

    private static readonly Point[] Neighbours =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    ];

    /// <summary>
    /// Bir chunk'ın tile indekslerinden FNV-1a sağlaması.
    ///
    /// Amacı: üretimin beklenen algoritmayla eşleştiğini dışarıdan
    /// doğrulayabilmek. <c>Tools/verify_worldgen.py</c> aynı sağlamayı
    /// bağımsız olarak hesaplar; iki değer tutuyorsa C# implementasyonu
    /// doğrulanmış algoritmayla aynı sonucu veriyor demektir.
    /// </summary>
    public uint ComputeChunkChecksum(int chunkX, int chunkY, int chunkSize)
    {
        var hash = 2166136261u;

        for (var y = 0; y < chunkSize; y++)
        {
            for (var x = 0; x < chunkSize; x++)
            {
                var index = GetTileIndex(chunkX * chunkSize + x, chunkY * chunkSize + y);
                hash = (hash ^ (uint)index) * 16777619u;
            }
        }

        return hash;
    }
}
