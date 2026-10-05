using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Gelismis ilkeller: pahli kutu, kapsul, yol boyunca boru, yay, ekstruzyon
/// ve kesitler arasi gecis (loft). Kutu/silindirden olusan modellerin
/// "oyuncak" gorunumunu kiran sey yumusak kenarlar; isik kenarlarda
/// parlayabilsin diye bu ilkeller yumusak normal uretir.
/// </summary>
public static partial class Shapes
{
    // ═══════════════════════════════════════════════════════════════════
    // Pahli kutu
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kenarlari <paramref name="radius"/> yaricapla yuvarlatilmis kutu,
    /// merkezi orijinde. Duz yuzler tek dortgen; yalnizca kenar ve koseler
    /// <paramref name="segments"/> adimla bolunur.
    /// </summary>
    public static void RoundedBox(MeshData m, in Matrix4x4 xf, Vector3 size, float radius, int segments, Color color, float uvScale = 1f, Faces faces = Faces.All)
    {
        var h = size * 0.5f;
        var r = MathF.Min(radius, MathF.Min(h.X, MathF.Min(h.Y, h.Z)) * 0.999f);
        if (r <= 1e-4f || segments <= 0)
        {
            Box(m, xf, size, color, uvScale, faces);
            return;
        }

        var core = h - new Vector3(r);
        Matrix4x4.Invert(xf, out var inv);
        var nxf = Matrix4x4.Transpose(inv);

        // Bir eksen boyunca ornek konumlari: [-h .. -core] kenar yayi, [core .. h] kenar yayi.
        float[] Samples(float half, float c)
        {
            var list = new float[(segments + 1) * 2];
            for (var i = 0; i <= segments; i++)
            {
                var a = (1f - i / (float)segments) * MathF.PI / 2;
                list[i] = -c - MathF.Sin(a) * (half - c);
                list[segments + 1 + i] = c + MathF.Sin(i / (float)segments * MathF.PI / 2) * (half - c);
            }

            return list;
        }

        foreach (var (n, u, v, f) in BoxFaces)
        {
            if ((faces & f) == 0)
            {
                continue;
            }

            var hu = MathF.Abs(Vector3.Dot(u, h));
            var hv = MathF.Abs(Vector3.Dot(v, h));
            var hn = MathF.Abs(Vector3.Dot(n, h));
            var cu = MathF.Abs(Vector3.Dot(u, core));
            var cv = MathF.Abs(Vector3.Dot(v, core));
            var su = Samples(hu, cu);
            var sv = Samples(hv, cv);
            var wu = SafeNormalize(Vector3.TransformNormal(u, xf));
            var wv = SafeNormalize(Vector3.TransformNormal(v, xf));
            var start = m.VertexCount;
            foreach (var y in sv)
            {
                foreach (var x in su)
                {
                    // Duzlemdeki nokta -> cekirdek kutuya kirpilmis nokta + yaricap yonu
                    var p = n * hn + u * x + v * y;
                    var c = Vector3.Clamp(p, -core, core);
                    var d = p - c;
                    var dir = d.LengthSquared() > 1e-12f ? Vector3.Normalize(d) : n;
                    var pos = c + dir * r;
                    var wp = Vector3.Transform(pos, xf);
                    m.AddVertex(wp, SafeNormalize(Vector3.TransformNormal(dir, nxf)),
                        new Vector2(Vector3.Dot(wp, wu) * uvScale, -Vector3.Dot(wp, wv) * uvScale), color);
                }
            }

            var cols = su.Length;
            for (var j = 0; j < sv.Length - 1; j++)
            {
                for (var i = 0; i < cols - 1; i++)
                {
                    var a = start + j * cols + i;
                    m.AddQuad(a, a + 1, a + cols + 1, a + cols);
                }
            }
        }
    }

    /// <summary>Tabani y=0'da duran pahli kutu.</summary>
    public static void RoundedBoxOnGround(MeshData m, in Matrix4x4 xf, Vector3 size, float radius, int segments, Color color, float uvScale = 1f)
    {
        RoundedBox(m, Matrix4x4.CreateTranslation(0, size.Y * 0.5f, 0) * xf, size, radius, segments, color, uvScale);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Kapsul
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Y ekseni boyunca kapsul: alt ucu y=0, ust ucu y=<paramref name="height"/>.
    /// Yukseklik 2r'den kucukse kure olur.
    /// </summary>
    public static void Capsule(MeshData m, in Matrix4x4 xf, float radius, float height, int rings, int segments, Color color, float uvScale = 1f)
    {
        var r = MathF.Min(radius, height * 0.5f);
        var profile = new List<Vector2>();
        var half = Math.Max(2, rings / 2);
        for (var i = 0; i <= half; i++)
        {
            var a = -MathF.PI / 2 + i / (float)half * MathF.PI / 2;
            profile.Add(new Vector2(MathF.Cos(a) * r, r + MathF.Sin(a) * r));
        }

        for (var i = 0; i <= half; i++)
        {
            var a = i / (float)half * MathF.PI / 2;
            profile.Add(new Vector2(MathF.Cos(a) * r, height - r + MathF.Sin(a) * r));
        }

        Lathe(m, xf, profile, segments, color, uvScale, smoothProfile: true);
    }

    /// <summary>Iki nokta arasinda kapsul (uzuv, sap, cubuk).</summary>
    public static void CapsuleBetween(MeshData m, Vector3 from, Vector3 to, float radius, int rings, int segments, Color color, float uvScale = 1f)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 1e-5f)
        {
            Sphere(m, Matrix4x4.CreateTranslation(from), radius, rings, segments, color, uvScale);
            return;
        }

        dir /= len;
        var basis = BasisY(dir);
        // Kapsulun uclari tam noktalarda olsun diye yaricap kadar disari tasar.
        var xf = Matrix4x4.CreateTranslation(0, -radius, 0) * basis * Matrix4x4.CreateTranslation(from);
        Capsule(m, xf, radius, len + radius * 2, rings, segments, color, uvScale);
    }

    /// <summary>Y eksenini verilen yone ceviren dondurme matrisi.</summary>
    public static Matrix4x4 BasisY(Vector3 dir)
    {
        dir = SafeNormalize(dir);
        var helper = MathF.Abs(dir.Z) < 0.95f ? Vector3.UnitZ : Vector3.UnitX;
        var x = SafeNormalize(Vector3.Cross(dir, helper));
        var z = Vector3.Cross(x, dir);
        return new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            dir.X, dir.Y, dir.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Boru (yol boyunca supurme)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nokta dizisi boyunca dairesel kesitli boru: hortum, kulp, kuyruk,
    /// biyik, kivrik demir. Kesitler paralel tasima ile yonlenir (burulma
    /// olmaz). <paramref name="radii"/> verilirse her noktada farkli yaricap
    /// (sivrilen uclar).
    /// </summary>
    public static void Tube(MeshData m, IReadOnlyList<Vector3> points, float radius, int sides, Color color,
        bool capStart = true, bool capEnd = true, IReadOnlyList<float>? radii = null, float uvScale = 1f)
    {
        if (points.Count < 2)
        {
            return;
        }

        var n = points.Count;
        var tangents = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            var a = points[Math.Max(0, i - 1)];
            var b = points[Math.Min(n - 1, i + 1)];
            tangents[i] = SafeNormalize(b - a);
        }

        // Ilk kesit icin herhangi bir dik vektor, sonra paralel tasima
        var helper = MathF.Abs(tangents[0].Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        var normal = SafeNormalize(Vector3.Cross(Vector3.Cross(tangents[0], helper), tangents[0]));
        var start = m.VertexCount;
        var dist = 0f;
        for (var i = 0; i < n; i++)
        {
            if (i > 0)
            {
                dist += Vector3.Distance(points[i], points[i - 1]);
                var axis = Vector3.Cross(tangents[i - 1], tangents[i]);
                var s = axis.Length();
                if (s > 1e-6f)
                {
                    var ang = MathF.Atan2(s, Vector3.Dot(tangents[i - 1], tangents[i]));
                    normal = Vector3.Transform(normal, Quaternion.CreateFromAxisAngle(axis / s, ang));
                }
            }

            var binormal = Vector3.Cross(tangents[i], normal);
            var ri = radii is not null ? radii[Math.Min(i, radii.Count - 1)] : radius;
            for (var k = 0; k <= sides; k++)
            {
                var a = k / (float)sides * MathF.Tau;
                var dir = normal * MathF.Cos(a) + binormal * MathF.Sin(a);
                m.AddVertex(points[i] + dir * ri, dir, new Vector2(k / (float)sides * MathF.Tau * ri * uvScale, dist * uvScale), color);
            }
        }

        var stride = sides + 1;
        for (var i = 0; i < n - 1; i++)
        {
            for (var k = 0; k < sides; k++)
            {
                var a = start + i * stride + k;
                var b = a + stride;
                // Kesit acisi artarken (k) ve yol boyunca (i) disa bakan sarim
                m.AddQuad(a, a + 1, b + 1, b);
            }
        }

        if (capStart)
        {
            Cap(m, points[0], -tangents[0], start, sides, color);
        }

        if (capEnd)
        {
            Cap(m, points[n - 1], tangents[n - 1], start + (n - 1) * stride, sides, color);
        }
    }

    private static void Cap(MeshData m, Vector3 center, Vector3 normal, int ringStart, int sides, Color color)
    {
        var ci = m.AddVertex(center, normal, Vector2.Zero, color);
        var first = m.VertexCount;
        for (var k = 0; k <= sides; k++)
        {
            m.AddVertex(m.Positions[ringStart + k], normal, Vector2.Zero, color);
        }

        for (var k = 0; k < sides; k++)
        {
            var a = first + k;
            var b = first + k + 1;
            // Normal yonune gore sarim
            var cross = Vector3.Cross(m.Positions[a] - center, m.Positions[b] - center);
            if (Vector3.Dot(cross, normal) >= 0)
            {
                m.AddTriangle(ci, a, b);
            }
            else
            {
                m.AddTriangle(ci, b, a);
            }
        }
    }

    /// <summary>
    /// Yay (kismi halka): XZ duzleminde, Y ekseni etrafinda
    /// <paramref name="startAngle"/>'dan <paramref name="sweep"/> kadar.
    /// Kulp, kemer, cerceve kavsi.
    /// </summary>
    public static void Arc(MeshData m, in Matrix4x4 xf, float majorRadius, float minorRadius, float startAngle, float sweep,
        int majorSegs, int minorSegs, Color color, bool caps = true)
    {
        var pts = new List<Vector3>(majorSegs + 1);
        for (var i = 0; i <= majorSegs; i++)
        {
            var a = startAngle + i / (float)majorSegs * sweep;
            pts.Add(Vector3.Transform(new Vector3(MathF.Cos(a) * majorRadius, 0, MathF.Sin(a) * majorRadius), xf));
        }

        var scale = Vector3.TransformNormal(Vector3.UnitX, xf).Length();
        Tube(m, pts, minorRadius * scale, minorSegs, color, caps, caps);
    }

    /// <summary>Uc noktadan duzgun egri (kuadratik Bezier) noktalari: boru icin yumusak kivrim.</summary>
    public static List<Vector3> Curve(Vector3 a, Vector3 control, Vector3 b, int steps)
    {
        var list = new List<Vector3>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var u = 1 - t;
            list.Add(u * u * a + 2 * u * t * control + t * t * b);
        }

        return list;
    }

    /// <summary>Kubik Bezier: iki kontrol noktali kivrim (hortum, kuyruk).</summary>
    public static List<Vector3> Curve(Vector3 a, Vector3 c1, Vector3 c2, Vector3 b, int steps)
    {
        var list = new List<Vector3>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var u = 1 - t;
            list.Add(u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b);
        }

        return list;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Ekstruzyon
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// XY duzlemindeki basit cokgeni (saat yonu tersine, icbukey olabilir)
    /// Z ekseni boyunca <paramref name="depth"/> kalinlikta kalinlastirir;
    /// orijinde ortalanir. Kanat, siluet, tabela, kapi cercevesi.
    /// </summary>
    public static void Extrude(MeshData m, in Matrix4x4 xf, IReadOnlyList<Vector2> polygon, float depth, Color color, float uvScale = 1f, bool caps = true)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        var pts = SignedArea(polygon) < 0 ? polygon.Reverse().ToList() : polygon.ToList();
        var hz = depth * 0.5f;
        if (caps)
        {
            var tris = Triangulate(pts);
            foreach (var (zz, nz) in new[] { (hz, 1f), (-hz, -1f) })
            {
                var start = m.VertexCount;
                var n = SafeNormalize(Vector3.TransformNormal(new Vector3(0, 0, nz), xf));
                foreach (var p in pts)
                {
                    var wp = Vector3.Transform(new Vector3(p.X, p.Y, zz), xf);
                    m.AddVertex(wp, n, new Vector2(p.X, -p.Y) * uvScale, color);
                }

                for (var t = 0; t < tris.Count; t += 3)
                {
                    if (nz > 0)
                    {
                        m.AddTriangle(start + tris[t], start + tris[t + 1], start + tris[t + 2]);
                    }
                    else
                    {
                        m.AddTriangle(start + tris[t], start + tris[t + 2], start + tris[t + 1]);
                    }
                }
            }
        }

        // Yan duvarlar (her kenar duz normal)
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            var e = b - a;
            if (e.LengthSquared() < 1e-12f)
            {
                continue;
            }

            var n2 = Vector2.Normalize(new Vector2(e.Y, -e.X));
            var normal = new Vector3(n2.X, n2.Y, 0);
            var u = new Vector3(Vector2.Normalize(e), 0);
            QuadLocal(m, xf, new Vector3(a, -hz), new Vector3(b, -hz), new Vector3(b, hz), new Vector3(a, hz), normal, u, Vector3.UnitZ, color, uvScale);
        }
    }

    public static float SignedArea(IReadOnlyList<Vector2> poly)
    {
        var area = 0f;
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return area * 0.5f;
    }

    /// <summary>Kulak kirpma ucgenlemesi (saat yonu tersine basit cokgen). Indeks uclusu listesi doner.</summary>
    public static List<int> Triangulate(IReadOnlyList<Vector2> poly)
    {
        var result = new List<int>();
        var idx = Enumerable.Range(0, poly.Count).ToList();
        var guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            var found = false;
            for (var i = 0; i < idx.Count; i++)
            {
                var i0 = idx[(i - 1 + idx.Count) % idx.Count];
                var i1 = idx[i];
                var i2 = idx[(i + 1) % idx.Count];
                var a = poly[i0];
                var b = poly[i1];
                var c = poly[i2];
                if (Cross(b - a, c - b) <= 1e-9f)
                {
                    continue; // icbukey kose
                }

                var inside = false;
                foreach (var j in idx)
                {
                    if (j == i0 || j == i1 || j == i2)
                    {
                        continue;
                    }

                    if (PointInTriangle(poly[j], a, b, c))
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside)
                {
                    continue;
                }

                result.Add(i0);
                result.Add(i1);
                result.Add(i2);
                idx.RemoveAt(i);
                found = true;
                break;
            }

            if (!found)
            {
                break; // bozuk cokgen: kalanini yelpazeyle kapat
            }
        }

        for (var i = 1; i + 1 < idx.Count; i++)
        {
            result.Add(idx[0]);
            result.Add(idx[i]);
            result.Add(idx[i + 1]);
        }

        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var d1 = Cross(b - a, p - a);
        var d2 = Cross(c - b, p - b);
        var d3 = Cross(a - c, p - c);
        return d1 >= 0 && d2 >= 0 && d3 >= 0;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Loft (kesitler arasi gecis)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Loft kesiti: Z konumunda, merkezi (0, CenterY), yuvarlak koseli dikdortgen.</summary>
    public readonly record struct LoftSection(float Z, float CenterY, float HalfWidth, float HalfHeight, float Corner);

    /// <summary>
    /// Z ekseni boyunca siralanmis yuvarlak dikdortgen kesitleri birlestirir:
    /// araba kasasi, vapur govdesi, kedi govdesi. Normaller yumusak.
    /// </summary>
    public static void Loft(MeshData m, in Matrix4x4 xf, IReadOnlyList<LoftSection> sections, int cornerSegments, Color color,
        bool capStart = true, bool capEnd = true, float uvScale = 1f)
    {
        if (sections.Count < 2)
        {
            return;
        }

        var ring = RingTemplate(cornerSegments);
        var rows = new List<Vector3[]>(sections.Count);
        foreach (var s in sections)
        {
            var corner = MathF.Min(s.Corner, MathF.Min(s.HalfWidth, s.HalfHeight) * 0.999f);
            var row = new Vector3[ring.Count];
            for (var i = 0; i < ring.Count; i++)
            {
                var (q, d) = ring[i];
                var cx = q.X * (s.HalfWidth - corner) + d.X * corner;
                var cy = q.Y * (s.HalfHeight - corner) + d.Y * corner;
                row[i] = new Vector3(cx, s.CenterY + cy, s.Z);
            }

            rows.Add(row);
        }

        // Yumusak normaller: komsu satir ve sutunlardan merkezi fark
        var cols = ring.Count;
        var start = m.VertexCount;
        Matrix4x4.Invert(xf, out var inv);
        var nxf = Matrix4x4.Transpose(inv);
        var vDist = 0f;
        for (var r = 0; r < rows.Count; r++)
        {
            if (r > 0)
            {
                vDist += Vector3.Distance(rows[r][0], rows[r - 1][0]);
            }

            var uDist = 0f;
            for (var c = 0; c < cols; c++)
            {
                var p = rows[r][c];
                var along = rows[Math.Min(rows.Count - 1, r + 1)][c] - rows[Math.Max(0, r - 1)][c];
                var around = rows[r][(c + 1) % cols] - rows[r][(c - 1 + cols) % cols];
                var n = SafeNormalize(Vector3.Cross(around, along));
                // Kesitin merkezinden disa baksin
                var center = new Vector3(0, sections[r].CenterY, sections[r].Z);
                if (Vector3.Dot(n, p - center) < 0)
                {
                    n = -n;
                }

                if (c > 0)
                {
                    uDist += Vector3.Distance(rows[r][c], rows[r][c - 1]);
                }

                m.AddVertex(Vector3.Transform(p, xf), SafeNormalize(Vector3.TransformNormal(n, nxf)), new Vector2(uDist * uvScale, vDist * uvScale), color);
            }
        }

        // Sarim: Z artarken disari bakacak sekilde
        for (var r = 0; r < rows.Count - 1; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var a = start + r * cols + c;
                var b = start + r * cols + (c + 1) % cols;
                var a2 = a + cols;
                var b2 = b + cols;
                AddOriented(m, a, b, b2, xf, sections[r]);
                AddOriented(m, a, b2, a2, xf, sections[r]);
            }
        }

        if (capStart)
        {
            LoftCap(m, rows[0], new Vector3(0, sections[0].CenterY, sections[0].Z), -Vector3.UnitZ, xf, nxf, color);
        }

        if (capEnd)
        {
            var last = sections[^1];
            LoftCap(m, rows[^1], new Vector3(0, last.CenterY, last.Z), Vector3.UnitZ, xf, nxf, color);
        }
    }

    /// <summary>Ucgeni, kesit merkezinden disa bakacak sekilde ekler.</summary>
    private static void AddOriented(MeshData m, int a, int b, int c, in Matrix4x4 xf, LoftSection s)
    {
        var pa = m.Positions[a];
        var n = Vector3.Cross(m.Positions[b] - pa, m.Positions[c] - pa);
        var avgNormal = m.Normals[a] + m.Normals[b] + m.Normals[c];
        if (Vector3.Dot(n, avgNormal) >= 0)
        {
            m.AddTriangle(a, b, c);
        }
        else
        {
            m.AddTriangle(a, c, b);
        }
    }

    private static void LoftCap(MeshData m, Vector3[] row, Vector3 center, Vector3 normal, in Matrix4x4 xf, in Matrix4x4 nxf, Color color)
    {
        var wn = SafeNormalize(Vector3.TransformNormal(normal, nxf));
        var ci = m.AddVertex(Vector3.Transform(center, xf), wn, Vector2.Zero, color);
        var first = m.VertexCount;
        foreach (var p in row)
        {
            m.AddVertex(Vector3.Transform(p, xf), wn, new Vector2(p.X, p.Y), color);
        }

        for (var k = 0; k < row.Length; k++)
        {
            var a = first + k;
            var b = first + (k + 1) % row.Length;
            var cross = Vector3.Cross(m.Positions[a] - m.Positions[ci], m.Positions[b] - m.Positions[ci]);
            if (Vector3.Dot(cross, wn) >= 0)
            {
                m.AddTriangle(ci, a, b);
            }
            else
            {
                m.AddTriangle(ci, b, a);
            }
        }
    }

    /// <summary>Yuvarlak dikdortgen halkasi: (kose merkezinin isareti, kose yonu) ciftleri.</summary>
    private static List<(Vector2 Quadrant, Vector2 Dir)> RingTemplate(int cornerSegments)
    {
        var list = new List<(Vector2, Vector2)>();
        var segs = Math.Max(1, cornerSegments);
        var quadrants = new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        for (var q = 0; q < 4; q++)
        {
            var baseAngle = q * MathF.PI / 2;
            for (var i = 0; i <= segs; i++)
            {
                var a = baseAngle + i / (float)segs * MathF.PI / 2;
                list.Add((quadrants[q], new Vector2(MathF.Cos(a), MathF.Sin(a))));
            }
        }

        return list;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Renk yardimcilari
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Verilen kose araligini yukseklige gore koyulastirir (sahte ortam
    /// kapatmasi): tabana yakin kisimlar biraz koyu, nesne yere "oturur".
    /// </summary>
    public static void ShadeByHeight(MeshData m, int fromVertex, float y0, float y1, float darkenAtBottom)
    {
        var span = MathF.Max(1e-4f, y1 - y0);
        for (var i = fromVertex; i < m.VertexCount; i++)
        {
            var t = Math.Clamp((m.Positions[i].Y - y0) / span, 0f, 1f);
            var k = 1f - darkenAtBottom * (1f - t) * (1f - t);
            var c = m.Colors[i];
            m.Colors[i] = new Color((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k), c.A);
        }
    }
}
