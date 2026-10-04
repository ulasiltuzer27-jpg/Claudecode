using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Net;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Physics;
using PilavciSimulator.World;

namespace PilavciSimulator.Sim;

/// <summary>
/// Oyun durumunun tamami. Host'ta <see cref="Simulation"/> ilerletir;
/// istemcide ag mesajlariyla dolar. Hem tek oyunculu hem co-op ayni nesne.
/// </summary>
public sealed class GameWorld
{
    public GameData Data { get; }
    public DistrictLayout Layout { get; }
    public CollisionWorld Collision => Layout.Collision;
    public bool IsHost { get; }

    public readonly Dictionary<int, Entity> Entities = new();
    public readonly List<PlayerEntity> Players = new();
    public readonly List<ItemEntity> Items = new();
    public readonly List<StationEntity> Stations = new();
    public readonly List<CustomerEntity> Customers = new();
    public readonly List<VehicleEntity> Vehicles = new();
    public readonly List<AnimalEntity> Animals = new();

    public WorldClock Clock { get; } = new();
    public WeatherState Weather { get; } = new();
    public EconomyState Economy { get; } = new();
    public ProgressState Progress { get; } = new();
    public ReputationState Reputation { get; } = new();
    public EventsState Events { get; } = new();

    public Rng Rng;
    public int NextId = 1;
    /// <summary>Gercek zaman (saniye), animasyonlar icin.</summary>
    public float Time;
    public bool GlobalsDirty = true;
    public readonly List<int> RemovedIds = new();
    public event Action<WorldEvent>? EventRaised;

    /// <summary>Gun sonu ekrani acik: simulasyon durur.</summary>
    public bool DayOver;

    public GameWorld(GameData data, DistrictLayout layout, bool isHost, int seed)
    {
        Data = data;
        Layout = layout;
        IsHost = isHost;
        Rng = new Rng(seed);
    }

    public int Level => ProgressState.LevelFor(Progress.Xp, Data.Balance.XpLevels);

    // ── Varlik yonetimi ─────────────────────────────────────────────

    public T Spawn<T>(T e) where T : Entity
    {
        e.Id = NextId++;
        e.StateDirty = true;
        e.MotionDirty = true;
        Add(e);
        return e;
    }

    public void Add(Entity e)
    {
        Entities[e.Id] = e;
        switch (e)
        {
            case PlayerEntity p:
                Players.Add(p);
                break;
            case ItemEntity i:
                Items.Add(i);
                break;
            case StationEntity s:
                Stations.Add(s);
                break;
            case CustomerEntity c:
                Customers.Add(c);
                break;
            case VehicleEntity v:
                Vehicles.Add(v);
                break;
            case AnimalEntity a:
                Animals.Add(a);
                break;
        }

        if (e.Id >= NextId)
        {
            NextId = e.Id + 1;
        }
    }

    public void Remove(int id)
    {
        if (!Entities.Remove(id, out var e))
        {
            return;
        }

        switch (e)
        {
            case PlayerEntity p:
                Players.Remove(p);
                break;
            case ItemEntity i:
                Items.Remove(i);
                break;
            case StationEntity s:
                Stations.Remove(s);
                break;
            case CustomerEntity c:
                Customers.Remove(c);
                break;
            case VehicleEntity v:
                Vehicles.Remove(v);
                break;
            case AnimalEntity a:
                Animals.Remove(a);
                break;
        }

        if (IsHost)
        {
            RemovedIds.Add(id);
        }
    }

    public void Clear()
    {
        Entities.Clear();
        Players.Clear();
        Items.Clear();
        Stations.Clear();
        Customers.Clear();
        Vehicles.Clear();
        Animals.Clear();
        RemovedIds.Clear();
    }

    public T? Get<T>(int id) where T : Entity => id != 0 && Entities.TryGetValue(id, out var e) ? e as T : null;

    public StationEntity? StationByTag(string tag) => Stations.FirstOrDefault(s => s.Tag == tag);

    public IEnumerable<StationEntity> Carts => Stations.Where(s => s.Type == StationType.Cart);

    public ItemEntity? ItemInSocket(int stationId, int socket) =>
        Items.FirstOrDefault(i => i.Attach == Attach.Socket && i.ParentId == stationId && i.SocketIndex == socket);

    public ItemEntity? HeldBy(PlayerEntity p) => Get<ItemEntity>(p.HeldItemId);

    public void Raise(WorldEvent e) => EventRaised?.Invoke(e);

    public void Sound(string id, Vector3 pos) => Raise(WorldEvent.Sound(id, pos));

    public void Toast(string key, string arg = "", byte color = 0, int player = 0) => Raise(WorldEvent.Toast(key, arg, color, player));

    // ── Konum hesaplari (host ve istemci ayni sonucu verir) ─────────

    /// <summary>Bir istasyon yuvasinin dunya konumu.</summary>
    public static Vector3 SocketWorld(StationEntity s, SocketDef sock) => Entity.LocalToWorld(s.Position, s.Yaw, sock.Position);

    public static Vector3 PartWorld(StationEntity s, PartDef part) => Entity.LocalToWorld(s.Position, s.Yaw, part.Center);

    /// <summary>Esyanin dunyadaki konumu (yuvada/elde ise ebeveynden hesaplanir).</summary>
    public Vector3 ItemPosition(ItemEntity item)
    {
        switch (item.Attach)
        {
            case Attach.Socket when Get<StationEntity>(item.ParentId) is { } s:
            {
                var socks = StationDefs.Sockets(s);
                foreach (var sock in socks)
                {
                    if (sock.Index == item.SocketIndex)
                    {
                        return SocketWorld(s, sock);
                    }
                }

                return s.Position;
            }
            case Attach.Held when Get<PlayerEntity>(item.ParentId) is { } p:
                return HoldPoint(p);
            default:
                return item.Position;
        }
    }

    public float ItemYaw(ItemEntity item) => item.Attach switch
    {
        Attach.Socket when Get<StationEntity>(item.ParentId) is { } s => s.Yaw,
        Attach.Held when Get<PlayerEntity>(item.ParentId) is { } p => p.Yaw,
        _ => item.Yaw,
    };

    /// <summary>Uzaktan gorunen oyuncunun elindeki esya noktasi.</summary>
    public static Vector3 HoldPoint(PlayerEntity p)
    {
        var eye = p.Position + new Vector3(0, p.Crouch ? 1.0f : 1.6f, 0);
        return eye + Entity.Forward(p.Yaw) * 0.55f - new Vector3(0, 0.45f, 0);
    }

    public static Vector3 EyePosition(PlayerEntity p) => p.Position + new Vector3(0, p.Crouch ? 1.05f : 1.62f, 0);

    /// <summary>Arabanin carpisma kutusu (dinamik dunya icin).</summary>
    public static BoxCollider CartCollider(StationEntity cart)
    {
        var body = StationDefs.Bodies(cart)[0];
        var c = Entity.LocalToWorld(cart.Position, cart.Yaw, body.Center);
        return new BoxCollider(c, body.Half, cart.Yaw, body.Flags, cart.Id);
    }

    /// <summary>Dinamik carpismalari yeniler: arabalar ve dukkan kepengi.</summary>
    public void RefreshDynamicColliders()
    {
        Collision.Dynamic.Clear();
        foreach (var s in Stations)
        {
            if (s.Type == StationType.Cart)
            {
                Collision.Dynamic.Add(CartCollider(s));
            }
            else if (s.Type == StationType.Table)
            {
                foreach (var b in StationDefs.Bodies(s))
                {
                    Collision.Dynamic.Add(new BoxCollider(Entity.LocalToWorld(s.Position, s.Yaw, b.Center), b.Half, s.Yaw, b.Flags, s.Id));
                }
            }
            else if (s.Type is StationType.KazanOcagi or StationType.Fridge or StationType.Pantry or StationType.Bed or StationType.Trash)
            {
                foreach (var b in StationDefs.Bodies(s))
                {
                    Collision.Dynamic.Add(new BoxCollider(Entity.LocalToWorld(s.Position, s.Yaw, b.Center), b.Half, s.Yaw, b.Flags, s.Id));
                }
            }
        }

        if (!Progress.Has("dukkan"))
        {
            Collision.Dynamic.Add(Layout.ShopShutter);
        }
    }

    public int NetworkPeerCount { get; set; }
}
