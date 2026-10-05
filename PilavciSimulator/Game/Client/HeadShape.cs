using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Client;

/// <summary>
/// Tek parca bas yuzeyi: elipsoit kafatasi, altta daralip one cikan cene.
/// Iki kurenin (kafatasi + cene) kesisimi tegete yakin oldugundan yuzde
/// dalgali bir cizgi birakiyordu; burada tek yuzey var. Yuz parcalari
/// (goz, kas, agiz, biyik) <see cref="OnFace"/> ile yuzeye oturtulur.
///
/// Bas uzayi: boyun tabani y=0, yuz -Z'ye bakar.
/// </summary>
public static class HeadShape
{
    public static readonly Vector3 Center = new(0, 0.105f, 0.005f);
    public const float Rx = 0.105f, Ry = 0.122f, Rz = 0.113f;

    /// <summary>Birim yonden yuzey noktasi.</summary>
    public static Vector3 Point(Vector3 d)
    {
        var t = SmoothStep(MathF.Max(0f, -d.Y) / 0.9f);
        var front = MathF.Max(0f, -d.Z);
        var x = d.X * Rx * (1f - 0.22f * t);
        var y = d.Y * Ry * (1f + 0.08f * t * front);
        var z = d.Z * Rz * (1f - 0.1f * t * (1f - front)) - 0.01f * t * front;
        return Center + new Vector3(x, y, z);
    }

    private static float SmoothStep(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * (3f - 2f * u);
    }

    /// <summary>Kutupsal (teta: +Y'den), boylam (fi) acilariyla nokta.</summary>
    private static Vector3 At(float theta, float phi) =>
        Point(new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi)));

    private static Vector3 NormalAt(float theta, float phi)
    {
        const float e = 1e-3f;
        var dt = At(theta + e, phi) - At(theta - e, phi);
        var dp = At(theta, phi + e) - At(theta, phi - e);
        var n = Vector3.Cross(dp, dt);
        var p = At(theta, phi);
        if (n.LengthSquared() < 1e-14f)
        {
            return Vector3.Normalize(p - Center);
        }

        n = Vector3.Normalize(n);
        return Vector3.Dot(n, p - Center) < 0 ? -n : n;
    }

    /// <summary>Basin yuzey agi (yumusak normaller).</summary>
    public static void Mesh(MeshData m, in Matrix4x4 xf, Color color, int rings = 16, int segments = 24)
    {
        var start = m.VertexCount;
        for (var r = 0; r <= rings; r++)
        {
            var theta = MathF.PI * r / rings;
            for (var s = 0; s <= segments; s++)
            {
                var phi = MathF.Tau * s / segments;
                var p = Vector3.Transform(At(theta, phi), xf);
                var n = Shapes.SafeNormalize(Vector3.TransformNormal(NormalAt(theta, phi), xf));
                m.AddVertex(p, n, new Vector2((float)s / segments, (float)r / rings), color);
            }
        }

        var row = segments + 1;
        for (var r = 0; r < rings; r++)
        {
            for (var s = 0; s < segments; s++)
            {
                var a = start + r * row + s;
                var b = a + row;
                // Sarim disa bakan normalle ayni yonde olsun
                var pa = m.Positions[a];
                var face = Vector3.Cross(m.Positions[b] - pa, m.Positions[b + 1] - pa);
                if (face.LengthSquared() < 1e-16f)
                {
                    face = Vector3.Cross(m.Positions[b + 1] - pa, m.Positions[a + 1] - pa);
                }

                if (Vector3.Dot(face, m.Normals[a] + m.Normals[b + 1]) >= 0)
                {
                    m.AddQuad(a, b, b + 1, a + 1);
                }
                else
                {
                    m.AddQuad(a, a + 1, b + 1, b);
                }
            }
        }
    }

    /// <summary>
    /// Yuzun on tarafinda (x, y) izdusumune denk gelen yuzey noktasi ve normali;
    /// nokta normal boyunca <paramref name="lift"/> kadar disari tasinir.
    /// </summary>
    public static (Vector3 Position, Vector3 Normal) OnFace(float x, float y, float lift = 0f)
    {
        // Baslangic: duz elipsoit tahmini, sonra (teta, fi) uzerinde Newton
        var dx = Math.Clamp(x / Rx, -0.98f, 0.98f);
        var dy = Math.Clamp((y - Center.Y) / Ry, -0.98f, 0.98f);
        var theta = MathF.Acos(dy);
        var phi = MathF.Atan2(-MathF.Sqrt(MathF.Max(0.0001f, 1f - dx * dx - dy * dy)), dx);
        for (var i = 0; i < 12; i++)
        {
            var p = At(theta, phi);
            var ex = p.X - x;
            var ey = p.Y - y;
            if (ex * ex + ey * ey < 1e-12f)
            {
                break;
            }

            const float h = 1e-3f;
            var pt = (At(theta + h, phi) - At(theta - h, phi)) / (2 * h);
            var pp = (At(theta, phi + h) - At(theta, phi - h)) / (2 * h);
            var det = pt.X * pp.Y - pp.X * pt.Y;
            if (MathF.Abs(det) < 1e-9f)
            {
                break;
            }

            theta -= (ex * pp.Y - pp.X * ey) / det;
            phi -= (pt.X * ey - ex * pt.Y) / det;
        }

        var n = NormalAt(theta, phi);
        return (At(theta, phi) + n * lift, n);
    }

    /// <summary>
    /// Yuzey uzerinde yerel cerceve: yerel -Z disari (normal), +Y yukari.
    /// <paramref name="depth"/> pozitifse yuzeyin icine gomulur.
    /// </summary>
    public static Matrix4x4 Frame(float x, float y, float depth = 0f)
    {
        var (p, n) = OnFace(x, y);
        var z = -n;
        var r = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, z));
        var u = Vector3.Cross(z, r);
        var o = p - n * depth;
        return new Matrix4x4(r.X, r.Y, r.Z, 0, u.X, u.Y, u.Z, 0, z.X, z.Y, z.Z, 0, o.X, o.Y, o.Z, 1);
    }
}
