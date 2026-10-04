using System.Numerics;
using PilavciSimulator.Net;

namespace PilavciSimulator.Sim.Entities;

public enum StationType : byte
{
    KazanOcagi = 1,
    Stovetop,
    Sink,
    CuttingBoard,
    Fridge,
    Pantry,
    Laptop,
    Bed,
    Trash,
    Pallet,
    Cart,
    Table,
}

/// <summary>Pilav arabasinin durumu.</summary>
public sealed class CartState
{
    public bool Open;
    public bool HeaterOn = true;
    public int CleanPlates;
    public int DirtyPlates;
    public int Packages;
    public int Ayran;
    public int Tursu;
    public float PepperG;
    /// <summary>Isitici tupu (%).</summary>
    public float Gas = 100;
    public int PusherId;
    /// <summary>Arabanin durdugu satis noktasi ("" = hicbiri).</summary>
    public string Spot = "";
    /// <summary>Zabita el koydu: bu dakikaya kadar acilamaz (mutlak oyun dakikasi).</summary>
    public float ClosedUntil;
    /// <summary>Arabanin kendi boya rengi (kozmetik, hex).</summary>
    public string Paint = "";

    public void Write(NetWriter w)
    {
        w.Bool(Open);
        w.Bool(HeaterOn);
        w.VarInt(CleanPlates);
        w.VarInt(DirtyPlates);
        w.VarInt(Packages);
        w.VarInt(Ayran);
        w.VarInt(Tursu);
        w.Float(PepperG);
        w.Float(Gas);
        w.Int(PusherId);
        w.String(Spot);
        w.Float(ClosedUntil);
        w.String(Paint);
    }

    public void Read(NetReader r)
    {
        Open = r.Bool();
        HeaterOn = r.Bool();
        CleanPlates = r.VarInt();
        DirtyPlates = r.VarInt();
        Packages = r.VarInt();
        Ayran = r.VarInt();
        Tursu = r.VarInt();
        PepperG = r.Float();
        Gas = r.Float();
        PusherId = r.Int();
        Spot = r.Str();
        ClosedUntil = r.Float();
        Paint = r.Str();
    }
}

/// <summary>
/// Sabit (ya da araba gibi itilebilen) donanim: ocak, lavabo, buzdolabi,
/// kiler, laptop, yatak, araba, masa. Parca ve yuva yerlesimi tipine ve
/// kademesine gore <see cref="StationDefs"/>'te.
/// </summary>
public sealed class StationEntity : Entity
{
    public override EntityKind Kind => EntityKind.Station;

    public StationType Type;
    /// <summary>Yerlesim etiketi (ocakA, pantry, cart...).</summary>
    public string Tag = "";
    public int Tier;
    /// <summary>Yukseltme alinmadan kilitli donanim (ikinci ocak gibi) cizilir ama kullanilamaz.</summary>
    public bool Enabled = true;
    /// <summary>Ocak gozlerinin ates duzeyi 0..3.</summary>
    public readonly byte[] Heat = new byte[4];

    // Kesme tahtasi
    public float BoardChickenKg;
    public float BoardShred;
    public float BoardQuality;

    public CartState? Cart;

    /// <summary>Masa: dolu sandalyeler (bit maskesi).</summary>
    public int SeatMask;

    /// <summary>Host: araba itilirken onceki konum (carpisma geri alma).</summary>
    public Vector3 PrevPosition;

    public static StationEntity Create(StationType type, Vector3 pos, float yaw, int tier = 0)
    {
        var s = new StationEntity { Type = type, Position = pos, Yaw = yaw, Tier = tier };
        if (type == StationType.Cart)
        {
            s.Cart = new CartState();
        }

        return s;
    }

    public override void WriteState(NetWriter w)
    {
        w.Byte((byte)Type);
        w.String(Tag);
        w.Byte((byte)Tier);
        w.Bool(Enabled);
        for (var i = 0; i < Heat.Length; i++)
        {
            w.Byte(Heat[i]);
        }

        w.Float(BoardChickenKg);
        w.Float(BoardShred);
        w.Float(BoardQuality);
        w.Int(SeatMask);
        w.Bool(Cart is not null);
        Cart?.Write(w);
    }

    public override void ReadState(NetReader r)
    {
        Type = (StationType)r.Byte();
        Tag = r.Str();
        Tier = r.Byte();
        Enabled = r.Bool();
        for (var i = 0; i < Heat.Length; i++)
        {
            Heat[i] = r.Byte();
        }

        BoardChickenKg = r.Float();
        BoardShred = r.Float();
        BoardQuality = r.Float();
        SeatMask = r.Int();
        if (r.Bool())
        {
            Cart ??= new CartState();
            Cart.Read(r);
        }
    }
}
