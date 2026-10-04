using System.Numerics;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Sim.Economy;

/// <summary>Fiyatlar, toptanci siparisleri, teslimat ve yukseltmeler.</summary>
public static class EconomyLogic
{
    public const int MinPrice = 5;
    public const int MaxPrice = 2000;

    public static void InitNewGame(GameWorld w)
    {
        var e = w.Economy;
        var b = w.Data.Balance;
        e.Money = b.StartMoney;
        e.Prices.Clear();
        foreach (var m in w.Data.Menu)
        {
            e.Prices[m.Id] = m.DefaultPrice;
        }

        e.Stock.Clear();
        e.Stock["pirinc"] = 10;
        e.Stock["tereyagi"] = 1;
        e.Stock["tuz"] = 1;
        e.Stock["nohut"] = 2;
        e.Stock["konserve_nohut"] = 1;
        e.Stock["karabiber"] = 0.25f;
        e.Stock["ayran"] = 12;
        e.Stock["tursu"] = 20;
        e.Stock["paket"] = 20;
        e.Stock["gaz"] = 0;
        e.SupplyIndex = 1;
        e.MarketIndex = 1;
        e.Pending.Clear();
        e.Today = new DayLedger { Day = 1, RepStart = w.Reputation.Stars };
        e.History.Clear();
    }

    /// <summary>Bir malzemenin guncel (enflasyonlu) fiyati.</summary>
    public static int SupplyPrice(GameWorld w, string supplyId, bool express)
    {
        var def = w.Data.SupplyById[supplyId];
        var p = def.Price * w.Economy.SupplyIndex * (express ? 1f + w.Data.Balance.ExpressDeliveryMarkup : 1f);
        return (int)MathF.Round(p / 5f) * 5;
    }

    /// <summary>Menudeki bir kalemin "normal" fiyati (musterinin gozunde).</summary>
    public static float ReferencePrice(GameWorld w, string menuId) =>
        w.Data.MenuById[menuId].RefPrice * w.Economy.MarketIndex;

    public static bool SetPrice(GameWorld w, string menuId, int price)
    {
        if (!w.Data.MenuById.ContainsKey(menuId))
        {
            return false;
        }

        w.Economy.Prices[menuId] = Math.Clamp(price, MinPrice, MaxPrice);
        w.GlobalsDirty = true;
        return true;
    }

    /// <summary>"pirinc_5:2,tuz_1:1" bicimindeki sepeti satin alir.</summary>
    public static bool BuySupplies(GameWorld w, string basket, bool express)
    {
        var lines = ParseBasket(basket);
        if (lines.Count == 0)
        {
            return false;
        }

        var level = w.Level;
        var total = 0;
        foreach (var (id, qty) in lines)
        {
            if (!w.Data.SupplyById.TryGetValue(id, out var def) || def.Level > level || qty <= 0 || qty > 50)
            {
                return false;
            }

            total += SupplyPrice(w, id, express) * qty;
        }

        if (total > w.Economy.Money)
        {
            w.Toast("toast.no_money", "", 1);
            w.Sound("ui_error", w.Layout.PalletPos);
            return false;
        }

        w.Economy.Money -= total;
        w.Economy.Today.Supplies += total;
        var arrive = express
            ? w.Clock.Absolute + w.Data.Balance.ExpressDeliveryMinutes
            : w.Clock.Day * 1440f + 7 * 60f; // ertesi sabah 07:00
        foreach (var (id, qty) in lines)
        {
            w.Economy.Pending.Add(new Delivery { SupplyId = id, Packs = qty, ArriveAt = arrive, Express = express });
        }

        w.GlobalsDirty = true;
        w.Toast(express ? "toast.ordered_express" : "toast.ordered", $"{total}", 2);
        return true;
    }

    public static List<(string Id, int Qty)> ParseBasket(string basket)
    {
        var result = new List<(string, int)>();
        foreach (var part in basket.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && int.TryParse(kv[1], out var q) && q > 0)
            {
                result.Add((kv[0].Trim(), q));
            }
        }

        return result;
    }

    public static bool CanBuyUpgrade(GameWorld w, string id, out string reasonKey)
    {
        reasonKey = "";
        if (!w.Data.UpgradeById.TryGetValue(id, out var u))
        {
            reasonKey = "upg.reason.unknown";
            return false;
        }

        if (w.Progress.Has(id))
        {
            reasonKey = "upg.reason.owned";
            return false;
        }

        if (u.Requires is not null && !w.Progress.Has(u.Requires))
        {
            reasonKey = "upg.reason.requires";
            return false;
        }

        if (w.Level < u.Level)
        {
            reasonKey = "upg.reason.level";
            return false;
        }

        if (w.Economy.Money < UpgradePrice(w, u))
        {
            reasonKey = "upg.reason.money";
            return false;
        }

        return true;
    }

    public static int UpgradePrice(GameWorld w, Data.UpgradeDef u) => u.Price;

    public static bool BuyUpgrade(GameWorld w, string id)
    {
        if (!CanBuyUpgrade(w, id, out _))
        {
            return false;
        }

        var u = w.Data.UpgradeById[id];
        var price = UpgradePrice(w, u);
        w.Economy.Money -= price;
        w.Economy.Today.Upgrades += price;
        w.Progress.Upgrades.Add(id);
        ApplyUpgrade(w, id);
        w.GlobalsDirty = true;
        w.Toast("toast.upgrade_bought", u.NameKey, 2);
        w.Sound("levelup", w.Layout.PlayerSpawn);
        if (u.Category == "ruhsat")
        {
            w.Progress.AddStat("licenses");
        }

        if (id == "dukkan")
        {
            w.Progress.AddStat("shop_owned");
        }

        if (u.Category == "kozmetik" && u.Value is not null)
        {
            SelectPaint(w, id);
        }

        Progression.CheckAchievements(w);
        return true;
    }

    /// <summary>Yukseltmenin dunyaya etkisi. Yuklemede de cagrilir (idempotent).</summary>
    public static void ApplyUpgrade(GameWorld w, string id)
    {
        switch (id)
        {
            case "kazan_orta" or "kazan_buyuk":
            {
                var tier = w.Progress.Has("kazan_buyuk") ? 2 : 1;
                foreach (var k in w.Items.Where(i => i.Type == ItemType.Kazan))
                {
                    k.Tier = Math.Max(k.Tier, tier);
                    k.MarkState();
                }

                break;
            }
            case "ocak_ek":
                foreach (var s in w.Stations)
                {
                    if (s.Type == StationType.Stovetop)
                    {
                        s.Tier = 1;
                        s.MarkState();
                    }
                    else if (s.Tag == "ocakB")
                    {
                        s.Enabled = true;
                        s.MarkState();
                    }
                }

                break;
            case "tencere_ek":
                if (w.Items.Count(i => i.Type == ItemType.Tencere) < 3)
                {
                    var t = w.Spawn(ItemEntity.Create(ItemType.Tencere));
                    t.Attach = Attach.Free;
                    t.Position = new Vector3(-103.3f, 1.05f, -16.9f);
                }

                break;
            case "tabak_seti":
                foreach (var c in w.Carts)
                {
                    c.Cart!.CleanPlates += w.Data.Balance.PlatesUpgradeAmount;
                    c.MarkState();
                }

                break;
            case "araba_buyuk":
                foreach (var c in w.Carts)
                {
                    CartLogic.Upgrade(w, c, 1);
                }

                break;
            case "dukkan":
                w.RefreshDynamicColliders();
                break;
        }
    }

    public static bool SelectPaint(GameWorld w, string upgradeId)
    {
        if (upgradeId == "")
        {
            w.Progress.Paint = "";
        }
        else if (w.Progress.Has(upgradeId) && w.Data.UpgradeById[upgradeId].Value is { } v)
        {
            w.Progress.Paint = v;
        }
        else
        {
            return false;
        }

        foreach (var c in w.Carts)
        {
            c.Cart!.Paint = w.Progress.Paint;
            c.MarkState();
        }

        w.GlobalsDirty = true;
        return true;
    }

    /// <summary>Vadesi gelen siparisler: kamyonet cagrilir (EventSystem teslim eder).</summary>
    public static List<Delivery> DueDeliveries(GameWorld w)
    {
        var now = w.Clock.Absolute;
        var due = w.Economy.Pending.Where(d => d.ArriveAt <= now).ToList();
        if (due.Count > 0)
        {
            w.Economy.Pending.RemoveAll(d => d.ArriveAt <= now);
            w.GlobalsDirty = true;
        }

        return due;
    }

    /// <summary>Teslim edilen kolileri paletin ustune/yanina dizer.</summary>
    public static void SpawnBoxes(GameWorld w, IEnumerable<Delivery> deliveries)
    {
        var pallet = w.Layout.PalletPos;
        var existing = w.Items.Count(i => i.Type == ItemType.Koli && i.Attach == Attach.Free && Vector3.Distance(i.Position, pallet) < 3f);
        var n = existing;
        foreach (var d in deliveries)
        {
            var def = w.Data.SupplyById[d.SupplyId];
            for (var k = 0; k < d.Packs; k++)
            {
                var box = w.Spawn(ItemEntity.Create(ItemType.Koli));
                box.SupplyId = d.SupplyId;
                box.SupplyAmount = def.Pack;
                var layer = n / 6;
                var slot = n % 6;
                var x = (slot % 3 - 1) * 0.55f;
                var z = (slot / 3 - 0.5f) * 0.45f;
                box.Attach = Attach.Free;
                box.Position = pallet + new Vector3(x, 0.14f + layer * 0.37f, z);
                if (layer > 2)
                {
                    box.Position = pallet + new Vector3(1.6f + (n % 4) * 0.55f, 0.0f, -0.5f + (n / 4 % 3) * 0.45f);
                }

                box.Yaw = 0;
                box.Resting = true;
                n++;
            }
        }
    }
}
