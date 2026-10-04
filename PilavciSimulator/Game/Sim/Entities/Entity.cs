using System.Numerics;
using PilavciSimulator.Net;

namespace PilavciSimulator.Sim.Entities;

public enum EntityKind : byte
{
    Player = 1,
    Item = 2,
    Station = 3,
    Customer = 4,
    Vehicle = 5,
    Animal = 6,
}

/// <summary>
/// Dunyadaki her nesnenin tabani. Host'ta simulasyon degistirir; istemcide
/// yalnizca ag mesajlariyla guncellenir.
///
/// Iki ayri replikasyon kanali var:
///  - Hareket (konum + yon): sik degisir, guvenilmez kanaldan, eskisi
///    kaybolsa da sorun yok.
///  - Durum (icerik, sicaklik, siparis...): guvenilir kanaldan, yalnizca
///    degistiginde.
/// </summary>
public abstract class Entity
{
    public int Id;
    public abstract EntityKind Kind { get; }
    public Vector3 Position;
    public float Yaw;

    /// <summary>Host: durum degisti, bir sonraki gonderimde yollanacak.</summary>
    public bool StateDirty = true;
    /// <summary>Host: konum degisti.</summary>
    public bool MotionDirty = true;

    // ── Istemci tarafi yumusatma ────────────────────────────────────
    public Vector3 RenderPosition;
    public float RenderYaw;
    public bool HasRenderPose;

    public void MarkState() => StateDirty = true;

    public void SetMotion(Vector3 position, float yaw)
    {
        if (position != Position || yaw != Yaw)
        {
            Position = position;
            Yaw = yaw;
            MotionDirty = true;
        }
    }

    public virtual void WriteMotion(NetWriter w)
    {
        w.Vec3(Position);
        w.Angle(Yaw);
    }

    public virtual void ReadMotion(NetReader r)
    {
        Position = r.Vec3();
        Yaw = r.Angle();
    }

    public abstract void WriteState(NetWriter w);
    public abstract void ReadState(NetReader r);

    /// <summary>Istemci: gorunen konumu ag konumuna dogru yumusakca kaydir.</summary>
    public void Smooth(float dt, float rate = 14f)
    {
        if (!HasRenderPose || Vector3.DistanceSquared(RenderPosition, Position) > 16f)
        {
            RenderPosition = Position;
            RenderYaw = Yaw;
            HasRenderPose = true;
            return;
        }

        var k = 1f - MathF.Exp(-rate * dt);
        RenderPosition = Vector3.Lerp(RenderPosition, Position, k);
        RenderYaw = LerpAngle(RenderYaw, Yaw, k);
    }

    public static float LerpAngle(float a, float b, float t)
    {
        var d = (b - a) % MathF.Tau;
        if (d > MathF.PI)
        {
            d -= MathF.Tau;
        }
        else if (d < -MathF.PI)
        {
            d += MathF.Tau;
        }

        return a + d * t;
    }

    public static float YawFromDirection(Vector3 dir) => MathF.Atan2(-dir.X, -dir.Z);

    public static Vector3 Forward(float yaw) => new(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
    public static Vector3 Right(float yaw) => new(MathF.Cos(yaw), 0, -MathF.Sin(yaw));

    /// <summary>Yerel (varlik eksenlerinde) ofseti dunyaya cevirir.</summary>
    public static Vector3 LocalToWorld(Vector3 origin, float yaw, Vector3 local) =>
        origin + Right(yaw) * local.X + Vector3.UnitY * local.Y - Forward(yaw) * local.Z;
}
