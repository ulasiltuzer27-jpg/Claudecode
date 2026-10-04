using System.Numerics;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>Eksen hizali sinir kutusu.</summary>
public struct Bounds
{
    public Vector3 Min;
    public Vector3 Max;

    public Bounds(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static Bounds Empty => new(new Vector3(float.MaxValue), new Vector3(float.MinValue));

    public readonly bool IsEmpty => Min.X > Max.X;
    public readonly Vector3 Center => (Min + Max) * 0.5f;
    public readonly Vector3 Extents => (Max - Min) * 0.5f;

    public void Encapsulate(Vector3 p)
    {
        Min = Vector3.Min(Min, p);
        Max = Vector3.Max(Max, p);
    }

    public void Encapsulate(Bounds b)
    {
        if (b.IsEmpty)
        {
            return;
        }

        Min = Vector3.Min(Min, b.Min);
        Max = Vector3.Max(Max, b.Max);
    }

    /// <summary>Donusturulmus kutunun yeni AABB'si (8 kose).</summary>
    public readonly Bounds Transform(in Matrix4x4 m)
    {
        var r = Empty;
        for (var i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? Min.X : Max.X, (i & 2) == 0 ? Min.Y : Max.Y, (i & 4) == 0 ? Min.Z : Max.Z);
            r.Encapsulate(Vector3.Transform(c, m));
        }

        return r;
    }
}

/// <summary>Gorus konisi: 6 duzlem, AABB testi.</summary>
public struct Frustum
{
    private Plane _p0, _p1, _p2, _p3, _p4, _p5;

    /// <summary>System.Numerics kuralinda view*projection matrisinden.</summary>
    public static Frustum FromViewProjection(in Matrix4x4 vp)
    {
        // Gribb-Hartmann, satir vektoru kurali: sutunlardan duzlemler.
        var f = new Frustum
        {
            _p0 = Normalize(new Plane(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41)),
            _p1 = Normalize(new Plane(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41)),
            _p2 = Normalize(new Plane(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42)),
            _p3 = Normalize(new Plane(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42)),
            _p4 = Normalize(new Plane(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43)),
            _p5 = Normalize(new Plane(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43)),
        };
        return f;
    }

    private static Plane Normalize(Plane p)
    {
        var len = p.Normal.Length();
        return len > 0 ? new Plane(p.Normal / len, p.D / len) : p;
    }

    public readonly bool Intersects(in Bounds b) =>
        Test(_p0, b) && Test(_p1, b) && Test(_p2, b) && Test(_p3, b) && Test(_p4, b) && Test(_p5, b);

    public readonly bool Intersects(Vector3 center, float radius) =>
        TestSphere(_p0, center, radius) && TestSphere(_p1, center, radius) && TestSphere(_p2, center, radius) &&
        TestSphere(_p3, center, radius) && TestSphere(_p4, center, radius) && TestSphere(_p5, center, radius);

    private static bool Test(in Plane p, in Bounds b)
    {
        // Duzlemin normaline en uzak kose disaridaysa kutu tamamen disarida.
        var v = new Vector3(
            p.Normal.X >= 0 ? b.Max.X : b.Min.X,
            p.Normal.Y >= 0 ? b.Max.Y : b.Min.Y,
            p.Normal.Z >= 0 ? b.Max.Z : b.Min.Z);
        return Vector3.Dot(p.Normal, v) + p.D >= 0;
    }

    private static bool TestSphere(in Plane p, Vector3 c, float r) => Vector3.Dot(p.Normal, c) + p.D >= -r;
}
