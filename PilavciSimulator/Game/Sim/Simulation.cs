using System.Numerics;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Events;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Sim;

/// <summary>
/// Host simulasyonu. Her tik: saat -> basili tutulan eylemler -> pisirme
/// -> esya fizigi -> araba -> musteriler -> sokak olaylari -> egitim.
/// Tek oyunculu oyun da bunu calistirir (eslesi olmayan host).
/// </summary>
public sealed class Simulation
{
    public GameWorld World { get; }
    public CustomerSystem Customers { get; }
    public EventSystem Events { get; }

    private readonly HashSet<int> _stirring = new();
    private readonly Dictionary<int, float> _dishProgress = new();
    private float _gasAccumulator;
    private float _slowTimer;
    private readonly Dictionary<int, (Food Food, bool Burning)> _potWatch = new();

    public Simulation(GameWorld world)
    {
        World = world;
        Customers = new CustomerSystem(world);
        Events = new EventSystem(world, Customers);
    }

    /// <summary>Yeni oyun: istasyonlari ve baslangic esyalarini yerlestirir.</summary>
    public static void InitNewGame(GameWorld w)
    {
        w.Clear();
        w.NextId = 1;
        foreach (var sp in w.Layout.Stations)
        {
            var st = StationEntity.Create(sp.Type, sp.Position, sp.Yaw, sp.Tier);
            st.Tag = sp.Tag;
            if (sp.Tag == "ocakB")
            {
                st.Enabled = false;
            }

            if (st.Cart is { } c)
            {
                c.CleanPlates = w.Data.Balance.StartPlates;
                c.Ayran = 6;
                c.Tursu = 10;
                c.Packages = 10;
                c.PepperG = 100;
                c.Gas = 100;
            }

            w.Spawn(st);
        }

        foreach (var ip in w.Layout.Items)
        {
            var it = ItemEntity.Create(ip.Type);
            if (ip.SocketTag is not null && w.StationByTag(ip.SocketTag) is { } st)
            {
                it.Attach = Attach.Socket;
                it.ParentId = st.Id;
                it.SocketIndex = ip.SocketIndex;
            }
            else
            {
                it.Attach = Attach.Free;
                it.Position = ip.Position;
                it.Yaw = ip.Yaw;
            }

            w.Spawn(it);
        }

        w.Clock.Day = 1;
        w.Clock.Minute = w.Data.Balance.DayStartMinute;
        w.Reputation.Stars = 2.5f;
        EconomyLogic.InitNewGame(w);
        DayLogic.RollDay(w);
        w.Events.News.Insert(0, "news.welcome|");
        w.RefreshDynamicColliders();
        w.GlobalsDirty = true;
    }

    public PlayerEntity AddPlayer(string name, int peerId)
    {
        var w = World;
        var used = w.Players.Select(p => p.ColorIndex).ToHashSet();
        byte color = 0;
        while (used.Contains(color) && color < 3)
        {
            color++;
        }

        var offset = new Vector3(0, 0, w.Players.Count * 0.8f);
        var p = w.Spawn(new PlayerEntity
        {
            Name = name,
            ColorIndex = color,
            PeerId = peerId,
            Position = w.Layout.PlayerSpawn + offset,
            Yaw = w.Layout.PlayerSpawnYaw,
        });
        w.Toast("toast.player_joined", name, 0);
        return p;
    }

    /// <summary>Oyuncu ayrildi: elindeki esya yere, ittigi araba serbest.</summary>
    public void RemovePlayer(PlayerEntity p)
    {
        var w = World;
        if (w.HeldBy(p) is not null)
        {
            Interactions.Drop(w, p, throwIt: false);
        }

        if (p.PushingCartId != 0)
        {
            CartLogic.Release(w, p);
        }

        foreach (var c in w.Customers.Where(c => c.ServerPlayerId == p.Id))
        {
            c.ServerPlayerId = 0;
        }

        w.Toast("toast.player_left", p.Name, 1);
        w.Remove(p.Id);
    }

    public void Tick(float dt)
    {
        var w = World;
        w.Time += dt;
        if (w.DayOver)
        {
            return;
        }

        var dtMin = dt * w.Data.Balance.MinutesPerSecond;
        w.Clock.Minute += dtMin;
        if (w.Clock.Minute >= w.Data.Balance.PassOutMinute)
        {
            w.Clock.Minute = w.Data.Balance.PassOutMinute;
            DayLogic.EndDay(w, passedOut: true);
            return;
        }

        w.RefreshDynamicColliders();
        Holds(dtMin);
        Cooking(dtMin);
        ItemPhysics(dt);
        Carts(dt, dtMin);
        Customers.Update(dt, dtMin);
        Events.Update(dt, dtMin);

        _slowTimer += dt;
        if (_slowTimer >= 0.5f)
        {
            _slowTimer = 0;
            Progression.UpdateTutorial(w);
            Progression.OnTutorialStepShown(w);
            Progression.CheckAchievements(w);
            w.GlobalsDirty = true; // saat ilerliyor; istemciler 2 Hz guncel saat alsin
        }
    }

    // ── Basili tutulan eylemler ──────────────────────────────────────
    private void Holds(float dtMin)
    {
        var w = World;
        _stirring.Clear();
        foreach (var p in w.Players)
        {
            if (p.HoldAction == ActionId.None)
            {
                continue;
            }

            var held = w.HeldBy(p);
            switch (p.HoldAction)
            {
                case ActionId.HoldWash when held?.Type == ItemType.Suzgec && NearPart(p, StationType.Sink, StationDefs.PartFaucet):
                    if (held.GrainKg > 0 && !held.GrainIsBulgur && held.Wash < 1)
                    {
                        held.Wash = MathF.Min(1, held.Wash + dtMin * 0.16f);
                        held.MarkState();
                        if (held.Wash >= 1)
                        {
                            Progression.Tutorial(w, "washed");
                        }
                    }

                    break;
                case ActionId.HoldFill when held is not null && NearPart(p, StationType.Sink, StationDefs.PartFaucet):
                    if (held.Type == ItemType.OlcuKabi)
                    {
                        held.WaterL = MathF.Min(Interactions.JugCapacity, held.WaterL + dtMin * 0.6f);
                    }
                    else if (held.Pot is { } pot)
                    {
                        var add = MathF.Min(Interactions.PotWaterCapacity(held) - pot.WaterL, dtMin * 0.7f);
                        pot.WaterL += MathF.Max(0, add);
                        pot.WaterAdded += MathF.Max(0, add);
                        pot.Temp = MathF.Min(pot.Temp, 40);
                    }

                    held.MarkState();
                    break;
                case ActionId.HoldStir when held?.Type == ItemType.Kasik && w.Get<ItemEntity>(p.HoldTargetId) is { Pot: not null } potItem:
                    if (Vector3.Distance(w.ItemPosition(potItem), p.Position) < 3f)
                    {
                        _stirring.Add(potItem.Id);
                    }

                    break;
                case ActionId.HoldShred when held is null && w.Get<StationEntity>(p.HoldTargetId) is { Type: StationType.CuttingBoard } board:
                    if (board.BoardChickenKg > 0 && Vector3.Distance(board.Position, p.Position) < 3f)
                    {
                        board.BoardShred += dtMin * 0.2f;
                        board.MarkState();
                        if (board.BoardShred >= 1f)
                        {
                            FinishShred(board);
                        }
                    }

                    break;
                case ActionId.HoldWashDishes when held is null && w.Get<StationEntity>(p.HoldTargetId) is { Type: StationType.Cart } cart:
                    if (cart.Cart!.DirtyPlates > 0 && Vector3.Distance(cart.Position, p.Position) < 3.5f)
                    {
                        var prog = _dishProgress.GetValueOrDefault(cart.Id) + dtMin;
                        if (prog >= 1.2f)
                        {
                            prog = 0;
                            cart.Cart.DirtyPlates--;
                            cart.Cart.CleanPlates++;
                            cart.MarkState();
                            w.Sound("plate", cart.Position);
                        }

                        _dishProgress[cart.Id] = prog;
                    }

                    break;
            }
        }
    }

    private bool NearPart(PlayerEntity p, StationType type, byte part)
    {
        foreach (var s in World.Stations)
        {
            if (s.Type != type)
            {
                continue;
            }

            foreach (var pd in StationDefs.Parts(s))
            {
                if (pd.Id == part && Vector3.Distance(GameWorld.PartWorld(s, pd), p.Position + new Vector3(0, 1.2f, 0)) < 2.6f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void FinishShred(StationEntity board)
    {
        var w = World;
        var tray = w.Spawn(ItemEntity.Create(ItemType.TavukTepsisi));
        tray.Servings = MathF.Floor(board.BoardChickenKg * CookingModel.ChickenServingsPerKg);
        tray.Quality = board.BoardQuality;
        tray.Temp = 60f;
        if (w.ItemInSocket(board.Id, 0) is null)
        {
            tray.Attach = Attach.Socket;
            tray.ParentId = board.Id;
            tray.SocketIndex = 0;
        }
        else
        {
            tray.Attach = Attach.Free;
            tray.Position = board.Position + new Vector3(0, 0.05f, 0);
        }

        board.BoardChickenKg = 0;
        board.BoardShred = 0;
        board.MarkState();
        w.Sound("shred", board.Position);
        w.Toast("toast.tray_ready", $"{tray.Servings:0}", 2);
    }

    // ── Pisirme ──────────────────────────────────────────────────────
    private void Cooking(float dtMin)
    {
        var w = World;
        var heaterTarget = w.Progress.Has("tezgah_isitici") ? 78f : 62f;
        foreach (var it in w.Items)
        {
            switch (it.Type)
            {
                case ItemType.Kazan or ItemType.Tencere:
                {
                    var heat = HeatSource.None;
                    if (it.Attach == Attach.Socket && w.Get<StationEntity>(it.ParentId) is { } st)
                    {
                        if (st.Type is StationType.KazanOcagi or StationType.Stovetop && st.Enabled)
                        {
                            var lvl = st.Type == StationType.KazanOcagi ? st.Heat[0] : st.Heat[Math.Min(it.SocketIndex, 3)];
                            heat = new HeatSource(lvl);
                        }
                        else if (st.Type == StationType.Cart && st.Cart is { HeaterOn: true, Gas: > 0 } cart)
                        {
                            heat = new HeatSource(0, heaterTarget);
                            cart.Gas = MathF.Max(0, cart.Gas - dtMin * (w.Progress.Has("tezgah_isitici") ? 0.05f : 0.08f));
                            if (cart.Gas <= 0)
                            {
                                w.Toast("toast.cart_gas_empty", "", 1);
                                st.MarkState();
                            }
                        }
                    }

                    var pot = it.Pot!;
                    var prevFood = pot.Food;
                    var prevBurn = pot.Burn;
                    var prevTemp = pot.Temp;
                    CookingModel.Step(pot, heat, dtMin, _stirring.Contains(it.Id));
                    if (pot.Food != prevFood || MathF.Abs(pot.Temp - prevTemp) > 0.01f || pot.Burn != prevBurn || heat.Level > 0)
                    {
                        it.MarkState();
                    }

                    WatchPot(it, prevFood, prevBurn);
                    break;
                }
                case ItemType.Suzgec when it.Attach == Attach.Socket && it.GrainKg > 0 && !it.GrainIsBulgur && it.Soak < 1:
                    if (w.Get<StationEntity>(it.ParentId)?.Type == StationType.Sink)
                    {
                        it.Soak = MathF.Min(1, it.Soak + dtMin / 20f);
                        it.MarkState();
                    }

                    break;
                case ItemType.TavukTepsisi:
                {
                    var warm = it.Attach == Attach.Socket && w.Get<StationEntity>(it.ParentId) is { Type: StationType.Cart, Cart: { HeaterOn: true, Gas: > 0 } };
                    var target = warm ? heaterTarget - 8 : CookingModel.Ambient;
                    var before = it.Temp;
                    it.Temp += (target - it.Temp) * MathF.Min(1f, dtMin * (warm ? 0.05f : 0.012f));
                    if ((int)before != (int)it.Temp)
                    {
                        it.MarkState();
                    }

                    break;
                }
                case ItemType.Tabak or ItemType.PaketKap when it.Serving is { } s && !s.IsEmpty:
                {
                    var before = s.Temp;
                    s.Temp = MathF.Max(CookingModel.Ambient, s.Temp - dtMin * (it.Type == ItemType.PaketKap ? 0.5f : 1.0f));
                    if ((int)before != (int)s.Temp)
                    {
                        it.MarkState();
                    }

                    break;
                }
            }
        }

        // Dukkan/depo ocak gaz gideri
        foreach (var st in w.Stations)
        {
            if (st.Type is StationType.KazanOcagi or StationType.Stovetop)
            {
                for (var i = 0; i < StationDefs.BurnerCount(st); i++)
                {
                    _gasAccumulator += st.Heat[i] * dtMin * w.Data.Balance.GasPerBurnerMinute;
                }
            }
        }

        if (_gasAccumulator >= 1f)
        {
            var whole = (int)_gasAccumulator;
            _gasAccumulator -= whole;
            w.Economy.Today.Gas += whole;
            w.Economy.Money -= whole;
        }
    }

    private void WatchPot(ItemEntity it, Food prevFood, float prevBurn)
    {
        var w = World;
        var pot = it.Pot!;
        var pos = w.ItemPosition(it);
        if (pot.Burn >= 0.25f && prevBurn < 0.25f)
        {
            w.Toast("toast.burning", Interactions.ItemNameKey(it), 1);
            w.Sound("sizzle", pos);
        }

        if (pot.Food != prevFood)
        {
            switch (pot.Food)
            {
                case Food.Pilav or Food.BulgurPilav or Food.EtliPilav when prevFood == Food.Raw:
                    w.Progress.AddStat("kazan_cooked");
                    if (pot.Quality >= 95)
                    {
                        w.Progress.AddStat("perfect_pilav");
                    }

                    w.Toast("toast.pilav_ready", $"{pot.Quality:0}|{pot.Hint ?? ""}", 2);
                    w.Sound("bell", pos);
                    Progression.AddXp(w, 10 + (int)(pot.Quality / 10));
                    break;
                case Food.Nohut or Food.Fasulye or Food.TavukHaslama when prevFood == Food.Raw:
                    if (pot.Food == Food.Nohut)
                    {
                        w.Progress.AddStat("nohut_cooked");
                    }

                    w.Toast("toast.food_ready", $"food.{pot.Food.ToString().ToLowerInvariant()}|{pot.Quality:0}", 2);
                    w.Sound("bell", pos);
                    Progression.AddXp(w, 6);
                    break;
                case Food.Ruined when prevFood != Food.None:
                    w.Progress.AddStat("burnt_pot");
                    w.Toast("toast.ruined", pot.Hint ?? "", 1);
                    w.Sound("bad", pos);
                    w.Raise(new WorldEvent { Type = WorldEventType.Burst, Key = "smoke", Position = pos + new Vector3(0, 0.4f, 0), Arg = "" });
                    break;
            }

            Progression.CheckAchievements(w);
        }
    }

    // ── Esya fizigi ──────────────────────────────────────────────────
    private void ItemPhysics(float dt)
    {
        var w = World;
        foreach (var it in w.Items.ToList())
        {
            if (it.Attach != Attach.Free || it.Resting)
            {
                continue;
            }

            it.Velocity.Y -= 18f * dt;
            var pos = it.Position + it.Velocity * dt;
            var ground = w.Collision.GroundHeight(new Vector2(pos.X, pos.Z), 0.05f, it.Position.Y + 0.05f);
            if (pos.Y <= ground)
            {
                pos.Y = ground;
                if (MathF.Abs(it.Velocity.Y) > 2.5f)
                {
                    it.Velocity = new Vector3(it.Velocity.X * 0.4f, -it.Velocity.Y * 0.25f, it.Velocity.Z * 0.4f);
                    w.Sound("drop", pos);
                }
                else
                {
                    it.Velocity = Vector3.Zero;
                    it.Resting = true;
                    it.MarkState();
                    // Dusen tabagin icindeki yemek dokulur.
                    if (it.Serving is { } s && !s.IsEmpty)
                    {
                        s.Clear();
                        s.Dirty = true;
                    }
                }
            }

            // Yatay engel: duvara carpinca dur
            if (w.Collision.Raycast(it.Position + new Vector3(0, 0.05f, 0), Vector3.Normalize(pos - it.Position + new Vector3(0, 1e-4f, 0)), Vector3.Distance(pos, it.Position) + 0.05f,
                    Physics.ColliderFlags.Solid, out var hit) && hit.Normal.Y < 0.5f)
            {
                pos = new Vector3(it.Position.X, pos.Y, it.Position.Z);
                it.Velocity = new Vector3(0, it.Velocity.Y, 0);
            }

            if (pos.Y < -4f)
            {
                // Denize dustu: onemli araclar depoya geri gelir.
                pos = new Vector3(-104.6f, 1.06f, -16.9f);
                it.Velocity = Vector3.Zero;
                it.Resting = true;
                w.Toast("toast.item_rescued", Interactions.ItemNameKey(it), 0);
            }

            it.SetMotion(pos, it.Yaw);
        }
    }

    // ── Araba ────────────────────────────────────────────────────────
    private void Carts(float dt, float dtMin)
    {
        var w = World;
        foreach (var cart in w.Carts)
        {
            var c = cart.Cart!;
            if (c.PusherId != 0 && w.Get<PlayerEntity>(c.PusherId) is null)
            {
                c.PusherId = 0;
                cart.MarkState();
            }

            if (c.PusherId == 0)
            {
                CartLogic.UpdateSpot(w, cart);
            }

            // Cirak: bulasik yikar
            if (w.Progress.Has("cirak") && c.Open && c.DirtyPlates > 0)
            {
                var prog = _dishProgress.GetValueOrDefault(-cart.Id) + dtMin;
                if (prog >= 2f)
                {
                    prog = 0;
                    c.DirtyPlates--;
                    c.CleanPlates++;
                    cart.MarkState();
                }

                _dishProgress[-cart.Id] = prog;
            }
        }
    }
}
