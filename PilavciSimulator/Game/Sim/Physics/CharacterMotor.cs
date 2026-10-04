using System.Numerics;

namespace PilavciSimulator.Sim.Physics;

/// <summary>
/// Oyuncu hareketi: dik silindir (yaricap 0.3 m) yaw'li kutulara karsi.
///
///  - Yatay: kucuk alt adimlarla ilerle, cakisan kutulardan en yakin
///    noktanin tersine it. Ust yuzeyi ayak + basamak yuksekliginin
///    altinda kalan kutular (kaldirim, esik) engel sayilmaz.
///  - Dikey: yercekimi; zemin = dairenin altindaki en yuksek ust yuzey.
///    Engel sayilmayan kaldirim boylece "basamak cikma" olur. Inerken
///    yere yapisma (snap) var: merdivenden inerken havada suzulmesin.
///
/// Duvara capraz yurununce kayma kendiliginden olur (yalnizca dik bilesen
/// geri itiliyor).
/// </summary>
public sealed class CharacterMotor
{
    public Vector3 Position;
    public Vector3 Velocity;
    public bool Grounded { get; private set; }
    public float Radius = 0.3f;
    public float Height = 1.75f;
    public float StepHeight = 0.36f;
    public float Gravity = 22f;
    public float JumpSpeed = 6.2f;
    /// <summary>Kendi kutusunu (ittigi araba) yok saymak icin.</summary>
    public int IgnoreOwner;

    public void Move(CollisionWorld world, Vector2 wishVelocityXZ, bool jump, float dt)
    {
        var wasGrounded = Grounded;
        var delta = wishVelocityXZ * dt;
        var len = delta.Length();
        var steps = Math.Max(1, (int)MathF.Ceiling(len / (Radius * 0.5f)));
        var step = delta / steps;
        var xz = new Vector2(Position.X, Position.Z);
        for (var s = 0; s < steps; s++)
        {
            xz += step;
            xz = ResolveHorizontal(world, xz, Position.Y);
        }

        Position.X = xz.X;
        Position.Z = xz.Y;
        Velocity.X = wishVelocityXZ.X;
        Velocity.Z = wishVelocityXZ.Y;

        if (jump && Grounded)
        {
            Velocity.Y = JumpSpeed;
            Grounded = false;
        }

        Velocity.Y -= Gravity * dt;
        var newY = Position.Y + Velocity.Y * dt;
        var ground = world.GroundHeight(xz, Radius * 0.85f, Position.Y + StepHeight, IgnoreOwner);

        if (newY <= ground)
        {
            newY = ground;
            Velocity.Y = 0;
            Grounded = true;
        }
        else if (wasGrounded && Velocity.Y <= 0 && Position.Y - ground <= StepHeight + 0.01f)
        {
            newY = ground;
            Velocity.Y = 0;
            Grounded = true;
        }
        else
        {
            Grounded = false;
        }

        // Tavan
        if (Velocity.Y > 0)
        {
            var head = newY + Height;
            foreach (var i in world.QueryStatic(xz.X - Radius, xz.Y - Radius, xz.X + Radius, xz.Y + Radius))
            {
                var c = world.Static[i];
                if (c.Has(ColliderFlags.Solid) && c.Bottom > Position.Y + Height - 0.05f && c.Bottom < head && c.OverlapsCircleXZ(xz, Radius))
                {
                    newY = c.Bottom - Height;
                    Velocity.Y = 0;
                }
            }
        }

        Position.Y = newY;
    }

    private Vector2 ResolveHorizontal(CollisionWorld world, Vector2 xz, float feetY)
    {
        for (var iter = 0; iter < 4; iter++)
        {
            var moved = false;
            foreach (var i in world.QueryStatic(xz.X - Radius - 0.1f, xz.Y - Radius - 0.1f, xz.X + Radius + 0.1f, xz.Y + Radius + 0.1f))
            {
                var c = world.Static[i];
                if (Push(ref xz, c, feetY))
                {
                    moved = true;
                }
            }

            foreach (var c in world.Dynamic)
            {
                if (c.OwnerId != 0 && c.OwnerId == IgnoreOwner)
                {
                    continue;
                }

                if (Push(ref xz, c, feetY))
                {
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        return xz;
    }

    private bool Push(ref Vector2 xz, BoxCollider c, float feetY)
    {
        if (!c.Has(ColliderFlags.Solid))
        {
            return false;
        }

        if (c.Top <= feetY + StepHeight || c.Bottom >= feetY + Height)
        {
            return false;
        }

        var cp = c.ClosestPointXZ(xz);
        var d = xz - cp;
        var dist = d.Length();
        if (dist >= Radius)
        {
            return false;
        }

        if (dist < 1e-4f)
        {
            // Merkez kutunun icinde: en kisa eksenden disari cik.
            var l = c.ToLocal(new Vector3(xz.X, c.Center.Y, xz.Y));
            var px = c.Half.X - MathF.Abs(l.X);
            var pz = c.Half.Z - MathF.Abs(l.Z);
            Vector3 outLocal = px < pz
                ? new Vector3(MathF.Sign(l.X == 0 ? 1 : l.X) * (c.Half.X + Radius), 0, l.Z)
                : new Vector3(l.X, 0, MathF.Sign(l.Z == 0 ? 1 : l.Z) * (c.Half.Z + Radius));
            var w = c.ToWorld(outLocal);
            xz = new Vector2(w.X, w.Z);
            return true;
        }

        xz += d / dist * (Radius - dist + 0.001f);
        return true;
    }
}
