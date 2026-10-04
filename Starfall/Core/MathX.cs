using System.Numerics;
using System.Runtime.CompilerServices;

namespace Starfall.Core;

/// <summary>
/// Matematik yardimcilari. System.Numerics satir-vektor kuralini kullanir
/// (v * M); matrisler GLSL'ye oldugu gibi yuklenir ve shader'da P*V*M*v
/// yazilir — iki kural birbirinin devrigidir, sonuc aynidir.
/// </summary>
public static class MathX
{
    public const float Pi = MathF.PI;
    public const float TwoPi = MathF.PI * 2f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Smoothstep(float e0, float e1, float x)
    {
        float t = Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float WrapAngle(float a) => MathF.Atan2(MathF.Sin(a), MathF.Cos(a));

    public static float Damp(float current, float target, float rate, float dt) =>
        current + (target - current) * MathF.Min(1f, dt * rate);

    /// <summary>OpenGL perspektifi (NDC z = [-1, 1]). System.Numerics'inki [0, 1] uretir.</summary>
    public static Matrix4x4 PerspectiveGL(float fovYRad, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovYRad / 2f);
        var m = new Matrix4x4
        {
            M11 = f / aspect,
            M22 = f,
            M33 = (far + near) / (near - far),
            M34 = -1f,
            M43 = 2f * far * near / (near - far),
        };
        return m;
    }

    public static Matrix4x4 OrthoGL(float left, float right, float bottom, float top, float near, float far)
    {
        var m = Matrix4x4.Identity;
        m.M11 = 2f / (right - left);
        m.M22 = 2f / (top - bottom);
        m.M33 = -2f / (far - near);
        m.M41 = -(right + left) / (right - left);
        m.M42 = -(top + bottom) / (top - bottom);
        m.M43 = -(far + near) / (far - near);
        return m;
    }

    /// <summary>three.js'in varsayilan 'XYZ' Euler sirasi (once Z, sonra Y, sonra X uygulanir).</summary>
    public static Matrix4x4 EulerXYZ(float x, float y, float z) =>
        Matrix4x4.CreateRotationZ(z) * Matrix4x4.CreateRotationY(y) * Matrix4x4.CreateRotationX(x);

    /// <summary>three.js 'YXZ' Euler sirasi (yaw-pitch-roll).</summary>
    public static Matrix4x4 EulerYXZ(float x, float y, float z) =>
        Matrix4x4.CreateRotationZ(z) * Matrix4x4.CreateRotationX(x) * Matrix4x4.CreateRotationY(y);

    public static Matrix4x4 Compose(Vector3 pos, Matrix4x4 rot, Vector3 scale) =>
        Matrix4x4.CreateScale(scale) * rot * Matrix4x4.CreateTranslation(pos);

    public static Matrix4x4 TRS(Vector3 pos, float yaw, float scale) =>
        Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(pos);

    public static float SrgbToLinear(float c) =>
        c <= 0.04045f ? c * 0.0773993808f : MathF.Pow(c * 0.9478672986f + 0.0521327014f, 2.4f);

    public static float LinearToSrgb(float c) =>
        c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;

    /// <summary>"#rrggbb" → dogrusal renk (three.js Color.set ile ayni donusum).</summary>
    public static Vector3 Hex(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        int n = Convert.ToInt32(hex, 16);
        float r = ((n >> 16) & 255) / 255f;
        float g = ((n >> 8) & 255) / 255f;
        float b = (n & 255) / 255f;
        return new Vector3(SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b));
    }

    /// <summary>"#rrggbb" → sRGB (arayuz icin, donusumsuz).</summary>
    public static Vector4 HexSrgb(string hex, float alpha = 1f)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        int n = Convert.ToInt32(hex, 16);
        return new Vector4(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, alpha);
    }

    public static Vector3 LerpColor(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    public static float DistXZ(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);
}

/// <summary>Tohumlu rastgelelik: JS surumundeki mulberry32 ile bit bit ayni.</summary>
public sealed class Rng
{
    private uint _a;

    public Rng(uint seed) { _a = seed; }

    /// <summary>[0, 1) — JS'teki gibi double: karistirma (shuffle) birebir ayni cikar.</summary>
    public double NextD()
    {
        unchecked
        {
            _a += 0x6d2b79f5;
            uint t = _a;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            return (t ^ (t >> 14)) / 4294967296.0;
        }
    }

    public float Next() => (float)NextD();

    public float Range(float a, float b) => a + (b - a) * Next();
    public int Int(int n) => (int)(Next() * n) % Math.Max(1, n);
}
