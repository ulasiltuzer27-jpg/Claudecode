using Microsoft.Xna.Framework;
using PixelSurvival.World;

namespace PixelSurvival.Systems.Hostiles;

/// <summary>Yol arama denemesinin sonucu.</summary>
public enum PathResult
{
    /// <summary>Hedefe kadar tam bir yol bulundu.</summary>
    Complete,

    /// <summary>
    /// Hedefe ulaşılamadı (duvarla çevrili ya da düğüm bütçesi bitti) ama
    /// hedefe EN YAKIN ulaşılabilir kareye giden yol döndürüldü.
    /// </summary>
    Partial,

    /// <summary>Başlangıçtan daha iyi bir kare yok — yol boş.</summary>
    None
}

/// <summary>
/// Tile ızgarası üzerinde A* yol bulma.
///
/// ── Neden gerekti ───────────────────────────────────────────────────────
/// Düşmanlar madde 15'ten beri oyuncuya DÜZ ÇİZGİDE yürüyordu. Aradaki
/// duvara girip <see cref="Systems.Collision.TileCollider"/> tarafından
/// durduruluyorlar, sonra da aynı yöne itmeye devam ediyorlardı: oyuncu
/// bir kayanın arkasına geçtiğinde düşman kayaya yaslanıp titriyordu.
///
/// ── Neden bütçeli ───────────────────────────────────────────────────────
/// Dünya SONSUZ. Ulaşılamayan bir hedefe klasik A*, "açık küme boşalana
/// kadar" arar — burada o küme hiç boşalmaz ve arama bütün chunk'ları
/// üretmeye başlar. <see cref="DefaultNodeBudget"/> aramayı sınırlar;
/// bütçe biterse hedefe en yakın ulaşılabilir kareye giden yol döner
/// (<see cref="PathResult.Partial"/>). Düşman böylece donmak yerine
/// oyuncuya doğru yaklaşabildiği kadar yaklaşır.
///
/// ── Neden sınıf, static değil ───────────────────────────────────────────
/// Arama tabloları (maliyet, geldiği yer, kapalı küme, öncelik kuyruğu)
/// ÇAĞRILAR ARASINDA YENİDEN KULLANILIYOR. Static metotlarda her çağrı
/// dört koleksiyon ayırırdı; saniyede onlarca yeniden planlama ile bu,
/// düzenli çöp toplama duraklamaları demek. Bunun bedeli: bir örnek aynı
/// anda TEK arama yapabilir (thread-safe DEĞİL).
/// </summary>
public sealed class TilePathfinder
{
    /// <summary>Dik komşunun maliyeti. Tamsayı: kayan nokta birikimi yok.</summary>
    public const int OrthogonalCost = 10;

    /// <summary>Çapraz komşunun maliyeti — 10 × √2 ≈ 14.</summary>
    public const int DiagonalCost = 14;

    /// <summary>
    /// Tek aramada genişletilecek en fazla düğüm.
    ///
    /// 512 düğüm, tipik bir düşman-oyuncu mesafesinde (≈25 tile) engelli
    /// bir yolu bulmaya fazlasıyla yetiyor; ulaşılamaz hedefte ise arama
    /// birkaç yüz mikrosaniyede kesiliyor.
    /// </summary>
    public const int DefaultNodeBudget = 512;

    /// <summary>Sekiz komşu. Çaprazlar sonda: dik yönler önce denensin.</summary>
    private static readonly Point[] Neighbours =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    ];

    private readonly Dictionary<Point, int> _cost = [];
    private readonly Dictionary<Point, Point> _cameFrom = [];
    private readonly HashSet<Point> _closed = [];
    private readonly PriorityQueue<Point, int> _open = new();

    /// <summary>Son aramada genişletilen düğüm sayısı — ayar ve test için.</summary>
    public int LastExpandedNodes { get; private set; }

    /// <summary>Oyun içi kullanım: katılığı haritadan okur.</summary>
    public PathResult FindPath(TileMap map, Point start, Point goal, List<Point> path,
                               int nodeBudget = DefaultNodeBudget) =>
        FindPath(map.IsSolidTile, start, goal, path, nodeBudget);

    /// <summary>
    /// A* araması.
    ///
    /// Katılık bir <see cref="Func{T1,T2,TResult}"/> ile alınıyor:
    /// <see cref="TileMap"/> Content Pipeline'dan gelen bir
    /// <see cref="Tileset"/> istiyor, oysa yol bulmanın doğruluğu içerikten
    /// bağımsız. Böylece <c>--self-test</c> içinde elle çizilmiş bir
    /// labirentle sınanabiliyor.
    /// </summary>
    /// <param name="path">Sonuç. Başlangıç karesi DAHİL DEĞİL — orada zaten duruluyor.</param>
    public PathResult FindPath(Func<int, int, bool> isSolid, Point start, Point goal,
                               List<Point> path, int nodeBudget = DefaultNodeBudget)
    {
        path.Clear();
        _cost.Clear();
        _cameFrom.Clear();
        _closed.Clear();
        _open.Clear();
        LastExpandedNodes = 0;

        if (start == goal) return PathResult.Complete;

        _cost[start] = 0;
        _open.Enqueue(start, Heuristic(start, goal));

        // Hedefe en yakin ulasilabilir kare. Butce bitse ya da hedef
        // duvarla cevrili olsa bile dusman DONMAMALI; bu kareye kadar
        // yuruyup gerisini duz cizgide deniyor.
        var best = start;
        var bestHeuristic = Heuristic(start, goal);

        while (_open.TryDequeue(out var current, out _))
        {
            if (!_closed.Add(current)) continue;   // kuyrukta kalmis eski kopya

            if (current == goal)
            {
                Reconstruct(current, start, path);
                return PathResult.Complete;
            }

            var currentHeuristic = Heuristic(current, goal);
            if (currentHeuristic < bestHeuristic)
            {
                bestHeuristic = currentHeuristic;
                best = current;
            }

            if (++LastExpandedNodes >= nodeBudget) break;

            var currentCost = _cost[current];

            foreach (var offset in Neighbours)
            {
                var next = new Point(current.X + offset.X, current.Y + offset.Y);

                if (_closed.Contains(next) || isSolid(next.X, next.Y)) continue;

                var diagonal = offset.X != 0 && offset.Y != 0;

                // Kose kesme yasak: iki dik komsudan biri doluysa capraz
                // gecis, carpisma kutusunun duvarin kosesine sikismasi
                // demek. Yol bulucu boyle bir adim onerirse dusman
                // gorunurde bos bir kosede takilir.
                if (diagonal &&
                    (isSolid(current.X + offset.X, current.Y) ||
                     isSolid(current.X, current.Y + offset.Y)))
                {
                    continue;
                }

                var tentative = currentCost + (diagonal ? DiagonalCost : OrthogonalCost);

                if (_cost.TryGetValue(next, out var known) && tentative >= known) continue;

                _cost[next] = tentative;
                _cameFrom[next] = current;
                _open.Enqueue(next, tentative + Heuristic(next, goal));
            }
        }

        if (best == start) return PathResult.None;

        Reconstruct(best, start, path);
        return PathResult.Partial;
    }

    /// <summary>
    /// Octile mesafe — sekiz yönlü ızgaranın gerçek en kısa mesafesi.
    ///
    /// Manhattan mesafesi burada AŞIRI tahmin ederdi (çaprazı saymaz) ve
    /// A*'ın en kısa yolu bulma garantisi düşerdi.
    /// </summary>
    private static int Heuristic(Point from, Point to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);

        return OrthogonalCost * (dx + dy) +
               (DiagonalCost - 2 * OrthogonalCost) * Math.Min(dx, dy);
    }

    /// <summary>Geldiği-yer zincirini baştan sona çevirip listeye yazar.</summary>
    private void Reconstruct(Point end, Point start, List<Point> path)
    {
        for (var node = end; node != start; node = _cameFrom[node])
        {
            path.Add(node);
        }

        path.Reverse();
    }

    /// <summary>
    /// İki nokta arasında engel var mı.
    ///
    /// Yol bulmanın maliyetini ödemeden önceki ucuz kontrol: açık arazide
    /// düşmanların çoğu oyuncuyu doğrudan görüyor ve A* çalıştırmaya gerek
    /// kalmıyor.
    ///
    /// Örnekleme noktanın kendisiyle değil, <paramref name="radius"/>
    /// kadar şişirilmiş bir kutuyla yapılıyor: nokta bazlı bir kontrol,
    /// gövdesi sığmayan bir aralıktan "görüyorum" derdi ve düşman o
    /// aralığa kafa atardı.
    /// </summary>
    public static bool HasLineOfSight(TileMap map, Vector2 from, Vector2 to, float radius)
    {
        var delta = to - from;
        var distance = delta.Length();

        if (distance < 0.001f) return true;

        var direction = delta / distance;

        // Yarim tile adim: 16 pixel'lik bir tile hicbir ornekleme arasinda
        // tamamen atlanamaz.
        var step = map.TileSize * 0.5f;
        var steps = (int)MathF.Ceiling(distance / step);

        for (var i = 1; i <= steps; i++)
        {
            var travelled = MathF.Min(i * step, distance);
            var point = from + direction * travelled;

            if (map.IsSolidAtWorld(point.X - radius, point.Y - radius) ||
                map.IsSolidAtWorld(point.X + radius, point.Y - radius) ||
                map.IsSolidAtWorld(point.X - radius, point.Y + radius) ||
                map.IsSolidAtWorld(point.X + radius, point.Y + radius))
            {
                return false;
            }
        }

        return true;
    }
}
