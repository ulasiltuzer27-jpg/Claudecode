using System.Numerics;
using PilavciSimulator.Net;
using PilavciSimulator.Sim.Cooking;

namespace PilavciSimulator.Sim.Entities;

public enum Attach : byte
{
    /// <summary>Dunyada serbest (bir yuzeyde ya da dusuyor).</summary>
    Free,
    /// <summary>Bir oyuncunun elinde.</summary>
    Held,
    /// <summary>Bir istasyonun yuvasinda (ocak, araba, lavabo).</summary>
    Socket,
}

/// <summary>
/// Tasinabilir esya. Turune gore farkli icerik alanlari kullanilir; tek
/// sinif tutmak serilestirmeyi ve ag kodunu sade birakiyor.
/// </summary>
public sealed class ItemEntity : Entity
{
    public override EntityKind Kind => EntityKind.Item;

    public ItemType Type;
    /// <summary>Kazan kademesi (0 kucuk, 1 orta, 2 buyuk).</summary>
    public int Tier;

    public Attach Attach;
    /// <summary>Held: oyuncu kimligi. Socket: istasyon kimligi.</summary>
    public int ParentId;
    public int SocketIndex;

    /// <summary>Host: serbest dusme hizi.</summary>
    public Vector3 Velocity;
    public bool Resting = true;

    // ── Icerik ──────────────────────────────────────────────────────
    /// <summary>Kazan / tencere.</summary>
    public PotState? Pot;
    /// <summary>Tabak / paket kap.</summary>
    public ServingState? Serving;

    /// <summary>Suzgec: icindeki pirinc/bulgur (kg) ve yikama/ıslatma.</summary>
    public float GrainKg;
    public bool GrainIsBulgur;
    public float Wash;
    public float Soak;

    /// <summary>Olcu kabi: su (L).</summary>
    public float WaterL;

    /// <summary>Tereyagi / tavuk / et paketi: kalan miktar (g ya da kg).</summary>
    public float Amount;

    /// <summary>Tavuk tepsisi: kalan servis, kalite, sicaklik.</summary>
    public float Servings;
    public float Quality;
    public float Temp = 20f;

    /// <summary>Koli: malzeme kimligi ve miktari.</summary>
    public string SupplyId = "";
    public float SupplyAmount;

    public ItemInfo Info => ItemInfos.Get(Type);

    public static ItemEntity Create(ItemType type, int tier = 0)
    {
        var e = new ItemEntity { Type = type, Tier = tier };
        if (type is ItemType.Kazan or ItemType.Tencere)
        {
            e.Pot = new PotState();
        }
        else if (type is ItemType.Tabak or ItemType.PaketKap)
        {
            e.Serving = new ServingState();
        }

        return e;
    }

    public override void WriteState(NetWriter w)
    {
        w.Byte((byte)Type);
        w.Byte((byte)Tier);
        w.Byte((byte)Attach);
        w.Int(ParentId);
        w.Byte((byte)SocketIndex);
        w.Bool(Resting);
        switch (Type)
        {
            case ItemType.Kazan or ItemType.Tencere:
                Pot!.Write(w);
                break;
            case ItemType.Tabak or ItemType.PaketKap:
                Serving!.Write(w);
                break;
            case ItemType.Suzgec:
                w.Float(GrainKg);
                w.Bool(GrainIsBulgur);
                w.Float(Wash);
                w.Float(Soak);
                break;
            case ItemType.OlcuKabi:
                w.Float(WaterL);
                break;
            case ItemType.Tereyagi or ItemType.TavukPaketi or ItemType.EtPaketi:
                w.Float(Amount);
                break;
            case ItemType.TavukTepsisi:
                w.Float(Servings);
                w.Float(Quality);
                w.Float(Temp);
                break;
            case ItemType.Koli:
                w.String(SupplyId);
                w.Float(SupplyAmount);
                break;
        }
    }

    public override void ReadState(NetReader r)
    {
        Type = (ItemType)r.Byte();
        Tier = r.Byte();
        Attach = (Attach)r.Byte();
        ParentId = r.Int();
        SocketIndex = r.Byte();
        Resting = r.Bool();
        switch (Type)
        {
            case ItemType.Kazan or ItemType.Tencere:
                Pot ??= new PotState();
                Pot.Read(r);
                break;
            case ItemType.Tabak or ItemType.PaketKap:
                Serving ??= new ServingState();
                Serving.Read(r);
                break;
            case ItemType.Suzgec:
                GrainKg = r.Float();
                GrainIsBulgur = r.Bool();
                Wash = r.Float();
                Soak = r.Float();
                break;
            case ItemType.OlcuKabi:
                WaterL = r.Float();
                break;
            case ItemType.Tereyagi or ItemType.TavukPaketi or ItemType.EtPaketi:
                Amount = r.Float();
                break;
            case ItemType.TavukTepsisi:
                Servings = r.Float();
                Quality = r.Float();
                Temp = r.Float();
                break;
            case ItemType.Koli:
                SupplyId = r.Str();
                SupplyAmount = r.Float();
                break;
        }
    }
}
