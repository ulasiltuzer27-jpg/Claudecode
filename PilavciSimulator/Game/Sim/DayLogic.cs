using System.Numerics;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim;

/// <summary>Gun basi, gun sonu, uyku, hava ve enflasyon.</summary>
public static class DayLogic
{
    public static readonly string[] DayKeys = ["day.mon", "day.tue", "day.wed", "day.thu", "day.fri", "day.sat", "day.sun"];

    public static bool RequestSleep(GameWorld w, PlayerEntity p)
    {
        if (w.Clock.Minute < w.Data.Balance.SleepAllowedMinute)
        {
            return false;
        }

        p.SleepReady = !p.SleepReady;
        p.MarkState();
        var active = w.Players.Where(x => x.Connected).ToList();
        if (active.All(x => x.SleepReady))
        {
            EndDay(w, passedOut: false);
        }
        else if (p.SleepReady)
        {
            w.Toast("toast.sleep_wait", $"{active.Count(x => x.SleepReady)}|{active.Count}", 0);
        }

        return true;
    }

    /// <summary>Gunu kapatir: giderler, istatistikler, rapor. Simulasyon rapor kapanana kadar durur.</summary>
    public static void EndDay(GameWorld w, bool passedOut)
    {
        if (w.DayOver)
        {
            return;
        }

        var led = w.Economy.Today;
        led.Day = w.Clock.Day;

        foreach (var cart in w.Carts)
        {
            if (!CartLogic.InDepot(w, cart))
            {
                var fine = (int)(w.Data.Balance.CartTowFine * w.Economy.SupplyIndex);
                led.Fines += fine;
                w.Economy.Money -= fine;
                w.Economy.Today.Fines += 0;
                w.Toast("toast.cart_towed", $"{fine}", 1);
            }
        }

        foreach (var id in w.Progress.Upgrades)
        {
            if (w.Data.UpgradeById.TryGetValue(id, out var u) && u.Daily > 0)
            {
                if (id == "dukkan")
                {
                    led.Rent += u.Daily;
                }
                else
                {
                    led.Wages += u.Daily;
                }

                w.Economy.Money -= u.Daily;
            }
        }

        led.RepEnd = w.Reputation.Stars;
        var pr = w.Progress;
        pr.AddStat("days_played");
        pr.MaxStat("best_day_profit", led.Profit);
        pr.AddStat("total_revenue", led.Income);
        if (w.Players.Count(p => p.Connected) > 1)
        {
            pr.AddStat("coop_days");
        }

        w.Economy.History.Add(led);
        if (w.Economy.History.Count > 30)
        {
            w.Economy.History.RemoveAt(0);
        }

        Progression.CheckAchievements(w);
        w.DayOver = true;
        w.GlobalsDirty = true;
        w.Raise(new WorldEvent { Type = WorldEventType.DayEnded, Value = w.Clock.Day, Key = passedOut ? "passout" : "sleep", Arg = "" });
    }

    /// <summary>Rapor kapandiktan sonra (host) ertesi sabaha gecer.</summary>
    public static void StartNextDay(GameWorld w)
    {
        var overnight = 1440f - w.Clock.Minute + w.Data.Balance.DayStartMinute;
        w.Clock.Day++;
        w.Clock.Minute = w.Data.Balance.DayStartMinute;
        w.DayOver = false;

        // Gece: tencereler bayatlar/ıslanir, sokaktaki her sey temizlenir.
        foreach (var it in w.Items.ToList())
        {
            if (it.Pot is { } pot)
            {
                CookingModel.Overnight(pot, overnight);
                it.MarkState();
            }

            if (it.Type == ItemType.Suzgec && it.Attach == Attach.Socket)
            {
                it.Soak = 1;
                it.MarkState();
            }

            if (it.Serving is { } s && !s.IsEmpty)
            {
                // Tabakta bekleyen yemek atilir, tabak kirli sayilir.
                s.Clear();
                s.Dirty = true;
                it.MarkState();
            }

            if (it.Type == ItemType.TavukTepsisi)
            {
                it.Quality = MathF.Max(0, it.Quality - 18);
                it.Temp = CookingModel.Ambient;
                it.MarkState();
            }
        }

        foreach (var c in w.Customers.ToList())
        {
            w.Remove(c.Id);
        }

        foreach (var v in w.Vehicles.Where(v => v.Type != VehicleType.Ferry).ToList())
        {
            w.Remove(v.Id);
        }

        foreach (var a in w.Animals.Where(a => !a.Mascot).ToList())
        {
            w.Remove(a.Id);
        }

        foreach (var cart in w.Carts)
        {
            if (!CartLogic.InDepot(w, cart))
            {
                CartLogic.ReturnHome(w, cart);
            }

            cart.Cart!.Open = false;
            cart.Cart.ClosedUntil = 0;
            cart.MarkState();
        }

        foreach (var st in w.Stations)
        {
            if (st.Type is StationType.KazanOcagi or StationType.Stovetop)
            {
                Array.Clear(st.Heat);
                st.MarkState();
            }

            if (st.Type == StationType.Table)
            {
                st.SeatMask = 0;
            }
        }

        var spawn = w.Layout.PlayerSpawn;
        var k = 0;
        foreach (var p in w.Players)
        {
            if (p.HeldItemId != 0 && w.HeldBy(p) is { } held)
            {
                held.Attach = Attach.Free;
                held.ParentId = 0;
                held.Position = new Vector3(-104.6f + k * 0.3f, 1.05f, -16.9f);
                held.MarkState();
            }

            p.HeldItemId = 0;
            p.PushingCartId = 0;
            p.SleepReady = false;
            p.HoldAction = 0;
            p.SetMotion(spawn + new Vector3(0, 0, k * 0.8f), w.Layout.PlayerSpawnYaw);
            p.MarkState();
            k++;
        }

        w.Events.ZabitaActive = false;
        w.Events.CatPetsToday = 0;
        w.Economy.Today = new DayLedger { Day = w.Clock.Day, RepStart = w.Reputation.Stars };
        RollDay(w);
        w.GlobalsDirty = true;
        w.Raise(new WorldEvent { Type = WorldEventType.DayStarted, Value = w.Clock.Day, Key = "", Arg = "" });
    }

    /// <summary>Gunun havasi, mac gunu, enflasyon haberleri.</summary>
    public static void RollDay(GameWorld w)
    {
        var rng = w.Rng;
        var ev = w.Events;
        ev.News.Clear();
        var dow = w.Clock.DayOfWeek;

        // Hava
        var roll = rng.NextFloat();
        var weather = w.Weather;
        weather.Kind = w.Clock.Day == 1 ? WeatherKind.Clear : roll < 0.62f ? WeatherKind.Clear : roll < 0.84f ? WeatherKind.Cloudy : WeatherKind.Rain;
        weather.Cloud = weather.Kind switch
        {
            WeatherKind.Clear => rng.Range(0.1f, 0.4f),
            WeatherKind.Cloudy => rng.Range(0.6f, 0.85f),
            _ => 0.95f,
        };
        if (weather.Kind == WeatherKind.Rain)
        {
            weather.RainStart = rng.Range(8 * 60f, 16 * 60f);
            weather.RainEnd = weather.RainStart + rng.Range(90f, 300f);
            ev.News.Add("news.rain|" + (int)(weather.RainStart / 60));
        }
        else
        {
            weather.RainStart = weather.RainEnd = -1;
        }

        weather.Rain = 0;

        // Mac: cumartesi her zaman, carsamba bazen
        ev.MatchToday = dow == 5 || (dow == 2 && rng.Chance(0.5f));
        if (ev.MatchToday)
        {
            ev.News.Add("news.match|");
        }

        // Pazartesi zam
        if (dow == 0 && w.Clock.Day > 1)
        {
            var b = w.Data.Balance;
            var rate = rng.Range(b.InflationMin, b.InflationMax);
            w.Economy.SupplyIndex *= 1 + rate;
            w.Economy.MarketIndex *= 1 + rate * 0.8f;
            w.Economy.LastInflation = rate;
            ev.News.Add("news.inflation|" + (int)MathF.Round(rate * 100));
        }

        // Teslimat hatirlatmasi
        var deliveries = w.Economy.Pending.Count(d => d.ArriveAt <= w.Clock.Day * 1440f);
        if (deliveries > 0)
        {
            ev.News.Add("news.delivery|" + deliveries);
        }

        ev.NextFerry = w.Clock.Absolute + 60;
        w.Rng = rng;
    }
}
