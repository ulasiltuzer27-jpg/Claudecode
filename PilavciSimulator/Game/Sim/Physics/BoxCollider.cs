using System.Numerics;

namespace PilavciSimulator.Sim.Physics;

[Flags]
public enum ColliderFlags : ushort
{
    None = 0,
    /// <summary>Oyuncuyu durdurur.</summary>
    Solid = 1,
    /// <summary>Musteri yol bulmasi bu kutudan kacinir (bilgi amacli).</summary>
    BlocksNav = 2,
    /// <summary>Ustune esya konabilir (tezgah, raf, masa, zemin).</summary>
    Placeable = 4,
    /// <summary>Isin atmalari (etkilesim hedefleme) bu kutuda durur.</summary>
    BlocksRay = 8,
    /// <summary>Hareketli (araba); her tik guncellenir.</summary>
    Dynamic = 16,
    /// <summary>Gorunmez sinir duvari: isin durdurmaz.</summary>
    Invisible = 32,

    Default = Solid | BlocksNav | BlocksRay,
    Surface = Solid | BlocksNav | BlocksRay | Placeable,
}

/// <summary>
/// Yalnizca Y ekseni etrafinda donebilen kutu. Dunyadaki her engel
/// (bina, tezgah, araba) bununla temsil ediliyor; tam OBB'ye gerek yok
/// cunku hicbir sey yana yatmiyor.
/// </summary>
public struct BoxCollider
{
    public Vector3 Center;
    public Vector3 Half;
    public float Yaw;
    public ColliderFlags Flags;
    /// <summary>Hareketli kutularda sahibi varlik; statiklerde 0.</summary>
    public int OwnerId;

    private float _cos;
    private float _sin;
    private bool _prepared;

    public BoxCollider(Vector3 center, Vector3 half, float yaw = 0f, ColliderFlags flags = ColliderFlags.Default, int ownerId = 0)
    {
        Center = center;
        Half = half;
        Yaw = yaw;
        Flags = flags;
        OwnerId = ownerId;
        _cos = MathF.Cos(yaw);
        _sin = MathF.Sin(yaw);
        _prepared = true;
    }

    public static BoxCollider FromMinMax(Vector3 min, Vector3 max, ColliderFlags flags = ColliderFlags.Default) =>
        new((min + max) * 0.5f, (max - min) * 0.5f, 0f, flags);

    /// <summary>Tabani y'de, ustu y+h'de; merkez XZ ve yaw ile.</summary>
    public static BoxCollider OnGround(Vector3 basePos, Vector3 size, float yaw = 0f, ColliderFlags flags = ColliderFlags.Default) =>
        new(basePos + new Vector3(0, size.Y * 0.5f, 0), size * 0.5f, yaw, flags);

    public readonly float Top => Center.Y + Half.Y;
    public readonly float Bottom => Center.Y - Half.Y;
    public readonly bool Has(ColliderFlags f) => (Flags & f) == f;

    private void Prepare()
    {
        if (!_prepared)
        {
            _cos = MathF.Cos(Yaw);
            _sin = MathF.Sin(Yaw);
            _prepared = true;
        }
    }

    public void SetPose(Vector3 center, float yaw)
    {
        Center = center;
        Yaw = yaw;
        _cos = MathF.Cos(yaw);
        _sin = MathF.Sin(yaw);
        _prepared = true;
    }

    /// <summary>Dunya noktasini kutunun yerel eksenlerine cevirir (merkeze gore).</summary>
    public Vector3 ToLocal(Vector3 world)
    {
        Prepare();
        var d = world - Center;
        // Yerel = R(-yaw) * d ; System.Numerics RotationY ile tutarli.
        return new Vector3(d.X * _cos - d.Z * _sin, d.Y, d.X * _sin + d.Z * _cos);
    }

    public Vector3 ToWorldDir(Vector3 local)
    {
        Prepare();
        return new Vector3(local.X * _cos + local.Z * _sin, local.Y, -local.X * _sin + local.Z * _cos);
    }

    public Vector3 ToWorld(Vector3 local) => Center + ToWorldDir(local);

    /// <summary>XZ duzleminde kutuya en yakin nokta (dunya XZ).</summary>
    public Vector2 ClosestPointXZ(Vector2 p)
    {
        var l = ToLocal(new Vector3(p.X, Center.Y, p.Y));
        var cx = Math.Clamp(l.X, -Half.X, Half.X);
        var cz = Math.Clamp(l.Z, -Half.Z, Half.Z);
        var w = ToWorld(new Vector3(cx, 0, cz));
        return new Vector2(w.X, w.Z);
    }

    /// <summary>Nokta kutunun XZ izdusumu icinde mi.</summary>
    public bool ContainsXZ(Vector2 p, float margin = 0f)
    {
        var l = ToLocal(new Vector3(p.X, Center.Y, p.Y));
        return MathF.Abs(l.X) <= Half.X + margin && MathF.Abs(l.Z) <= Half.Z + margin;
    }

    /// <summary>Daire (XZ) kutuyla kesisiyor mu.</summary>
    public bool OverlapsCircleXZ(Vector2 c, float r)
    {
        var cp = ClosestPointXZ(c);
        return Vector2.DistanceSquared(cp, c) <= r * r;
    }

    /// <summary>Isin-kutu kesisimi (slab yontemi, yerel uzayda).</summary>
    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out float t, out Vector3 normal)
    {
        var o = ToLocal(origin);
        Prepare();
        var d = new Vector3(dir.X * _cos - dir.Z * _sin, dir.Y, dir.X * _sin + dir.Z * _cos);
        var tMin = 0f;
        var tMax = maxDist;
        var nLocal = Vector3.Zero;
        for (var axis = 0; axis < 3; axis++)
        {
            var oa = axis == 0 ? o.X : axis == 1 ? o.Y : o.Z;
            var da = axis == 0 ? d.X : axis == 1 ? d.Y : d.Z;
            var ha = axis == 0 ? Half.X : axis == 1 ? Half.Y : Half.Z;
            if (MathF.Abs(da) < 1e-8f)
            {
                if (oa < -ha || oa > ha)
                {
                    t = 0;
                    normal = default;
                    return false;
                }

                continue;
            }

            var inv = 1f / da;
            var t1 = (-ha - oa) * inv;
            var t2 = (ha - oa) * inv;
            var sign = -1f;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                sign = 1f;
            }

            if (t1 > tMin)
            {
                tMin = t1;
                nLocal = axis switch
                {
                    0 => new Vector3(sign, 0, 0),
                    1 => new Vector3(0, sign, 0),
                    _ => new Vector3(0, 0, sign),
                };
            }

            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
            {
                t = 0;
                normal = default;
                return false;
            }
        }

        t = tMin;
        normal = nLocal == Vector3.Zero ? -Vector3.Normalize(dir) : ToWorldDir(nLocal);
        return true;
    }
}
