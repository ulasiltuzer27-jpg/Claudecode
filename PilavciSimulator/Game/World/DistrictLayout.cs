using System.Numerics;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Navigation;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.World;

/// <summary>Bir satis noktasinin alani.</summary>
public sealed class SpotZone
{
    public required string Id { get; init; }
    public Vector3 Center;
    /// <summary>XZ yari boyut.</summary>
    public Vector2 Half;
    /// <summary>Arabanin onerilen park konumu ve yonu.</summary>
    public Vector3 CartPos;
    public float CartYaw;
    /// <summary>Bu noktaya musteri getiren kapilar/uclar (nav dugum indeksleri).</summary>
    public List<int> SourceNodes = new();

    public bool Contains(Vector3 p, float margin = 0f) =>
        MathF.Abs(p.X - Center.X) <= Half.X + margin && MathF.Abs(p.Z - Center.Z) <= Half.Y + margin;
}

public sealed record StationPlacement(StationType Type, Vector3 Position, float Yaw, string Tag, int Tier = 0);

public sealed record ItemPlacement(ItemType Type, Vector3 Position, float Yaw, string? SocketTag = null, int SocketIndex = 0);

public enum LampKind : byte
{
    /// <summary>Sokak lambasi: yalnizca karanlikta.</summary>
    Street,
    /// <summary>Ic mekan: her zaman (karanlikta daha belirgin).</summary>
    Indoor,
    /// <summary>Vitrin/tabela: aksam.</summary>
    Shop,
}

public readonly record struct LampInfo(Vector3 Position, float Range, Vector3 Color, LampKind Kind);

/// <summary>Arka planda duran, oynanisa karismayan NPC (kahvehanedeki amcalar).</summary>
public readonly record struct DecorNpc(Vector3 Position, float Yaw, uint Seed, bool Sitting, string TypeId);

/// <summary>Bir dikdortgen (XZ).</summary>
public readonly record struct AreaRect(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public bool Contains(Vector3 p, float margin = 0f) =>
        p.X >= MinX - margin && p.X <= MaxX + margin && p.Z >= MinZ - margin && p.Z <= MaxZ + margin;

    public Vector3 Center => new((MinX + MaxX) / 2, 0, (MinZ + MaxZ) / 2);
}

/// <summary>
/// Mahallenin tum statik bilgisi: carpisma, yol grafigi, satis noktalari,
/// istasyon ve esya yerlesimi, lambalar. Host ve istemci ayni koddan
/// uretir (DistrictBuilder); ag uzerinden gonderilmez.
/// </summary>
public sealed class DistrictLayout
{
    public CollisionWorld Collision { get; } = new();
    public NavGraph Nav { get; } = new();
    public List<SpotZone> Spots { get; } = new();
    public List<StationPlacement> Stations { get; } = new();
    public List<ItemPlacement> Items { get; } = new();
    public List<LampInfo> Lamps { get; } = new();
    public List<DecorNpc> Decor { get; } = new();
    /// <summary>Hazir model yerlesimleri (CC0 ya da prosedurel yedek); festival susleri gibi sonradan cizilenler icin de referans.</summary>
    public List<PrefabPlacement> Prefabs { get; } = new();

    public Vector3 PlayerSpawn;
    public float PlayerSpawnYaw;
    public AreaRect DepotArea;
    public AreaRect ShopArea;
    public Vector3 PalletPos;
    /// <summary>Dukkan kepengi: dukkan alinmadan girisi kapatan kutu.</summary>
    public BoxCollider ShopShutter;

    public Vector3 VanEntry;
    public Vector3 VanStop;
    public Vector3 VanExit;
    public Vector3 FerryDock;
    public Vector3 FerryFar;
    public Vector3 PierDoor;
    public float RoadLaneNorth = -2f;
    public float RoadLaneSouth = 2f;
    public float RoadMinX = -128f;
    public float RoadMaxX = 128f;
    public List<Vector3> CatSpawns { get; } = new();
    public List<Vector3> SeagullPerches { get; } = new();
    public List<float> Crosswalks { get; } = new();

    /// <summary>Deniz yuzeyi (su shader'i icin).</summary>
    public float SeaLevel = -1.1f;
    public AreaRect Sea;

    /// <summary>Istemci: cizim geometrisi (testlerde null degil ama GPU'ya yuklenmez).</summary>
    public StaticScene.Builder? Geometry;

    public SpotZone? SpotAt(Vector3 p, float margin = 0f) => Spots.FirstOrDefault(s => s.Contains(p, margin));

    public SpotZone Spot(string id) => Spots.First(s => s.Id == id);
}
