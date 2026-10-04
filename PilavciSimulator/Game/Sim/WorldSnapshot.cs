using PilavciSimulator.Net;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim;

/// <summary>
/// Dunyanin tam ikili goruntusu. Hem kayit dosyasi hem co-op'a katilan
/// oyuncuya gonderilen "tam durum" bu bicimi kullanir; tek serilestirici,
/// tek gidis-donus testi (Tests/SimTests.World_serialization_round_trips).
/// </summary>
public static class WorldSnapshot
{
    /// <summary>Bicim surumu. Alan eklerken artirin ve Read'de eski surumu destekleyin.</summary>
    public const int Version = 1;

    public static Entity Create(EntityKind kind) => kind switch
    {
        EntityKind.Player => new PlayerEntity(),
        EntityKind.Item => new ItemEntity(),
        EntityKind.Station => new StationEntity(),
        EntityKind.Customer => new CustomerEntity(),
        EntityKind.Vehicle => new VehicleEntity(),
        EntityKind.Animal => new AnimalEntity(),
        _ => throw new InvalidDataException($"bilinmeyen varlik turu {kind}"),
    };

    public static void WriteGlobals(GameWorld w, NetWriter wr)
    {
        w.Clock.Write(wr);
        w.Weather.Write(wr);
        w.Economy.Write(wr);
        w.Progress.Write(wr);
        w.Reputation.Write(wr);
        w.Events.Write(wr);
        wr.Bool(w.DayOver);
    }

    public static void ReadGlobals(GameWorld w, NetReader r)
    {
        w.Clock.Read(r);
        w.Weather.Read(r);
        w.Economy.Read(r);
        w.Progress.Read(r);
        w.Reputation.Read(r);
        w.Events.Read(r);
        w.DayOver = r.Bool();
    }

    public static void WriteEntity(Entity e, NetWriter wr)
    {
        wr.Byte((byte)e.Kind);
        wr.Int(e.Id);
        e.WriteMotion(wr);
        e.WriteState(wr);
    }

    public static Entity ReadEntity(NetReader r)
    {
        var kind = (EntityKind)r.Byte();
        var e = Create(kind);
        e.Id = r.Int();
        e.ReadMotion(r);
        e.ReadState(r);
        return e;
    }

    /// <param name="includePlayers">Kayitta oyuncular yazilmaz (her acilista depoda dogarlar).</param>
    /// <param name="transient">Musteri, arac, hayvan gibi gecici varliklar (ag icin evet, kayit icin hayir).</param>
    public static byte[] Write(GameWorld w, bool includePlayers, bool transient = true)
    {
        var wr = new NetWriter(64 * 1024);
        wr.Int(Version);
        WriteGlobals(w, wr);
        wr.Int(w.NextId);
        wr.UInt(w.Rng.State);
        var list = w.Entities.Values.Where(e =>
            (includePlayers || e.Kind != EntityKind.Player) &&
            (transient || e.Kind is EntityKind.Item or EntityKind.Station or EntityKind.Player || e is AnimalEntity { Mascot: true })).ToList();
        wr.VarInt(list.Count);
        foreach (var e in list)
        {
            WriteEntity(e, wr);
        }

        return wr.ToArray();
    }

    public static void Read(GameWorld w, byte[] data)
    {
        var r = new NetReader(data);
        var version = r.Int();
        if (version > Version)
        {
            throw new InvalidDataException($"kayit surumu {version} bu oyundan yeni ({Version})");
        }

        ReadGlobals(w, r);
        var nextId = r.Int();
        w.Rng.State = r.UInt();
        w.Clear();
        var n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            var e = ReadEntity(r);
            w.Add(e);
        }

        w.NextId = Math.Max(w.NextId, nextId);
        w.RefreshDynamicColliders();
    }
}
