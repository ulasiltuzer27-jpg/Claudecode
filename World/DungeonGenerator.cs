using Microsoft.Xna.Framework;

namespace PixelSurvival.World;

/// <summary>
/// AŞAMA 2 / MADDE 16 — sınırlı, oda-koridor tabanlı zindan üretimi.
///
/// Sonsuz dünyadan farklı olarak SINIRLIDIR: verilen genişlik/yükseklik
/// dışındaki her koordinat duvardır. Bu sayede kamera clamp'i devreye girer
/// ve oyuncu zindanın "dışına" bakamaz.
///
/// Üretim, girişin dünya koordinatından türeyen bir tohumla yapılır:
/// aynı girişe tekrar girildiğinde AYNI zindan çıkar. Rastgele bir tohum
/// kullanılsaydı zindan her girişte değişir, oyuncu haritayı öğrenemezdi.
///
/// Tüm tile'lar talep üzerine hesaplanmaz; oda listesi bir kez kurulur ve
/// grid'e yazılır. Zindan küçük (varsayılan 64x48 tile) olduğu için bellek
/// sorunu değil ve <see cref="GetTileIndex"/> sabit zamanlı kalıyor.
/// </summary>
public sealed class DungeonGenerator : ITileGenerator
{
    private readonly int[,] _tiles;
    private readonly Tileset _tileset;
    private readonly int _tileSize;

    public int Seed { get; }
    public int Columns { get; }
    public int Rows { get; }

    /// <summary>Oyuncunun zindana girdiğinde belireceği tile.</summary>
    public Point EntranceTile { get; private set; }

    /// <summary>Boss'un doğduğu tile — en uzaktaki odanın merkezi.</summary>
    public Point BossTile { get; private set; }

    /// <summary>Üretilen odaların merkezleri; NPC/ganimet yerleştirme için.</summary>
    public IReadOnlyList<Point> RoomCenters => _roomCenters;

    private readonly List<Point> _roomCenters = [];

    public Rectangle? Bounds => new(0, 0, Columns * _tileSize, Rows * _tileSize);

    public DungeonGenerator(Tileset tileset, int seed, int columns, int rows,
                            int roomCount, int minRoom, int maxRoom)
    {
        _tileset = tileset;
        _tileSize = tileset.TileSize;
        Seed = seed;
        Columns = columns;
        Rows = rows;

        var wall = tileset.IndexOf("dungeon_wall");
        var floor = tileset.IndexOf("dungeon_floor");
        var entrance = tileset.IndexOf("dungeon_entrance");

        _tiles = new int[columns, rows];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                _tiles[x, y] = wall;
            }
        }

        var state = (uint)seed ^ 0x1F123BB5u;
        if (state == 0)
        {
            state = 0x9E3779B9u;
        }

        // --- Odaları yerleştir ---
        var rooms = new List<Rectangle>();
        var attempts = roomCount * 12;

        while (rooms.Count < roomCount && attempts-- > 0)
        {
            var w = minRoom + (int)(NextUnit(ref state) * (maxRoom - minRoom + 1));
            var h = minRoom + (int)(NextUnit(ref state) * (maxRoom - minRoom + 1));

            // 1 tile'lık dış kenar payı: oda haritanın kenarına yapışmasın.
            var x = 2 + (int)(NextUnit(ref state) * (columns - w - 4));
            var y = 2 + (int)(NextUnit(ref state) * (rows - h - 4));

            var candidate = new Rectangle(x, y, w, h);

            // Odalar arasında en az 1 tile duvar kalsın diye şişirerek kontrol et.
            var padded = new Rectangle(x - 1, y - 1, w + 2, h + 2);
            if (rooms.Any(r => r.Intersects(padded)))
            {
                continue;
            }

            rooms.Add(candidate);
        }

        if (rooms.Count == 0)
        {
            throw new InvalidOperationException(
                $"Zindan tohumu {seed}: hiç oda yerleştirilemedi. " +
                $"dungeons.json'daki oda boyutu harita boyutuna göre çok büyük olabilir.");
        }

        foreach (var room in rooms)
        {
            Carve(room, floor);
            _roomCenters.Add(new Point(room.X + room.Width / 2, room.Y + room.Height / 2));
        }

        // --- Odaları koridorlarla bağla ---
        // Her oda bir öncekine bağlanır: bağlantısız oda kalmaz, oyuncu
        // zindanın bir kısmına asla erişemez duruma düşmez.
        for (var i = 1; i < _roomCenters.Count; i++)
        {
            ConnectRooms(_roomCenters[i - 1], _roomCenters[i], floor,
                         NextUnit(ref state) < 0.5f);
        }

        EntranceTile = _roomCenters[0];
        _tiles[EntranceTile.X, EntranceTile.Y] = entrance;

        // Boss girişten EN UZAK odada: oyuncu zindanı gezmeden karşılaşmasın.
        BossTile = _roomCenters
            .OrderByDescending(c => Math.Abs(c.X - EntranceTile.X) + Math.Abs(c.Y - EntranceTile.Y))
            .First();
    }

    private void Carve(Rectangle room, int floor)
    {
        for (var y = room.Top; y < room.Bottom; y++)
        {
            for (var x = room.Left; x < room.Right; x++)
            {
                if (x > 0 && y > 0 && x < Columns - 1 && y < Rows - 1)
                {
                    _tiles[x, y] = floor;
                }
            }
        }
    }

    /// <summary>L şeklinde koridor. horizontalFirst, dönüşün nerede olacağını belirler.</summary>
    private void ConnectRooms(Point a, Point b, int floor, bool horizontalFirst)
    {
        if (horizontalFirst)
        {
            CarveHorizontal(a.X, b.X, a.Y, floor);
            CarveVertical(a.Y, b.Y, b.X, floor);
        }
        else
        {
            CarveVertical(a.Y, b.Y, a.X, floor);
            CarveHorizontal(a.X, b.X, b.Y, floor);
        }
    }

    private void CarveHorizontal(int x1, int x2, int y, int floor)
    {
        for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
        {
            if (x > 0 && y > 0 && x < Columns - 1 && y < Rows - 1)
            {
                _tiles[x, y] = floor;
            }
        }
    }

    private void CarveVertical(int y1, int y2, int x, int floor)
    {
        for (var y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
        {
            if (x > 0 && y > 0 && x < Columns - 1 && y < Rows - 1)
            {
                _tiles[x, y] = floor;
            }
        }
    }

    /// <summary>Sınır dışı her zaman duvardır — oyuncu zindandan taşamaz.</summary>
    public int GetTileIndex(int tileX, int tileY)
    {
        if (tileX < 0 || tileY < 0 || tileX >= Columns || tileY >= Rows)
        {
            return _tileset.IndexOf("dungeon_wall");
        }

        return _tiles[tileX, tileY];
    }

    public bool IsSolid(int tileX, int tileY) => _tileset.IsSolid(GetTileIndex(tileX, tileY));

    /// <summary>Zindan içindeki dünya pixel konumu (tile'ın alt-orta noktası).</summary>
    public Vector2 WorldPositionOf(Point tile) => new(
        tile.X * _tileSize + _tileSize / 2f,
        (tile.Y + 1) * _tileSize);

    /// <summary>xorshift32 — Noise.cs ile aynı taşınabilir PRNG.</summary>
    private static float NextUnit(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state / 4294967296f;
    }
}
