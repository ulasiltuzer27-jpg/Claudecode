using System.Numerics;

namespace PilavciSimulator.Sim.Entities;

public enum ItemType : byte
{
    Kazan = 1,
    Tencere,
    Suzgec,
    OlcuKabi,
    Kasik,
    TuzKutusu,
    Tereyagi,
    TavukPaketi,
    EtPaketi,
    TavukTepsisi,
    Tabak,
    PaketKap,
    Koli,
}

[Flags]
public enum SocketKind : byte
{
    None = 0,
    /// <summary>Kazan ocagi (yalniz kazan).</summary>
    KazanBurner = 1,
    /// <summary>Tencere gozu (tencere, kucuk kazan).</summary>
    Burner = 2,
    /// <summary>Araba: isitmali kazan yuvasi.</summary>
    CartKazan = 4,
    /// <summary>Araba: tencere/tepsi yuvasi.</summary>
    CartTopping = 8,
    /// <summary>Lavabo teknesi (suzgec ıslatma).</summary>
    Basin = 16,
    /// <summary>Kesme tahtasi (tavuk tepsisi).</summary>
    Board = 32,
}

/// <summary>Esya turunun sabit ozellikleri.</summary>
public sealed record ItemInfo(
    ItemType Type,
    string NameKey,
    Vector3 HalfSize,
    SocketKind Sockets,
    bool TwoHanded,
    float CarrySpeed);

public static class ItemInfos
{
    private static readonly Dictionary<ItemType, ItemInfo> Table = new()
    {
        [ItemType.Kazan] = new(ItemType.Kazan, "item.kazan", new Vector3(0.3f, 0.22f, 0.3f), SocketKind.KazanBurner | SocketKind.CartKazan, true, 0.8f),
        [ItemType.Tencere] = new(ItemType.Tencere, "item.tencere", new Vector3(0.18f, 0.12f, 0.18f), SocketKind.Burner | SocketKind.CartTopping | SocketKind.KazanBurner, false, 0.95f),
        [ItemType.Suzgec] = new(ItemType.Suzgec, "item.suzgec", new Vector3(0.17f, 0.07f, 0.17f), SocketKind.Basin, false, 1f),
        [ItemType.OlcuKabi] = new(ItemType.OlcuKabi, "item.olcukabi", new Vector3(0.08f, 0.1f, 0.08f), SocketKind.None, false, 1f),
        [ItemType.Kasik] = new(ItemType.Kasik, "item.kasik", new Vector3(0.04f, 0.03f, 0.2f), SocketKind.None, false, 1f),
        [ItemType.TuzKutusu] = new(ItemType.TuzKutusu, "item.tuz", new Vector3(0.05f, 0.08f, 0.05f), SocketKind.None, false, 1f),
        [ItemType.Tereyagi] = new(ItemType.Tereyagi, "item.tereyagi", new Vector3(0.06f, 0.03f, 0.04f), SocketKind.None, false, 1f),
        [ItemType.TavukPaketi] = new(ItemType.TavukPaketi, "item.tavukpaketi", new Vector3(0.12f, 0.04f, 0.09f), SocketKind.None, false, 1f),
        [ItemType.EtPaketi] = new(ItemType.EtPaketi, "item.etpaketi", new Vector3(0.11f, 0.04f, 0.08f), SocketKind.None, false, 1f),
        [ItemType.TavukTepsisi] = new(ItemType.TavukTepsisi, "item.tavuktepsisi", new Vector3(0.2f, 0.04f, 0.14f), SocketKind.CartTopping | SocketKind.Board, false, 1f),
        [ItemType.Tabak] = new(ItemType.Tabak, "item.tabak", new Vector3(0.12f, 0.04f, 0.12f), SocketKind.None, false, 1f),
        [ItemType.PaketKap] = new(ItemType.PaketKap, "item.paketkap", new Vector3(0.11f, 0.04f, 0.09f), SocketKind.None, false, 1f),
        [ItemType.Koli] = new(ItemType.Koli, "item.koli", new Vector3(0.25f, 0.18f, 0.2f), SocketKind.None, true, 0.85f),
    };

    public static ItemInfo Get(ItemType t) => Table[t];

    /// <summary>Kazan kademesine gore kapasite (kg pirinc).</summary>
    public static float KazanCapacityKg(int tier) => tier switch
    {
        0 => 3f,
        1 => 6f,
        _ => 10f,
    };

    public static float KazanRadius(int tier) => tier switch
    {
        0 => 0.24f,
        1 => 0.3f,
        _ => 0.36f,
    };

    public const float TencereCapacityKg = 2f;
}
