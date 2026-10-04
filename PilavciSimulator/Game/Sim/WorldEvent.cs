using System.Numerics;
using PilavciSimulator.Net;

namespace PilavciSimulator.Sim;

public enum WorldEventType : byte
{
    /// <summary>3B ses (Key = ses kimligi).</summary>
    Sound = 1,
    /// <summary>Bildirim (Key = Loc anahtari, Arg = bicim argumanlari '|' ile).</summary>
    Toast,
    /// <summary>Para efekti (Value = tutar).</summary>
    Coin,
    /// <summary>Para ustu ekranini ac (PlayerId hedef, Value = musteri kimligi).</summary>
    OpenCash,
    LevelUp,
    Achievement,
    /// <summary>Parcacik patlamasi (Key = tur).</summary>
    Burst,
    /// <summary>Gun bitti: rapor ekrani (Value = gun).</summary>
    DayEnded,
    DayStarted,
    Chat,
    /// <summary>Zabita uyarisi baslatildi/bitti.</summary>
    Zabita,
}

/// <summary>
/// Host'ta olusan, istemcilerde geri bildirim (ses, efekt, bildirim)
/// uretecek olay. Durumu degistirmez; durum ayrica replike edilir.
/// </summary>
public struct WorldEvent
{
    public WorldEventType Type;
    /// <summary>0 = herkes; aksi halde yalnizca bu oyuncu.</summary>
    public int PlayerId;
    public string Key;
    public string Arg;
    public Vector3 Position;
    public int Value;
    public byte Color;

    public static WorldEvent Sound(string id, Vector3 pos) => new() { Type = WorldEventType.Sound, Key = id, Position = pos, Arg = "" };

    public static WorldEvent Toast(string key, string arg = "", byte color = 0, int player = 0) =>
        new() { Type = WorldEventType.Toast, Key = key, Arg = arg, Color = color, PlayerId = player };

    public void Write(NetWriter w)
    {
        w.Byte((byte)Type);
        w.Int(PlayerId);
        w.String(Key);
        w.String(Arg);
        w.Vec3(Position);
        w.Int(Value);
        w.Byte(Color);
    }

    public static WorldEvent Read(NetReader r) => new()
    {
        Type = (WorldEventType)r.Byte(),
        PlayerId = r.Int(),
        Key = r.Str(),
        Arg = r.Str(),
        Position = r.Vec3(),
        Value = r.Int(),
        Color = r.Byte(),
    };
}
