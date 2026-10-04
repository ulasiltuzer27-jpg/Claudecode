using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Navigation;
using PilavciSimulator.World;

namespace PilavciSimulator.Sim.Customers;

/// <summary>
/// Musteri ve yaya yapay zekasi (yalnizca host).
///
/// Iki nufus var: sokakta dolasan yayalar (hayat katar, acik bir arabanin
/// yanindan gecerken bazen musteriye donusur) ve satis noktasina dogrudan
/// gelen musteriler. Talep: noktanin saatlik egrisi x itibar x hava x fiyat
/// x taninirlik x etkinlikler.
/// </summary>
public sealed class CustomerSystem
{
    /// <summary>customers.json'daki sabir degerlerinin oyun dakikasina carpani.</summary>
    public const float PatienceScale = 20f;
    public const int MaxCustomers = 70;
    public const int MaxQueue = 6;
    public const int TargetStrollers = 16;

    private readonly GameWorld _w;
    private float _spawnAccumulator;
    private float _strollTimer;
    private int _ticket;
    private readonly Dictionary<int, int> _tickets = new();

    public CustomerSystem(GameWorld w) => _w = w;

    public static void Say(GameWorld w, CustomerEntity c, string key, string arg, float seconds)
    {
        c.BubbleKey = key;
        c.BubbleArg = arg;
        c.BubbleUntil = w.Time + seconds;
        c.MarkState();
    }

    public static void Leave(GameWorld w, CustomerEntity c)
    {
        c.State = CustomerState.Leaving;
        c.QueueIndex = -1;
        c.Path.Clear();
        c.Timer = 0;
        c.MarkState();
    }

    public void Update(float dt, float dtMin)
    {
        var w = _w;
        SpawnCustomers(dtMin);
        SpawnStrollers(dt);
        UpdateQueues();

        foreach (var c in w.Customers.ToList())
        {
            if (c.TypeId is "zabita" or "cirak")
            {
                continue;
            }

            UpdateOne(c, dt, dtMin);
        }

        Separate(dt);
    }

    // ── Dogum ───────────────────────────────────────────────────────

    public float DemandPerHour(StationEntity cart, out SpotZone? zone)
    {
        var w = _w;
        zone = null;
        var c = cart.Cart!;
        if (!c.Open || c.PusherId != 0 || c.Spot.Length == 0 || !w.Data.SpotById.TryGetValue(c.Spot, out var spot))
        {
            return 0;
        }

        zone = w.Layout.Spots.FirstOrDefault(s => s.Id == c.Spot);
        var hour = w.Clock.Hour;
        var rate = spot.BaseDemand * spot.Hours[hour] * spot.Days[w.Clock.DayOfWeek];
        if (spot.MatchDayOnly)
        {
            rate = spot.BaseDemand * spot.Hours[hour] * (w.Events.MatchToday ? 1f : 0.06f);
        }

        rate *= 0.45f + w.Reputation.Stars * 0.22f;
        rate *= 0.8f + 0.45f * w.Reputation.FameOf(spot.Id);
        if (w.Weather.Rain > 0.2f)
        {
            rate *= w.Progress.Has("semsiye") ? 0.85f : 0.55f;
        }

        if (hour >= 19 && w.Progress.Has("isikli_tabela"))
        {
            rate *= 1.35f;
        }

        if (w.Progress.CatMascot)
        {
            rate *= 1.06f;
        }

        // Fiyat seviyesi: menudeki ortalama fiyat / referans
        var sum = 0f;
        var n = 0;
        foreach (var m in w.Data.Menu)
        {
            if (m.Kind != "plate" || m.Level > w.Level)
            {
                continue;
            }

            sum += w.Economy.PriceOf(m.Id) / MathF.Max(1, Economy.EconomyLogic.ReferencePrice(w, m.Id));
            n++;
        }

        var ratio = n > 0 ? sum / n : 1f;
        rate *= Math.Clamp(1f + (1f - ratio) * spot.PriceSensitivity * 1.2f, 0.3f, 1.5f);
        return rate;
    }

    private void SpawnCustomers(float dtMin)
    {
        var w = _w;
        if (w.Customers.Count >= MaxCustomers)
        {
            return;
        }

        foreach (var cart in w.Carts)
        {
            var rate = DemandPerHour(cart, out var zone);
            if (rate <= 0 || zone is null)
            {
                continue;
            }

            var queue = w.Customers.Count(c => c.CartId == cart.Id && c.State is CustomerState.Queue or CustomerState.Ordered);
            var approaching = w.Customers.Count(c => c.CartId == cart.Id && c.State == CustomerState.Approach);
            if (queue >= MaxQueue || approaching >= 6)
            {
                continue;
            }

            _spawnAccumulator += rate / 60f * dtMin;
            while (_spawnAccumulator >= 1f)
            {
                _spawnAccumulator -= 1f;
                if (w.Rng.NextFloat() < 0.9f)
                {
                    SpawnCustomerFor(cart, zone);
                }
            }
        }
    }

    private CustomerEntity? SpawnCustomerFor(StationEntity cart, SpotZone zone)
    {
        var w = _w;
        if (!w.Data.SpotById.TryGetValue(zone.Id, out var spot) || zone.SourceNodes.Count == 0)
        {
            return null;
        }

        var type = PickType(spot.Mix);
        var nav = w.Layout.Nav;
        // Kaynak: arabanin cok dibinde olmayan ama uzak da olmayan bir kapi/uc.
        // Yuruyus suresi oyun dakikasi olarak pahali (1 sn = 1 dk), bu yuzden yakin kaynaklar tercih edilir.
        var sources = zone.SourceNodes.Where(i =>
        {
            var d = Vector3.Distance(nav.Nodes[i], cart.Position);
            return d > 8f && d < 30f;
        }).ToList();
        if (sources.Count == 0)
        {
            sources = zone.SourceNodes.OrderBy(i => Vector3.Distance(nav.Nodes[i], cart.Position)).Take(3).ToList();
        }

        var start = sources[w.Rng.Range(0, sources.Count)];
        var c = CreateCustomer(type, nav.Nodes[start]);
        c.SpotId = zone.Id;
        StartApproach(c, cart);
        return c;
    }

    private string PickType(Dictionary<string, float> mix)
    {
        var total = mix.Values.Sum();
        var r = _w.Rng.NextFloat() * total;
        foreach (var (k, v) in mix)
        {
            r -= v;
            if (r <= 0)
            {
                return k;
            }
        }

        return mix.Keys.First();
    }

    private CustomerEntity CreateCustomer(string type, Vector3 at)
    {
        var w = _w;
        var def = w.Data.CustomerById[type];
        var c = w.Spawn(new CustomerEntity
        {
            TypeId = type,
            Seed = w.Rng.NextUInt(),
            Position = at,
            Speed = def.Speed * w.Rng.Range(0.9f, 1.1f),
            Patience = 1f,
            State = CustomerState.Stroll,
            Anim = CustomerAnim.Walk,
        });
        c.Position.Y = Ground(at);
        _tickets[c.Id] = _ticket++;
        return c;
    }

    private void SpawnStrollers(float dt)
    {
        var w = _w;
        _strollTimer -= dt;
        if (_strollTimer > 0)
        {
            return;
        }

        _strollTimer = 1.5f;
        var hour = w.Clock.Hour;
        var target = hour < 7 || hour >= 23 ? 3 : hour >= 21 ? 8 : TargetStrollers;
        if (w.Weather.Rain > 0.3f)
        {
            target /= 2;
        }

        var strollers = w.Customers.Count(c => c.State == CustomerState.Stroll);
        if (strollers >= target || w.Customers.Count >= MaxCustomers)
        {
            return;
        }

        var nav = w.Layout.Nav;
        var ends = Enumerable.Range(0, nav.Nodes.Count).Where(i => nav.Tags[i] != NavTag.None).ToList();
        var a = ends[w.Rng.Range(0, ends.Count)];
        var b = ends[w.Rng.Range(0, ends.Count)];
        if (a == b)
        {
            return;
        }

        var types = w.Data.Customers;
        var c = CreateCustomer(types[w.Rng.Range(0, types.Count)].Id, nav.Nodes[a]);
        c.Path = nav.FindPath(a, b);
        c.PathIndex = 0;
    }

    /// <summary>Vapurdan inen yolcular (EventSystem cagirir).</summary>
    public void SpawnFerryPassengers(int count)
    {
        var w = _w;
        var nav = w.Layout.Nav;
        var door = nav.Nearest(w.Layout.PierDoor);
        var ends = Enumerable.Range(0, nav.Nodes.Count).Where(i => nav.Tags[i] == NavTag.Edge || (nav.Tags[i] == NavTag.Door && nav.Areas[i] != "iskele")).ToList();
        var iskeleCart = w.Carts.FirstOrDefault(c => c.Cart is { Open: true, Spot: "iskele" });
        for (var i = 0; i < count && w.Customers.Count < MaxCustomers; i++)
        {
            var type = PickType(w.Data.SpotById["iskele"].Mix);
            var c = CreateCustomer(type, nav.Nodes[door] + new Vector3(w.Rng.Range(-1f, 1f), 0, w.Rng.Range(-0.5f, 0.5f)));
            c.SpotId = "iskele";
            if (iskeleCart is not null && w.Rng.Chance(0.35f + w.Reputation.Stars * 0.05f))
            {
                StartApproach(c, iskeleCart);
            }
            else
            {
                c.Path = nav.FindPath(door, ends[w.Rng.Range(0, ends.Count)]);
            }
        }
    }

    private void StartApproach(CustomerEntity c, StationEntity cart)
    {
        var w = _w;
        var nav = w.Layout.Nav;
        c.CartId = cart.Id;
        c.State = CustomerState.Approach;
        c.Anim = CustomerAnim.Walk;
        var from = nav.Nearest(c.Position);
        var to = nav.Nearest(cart.Position);
        c.Path = nav.FindPath(from, to);
        c.PathIndex = 0;
        c.WaitedMinutes = 0;
        c.Patience = 1f;
        c.MarkState();
    }

    // ── Kuyruk ──────────────────────────────────────────────────────

    private void UpdateQueues()
    {
        var w = _w;
        foreach (var cart in w.Carts)
        {
            var list = w.Customers
                .Where(c => c.CartId == cart.Id && c.State is CustomerState.Approach or CustomerState.Queue or CustomerState.Ordered)
                .OrderBy(c => c.State == CustomerState.Ordered ? -1 : _tickets.GetValueOrDefault(c.Id))
                .ToList();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].QueueIndex != i)
                {
                    list[i].QueueIndex = i;
                    list[i].MarkState();
                }
            }

            // Araba kapandi ya da gitti: kuyruk dagilir.
            var c0 = cart.Cart!;
            if (!c0.Open || c0.PusherId != 0)
            {
                foreach (var cu in list)
                {
                    Say(w, cu, "say.closed", "", 3f);
                    Leave(w, cu);
                }
            }
        }
    }

    // ── Tek musteri ─────────────────────────────────────────────────

    private void UpdateOne(CustomerEntity c, float dt, float dtMin)
    {
        var w = _w;
        var cart = w.Get<StationEntity>(c.CartId);
        var type = w.Data.CustomerById.GetValueOrDefault(c.TypeId);
        var patienceMinutes = (type?.Patience ?? 6f) * PatienceScale;
        switch (c.State)
        {
            case CustomerState.Stroll:
            {
                if (FollowPath(c, dt))
                {
                    w.Remove(c.Id);
                    return;
                }

                // Acik bir arabanin yanindan geciyorsa ugrayabilir.
                c.Timer += dt;
                if (c.Timer > 1f)
                {
                    c.Timer = 0;
                    foreach (var k in w.Carts)
                    {
                        var kc = k.Cart!;
                        if (kc.Open && kc.PusherId == 0 && Vector3.DistanceSquared(k.Position, c.Position) < 64f &&
                            w.Customers.Count(x => x.CartId == k.Id && x.State is CustomerState.Approach or CustomerState.Queue or CustomerState.Ordered) < MaxQueue &&
                            w.Rng.Chance(0.06f + w.Reputation.Stars * 0.012f))
                        {
                            c.SpotId = kc.Spot;
                            StartApproach(c, k);
                            Say(w, c, "say.smell", "", 3f);
                            break;
                        }
                    }
                }

                break;
            }
            case CustomerState.Approach:
            {
                if (cart is null)
                {
                    Leave(w, c);
                    return;
                }

                var slot = StationDefs.QueueSlot(cart, Math.Max(0, c.QueueIndex));
                if (c.PathIndex < c.Path.Count && Vector3.DistanceSquared(c.Position, slot) > 36f)
                {
                    FollowPath(c, dt);
                }
                else if (MoveTo(c, slot, dt, 0.15f))
                {
                    c.State = CustomerState.Queue;
                    c.Anim = CustomerAnim.Idle;
                    c.MarkState();
                }

                c.WaitedMinutes += dtMin * 0.3f;
                break;
            }
            case CustomerState.Queue:
            {
                if (cart is null)
                {
                    Leave(w, c);
                    return;
                }

                var slot = StationDefs.QueueSlot(cart, c.QueueIndex);
                if (MoveTo(c, slot, dt, 0.15f))
                {
                    FaceTowards(c, cart.Position, dt);
                    c.Anim = CustomerAnim.Idle;
                }

                c.WaitedMinutes += dtMin * 0.6f;
                Drain(c, dtMin * 0.6f / patienceMinutes);
                if (c.QueueIndex == 0 && Vector3.DistanceSquared(c.Position, slot) < 0.2f)
                {
                    var order = CustomerLogic.CreateOrder(w, c, cart, out var reason);
                    if (order is null)
                    {
                        if (reason == "say.expensive")
                        {
                            w.Economy.Today.PriceRefused++;
                        }

                        Say(w, c, reason ?? "say.nothing", "", 4f);
                        c.Mood = Mood.Neutral;
                        Leave(w, c);
                        return;
                    }

                    c.Order = order;
                    c.State = CustomerState.Ordered;
                    c.Anim = CustomerAnim.Talk;
                    c.Timer = 0;
                    var voice = type?.Voice ?? "casual";
                    Say(w, c, $"say.order.{voice}.{w.Rng.Range(0, 4)}", "", 999f);
                    w.Sound("bell", c.Position);
                }

                if (c.Patience <= 0)
                {
                    Angry(c, "say.too_long");
                }

                break;
            }
            case CustomerState.Ordered:
            {
                if (cart is null)
                {
                    Leave(w, c);
                    return;
                }

                FaceTowards(c, cart.Position, dt);
                c.Timer += dt;
                if (c.Timer > 2.5f && c.Anim == CustomerAnim.Talk)
                {
                    c.Anim = CustomerAnim.Idle;
                    c.MarkState();
                }

                c.WaitedMinutes += dtMin;
                Drain(c, dtMin / patienceMinutes);
                if (c.Patience < 0.3f && c.BubbleKey.StartsWith("say.order", StringComparison.Ordinal) && w.Rng.Chance(dt * 0.1f))
                {
                    c.Anim = CustomerAnim.Angry;
                    c.MarkState();
                }

                if (c.Patience <= 0)
                {
                    Angry(c, "say.too_long");
                }

                break;
            }
            case CustomerState.Paying:
            {
                if (cart is not null)
                {
                    FaceTowards(c, cart.Position, dt);
                }

                Drain(c, dtMin * 0.35f / patienceMinutes);
                if (c.Patience <= 0)
                {
                    // Para ustunu alamadan gitti: para kasada kaldi ama itibar gitti.
                    w.Economy.Money += c.PaidAmount;
                    w.Economy.Today.Revenue += c.PaidAmount;
                    c.PaidAmount = 0;
                    w.GlobalsDirty = true;
                    CustomerLogic.ReputationAdd(w, 5, c.SpotId, 1.5f);
                    Angry(c, "say.no_change");
                }

                break;
            }
            case CustomerState.ToEat:
            {
                if (cart is null || c.HoldsPackage)
                {
                    Leave(w, c);
                    return;
                }

                c.Patience = 1f;
                var target = EatPosition(c, cart);
                if (MoveTo(c, target, dt, 0.2f))
                {
                    c.State = CustomerState.Eating;
                    c.Anim = c.SeatTableId != 0 ? CustomerAnim.Sit : CustomerAnim.Eat;
                    c.Timer = 0;
                    c.MarkState();
                }

                break;
            }
            case CustomerState.Eating:
            {
                c.Timer += dtMin;
                if (cart is not null)
                {
                    FaceTowards(c, cart.Position, dt * 0.3f);
                }

                if (w.Rng.Chance(dt * 0.4f))
                {
                    w.Sound("eat", c.Position);
                }

                // Iskelede martilar
                if (c.SpotId == "iskele" && c.HoldsPlate && w.Rng.Chance(dtMin * 0.012f))
                {
                    c.HoldsPlate = false;
                    w.Progress.AddStat("seagull_thefts");
                    w.Sound("seagull", c.Position);
                    Say(w, c, "say.seagull", "", 5f);
                    c.Anim = CustomerAnim.Angry;
                    c.MarkState();
                    Progression.CheckAchievements(w);
                    w.Raise(new WorldEvent { Type = WorldEventType.Burst, Key = "seagull", Position = c.Position + new Vector3(0, 1.4f, 0), Arg = "" });
                }

                if (c.Timer > 22f + (c.Seed % 15))
                {
                    if (c.SeatTableId != 0 && w.Get<StationEntity>(c.SeatTableId) is { } table)
                    {
                        table.SeatMask &= ~(1 << c.EatIndex);
                        c.SeatTableId = 0;
                    }

                    c.State = c.HoldsPlate ? CustomerState.ReturnPlate : CustomerState.Leaving;
                    c.Anim = CustomerAnim.Walk;
                    c.MarkState();
                    if (c.LastSatisfaction >= 80 && w.Rng.Chance(0.4f))
                    {
                        Say(w, c, "say.bye_happy", "", 3f);
                    }
                }

                break;
            }
            case CustomerState.ReturnPlate:
            {
                if (cart is null)
                {
                    Leave(w, c);
                    return;
                }

                var bin = Entity.LocalToWorld(cart.Position, cart.Yaw, new Vector3(-StationDefs.CartHalfLength(cart.Tier) + 0.25f, 0, -0.95f));
                if (MoveTo(c, bin, dt, 0.35f))
                {
                    cart.Cart!.DirtyPlates++;
                    cart.MarkState();
                    c.HoldsPlate = false;
                    w.Sound("plate", bin);
                    Leave(w, c);
                }

                break;
            }
            case CustomerState.Leaving:
            {
                if (c.Path.Count == 0 || c.PathIndex >= c.Path.Count)
                {
                    if (c.Timer > 0 && c.PathIndex >= c.Path.Count && c.Path.Count > 0)
                    {
                        w.Remove(c.Id);
                        return;
                    }

                    var nav = w.Layout.Nav;
                    var ends = Enumerable.Range(0, nav.Nodes.Count).Where(i => nav.Tags[i] != NavTag.None).ToList();
                    var far = ends.OrderByDescending(i => Vector3.DistanceSquared(nav.Nodes[i], c.Position) * (0.5f + (Rng.Hash((uint)i, c.Seed) & 255) / 255f)).First();
                    c.Path = nav.FindPath(nav.Nearest(c.Position), far);
                    c.PathIndex = 0;
                    c.Timer = 1;
                    c.Anim = CustomerAnim.Walk;
                    c.MarkState();
                    if (c.Path.Count == 0)
                    {
                        w.Remove(c.Id);
                        return;
                    }
                }

                if (FollowPath(c, dt))
                {
                    w.Remove(c.Id);
                }

                break;
            }
        }
    }

    private Vector3 EatPosition(CustomerEntity c, StationEntity cart)
    {
        var w = _w;
        // Dukkanda: bos sandalye
        if (cart.Cart!.Spot == "dukkan" && c.SeatTableId == 0)
        {
            foreach (var t in w.Stations.Where(s => s.Type == StationType.Table))
            {
                for (var seat = 0; seat < 2; seat++)
                {
                    if ((t.SeatMask & (1 << seat)) == 0)
                    {
                        t.SeatMask |= 1 << seat;
                        c.SeatTableId = t.Id;
                        c.EatIndex = seat;
                        break;
                    }
                }

                if (c.SeatTableId != 0)
                {
                    break;
                }
            }
        }

        if (c.SeatTableId != 0 && w.Get<StationEntity>(c.SeatTableId) is { } table)
        {
            return table.Position + new Vector3(c.EatIndex == 0 ? -0.75f : 0.75f, 0, 0);
        }

        if (c.EatIndex == 0)
        {
            c.EatIndex = 1 + (int)(Rng.Hash(c.Seed) % 6);
        }

        return StationDefs.EatSpot(cart, c.EatIndex);
    }

    private void Angry(CustomerEntity c, string key)
    {
        var w = _w;
        w.Economy.Today.Angry++;
        c.Mood = Mood.Angry;
        c.Anim = CustomerAnim.Angry;
        Say(w, c, key, "", 4f);
        CustomerLogic.ReputationAdd(w, 0, c.SpotId, 1f);
        w.Sound("bad", c.Position);
        Leave(w, c);
    }

    private static void Drain(CustomerEntity c, float amount)
    {
        var before = c.Patience;
        c.Patience = MathF.Max(0, c.Patience - amount);
        if ((int)(before * 20) != (int)(c.Patience * 20))
        {
            c.MarkState();
        }
    }

    // ── Hareket ─────────────────────────────────────────────────────

    /// <summary>Yolu izler; sona varinca true.</summary>
    private bool FollowPath(CustomerEntity c, float dt)
    {
        if (c.PathIndex >= c.Path.Count)
        {
            return true;
        }

        var target = c.Path[c.PathIndex];
        if (MoveTo(c, target, dt, 0.6f))
        {
            c.PathIndex++;
        }

        return c.PathIndex >= c.Path.Count;
    }

    private bool MoveTo(CustomerEntity c, Vector3 target, float dt, float tolerance)
    {
        var to = new Vector3(target.X - c.Position.X, 0, target.Z - c.Position.Z);
        var dist = to.Length();
        if (dist <= tolerance)
        {
            if (c.Anim == CustomerAnim.Walk)
            {
                c.Anim = CustomerAnim.Idle;
                c.MarkState();
            }

            return true;
        }

        var step = MathF.Min(dist, c.Speed * dt);
        var dir = to / dist;
        var pos = c.Position + dir * step;
        pos.Y = Ground(pos);
        var yaw = Entity.LerpAngle(c.Yaw, Entity.YawFromDirection(dir), MathF.Min(1f, dt * 8f));
        c.SetMotion(pos, yaw);
        if (c.Anim != CustomerAnim.Walk)
        {
            c.Anim = CustomerAnim.Walk;
            c.MarkState();
        }

        return false;
    }

    private void FaceTowards(CustomerEntity c, Vector3 p, float dt)
    {
        var d = p - c.Position;
        d.Y = 0;
        if (d.LengthSquared() < 0.01f)
        {
            return;
        }

        var yaw = Entity.LerpAngle(c.Yaw, Entity.YawFromDirection(Vector3.Normalize(d)), MathF.Min(1f, dt * 6f));
        if (MathF.Abs(yaw - c.Yaw) > 0.001f)
        {
            c.SetMotion(c.Position, yaw);
        }
    }

    private float Ground(Vector3 p) => _w.Collision.GroundHeight(new Vector2(p.X, p.Z), 0.15f, p.Y + 0.4f);

    /// <summary>Basit ayrisma: ust uste binmesinler.</summary>
    private void Separate(float dt)
    {
        var list = _w.Customers;
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.State is CustomerState.Eating or CustomerState.Queue or CustomerState.Ordered or CustomerState.Paying)
            {
                continue;
            }

            for (var j = 0; j < list.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var b = list[j];
                var d = new Vector2(a.Position.X - b.Position.X, a.Position.Z - b.Position.Z);
                var l2 = d.LengthSquared();
                if (l2 < 0.36f && l2 > 1e-5f)
                {
                    var push = d / MathF.Sqrt(l2) * (0.6f - MathF.Sqrt(l2)) * 2f * dt;
                    a.SetMotion(a.Position + new Vector3(push.X, 0, push.Y), a.Yaw);
                }
            }
        }
    }
}
