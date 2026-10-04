using PilavciSimulator.Net;

namespace PilavciSimulator.Sim.Entities;

public enum VehicleType : byte
{
    DeliveryVan = 1,
    ZabitaVan,
    Ferry,
    Car,
}

/// <summary>Arac: toptanci kamyoneti, zabita arabasi, vapur, gecen araba.</summary>
public sealed class VehicleEntity : Entity
{
    public override EntityKind Kind => EntityKind.Vehicle;

    public VehicleType Type;
    public byte State;
    public bool Lights;
    public uint Seed;

    // Host
    public float Timer;
    public System.Numerics.Vector3 Target;
    public float Speed;

    public override void WriteState(NetWriter w)
    {
        w.Byte((byte)Type);
        w.Byte(State);
        w.Bool(Lights);
        w.UInt(Seed);
    }

    public override void ReadState(NetReader r)
    {
        Type = (VehicleType)r.Byte();
        State = r.Byte();
        Lights = r.Bool();
        Seed = r.UInt();
    }
}

public enum AnimalType : byte
{
    Cat = 1,
    Seagull,
}

public enum AnimalState : byte
{
    Idle,
    Walk,
    Sneak,
    Eat,
    Flee,
    Sit,
    Fly,
    Purr,
}

/// <summary>Sokak kedisi ve martilar.</summary>
public sealed class AnimalEntity : Entity
{
    public override EntityKind Kind => EntityKind.Animal;

    public AnimalType Type;
    public AnimalState State;
    public uint Seed;
    public bool Mascot;

    // Host
    public float Timer;
    public int TargetId;
    public System.Numerics.Vector3 Target;

    public override void WriteState(NetWriter w)
    {
        w.Byte((byte)Type);
        w.Byte((byte)State);
        w.UInt(Seed);
        w.Bool(Mascot);
    }

    public override void ReadState(NetReader r)
    {
        Type = (AnimalType)r.Byte();
        State = (AnimalState)r.Byte();
        Seed = r.UInt();
        Mascot = r.Bool();
    }
}
