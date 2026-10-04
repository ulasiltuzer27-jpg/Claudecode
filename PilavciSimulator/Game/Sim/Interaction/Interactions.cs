using System.Numerics;
using PilavciSimulator.Net;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Interaction;

/// <summary>Ekranda gosterilecek bir etkilesim secenegi.</summary>
public readonly record struct InteractionOption(ActionId Action, InputSlot Slot, string LabelKey, string Arg = "", bool Enabled = true);

/// <summary>Istemciden host'a giden istek.</summary>
public struct ActionRequest
{
    public ActionId Action;
    public TargetKind Kind;
    public int EntityId;
    public byte Part;
    public Vector3 Point;
    public int Param;
    public string Text;

    public static ActionRequest From(ActionId a, Target t) => new()
    {
        Action = a,
        Kind = t.Kind,
        EntityId = t.EntityId,
        Part = t.Part,
        Point = t.Point,
        Text = "",
    };

    public readonly Target AsTarget => new(Kind, EntityId, Part, Point, Vector3.UnitY, 0);

    public void Write(NetWriter w)
    {
        w.Byte((byte)Action);
        w.Byte((byte)Kind);
        w.Int(EntityId);
        w.Byte(Part);
        w.Vec3(Point);
        w.Int(Param);
        w.String(Text);
    }

    public static ActionRequest Read(NetReader r) => new()
    {
        Action = (ActionId)r.Byte(),
        Kind = (TargetKind)r.Byte(),
        EntityId = r.Int(),
        Part = r.Byte(),
        Point = r.Vec3(),
        Param = r.Int(),
        Text = r.Str(),
    };
}

/// <summary>
/// Oyunun tum etkilesim kurallari tek yerde. <see cref="Options"/> istemcide
/// ipucu cizer; <see cref="Execute"/> host'ta istegi AYNI kurallarla tekrar
/// kontrol edip uygular. Boylece istemci ile host'un "yapilabilir" fikri
/// asla ayrismaz.
/// </summary>
public static class Interactions
{
    public const float GrainPerClick = 1f;
    public const float ChickpeaPerClick = 0.5f;
    public const float ButterPerClick = 50f;
    public const float WaterPerClick = 0.5f;
    public const float SaltPerClick = 5f;
    public const float SuzgecCapacity = 5f;
    public const float JugCapacity = 2f;
    public const float PepperPerPlate = 2f;

    public static float PotCapacityKg(ItemEntity pot) =>
        pot.Type == ItemType.Kazan ? ItemInfos.KazanCapacityKg(pot.Tier) : ItemInfos.TencereCapacityKg;

    public static float PotWaterCapacity(ItemEntity pot) => PotCapacityKg(pot) * 3.2f;

    private static bool InDepot(GameWorld w, Vector3 p) => w.Layout.DepotArea.Contains(p, 1.5f);

    // ═══════════════════════════════════════════════════════════════
    // Secenekler
    // ═══════════════════════════════════════════════════════════════
    public static List<InteractionOption> Options(GameWorld w, PlayerEntity p, Target t)
    {
        var list = new List<InteractionOption>();
        var held = w.HeldBy(p);

        if (p.PushingCartId != 0)
        {
            list.Add(new InteractionOption(ActionId.ReleaseCart, InputSlot.Interact, "act.release_cart"));
            return list;
        }

        switch (t.Kind)
        {
            case TargetKind.Item when w.Get<ItemEntity>(t.EntityId) is { } item:
                ItemOptions(w, p, held, item, list);
                break;
            case TargetKind.Part when w.Get<StationEntity>(t.EntityId) is { } st:
                PartOptions(w, p, held, st, t.Part, list);
                break;
            case TargetKind.Socket when held is not null && w.Get<StationEntity>(t.EntityId) is { } st2:
                if (st2.Enabled && w.ItemInSocket(st2.Id, t.Part) is null)
                {
                    list.Add(new InteractionOption(ActionId.PlaceSocket, InputSlot.Interact, "act.place"));
                }
                else if (!st2.Enabled)
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.locked", "", false));
                }

                break;
            case TargetKind.Customer when w.Get<CustomerEntity>(t.EntityId) is { } c:
                CustomerOptions(w, p, held, c, list);
                break;
            case TargetKind.Animal when w.Get<AnimalEntity>(t.EntityId) is { } a && a.Type == AnimalType.Cat:
                list.Add(new InteractionOption(ActionId.ShooCat, InputSlot.Interact, "act.shoo_cat"));
                list.Add(new InteractionOption(ActionId.PetCat, InputSlot.Secondary, "act.pet_cat"));
                break;
            case TargetKind.Surface when held is not null:
                list.Add(new InteractionOption(ActionId.PlaceSurface, InputSlot.Interact, "act.place"));
                break;
        }

        return list;
    }

    private static void ItemOptions(GameWorld w, PlayerEntity p, ItemEntity? held, ItemEntity item, List<InteractionOption> list)
    {
        var pot = item.Pot;
        if (held is null)
        {
            if (item.Attach != Attach.Held)
            {
                list.Add(new InteractionOption(ActionId.PickUp, InputSlot.Interact, "act.pickup", ItemNameKey(item)));
            }

            if (pot is not null)
            {
                list.Add(new InteractionOption(ActionId.ToggleLid, InputSlot.Secondary, pot.Lid ? "act.lid_open" : "act.lid_close"));
            }

            return;
        }

        // Elde bir sey varken baska bir esyaya bakiliyor.
        if (pot is not null)
        {
            var cap = PotCapacityKg(item);
            var grain = CookingModel.Grain(pot);
            var hasFood = pot.Food is not Food.None and not Food.Raw;
            switch (held.Type)
            {
                case ItemType.Suzgec when held.GrainKg > 0:
                    if (hasFood)
                    {
                        list.Add(new InteractionOption(ActionId.None, InputSlot.Use, "act.pot_busy", "", false));
                    }
                    else
                    {
                        var fits = grain + held.GrainKg <= cap + 0.01f;
                        list.Add(new InteractionOption(ActionId.PourGrain, InputSlot.Use, fits ? "act.pour_grain" : "act.pot_full", Kg(held.GrainKg), fits));
                    }

                    break;
                case ItemType.Tereyagi:
                    list.Add(new InteractionOption(ActionId.AddButter, InputSlot.Use, "act.add_butter", $"{pot.ButterG:0}", !hasFood));
                    break;
                case ItemType.OlcuKabi:
                    var canPour = held.WaterL > 0.01f && pot.WaterL < PotWaterCapacity(item) && !hasFood;
                    list.Add(new InteractionOption(ActionId.AddWater, InputSlot.Use, held.WaterL > 0.01f ? "act.add_water" : "act.jug_empty", $"{pot.WaterL:0.0}", canPour));
                    break;
                case ItemType.TuzKutusu:
                    list.Add(new InteractionOption(ActionId.AddSalt, InputSlot.Use, "act.add_salt", $"{pot.SaltG:0}", !hasFood && w.Economy.StockOf("tuz") >= SaltPerClick / 1000f));
                    break;
                case ItemType.TavukPaketi:
                    list.Add(new InteractionOption(ActionId.AddChicken, InputSlot.Use, "act.add_chicken", Kg(held.Amount), !hasFood && pot.ChickenKg + held.Amount <= cap + 0.01f));
                    break;
                case ItemType.EtPaketi:
                    list.Add(new InteractionOption(ActionId.AddMeat, InputSlot.Use, "act.add_meat", Kg(held.Amount), !hasFood));
                    break;
                case ItemType.Kasik:
                    list.Add(new InteractionOption(ActionId.HoldStir, InputSlot.HoldUse, "act.stir"));
                    break;
                case ItemType.Tabak or ItemType.PaketKap:
                {
                    var s = held.Serving!;
                    if (CookingModel.IsPilavFood(pot.Food))
                    {
                        var ok = pot.Scoops >= 1 && s.Scoops < 3 && (s.Base == Food.None || s.Base == pot.Food) && !s.Dirty;
                        list.Add(new InteractionOption(ActionId.Scoop, InputSlot.Use, s.Scoops >= 3 ? "act.plate_full" : "act.scoop", ScoopName(s.Scoops + 1), ok));
                    }
                    else if (pot.Food is Food.Nohut or Food.Fasulye)
                    {
                        var already = pot.Food == Food.Nohut ? s.Nohut : s.Fasulye;
                        list.Add(new InteractionOption(ActionId.AddTopping, InputSlot.Use, pot.Food == Food.Nohut ? "act.add_nohut" : "act.add_fasulye", "", !already && pot.Scoops >= 1 && !s.Dirty));
                    }
                    else if (pot.Food == Food.Raw && !pot.IsEmpty)
                    {
                        list.Add(new InteractionOption(ActionId.None, InputSlot.Use, "act.not_ready", "", false));
                    }

                    break;
                }
            }

            if (held.Type is not (ItemType.Tabak or ItemType.PaketKap))
            {
                list.Add(new InteractionOption(ActionId.ToggleLid, InputSlot.Secondary, pot.Lid ? "act.lid_open" : "act.lid_close"));
            }

            return;
        }

        if (item.Type == ItemType.TavukTepsisi && held.Type is ItemType.Tabak or ItemType.PaketKap)
        {
            var s = held.Serving!;
            list.Add(new InteractionOption(ActionId.AddTopping, InputSlot.Use, "act.add_tavuk", "", !s.Tavuk && item.Servings >= 1 && !s.Dirty));
        }
    }

    private static void PartOptions(GameWorld w, PlayerEntity p, ItemEntity? held, StationEntity st, byte part, List<InteractionOption> list)
    {
        if (!st.Enabled)
        {
            list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.locked", "", false));
            return;
        }

        var eco = w.Economy;
        var level = w.Level;
        switch (st.Type)
        {
            case StationType.KazanOcagi or StationType.Stovetop when part >= StationDefs.PartKnob0 && part < StationDefs.PartKnob0 + 4:
            {
                var i = part - StationDefs.PartKnob0;
                var heat = st.Heat[i];
                list.Add(new InteractionOption(ActionId.KnobUp, InputSlot.Interact, "act.knob_up", $"{heat}", heat < 3));
                list.Add(new InteractionOption(ActionId.KnobDown, InputSlot.Secondary, "act.knob_down", $"{heat}", heat > 0));
                return;
            }
            case StationType.Sink when part == StationDefs.PartFaucet:
                if (held?.Type == ItemType.Suzgec)
                {
                    list.Add(new InteractionOption(ActionId.HoldWash, InputSlot.HoldInteract, "act.wash", $"{held.Wash * 100:0}", held.GrainKg > 0 && !held.GrainIsBulgur && held.Wash < 1f));
                }
                else if (held?.Type == ItemType.OlcuKabi || held?.Pot is not null)
                {
                    var full = held.Type == ItemType.OlcuKabi ? held.WaterL >= JugCapacity - 0.01f : held.Pot!.WaterL >= PotWaterCapacity(held) - 0.01f;
                    list.Add(new InteractionOption(ActionId.HoldFill, InputSlot.HoldInteract, "act.fill", "", !full));
                }
                else
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.need_container", "", false));
                }

                return;
            case StationType.CuttingBoard when part == StationDefs.PartBoard:
                if (held?.Pot is { Food: Food.TavukHaslama })
                {
                    var free = st.BoardChickenKg <= 0 && w.ItemInSocket(st.Id, 0) is null;
                    list.Add(new InteractionOption(ActionId.MoveChickenToBoard, InputSlot.Interact, free ? "act.chicken_to_board" : "act.board_busy", "", free));
                }
                else if (held is null && st.BoardChickenKg > 0)
                {
                    list.Add(new InteractionOption(ActionId.HoldShred, InputSlot.HoldUse, "act.shred", $"{st.BoardShred * 100:0}"));
                }

                return;
            case StationType.Fridge:
            {
                var (action, stock, amount, lvl) = part switch
                {
                    StationDefs.PartButter => (ActionId.TakeButter, "tereyagi", 0.25f, 1),
                    StationDefs.PartChicken => (ActionId.TakeChicken, "tavuk", 1f, 2),
                    _ => (ActionId.TakeMeat, "et", 0.5f, 5),
                };
                if (level < lvl)
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.need_level", $"{lvl}", false));
                }
                else if (held is not null)
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.hands_full", "", false));
                }
                else
                {
                    list.Add(new InteractionOption(action, InputSlot.Interact, "act.take_" + stock, Kg(eco.StockOf(stock)), eco.StockOf(stock) >= amount - 1e-3f));
                }

                return;
            }
            case StationType.Pantry:
                PantryOptions(w, held, part, list, level);
                return;
            case StationType.Laptop:
                list.Add(new InteractionOption(ActionId.OpenLaptop, InputSlot.Interact, "act.laptop"));
                return;
            case StationType.Bed:
            {
                var allowed = w.Clock.Minute >= w.Data.Balance.SleepAllowedMinute;
                list.Add(new InteractionOption(ActionId.Sleep, InputSlot.Interact, allowed ? (p.SleepReady ? "act.sleep_cancel" : "act.sleep") : "act.sleep_early", w.Clock.Clock, allowed));
                return;
            }
            case StationType.Trash when held is not null:
                list.Add(new InteractionOption(ActionId.EmptyIntoTrash, InputSlot.Interact, "act.trash"));
                return;
            case StationType.Cart:
                CartOptions(w, p, held, st, part, list);
                return;
        }
    }

    private static void PantryOptions(GameWorld w, ItemEntity? held, byte part, List<InteractionOption> list, int level)
    {
        var eco = w.Economy;
        if (part == StationDefs.PartStockDrop)
        {
            list.Add(held?.Type == ItemType.Koli
                ? new InteractionOption(ActionId.StockBox, InputSlot.Interact, "act.stock_box")
                : new InteractionOption(ActionId.None, InputSlot.Interact, "act.stock_info", "", false));
            return;
        }

        var (action, stock, needs, lvl) = part switch
        {
            StationDefs.PartRice => (ActionId.TakeRice, "pirinc", ItemType.Suzgec, 1),
            StationDefs.PartBulgur => (ActionId.TakeBulgur, "bulgur", ItemType.Suzgec, 3),
            StationDefs.PartChickpea => (ActionId.TakeChickpea, "nohut", ItemType.Tencere, 1),
            StationDefs.PartCanned => (ActionId.TakeCanned, "konserve_nohut", ItemType.Tencere, 1),
            _ => (ActionId.TakeBeans, "fasulye", ItemType.Tencere, 4),
        };

        var amount = needs == ItemType.Suzgec ? GrainPerClick : ChickpeaPerClick;
        if (level < lvl)
        {
            list.Add(new InteractionOption(ActionId.None, InputSlot.Use, "act.need_level", $"{lvl}", false));
            return;
        }

        if (held?.Type != needs)
        {
            list.Add(new InteractionOption(ActionId.None, InputSlot.Use, needs == ItemType.Suzgec ? "act.need_suzgec" : "act.need_tencere", Kg(eco.StockOf(stock)), false));
            return;
        }

        var enough = eco.StockOf(stock) >= amount - 1e-3f;
        bool room;
        if (needs == ItemType.Suzgec)
        {
            var isBulgur = action == ActionId.TakeBulgur;
            room = held.GrainKg + amount <= SuzgecCapacity + 0.01f && (held.GrainKg <= 0 || held.GrainIsBulgur == isBulgur);
        }
        else
        {
            var pot = held.Pot!;
            var food = pot.Food is not Food.None and not Food.Raw;
            room = !food && pot.ChickpeaKg + pot.BeansKg + amount <= ItemInfos.TencereCapacityKg + 0.01f;
        }

        list.Add(new InteractionOption(action, InputSlot.Use, "act.take_" + stock, Kg(eco.StockOf(stock)), enough && room));
    }

    private static void CartOptions(GameWorld w, PlayerEntity p, ItemEntity? held, StationEntity cart, byte part, List<InteractionOption> list)
    {
        var c = cart.Cart!;
        var inDepot = InDepot(w, cart.Position);
        var eco = w.Economy;
        switch (part)
        {
            case StationDefs.CartHandle:
                if (c.PusherId != 0 && c.PusherId != p.Id)
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.someone_pushing", "", false));
                }
                else
                {
                    list.Add(new InteractionOption(ActionId.PushCart, InputSlot.Interact, held is null ? "act.push_cart" : "act.hands_full", "", held is null));
                }

                return;
            case StationDefs.CartSign:
            {
                var closed = c.ClosedUntil > w.Clock.Absolute;
                list.Add(new InteractionOption(ActionId.ToggleCartOpen, InputSlot.Interact, c.Open ? "act.cart_close" : closed ? "act.cart_closed_zabita" : "act.cart_open", "", c.Open || !closed));
                return;
            }
            case StationDefs.CartPriceBoard:
                list.Add(new InteractionOption(ActionId.OpenPriceBoard, InputSlot.Interact, "act.price_board"));
                return;
            case StationDefs.CartPlates:
                if (held is null)
                {
                    list.Add(new InteractionOption(ActionId.TakePlate, InputSlot.Interact, "act.take_plate", $"{c.CleanPlates}", c.CleanPlates > 0));
                }
                else if (held.Type == ItemType.Tabak && held.Serving!.IsEmpty)
                {
                    list.Add(new InteractionOption(ActionId.ReturnPlate, InputSlot.Interact, "act.return_plate"));
                }

                return;
            case StationDefs.CartPackages:
                if (held is null)
                {
                    list.Add(new InteractionOption(ActionId.TakePackage, InputSlot.Interact, "act.take_package", $"{c.Packages}", c.Packages > 0));
                }
                else if (inDepot)
                {
                    list.Add(new InteractionOption(ActionId.LoadPackages, InputSlot.Interact, "act.load_packages", $"{eco.StockOf("paket"):0}", eco.StockOf("paket") >= 1));
                }

                if (held is null && inDepot)
                {
                    list.Add(new InteractionOption(ActionId.LoadPackages, InputSlot.Secondary, "act.load_packages", $"{eco.StockOf("paket"):0}", eco.StockOf("paket") >= 1));
                }

                return;
            case StationDefs.CartDirtyBin:
                list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.dirty_info", $"{c.DirtyPlates}", false));
                return;
            case StationDefs.CartBucket:
                list.Add(new InteractionOption(ActionId.HoldWashDishes, InputSlot.HoldInteract, "act.wash_dishes", $"{c.DirtyPlates}", held is null && c.DirtyPlates > 0));
                return;
            case StationDefs.CartCooler:
                if (held?.Serving is { } s1)
                {
                    list.Add(new InteractionOption(ActionId.AddAyran, InputSlot.Use, "act.add_ayran", $"{c.Ayran}", !s1.Ayran && c.Ayran > 0));
                }

                if (inDepot)
                {
                    list.Add(new InteractionOption(ActionId.LoadCooler, InputSlot.Interact, "act.load_cooler", $"{eco.StockOf("ayran"):0}", eco.StockOf("ayran") >= 1 && c.Ayran < CoolerCapacity(w)));
                }
                else if (held is null)
                {
                    list.Add(new InteractionOption(ActionId.None, InputSlot.Interact, "act.cooler_info", $"{c.Ayran}", false));
                }

                return;
            case StationDefs.CartPickles:
                if (held?.Serving is { } s2)
                {
                    list.Add(new InteractionOption(ActionId.AddPickle, InputSlot.Use, "act.add_pickle", $"{c.Tursu}", !s2.Tursu && c.Tursu > 0));
                }

                if (inDepot)
                {
                    list.Add(new InteractionOption(ActionId.LoadPickles, InputSlot.Interact, "act.load_pickles", $"{eco.StockOf("tursu"):0}", eco.StockOf("tursu") >= 1));
                }

                return;
            case StationDefs.CartPepper:
                if (held?.Serving is { } s3)
                {
                    list.Add(new InteractionOption(ActionId.AddPepper, InputSlot.Use, "act.add_pepper", "", !s3.Pepper && c.PepperG >= PepperPerPlate));
                }

                if (inDepot)
                {
                    list.Add(new InteractionOption(ActionId.LoadPepper, InputSlot.Interact, "act.load_pepper", Kg(eco.StockOf("karabiber")), eco.StockOf("karabiber") >= 0.05f));
                }

                return;
            case StationDefs.CartHeater:
                list.Add(new InteractionOption(ActionId.ToggleHeater, InputSlot.Interact, c.HeaterOn ? "act.heater_off" : "act.heater_on", $"{c.Gas:0}"));
                return;
            case StationDefs.CartGas:
                list.Add(inDepot
                    ? new InteractionOption(ActionId.LoadGas, InputSlot.Interact, "act.load_gas", $"{c.Gas:0}", eco.StockOf("gaz") >= 1 && c.Gas < 99)
                    : new InteractionOption(ActionId.None, InputSlot.Interact, "act.gas_info", $"{c.Gas:0}", false));
                return;
            case StationDefs.CartCashBox:
            {
                var waiting = w.Customers.FirstOrDefault(cu => cu.State == CustomerState.Paying && cu.CartId == cart.Id);
                list.Add(new InteractionOption(ActionId.OpenCashBox, InputSlot.Interact, "act.cashbox", "", waiting is not null));
                return;
            }
        }
    }

    private static void CustomerOptions(GameWorld w, PlayerEntity p, ItemEntity? held, CustomerEntity c, List<InteractionOption> list)
    {
        if (c.State == CustomerState.Ordered)
        {
            if (held?.Serving is { } s && !s.IsEmpty && !s.Dirty)
            {
                list.Add(new InteractionOption(ActionId.Serve, InputSlot.Use, "act.serve"));
            }

            list.Add(new InteractionOption(ActionId.RefuseOrder, InputSlot.Secondary, "act.refuse"));
        }
        else if (c.State == CustomerState.Paying)
        {
            list.Add(new InteractionOption(ActionId.OpenCashBox, InputSlot.Interact, "act.give_change", $"{c.PaidAmount - c.DueAmount}"));
        }
    }

    public static int CoolerCapacity(GameWorld w) => w.Progress.Has("ayran_dolabi") ? 36 : 12;

    public static string ItemNameKey(ItemEntity item) => item.Type switch
    {
        ItemType.Kazan => "item.kazan",
        _ => item.Info.NameKey,
    };

    public static string Kg(float kg) => kg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    public static string ScoopName(int scoops) => scoops switch
    {
        1 => "size.yarim",
        2 => "size.tam",
        _ => "size.duble",
    };

    // ═══════════════════════════════════════════════════════════════
    // Uygulama (yalnizca host)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Istegi dogrulayip uygular. Gecersizse false (istemcinin gordugu
    /// durum bir kare eski olabilir; bu normal, sessizce yok sayilir).
    /// </summary>
    public static bool Execute(GameWorld w, PlayerEntity p, ActionRequest req)
    {
        switch (req.Action)
        {
            case ActionId.Drop:
                return Drop(w, p, throwIt: false);
            case ActionId.Throw:
                return Drop(w, p, throwIt: true);
            case ActionId.GiveChange:
                return CustomerLogic.CompletePayment(w, p, w.Get<CustomerEntity>(req.EntityId), req.Param);
            case ActionId.SetPrice:
                return EconomyLogic.SetPrice(w, req.Text, req.Param);
            case ActionId.BuySupplies:
                return EconomyLogic.BuySupplies(w, req.Text, req.Param == 1);
            case ActionId.BuyUpgrade:
                return EconomyLogic.BuyUpgrade(w, req.Text);
            case ActionId.SelectCosmetic:
                return EconomyLogic.SelectPaint(w, req.Text);
            case ActionId.Chat:
                p.Chat = req.Text.Length > 80 ? req.Text[..80] : req.Text;
                p.ChatUntil = w.Time + 6f;
                p.MarkState();
                w.Raise(new WorldEvent { Type = WorldEventType.Chat, Key = p.Name, Arg = p.Chat, PlayerId = 0 });
                return true;
            case ActionId.SleepVote:
                p.SleepReady = !p.SleepReady;
                p.MarkState();
                return true;
            case ActionId.ReleaseCart:
                return CartLogic.Release(w, p);
        }

        var target = req.AsTarget;
        if (req.Kind == TargetKind.Surface)
        {
            target = new Target(TargetKind.Surface, 0, 0, req.Point, Vector3.UnitY, 0);
        }

        // Mesafe: oyuncu hedefin yakininda olmali (gecikmeye tolerans payi).
        if (!InRange(w, p, req))
        {
            return false;
        }

        var options = Options(w, p, target);
        var ok = false;
        foreach (var o in options)
        {
            if (o.Action == req.Action && o.Enabled)
            {
                ok = true;
                break;
            }
        }

        if (!ok)
        {
            return false;
        }

        var held = w.HeldBy(p);
        p.UseCounter++;
        p.MarkState();
        switch (req.Action)
        {
            case ActionId.PickUp:
                return PickUp(w, p, w.Get<ItemEntity>(req.EntityId)!);
            case ActionId.PlaceSocket:
                return PlaceSocket(w, p, held!, w.Get<StationEntity>(req.EntityId)!, req.Part);
            case ActionId.PlaceSurface:
                return PlaceSurface(w, p, held!, req.Point);
            case ActionId.ToggleLid:
            {
                var pot = w.Get<ItemEntity>(req.EntityId)!;
                pot.Pot!.Lid = !pot.Pot.Lid;
                pot.MarkState();
                w.Sound("lid", w.ItemPosition(pot));
                return true;
            }
            case ActionId.KnobUp or ActionId.KnobDown:
            {
                var st = w.Get<StationEntity>(req.EntityId)!;
                var i = req.Part - StationDefs.PartKnob0;
                var before = st.Heat[i];
                st.Heat[i] = (byte)Math.Clamp(st.Heat[i] + (req.Action == ActionId.KnobUp ? 1 : -1), 0, 3);
                st.MarkState();
                w.Sound(before == 0 && st.Heat[i] > 0 ? "ignite" : "knob", st.Position);
                Progression.Tutorial(w, "knob");
                return true;
            }
            default:
                return CookingActions.Execute(w, p, held, req);
        }
    }

    private static bool InRange(GameWorld w, PlayerEntity p, ActionRequest req)
    {
        Vector3 at;
        switch (req.Kind)
        {
            case TargetKind.Item when w.Get<ItemEntity>(req.EntityId) is { } i:
                at = w.ItemPosition(i);
                break;
            case TargetKind.Part or TargetKind.Socket when w.Get<StationEntity>(req.EntityId) is { } s:
                at = s.Position;
                break;
            case TargetKind.Customer when w.Get<CustomerEntity>(req.EntityId) is { } c:
                at = c.Position;
                break;
            case TargetKind.Animal when w.Get<AnimalEntity>(req.EntityId) is { } a:
                at = a.Position;
                break;
            case TargetKind.Surface:
                at = req.Point;
                break;
            default:
                return false;
        }

        return Vector3.Distance(at, p.Position) < 5.5f;
    }

    public static bool PickUp(GameWorld w, PlayerEntity p, ItemEntity item)
    {
        if (p.HeldItemId != 0 || item.Attach == Attach.Held)
        {
            return false;
        }

        item.Attach = Attach.Held;
        item.ParentId = p.Id;
        item.Resting = true;
        item.Velocity = Vector3.Zero;
        item.MarkState();
        p.HeldItemId = item.Id;
        p.MarkState();
        w.Sound("pickup", p.Position);
        Progression.Tutorial(w, "pickup:" + item.Type);
        return true;
    }

    public static bool PlaceSocket(GameWorld w, PlayerEntity p, ItemEntity item, StationEntity st, byte socket)
    {
        item.Attach = Attach.Socket;
        item.ParentId = st.Id;
        item.SocketIndex = socket;
        item.MarkState();
        p.HeldItemId = 0;
        p.MarkState();
        w.Sound(item.Type is ItemType.Kazan or ItemType.Tencere ? "lid" : "place", GameWorld.SocketWorld(st, StationDefs.Sockets(st)[socket]));
        Progression.Tutorial(w, $"socket:{item.Type}:{st.Type}");
        return true;
    }

    public static bool PlaceSurface(GameWorld w, PlayerEntity p, ItemEntity item, Vector3 point)
    {
        item.Attach = Attach.Free;
        item.ParentId = 0;
        item.SetMotion(point + new Vector3(0, 0.005f, 0), p.Yaw);
        item.Resting = true;
        item.Velocity = Vector3.Zero;
        item.MarkState();
        p.HeldItemId = 0;
        p.MarkState();
        w.Sound("place", point);
        return true;
    }

    public static bool Drop(GameWorld w, PlayerEntity p, bool throwIt)
    {
        var item = w.HeldBy(p);
        if (item is null)
        {
            return false;
        }

        item.Attach = Attach.Free;
        item.ParentId = 0;
        var fwd = CameraForward(p);
        item.SetMotion(GameWorld.EyePosition(p) + fwd * 0.5f - new Vector3(0, 0.3f, 0), p.Yaw);
        item.Velocity = throwIt ? fwd * 6f + new Vector3(0, 2f, 0) : fwd * 1.2f;
        item.Resting = false;
        item.MarkState();
        p.HeldItemId = 0;
        p.MarkState();
        return true;
    }

    public static Vector3 CameraForward(PlayerEntity p)
    {
        var cp = MathF.Cos(p.Pitch);
        return new Vector3(-MathF.Sin(p.Yaw) * cp, MathF.Sin(p.Pitch), -MathF.Cos(p.Yaw) * cp);
    }
}
