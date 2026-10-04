using PilavciSimulator.Net;
using PilavciSimulator.Sim.Cooking;

namespace PilavciSimulator.Sim.Customers;

/// <summary>Bir musterinin siparisi.</summary>
public sealed class OrderSpec
{
    /// <summary>menu.json kimligi (sade, nohutlu...).</summary>
    public string MenuId = "";
    public Food Base;
    /// <summary>1 = yarim, 2 = tam, 3 = duble.</summary>
    public int Scoops = 2;
    public bool Nohut;
    public bool Tavuk;
    public bool Fasulye;
    public bool Pepper;
    public bool Tursu;
    public bool Ayran;
    public bool Package;
    /// <summary>Siparis anindaki toplam fiyat (TL).</summary>
    public int Price;

    public void Write(NetWriter w)
    {
        w.String(MenuId);
        w.Byte((byte)Base);
        w.Byte((byte)Scoops);
        var f = (Nohut ? 1 : 0) | (Tavuk ? 2 : 0) | (Fasulye ? 4 : 0) | (Pepper ? 8 : 0) | (Tursu ? 16 : 0) | (Ayran ? 32 : 0) | (Package ? 64 : 0);
        w.Byte((byte)f);
        w.VarInt(Price);
    }

    public void Read(NetReader r)
    {
        MenuId = r.Str();
        Base = (Food)r.Byte();
        Scoops = r.Byte();
        var f = r.Byte();
        Nohut = (f & 1) != 0;
        Tavuk = (f & 2) != 0;
        Fasulye = (f & 4) != 0;
        Pepper = (f & 8) != 0;
        Tursu = (f & 16) != 0;
        Ayran = (f & 32) != 0;
        Package = (f & 64) != 0;
        Price = r.VarInt();
    }
}
