using PilavciSimulator.Net;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Sim.Entities;

/// <summary>
/// Oyuncu. Konumunu SAHIBI belirler (istemci kendi hareketini simule edip
/// host'a yollar; co-op'ta hile endisesi yok, tepkisellik kazaniliyor).
/// Elindeki esya, ittigi araba ve basili tuttugu eylem host'ta.
/// </summary>
public sealed class PlayerEntity : Entity
{
    public override EntityKind Kind => EntityKind.Player;

    public string Name = "";
    public byte ColorIndex;
    public float Pitch;
    public bool Crouch;
    public int HeldItemId;
    public int PushingCartId;
    /// <summary>Basili tutulan eylem ve hedefi (yikama, karistirma...).</summary>
    public ActionId HoldAction;
    public int HoldTargetId;
    public byte HoldPart;
    /// <summary>Uyumaya hazir (co-op gun sonu oylamasi).</summary>
    public bool SleepReady;
    public bool Connected = true;
    /// <summary>Host: bu oyuncunun ag baglantisi (-1 = host'un kendisi).</summary>
    public int PeerId = -1;
    /// <summary>Son kullanma animasyonu (uzaktan gorunum icin sayac).</summary>
    public byte UseCounter;
    /// <summary>Host oyuncuyu zorla tasidiginda artar (gun basi); istemci konumunu buna gore sifirlar.</summary>
    public byte TeleportSeq;
    /// <summary>Kisa sohbet balonu.</summary>
    public string Chat = "";
    public float ChatUntil;

    public static readonly uint[] Colors = [0xE8792E, 0x3C7FB1, 0x4E9F4A, 0xC8463C];

    public override void WriteMotion(NetWriter w)
    {
        base.WriteMotion(w);
        w.Short((short)(Pitch * 10000));
        w.Bool(Crouch);
    }

    public override void ReadMotion(NetReader r)
    {
        base.ReadMotion(r);
        Pitch = r.Short() / 10000f;
        Crouch = r.Bool();
    }

    public override void WriteState(NetWriter w)
    {
        w.String(Name);
        w.Byte(ColorIndex);
        w.Int(HeldItemId);
        w.Int(PushingCartId);
        w.Byte((byte)HoldAction);
        w.Int(HoldTargetId);
        w.Byte(HoldPart);
        w.Bool(SleepReady);
        w.Bool(Connected);
        w.Byte(UseCounter);
        w.Byte(TeleportSeq);
        w.String(Chat);
        w.Float(ChatUntil);
    }

    public override void ReadState(NetReader r)
    {
        Name = r.Str();
        ColorIndex = r.Byte();
        HeldItemId = r.Int();
        PushingCartId = r.Int();
        HoldAction = (ActionId)r.Byte();
        HoldTargetId = r.Int();
        HoldPart = r.Byte();
        SleepReady = r.Bool();
        Connected = r.Bool();
        UseCounter = r.Byte();
        TeleportSeq = r.Byte();
        Chat = r.Str();
        ChatUntil = r.Float();
    }
}
