using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.World;

/// <summary>Üretilmiş tek bir chunk'ın tile indeksleri.</summary>
public sealed class Chunk(Point coordinate, int size)
{
    public Point Coordinate { get; } = coordinate;
    public int Size { get; } = size;
    public int[,] Tiles { get; } = new int[size, size];

    /// <summary>Bu chunk'ın kapsadığı dünya pixel alanı.</summary>
    public Rectangle WorldBounds(int tileSize) => new(
        Coordinate.X * Size * tileSize,
        Coordinate.Y * Size * tileSize,
        Size * tileSize,
        Size * tileSize);
}

/// <summary>
/// AŞAMA 1 / MADDE 5 — chunk tabanlı prosedürel dünya.
///
/// Madde 4'teki 40x30 elle yazılmış test haritasının yerini aldı.
/// Dünya artık SINIRSIZ: chunk'lar kameranın çevresinde talep üzerine üretilir,
/// uzaklaşınca bellekten atılır.
///
/// Dışarıya verilen arayüz madde 4'tekiyle aynı kaldı — <see cref="IsSolidTile"/>
/// ve <see cref="Draw"/> çağıran kod (Player, TileCollider, Game1) değişmedi.
/// TEK istisna: eski <c>Bounds</c> özelliği kaldırıldı, sonsuz dünyada karşılığı yok.
/// </summary>
public sealed class TileMap
{
    /// <summary>
    /// Chunk kenar uzunluğu (tile). 32 tile = 512 dünya pixel'i.
    /// Görüş alanı ~427x240 pixel olduğu için aynı anda 2x2 civarı chunk görünür.
    /// Daha küçük chunk = daha sık üretim çağrısı, daha büyük = daha uzun donma.
    /// </summary>
    public const int ChunkSize = 32;

    /// <summary>Bu yarıçapın dışındaki chunk'lar bellekten atılır.</summary>
    private const int KeepRadiusInChunks = 3;

    private readonly Dictionary<Point, Chunk> _chunks = [];

    /// <summary>
    /// Oyuncunun dünyada yaptığı KALICI değişiklikler (madde 6: toplanan kaynaklar).
    ///
    /// Neden ayrı bir katman?
    /// Chunk'lar bellekten atılıp geri geldiklerinde gürültüden YENİDEN üretilirler.
    /// Kesilen ağaç chunk verisine yazılsaydı, oyuncu uzaklaşıp geri döndüğünde
    /// ağaç geri gelirdi. Bu sözlük chunk yaşam döngüsünden bağımsız yaşar ve
    /// her okuma yolunda üretimin ÖNÜNE geçer.
    ///
    /// Madde 9'daki inşa sistemi de bu katmanı kullanacak. Kalıcılık (save)
    /// geldiğinde diske yazılması gereken tek dünya verisi budur — gerisi
    /// tohumdan yeniden üretilebilir.
    /// </summary>
    private readonly Dictionary<Point, int> _overrides = [];
    private readonly ITileGenerator _generator;
    private readonly Tileset _tileset;

    public int TileSize => _tileset.TileSize;
    public int LoadedChunkCount => _chunks.Count;
    public int ModifiedTileCount => _overrides.Count;
    public int Seed => _generator.Seed;

    /// <summary>
    /// Haritanın sınırları; sonsuz dünyada null. Kamera bunu doğrudan
    /// <c>Camera2D.Follow</c>'a geçirir — madde 5'te <c>Rectangle?</c>
    /// yapılmasının sebebi tam olarak buydu.
    /// </summary>
    public Rectangle? Bounds => _generator.Bounds;

    public TileMap(ITileGenerator generator, Tileset tileset)
    {
        _generator = generator;
        _tileset = tileset;
    }

    /// <summary>Dünya pixel'ini chunk koordinatına çevirir.</summary>
    public Point WorldToChunk(Vector2 worldPosition) => new(
        (int)Math.Floor(worldPosition.X / (ChunkSize * TileSize)),
        (int)Math.Floor(worldPosition.Y / (ChunkSize * TileSize)));

    /// <summary>
    /// Görünür alanı kapsayan chunk'ları üretir, uzaktakileri atar.
    /// Her karede çağrılır ama zaten yüklü chunk'lar için maliyeti sözlük
    /// aramasından ibarettir.
    /// </summary>
    public void UpdateLoadedChunks(Rectangle visibleWorldArea, Vector2 centerWorldPosition)
    {
        var chunkPixels = ChunkSize * TileSize;

        var minX = (int)Math.Floor((double)visibleWorldArea.Left / chunkPixels);
        var minY = (int)Math.Floor((double)visibleWorldArea.Top / chunkPixels);
        var maxX = (int)Math.Floor((double)visibleWorldArea.Right / chunkPixels);
        var maxY = (int)Math.Floor((double)visibleWorldArea.Bottom / chunkPixels);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                EnsureChunk(new Point(x, y));
            }
        }

        UnloadDistantChunks(WorldToChunk(centerWorldPosition));
    }

    private Chunk EnsureChunk(Point coordinate)
    {
        if (_chunks.TryGetValue(coordinate, out var existing))
        {
            return existing;
        }

        var chunk = new Chunk(coordinate, ChunkSize);

        for (var y = 0; y < ChunkSize; y++)
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                chunk.Tiles[x, y] = _generator.GetTileIndex(
                    coordinate.X * ChunkSize + x,
                    coordinate.Y * ChunkSize + y);
            }
        }

        // Üretimden SONRA oyuncu değişikliklerini uygula: chunk yeniden
        // yüklendiğinde kesilen ağaç geri gelmesin.
        if (_overrides.Count > 0)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                for (var x = 0; x < ChunkSize; x++)
                {
                    var world = new Point(
                        coordinate.X * ChunkSize + x,
                        coordinate.Y * ChunkSize + y);

                    if (_overrides.TryGetValue(world, out var overridden))
                    {
                        chunk.Tiles[x, y] = overridden;
                    }
                }
            }
        }

        _chunks[coordinate] = chunk;
        return chunk;
    }

    /// <summary>
    /// Bir tile'ı kalıcı olarak değiştirir. Toplama (madde 6) ve ileride
    /// inşa (madde 9) bu metodu kullanır.
    /// </summary>
    public void SetTile(int tileX, int tileY, int tileIndex)
    {
        var key = new Point(tileX, tileY);
        _overrides[key] = tileIndex;

        // Yüklü chunk varsa anında güncelle, yeniden üretim beklenmesin.
        var chunkCoordinate = ToChunkCoordinate(tileX, tileY);
        if (_chunks.TryGetValue(chunkCoordinate, out var chunk))
        {
            chunk.Tiles[
                tileX - chunkCoordinate.X * ChunkSize,
                tileY - chunkCoordinate.Y * ChunkSize] = tileIndex;
        }
    }

    private static Point ToChunkCoordinate(int tileX, int tileY) => new(
        (int)Math.Floor((double)tileX / ChunkSize),
        (int)Math.Floor((double)tileY / ChunkSize));

    private void UnloadDistantChunks(Point center)
    {
        // Sözlükten dolaşırken silinemez; önce adayları topla.
        List<Point>? doomed = null;

        foreach (var coordinate in _chunks.Keys)
        {
            var distance = Math.Max(
                Math.Abs(coordinate.X - center.X),
                Math.Abs(coordinate.Y - center.Y));

            if (distance > KeepRadiusInChunks)
            {
                (doomed ??= []).Add(coordinate);
            }
        }

        if (doomed is null)
        {
            return;
        }

        foreach (var coordinate in doomed)
        {
            _chunks.Remove(coordinate);
        }
    }

    /// <summary>
    /// Tile koordinatındaki hücre katı mı.
    ///
    /// Chunk yüklü değilse üreticiye DOĞRUDAN sorulur, chunk üretilmez.
    /// Çarpışma her karede bu metodu çağırıyor; oradan chunk ayırmak
    /// beklenmedik donmalara yol açardı.
    /// </summary>
    public bool IsSolidTile(int tileX, int tileY)
    {
        // Override her okuma yolunda önce gelir — chunk yüklü olsun olmasın.
        if (_overrides.TryGetValue(new Point(tileX, tileY), out var overridden))
        {
            return _tileset.IsSolid(overridden);
        }

        var chunkCoordinate = ToChunkCoordinate(tileX, tileY);

        if (!_chunks.TryGetValue(chunkCoordinate, out var chunk))
        {
            return _generator.IsSolid(tileX, tileY);
        }

        var localX = tileX - chunkCoordinate.X * ChunkSize;
        var localY = tileY - chunkCoordinate.Y * ChunkSize;
        return _tileset.IsSolid(chunk.Tiles[localX, localY]);
    }

    /// <summary>Dünya pixel koordinatındaki nokta katı bir tile'a mı düşüyor.</summary>
    public bool IsSolidAtWorld(float worldX, float worldY) =>
        IsSolidTile(
            (int)MathF.Floor(worldX / TileSize),
            (int)MathF.Floor(worldY / TileSize));

    /// <summary>Yalnızca görünür alandaki tile'ları çizer (frustum culling).</summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle visibleWorldArea)
    {
        var minTileX = (int)Math.Floor((double)visibleWorldArea.Left / TileSize);
        var minTileY = (int)Math.Floor((double)visibleWorldArea.Top / TileSize);
        var maxTileX = (int)Math.Floor((double)visibleWorldArea.Right / TileSize);
        var maxTileY = (int)Math.Floor((double)visibleWorldArea.Bottom / TileSize);

        for (var tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            for (var tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                var index = GetTileIndex(tileX, tileY);

                // Varyant KONUMDAN türer: aynı tile her karede aynı varyantı
                // gösterir (titremez) ama komşuları farklı olur, böylece
                // geniş alanlar tekrar eden bir ızgara gibi görünmez.
                var variant = (int)(Noise.HashToUnit(tileX, tileY, Seed ^ 0x5AF3) *
                                    _tileset.Variants);

                spriteBatch.Draw(
                    _tileset.Texture,
                    new Rectangle(tileX * TileSize, tileY * TileSize, TileSize, TileSize),
                    _tileset.GetSourceRectangle(index, variant),
                    Color.White);
            }
        }
    }

    /// <summary>Tile koordinatındaki tile'ın indeksi (override'lar dahil).</summary>
    public int GetTileIndex(int tileX, int tileY)
    {
        if (_overrides.TryGetValue(new Point(tileX, tileY), out var overridden))
        {
            return overridden;
        }

        var chunkCoordinate = ToChunkCoordinate(tileX, tileY);
        var chunk = EnsureChunk(chunkCoordinate);
        return chunk.Tiles[
            tileX - chunkCoordinate.X * ChunkSize,
            tileY - chunkCoordinate.Y * ChunkSize];
    }

    /// <summary>
    /// Bu tile hangi görsel varyantı kullanır.
    ///
    /// Konumdan türetilir, saklanmaz: hem ek bellek istemiyor hem de host
    /// ile istemci aynı sonuca varıyor. Gürültü değil hash kullanılıyor —
    /// komşu tile'lar arasında korelasyon istemiyoruz, aksi halde
    /// varyantlar öbek öbek dizilir ve desen yine görünür olur.
    /// </summary>
    private int VariantAt(int tileX, int tileY)
    {
        if (_tileset.Variants <= 1)
        {
            return 0;
        }

        var value = Noise.HashToUnit(tileX, tileY, _generator.Seed ^ 0x5CA1AB1E);
        return Math.Min((int)(value * _tileset.Variants), _tileset.Variants - 1);
    }

    /// <summary>Yüklü chunk'ların dünya sınırları — F2 debug görünümü için.</summary>
    public IEnumerable<Rectangle> LoadedChunkBounds =>
        _chunks.Values.Select(c => c.WorldBounds(TileSize));
}
