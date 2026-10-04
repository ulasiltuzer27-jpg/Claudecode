using System.Numerics;

namespace Starfall.Physics;

/// <summary>
/// Kinematik karakter kontrolcusu: kapsul (uc kureyle yaklasik), kayarak hareket,
/// basamak cikma, egim siniri, zemine yapisma. Rapier'in KinematicCharacterController'inin
/// yaptigi isin karsiligi; davranislar tools/verify ile olculur.
/// </summary>
public sealed class CharacterMotor
{
    public readonly CollisionWorld World;
    public Vector3 Feet;
    public float Radius = 0.3f;
    public float HalfHeight = 0.25f;
    public float MaxSlopeCos = MathF.Cos(48f * MathF.PI / 180f);
    public float StepHeight = 0.35f;
    public float SnapDistance = 0.35f;

    public bool Grounded;
    public Vector3 GroundNormal = Vector3.UnitY;
    public Shape? GroundShape;
    public string? GroundTag;
    public bool HitCeiling;
    public bool HitWall;
    public Vector3 WallNormal;
    public Shape? WallShape;

    private readonly List<Contact> _contacts = new();

    public CharacterMotor(CollisionWorld world)
    {
        World = world;
    }

    public float Height => Radius * 2 + HalfHeight * 2;

    private Vector3 SphereCenter(int i) => Feet + new Vector3(0, Radius + HalfHeight * i, 0);

    /// <summary>Hareketi uygula; gercekte alinan yer degistirmeyi dondurur.</summary>
    public Vector3 Move(Vector3 delta, bool allowSnap)
    {
        var start = Feet;
        bool wasGrounded = Grounded;
        Grounded = false;
        HitCeiling = false;
        HitWall = false;
        GroundShape = null;
        GroundTag = null;
        GroundNormal = Vector3.UnitY;
        int sub = Math.Max(1, (int)MathF.Ceiling(delta.Length() / 0.12f));
        var step = delta / sub;
        for (int i = 0; i < sub; i++)
        {
            Feet += step;
            Resolve();
        }

        // basamak: yerdeyken duvara takildiysak bir basamak yukaridan dene
        var horiz = new Vector3(delta.X, 0, delta.Z);
        if (HitWall && wasGrounded && horiz.LengthSquared() > 1e-6f && delta.Y <= 0.01f)
        {
            var progressed = Feet - start;
            float got = new Vector3(progressed.X, 0, progressed.Z).Length();
            float want = horiz.Length();
            if (got < want * 0.6f)
            {
                var save = Feet;
                bool sg = Grounded; var sn = GroundNormal; var ss = GroundShape; var st = GroundTag;
                Feet = start + new Vector3(0, StepHeight, 0);
                Resolve();
                Feet += horiz;
                Resolve();
                var after = Feet;
                Feet -= new Vector3(0, StepHeight + 0.05f, 0);
                Resolve();
                var p2 = Feet - start;
                float got2 = new Vector3(p2.X, 0, p2.Z).Length();
                if (Grounded && got2 > got + 0.01f && Feet.Y - start.Y <= StepHeight + 0.02f)
                {
                    // kabul
                }
                else
                {
                    Feet = save;
                    Grounded = sg; GroundNormal = sn; GroundShape = ss; GroundTag = st;
                }
                _ = after;
            }
        }

        // zemine yapisma (yokus asagi yururken havaya ucmasin)
        if (!Grounded && wasGrounded && allowSnap)
        {
            var c = SphereCenter(0);
            if (World.Raycast(c, -Vector3.UnitY, Radius + SnapDistance, out var hit) && hit.Normal.Y >= MaxSlopeCos)
            {
                Feet.Y = hit.Point.Y + 0.002f;
                Resolve();
                if (!Grounded)
                {
                    Grounded = true;
                    GroundNormal = hit.Normal;
                    GroundShape = hit.Shape;
                    GroundTag = hit.Shape?.Tag ?? "ground";
                }
            }
        }
        return Feet - start;
    }

    private void Resolve()
    {
        for (int iter = 0; iter < 4; iter++)
        {
            bool any = false;
            for (int si = 0; si < 3; si++)
            {
                _contacts.Clear();
                World.SphereContacts(SphereCenter(si), Radius, _contacts);
                foreach (var c in _contacts)
                {
                    if (c.Depth <= 1e-5f) continue;
                    any = true;
                    var n = c.Normal;
                    if (n.Y >= MaxSlopeCos)
                    {
                        // yurunebilir zemin: dikey it (egimde kaymasin)
                        Feet.Y += MathF.Min(c.Depth / n.Y, c.Depth * 2.5f) + 0.0005f;
                        if (si == 0 || !Grounded)
                        {
                            Grounded = true;
                            GroundNormal = n;
                            GroundShape = c.Shape;
                            GroundTag = c.Tag;
                        }
                    }
                    else if (n.Y <= -0.5f)
                    {
                        Feet += n * (c.Depth + 0.0005f);
                        HitCeiling = true;
                    }
                    else
                    {
                        // dik yuzey: yalnizca yatay it (tirmanmasin, asagi kaysin)
                        var nh = new Vector3(n.X, 0, n.Z);
                        float l = nh.Length();
                        if (l < 1e-5f) continue;
                        nh /= l;
                        float push = MathF.Min(c.Depth / MathF.Max(0.25f, l), c.Depth * 4f);
                        Feet += nh * (push + 0.0005f);
                        HitWall = true;
                        WallNormal = nh;
                        WallShape = c.Shape;
                    }
                }
            }
            if (!any) break;
        }
    }

    /// <summary>Isinlama: carpismayi cozerek yerlestir.</summary>
    public void Teleport(Vector3 feet)
    {
        Feet = feet;
        Grounded = false;
        Resolve();
    }
}
