using System.Numerics;
using PilavciSimulator.Core;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Customers;

/// <summary>Arabada su an neler var: musteriler yalnizca bunlari ister.</summary>
public sealed class CartMenu
{
    public HashSet<Food> Bases = new();
    public bool Nohut;
    public bool Tavuk;
    public bool Fasulye;
    public bool Ayran;
    public bool Tursu;
    public bool Pepper;
    public bool Packages;
    public bool Plates;

    public bool Has(MenuItemDef m)
    {
        if (m.Kind != "plate")
        {
            return false;
        }

        var baseFood = m.Base switch
        {
            "bulgur" => Food.BulgurPilav,
            "etli" => Food.EtliPilav,
            _ => Food.Pilav,
        };
        if (!Bases.Contains(baseFood))
        {
            return false;
        }

        foreach (var t in m.Toppings)
        {
            if (t == "nohut" && !Nohut || t == "tavuk" && !Tavuk || t == "fasulye" && !Fasulye)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Siparis olusturma, servis puanlama, odeme.</summary>
public static class CustomerLogic
{
    public static readonly int[] Bills = [200, 100, 50, 20, 10, 5, 1];

    public static float SizeMultiplier(int scoops) => scoops switch
    {
        1 => 0.65f,
        2 => 1f,
        _ => 1.5f,
    };

    public static CartMenu MenuOf(GameWorld w, StationEntity cart)
    {
        var m = new CartMenu();
        foreach (var it in w.Items)
        {
            if (it.Attach != Attach.Socket || it.ParentId != cart.Id)
            {
                continue;
            }

            if (it.Pot is { } pot && pot.Scoops >= 1)
            {
                if (CookingModel.IsPilavFood(pot.Food))
                {
                    m.Bases.Add(pot.Food);
                }
                else if (pot.Food == Food.Nohut)
                {
                    m.Nohut = true;
                }
                else if (pot.Food == Food.Fasulye)
                {
                    m.Fasulye = true;
                }
            }
            else if (it.Type == ItemType.TavukTepsisi && it.Servings >= 1)
            {
                m.Tavuk = true;
            }
        }

        var c = cart.Cart!;
        m.Ayran = c.Ayran > 0;
        m.Tursu = c.Tursu > 0;
        m.Pepper = c.PepperG >= 2;
        m.Packages = c.Packages > 0;
        m.Plates = c.CleanPlates > 0;
        return m;
    }

    public static Food BaseOf(MenuItemDef m) => m.Base switch
    {
        "bulgur" => Food.BulgurPilav,
        "etli" => Food.EtliPilav,
        _ => Food.Pilav,
    };

    /// <summary>
    /// Musterinin siparisi. Fiyat pahaliysa siparis vermeyebilir (null,
    /// reason = "say.expensive"); arabada uygun bir sey yoksa reason =
    /// "say.nothing".
    /// </summary>
    public static OrderSpec? CreateOrder(GameWorld w, CustomerEntity c, StationEntity cart, out string? reason)
    {
        reason = null;
        var type = w.Data.CustomerById[c.TypeId];
        var menu = MenuOf(w, cart);
        var rng = new Rng(c.Seed ^ 0xABCDu);
        var level = w.Level;
        var candidates = new List<(MenuItemDef Item, float Weight)>();
        foreach (var (id, weight) in type.Prefs)
        {
            var m = w.Data.MenuById[id];
            if (m.Level <= level && menu.Has(m))
            {
                candidates.Add((m, weight));
            }
        }

        if (candidates.Count == 0 && (menu.Plates || menu.Packages))
        {
            // Istedigi yok: elde ne varsa "bari ondan olsun" diyebilir.
            foreach (var m in w.Data.Menu)
            {
                if (m.Kind == "plate" && m.Level <= level && menu.Has(m))
                {
                    candidates.Add((m, 1f));
                }
            }

            if (candidates.Count > 0 && !rng.Chance(0.65f))
            {
                candidates.Clear();
            }
        }

        if (candidates.Count == 0 || (!menu.Plates && !menu.Packages))
        {
            reason = "say.nothing";
            return null;
        }

        var total = candidates.Sum(x => x.Weight);
        var pick = rng.NextFloat() * total;
        var chosen = candidates[^1].Item;
        foreach (var (item, weight) in candidates)
        {
            pick -= weight;
            if (pick <= 0)
            {
                chosen = item;
                break;
            }
        }

        var o = new OrderSpec { MenuId = chosen.Id, Base = BaseOf(chosen) };
        o.Nohut = chosen.Toppings.Contains("nohut");
        o.Tavuk = chosen.Toppings.Contains("tavuk");
        o.Fasulye = chosen.Toppings.Contains("fasulye");
        var sizeRoll = rng.NextFloat() * type.Sizes.Sum();
        o.Scoops = sizeRoll < type.Sizes[0] ? 1 : sizeRoll < type.Sizes[0] + type.Sizes[1] ? 2 : 3;
        o.Package = (rng.Chance(type.Package) && menu.Packages) || !menu.Plates;
        o.Ayran = menu.Ayran && rng.Chance(type.Ayran);
        o.Tursu = menu.Tursu && rng.Chance(type.Tursu);
        o.Pepper = menu.Pepper && rng.Chance(type.Pepper);
        o.Price = PriceOf(w, o);

        // Fiyat kabul: referans fiyatin ustundeyse olasilik duser.
        var refPrice = RefPriceOf(w, o);
        var ratio = o.Price / MathF.Max(1, refPrice);
        if (ratio > 1.02f)
        {
            var spotSens = w.Data.SpotById.TryGetValue(c.SpotId, out var spot) ? spot.PriceSensitivity : 1f;
            var accept = Math.Clamp(1f - (ratio - 1f) * 2.4f * type.PriceSensitivity * spotSens, 0.04f, 1f);
            if (!rng.Chance(accept))
            {
                reason = "say.expensive";
                return null;
            }
        }

        return o;
    }

    public static int PriceOf(GameWorld w, OrderSpec o)
    {
        var e = w.Economy;
        var plate = e.PriceOf(o.MenuId) * SizeMultiplier(o.Scoops);
        var p = (int)MathF.Round(plate / 5f) * 5;
        if (o.Ayran)
        {
            p += e.PriceOf("ayran");
        }

        if (o.Tursu)
        {
            p += e.PriceOf("tursu");
        }

        return p;
    }

    public static float RefPriceOf(GameWorld w, OrderSpec o)
    {
        var r = EconomyLogic.ReferencePrice(w, o.MenuId) * SizeMultiplier(o.Scoops);
        if (o.Ayran)
        {
            r += EconomyLogic.ReferencePrice(w, "ayran");
        }

        if (o.Tursu)
        {
            r += EconomyLogic.ReferencePrice(w, "tursu");
        }

        return r;
    }

    /// <summary>Tabak ile siparisin uyumu ve memnuniyet (0..100), en belirgin sikayet.</summary>
    public static float Evaluate(OrderSpec o, ServingState s, bool isPackage, float waitedRatio, out string complaint, out int due)
    {
        var score = 100f;
        var worst = (0f, "say.happy");
        void Pen(float v, string key)
        {
            score -= v;
            if (v > worst.Item1)
            {
                worst = (v, key);
            }
        }

        due = o.Price;
        if (s.Scoops == 0 || s.Base == Food.None)
        {
            Pen(60, "say.wrong");
        }
        else if (s.Base != o.Base)
        {
            Pen(45, "say.wrong");
        }

        if (s.Scoops < o.Scoops)
        {
            Pen(20 * (o.Scoops - s.Scoops), "say.small");
        }
        else if (s.Scoops > o.Scoops)
        {
            score += 3;
        }

        if (o.Nohut && !s.Nohut)
        {
            Pen(25, "say.missing_nohut");
        }
        else if (!o.Nohut && s.Nohut)
        {
            Pen(6, "say.wrong");
        }

        if (o.Tavuk && !s.Tavuk)
        {
            Pen(25, "say.missing_tavuk");
        }
        else if (!o.Tavuk && s.Tavuk)
        {
            Pen(6, "say.wrong");
        }

        if (o.Fasulye && !s.Fasulye)
        {
            Pen(25, "say.missing_fasulye");
        }
        else if (!o.Fasulye && s.Fasulye)
        {
            Pen(6, "say.wrong");
        }

        if (o.Pepper && !s.Pepper)
        {
            Pen(6, "say.missing_pepper");
        }

        if (o.Ayran && !s.Ayran)
        {
            Pen(14, "say.missing_ayran");
        }

        if (o.Tursu && !s.Tursu)
        {
            Pen(12, "say.missing_tursu");
        }

        if (o.Package != isPackage)
        {
            Pen(8, o.Package ? "say.wanted_package" : "say.wanted_plate");
        }

        // Kalite
        var qd = (s.Quality - 70f) * 0.5f;
        if (qd < 0)
        {
            Pen(-qd, s.Stale ? "say.stale" : "say.bad_taste");
        }
        else
        {
            score += qd;
        }

        // Sicaklik
        if (s.Temp < 40)
        {
            Pen(20, "say.cold");
        }
        else if (s.Temp < 55)
        {
            Pen(8, "say.lukewarm");
        }
        else if (s.Temp >= 65)
        {
            score += 3;
        }

        if (s.Stale)
        {
            Pen(8, "say.stale");
        }

        // Bekleme
        Pen(MathF.Pow(Math.Clamp(waitedRatio, 0f, 1.2f), 2) * 22f, "say.slow");

        // Eksik ekstralar odenmez.
        if (o.Ayran && !s.Ayran)
        {
            due -= 25;
        }

        if (o.Tursu && !s.Tursu)
        {
            due -= 15;
        }

        due = Math.Max(due, 0);
        score = Math.Clamp(score, 0f, 100f);
        complaint = score >= 80 ? "say.happy" : score >= 55 ? (worst.Item1 >= 8 ? worst.Item2 : "say.ok") : worst.Item2;
        return score;
    }

    public static bool Serve(GameWorld w, PlayerEntity p, CustomerEntity c, ItemEntity plate)
    {
        if (c.State != CustomerState.Ordered || c.Order is null || plate.Serving is null)
        {
            return false;
        }

        var type = w.Data.CustomerById[c.TypeId];
        var patienceMinutes = type.Patience * CustomerSystem.PatienceScale;
        var sat = Evaluate(c.Order, plate.Serving, plate.Type == ItemType.PaketKap, c.WaitedMinutes / patienceMinutes, out var complaint, out var due);
        c.LastSatisfaction = sat;
        c.Served = true;
        c.HoldsPackage = plate.Type == ItemType.PaketKap;
        c.HoldsPlate = !c.HoldsPackage;
        c.DueAmount = due;
        c.ServerPlayerId = p.Id;
        c.Mood = sat >= 80 ? Mood.Happy : sat >= 55 ? Mood.Neutral : Mood.Angry;
        c.Anim = sat >= 80 ? CustomerAnim.Happy : sat >= 55 ? CustomerAnim.Talk : CustomerAnim.Angry;
        CustomerSystem.Say(w, c, complaint, "", 6f);

        // Tabak elden musteriye gecer.
        p.HeldItemId = 0;
        p.MarkState();
        w.Remove(plate.Id);

        var led = w.Economy.Today;
        led.Served++;
        led.SatisfactionSum += sat;
        led.SatisfactionCount++;
        led.BestQuality = Math.Max(led.BestQuality, (int)plate.Serving.Quality);
        var pr = w.Progress;
        pr.AddStat("served");
        if (c.TypeId == "turist")
        {
            pr.AddStat("tourists_served");
        }

        if (w.Weather.Rain > 0.3f)
        {
            pr.AddStat("rain_served");
        }

        if (w.Clock.Hour >= 20 || w.Clock.Hour < 5)
        {
            pr.AddStat("night_served");
        }

        if (c.SpotId == "stadyum" && w.Events.MatchToday)
        {
            pr.AddStat("match_served");
        }

        ReputationAdd(w, sat, c.SpotId);
        Progression.AddXp(w, 4 + (int)(sat / 12));
        w.Sound(sat >= 55 ? "serve" : "bad", c.Position);

        // Odeme: kart ya da tam para ise hemen biter.
        var rng = new Rng(c.Seed ^ 0x5151u);
        var tip = sat >= 85 && rng.Chance(type.Tip) ? rng.Range(1, 5) * 5 : 0;
        if (due == 0)
        {
            Finish(w, c, 0, tip, paid: 0, change: 0);
            return true;
        }

        if (w.Progress.Has("pos_cihazi") && rng.Chance(type.Card))
        {
            w.Sound("knob", c.Position);
            Finish(w, c, due, tip, paid: due, change: 0);
            pr.AddStat("payments");
            return true;
        }

        var paid = ChoosePayment(due, ref rng);
        if (paid == due)
        {
            Finish(w, c, due, tip, paid, 0);
            pr.AddStat("payments");
            return true;
        }

        c.PaidAmount = paid;
        c.State = CustomerState.Paying;
        c.Timer = 0;
        c.MarkState();
        w.Raise(new WorldEvent { Type = WorldEventType.OpenCash, PlayerId = p.Id, Value = c.Id, Key = "", Arg = "" });
        Progression.Tutorial(w, "served");
        return true;
    }

    /// <summary>Musterinin uzattigi para: cogunlukla tutari gecen ilk "yuvarlak" banknot.</summary>
    public static int ChoosePayment(int due, ref Rng rng)
    {
        if (due % 5 == 0 && rng.Chance(0.15f))
        {
            return due;
        }

        int[] steps = [10, 20, 50, 100, 200];
        var options = new List<int>();
        foreach (var s in steps)
        {
            var v = (int)MathF.Ceiling(due / (float)s) * s;
            if (v > due && v - due <= 190 && !options.Contains(v))
            {
                options.Add(v);
            }
        }

        if (options.Count == 0)
        {
            return (int)MathF.Ceiling(due / 100f) * 100 + (due % 100 == 0 ? 100 : 0);
        }

        // Kucuk para ustu daha olasi, ama bazen 200'luk uzatan da var.
        var i = Math.Min(options.Count - 1, (int)(MathF.Pow(rng.NextFloat(), 1.6f) * options.Count));
        return options[i];
    }

    /// <summary>Para ustu verildi: dogru mu, eksik mi, fazla mi.</summary>
    public static bool CompletePayment(GameWorld w, PlayerEntity p, CustomerEntity? c, int change)
    {
        if (c is null || c.State != CustomerState.Paying || change < 0 || change > 10000)
        {
            return false;
        }

        var correct = c.PaidAmount - c.DueAmount;
        var diff = change - correct;
        var pr = w.Progress;
        pr.AddStat("payments");
        var rng = new Rng(c.Seed ^ 0x7777u);
        var tip = c.LastSatisfaction >= 85 && rng.Chance(w.Data.CustomerById[c.TypeId].Tip) ? rng.Range(1, 5) * 5 : 0;
        if (diff == 0)
        {
            pr.AddStat("correct_change");
            CustomerSystem.Say(w, c, "say.thanks", "", 4f);
            Finish(w, c, c.DueAmount, tip, c.PaidAmount, change);
            ReputationAdd(w, 85, c.SpotId, weight: 0.3f);
        }
        else if (diff < 0)
        {
            // Eksik para ustu: musteri kizgin, eksik kalani "kaybeder" (biz kazaniriz ama itibar duser).
            CustomerSystem.Say(w, c, "say.change_short", $"{-diff}", 5f);
            c.Mood = Mood.Angry;
            c.Anim = CustomerAnim.Angry;
            Finish(w, c, c.DueAmount - diff, 0, c.PaidAmount, change);
            ReputationAdd(w, 10, c.SpotId, weight: 1.2f);
            w.Sound("bad", c.Position);
        }
        else
        {
            // Fazla verdin: durust musteri bazen geri verir.
            if (rng.Chance(0.35f))
            {
                CustomerSystem.Say(w, c, "say.change_extra_honest", $"{diff}", 5f);
                Finish(w, c, c.DueAmount, tip, c.PaidAmount, correct);
                ReputationAdd(w, 90, c.SpotId, weight: 0.3f);
            }
            else
            {
                CustomerSystem.Say(w, c, "say.change_extra", $"{diff}", 4f);
                w.Economy.Today.ChangeLoss += diff;
                Finish(w, c, c.DueAmount, tip, c.PaidAmount, change, lossAlreadyCounted: true);
            }
        }

        Progression.CheckAchievements(w);
        Progression.Tutorial(w, "change");
        return true;
    }

    private static void Finish(GameWorld w, CustomerEntity c, int revenue, int tip, int paid, int change, bool lossAlreadyCounted = false)
    {
        // Kasaya giren net para = odenen - para ustu + bahsis.
        var net = paid > 0 ? paid - change : revenue;
        w.Economy.Money += net + tip;
        var led = w.Economy.Today;
        if (lossAlreadyCounted)
        {
            // net = due - fazla; geliri tam yaz, kayip ayri kalemde (ChangeLoss).
            led.Revenue += revenue;
        }
        else
        {
            led.Revenue += net;
        }

        led.Tips += tip;
        w.GlobalsDirty = true;
        if (net + tip > 0)
        {
            w.Raise(new WorldEvent { Type = WorldEventType.Coin, Value = net + tip, Position = c.Position + new Vector3(0, 1.9f, 0), Key = "", Arg = "" });
            w.Sound("cash", c.Position);
        }

        if (tip > 0)
        {
            CustomerSystem.Say(w, c, "say.tip", $"{tip}", 4f);
            w.Sound("tip", c.Position);
        }

        c.PaidAmount = 0;
        c.State = c.HoldsPackage ? CustomerState.Leaving : CustomerState.ToEat;
        c.Timer = 0;
        c.Path.Clear();
        c.MarkState();
    }

    public static bool Refuse(GameWorld w, CustomerEntity c)
    {
        if (c.State != CustomerState.Ordered)
        {
            return false;
        }

        w.Economy.Today.Refused++;
        CustomerSystem.Say(w, c, "say.refused", "", 4f);
        c.Mood = Mood.Neutral;
        ReputationAdd(w, 45, c.SpotId, weight: 0.4f);
        CustomerSystem.Leave(w, c);
        return true;
    }

    /// <summary>Itibar: son memnuniyetlerin hareketli ortalamasi -> 1..5 yildiz.</summary>
    public static void ReputationAdd(GameWorld w, float satisfaction, string spot, float weight = 1f)
    {
        var r = w.Reputation;
        r.Recent.Add(satisfaction);
        if (r.Recent.Count > 60)
        {
            r.Recent.RemoveAt(0);
        }

        var avg = r.Recent.Average();
        var target = 1f + avg / 100f * 4f;
        r.Stars = Math.Clamp(r.Stars + (target - r.Stars) * 0.06f * weight, 0.5f, 5f);
        if (spot.Length > 0)
        {
            r.Fame[spot] = MathF.Min(1f, r.FameOf(spot) + 0.008f * weight);
        }

        w.GlobalsDirty = true;
    }
}
