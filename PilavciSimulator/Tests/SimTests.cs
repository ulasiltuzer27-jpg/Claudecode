using System.Numerics;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;
using PilavciSimulator.World;
using Xunit;
using Xunit.Abstractions;

namespace PilavciSimulator.Tests;

/// <summary>Simulasyon testleri: GPU yok, pencere yok; host mantigi dogrudan.</summary>
public class SimTests
{
    private readonly ITestOutputHelper _out;

    public SimTests(ITestOutputHelper output) => _out = output;

    public static (GameWorld World, Simulation Sim, PlayerEntity Player) NewGame(int seed = 42)
    {
        var data = GameData.Load(Path.Combine(TestPaths.GameDir, "Data"));
        var layout = DistrictBuilder.Build(withGeometry: false, signs: null);
        var w = new GameWorld(data, layout, isHost: true, seed);
        Simulation.InitNewGame(w);
        var sim = new Simulation(w);
        var p = sim.AddPlayer("Test", -1);
        return (w, sim, p);
    }

    [Fact]
    public void Data_loads_and_validates()
    {
        var data = GameData.Load(Path.Combine(TestPaths.GameDir, "Data"));
        Assert.NotEmpty(data.Menu);
        Assert.Equal(6, data.Spots.Count);
        Assert.All(data.Customers, c => Assert.InRange(c.Sizes.Sum(), 0.99f, 1.01f));
    }

    [Fact]
    public void District_navigation_is_connected_and_spots_have_sources()
    {
        var layout = DistrictBuilder.Build(withGeometry: false, signs: null);
        Assert.True(layout.Nav.IsConnected(), "yol grafigi kopuk");
        foreach (var s in layout.Spots)
        {
            Assert.True(s.SourceNodes.Count > 0, $"{s.Id} noktasina musteri gelmez");
        }

        // Her satis noktasinin park yeri gercekten noktanin icinde
        foreach (var s in layout.Spots)
        {
            Assert.True(s.Contains(s.CartPos), s.Id);
        }
    }

    [Fact]
    public void District_geometry_builds_without_gpu()
    {
        var layout = DistrictBuilder.Build(withGeometry: true, signs: null);
        Assert.NotNull(layout.Geometry);
        Assert.True(layout.Collision.Static.Count > 80, $"{layout.Collision.Static.Count} kutu");
    }

    [Fact]
    public void Player_can_walk_out_of_the_depot_and_up_the_curb()
    {
        var (w, _, _) = NewGame();
        var motor = new PilavciSimulator.Sim.Physics.CharacterMotor { Position = w.Layout.PlayerSpawn };
        w.RefreshDynamicColliders();
        // Kapiya dogru (guney) yuru
        for (var i = 0; i < 400; i++)
        {
            var dir = Vector2.Normalize(new Vector2(-103.8f, -6f) - new Vector2(motor.Position.X, motor.Position.Z));
            motor.Move(w.Collision, dir * 4.5f, false, 1 / 60f);
        }

        _out.WriteLine($"son konum {motor.Position}");
        Assert.True(motor.Position.Z > -8.5f, "depodan cikamadi");
        Assert.InRange(motor.Position.Y, 0.1f, 0.2f);
    }

    [Fact]
    public void Walls_stop_the_player()
    {
        var (w, _, _) = NewGame();
        var motor = new PilavciSimulator.Sim.Physics.CharacterMotor { Position = new Vector3(-104, 0.15f, -15) };
        for (var i = 0; i < 300; i++)
        {
            motor.Move(w.Collision, new Vector2(0, -4.5f), false, 1 / 60f);
        }

        Assert.True(motor.Position.Z > -21.75f, $"arka duvardan gecti: {motor.Position}");
    }

    [Fact]
    public void World_serialization_round_trips()
    {
        var (w, sim, _) = NewGame();
        for (var i = 0; i < 600; i++)
        {
            sim.Tick(1 / 30f);
        }

        var bytes = WorldSnapshot.Write(w, includePlayers: true);
        var data = GameData.Load(Path.Combine(TestPaths.GameDir, "Data"));
        var w2 = new GameWorld(data, DistrictBuilder.Build(false, null), isHost: false, 1);
        WorldSnapshot.Read(w2, bytes);
        Assert.Equal(w.Entities.Count, w2.Entities.Count);
        Assert.Equal(w.Economy.Money, w2.Economy.Money);
        Assert.Equal(w.Clock.Minute, w2.Clock.Minute);
        Assert.Equal(w.Items.Count, w2.Items.Count);
        var again = WorldSnapshot.Write(w2, includePlayers: true);
        Assert.Equal(bytes, again);
    }

    // ── Bot ile tam gun ──────────────────────────────────────────────

    private sealed class Bot
    {
        private readonly GameWorld _w;
        private readonly Simulation _sim;
        private readonly PlayerEntity _p;
        public int Failures;

        public Bot(GameWorld w, Simulation sim, PlayerEntity p)
        {
            _w = w;
            _sim = sim;
            _p = p;
        }

        public void Tick(float seconds)
        {
            for (var t = 0f; t < seconds; t += 1 / 30f)
            {
                _sim.Tick(1 / 30f);
            }
        }

        public void TickUntil(Func<bool> cond, float maxSeconds)
        {
            for (var t = 0f; t < maxSeconds && !cond(); t += 1 / 30f)
            {
                _sim.Tick(1 / 30f);
            }
        }

        private void GoTo(Vector3 p) => _p.SetMotion(p + new Vector3(0.6f, 0, 0.6f), _p.Yaw);

        public bool Do(ActionId a, TargetKind kind, int id, byte part = 0, Vector3 point = default, int param = 0, string text = "")
        {
            var ok = Interactions.Execute(_w, _p, new ActionRequest { Action = a, Kind = kind, EntityId = id, Part = part, Point = point, Param = param, Text = text });
            if (!ok)
            {
                Failures++;
            }

            return ok;
        }

        public ItemEntity Item(ItemType t) => _w.Items.First(i => i.Type == t);
        public StationEntity Station(string tag) => _w.StationByTag(tag)!;
        public ItemEntity? Held => _w.HeldBy(_p);

        public void Pick(ItemEntity it)
        {
            GoTo(_w.ItemPosition(it));
            Do(ActionId.PickUp, TargetKind.Item, it.Id);
        }

        public void PutDown()
        {
            var island = new Vector3(-104.0f + (_w.Items.Count % 5) * 0.4f, 1.05f, -16.9f);
            GoTo(island);
            Do(ActionId.PlaceSurface, TargetKind.Surface, 0, 0, island);
        }

        public void Part(string tag, byte part, ActionId a)
        {
            var st = Station(tag);
            GoTo(st.Position);
            Do(a, TargetKind.Part, st.Id, part);
        }

        public void Hold(ActionId a, int targetId, Func<bool> until, float max = 60)
        {
            _p.HoldAction = a;
            _p.HoldTargetId = targetId;
            TickUntil(until, max);
            _p.HoldAction = ActionId.None;
        }

        public void CookPilav(float kg)
        {
            var kazan = Item(ItemType.Kazan);
            var pot = kazan.Pot!;
            // Pirinc al, yika
            Pick(Item(ItemType.Suzgec));
            for (var i = 0; i < (int)kg; i++)
            {
                Part("pantry", StationDefs.PartRice, ActionId.TakeRice);
            }

            var sink = Station("sink");
            GoTo(sink.Position);
            Hold(ActionId.HoldWash, sink.Id, () => Held!.Wash >= 1f);
            PutDown();

            // Tereyagi, ates
            var ocak = Station("ocakA");
            GoTo(ocak.Position);
            Do(ActionId.KnobUp, TargetKind.Part, ocak.Id, StationDefs.PartKnob0);
            Do(ActionId.KnobUp, TargetKind.Part, ocak.Id, StationDefs.PartKnob0);
            while (pot.ButterG < kg * 100 - 1)
            {
                if (Held?.Type != ItemType.Tereyagi)
                {
                    Part("fridge", StationDefs.PartButter, ActionId.TakeButter);
                }

                GoTo(ocak.Position);
                Do(ActionId.AddButter, TargetKind.Item, kazan.Id);
            }

            if (Held is not null)
            {
                Part("trash", StationDefs.PartBin, ActionId.EmptyIntoTrash);
            }

            TickUntil(() => pot.Temp > 106, 20);
            Pick(Item(ItemType.Suzgec));
            GoTo(ocak.Position);
            Do(ActionId.PourGrain, TargetKind.Item, kazan.Id);
            PutDown();
            Pick(Item(ItemType.Kasik));
            GoTo(ocak.Position);
            Hold(ActionId.HoldStir, kazan.Id, () => pot.Toast >= 0.9f, 40);
            PutDown();

            // Su
            Pick(Item(ItemType.OlcuKabi));
            while (pot.WaterAdded < kg * 1.55f)
            {
                if (Held!.WaterL < 0.5f)
                {
                    GoTo(sink.Position);
                    Hold(ActionId.HoldFill, sink.Id, () => Held!.WaterL >= 2f, 10);
                }

                GoTo(ocak.Position);
                Do(ActionId.AddWater, TargetKind.Item, kazan.Id);
            }

            PutDown();
            Pick(Item(ItemType.TuzKutusu));
            GoTo(ocak.Position);
            while (pot.SaltG < kg * 14)
            {
                Do(ActionId.AddSalt, TargetKind.Item, kazan.Id);
            }

            PutDown();
            GoTo(ocak.Position);
            Do(ActionId.ToggleLid, TargetKind.Item, kazan.Id);
            TickUntil(() => pot.WaterL <= 0.005f, 120);
            Do(ActionId.KnobDown, TargetKind.Part, ocak.Id, StationDefs.PartKnob0);
            Do(ActionId.KnobDown, TargetKind.Part, ocak.Id, StationDefs.PartKnob0);
            TickUntil(() => pot.Food == Food.Pilav && pot.Rest >= 14, 60);
        }

        public void LoadAndGo(string spot)
        {
            var kazan = Item(ItemType.Kazan);
            var cart = _w.Carts.First();
            Pick(kazan);
            GoTo(cart.Position);
            Do(ActionId.PlaceSocket, TargetKind.Socket, cart.Id, 0);
            Do(ActionId.PushCart, TargetKind.Part, cart.Id, StationDefs.CartHandle);
            var zone = _w.Layout.Spot(spot);
            CartLogic.ApplyPushPose(_w, _p, zone.CartPos, zone.CartYaw);
            GoTo(zone.CartPos);
            Do(ActionId.ReleaseCart, TargetKind.None, 0);
            Tick(0.2f);
            Do(ActionId.ToggleCartOpen, TargetKind.Part, cart.Id, StationDefs.CartSign);
        }

        /// <summary>Zabita gelince arabayi alandan cikarir, gidince geri doner.</summary>
        public void HandleZabita(string spot)
        {
            var cart = _w.Carts.First();
            var zone = _w.Layout.Spot(spot);
            if (_w.Events.ZabitaActive && zone.Contains(cart.Position))
            {
                Do(ActionId.PushCart, TargetKind.Part, cart.Id, StationDefs.CartHandle);
                CartLogic.ApplyPushPose(_w, _p, zone.CartPos + new Vector3(14, 0, 0), zone.CartYaw);
                Do(ActionId.ReleaseCart, TargetKind.None, 0);
                Escaped++;
            }
            else if (!_w.Events.ZabitaActive && !zone.Contains(cart.Position))
            {
                Do(ActionId.PushCart, TargetKind.Part, cart.Id, StationDefs.CartHandle);
                CartLogic.ApplyPushPose(_w, _p, zone.CartPos, zone.CartYaw);
                Do(ActionId.ReleaseCart, TargetKind.None, 0);
                Tick(0.1f);
            }

            if (!cart.Cart!.Open && zone.Contains(cart.Position) && cart.Cart.ClosedUntil <= _w.Clock.Absolute)
            {
                Do(ActionId.ToggleCartOpen, TargetKind.Part, cart.Id, StationDefs.CartSign);
            }
        }

        public int Escaped;

        /// <summary>Kuyruktaki siparisi hazirlayip verir.</summary>
        public bool ServeNext()
        {
            var cart = _w.Carts.First();
            var c = _w.Customers.FirstOrDefault(x => x.State == CustomerState.Ordered && x.CartId == cart.Id);
            if (c is null)
            {
                var paying = _w.Customers.FirstOrDefault(x => x.State == CustomerState.Paying);
                if (paying is not null)
                {
                    Do(ActionId.GiveChange, TargetKind.Customer, paying.Id, param: paying.PaidAmount - paying.DueAmount);
                    return true;
                }

                return false;
            }

            var o = c.Order!;
            var kazan = Item(ItemType.Kazan);
            GoTo(cart.Position);
            Do(o.Package ? ActionId.TakePackage : ActionId.TakePlate, TargetKind.Part, cart.Id, o.Package ? StationDefs.CartPackages : StationDefs.CartPlates);
            for (var i = 0; i < o.Scoops; i++)
            {
                Do(ActionId.Scoop, TargetKind.Item, kazan.Id);
            }

            if (o.Ayran)
            {
                Do(ActionId.AddAyran, TargetKind.Part, cart.Id, StationDefs.CartCooler);
            }

            if (o.Tursu)
            {
                Do(ActionId.AddPickle, TargetKind.Part, cart.Id, StationDefs.CartPickles);
            }

            if (o.Pepper)
            {
                Do(ActionId.AddPepper, TargetKind.Part, cart.Id, StationDefs.CartPepper);
            }

            GoTo(c.Position);
            if (!Do(ActionId.Serve, TargetKind.Customer, c.Id))
            {
                Do(ActionId.EmptyIntoTrash, TargetKind.None, 0);
                return false;
            }

            if (c.State == CustomerState.Paying)
            {
                Do(ActionId.GiveChange, TargetKind.Customer, c.Id, param: c.PaidAmount - c.DueAmount);
            }

            return true;
        }
    }

    [Fact]
    public void Bot_plays_a_full_day_and_makes_profit()
    {
        var (w, sim, p) = NewGame(7);
        var bot = new Bot(w, sim, p);
        var startMoney = w.Economy.Money;
        bot.CookPilav(3);
        var pot = bot.Item(ItemType.Kazan).Pot!;
        _out.WriteLine($"pilav hazir: kalite {pot.Quality:F0}, kepce {pot.Scoops}, saat {w.Clock.Clock}, bot hatasi {bot.Failures}");
        _out.WriteLine($"  ipucu {pot.Hint} yika {pot.RiceWash:F2} yag {pot.ButterG} kavur {pot.Toast:F2} su {pot.WaterAbsorbed:F2} tuz {pot.SaltG} dem {pot.Rest:F1} yanik {pot.Burn:F2} yuksek {pot.HighHeatMinutes:F1} karistirilmadan {pot.UnstirredHotMinutes:F1}");
        Assert.Equal(Food.Pilav, pot.Food);
        Assert.True(pot.Quality >= 85, $"bot pilavi {pot.Quality}");
        Assert.Equal(0, bot.Failures);

        bot.LoadAndGo("sanayi");
        Assert.True(w.Carts.First().Cart!.Open);
        Assert.Equal("sanayi", w.Carts.First().Cart!.Spot);

        var served = 0;
        var lastHour = -1;
        while (w.Clock.Minute < 20 * 60 && !w.DayOver)
        {
            if (w.Clock.Hour != lastHour)
            {
                lastHour = w.Clock.Hour;
                var states = string.Join(" ", w.Customers.GroupBy(c => c.State).Select(g => $"{g.Key}:{g.Count()}"));
                _out.WriteLine($"  {w.Clock.Clock} talep/saat {sim.Customers.DemandPerHour(w.Carts.First(), out _):F1} kepce {bot.Item(ItemType.Kazan).Pot!.Scoops} servis {w.Economy.Today.Served} | {states}");
            }

            bot.Tick(0.5f);
            bot.HandleZabita("sanayi");
            while (bot.ServeNext())
            {
                served++;
            }

            if (bot.Item(ItemType.Kazan).Pot!.Scoops < 3 && w.Customers.All(c => c.State != CustomerState.Paying))
            {
                break;
            }
        }

        var led = w.Economy.Today;
        _out.WriteLine($"zabitadan kacis {bot.Escaped}");
        _out.WriteLine($"servis {led.Served}, kizgin {led.Angry}, pahali {led.PriceRefused}, ret {led.Refused}, ort.memnuniyet {led.AvgSatisfaction:F0}");
        _out.WriteLine($"giderler: malzeme {led.Supplies} yukseltme {led.Upgrades} ceza {led.Fines} gaz {led.Gas} kayip {led.ChangeLoss}");
        _out.WriteLine($"gelir {led.Revenue} bahsis {led.Tips} gider {led.Expenses}, para {startMoney} -> {w.Economy.Money}, yildiz {w.Reputation.Stars:F2}, xp {w.Progress.Xp} lv {w.Level}, saat {w.Clock.Clock}");
        Assert.True(led.Served >= 15, $"yalnizca {led.Served} servis");
        Assert.True(w.Economy.Money > startMoney, "kar edilmedi");
        Assert.True(led.AvgSatisfaction > 60);
        Assert.True(w.Progress.Stat("correct_change") > 0 || w.Progress.Stat("payments") > 0);
    }

    [Fact]
    public void Day_end_and_next_day_resets_the_street()
    {
        var (w, sim, p) = NewGame();
        w.Clock.Minute = w.Data.Balance.PassOutMinute - 0.1f;
        sim.Tick(0.5f);
        Assert.True(w.DayOver);
        DayLogic.StartNextDay(w);
        Assert.Equal(2, w.Clock.Day);
        Assert.False(w.DayOver);
        Assert.Empty(w.Customers);
        Assert.Equal(w.Data.Balance.DayStartMinute, w.Clock.Minute);
    }

    [Fact]
    public void Upgrades_apply_and_cart_sockets_are_remapped()
    {
        var (w, _, _) = NewGame();
        w.Economy.Money = 100000;
        w.Progress.Xp = 100000;
        var cart = w.Carts.First();
        var tencere = w.Items.First(i => i.Type == ItemType.Tencere);
        tencere.Attach = Attach.Socket;
        tencere.ParentId = cart.Id;
        tencere.SocketIndex = 1;
        Assert.True(EconomyLogic.BuyUpgrade(w, "araba_buyuk"));
        Assert.Equal(1, cart.Tier);
        Assert.Equal(2, tencere.SocketIndex);
        Assert.True(EconomyLogic.BuyUpgrade(w, "kazan_orta"));
        Assert.Equal(1, w.Items.First(i => i.Type == ItemType.Kazan).Tier);
        Assert.False(EconomyLogic.BuyUpgrade(w, "kazan_orta"));
        Assert.True(EconomyLogic.BuyUpgrade(w, "ocak_ek"));
        Assert.True(w.StationByTag("ocakB")!.Enabled);
    }

    [Fact]
    public void Buying_supplies_schedules_delivery_and_boxes_arrive()
    {
        var (w, sim, _) = NewGame();
        var money = w.Economy.Money;
        Assert.True(EconomyLogic.BuySupplies(w, "pirinc_5:2,tuz_1:1", express: true));
        Assert.True(w.Economy.Money < money);
        for (var i = 0; i < 30 * 90; i++)
        {
            sim.Tick(1 / 30f);
        }

        Assert.Equal(3, w.Items.Count(i => i.Type == ItemType.Koli));
    }

    [Fact]
    public void Payment_choice_is_never_less_than_due()
    {
        var rng = new PilavciSimulator.Core.Rng(3);
        for (var due = 1; due < 900; due += 7)
        {
            var paid = CustomerLogic.ChoosePayment(due, ref rng);
            Assert.True(paid >= due, $"{due} icin {paid}");
        }
    }

    [Fact]
    public void Wrong_order_is_scored_low()
    {
        var o = new OrderSpec { Base = Food.Pilav, Scoops = 2, Nohut = true, Ayran = true, Price = 95 };
        var good = new ServingState { Base = Food.Pilav, Scoops = 2, Nohut = true, Ayran = true, Quality = 90, Temp = 70 };
        var bad = new ServingState { Base = Food.Pilav, Scoops = 1, Quality = 60, Temp = 30 };
        var sGood = CustomerLogic.Evaluate(o, good, false, 0.1f, out _, out var dueGood);
        var sBad = CustomerLogic.Evaluate(o, bad, false, 0.9f, out var complaint, out var dueBad);
        Assert.True(sGood > 85);
        Assert.True(sBad < 30);
        Assert.Equal(95, dueGood);
        Assert.Equal(70, dueBad);
        Assert.NotEqual("say.happy", complaint);
    }
}
