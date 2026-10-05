using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Prosedurel geometri ilkelleri. Tum sekiller verilen donusumle
/// <see cref="MeshData"/>'ya eklenir; UV'ler donusumden SONRAKI konumdan
/// metre basina <c>uvScale</c> ile hesaplanir. Boylece yan yana duran iki
/// ayri kutudaki tugla deseni birbirine kayiksiz devam eder.
///
/// Sarim kurali: on yuz saat yonunun tersine (OpenGL varsayilani).
/// Bir yuzun koseleri u x v = normal olacak eksenlerle uretilir.
/// </summary>
public static partial class Shapes
{
    [Flags]
    public enum Faces
    {
        None = 0,
        PosX = 1,
        NegX = 2,
        PosY = 4,
        NegY = 8,
        PosZ = 16,
        NegZ = 32,
        All = 63,
        Sides = PosX | NegX | PosZ | NegZ,
        NoBottom = All & ~NegY,
    }

    private static readonly (Vector3 N, Vector3 U, Vector3 V, Faces F)[] BoxFaces =
    [
        (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, Faces.PosX),
        (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY, Faces.NegX),
        (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ, Faces.PosY),
        (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, Faces.NegY),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, Faces.PosZ),
        (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY, Faces.NegZ),
    ];

    /// <summary>Merkezi yerel orijinde, boyutu <paramref name="size"/> olan kutu.</summary>
    public static void Box(MeshData m, in Matrix4x4 xf, Vector3 size, Color color, float uvScale = 1f, Faces faces = Faces.All)
    {
        var h = size * 0.5f;
        foreach (var (n, u, v, f) in BoxFaces)
        {
            if ((faces & f) == 0)
            {
                continue;
            }

            var hu = MathF.Abs(Vector3.Dot(u, h));
            var hv = MathF.Abs(Vector3.Dot(v, h));
            var hn = MathF.Abs(Vector3.Dot(n, h));
            var c = n * hn;
            QuadLocal(m, xf, c - u * hu - v * hv, c + u * hu - v * hv, c + u * hu + v * hv, c - u * hu + v * hv, n, u, v, color, uvScale);
        }
    }

    /// <summary>Min/maks koselerle kutu (dunya koordinatinda, donussuz).</summary>
    public static void BoxMinMax(MeshData m, Vector3 min, Vector3 max, Color color, float uvScale = 1f, Faces faces = Faces.All)
    {
        var xf = Matrix4x4.CreateTranslation((min + max) * 0.5f);
        Box(m, xf, max - min, color, uvScale, faces);
    }

    /// <summary>Tabani y=0'da olan kutu: dunyada "yere oturan" nesneler icin pratik.</summary>
    public static void BoxOnGround(MeshData m, in Matrix4x4 xf, Vector3 size, Color color, float uvScale = 1f, Faces faces = Faces.All)
    {
        var local = Matrix4x4.CreateTranslation(0, size.Y * 0.5f, 0) * xf;
        Box(m, local, size, color, uvScale, faces);
    }

    /// <summary>Yerel uzayda verilen dort koseyle (saat yonu tersine) dortgen.</summary>
    public static void QuadLocal(MeshData m, in Matrix4x4 xf, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Vector3 uAxis, Vector3 vAxis, Color color, float uvScale)
    {
        var wa = Vector3.Transform(a, xf);
        var wb = Vector3.Transform(b, xf);
        var wc = Vector3.Transform(c, xf);
        var wd = Vector3.Transform(d, xf);
        var wn = SafeNormalize(Vector3.TransformNormal(normal, xf));
        var wu = SafeNormalize(Vector3.TransformNormal(uAxis, xf));
        var wv = SafeNormalize(Vector3.TransformNormal(vAxis, xf));
        Vector2 Uv(Vector3 p) => new(Vector3.Dot(p, wu) * uvScale, -Vector3.Dot(p, wv) * uvScale);
        var ia = m.AddVertex(wa, wn, Uv(wa), color);
        var ib = m.AddVertex(wb, wn, Uv(wb), color);
        var ic = m.AddVertex(wc, wn, Uv(wc), color);
        var id = m.AddVertex(wd, wn, Uv(wd), color);
        m.AddQuad(ia, ib, ic, id);
    }

    /// <summary>Dunya koordinatinda dortgen; normal koselerden hesaplanir.</summary>
    public static void Quad(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float uvScale = 1f)
    {
        var n = SafeNormalize(Vector3.Cross(b - a, c - a));
        var u = SafeNormalize(b - a);
        var v = Vector3.Cross(n, u);
        QuadLocal(m, Matrix4x4.Identity, a, b, c, d, n, u, v, color, uvScale);
    }

    /// <summary>Dunya koordinatinda dortgen; UV'ler 0..1 (tabela/isaret gibi tek resimlik yuzeyler).</summary>
    public static void QuadUv(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color,
        Vector2 uvMin, Vector2 uvMax)
    {
        var n = SafeNormalize(Vector3.Cross(b - a, c - a));
        var ia = m.AddVertex(a, n, new Vector2(uvMin.X, uvMax.Y), color);
        var ib = m.AddVertex(b, n, new Vector2(uvMax.X, uvMax.Y), color);
        var ic = m.AddVertex(c, n, new Vector2(uvMax.X, uvMin.Y), color);
        var id = m.AddVertex(d, n, new Vector2(uvMin.X, uvMin.Y), color);
        m.AddQuad(ia, ib, ic, id);
    }

    public static void Triangle(MeshData m, Vector3 a, Vector3 b, Vector3 c, Color color, float uvScale = 1f)
    {
        var n = SafeNormalize(Vector3.Cross(b - a, c - a));
        var u = SafeNormalize(b - a);
        var v = Vector3.Cross(n, u);
        Vector2 Uv(Vector3 p) => new(Vector3.Dot(p, u) * uvScale, -Vector3.Dot(p, v) * uvScale);
        var ia = m.AddVertex(a, n, Uv(a), color);
        var ib = m.AddVertex(b, n, Uv(b), color);
        var ic = m.AddVertex(c, n, Uv(c), color);
        m.AddTriangle(ia, ib, ic);
    }

    /// <summary>
    /// Donel yuzey: profil (yaricap, yukseklik) noktalarinin Y ekseni
    /// etrafinda dondurulmesi. Kazan, tabak, kase, kubbe, minare...
    /// Profil boyunca her bolum kendi normalini alir (keskin low-poly
    /// kenarlar), cevre boyunca normaller yumusak.
    /// Profil asagidan yukari, dis yuzey disari bakacak sekilde verilmeli.
    /// </summary>
    public static void Lathe(MeshData m, in Matrix4x4 xf, IReadOnlyList<Vector2> profile, int segments, Color color,
        float uvScale = 1f, bool smoothProfile = false, float startAngle = 0f, float sweep = MathF.Tau)
    {
        Matrix4x4.Invert(xf, out var inv);
        var nxf = Matrix4x4.Transpose(inv);
        for (var p = 0; p < profile.Count - 1; p++)
        {
            var p0 = profile[p];
            var p1 = profile[p + 1];
            var d = p1 - p0;
            if (d.LengthSquared() < 1e-10f)
            {
                continue;
            }

            // Profil duzleminde disa bakan normal: (dy, -dr) donmus.
            var segN = Vector2.Normalize(new Vector2(d.Y, -d.X));
            Vector2 n0 = segN, n1 = segN;
            if (smoothProfile)
            {
                if (p > 0)
                {
                    var dp = p0 - profile[p - 1];
                    if (dp.LengthSquared() > 1e-10f)
                    {
                        n0 = Vector2.Normalize(segN + Vector2.Normalize(new Vector2(dp.Y, -dp.X)));
                    }
                }

                if (p + 2 < profile.Count)
                {
                    var dn = profile[p + 2] - p1;
                    if (dn.LengthSquared() > 1e-10f)
                    {
                        n1 = Vector2.Normalize(segN + Vector2.Normalize(new Vector2(dn.Y, -dn.X)));
                    }
                }
            }

            var vLen = d.Length();
            var baseIndex = m.VertexCount;
            for (var s = 0; s <= segments; s++)
            {
                var a = startAngle + s / (float)segments * sweep;
                var cos = MathF.Cos(a);
                var sin = MathF.Sin(a);
                var r0 = new Vector3(p0.X * cos, p0.Y, p0.X * sin);
                var r1 = new Vector3(p1.X * cos, p1.Y, p1.X * sin);
                var nn0 = new Vector3(n0.X * cos, n0.Y, n0.X * sin);
                var nn1 = new Vector3(n1.X * cos, n1.Y, n1.X * sin);
                var circ = MathF.Max(p0.X, p1.X) * sweep;
                var u = s / (float)segments * circ * uvScale;
                m.AddVertex(Vector3.Transform(r0, xf), SafeNormalize(Vector3.TransformNormal(nn0, nxf)), new Vector2(u, -p0.Y * uvScale), color);
                m.AddVertex(Vector3.Transform(r1, xf), SafeNormalize(Vector3.TransformNormal(nn1, nxf)), new Vector2(u, -(p0.Y + vLen) * uvScale), color);
            }

            for (var s = 0; s < segments; s++)
            {
                var i0 = baseIndex + s * 2;
                var i1 = i0 + 1;
                var i2 = i0 + 2;
                var i3 = i0 + 3;
                // Profil asagidan yukari, aci artarken: (i0, i2, i3, i1) disaridan saat yonu tersine.
                m.AddTriangle(i0, i1, i3);
                m.AddTriangle(i0, i3, i2);
            }
        }
    }

    /// <summary>Tabani y=0'da, ustu y=height'te silindir.</summary>
    public static void Cylinder(MeshData m, in Matrix4x4 xf, float radius, float height, int segments, Color color,
        bool capTop = true, bool capBottom = true, float uvScale = 1f)
    {
        Frustum(m, xf, radius, radius, height, segments, color, capTop, capBottom, uvScale);
    }

    /// <summary>Kesik koni (alt yaricap r0, ust yaricap r1).</summary>
    public static void Frustum(MeshData m, in Matrix4x4 xf, float r0, float r1, float height, int segments, Color color,
        bool capTop = true, bool capBottom = true, float uvScale = 1f)
    {
        var profile = new List<Vector2>(4);
        if (capBottom && r0 > 0)
        {
            profile.Add(new Vector2(0, 0));
        }

        profile.Add(new Vector2(r0, 0));
        profile.Add(new Vector2(r1, height));
        if (capTop && r1 > 0)
        {
            profile.Add(new Vector2(0, height));
        }

        Lathe(m, xf, profile, segments, color, uvScale);
    }

    /// <summary>Merkezi orijinde kure.</summary>
    public static void Sphere(MeshData m, in Matrix4x4 xf, float radius, int rings, int segments, Color color, float uvScale = 1f)
    {
        var profile = new List<Vector2>(rings + 1);
        for (var r = 0; r <= rings; r++)
        {
            var a = -MathF.PI / 2 + r / (float)rings * MathF.PI;
            profile.Add(new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius));
        }

        Lathe(m, xf, profile, segments, color, uvScale, smoothProfile: true);
    }

    /// <summary>Ustu y=0'da, yukari bakan daire.</summary>
    public static void Disc(MeshData m, in Matrix4x4 xf, float radius, int segments, Color color, float uvScale = 1f)
    {
        var n = SafeNormalize(Vector3.TransformNormal(Vector3.UnitY, xf));
        var center = Vector3.Transform(Vector3.Zero, xf);
        var ci = m.AddVertex(center, n, new Vector2(center.X, -center.Z) * uvScale, color);
        var first = m.VertexCount;
        for (var s = 0; s <= segments; s++)
        {
            var a = s / (float)segments * MathF.Tau;
            var p = Vector3.Transform(new Vector3(MathF.Cos(a) * radius, 0, MathF.Sin(a) * radius), xf);
            m.AddVertex(p, n, new Vector2(p.X, -p.Z) * uvScale, color);
        }

        for (var s = 0; s < segments; s++)
        {
            // Yukaridan bakinca saat yonu tersine: merkez, sonraki, onceki.
            m.AddTriangle(ci, first + s + 1, first + s);
        }
    }

    /// <summary>Halka (kazan kulpu, tabak kenari). XZ duzleminde, Y ekseni etrafinda.</summary>
    public static void Torus(MeshData m, in Matrix4x4 xf, float majorRadius, float minorRadius, int majorSegs, int minorSegs, Color color)
    {
        Matrix4x4.Invert(xf, out var inv);
        var nxf = Matrix4x4.Transpose(inv);
        var baseIndex = m.VertexCount;
        for (var i = 0; i <= majorSegs; i++)
        {
            var a = i / (float)majorSegs * MathF.Tau;
            var ca = MathF.Cos(a);
            var sa = MathF.Sin(a);
            for (var j = 0; j <= minorSegs; j++)
            {
                var b = j / (float)minorSegs * MathF.Tau;
                var cb = MathF.Cos(b);
                var sb = MathF.Sin(b);
                var n = new Vector3(cb * ca, sb, cb * sa);
                var p = new Vector3((majorRadius + minorRadius * cb) * ca, minorRadius * sb, (majorRadius + minorRadius * cb) * sa);
                m.AddVertex(Vector3.Transform(p, xf), SafeNormalize(Vector3.TransformNormal(n, nxf)), new Vector2(i / (float)majorSegs, j / (float)minorSegs), color);
            }
        }

        var stride = minorSegs + 1;
        for (var i = 0; i < majorSegs; i++)
        {
            for (var j = 0; j < minorSegs; j++)
            {
                var a = baseIndex + i * stride + j;
                var b = a + stride;
                m.AddTriangle(a, a + 1, b + 1);
                m.AddTriangle(a, b + 1, b);
            }
        }
    }

    /// <summary>
    /// Ucgen prizma (cati): tabani size.X x size.Z, yuksekligi size.Y,
    /// sirti Z ekseni boyunca. Tabani y=0'da.
    /// </summary>
    public static void Wedge(MeshData m, in Matrix4x4 xf, Vector3 size, Color color, float uvScale = 1f)
    {
        var hx = size.X / 2;
        var hz = size.Z / 2;
        var top = size.Y;
        var m4 = xf;
        Vector3 T(Vector3 p) => Vector3.Transform(p, m4);
        var a = new Vector3(-hx, 0, hz);
        var b = new Vector3(hx, 0, hz);
        var c = new Vector3(0, top, hz);
        var a2 = new Vector3(-hx, 0, -hz);
        var b2 = new Vector3(hx, 0, -hz);
        var c2 = new Vector3(0, top, -hz);
        // On ve arka uclar
        Triangle(m, T(a), T(b), T(c), color, uvScale);
        Triangle(m, T(b2), T(a2), T(c2), color, uvScale);
        // Egimli yuzeyler
        Quad(m, T(b), T(b2), T(c2), T(c), color, uvScale);
        Quad(m, T(a2), T(a), T(c), T(c2), color, uvScale);
    }

    /// <summary>Iki nokta arasinda kare kesitli cubuk (tel, demir, boru).</summary>
    public static void Beam(MeshData m, Vector3 from, Vector3 to, float thickness, Color color, float uvScale = 1f)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 1e-5f)
        {
            return;
        }

        dir /= len;
        var up = MathF.Abs(dir.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(up, dir));
        var realUp = Vector3.Cross(dir, right);
        var basis = new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            realUp.X, realUp.Y, realUp.Z, 0,
            dir.X, dir.Y, dir.Z, 0,
            (from.X + to.X) / 2, (from.Y + to.Y) / 2, (from.Z + to.Z) / 2, 1);
        Box(m, basis, new Vector3(thickness, thickness, len), color, uvScale);
    }

    public static Vector3 SafeNormalize(Vector3 v)
    {
        var l = v.Length();
        return l > 1e-8f ? v / l : Vector3.UnitY;
    }
}
