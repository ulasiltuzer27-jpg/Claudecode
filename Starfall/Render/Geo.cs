using System.Numerics;
using Starfall.Core;

namespace Starfall.Render;

/// <summary>
/// Kodla modelleme icin ucgen listesi (indekssiz): konum + normal + renk.
/// JS surumundeki geom.js'in karsiligi; ilkel uretecler three.js'in
/// geometri ureteclerini birebir izler (ayni bolumleme, ayni sarim yonu).
/// </summary>
public sealed class Geo
{
    public readonly List<Vector3> P = new();
    public readonly List<Vector3> N = new();
    public readonly List<Vector3> C = new();

    public int Count => P.Count;

    public Geo Clone()
    {
        var g = new Geo();
        g.P.AddRange(P);
        g.N.AddRange(N);
        g.C.AddRange(C);
        return g;
    }

    // ------------------------------------------------------------------
    // donusumler
    // ------------------------------------------------------------------
    public Geo Transform(Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var inv);
        var nm = Matrix4x4.Transpose(inv);
        for (int i = 0; i < P.Count; i++)
        {
            P[i] = Vector3.Transform(P[i], m);
            var n = Vector3.TransformNormal(N[i], nm);
            float l = n.Length();
            N[i] = l > 1e-8f ? n / l : Vector3.UnitY;
        }
        // aynalama sarim yonunu ters cevirir: ucgenleri yeniden sirala (arka yuz ayiklamasi bozulmasin)
        if (m.GetDeterminant() < 0)
        {
            for (int i = 0; i + 2 < P.Count; i += 3)
            {
                (P[i + 1], P[i + 2]) = (P[i + 2], P[i + 1]);
                (N[i + 1], N[i + 2]) = (N[i + 2], N[i + 1]);
                (C[i + 1], C[i + 2]) = (C[i + 2], C[i + 1]);
            }
        }
        return this;
    }

    public Geo Translate(float x, float y, float z) => Transform(Matrix4x4.CreateTranslation(x, y, z));
    public Geo Scale(float x, float y, float z) => Transform(Matrix4x4.CreateScale(x, y, z));
    public Geo RotateX(float a) => Transform(Matrix4x4.CreateRotationX(a));
    public Geo RotateY(float a) => Transform(Matrix4x4.CreateRotationY(a));
    public Geo RotateZ(float a) => Transform(Matrix4x4.CreateRotationZ(a));

    public Geo Color(Vector3 c)
    {
        C.Clear();
        for (int i = 0; i < P.Count; i++) C.Add(c);
        return this;
    }

    public Geo Color(string hex) => Color(MathX.Hex(hex));

    /// <summary>Duz golgeleme: her ucgenin normali yuz normali olur.</summary>
    public Geo FlatNormals()
    {
        for (int i = 0; i + 2 < P.Count; i += 3)
        {
            var n = Vector3.Cross(P[i + 1] - P[i], P[i + 2] - P[i]);
            float l = n.Length();
            n = l > 1e-10f ? n / l : Vector3.UnitY;
            N[i] = N[i + 1] = N[i + 2] = n;
        }
        return this;
    }

    /// <summary>Yumusak golgeleme: ayni konumdaki koselerin yuz normallerini ortala.</summary>
    public Geo SmoothNormals()
    {
        var acc = new Dictionary<(int, int, int), Vector3>();
        (int, int, int) Key(Vector3 p) => ((int)MathF.Round(p.X * 1e4f), (int)MathF.Round(p.Y * 1e4f), (int)MathF.Round(p.Z * 1e4f));
        for (int i = 0; i + 2 < P.Count; i += 3)
        {
            var fn = Vector3.Cross(P[i + 1] - P[i], P[i + 2] - P[i]);
            for (int j = 0; j < 3; j++)
            {
                var k = Key(P[i + j]);
                acc[k] = acc.TryGetValue(k, out var v) ? v + fn : fn;
            }
        }
        for (int i = 0; i < P.Count; i++)
        {
            var n = acc[Key(P[i])];
            float l = n.Length();
            N[i] = l > 1e-12f ? n / l : Vector3.UnitY;
        }
        return this;
    }

    /// <summary>Sinir kutusunu orijine ortala (three.js geometry.center()).</summary>
    public Geo Center()
    {
        var (mn, mx) = Bounds();
        var c = (mn + mx) * 0.5f;
        return Translate(-c.X, -c.Y, -c.Z);
    }

    /// <summary>Ayni konumdaki koseleri ayni miktarda kaydirir (yuzeyde catlak olusmaz).</summary>
    public Geo Jitter(float amount, uint seed)
    {
        var rng = new Rng(seed);
        var map = new Dictionary<(int, int, int), Vector3>();
        for (int i = 0; i < P.Count; i++)
        {
            var p = P[i];
            var key = ((int)MathF.Round(p.X * 1000f), (int)MathF.Round(p.Y * 1000f), (int)MathF.Round(p.Z * 1000f));
            if (!map.TryGetValue(key, out var d))
            {
                d = new Vector3((rng.Next() - 0.5f) * amount, (rng.Next() - 0.5f) * amount, (rng.Next() - 0.5f) * amount);
                map[key] = d;
            }
            P[i] = p + d;
        }
        return FlatNormals();
    }

    public Geo GradientY(string bottom, string top, float y0, float y1)
    {
        var a = MathX.Hex(bottom);
        var b = MathX.Hex(top);
        for (int i = 0; i < P.Count; i++)
        {
            float t = MathX.Clamp((P[i].Y - y0) / (y1 - y0), 0f, 1f);
            C[i] = Vector3.Lerp(a, b, t);
        }
        return this;
    }

    /// <summary>Ucgen basina hafif ton farki: low-poly yuzeylerde firca darbesi etkisi.</summary>
    public Geo FaceTint(float amount, uint seed)
    {
        var rng = new Rng(seed);
        for (int i = 0; i + 2 < C.Count; i += 3)
        {
            float k = 1f + (rng.Next() - 0.5f) * amount;
            C[i] *= k; C[i + 1] *= k; C[i + 2] *= k;
        }
        return this;
    }

    public Geo Append(Geo o)
    {
        P.AddRange(o.P);
        N.AddRange(o.N);
        C.AddRange(o.C);
        return this;
    }

    public static Geo Merge(params Geo[] parts)
    {
        var g = new Geo();
        foreach (var p in parts) g.Append(p);
        return g;
    }

    public static Geo Merge(IEnumerable<Geo> parts)
    {
        var g = new Geo();
        foreach (var p in parts) g.Append(p);
        return g;
    }

    public (Vector3 min, Vector3 max) Bounds()
    {
        var mn = new Vector3(float.MaxValue);
        var mx = new Vector3(float.MinValue);
        foreach (var p in P) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
        return (mn, mx);
    }

    // ------------------------------------------------------------------
    // parca yardimcilari (geom.js: part/box/sphere/cyl/cone/prism)
    // ------------------------------------------------------------------
    public static Geo Part(Geo g, string color, Vector3? pos = null, Vector3? rot = null, Vector3? scale = null)
    {
        g.Color(color);
        var r = rot ?? Vector3.Zero;
        var m = MathX.Compose(pos ?? Vector3.Zero, MathX.EulerXYZ(r.X, r.Y, r.Z), scale ?? Vector3.One);
        return g.Transform(m);
    }

    public static Vector3 V(float x, float y, float z) => new(x, y, z);

    public static Geo Box(string c, float w, float h, float d, Vector3? pos = null, Vector3? rot = null, Vector3? scale = null) =>
        Part(BoxGeo(w, h, d), c, pos, rot, scale);

    public static Geo Sphere(string c, float r, Vector3? pos = null, Vector3? scale = null, int ws = 12, int hs = 8) =>
        Part(SphereGeo(r, ws, hs), c, pos, null, scale);

    public static Geo Cyl(string c, float rTop, float rBottom, float h, Vector3? pos = null, Vector3? rot = null, int seg = 8) =>
        Part(CylinderGeo(rTop, rBottom, h, seg), c, pos, rot);

    public static Geo Cone(string c, float r, float h, Vector3? pos = null, Vector3? rot = null, int seg = 8, Vector3? scale = null) =>
        Part(CylinderGeo(0, r, h, seg), c, pos, rot, scale);

    public static Geo Blob(string c, float sx, float sy, float sz, int detail = 1) =>
        Part(Icosahedron(1, detail), c, null, null, new Vector3(sx, sy, sz));

    /// <summary>Ucgen prizma (cati gibi): tabani y=0, genislik x, derinlik z.</summary>
    public static Geo Prism(string c, float w, float h, float d, Vector3? pos = null, Vector3? rot = null)
    {
        var shape = new List<Vector2> { new(-w / 2, 0), new(w / 2, 0), new(0, h) };
        var g = Extrude(shape, d).Translate(0, 0, -d / 2);
        return Part(g, c, pos, rot);
    }

    // ------------------------------------------------------------------
    // ilkel uretecler (three.js ile ayni)
    // ------------------------------------------------------------------
    private static Geo FromIndexed(List<Vector3> pos, List<Vector3> nrm, List<int> idx)
    {
        var g = new Geo();
        foreach (int i in idx)
        {
            g.P.Add(pos[i]);
            g.N.Add(nrm[i]);
            g.C.Add(Vector3.One);
        }
        return g;
    }

    public static Geo BoxGeo(float w, float h, float d, int ws = 1, int hs = 1, int ds = 1)
    {
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var idx = new List<int>();
        void Plane(int u, int v, int wAxis, float udir, float vdir, float width, float height, float depth, int gx, int gy)
        {
            float sw = width / gx, sh = height / gy;
            float hw = width / 2, hh = height / 2, hd = depth / 2;
            int start = pos.Count;
            for (int iy = 0; iy <= gy; iy++)
            {
                float y = iy * sh - hh;
                for (int ix = 0; ix <= gx; ix++)
                {
                    float x = ix * sw - hw;
                    var vec = new float[3];
                    vec[u] = x * udir;
                    vec[v] = y * vdir;
                    vec[wAxis] = hd;
                    pos.Add(new Vector3(vec[0], vec[1], vec[2]));
                    var n = new float[3];
                    n[wAxis] = depth > 0 ? 1 : -1;
                    nrm.Add(new Vector3(n[0], n[1], n[2]));
                }
            }
            for (int iy = 0; iy < gy; iy++)
            {
                for (int ix = 0; ix < gx; ix++)
                {
                    int a = start + ix + (gx + 1) * iy;
                    int b = start + ix + (gx + 1) * (iy + 1);
                    int c = start + (ix + 1) + (gx + 1) * (iy + 1);
                    int dd = start + (ix + 1) + (gx + 1) * iy;
                    idx.AddRange(new[] { a, b, dd, b, c, dd });
                }
            }
        }
        // three.js BoxGeometry: px, nx, py, ny, pz, nz (x=0, y=1, z=2)
        Plane(2, 1, 0, -1, -1, d, h, w, ds, hs);
        Plane(2, 1, 0, 1, -1, d, h, -w, ds, hs);
        Plane(0, 2, 1, 1, 1, w, d, h, ws, ds);
        Plane(0, 2, 1, 1, -1, w, d, -h, ws, ds);
        Plane(0, 1, 2, 1, -1, w, h, d, ws, hs);
        Plane(0, 1, 2, -1, -1, w, h, -d, ws, hs);
        return FromIndexed(pos, nrm, idx);
    }

    public static Geo SphereGeo(float r, int ws = 12, int hs = 8, float phiStart = 0, float phiLen = MathX.TwoPi, float thetaStart = 0, float thetaLen = MathX.Pi)
    {
        ws = Math.Max(3, ws);
        hs = Math.Max(2, hs);
        float thetaEnd = MathF.Min(thetaStart + thetaLen, MathX.Pi);
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var grid = new List<int[]>();
        for (int iy = 0; iy <= hs; iy++)
        {
            var row = new int[ws + 1];
            float v = iy / (float)hs;
            for (int ix = 0; ix <= ws; ix++)
            {
                float u = ix / (float)ws;
                var p = new Vector3(
                    -r * MathF.Cos(phiStart + u * phiLen) * MathF.Sin(thetaStart + v * thetaLen),
                    r * MathF.Cos(thetaStart + v * thetaLen),
                    r * MathF.Sin(phiStart + u * phiLen) * MathF.Sin(thetaStart + v * thetaLen));
                pos.Add(p);
                nrm.Add(p.LengthSquared() > 0 ? Vector3.Normalize(p) : Vector3.UnitY);
                row[ix] = pos.Count - 1;
            }
            grid.Add(row);
        }
        var idx = new List<int>();
        for (int iy = 0; iy < hs; iy++)
        {
            for (int ix = 0; ix < ws; ix++)
            {
                int a = grid[iy][ix + 1], b = grid[iy][ix], c = grid[iy + 1][ix], d = grid[iy + 1][ix + 1];
                if (iy != 0 || thetaStart > 0) idx.AddRange(new[] { a, b, d });
                if (iy != hs - 1 || thetaEnd < MathX.Pi) idx.AddRange(new[] { b, c, d });
            }
        }
        return FromIndexed(pos, nrm, idx);
    }

    public static Geo CylinderGeo(float rTop, float rBottom, float h, int radial = 8, int heightSeg = 1, bool open = false, float thetaStart = 0, float thetaLen = MathX.TwoPi)
    {
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var idx = new List<int>();
        float half = h / 2;
        float slope = (rBottom - rTop) / h;
        var ring = new List<int[]>();
        for (int y = 0; y <= heightSeg; y++)
        {
            var row = new int[radial + 1];
            float v = y / (float)heightSeg;
            float radius = v * (rBottom - rTop) + rTop;
            for (int x = 0; x <= radial; x++)
            {
                float u = x / (float)radial;
                float th = u * thetaLen + thetaStart;
                float s = MathF.Sin(th), c = MathF.Cos(th);
                pos.Add(new Vector3(radius * s, -v * h + half, radius * c));
                nrm.Add(Vector3.Normalize(new Vector3(s, slope, c)));
                row[x] = pos.Count - 1;
            }
            ring.Add(row);
        }
        for (int x = 0; x < radial; x++)
        {
            for (int y = 0; y < heightSeg; y++)
            {
                int a = ring[y][x], b = ring[y + 1][x], c = ring[y + 1][x + 1], d = ring[y][x + 1];
                idx.AddRange(new[] { a, b, d, b, c, d });
            }
        }
        void Cap(bool top)
        {
            float radius = top ? rTop : rBottom;
            if (radius <= 0) return;
            float sign = top ? 1 : -1;
            int centerStart = pos.Count;
            for (int x = 1; x <= radial; x++)
            {
                pos.Add(new Vector3(0, half * sign, 0));
                nrm.Add(new Vector3(0, sign, 0));
            }
            int centerEnd = pos.Count;
            for (int x = 0; x <= radial; x++)
            {
                float th = x / (float)radial * thetaLen + thetaStart;
                pos.Add(new Vector3(radius * MathF.Sin(th), half * sign, radius * MathF.Cos(th)));
                nrm.Add(new Vector3(0, sign, 0));
            }
            for (int x = 0; x < radial; x++)
            {
                int c = centerStart + x, i = centerEnd + x;
                if (top) idx.AddRange(new[] { i, i + 1, c });
                else idx.AddRange(new[] { i + 1, i, c });
            }
        }
        if (!open)
        {
            Cap(true);
            Cap(false);
        }
        return FromIndexed(pos, nrm, idx);
    }

    public static Geo TorusGeo(float radius, float tube, int radialSeg = 8, int tubularSeg = 16, float arc = MathX.TwoPi)
    {
        var pos = new List<Vector3>();
        var nrm = new List<Vector3>();
        var idx = new List<int>();
        for (int j = 0; j <= radialSeg; j++)
        {
            for (int i = 0; i <= tubularSeg; i++)
            {
                float u = i / (float)tubularSeg * arc;
                float v = j / (float)radialSeg * MathX.TwoPi;
                var p = new Vector3((radius + tube * MathF.Cos(v)) * MathF.Cos(u), (radius + tube * MathF.Cos(v)) * MathF.Sin(u), tube * MathF.Sin(v));
                var center = new Vector3(radius * MathF.Cos(u), radius * MathF.Sin(u), 0);
                pos.Add(p);
                nrm.Add(Vector3.Normalize(p - center));
            }
        }
        for (int j = 1; j <= radialSeg; j++)
        {
            for (int i = 1; i <= tubularSeg; i++)
            {
                int a = (tubularSeg + 1) * j + i - 1;
                int b = (tubularSeg + 1) * (j - 1) + i - 1;
                int c = (tubularSeg + 1) * (j - 1) + i;
                int d = (tubularSeg + 1) * j + i;
                idx.AddRange(new[] { a, b, d, b, c, d });
            }
        }
        return FromIndexed(pos, nrm, idx);
    }

    public static Geo CircleGeo(float r, int segments = 16, float thetaStart = 0, float thetaLen = MathX.TwoPi)
    {
        var pos = new List<Vector3> { Vector3.Zero };
        var nrm = new List<Vector3> { Vector3.UnitZ };
        var idx = new List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float th = thetaStart + s / (float)segments * thetaLen;
            pos.Add(new Vector3(r * MathF.Cos(th), r * MathF.Sin(th), 0));
            nrm.Add(Vector3.UnitZ);
        }
        for (int i = 1; i <= segments; i++) idx.AddRange(new[] { i, i + 1, 0 });
        return FromIndexed(pos, nrm, idx);
    }

    private static readonly float T = (1f + MathF.Sqrt(5f)) / 2f;

    public static Geo Icosahedron(float r, int detail = 0)
    {
        float t = T;
        float[] v = { -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, 0, 0, -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, t, 0, -1, t, 0, 1, -t, 0, -1, -t, 0, 1 };
        int[] i = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        return Polyhedron(v, i, r, detail);
    }

    public static Geo Octahedron(float r, int detail = 0)
    {
        float[] v = { 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 1, 0, 0, -1 };
        int[] i = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4, 1, 4, 2 };
        return Polyhedron(v, i, r, detail);
    }

    public static Geo Dodecahedron(float r, int detail = 0)
    {
        float t = T, q = 1f / T;
        float[] v =
        {
            -1, -1, -1, -1, -1, 1, -1, 1, -1, -1, 1, 1, 1, -1, -1, 1, -1, 1, 1, 1, -1, 1, 1, 1,
            0, -q, -t, 0, -q, t, 0, q, -t, 0, q, t,
            -q, -t, 0, -q, t, 0, q, -t, 0, q, t, 0,
            -t, 0, -q, t, 0, -q, -t, 0, q, t, 0, q,
        };
        int[] i =
        {
            3, 11, 7, 3, 7, 15, 3, 15, 13, 7, 19, 17, 7, 17, 6, 7, 6, 15, 17, 4, 8, 17, 8, 10, 17, 10, 6, 8, 0, 16, 8, 16, 2, 8, 2, 10,
            0, 12, 1, 0, 1, 18, 0, 18, 16, 6, 10, 2, 6, 2, 13, 6, 13, 15, 2, 16, 18, 2, 18, 3, 2, 3, 13, 18, 1, 9, 18, 9, 11, 18, 11, 3,
            4, 14, 12, 4, 12, 0, 4, 0, 8, 11, 9, 5, 11, 5, 19, 11, 19, 7, 19, 5, 14, 19, 14, 4, 19, 4, 17, 1, 12, 14, 1, 14, 5, 1, 5, 9,
        };
        return Polyhedron(v, i, r, detail);
    }

    private static Geo Polyhedron(float[] verts, int[] indices, float radius, int detail)
    {
        var tri = new List<Vector3>();
        Vector3 Vx(int k) => new(verts[k * 3], verts[k * 3 + 1], verts[k * 3 + 2]);
        for (int f = 0; f < indices.Length; f += 3)
        {
            var a = Vx(indices[f]);
            var b = Vx(indices[f + 1]);
            var c = Vx(indices[f + 2]);
            int cols = detail + 1;
            var vv = new List<Vector3[]>();
            for (int i = 0; i <= cols; i++)
            {
                var aj = Vector3.Lerp(a, c, i / (float)cols);
                var bj = Vector3.Lerp(b, c, i / (float)cols);
                int rows = cols - i;
                var row = new Vector3[rows + 1];
                for (int j = 0; j <= rows; j++)
                    row[j] = (j == 0 && i == cols) ? aj : Vector3.Lerp(aj, bj, rows == 0 ? 0 : j / (float)rows);
                vv.Add(row);
            }
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < 2 * (cols - i) - 1; j++)
                {
                    int k = j / 2;
                    if (j % 2 == 0)
                    {
                        tri.Add(vv[i][k + 1]); tri.Add(vv[i + 1][k]); tri.Add(vv[i][k]);
                    }
                    else
                    {
                        tri.Add(vv[i][k + 1]); tri.Add(vv[i + 1][k + 1]); tri.Add(vv[i + 1][k]);
                    }
                }
            }
        }
        var g = new Geo();
        foreach (var p in tri)
        {
            var n = Vector3.Normalize(p);
            g.P.Add(n * radius);
            g.N.Add(n);
            g.C.Add(Vector3.One);
        }
        if (detail == 0) g.FlatNormals();
        return g;
    }

    // ------------------------------------------------------------------
    // sekiller ve ekstruzyon
    // ------------------------------------------------------------------
    public sealed class Shape2D
    {
        public readonly List<Vector2> Pts = new();
        private Vector2 _cur;

        public Shape2D MoveTo(float x, float y) { _cur = new(x, y); Pts.Add(_cur); return this; }
        public Shape2D LineTo(float x, float y) { _cur = new(x, y); Pts.Add(_cur); return this; }

        public Shape2D QuadTo(float cx, float cy, float x, float y, int div = 12)
        {
            var p0 = _cur;
            var c = new Vector2(cx, cy);
            var p1 = new Vector2(x, y);
            for (int i = 1; i <= div; i++)
            {
                float t = i / (float)div;
                float u = 1 - t;
                Pts.Add(u * u * p0 + 2 * u * t * c + t * t * p1);
            }
            _cur = p1;
            return this;
        }

        public List<Vector2> Points()
        {
            var pts = new List<Vector2>(Pts);
            if (pts.Count > 1 && Vector2.DistanceSquared(pts[0], pts[^1]) < 1e-10f) pts.RemoveAt(pts.Count - 1);
            return pts;
        }
    }

    public static List<Vector2> StarShape(float outer, float inner, int points = 5)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < points * 2; i++)
        {
            float r = i % 2 == 0 ? outer : inner;
            float a = i / (float)(points * 2) * MathX.TwoPi + MathX.Pi / 2;
            pts.Add(new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r));
        }
        return pts;
    }

    private static float SignedArea(List<Vector2> p)
    {
        float a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var q = p[i];
            var r = p[(i + 1) % p.Count];
            a += q.X * r.Y - r.X * q.Y;
        }
        return a / 2;
    }

    /// <summary>Kulak kirpma ile cokgen ucgenleme (delik yok). CCW cikti.</summary>
    public static List<int> Triangulate(List<Vector2> poly)
    {
        var res = new List<int>();
        int n = poly.Count;
        if (n < 3) return res;
        var idx = Enumerable.Range(0, n).ToList();
        if (SignedArea(poly) < 0) idx.Reverse();
        bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            float d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            float d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                var a = poly[ia]; var b = poly[ib]; var c = poly[ic];
                float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (cross <= 1e-9f) continue;
                bool ear = true;
                foreach (int j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    if (Inside(poly[j], a, b, c)) { ear = false; break; }
                }
                if (!ear) continue;
                res.AddRange(new[] { ia, ib, ic });
                idx.RemoveAt(i);
                cut = true;
                break;
            }
            if (!cut) break;
        }
        if (idx.Count == 3) res.AddRange(new[] { idx[0], idx[1], idx[2] });
        return res;
    }

    /// <summary>
    /// Cokgeni z=0..depth arasinda ekstrude eder. bevel &gt; 0 ise on/arka yuzler
    /// bevel kadar disari tasar ve kenarlar pahli olur (three.js bevel'ine yakin).
    /// </summary>
    public static Geo Extrude(List<Vector2> shape, float depth, float bevelThickness = 0, float bevelSize = 0)
    {
        var poly = new List<Vector2>(shape);
        if (SignedArea(poly) < 0) poly.Reverse();
        var tris = Triangulate(poly);
        var g = new Geo();
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            g.P.Add(a); g.P.Add(b); g.P.Add(c);
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            g.N.Add(n); g.N.Add(n); g.N.Add(n);
            g.C.Add(Vector3.One); g.C.Add(Vector3.One); g.C.Add(Vector3.One);
        }
        // kenar halkalari: bevel varsa disa itilmis orta halka
        var expanded = bevelSize > 0 ? Offset(poly, bevelSize) : poly;
        float zBack = -bevelThickness, zFront = depth + bevelThickness;
        // kapaklar (orijinal cokgen, disa tasan z'lerde)
        for (int i = 0; i < tris.Count; i += 3)
        {
            var a = poly[tris[i]]; var b = poly[tris[i + 1]]; var c = poly[tris[i + 2]];
            Tri(new(a, zFront), new(b, zFront), new(c, zFront));
            Tri(new(c, zBack), new(b, zBack), new(a, zBack));
        }
        void Wall(List<Vector2> r0, float z0, List<Vector2> r1, float z1)
        {
            for (int i = 0; i < r0.Count; i++)
            {
                int j = (i + 1) % r0.Count;
                var a0 = new Vector3(r0[i], z0); var b0 = new Vector3(r0[j], z0);
                var a1 = new Vector3(r1[i], z1); var b1 = new Vector3(r1[j], z1);
                Tri(a0, b0, b1);
                Tri(a0, b1, a1);
            }
        }
        if (bevelThickness > 0 || bevelSize > 0)
        {
            Wall(poly, zBack, expanded, 0);
            Wall(expanded, 0, expanded, depth);
            Wall(expanded, depth, poly, zFront);
        }
        else
        {
            Wall(poly, 0, poly, depth);
        }
        return g;
    }

    private static List<Vector2> Offset(List<Vector2> poly, float d)
    {
        var res = new List<Vector2>();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var prev = poly[(i + n - 1) % n];
            var cur = poly[i];
            var next = poly[(i + 1) % n];
            var e0 = Vector2.Normalize(cur - prev);
            var e1 = Vector2.Normalize(next - cur);
            var n0 = new Vector2(e0.Y, -e0.X);
            var n1 = new Vector2(e1.Y, -e1.X);
            var m = n0 + n1;
            float ml = m.Length();
            if (ml < 1e-5f) { res.Add(cur + n0 * d); continue; }
            m /= ml;
            float k = MathF.Min(2.5f, 1f / MathF.Max(0.2f, Vector2.Dot(m, n0)));
            res.Add(cur + m * d * k);
        }
        return res;
    }

    /// <summary>Catmull-Rom egrisi boyunca tup (kopru halatlari).</summary>
    public static Geo Tube(List<Vector3> controlPts, int segments, float radius, int radial = 4)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i <= segments; i++) pts.Add(CatmullRom(controlPts, i / (float)segments));
        var g = new Geo();
        var prevN = Vector3.UnitY;
        var rings = new List<Vector3[]>();
        var ringN = new List<Vector3[]>();
        for (int i = 0; i < pts.Count; i++)
        {
            var tan = Vector3.Normalize(pts[Math.Min(i + 1, pts.Count - 1)] - pts[Math.Max(i - 1, 0)]);
            var side = Vector3.Cross(tan, prevN);
            if (side.LengthSquared() < 1e-6f) side = Vector3.Cross(tan, Vector3.UnitX);
            side = Vector3.Normalize(side);
            var up = Vector3.Normalize(Vector3.Cross(side, tan));
            prevN = up;
            var ring = new Vector3[radial + 1];
            var rn = new Vector3[radial + 1];
            for (int k = 0; k <= radial; k++)
            {
                float a = k / (float)radial * MathX.TwoPi;
                var n = up * MathF.Cos(a) + side * MathF.Sin(a);
                ring[k] = pts[i] + n * radius;
                rn[k] = n;
            }
            rings.Add(ring);
            ringN.Add(rn);
        }
        for (int i = 0; i < rings.Count - 1; i++)
        {
            for (int k = 0; k < radial; k++)
            {
                var a = rings[i][k]; var b = rings[i + 1][k]; var c = rings[i + 1][k + 1]; var d = rings[i][k + 1];
                var na = ringN[i][k]; var nb = ringN[i + 1][k]; var nc = ringN[i + 1][k + 1]; var nd = ringN[i][k + 1];
                g.P.AddRange(new[] { a, b, d, b, c, d });
                g.N.AddRange(new[] { na, nb, nd, nb, nc, nd });
                for (int z = 0; z < 6; z++) g.C.Add(Vector3.One);
            }
        }
        return g;
    }

    public static Vector3 CatmullRom(List<Vector3> p, float t)
    {
        int n = p.Count;
        float f = t * (n - 1);
        int i = Math.Min((int)MathF.Floor(f), n - 2);
        float u = f - i;
        var p0 = p[Math.Max(i - 1, 0)];
        var p1 = p[i];
        var p2 = p[i + 1];
        var p3 = p[Math.Min(i + 2, n - 1)];
        float u2 = u * u, u3 = u2 * u;
        return 0.5f * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2 + (-p0 + 3 * p1 - 3 * p2 + p3) * u3);
    }
}
