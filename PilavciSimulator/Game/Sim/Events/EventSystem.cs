using System.Numerics;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Events;

/// <summary>
/// Sokagin olaylari (yalnizca host): hava, zabita baskini, toptanci
/// kamyoneti, vapur, sokak kedisi, martilar ve gecen arabalar.
/// </summary>
public sealed class EventSystem
{
    private readonly GameWorld _w;
    private readonly CustomerSystem _customers;
    private float _zabitaCooldownUntil;
    private float _catCheck;
    private readonly List<Delivery> _vanCargo = new();
    private float _trafficTimer;

    public EventSystem(GameWorld w, CustomerSystem customers)
    {
        _w = w;
        _customers = customers;
    }

    public void Update(float dt, float dtMin)
    {
        Weather(dtMin);
        Zabita(dt, dtMin);
        Deliveries(dt);
        Ferry(dt);
        Cat(dt, dtMin);
        Seagulls(dt);
        Traffic(dt);
    }

    // ── Hava ────────────────────────────────────────────────────────
    private void Weather(float dtMin)
    {
        var wt = _w.Weather;
        var m = _w.Clock.Minute;
        var target = wt.Kind == WeatherKind.Rain && m >= wt.RainStart && m <= wt.RainEnd ? 1f : 0f;
        var before = wt.Rain;
        wt.Rain += (target - wt.Rain) * MathF.Min(1f, dtMin * 0.05f);
        if (MathF.Abs(before - wt.Rain) > 0.001f && (int)(before * 20) != (int)(wt.Rain * 20))
        {
            _w.GlobalsDirty = true;
        }
    }

    // ── Zabita ──────────────────────────────────────────────────────
    private void Zabita(float dt, float dtMin)
    {
        var w = _w;
        var ev = w.Events;
        var now = w.Clock.Absolute;
        if (!ev.ZabitaActive)
        {
            if (now < _zabitaCooldownUntil || w.Clock.Hour < 8 || w.Clock.Hour >= 22)
            {
                return;
            }

            foreach (var cart in w.Carts)
            {
                var c = cart.Cart!;
                if (!c.Open || c.Spot.Length == 0 || !w.Data.SpotById.TryGetValue(c.Spot, out var spot))
                {
                    continue;
                }

                if (spot.License is null || w.Progress.Has(spot.License))
                {
                    continue;
                }

                if (w.Rng.Chance(spot.ZabitaRisk / 60f * dtMin))
                {
                    StartZabita(cart, c.Spot);
                    return;
                }
            }

            return;
        }

        // Baskin suruyor
        var target = w.Carts.FirstOrDefault();
        var van = w.Get<VehicleEntity>(ev.ZabitaVanId);
        if (van is not null)
        {
            DriveVan(van, dt);
        }

        if (target is null)
        {
            EndZabita(escaped: true);
            return;
        }

        var stillThere = target.Cart!.Spot == ev.ZabitaSpot && target.Cart.PusherId == 0;
        var zone = w.Layout.Spots.FirstOrDefault(s => s.Id == ev.ZabitaSpot);
        var away = zone is null || !zone.Contains(target.Position, 2.5f);
        if (away)
        {
            w.Progress.AddStat("zabita_escaped");
            w.Toast("toast.zabita_escaped", "", 2);
            Progression.CheckAchievements(w);
            EndZabita(escaped: true);
            return;
        }

        if (now >= ev.ZabitaDeadline && stillThere)
        {
            var fine = (int)(w.Data.Balance.ZabitaFine * w.Economy.SupplyIndex);
            w.Economy.Money -= fine;
            w.Economy.Today.Fines += fine;
            target.Cart.ClosedUntil = now + 60;
            CartLogic.SetOpen(w, target, false);
            w.Reputation.Stars = MathF.Max(0.5f, w.Reputation.Stars - 0.15f);
            w.Progress.AddStat("zabita_fined");
            w.Toast("toast.zabita_fined", $"{fine}", 1);
            w.Sound("whistle", target.Position);
            Progression.CheckAchievements(w);
            EndZabita(escaped: false);
        }
    }

    private void StartZabita(StationEntity cart, string spot)
    {
        var w = _w;
        var ev = w.Events;
        ev.ZabitaActive = true;
        ev.ZabitaSpot = spot;
        ev.ZabitaDeadline = w.Clock.Absolute + 50;
        var fromEast = cart.Position.X > 0;
        var lane = fromEast ? w.Layout.RoadLaneNorth : w.Layout.RoadLaneSouth;
        var start = new Vector3(fromEast ? w.Layout.RoadMaxX + 12 : w.Layout.RoadMinX - 12, 0, lane);
        var van = w.Spawn(new VehicleEntity { Type = VehicleType.ZabitaVan, Position = start, Yaw = fromEast ? MathF.PI : 0f, Lights = true, Speed = 9f });
        van.Target = new Vector3(cart.Position.X + (fromEast ? 7 : -7), 0, lane);
        ev.ZabitaVanId = van.Id;
        w.GlobalsDirty = true;
        w.Raise(new WorldEvent { Type = WorldEventType.Zabita, Value = 1, Key = spot, Position = cart.Position, Arg = "" });
        w.Sound("whistle", cart.Position);
    }

    private void EndZabita(bool escaped)
    {
        var w = _w;
        var ev = w.Events;
        ev.ZabitaActive = false;
        _zabitaCooldownUntil = w.Clock.Absolute + 120;
        if (w.Get<VehicleEntity>(ev.ZabitaVanId) is { } van)
        {
            van.State = 2;
            van.Target = new Vector3(van.Position.X < 0 ? w.Layout.RoadMinX - 15 : w.Layout.RoadMaxX + 15, 0, van.Position.Z);
            van.MarkState();
        }

        w.GlobalsDirty = true;
        w.Raise(new WorldEvent { Type = WorldEventType.Zabita, Value = escaped ? 2 : 3, Key = ev.ZabitaSpot, Arg = "" });
    }

    private void DriveVan(VehicleEntity van, float dt)
    {
        var to = van.Target - van.Position;
        to.Y = 0;
        var d = to.Length();
        if (d < 0.3f)
        {
            if (van.State == 2)
            {
                _w.Remove(van.Id);
            }

            return;
        }

        var speed = MathF.Min(van.Speed, d * 1.2f + 0.5f);
        var dir = to / d;
        van.SetMotion(van.Position + dir * MathF.Min(d, speed * dt), Entity.YawFromDirection(dir) + MathF.PI / 2);
    }

    // ── Teslimat ────────────────────────────────────────────────────
    private void Deliveries(float dt)
    {
        var w = _w;
        var due = EconomyLogic.DueDeliveries(w);
        if (due.Count > 0)
        {
            _vanCargo.AddRange(due);
            if (!w.Vehicles.Any(v => v.Type == VehicleType.DeliveryVan))
            {
                var van = w.Spawn(new VehicleEntity
                {
                    Type = VehicleType.DeliveryVan, Position = w.Layout.VanEntry, Yaw = MathF.PI, Speed = 10f, Target = w.Layout.VanStop,
                });
                van.Seed = w.Rng.NextUInt();
            }
        }

        foreach (var van in w.Vehicles.Where(v => v.Type == VehicleType.DeliveryVan).ToList())
        {
            switch (van.State)
            {
                case 0:
                    DriveVan(van, dt);
                    if (Vector3.DistanceSquared(van.Position, van.Target) < 0.2f)
                    {
                        van.State = 1;
                        van.Timer = 0;
                        van.MarkState();
                        w.Sound("van_horn", van.Position);
                        EconomyLogic.SpawnBoxes(w, _vanCargo);
                        _vanCargo.Clear();
                        w.Toast("toast.delivery_arrived", "", 2);
                    }

                    break;
                case 1:
                    van.Timer += dt;
                    if (van.Timer > 4f)
                    {
                        van.State = 2;
                        van.Target = w.Layout.VanExit;
                        van.MarkState();
                    }

                    break;
                default:
                    DriveVan(van, dt);
                    break;
            }
        }
    }

    // ── Vapur ───────────────────────────────────────────────────────
    private void Ferry(float dt)
    {
        var w = _w;
        var ev = w.Events;
        var ferry = w.Get<VehicleEntity>(ev.FerryId);
        if (ferry is null)
        {
            ferry = w.Spawn(new VehicleEntity { Type = VehicleType.Ferry, Position = w.Layout.FerryFar, State = 0 });
            ev.FerryId = ferry.Id;
            w.GlobalsDirty = true;
        }

        var dock = w.Layout.FerryDock;
        var far = w.Layout.FerryFar;
        switch (ferry.State)
        {
            case 0: // bekliyor (uzakta)
                if (w.Clock.Absolute >= ev.NextFerry && w.Clock.Hour >= 7 && w.Clock.Hour < 22)
                {
                    ferry.State = 1;
                    ferry.MarkState();
                }

                break;
            case 1: // geliyor
                if (MoveFerry(ferry, dock, dt))
                {
                    ferry.State = 2;
                    ferry.Timer = 0;
                    ferry.MarkState();
                    w.Sound("horn", dock);
                }

                break;
            case 2: // iskelede
                ferry.Timer += dt;
                if (ferry.Timer > 2f && ferry.Timer - dt <= 2f)
                {
                    var count = 3 + w.Rng.Range(0, 6) + (w.Clock.Hour is 8 or 18 or 19 ? 4 : 0);
                    _customers.SpawnFerryPassengers(count);
                }

                if (ferry.Timer > 12f)
                {
                    ferry.State = 3;
                    ferry.MarkState();
                    w.Sound("horn", dock);
                }

                break;
            default: // gidiyor
                if (MoveFerry(ferry, far, dt))
                {
                    ferry.State = 0;
                    ev.NextFerry = w.Clock.Absolute + 40;
                    ferry.MarkState();
                }

                break;
        }
    }

    private static bool MoveFerry(VehicleEntity f, Vector3 target, float dt)
    {
        var to = target - f.Position;
        var d = to.Length();
        if (d < 0.5f)
        {
            f.SetMotion(target, f.Yaw);
            return true;
        }

        var speed = MathF.Min(14f, d * 0.25f + 1f);
        var dir = to / d;
        f.SetMotion(f.Position + dir * MathF.Min(d, speed * dt), Entity.LerpAngle(f.Yaw, Entity.YawFromDirection(dir), MathF.Min(1, dt)));
        return false;
    }

    // ── Kedi ────────────────────────────────────────────────────────
    private void Cat(float dt, float dtMin)
    {
        var w = _w;
        var ev = w.Events;
        var cat = w.Get<AnimalEntity>(ev.CatId);
        var cart = w.Carts.FirstOrDefault();
        if (cat is null)
        {
            if (w.Progress.CatMascot && cart is not null)
            {
                cat = w.Spawn(new AnimalEntity { Type = AnimalType.Cat, Position = cart.Position + new Vector3(1.5f, 0, 1f), Seed = 7, Mascot = true, State = AnimalState.Sit });
                ev.CatId = cat.Id;
                w.GlobalsDirty = true;
                return;
            }

            _catCheck += dtMin;
            if (_catCheck < 45f || cart is null || !cart.Cart!.Open || w.Layout.CatSpawns.Count == 0)
            {
                return;
            }

            _catCheck = 0;
            if (!w.Rng.Chance(0.45f))
            {
                return;
            }

            var spawn = w.Layout.CatSpawns.OrderBy(p => Vector3.DistanceSquared(p, cart.Position)).First();
            if (Vector3.Distance(spawn, cart.Position) > 60f)
            {
                spawn = cart.Position + new Vector3(w.Rng.Range(-14f, 14f), 0, -9f);
            }

            cat = w.Spawn(new AnimalEntity { Type = AnimalType.Cat, Position = spawn, Seed = w.Rng.NextUInt(), State = AnimalState.Walk });
            ev.CatId = cat.Id;
            w.GlobalsDirty = true;
            w.Sound("meow", spawn);
            return;
        }

        cat.Timer += dt;
        if (cart is null)
        {
            return;
        }

        var side = Entity.LocalToWorld(cart.Position, cart.Yaw, new Vector3(StationDefs.CartHalfLength(cart.Tier) + 0.5f, 0, 0.55f));
        switch (cat.State)
        {
            case AnimalState.Walk or AnimalState.Sneak:
            {
                var tray = w.Items.FirstOrDefault(i => i.Type == ItemType.TavukTepsisi && i.Attach == Attach.Socket && i.ParentId == cart.Id && i.Servings >= 1);
                var goal = tray is not null && !cat.Mascot ? side : cart.Position + new Vector3(2.2f, 0, 1.2f);
                var arrived = Walk(cat, goal, dt, cat.State == AnimalState.Sneak ? 0.9f : 1.3f);
                if (Vector3.Distance(cat.Position, cart.Position) < 6 && cat.State == AnimalState.Walk && !cat.Mascot)
                {
                    cat.State = AnimalState.Sneak;
                    cat.MarkState();
                }

                if (arrived)
                {
                    if (tray is not null && !cat.Mascot)
                    {
                        cat.State = AnimalState.Eat;
                        cat.Timer = 0;
                        cat.MarkState();
                    }
                    else
                    {
                        cat.State = AnimalState.Sit;
                        cat.MarkState();
                    }
                }

                break;
            }
            case AnimalState.Eat:
                if (cat.Timer > 4f)
                {
                    var tray = w.Items.FirstOrDefault(i => i.Type == ItemType.TavukTepsisi && i.Attach == Attach.Socket && i.ParentId == cart.Id && i.Servings >= 1);
                    if (tray is not null)
                    {
                        tray.Servings -= 1;
                        tray.MarkState();
                        w.Toast("toast.cat_stole", "", 1);
                        w.Sound("meow", cat.Position);
                    }

                    cat.State = AnimalState.Flee;
                    cat.Timer = 0;
                    cat.MarkState();
                }

                break;
            case AnimalState.Purr:
                if (cat.Timer > 18f)
                {
                    cat.State = cat.Mascot ? AnimalState.Sit : AnimalState.Flee;
                    cat.Timer = 0;
                    cat.MarkState();
                }

                break;
            case AnimalState.Sit:
                if (cat.Mascot)
                {
                    var spot = cart.Position + Entity.Right(cart.Yaw) * (StationDefs.CartHalfLength(cart.Tier) + 0.9f) + Entity.Forward(cart.Yaw) * -0.9f;
                    if (Vector3.Distance(cat.Position, spot) > 2.5f)
                    {
                        Walk(cat, spot, dt, 2.4f);
                    }
                }
                else if (cat.Timer > 30f)
                {
                    cat.State = AnimalState.Walk;
                    cat.Timer = 0;
                    cat.MarkState();
                }

                break;
            case AnimalState.Flee:
            {
                var away = cat.Position + Vector3.Normalize(cat.Position - cart.Position + new Vector3(0.01f, 0, 0.01f)) * 30f;
                Walk(cat, away, dt, 3.5f);
                if (cat.Timer > 6f)
                {
                    w.Remove(cat.Id);
                    ev.CatId = 0;
                    w.GlobalsDirty = true;
                }

                break;
            }
        }
    }

    private bool Walk(AnimalEntity a, Vector3 target, float dt, float speed)
    {
        var to = target - a.Position;
        to.Y = 0;
        var d = to.Length();
        if (d < 0.25f)
        {
            return true;
        }

        var dir = to / d;
        var pos = a.Position + dir * MathF.Min(d, speed * dt);
        pos.Y = _w.Collision.GroundHeight(new Vector2(pos.X, pos.Z), 0.1f, pos.Y + 0.4f);
        a.SetMotion(pos, Entity.LerpAngle(a.Yaw, Entity.YawFromDirection(dir), MathF.Min(1, dt * 6)));
        return false;
    }

    // ── Martilar (iskelede dolasan suru) ────────────────────────────
    private void Seagulls(float dt)
    {
        var w = _w;
        var gulls = w.Animals.Where(a => a.Type == AnimalType.Seagull).ToList();
        if (gulls.Count < 4)
        {
            var g = w.Spawn(new AnimalEntity { Type = AnimalType.Seagull, Seed = w.Rng.NextUInt(), State = AnimalState.Fly });
            g.Position = w.Layout.FerryDock + new Vector3(w.Rng.Range(-20f, 20f), 9, w.Rng.Range(-20f, 10f));
            return;
        }

        foreach (var g in gulls)
        {
            // Iskele uzerinde daire ciz, bazen bir korkuluga kon.
            g.Timer += dt;
            var center = w.Layout.FerryDock + new Vector3(-6, 0, -18);
            var phase = (g.Seed % 628) / 100f + g.Timer * (0.25f + (g.Seed % 7) * 0.03f);
            var radius = 14f + (g.Seed % 9);
            var perch = w.Layout.SeagullPerches[(int)(g.Seed % (uint)w.Layout.SeagullPerches.Count)];
            var sitting = (int)(g.Timer / 20f + g.Seed % 3) % 3 == 0;
            Vector3 target;
            if (sitting)
            {
                target = perch;
                if (g.State != AnimalState.Sit && Vector3.Distance(g.Position, perch) < 0.3f)
                {
                    g.State = AnimalState.Sit;
                    g.MarkState();
                }
            }
            else
            {
                target = center + new Vector3(MathF.Cos(phase) * radius, 8 + MathF.Sin(phase * 2.3f) * 2, MathF.Sin(phase) * radius);
                if (g.State != AnimalState.Fly)
                {
                    g.State = AnimalState.Fly;
                    g.MarkState();
                    if (w.Rng.Chance(0.3f))
                    {
                        w.Sound("seagull", g.Position);
                    }
                }
            }

            var to = target - g.Position;
            var d = to.Length();
            if (d > 0.05f)
            {
                var step = MathF.Min(d, 7f * dt);
                g.SetMotion(g.Position + to / d * step, Entity.LerpAngle(g.Yaw, Entity.YawFromDirection(to / d), MathF.Min(1, dt * 3)));
            }
        }
    }

    // ── Trafik ──────────────────────────────────────────────────────
    private void Traffic(float dt)
    {
        var w = _w;
        _trafficTimer -= dt;
        var cars = w.Vehicles.Where(v => v.Type == VehicleType.Car).ToList();
        var hour = w.Clock.Hour;
        var wanted = hour < 6 ? 1 : hour < 8 ? 4 : hour < 21 ? 6 : 3;
        if (_trafficTimer <= 0 && cars.Count < wanted)
        {
            _trafficTimer = 4f;
            var east = w.Rng.Chance(0.5f);
            var lane = east ? w.Layout.RoadLaneSouth : w.Layout.RoadLaneNorth;
            var x = east ? w.Layout.RoadMinX - 10 : w.Layout.RoadMaxX + 10;
            var car = w.Spawn(new VehicleEntity
            {
                Type = VehicleType.Car, Position = new Vector3(x, 0, lane), Yaw = east ? 0f : MathF.PI, Seed = w.Rng.NextUInt(), Speed = w.Rng.Range(7f, 10f),
                Lights = hour >= 19 || hour < 7,
            });
            car.Target = new Vector3(east ? w.Layout.RoadMaxX + 15 : w.Layout.RoadMinX - 15, 0, lane);
        }

        foreach (var car in cars)
        {
            var dir = car.Target.X > car.Position.X ? 1f : -1f;
            // Onunde biri var mi (oyuncu, musteri, araba, baska arac)?
            var blocked = false;
            var aheadMin = car.Position.X + dir * 2.4f;
            var aheadMax = car.Position.X + dir * 8f;
            var lo = MathF.Min(aheadMin, aheadMax);
            var hi = MathF.Max(aheadMin, aheadMax);
            bool InLane(Vector3 p) => p.X > lo && p.X < hi && MathF.Abs(p.Z - car.Position.Z) < 1.6f && p.Y < 1f;
            foreach (var p in w.Players)
            {
                blocked |= InLane(p.Position);
            }

            foreach (var c in w.Customers)
            {
                blocked |= InLane(c.Position);
            }

            foreach (var s in w.Carts)
            {
                blocked |= InLane(s.Position);
            }

            foreach (var v in w.Vehicles)
            {
                if (v != car && v.Type is VehicleType.Car or VehicleType.DeliveryVan or VehicleType.ZabitaVan)
                {
                    blocked |= InLane(v.Position);
                }
            }

            car.Timer = blocked ? MathF.Max(0, car.Timer - dt * 12f) : MathF.Min(car.Speed, car.Timer + dt * 4f);
            if (car.Timer > 0.01f)
            {
                car.SetMotion(car.Position + new Vector3(dir * car.Timer * dt, 0, 0), car.Yaw);
            }

            if (MathF.Abs(car.Position.X - car.Target.X) < 1f)
            {
                w.Remove(car.Id);
            }

            if (car.Lights != (hour >= 19 || hour < 7))
            {
                car.Lights = hour >= 19 || hour < 7;
                car.MarkState();
            }
        }
    }
}
