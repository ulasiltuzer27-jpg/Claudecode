using System.Numerics;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.Sim.Entities;

/// <summary>Istasyonun etkilesilebilir bir parcasi (dugme, cuval, kapak...).</summary>
public readonly record struct PartDef(byte Id, Vector3 Center, Vector3 Half, string NameKey);

/// <summary>Esya konabilen yuva.</summary>
public readonly record struct SocketDef(byte Index, SocketKind Kind, Vector3 Position);

/// <summary>Istasyon govdesinin carpisma kutusu (yerel).</summary>
public readonly record struct BodyDef(Vector3 Center, Vector3 Half, ColliderFlags Flags);

/// <summary>
/// Istasyon turlerinin yerlesimi. Tum koordinatlar istasyonun yerel
/// uzayinda: X sag, Y yukari, -Z on (musterinin/oyuncunun baktigi taraf
/// degil, istasyonun "yuzu"). Arabada -Z musteri tarafi, +X itme yonu.
/// </summary>
public static class StationDefs
{
    // ── Parca kimlikleri ─────────────────────────────────────────────
    public const byte PartBody = 0;
    public const byte PartKnob0 = 10; // 10..13 ocak dugmeleri
    public const byte PartFaucet = 20;
    public const byte PartBasin = 21;
    public const byte PartBoard = 30;
    public const byte PartButter = 40;
    public const byte PartChicken = 41;
    public const byte PartMeat = 42;
    public const byte PartRice = 50;
    public const byte PartBulgur = 51;
    public const byte PartChickpea = 52;
    public const byte PartCanned = 53;
    public const byte PartBeans = 54;
    public const byte PartStockDrop = 55;
    public const byte PartScreen = 60;
    public const byte PartBed = 61;
    public const byte PartBin = 62;
    public const byte CartHandle = 70;
    public const byte CartSign = 71;
    public const byte CartPriceBoard = 72;
    public const byte CartPlates = 73;
    public const byte CartPackages = 74;
    public const byte CartDirtyBin = 75;
    public const byte CartBucket = 76;
    public const byte CartCooler = 77;
    public const byte CartPickles = 78;
    public const byte CartPepper = 79;
    public const byte CartHeater = 80;
    public const byte CartCashBox = 81;
    public const byte CartGas = 82;

    public const float CounterHeight = 0.9f;
    public const float CartTop = 0.95f;

    public static int BurnerCount(StationEntity s) => s.Type switch
    {
        StationType.KazanOcagi => 1,
        StationType.Stovetop => s.Tier >= 1 ? 4 : 2,
        _ => 0,
    };

    public static float CartHalfLength(int tier) => tier >= 1 ? 1.25f : 0.85f;

    public static IReadOnlyList<SocketDef> Sockets(StationEntity s)
    {
        switch (s.Type)
        {
            case StationType.KazanOcagi:
                return [new SocketDef(0, SocketKind.KazanBurner, new Vector3(0, 0.42f, 0))];
            case StationType.Stovetop:
                return s.Tier >= 1
                    ?
                    [
                        new SocketDef(0, SocketKind.Burner, new Vector3(-0.42f, 0.07f, -0.02f)),
                        new SocketDef(1, SocketKind.Burner, new Vector3(-0.14f, 0.07f, -0.02f)),
                        new SocketDef(2, SocketKind.Burner, new Vector3(0.14f, 0.07f, -0.02f)),
                        new SocketDef(3, SocketKind.Burner, new Vector3(0.42f, 0.07f, -0.02f)),
                    ]
                    :
                    [
                        new SocketDef(0, SocketKind.Burner, new Vector3(-0.24f, 0.07f, -0.02f)),
                        new SocketDef(1, SocketKind.Burner, new Vector3(0.24f, 0.07f, -0.02f)),
                    ];
            case StationType.Sink:
                return [new SocketDef(0, SocketKind.Basin, new Vector3(0, CounterHeight - 0.12f, 0))];
            case StationType.CuttingBoard:
                return [new SocketDef(0, SocketKind.Board, new Vector3(0.05f, 0.03f, 0))];
            case StationType.Cart:
                return s.Tier >= 1
                    ?
                    [
                        new SocketDef(0, SocketKind.CartKazan, new Vector3(-0.62f, 0.8f, 0)),
                        new SocketDef(1, SocketKind.CartKazan, new Vector3(0.1f, 0.8f, 0)),
                        new SocketDef(2, SocketKind.CartTopping, new Vector3(0.82f, CartTop, -0.19f)),
                        new SocketDef(3, SocketKind.CartTopping, new Vector3(0.82f, CartTop, 0.19f)),
                    ]
                    :
                    [
                        new SocketDef(0, SocketKind.CartKazan, new Vector3(-0.3f, 0.8f, 0)),
                        new SocketDef(1, SocketKind.CartTopping, new Vector3(0.44f, CartTop, -0.19f)),
                        new SocketDef(2, SocketKind.CartTopping, new Vector3(0.44f, CartTop, 0.19f)),
                    ];
            default:
                return [];
        }
    }

    public static IReadOnlyList<PartDef> Parts(StationEntity s)
    {
        switch (s.Type)
        {
            case StationType.KazanOcagi:
                return
                [
                    new PartDef(PartKnob0, new Vector3(0, 0.22f, -0.33f), new Vector3(0.08f, 0.08f, 0.06f), "part.knob"),
                    new PartDef(PartBody, new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.2f, 0.3f), "station.kazanocagi"),
                ];
            case StationType.Stovetop:
            {
                var list = new List<PartDef>();
                foreach (var sock in Sockets(s))
                {
                    list.Add(new PartDef((byte)(PartKnob0 + sock.Index), new Vector3(sock.Position.X, 0.03f, -0.3f), new Vector3(0.05f, 0.04f, 0.04f), "part.knob"));
                }

                list.Add(new PartDef(PartBody, new Vector3(0, 0.03f, 0), new Vector3(s.Tier >= 1 ? 0.58f : 0.42f, 0.04f, 0.3f), "station.stovetop"));
                return list;
            }
            case StationType.Sink:
                return
                [
                    new PartDef(PartFaucet, new Vector3(0, CounterHeight + 0.25f, 0.22f), new Vector3(0.08f, 0.14f, 0.1f), "part.faucet"),
                    new PartDef(PartBasin, new Vector3(0, CounterHeight - 0.05f, 0), new Vector3(0.28f, 0.1f, 0.22f), "part.basin"),
                ];
            case StationType.CuttingBoard:
                return [new PartDef(PartBoard, new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.03f, 0.2f), "part.board")];
            case StationType.Fridge:
                return
                [
                    new PartDef(PartButter, new Vector3(0, 1.45f, -0.36f), new Vector3(0.34f, 0.18f, 0.06f), "part.butter"),
                    new PartDef(PartChicken, new Vector3(0, 1.05f, -0.36f), new Vector3(0.34f, 0.18f, 0.06f), "part.chicken"),
                    new PartDef(PartMeat, new Vector3(0, 0.65f, -0.36f), new Vector3(0.34f, 0.18f, 0.06f), "part.meat"),
                ];
            case StationType.Pantry:
                return
                [
                    new PartDef(PartRice, new Vector3(-0.8f, 0.4f, -0.05f), new Vector3(0.28f, 0.4f, 0.25f), "part.rice"),
                    new PartDef(PartBulgur, new Vector3(-0.25f, 0.35f, -0.05f), new Vector3(0.24f, 0.35f, 0.22f), "part.bulgur"),
                    new PartDef(PartChickpea, new Vector3(0.25f, 0.35f, -0.05f), new Vector3(0.24f, 0.35f, 0.22f), "part.chickpea"),
                    new PartDef(PartBeans, new Vector3(0.75f, 0.35f, -0.05f), new Vector3(0.24f, 0.35f, 0.22f), "part.beans"),
                    new PartDef(PartCanned, new Vector3(-0.5f, 1.25f, 0.05f), new Vector3(0.45f, 0.15f, 0.2f), "part.canned"),
                    new PartDef(PartStockDrop, new Vector3(0.45f, 1.25f, 0.05f), new Vector3(0.5f, 0.35f, 0.2f), "part.stockdrop"),
                ];
            case StationType.Laptop:
                return [new PartDef(PartScreen, new Vector3(0, 0.88f, 0), new Vector3(0.22f, 0.14f, 0.18f), "part.laptop")];
            case StationType.Bed:
                return [new PartDef(PartBed, new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.3f, 0.45f), "part.bed")];
            case StationType.Trash:
                return [new PartDef(PartBin, new Vector3(0, 0.35f, 0), new Vector3(0.22f, 0.35f, 0.22f), "part.trash")];
            case StationType.Cart:
            {
                var hl = CartHalfLength(s.Tier);
                return
                [
                    new PartDef(CartHandle, new Vector3(-hl - 0.15f, 0.95f, 0), new Vector3(0.12f, 0.08f, 0.38f), "part.handle"),
                    new PartDef(CartSign, new Vector3(0, 1.72f, 0), new Vector3(hl * 0.6f, 0.14f, 0.05f), "part.sign"),
                    new PartDef(CartPriceBoard, new Vector3(hl - 0.25f, 1.52f, 0.38f), new Vector3(0.22f, 0.12f, 0.04f), "part.priceboard"),
                    new PartDef(CartPlates, new Vector3(hl + 0.14f, 1.02f, 0.18f), new Vector3(0.13f, 0.08f, 0.13f), "part.plates"),
                    new PartDef(CartPackages, new Vector3(hl + 0.14f, 1.02f, -0.16f), new Vector3(0.12f, 0.08f, 0.11f), "part.packages"),
                    new PartDef(CartDirtyBin, new Vector3(-hl + 0.25f, 0.7f, -0.47f), new Vector3(0.2f, 0.12f, 0.08f), "part.dirtybin"),
                    new PartDef(CartBucket, new Vector3(-hl + 0.3f, 0.22f, 0.62f), new Vector3(0.18f, 0.2f, 0.18f), "part.bucket"),
                    new PartDef(CartCooler, new Vector3(0.1f, 0.45f, 0.46f), new Vector3(0.28f, 0.18f, 0.08f), "part.cooler"),
                    new PartDef(CartPickles, new Vector3(hl + 0.14f, 1.06f, 0.0f), new Vector3(0.06f, 0.1f, 0.06f), "part.pickles"),
                    new PartDef(CartPepper, new Vector3(hl - 0.08f, 1.0f, 0.34f), new Vector3(0.04f, 0.06f, 0.04f), "part.pepper"),
                    new PartDef(CartHeater, new Vector3(-0.3f, 0.55f, 0.43f), new Vector3(0.07f, 0.07f, 0.05f), "part.heater"),
                    new PartDef(CartCashBox, new Vector3(-hl + 0.18f, 1.0f, 0.3f), new Vector3(0.13f, 0.06f, 0.09f), "part.cashbox"),
                    new PartDef(CartGas, new Vector3(-0.35f, 0.25f, 0.05f), new Vector3(0.16f, 0.25f, 0.16f), "part.gas"),
                ];
            }
            case StationType.Table:
                return [new PartDef(PartBody, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.04f, 0.45f), "station.table")];
            default:
                return [];
        }
    }

    /// <summary>Carpisma govdeleri.</summary>
    public static IReadOnlyList<BodyDef> Bodies(StationEntity s)
    {
        switch (s.Type)
        {
            case StationType.KazanOcagi:
                return [new BodyDef(new Vector3(0, 0.21f, 0), new Vector3(0.3f, 0.21f, 0.3f), ColliderFlags.Default)];
            case StationType.Fridge:
                return [new BodyDef(new Vector3(0, 0.95f, 0), new Vector3(0.38f, 0.95f, 0.35f), ColliderFlags.Default)];
            case StationType.Pantry:
                return [new BodyDef(new Vector3(0, 0.9f, 0.1f), new Vector3(1.1f, 0.9f, 0.25f), ColliderFlags.Default)];
            case StationType.Bed:
                return [new BodyDef(new Vector3(0, 0.22f, 0), new Vector3(1.0f, 0.22f, 0.45f), ColliderFlags.Default)];
            case StationType.Trash:
                return [new BodyDef(new Vector3(0, 0.35f, 0), new Vector3(0.22f, 0.35f, 0.22f), ColliderFlags.Default)];
            case StationType.Cart:
            {
                var hl = CartHalfLength(s.Tier);
                return [new BodyDef(new Vector3(0, 0.75f, 0), new Vector3(hl + 0.05f, 0.75f, 0.42f), ColliderFlags.Solid | ColliderFlags.BlocksRay | ColliderFlags.Dynamic)];
            }
            case StationType.Table:
                return [new BodyDef(new Vector3(0, 0.39f, 0), new Vector3(0.45f, 0.39f, 0.45f), ColliderFlags.Surface)];
            default:
                // Tezgah ustu donanim (ocak, lavabo, tahta, laptop): tezgahin kendi kutusu yeter.
                return [];
        }
    }

    /// <summary>Kuyruk yerleri: arabanin musteri tarafinda, onden geriye.</summary>
    public static Vector3 QueueSlot(StationEntity cart, int index)
    {
        var hl = CartHalfLength(cart.Tier);
        var local = index == 0
            ? new Vector3(0.1f, 0, -0.95f)
            : new Vector3(-hl - 0.5f - (index - 1) * 0.75f, 0, -1.25f - MathF.Min(index, 3) * 0.05f);
        return Entity.LocalToWorld(cart.Position, cart.Yaw, local);
    }

    /// <summary>Yemek yeme noktalari: arabanin yaninda ayakta.</summary>
    public static Vector3 EatSpot(StationEntity cart, int index)
    {
        var hl = CartHalfLength(cart.Tier);
        var side = index % 2 == 0 ? 1f : -1f;
        var row = index / 2;
        var local = new Vector3(side * (hl + 0.8f + row * 0.7f), 0, -0.6f - row * 0.5f);
        return Entity.LocalToWorld(cart.Position, cart.Yaw, local);
    }

    /// <summary>Oyuncunun arabayi iterken durdugu nokta (tutamagin arkasi).</summary>
    public static Vector3 PushOffset(int tier) => new(-CartHalfLength(tier) - 0.75f, 0, 0);
}
