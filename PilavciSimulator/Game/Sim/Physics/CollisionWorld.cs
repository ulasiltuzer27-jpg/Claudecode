using System.Numerics;

namespace PilavciSimulator.Sim.Physics;

public struct RayHit
{
    public float Distance;
    public Vector3 Point;
    public Vector3 Normal;
    public int ColliderIndex;
    public bool IsDynamic;
    public int OwnerId;
    public ColliderFlags Flags;
}

/// <summary>
/// Statik kutular (izgara ile hizli sorgu) + her tik yenilenen dinamik
/// kutular (arabalar). Hem host simulasyonu hem istemcinin kendi oyuncu
/// hareketi ayni dunyayi kullanir; geometri her iki tarafta ayni koddan
/// (DistrictBuilder) uretildigi icin ag uzerinden gonderilmez.
/// </summary>
public sealed class CollisionWorld
{
    public const float CellSize = 4f;

    public List<BoxCollider> Static { get; } = new();
    public List<BoxCollider> Dynamic { get; } = new();

    private readonly Dictionary<(int, int), List<int>> _grid = new();
    private readonly List<int> _scratch = new();
    private readonly HashSet<int> _seen = new();

    public int AddStatic(BoxCollider c)
    {
        Static.Add(c);
        var index = Static.Count - 1;
        var r = MathF.Sqrt(c.Half.X * c.Half.X + c.Half.Z * c.Half.Z);
        var x0 = (int)MathF.Floor((c.Center.X - r) / CellSize);
        var x1 = (int)MathF.Floor((c.Center.X + r) / CellSize);
        var z0 = (int)MathF.Floor((c.Center.Z - r) / CellSize);
        var z1 = (int)MathF.Floor((c.Center.Z + r) / CellSize);
        for (var x = x0; x <= x1; x++)
        {
            for (var z = z0; z <= z1; z++)
            {
                if (!_grid.TryGetValue((x, z), out var list))
                {
                    list = new List<int>();
                    _grid[(x, z)] = list;
                }

                list.Add(index);
            }
        }

        return index;
    }

    /// <summary>XZ dikdortgenine dokunan statik kutularin indeksleri (tekrarsiz).</summary>
    public List<int> QueryStatic(float minX, float minZ, float maxX, float maxZ)
    {
        _scratch.Clear();
        _seen.Clear();
        var x0 = (int)MathF.Floor(minX / CellSize);
        var x1 = (int)MathF.Floor(maxX / CellSize);
        var z0 = (int)MathF.Floor(minZ / CellSize);
        var z1 = (int)MathF.Floor(maxZ / CellSize);
        for (var x = x0; x <= x1; x++)
        {
            for (var z = z0; z <= z1; z++)
            {
                if (_grid.TryGetValue((x, z), out var list))
                {
                    foreach (var i in list)
                    {
                        if (_seen.Add(i))
                        {
                            _scratch.Add(i);
                        }
                    }
                }
            }
        }

        return _scratch;
    }

    /// <summary>
    /// Verilen daireyi (XZ) kesen, ust yuzeyi <paramref name="maxTop"/>'u
    /// gecmeyen kutularin en yuksek ust yuzeyi. Hicbiri yoksa 0 (yer).
    /// </summary>
    public float GroundHeight(Vector2 xz, float radius, float maxTop, int ignoreOwner = 0)
    {
        var best = 0f;
        var idx = QueryStatic(xz.X - radius, xz.Y - radius, xz.X + radius, xz.Y + radius);
        foreach (var i in idx)
        {
            var c = Static[i];
            if (!c.Has(ColliderFlags.Solid) || c.Top > maxTop || c.Top <= best)
            {
                continue;
            }

            if (c.OverlapsCircleXZ(xz, radius))
            {
                best = c.Top;
            }
        }

        foreach (var c0 in Dynamic)
        {
            var c = c0;
            if (c.OwnerId == ignoreOwner || !c.Has(ColliderFlags.Solid) || c.Top > maxTop || c.Top <= best)
            {
                continue;
            }

            if (c.OverlapsCircleXZ(xz, radius))
            {
                best = c.Top;
            }
        }

        return best;
    }

    /// <summary>En yakin isin carpmasi; <paramref name="mask"/> bayraklarindan birini tasiyan kutular.</summary>
    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, ColliderFlags mask, out RayHit hit, int ignoreOwner = 0)
    {
        hit = default;
        hit.Distance = maxDist;
        var found = false;
        var end = origin + dir * maxDist;
        var idx = QueryStatic(MathF.Min(origin.X, end.X) - 0.5f, MathF.Min(origin.Z, end.Z) - 0.5f,
            MathF.Max(origin.X, end.X) + 0.5f, MathF.Max(origin.Z, end.Z) + 0.5f);
        foreach (var i in idx)
        {
            var c = Static[i];
            if ((c.Flags & mask) == 0)
            {
                continue;
            }

            if (c.Raycast(origin, dir, hit.Distance, out var t, out var n) && t < hit.Distance)
            {
                hit = new RayHit { Distance = t, Point = origin + dir * t, Normal = n, ColliderIndex = i, Flags = c.Flags };
                found = true;
            }
        }

        for (var i = 0; i < Dynamic.Count; i++)
        {
            var c = Dynamic[i];
            if ((c.Flags & mask) == 0 || (ignoreOwner != 0 && c.OwnerId == ignoreOwner))
            {
                continue;
            }

            if (c.Raycast(origin, dir, hit.Distance, out var t, out var n) && t < hit.Distance)
            {
                hit = new RayHit
                {
                    Distance = t, Point = origin + dir * t, Normal = n, ColliderIndex = i, IsDynamic = true,
                    OwnerId = c.OwnerId, Flags = c.Flags,
                };
                found = true;
            }
        }

        return found;
    }

    /// <summary>Kutunun (XZ, yaw'li) herhangi bir kati statik ya da dinamik engelle cakisip cakismadigi.</summary>
    public bool BoxBlocked(BoxCollider box, int ignoreOwner, float minTopToBlock)
    {
        var r = MathF.Sqrt(box.Half.X * box.Half.X + box.Half.Z * box.Half.Z);
        var idx = QueryStatic(box.Center.X - r, box.Center.Z - r, box.Center.X + r, box.Center.Z + r);
        foreach (var i in idx)
        {
            var c = Static[i];
            if (!c.Has(ColliderFlags.Solid) || c.Top < minTopToBlock || c.Bottom > box.Top)
            {
                continue;
            }

            if (BoxesOverlapXZ(box, c))
            {
                return true;
            }
        }

        foreach (var c in Dynamic)
        {
            if (c.OwnerId == ignoreOwner || !c.Has(ColliderFlags.Solid) || c.Top < minTopToBlock)
            {
                continue;
            }

            if (BoxesOverlapXZ(box, c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ayrik eksen teoremi, 2B (XZ), yaw'li iki dikdortgen.</summary>
    public static bool BoxesOverlapXZ(BoxCollider a, BoxCollider b)
    {
        Span<Vector2> axes = stackalloc Vector2[4];
        axes[0] = new Vector2(MathF.Cos(a.Yaw), -MathF.Sin(a.Yaw));
        axes[1] = new Vector2(MathF.Sin(a.Yaw), MathF.Cos(a.Yaw));
        axes[2] = new Vector2(MathF.Cos(b.Yaw), -MathF.Sin(b.Yaw));
        axes[3] = new Vector2(MathF.Sin(b.Yaw), MathF.Cos(b.Yaw));
        var ca = new Vector2(a.Center.X, a.Center.Z);
        var cb = new Vector2(b.Center.X, b.Center.Z);
        foreach (var ax in axes)
        {
            var ra = a.Half.X * MathF.Abs(Vector2.Dot(axes[0], ax)) + a.Half.Z * MathF.Abs(Vector2.Dot(axes[1], ax));
            var rb = b.Half.X * MathF.Abs(Vector2.Dot(axes[2], ax)) + b.Half.Z * MathF.Abs(Vector2.Dot(axes[3], ax));
            if (MathF.Abs(Vector2.Dot(cb - ca, ax)) > ra + rb)
            {
                return false;
            }
        }

        return true;
    }
}
