using System.Numerics;
using Starfall.Core;

namespace Starfall.Physics;

public enum ShapeKind { Box, Cylinder, Sphere, Hull }

/// <summary>
/// Yerel uzayda carpisma tanimi (yapi kuruculari dondurur). Kutu: Half = yari
/// kenarlar; silindir: Half.X = yaricap, Half.Y = yari yukseklik (yerel Y ekseni);
/// kure: Half.X = yaricap. Rot: three.js 'YXZ' Euler (x, y, z).
/// </summary>
public readonly record struct ShapeDef(ShapeKind Kind, Vector3 Pos, Vector3 Half, Vector3 Rot = default, ConvexHull? Hull = null)
{
    public static ShapeDef Box(float hx, float hy, float hz, Vector3 pos, Vector3 rot = default) => new(ShapeKind.Box, pos, new(hx, hy, hz), rot);
    public static ShapeDef Cyl(float r, float hh, Vector3 pos, Vector3 rot = default) => new(ShapeKind.Cylinder, pos, new(r, hh, r), rot);
    public static ShapeDef Ball(float r, Vector3 pos) => new(ShapeKind.Sphere, pos, new(r, r, r));

    /// <summary>Noktalarin disbukey zarfi (kaya, cati). pos: noktalarin koordinat sisteminin yerel konumu.</summary>
    public static ShapeDef HullOf(IReadOnlyList<Vector3> points, Vector3 pos = default, Vector3 rot = default)
        => FromHull(ConvexHull.Build(points), pos, rot);

    /// <summary>Hazir zarftan (or. olceklenmis kaya varyanti) sekil tanimi.</summary>
    public static ShapeDef FromHull(ConvexHull h, Vector3 pos = default, Vector3 rot = default)
    {
        // sekil merkezi = zarfin sinir kutusu merkezi; Rot merkez etrafinda uygulanir,
        // bu yuzden yerel ofset de ayni donusle tasinir
        var off = Vector3.Transform(h.Center, MathX.EulerYXZ(rot.X, rot.Y, rot.Z));
        return new(ShapeKind.Hull, pos + off, h.Half, rot, h);
    }
}

/// <summary>
/// Disbukey cokyuzlu: duzlemler (disa bakan normal + ofset) ve yuzey ucgenleri, merkeze gore.
/// Kucuk nokta kumeleri icin (kaya ~74 kose) kaba kuvvet duzlem taramasi yeterince hizli.
/// </summary>
public sealed class ConvexHull
{
    public Vector3[] Normals = Array.Empty<Vector3>();
    public float[] Offsets = Array.Empty<float>();
    public Vector3[] Tris = Array.Empty<Vector3>();
    public Vector3[] Points = Array.Empty<Vector3>();
    public Vector3 Center, Half;

    public static ConvexHull Build(IReadOnlyList<Vector3> input)
    {
        // tekil noktalar
        var pts = new List<Vector3>();
        foreach (var p in input)
        {
            bool dup = false;
            foreach (var q in pts) if (Vector3.DistanceSquared(p, q) < 1e-8f) { dup = true; break; }
            if (!dup) pts.Add(p);
        }
        var mn = new Vector3(float.MaxValue); var mx = new Vector3(float.MinValue);
        foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
        var center = (mn + mx) * 0.5f;
        for (int i = 0; i < pts.Count; i++) pts[i] -= center;
        float scale = MathF.Max(1e-3f, (mx - mn).Length());
        float eps = scale * 2e-5f;

        var normals = new List<Vector3>();
        var offsets = new List<float>();
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        for (int k = j + 1; k < n; k++)
        {
            var nn = Vector3.Cross(pts[j] - pts[i], pts[k] - pts[i]);
            float l = nn.Length();
            if (l < scale * scale * 1e-7f) continue;
            nn /= l;
            float d = Vector3.Dot(nn, pts[i]);
            int pos = 0, neg = 0;
            for (int m = 0; m < n; m++)
            {
                float sd = Vector3.Dot(nn, pts[m]) - d;
                if (sd > eps) pos++;
                else if (sd < -eps) neg++;
                if (pos > 0 && neg > 0) break;
            }
            if (pos > 0 && neg > 0) continue;
            if (pos > 0) { nn = -nn; d = -d; }
            bool same = false;
            for (int q = 0; q < normals.Count; q++)
                if (Vector3.Dot(normals[q], nn) > 0.99999f && MathF.Abs(offsets[q] - d) < eps * 4) { same = true; break; }
            if (same) continue;
            normals.Add(nn);
            offsets.Add(d);
        }

        // yuz cokgenleri -> yelpaze ucgenleri
        var tris = new List<Vector3>();
        for (int f = 0; f < normals.Count; f++)
        {
            var nn = normals[f];
            var on = new List<Vector3>();
            foreach (var p in pts) if (MathF.Abs(Vector3.Dot(nn, p) - offsets[f]) <= eps * 4) on.Add(p);
            if (on.Count < 3) continue;
            var c = Vector3.Zero;
            foreach (var p in on) c += p;
            c /= on.Count;
            var u = Vector3.Normalize(MathF.Abs(nn.Y) < 0.9f ? Vector3.Cross(nn, Vector3.UnitY) : Vector3.Cross(nn, Vector3.UnitX));
            var w = Vector3.Cross(nn, u);
            on.Sort((a, b) => MathF.Atan2(Vector3.Dot(a - c, w), Vector3.Dot(a - c, u)).CompareTo(MathF.Atan2(Vector3.Dot(b - c, w), Vector3.Dot(b - c, u))));
            for (int t = 1; t + 1 < on.Count; t++) { tris.Add(on[0]); tris.Add(on[t]); tris.Add(on[t + 1]); }
        }
        return new ConvexHull
        {
            Normals = normals.ToArray(), Offsets = offsets.ToArray(), Tris = tris.ToArray(), Points = pts.ToArray(),
            Center = center, Half = (mx - mn) * 0.5f,
        };
    }

    public ConvexHull Scaled(float s) => new()
    {
        Normals = Normals,
        Offsets = Offsets.Select(o => o * s).ToArray(),
        Tris = Tris.Select(t => t * s).ToArray(),
        Points = Points.Select(t => t * s).ToArray(),
        Center = Center * s,
        Half = Half * s,
    };

    /// <summary>En buyuk isaretli duzlem uzakligi (negatif = icerde).</summary>
    public float MaxSeparation(Vector3 l, out int face)
    {
        float best = float.MinValue;
        face = 0;
        for (int i = 0; i < Normals.Length; i++)
        {
            float sd = Vector3.Dot(Normals[i], l) - Offsets[i];
            if (sd > best) { best = sd; face = i; }
        }
        return best;
    }
}

public sealed class Shape
{
    public ShapeKind Kind;
    public Vector3 Center;
    public Vector3 Half;
    public Matrix4x4 Rot = Matrix4x4.Identity, InvRot = Matrix4x4.Identity;
    public bool Rotated;
    public ConvexHull? Hull;
    public string? Tag;
    public bool Enabled = true;
    public Vector3 Min, Max;
    public int Id;
    internal List<(int, int)> Cells = new();

    public Vector3 ToLocal(Vector3 p) => Rotated ? Vector3.Transform(p - Center, InvRot) : p - Center;
    public Vector3 DirToWorld(Vector3 d) => Rotated ? Vector3.TransformNormal(d, Rot) : d;
    public Vector3 DirToLocal(Vector3 d) => Rotated ? Vector3.TransformNormal(d, InvRot) : d;
}

public struct Contact
{
    public Vector3 Normal;  // carpilan yuzeyin disa bakan normali
    public float Depth;
    public Shape? Shape;    // null = arazi
    public string? Tag => Shape?.Tag ?? "ground";
}

public struct RayHit
{
    public float Distance;
    public Vector3 Point, Normal;
    public Shape? Shape;
}

/// <summary>
/// Statik carpisma dunyasi: adalarin yukseklik izgaralari + kutu/silindir/kure
/// sekilleri (XZ uzamsal izgarasinda). Dinamik govde yok; tek hareketli sey
/// karakter (CharacterMotor) ve o da kendi carpismasini burada sorgular.
/// </summary>
public sealed class CollisionWorld
{
    private const float CellSize = 8f;
    private readonly Dictionary<(int, int), List<Shape>> _grid = new();
    public readonly List<Shape> Shapes = new();
    private readonly List<World.Terrain> _terrains;
    private int _nextId;
    private readonly List<Shape> _tmp = new();
    private readonly HashSet<int> _seen = new();

    public CollisionWorld(List<World.Terrain> terrains)
    {
        _terrains = terrains;
    }

    public Shape Add(ShapeDef d, Vector3 at, float yaw = 0, string? tag = null)
    {
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw);
        var p = d.Pos;
        // yaw ile dondur (three.js: x' = x cos + z sin, z' = -x sin + z cos)
        var wpos = new Vector3(at.X + p.X * cy + p.Z * sy, at.Y + p.Y, at.Z - p.X * sy + p.Z * cy);
        var rot = MathX.EulerYXZ(d.Rot.X, d.Rot.Y + yaw, d.Rot.Z);
        var s = new Shape { Kind = d.Kind, Center = wpos, Half = d.Half, Hull = d.Hull, Tag = tag, Id = _nextId++ };
        bool identity = MathF.Abs(d.Rot.X) < 1e-6f && MathF.Abs(d.Rot.Z) < 1e-6f && MathF.Abs(d.Rot.Y + yaw) < 1e-6f;
        if (!identity && d.Kind != ShapeKind.Sphere)
        {
            s.Rotated = true;
            s.Rot = rot;
            Matrix4x4.Invert(rot, out s.InvRot);
        }
        ComputeBounds(s);
        Shapes.Add(s);
        Insert(s);
        return s;
    }

    public void Remove(Shape s)
    {
        foreach (var c in s.Cells) if (_grid.TryGetValue(c, out var l)) l.Remove(s);
        s.Cells.Clear();
        Shapes.Remove(s);
    }

    public void Move(Shape s, Vector3 center)
    {
        foreach (var c in s.Cells) if (_grid.TryGetValue(c, out var l)) l.Remove(s);
        s.Cells.Clear();
        s.Center = center;
        ComputeBounds(s);
        Insert(s);
    }

    private static void ComputeBounds(Shape s)
    {
        Vector3 ext;
        if (s.Kind == ShapeKind.Sphere) ext = new Vector3(s.Half.X);
        else if (!s.Rotated) ext = s.Kind != ShapeKind.Cylinder ? s.Half : new Vector3(s.Half.X, s.Half.Y, s.Half.X);
        else
        {
            var h = s.Kind != ShapeKind.Cylinder ? s.Half : new Vector3(s.Half.X, s.Half.Y, s.Half.X);
            var r = s.Rot;
            ext = new Vector3(
                MathF.Abs(r.M11) * h.X + MathF.Abs(r.M21) * h.Y + MathF.Abs(r.M31) * h.Z,
                MathF.Abs(r.M12) * h.X + MathF.Abs(r.M22) * h.Y + MathF.Abs(r.M32) * h.Z,
                MathF.Abs(r.M13) * h.X + MathF.Abs(r.M23) * h.Y + MathF.Abs(r.M33) * h.Z);
        }
        s.Min = s.Center - ext;
        s.Max = s.Center + ext;
    }

    private void Insert(Shape s)
    {
        int x0 = (int)MathF.Floor(s.Min.X / CellSize), x1 = (int)MathF.Floor(s.Max.X / CellSize);
        int z0 = (int)MathF.Floor(s.Min.Z / CellSize), z1 = (int)MathF.Floor(s.Max.Z / CellSize);
        for (int x = x0; x <= x1; x++)
        {
            for (int z = z0; z <= z1; z++)
            {
                if (!_grid.TryGetValue((x, z), out var l)) _grid[(x, z)] = l = new List<Shape>();
                l.Add(s);
                s.Cells.Add((x, z));
            }
        }
    }

    private List<Shape> Query(Vector3 min, Vector3 max)
    {
        _tmp.Clear();
        _seen.Clear();
        int x0 = (int)MathF.Floor(min.X / CellSize), x1 = (int)MathF.Floor(max.X / CellSize);
        int z0 = (int)MathF.Floor(min.Z / CellSize), z1 = (int)MathF.Floor(max.Z / CellSize);
        for (int x = x0; x <= x1; x++)
        {
            for (int z = z0; z <= z1; z++)
            {
                if (!_grid.TryGetValue((x, z), out var l)) continue;
                foreach (var s in l)
                {
                    if (!s.Enabled || !_seen.Add(s.Id)) continue;
                    if (s.Max.X < min.X || s.Min.X > max.X || s.Max.Y < min.Y || s.Min.Y > max.Y || s.Max.Z < min.Z || s.Min.Z > max.Z) continue;
                    _tmp.Add(s);
                }
            }
        }
        return _tmp;
    }

    // ------------------------------------------------------------------
    // kure carpismasi
    // ------------------------------------------------------------------
    public void SphereContacts(Vector3 c, float r, List<Contact> outList)
    {
        var ext = new Vector3(r);
        foreach (var s in Query(c - ext, c + ext))
        {
            if (SphereVsShape(c, r, s, out var n, out float d))
                outList.Add(new Contact { Normal = n, Depth = d, Shape = s });
        }
        TerrainContacts(c, r, outList);
    }

    private static bool SphereVsShape(Vector3 c, float r, Shape s, out Vector3 normal, out float depth)
    {
        normal = Vector3.UnitY;
        depth = 0;
        switch (s.Kind)
        {
            case ShapeKind.Sphere:
            {
                var v = c - s.Center;
                float dist = v.Length();
                float rr = r + s.Half.X;
                if (dist >= rr) return false;
                normal = dist > 1e-6f ? v / dist : Vector3.UnitY;
                depth = rr - dist;
                return true;
            }
            case ShapeKind.Box:
            {
                var l = s.ToLocal(c);
                var h = s.Half;
                var q = Vector3.Clamp(l, -h, h);
                var v = l - q;
                float d2 = v.LengthSquared();
                if (d2 > r * r) return false;
                if (d2 > 1e-10f)
                {
                    float dist = MathF.Sqrt(d2);
                    normal = s.DirToWorld(v / dist);
                    depth = r - dist;
                    return true;
                }
                // merkez kutunun icinde: en kisa cikis ekseni
                float dx = h.X - MathF.Abs(l.X), dy = h.Y - MathF.Abs(l.Y), dz = h.Z - MathF.Abs(l.Z);
                Vector3 ln;
                if (dy <= dx && dy <= dz) { ln = new Vector3(0, MathF.Sign(l.Y == 0 ? 1 : l.Y), 0); depth = dy + r; }
                else if (dx <= dz) { ln = new Vector3(MathF.Sign(l.X == 0 ? 1 : l.X), 0, 0); depth = dx + r; }
                else { ln = new Vector3(0, 0, MathF.Sign(l.Z == 0 ? 1 : l.Z)); depth = dz + r; }
                normal = s.DirToWorld(ln);
                return true;
            }
            case ShapeKind.Hull:
            {
                var h = s.Hull!;
                var l = s.ToLocal(c);
                float sep = h.MaxSeparation(l, out int face);
                if (sep > r) return false;
                if (sep <= 0)
                {
                    normal = s.DirToWorld(h.Normals[face]);
                    depth = r - sep;
                    return true;
                }
                // disarida: zarf yuzeyindeki en yakin nokta (kenar/kose bolgesinde duzlem testi yaniltir)
                float bestD2 = float.MaxValue;
                var bq = l;
                var tr = h.Tris;
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    var q = ClosestOnTri(l, tr[i], tr[i + 1], tr[i + 2]);
                    float d2 = Vector3.DistanceSquared(l, q);
                    if (d2 < bestD2) { bestD2 = d2; bq = q; }
                }
                if (bestD2 >= r * r) return false;
                float dist = MathF.Sqrt(bestD2);
                normal = s.DirToWorld(dist > 1e-6f ? (l - bq) / dist : h.Normals[face]);
                depth = r - dist;
                return true;
            }
            default: // silindir (yerel Y ekseni)
            {
                var l = s.ToLocal(c);
                float rad = s.Half.X, hh = s.Half.Y;
                float rl = MathF.Sqrt(l.X * l.X + l.Z * l.Z);
                float cy = MathX.Clamp(l.Y, -hh, hh);
                float qx = l.X, qz = l.Z;
                if (rl > rad) { qx = l.X / rl * rad; qz = l.Z / rl * rad; }
                var q = new Vector3(qx, cy, qz);
                var v = l - q;
                float d2 = v.LengthSquared();
                if (d2 > r * r) return false;
                if (d2 > 1e-10f)
                {
                    float dist = MathF.Sqrt(d2);
                    normal = s.DirToWorld(v / dist);
                    depth = r - dist;
                    return true;
                }
                float side = rad - rl, vert = hh - MathF.Abs(l.Y);
                if (vert < side)
                {
                    normal = s.DirToWorld(new Vector3(0, l.Y >= 0 ? 1 : -1, 0));
                    depth = vert + r;
                }
                else
                {
                    normal = s.DirToWorld(rl > 1e-6f ? new Vector3(l.X / rl, 0, l.Z / rl) : Vector3.UnitX);
                    depth = side + r;
                }
                return true;
            }
        }
    }

    private void TerrainContacts(Vector3 c, float r, List<Contact> outList)
    {
        foreach (var t in _terrains)
        {
            if (!t.Contains(c.X, c.Z)) continue;
            int ix0 = (int)MathF.Floor((c.X - r - t.MinX) / t.Cell), ix1 = (int)MathF.Floor((c.X + r - t.MinX) / t.Cell);
            int iz0 = (int)MathF.Floor((c.Z - r - t.MinZ) / t.Cell), iz1 = (int)MathF.Floor((c.Z + r - t.MinZ) / t.Cell);
            ix0 = Math.Max(0, ix0); iz0 = Math.Max(0, iz0);
            ix1 = Math.Min(t.Cells - 1, ix1); iz1 = Math.Min(t.Cells - 1, iz1);
            Contact best = default;
            float bestDepth = 0;
            for (int iz = iz0; iz <= iz1; iz++)
            {
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    var a = V(t, ix, iz); var b = V(t, ix + 1, iz); var d = V(t, ix, iz + 1); var e = V(t, ix + 1, iz + 1);
                    TriContact(c, r, a, d, b, ref best, ref bestDepth);
                    TriContact(c, r, b, d, e, ref best, ref bestDepth);
                }
            }
            if (bestDepth > 0) outList.Add(best);
        }
    }

    private static Vector3 V(World.Terrain t, int ix, int iz) => new(t.MinX + ix * t.Cell, t.Heights[iz * t.Verts + ix], t.MinZ + iz * t.Cell);

    private static void TriContact(Vector3 c, float r, Vector3 a, Vector3 b, Vector3 cc, ref Contact best, ref float bestDepth)
    {
        var fn = Vector3.Cross(b - a, cc - a);
        float fl = fn.Length();
        if (fl < 1e-8f) return;
        fn /= fl;
        if (fn.Y < 0) fn = -fn;
        float sd = Vector3.Dot(c - a, fn);
        if (sd > r) return;
        var q = ClosestOnTri(c, a, b, cc);
        Vector3 n;
        float depth;
        if (sd >= 0)
        {
            var v = c - q;
            float dist = v.Length();
            if (dist >= r) return;
            n = dist > 1e-6f ? v / dist : fn;
            depth = r - dist;
        }
        else
        {
            // merkez yuzeyin altinda: yalnizca izdusum ucgenin icindeyse yukari it
            var proj = c - fn * sd;
            if (Vector3.DistanceSquared(proj, q) > 1e-6f) return;
            n = fn;
            depth = r - sd;
        }
        if (depth > bestDepth)
        {
            bestDepth = depth;
            best = new Contact { Normal = n, Depth = depth, Shape = null };
        }
    }

    // Ericson, Real-Time Collision Detection 5.1.5
    private static Vector3 ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float denom = 1f / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }

    // ------------------------------------------------------------------
    // isin atma
    // ------------------------------------------------------------------
    public bool Raycast(Vector3 o, Vector3 dir, float maxDist, out RayHit hit, Shape? ignore = null)
    {
        hit = new RayHit { Distance = float.MaxValue };
        var end = o + dir * maxDist;
        var mn = Vector3.Min(o, end) - Vector3.One;
        var mx = Vector3.Max(o, end) + Vector3.One;
        bool any = false;
        // uzun yatay isinlar icin aday listesi buyuk olabilir; dunya kucuk oldugu icin sorun degil
        foreach (var s in Query(mn, mx))
        {
            if (s == ignore) continue;
            if (RayShape(o, dir, s, out float t, out var n) && t >= 0 && t <= maxDist && t < hit.Distance)
            {
                hit = new RayHit { Distance = t, Point = o + dir * t, Normal = n, Shape = s };
                any = true;
            }
        }
        if (RayTerrain(o, dir, MathF.Min(maxDist, hit.Distance), out float tt))
        {
            var p = o + dir * tt;
            hit = new RayHit { Distance = tt, Point = p, Normal = TerrainNormalAt(p), Shape = null };
            any = true;
        }
        return any;
    }

    private Vector3 TerrainNormalAt(Vector3 p)
    {
        foreach (var t in _terrains) if (t.Contains(p.X, p.Z)) return t.Normal(p.X, p.Z);
        return Vector3.UnitY;
    }

    public float TerrainHeight(float x, float z)
    {
        foreach (var t in _terrains) if (t.Contains(x, z)) return t.HeightTri(x, z);
        return -9f;
    }

    private bool RayTerrain(Vector3 o, Vector3 d, float maxDist, out float tHit)
    {
        tHit = 0;
        if (MathF.Abs(d.X) < 1e-6f && MathF.Abs(d.Z) < 1e-6f)
        {
            float h = TerrainHeight(o.X, o.Z);
            if (d.Y >= 0 || o.Y < h) return false;
            float t = (o.Y - h) / -d.Y;
            if (t > maxDist) return false;
            tHit = t;
            return true;
        }
        const float step = 0.5f;
        float prevT = 0;
        float prevDiff = o.Y - TerrainHeight(o.X, o.Z);
        if (prevDiff < 0) return false;
        for (float t = step; t <= maxDist + step; t += step)
        {
            float tc = MathF.Min(t, maxDist);
            var p = o + d * tc;
            float diff = p.Y - TerrainHeight(p.X, p.Z);
            if (diff < 0)
            {
                float lo = prevT, hi = tc;
                for (int k = 0; k < 10; k++)
                {
                    float mid = (lo + hi) / 2;
                    var pm = o + d * mid;
                    if (pm.Y - TerrainHeight(pm.X, pm.Z) < 0) hi = mid; else lo = mid;
                }
                tHit = (lo + hi) / 2;
                return true;
            }
            prevT = tc;
            prevDiff = diff;
            if (tc >= maxDist) break;
        }
        return false;
    }

    private static bool RayShape(Vector3 o, Vector3 d, Shape s, out float t, out Vector3 n)
    {
        t = 0;
        n = Vector3.UnitY;
        if (s.Kind == ShapeKind.Sphere)
        {
            var m = o - s.Center;
            float b = Vector3.Dot(m, d);
            float c = m.LengthSquared() - s.Half.X * s.Half.X;
            if (c > 0 && b > 0) return false;
            float disc = b * b - c;
            if (disc < 0) return false;
            t = MathF.Max(0, -b - MathF.Sqrt(disc));
            n = Vector3.Normalize(o + d * t - s.Center);
            return true;
        }
        var lo = s.ToLocal(o);
        var ld = s.DirToLocal(d);
        if (s.Kind == ShapeKind.Hull)
        {
            var h = s.Hull!;
            float tmin = 0, tmax = float.MaxValue;
            int face = -1;
            for (int i = 0; i < h.Normals.Length; i++)
            {
                float denom = Vector3.Dot(h.Normals[i], ld);
                float dist = Vector3.Dot(h.Normals[i], lo) - h.Offsets[i];
                if (MathF.Abs(denom) < 1e-9f)
                {
                    if (dist > 0) return false;
                    continue;
                }
                float tc = -dist / denom;
                if (denom < 0) { if (tc > tmin) { tmin = tc; face = i; } }
                else if (tc < tmax) tmax = tc;
                if (tmin > tmax) return false;
            }
            t = tmin;
            n = s.DirToWorld(face >= 0 ? h.Normals[face] : -ld);
            return true;
        }
        if (s.Kind == ShapeKind.Box)
        {
            float tmin = 0, tmax = float.MaxValue;
            int axis = -1;
            float sign = 1;
            for (int i = 0; i < 3; i++)
            {
                float oi = i == 0 ? lo.X : i == 1 ? lo.Y : lo.Z;
                float di = i == 0 ? ld.X : i == 1 ? ld.Y : ld.Z;
                float hi = i == 0 ? s.Half.X : i == 1 ? s.Half.Y : s.Half.Z;
                if (MathF.Abs(di) < 1e-8f)
                {
                    if (oi < -hi || oi > hi) return false;
                    continue;
                }
                float inv = 1f / di;
                float t1 = (-hi - oi) * inv, t2 = (hi - oi) * inv;
                float sg = -1;
                if (t1 > t2) { (t1, t2) = (t2, t1); sg = 1; }
                if (t1 > tmin) { tmin = t1; axis = i; sign = sg; }
                tmax = MathF.Min(tmax, t2);
                if (tmin > tmax) return false;
            }
            t = tmin;
            var ln = axis switch { 0 => new Vector3(sign, 0, 0), 1 => new Vector3(0, sign, 0), 2 => new Vector3(0, 0, sign), _ => -ld };
            n = s.DirToWorld(ln);
            return true;
        }
        // silindir
        float rad = s.Half.X, hh = s.Half.Y;
        float best = float.MaxValue;
        Vector3 bn = Vector3.UnitY;
        // yan yuzey
        float a = ld.X * ld.X + ld.Z * ld.Z;
        if (a > 1e-10f)
        {
            float b = 2 * (lo.X * ld.X + lo.Z * ld.Z);
            float c = lo.X * lo.X + lo.Z * lo.Z - rad * rad;
            float disc = b * b - 4 * a * c;
            if (disc >= 0)
            {
                float sq = MathF.Sqrt(disc);
                foreach (float tc in new[] { (-b - sq) / (2 * a), (-b + sq) / (2 * a) })
                {
                    if (tc < 0) continue;
                    float y = lo.Y + ld.Y * tc;
                    if (y >= -hh && y <= hh && tc < best)
                    {
                        best = tc;
                        var p = lo + ld * tc;
                        bn = Vector3.Normalize(new Vector3(p.X, 0, p.Z));
                    }
                }
            }
        }
        // kapaklar
        if (MathF.Abs(ld.Y) > 1e-8f)
        {
            foreach (float yy in new[] { hh, -hh })
            {
                float tc = (yy - lo.Y) / ld.Y;
                if (tc < 0 || tc >= best) continue;
                var p = lo + ld * tc;
                if (p.X * p.X + p.Z * p.Z <= rad * rad)
                {
                    best = tc;
                    bn = new Vector3(0, MathF.Sign(yy), 0);
                }
            }
        }
        // icerden baslayan isin
        if (lo.X * lo.X + lo.Z * lo.Z <= rad * rad && lo.Y >= -hh && lo.Y <= hh) { best = 0; bn = -ld; }
        if (best == float.MaxValue) return false;
        t = best;
        n = s.DirToWorld(bn);
        return true;
    }

    /// <summary>Asagi dogru isin: zemin yuksekligi (yapilar dahil).</summary>
    public bool GroundAt(float x, float z, float fromY, out float y, out Shape? shape, Shape? ignore = null)
    {
        y = 0;
        shape = null;
        if (!Raycast(new Vector3(x, fromY, z), -Vector3.UnitY, fromY + 60, out var hit, ignore)) return false;
        y = hit.Point.Y;
        shape = hit.Shape;
        return true;
    }

    public bool PointInside(Vector3 p)
    {
        foreach (var s in Query(p - Vector3.One * 0.01f, p + Vector3.One * 0.01f))
        {
            var l = s.ToLocal(p);
            switch (s.Kind)
            {
                case ShapeKind.Sphere: if (l.LengthSquared() < s.Half.X * s.Half.X) return true; break;
                case ShapeKind.Box: if (MathF.Abs(l.X) < s.Half.X && MathF.Abs(l.Y) < s.Half.Y && MathF.Abs(l.Z) < s.Half.Z) return true; break;
                case ShapeKind.Hull: if (s.Hull!.MaxSeparation(l, out _) < 0) return true; break;
                default: if (l.X * l.X + l.Z * l.Z < s.Half.X * s.Half.X && MathF.Abs(l.Y) < s.Half.Y) return true; break;
            }
        }
        return p.Y < TerrainHeight(p.X, p.Z) - 0.05f;
    }
}
