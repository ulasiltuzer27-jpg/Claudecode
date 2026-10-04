using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Matris ve renk donusumleri.
///
/// ── Matris kurali (ONEMLI) ─────────────────────────────────────────────
/// Oyun kodunun tamami System.Numerics kuralini kullanir: satir vektoru,
/// birlesim soldan saga (<c>olcek * donus * oteleme</c>), oteleme M41..M43.
/// raylib'in Matrix yapisi ayni bellek yerlesiminde ama devrik anlamli
/// (oteleme M14..M34). Bu yuzden raylib'e giden HER matris
/// <see cref="ToRay"/>'den gecer; raylib'den gelen her matris
/// <see cref="FromRay"/>'den. Iki kurali karistirmak tum sahneyi aynalar
/// ya da hic gostermez, bu yuzden donusum tek yerde.
///
/// Projeksiyonlar da burada: System.Numerics'in projeksiyonlari z'yi
/// [0,1]'e (Direct3D) esliyor; OpenGL [-1,1] istiyor.
/// </summary>
public static class Gfx
{
    public static Matrix4x4 ToRay(in Matrix4x4 m) => Matrix4x4.Transpose(m);
    public static Matrix4x4 FromRay(in Matrix4x4 m) => Matrix4x4.Transpose(m);

    /// <summary>OpenGL perspektif projeksiyonu (System.Numerics kurali).</summary>
    public static Matrix4x4 Perspective(float fovYRadians, float aspect, float near, float far)
    {
        var f = 1f / MathF.Tan(fovYRadians * 0.5f);
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) / (near - far), -1,
            0, 0, 2f * far * near / (near - far), 0);
    }

    /// <summary>OpenGL ortografik projeksiyon (System.Numerics kurali).</summary>
    public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float near, float far)
    {
        return new Matrix4x4(
            2f / (right - left), 0, 0, 0,
            0, 2f / (top - bottom), 0, 0,
            0, 0, -2f / (far - near), 0,
            -(right + left) / (right - left), -(top + bottom) / (top - bottom), -(far + near) / (far - near), 1);
    }

    /// <summary>Y ekseni etrafinda donus + oteleme: dunyadaki hemen her nesnenin donusumu.</summary>
    public static Matrix4x4 Trs(Vector3 position, float yaw, float scale = 1f)
    {
        var m = Matrix4x4.CreateRotationY(yaw);
        if (scale != 1f)
        {
            m = Matrix4x4.CreateScale(scale) * m;
        }

        m.Translation = position;
        return m;
    }

    public static Matrix4x4 Trs(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var m = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation);
        m.Translation = position;
        return m;
    }

    public static Color Rgb(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static Color Hex(uint rgb, int a = 255) =>
        new((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), (byte)a);

    public static Color WithAlpha(this Color c, float a) => new(c.R, c.G, c.B, (byte)Math.Clamp(a * 255f, 0f, 255f));

    public static Color Mul(this Color c, float k) =>
        new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), c.A);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    public static Vector3 ToVec3(this Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f);

    public static Color ToColor(this Vector3 v, float a = 1f) =>
        new((byte)Math.Clamp(v.X * 255f, 0, 255), (byte)Math.Clamp(v.Y * 255f, 0, 255), (byte)Math.Clamp(v.Z * 255f, 0, 255), (byte)Math.Clamp(a * 255f, 0, 255));
}
