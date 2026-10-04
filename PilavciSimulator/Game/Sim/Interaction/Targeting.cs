using System.Numerics;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.Sim.Interaction;

public enum TargetKind : byte
{
    None,
    Item,
    Part,
    Socket,
    Customer,
    Animal,
    Surface,
}

/// <summary>Oyuncunun baktigi sey.</summary>
public readonly record struct Target(TargetKind Kind, int EntityId, byte Part, Vector3 Point, Vector3 Normal, float Distance)
{
    public static readonly Target None = new(TargetKind.None, 0, 0, default, default, 0);
}

/// <summary>
/// Isin atarak hedef bulma. Istemci her kare kendi oyuncusu icin cagirir;
/// host da gelen istegi ayni fonksiyonla dogrulamaz (mesafe + secenek
/// kontrolu yeterli), ama ayni hacim tanimlarini kullanir.
/// </summary>
public static class Targeting
{
    public const float Reach = 2.4f;
    public const float CustomerReach = 3.2f;

    public static Target Find(GameWorld w, PlayerEntity player, Vector3 eye, Vector3 dir, ItemEntity? held)
    {
        var maxDist = CustomerReach;
        var wallDist = maxDist;
        RayHit staticHit = default;
        var hasStatic = w.Collision.Raycast(eye, dir, maxDist, ColliderFlags.BlocksRay, out staticHit, player.PushingCartId);
        if (hasStatic)
        {
            wallDist = staticHit.Distance;
        }

        var best = Target.None;
        var bestDist = wallDist + 0.05f;

        void Consider(TargetKind kind, int id, byte part, BoxCollider box, float reach)
        {
            if (box.Raycast(eye, dir, MathF.Min(bestDist, reach), out var t, out var n) && t < bestDist)
            {
                bestDist = t;
                best = new Target(kind, id, part, eye + dir * t, n, t);
            }
        }

        foreach (var it in w.Items)
        {
            if (it.Attach == Attach.Held || (held is not null && it.Id == held.Id))
            {
                continue;
            }

            var pos = w.ItemPosition(it);
            var info = it.Info;
            var half = info.HalfSize + new Vector3(0.03f);
            if (it.Type == ItemType.Kazan)
            {
                var r = ItemInfos.KazanRadius(it.Tier);
                half = new Vector3(r + 0.03f, half.Y, r + 0.03f);
            }

            Consider(TargetKind.Item, it.Id, 0, new BoxCollider(pos + new Vector3(0, half.Y, 0), half, w.ItemYaw(it)), Reach);
        }

        foreach (var s in w.Stations)
        {
            if (Vector3.DistanceSquared(s.Position, eye) > 36f)
            {
                continue;
            }

            foreach (var part in StationDefs.Parts(s))
            {
                Consider(TargetKind.Part, s.Id, part.Id, new BoxCollider(GameWorld.PartWorld(s, part), part.Half + new Vector3(0.02f), s.Yaw), Reach);
            }

            if (held is not null)
            {
                foreach (var sock in StationDefs.Sockets(s))
                {
                    if ((held.Info.Sockets & sock.Kind) == 0 || w.ItemInSocket(s.Id, sock.Index) is not null)
                    {
                        continue;
                    }

                    var c = GameWorld.SocketWorld(s, sock) + new Vector3(0, 0.08f, 0);
                    Consider(TargetKind.Socket, s.Id, sock.Index, new BoxCollider(c, new Vector3(0.2f, 0.12f, 0.2f), s.Yaw), Reach);
                }
            }
        }

        foreach (var c in w.Customers)
        {
            if (Vector3.DistanceSquared(c.Position, eye) > 25f)
            {
                continue;
            }

            Consider(TargetKind.Customer, c.Id, 0, new BoxCollider(c.Position + new Vector3(0, 0.85f, 0), new Vector3(0.32f, 0.85f, 0.32f), c.Yaw), CustomerReach);
        }

        foreach (var a in w.Animals)
        {
            if (a.Type != AnimalType.Cat)
            {
                continue;
            }

            Consider(TargetKind.Animal, a.Id, 0, new BoxCollider(a.Position + new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.2f, 0.3f), a.Yaw), Reach);
        }

        if (best.Kind != TargetKind.None)
        {
            return best;
        }

        // Yatay bir yuzey: elindekini birakmak icin.
        if (hasStatic && staticHit.Distance <= Reach && staticHit.Normal.Y > 0.7f && (staticHit.Flags & ColliderFlags.Placeable) != 0)
        {
            return new Target(TargetKind.Surface, 0, 0, staticHit.Point, staticHit.Normal, staticHit.Distance);
        }

        return Target.None;
    }
}
