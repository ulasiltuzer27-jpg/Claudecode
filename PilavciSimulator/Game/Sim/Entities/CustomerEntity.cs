using System.Numerics;
using PilavciSimulator.Net;
using PilavciSimulator.Sim.Customers;

namespace PilavciSimulator.Sim.Entities;

public enum CustomerState : byte
{
    /// <summary>Sokakta dolasan yaya (henuz musteri degil).</summary>
    Stroll,
    /// <summary>Arabaya yuruyor.</summary>
    Approach,
    /// <summary>Kuyrukta bekliyor.</summary>
    Queue,
    /// <summary>En onde, siparisi soyledi, tabak bekliyor.</summary>
    Ordered,
    /// <summary>Tabagi aldi, para ustu bekliyor.</summary>
    Paying,
    /// <summary>Yemek yerine yuruyor.</summary>
    ToEat,
    Eating,
    /// <summary>Bos tabagi arabaya birakmaya gidiyor.</summary>
    ReturnPlate,
    Leaving,
}

public enum CustomerAnim : byte
{
    Idle,
    Walk,
    Talk,
    Eat,
    Wave,
    Angry,
    Sit,
    Happy,
}

public enum Mood : byte
{
    None,
    Happy,
    Neutral,
    Angry,
    Love,
}

/// <summary>Musteri ya da yaya. Tum karar host'ta; istemci gorunumu cizer.</summary>
public sealed class CustomerEntity : Entity
{
    public override EntityKind Kind => EntityKind.Customer;

    public string TypeId = "";
    public uint Seed;
    public CustomerState State;
    public CustomerAnim Anim;
    public OrderSpec? Order;
    /// <summary>Sabir 0..1 (1 dolu).</summary>
    public float Patience = 1f;
    public Mood Mood;
    /// <summary>Konusma balonu: Loc anahtari + bicim argumani.</summary>
    public string BubbleKey = "";
    public string BubbleArg = "";
    public float BubbleUntil;
    /// <summary>Elde tabak var (yerken/tasirken gorunur).</summary>
    public bool HoldsPlate;
    public bool HoldsPackage;
    /// <summary>Para ustu bekleniyorsa: odenen ve odenecek tutar.</summary>
    public int PaidAmount;
    public int DueAmount;
    /// <summary>Servisi yapan oyuncu (para ustu ekrani ona acilir).</summary>
    public int ServerPlayerId;
    public int CartId;
    public int QueueIndex = -1;
    /// <summary>Oturuyorsa masa kimligi (dukkan).</summary>
    public int SeatTableId;

    // ── Yalnizca host ──────────────────────────────────────────────
    public List<Vector3> Path = new();
    public int PathIndex;
    public float Timer;
    public float WaitedMinutes;
    public float Speed = 1.35f;
    public string SpotId = "";
    public int EatIndex;
    public float LastSatisfaction;
    public bool Served;

    public override void WriteState(NetWriter w)
    {
        w.String(TypeId);
        w.UInt(Seed);
        w.Byte((byte)State);
        w.Byte((byte)Anim);
        w.Bool(Order is not null);
        Order?.Write(w);
        w.Unit8(Patience);
        w.Byte((byte)Mood);
        w.String(BubbleKey);
        w.String(BubbleArg);
        w.Float(BubbleUntil);
        w.Bool(HoldsPlate);
        w.Bool(HoldsPackage);
        w.VarInt(PaidAmount);
        w.VarInt(DueAmount);
        w.Int(ServerPlayerId);
        w.Int(CartId);
        w.Short((short)QueueIndex);
        w.Int(SeatTableId);
    }

    public override void ReadState(NetReader r)
    {
        TypeId = r.Str();
        Seed = r.UInt();
        State = (CustomerState)r.Byte();
        Anim = (CustomerAnim)r.Byte();
        if (r.Bool())
        {
            Order ??= new OrderSpec();
            Order.Read(r);
        }
        else
        {
            Order = null;
        }

        Patience = r.Unit8();
        Mood = (Mood)r.Byte();
        BubbleKey = r.Str();
        BubbleArg = r.Str();
        BubbleUntil = r.Float();
        HoldsPlate = r.Bool();
        HoldsPackage = r.Bool();
        PaidAmount = r.VarInt();
        DueAmount = r.VarInt();
        ServerPlayerId = r.Int();
        CartId = r.Int();
        QueueIndex = r.Short();
        SeatTableId = r.Int();
    }
}
