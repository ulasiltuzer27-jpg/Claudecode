using System.Numerics;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Interaction;

/// <summary>
/// Mutfak ve servis eylemlerinin uygulanmasi. Dogrulama
/// <see cref="Interactions.Options"/>'ta yapildi; burasi yalnizca
/// durumu degistirir, ses ve bildirim olaylarini yayar.
/// </summary>
public static class CookingActions
{
    public static bool Execute(GameWorld w, PlayerEntity p, ItemEntity? held, ActionRequest req)
    {
        var eco = w.Economy;
        var target = w.Get<ItemEntity>(req.EntityId);
        var station = w.Get<StationEntity>(req.EntityId);
        var pos = p.Position;
        switch (req.Action)
        {
            // ── Kilerden kaba ────────────────────────────────────────
            case ActionId.TakeRice or ActionId.TakeBulgur:
            {
                var stock = req.Action == ActionId.TakeRice ? "pirinc" : "bulgur";
                if (!eco.TakeStock(stock, Interactions.GrainPerClick))
                {
                    return false;
                }

                var s = held!;
                var old = s.GrainKg;
                s.GrainKg += Interactions.GrainPerClick;
                s.GrainIsBulgur = req.Action == ActionId.TakeBulgur;
                // Yeni (yikanmamis) pirinc ortalamayi dusurur.
                s.Wash = old > 0 ? s.Wash * old / s.GrainKg : 0;
                s.Soak = old > 0 ? s.Soak * old / s.GrainKg : 0;
                s.MarkState();
                w.Sound("pour_rice", pos);
                w.GlobalsDirty = true;
                Progression.Tutorial(w, "rice");
                return true;
            }
            case ActionId.TakeChickpea or ActionId.TakeCanned or ActionId.TakeBeans:
            {
                var stock = req.Action switch
                {
                    ActionId.TakeChickpea => "nohut",
                    ActionId.TakeCanned => "konserve_nohut",
                    _ => "fasulye",
                };
                if (!eco.TakeStock(stock, Interactions.ChickpeaPerClick))
                {
                    return false;
                }

                var pot = held!.Pot!;
                if (req.Action == ActionId.TakeBeans)
                {
                    var old = pot.BeansKg;
                    pot.BeansKg += Interactions.ChickpeaPerClick;
                    pot.BeansSoak = old > 0 ? pot.BeansSoak * old / pot.BeansKg : 0;
                }
                else
                {
                    var old = pot.ChickpeaKg;
                    pot.ChickpeaKg += Interactions.ChickpeaPerClick;
                    var newSoak = req.Action == ActionId.TakeCanned ? 1f : 0f;
                    pot.ChickpeaSoak = (pot.ChickpeaSoak * old + newSoak * Interactions.ChickpeaPerClick) / pot.ChickpeaKg;
                    if (req.Action == ActionId.TakeCanned)
                    {
                        pot.Canned = true;
                    }
                }

                CookingModel.UpdateResult(pot);
                held.MarkState();
                w.Sound("pour_rice", pos);
                w.GlobalsDirty = true;
                return true;
            }
            case ActionId.TakeButter or ActionId.TakeChicken or ActionId.TakeMeat:
            {
                var (stock, amount, type) = req.Action switch
                {
                    ActionId.TakeButter => ("tereyagi", 0.25f, ItemType.Tereyagi),
                    ActionId.TakeChicken => ("tavuk", 1f, ItemType.TavukPaketi),
                    _ => ("et", 0.5f, ItemType.EtPaketi),
                };
                if (!eco.TakeStock(stock, amount))
                {
                    return false;
                }

                var item = w.Spawn(ItemEntity.Create(type));
                item.Amount = type == ItemType.Tereyagi ? amount * 1000f : amount;
                item.Position = pos;
                Interactions.PickUp(w, p, item);
                w.GlobalsDirty = true;
                return true;
            }

            // ── Kaba ekleme ──────────────────────────────────────────
            case ActionId.PourGrain:
            {
                var pot = target!.Pot!;
                var s = held!;
                var old = pot.RiceKg;
                if (s.GrainIsBulgur)
                {
                    pot.BulgurKg += s.GrainKg;
                }
                else
                {
                    pot.RiceKg += s.GrainKg;
                    pot.RiceWash = (pot.RiceWash * old + s.Wash * s.GrainKg) / pot.RiceKg;
                    pot.RiceSoak = (pot.RiceSoak * old + s.Soak * s.GrainKg) / pot.RiceKg;
                }

                s.GrainKg = 0;
                s.Wash = 0;
                s.Soak = 0;
                s.MarkState();
                CookingModel.UpdateResult(pot);
                target.MarkState();
                w.Sound("pour_rice", w.ItemPosition(target));
                Progression.Tutorial(w, "pour");
                return true;
            }
            case ActionId.AddButter:
            {
                var pot = target!.Pot!;
                var amt = MathF.Min(Interactions.ButterPerClick, held!.Amount);
                pot.ButterG += amt;
                held.Amount -= amt;
                Consume(w, p, held);
                target.MarkState();
                w.Sound(pot.Temp > 90 ? "sizzle" : "place", w.ItemPosition(target));
                Progression.Tutorial(w, "butter");
                return true;
            }
            case ActionId.AddWater:
            {
                var pot = target!.Pot!;
                var amt = MathF.Min(Interactions.WaterPerClick, held!.WaterL);
                // Soguk su sicak kabi sogutur.
                var mass = pot.WaterL + CookingModel.Grain(pot) + 0.6f;
                pot.Temp = (pot.Temp * mass + 20f * amt) / (mass + amt);
                pot.WaterL += amt;
                pot.WaterAdded += amt;
                held.WaterL -= amt;
                held.MarkState();
                target.MarkState();
                w.Sound(pot.Temp > 95 ? "sizzle" : "pour_water", w.ItemPosition(target));
                Progression.Tutorial(w, "water");
                return true;
            }
            case ActionId.AddSalt:
            {
                if (!eco.TakeStock("tuz", Interactions.SaltPerClick / 1000f))
                {
                    return false;
                }

                target!.Pot!.SaltG += Interactions.SaltPerClick;
                CookingModel.UpdateResult(target.Pot);
                target.MarkState();
                w.Sound("salt", w.ItemPosition(target));
                Progression.Tutorial(w, "salt");
                return true;
            }
            case ActionId.AddChicken:
            {
                target!.Pot!.ChickenKg += held!.Amount;
                held.Amount = 0;
                Consume(w, p, held);
                CookingModel.UpdateResult(target.Pot);
                target.MarkState();
                w.Sound("place", w.ItemPosition(target));
                return true;
            }
            case ActionId.AddMeat:
            {
                target!.Pot!.MeatKg += held!.Amount;
                held.Amount = 0;
                Consume(w, p, held);
                target.MarkState();
                w.Sound(target.Pot.Temp > 90 ? "sizzle" : "place", w.ItemPosition(target));
                return true;
            }
            case ActionId.EmptyIntoTrash:
            {
                var h = held!;
                h.Pot?.Clear();
                if (h.Serving is { } sv)
                {
                    var wasDirty = sv.Dirty;
                    sv.Clear();
                    sv.Dirty = wasDirty;
                }

                h.GrainKg = 0;
                h.Wash = 0;
                h.Soak = 0;
                h.WaterL = 0;
                h.MarkState();
                if (h.Type is ItemType.Tereyagi or ItemType.TavukPaketi or ItemType.EtPaketi or ItemType.TavukTepsisi or ItemType.Koli)
                {
                    p.HeldItemId = 0;
                    p.MarkState();
                    w.Remove(h.Id);
                }

                w.Sound("drop", pos);
                return true;
            }
            case ActionId.StockBox:
            {
                var box = held!;
                if (!w.Data.SupplyById.TryGetValue(box.SupplyId, out var def))
                {
                    return false;
                }

                eco.AddStock(def.Stock, box.SupplyAmount);
                p.HeldItemId = 0;
                p.MarkState();
                w.Remove(box.Id);
                w.GlobalsDirty = true;
                w.Toast("toast.stocked", $"{def.NameKey}|{Interactions.Kg(box.SupplyAmount)}", 2, p.Id);
                w.Sound("place", pos);
                Progression.Tutorial(w, "stock");
                return true;
            }

            // ── Tavuk ────────────────────────────────────────────────
            case ActionId.MoveChickenToBoard:
            {
                var pot = held!.Pot!;
                station!.BoardChickenKg = pot.ChickenKg;
                station.BoardQuality = pot.Quality;
                station.BoardShred = 0;
                station.MarkState();
                pot.Clear();
                held.MarkState();
                w.Sound("place", station.Position);
                return true;
            }

            // ── Servis ───────────────────────────────────────────────
            case ActionId.TakePlate or ActionId.TakePackage:
            {
                var cart = station!.Cart!;
                var pkg = req.Action == ActionId.TakePackage;
                if (pkg)
                {
                    cart.Packages--;
                }
                else
                {
                    cart.CleanPlates--;
                }

                station.MarkState();
                var item = w.Spawn(ItemEntity.Create(pkg ? ItemType.PaketKap : ItemType.Tabak));
                item.Position = pos;
                Interactions.PickUp(w, p, item);
                w.Sound("plate", pos);
                Progression.Tutorial(w, "plate");
                return true;
            }
            case ActionId.ReturnPlate:
            {
                station!.Cart!.CleanPlates++;
                station.MarkState();
                p.HeldItemId = 0;
                p.MarkState();
                w.Remove(held!.Id);
                w.Sound("plate", pos);
                return true;
            }
            case ActionId.Scoop:
            {
                var pot = target!.Pot!;
                var s = held!.Serving!;
                CookingModel.Lock(pot);
                pot.Scoops -= 1;
                AddComponent(s, pot.Quality, pot.Temp, pot.Stale, s.Scoops == 0 ? 0.6f : 0.25f);
                s.Base = pot.Food;
                s.Scoops++;
                if (pot.Scoops < 1)
                {
                    pot.Clear();
                }

                target.MarkState();
                held.MarkState();
                w.Sound("scoop", w.ItemPosition(target));
                Progression.Tutorial(w, "scoop");
                return true;
            }
            case ActionId.AddTopping:
            {
                var s = held!.Serving!;
                if (target!.Pot is { } pot)
                {
                    CookingModel.Lock(pot);
                    pot.Scoops -= 1;
                    AddComponent(s, pot.Quality, pot.Temp, pot.Stale, 0.2f);
                    if (pot.Food == Food.Nohut)
                    {
                        s.Nohut = true;
                    }
                    else
                    {
                        s.Fasulye = true;
                    }

                    if (pot.Scoops < 1)
                    {
                        pot.Clear();
                    }
                }
                else
                {
                    target.Servings -= 1;
                    AddComponent(s, target.Quality, target.Temp, false, 0.2f);
                    s.Tavuk = true;
                    if (target.Servings < 1)
                    {
                        if (target.Attach == Attach.Held && w.Get<PlayerEntity>(target.ParentId) is { } holder)
                        {
                            holder.HeldItemId = 0;
                            holder.MarkState();
                        }

                        w.Remove(target.Id);
                    }
                }

                target.MarkState();
                held.MarkState();
                w.Sound("scoop", pos);
                return true;
            }
            case ActionId.AddPepper:
                station!.Cart!.PepperG -= Interactions.PepperPerPlate;
                held!.Serving!.Pepper = true;
                station.MarkState();
                held.MarkState();
                w.Sound("pepper", pos);
                return true;
            case ActionId.AddPickle:
                station!.Cart!.Tursu--;
                held!.Serving!.Tursu = true;
                station.MarkState();
                held.MarkState();
                w.Sound("place", pos);
                return true;
            case ActionId.AddAyran:
                station!.Cart!.Ayran--;
                held!.Serving!.Ayran = true;
                station.MarkState();
                held.MarkState();
                w.Sound("place", pos);
                return true;
            case ActionId.Serve:
                return CustomerLogic.Serve(w, p, w.Get<CustomerEntity>(req.EntityId)!, held!);
            case ActionId.RefuseOrder:
                return CustomerLogic.Refuse(w, w.Get<CustomerEntity>(req.EntityId)!);
            case ActionId.OpenCashBox:
            {
                var cust = req.Kind == TargetKind.Customer
                    ? w.Get<CustomerEntity>(req.EntityId)
                    : w.Customers.FirstOrDefault(c => c.State == CustomerState.Paying && c.CartId == req.EntityId);
                if (cust is null)
                {
                    return false;
                }

                w.Raise(new WorldEvent { Type = WorldEventType.OpenCash, PlayerId = p.Id, Value = cust.Id, Key = "", Arg = "" });
                return true;
            }

            // ── Araba ────────────────────────────────────────────────
            case ActionId.PushCart:
                return CartLogic.StartPush(w, p, station!);
            case ActionId.ToggleCartOpen:
                return CartLogic.ToggleOpen(w, p, station!);
            case ActionId.ToggleHeater:
                station!.Cart!.HeaterOn = !station.Cart.HeaterOn;
                station.MarkState();
                w.Sound("knob", station.Position);
                return true;
            case ActionId.LoadCooler:
                return CartLogic.Load(w, station!, "ayran", Interactions.CoolerCapacity(w), c => c.Ayran, (c, v) => c.Ayran = v);
            case ActionId.LoadPickles:
                return CartLogic.Load(w, station!, "tursu", 40, c => c.Tursu, (c, v) => c.Tursu = v);
            case ActionId.LoadPackages:
                return CartLogic.Load(w, station!, "paket", 60, c => c.Packages, (c, v) => c.Packages = v);
            case ActionId.LoadPepper:
            {
                var cart = station!.Cart!;
                var want = MathF.Max(0, 250f - cart.PepperG) / 1000f;
                var take = MathF.Min(want, eco.StockOf("karabiber"));
                if (take <= 0.001f || !eco.TakeStock("karabiber", take))
                {
                    return false;
                }

                cart.PepperG += take * 1000f;
                station.MarkState();
                w.GlobalsDirty = true;
                w.Sound("pepper", station.Position);
                return true;
            }
            case ActionId.LoadGas:
            {
                var cart = station!.Cart!;
                if (!eco.TakeStock("gaz", 100))
                {
                    // Kismi tup: ne varsa
                    var have = eco.StockOf("gaz");
                    if (have < 1)
                    {
                        return false;
                    }

                    eco.TakeStock("gaz", have);
                    cart.Gas = MathF.Min(100, cart.Gas + have);
                }
                else
                {
                    cart.Gas = 100;
                }

                station.MarkState();
                w.GlobalsDirty = true;
                w.Sound("place", station.Position);
                return true;
            }
            case ActionId.OpenLaptop or ActionId.OpenPriceBoard:
                return true;
            case ActionId.Sleep:
                return DayLogic.RequestSleep(w, p);
            case ActionId.ShooCat:
                return AnimalLogic.Shoo(w, p, w.Get<AnimalEntity>(req.EntityId)!);
            case ActionId.PetCat:
                return AnimalLogic.Pet(w, p, w.Get<AnimalEntity>(req.EntityId)!);
        }

        return false;
    }

    /// <summary>Tabaga yeni bir bilesen: kalite ve sicaklik agirlikli ortalama.</summary>
    private static void AddComponent(ServingState s, float quality, float temp, bool stale, float weight)
    {
        if (s.IsEmpty)
        {
            s.Quality = quality;
            s.Temp = temp;
        }
        else
        {
            s.Quality = s.Quality * (1 - weight) + quality * weight;
            s.Temp = MathF.Min(s.Temp, temp) * 0.5f + (s.Temp * (1 - weight) + temp * weight) * 0.5f;
        }

        s.Stale |= stale;
    }

    /// <summary>Tukenen paket elden kalkar.</summary>
    private static void Consume(GameWorld w, PlayerEntity p, ItemEntity item)
    {
        item.MarkState();
        if (item.Amount <= 0.001f)
        {
            p.HeldItemId = 0;
            p.MarkState();
            w.Remove(item.Id);
        }
    }
}
