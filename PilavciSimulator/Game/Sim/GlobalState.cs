using PilavciSimulator.Net;

namespace PilavciSimulator.Sim;

/// <summary>Oyun saati. 1 gercek saniye = 1 oyun dakikasi (balance.json).</summary>
public sealed class WorldClock
{
    public int Day = 1;
    public float Minute = 360;

    public int Hour => (int)(Minute / 60f) % 24;
    /// <summary>0 = Pazartesi.</summary>
    public int DayOfWeek => (Day - 1) % 7;
    public float Absolute => (Day - 1) * 1440f + Minute;
    public string Clock => $"{Hour:00}:{(int)Minute % 60:00}";

    public void Write(NetWriter w)
    {
        w.Int(Day);
        w.Float(Minute);
    }

    public void Read(NetReader r)
    {
        Day = r.Int();
        Minute = r.Float();
    }
}

public enum WeatherKind : byte
{
    Clear,
    Cloudy,
    Rain,
}

public sealed class WeatherState
{
    public WeatherKind Kind;
    public float RainStart;
    public float RainEnd;
    /// <summary>Su anki yagmur 0..1.</summary>
    public float Rain;
    public float Cloud = 0.35f;

    public void Write(NetWriter w)
    {
        w.Byte((byte)Kind);
        w.Float(RainStart);
        w.Float(RainEnd);
        w.Float(Rain);
        w.Float(Cloud);
    }

    public void Read(NetReader r)
    {
        Kind = (WeatherKind)r.Byte();
        RainStart = r.Float();
        RainEnd = r.Float();
        Rain = r.Float();
        Cloud = r.Float();
    }
}

/// <summary>Bir gunun hesabi (gun sonu raporu).</summary>
public sealed class DayLedger
{
    public int Day;
    public int Revenue;
    public int Tips;
    public int Supplies;
    public int Upgrades;
    public int Fines;
    public int Wages;
    public int Gas;
    public int Rent;
    public int ChangeLoss;
    public int Served;
    public int Angry;
    public int PriceRefused;
    public int Refused;
    public float SatisfactionSum;
    public int SatisfactionCount;
    public int Xp;
    public float RepStart;
    public float RepEnd;
    public int BestQuality;

    public int Income => Revenue + Tips;
    public int Expenses => Supplies + Upgrades + Fines + Wages + Gas + Rent + ChangeLoss;
    public int Profit => Income - Expenses;
    public float AvgSatisfaction => SatisfactionCount == 0 ? 0 : SatisfactionSum / SatisfactionCount;

    public void Write(NetWriter w)
    {
        w.Int(Day);
        foreach (var v in new[] { Revenue, Tips, Supplies, Upgrades, Fines, Wages, Gas, Rent, ChangeLoss, Served, Angry, PriceRefused, Refused, SatisfactionCount, Xp, BestQuality })
        {
            w.Int(v);
        }

        w.Float(SatisfactionSum);
        w.Float(RepStart);
        w.Float(RepEnd);
    }

    public void Read(NetReader r)
    {
        Day = r.Int();
        Revenue = r.Int();
        Tips = r.Int();
        Supplies = r.Int();
        Upgrades = r.Int();
        Fines = r.Int();
        Wages = r.Int();
        Gas = r.Int();
        Rent = r.Int();
        ChangeLoss = r.Int();
        Served = r.Int();
        Angry = r.Int();
        PriceRefused = r.Int();
        Refused = r.Int();
        SatisfactionCount = r.Int();
        Xp = r.Int();
        BestQuality = r.Int();
        SatisfactionSum = r.Float();
        RepStart = r.Float();
        RepEnd = r.Float();
    }
}

public sealed class Delivery
{
    public string SupplyId = "";
    public int Packs;
    public float ArriveAt;
    public bool Express;
}

public sealed class EconomyState
{
    public long Money;
    /// <summary>Menu fiyatlari (oyuncunun belirledigi).</summary>
    public Dictionary<string, int> Prices = new();
    /// <summary>Kiler stoku (stok anahtari -> miktar).</summary>
    public Dictionary<string, float> Stock = new();
    /// <summary>Toptanci fiyat endeksi (enflasyon).</summary>
    public float SupplyIndex = 1f;
    /// <summary>Musterinin kabul ettigi fiyat endeksi (enflasyonu biraz geriden izler).</summary>
    public float MarketIndex = 1f;
    public float LastInflation;
    public List<Delivery> Pending = new();
    public DayLedger Today = new();
    public List<DayLedger> History = new();

    public float StockOf(string key) => Stock.TryGetValue(key, out var v) ? v : 0f;

    public void AddStock(string key, float amount) => Stock[key] = MathF.Max(0, StockOf(key) + amount);

    public bool TakeStock(string key, float amount)
    {
        if (StockOf(key) + 1e-4f < amount)
        {
            return false;
        }

        Stock[key] = MathF.Max(0, StockOf(key) - amount);
        return true;
    }

    public int PriceOf(string menuId) => Prices.TryGetValue(menuId, out var p) ? p : 0;

    public void Write(NetWriter w)
    {
        w.Long(Money);
        w.VarInt(Prices.Count);
        foreach (var (k, v) in Prices)
        {
            w.String(k);
            w.VarInt(v);
        }

        w.VarInt(Stock.Count);
        foreach (var (k, v) in Stock)
        {
            w.String(k);
            w.Float(v);
        }

        w.Float(SupplyIndex);
        w.Float(MarketIndex);
        w.Float(LastInflation);
        w.VarInt(Pending.Count);
        foreach (var d in Pending)
        {
            w.String(d.SupplyId);
            w.VarInt(d.Packs);
            w.Float(d.ArriveAt);
            w.Bool(d.Express);
        }

        Today.Write(w);
        w.VarInt(History.Count);
        foreach (var h in History)
        {
            h.Write(w);
        }
    }

    public void Read(NetReader r)
    {
        Money = r.Long();
        Prices.Clear();
        var n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Prices[r.Str()] = r.VarInt();
        }

        Stock.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Stock[r.Str()] = r.Float();
        }

        SupplyIndex = r.Float();
        MarketIndex = r.Float();
        LastInflation = r.Float();
        Pending.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Pending.Add(new Delivery { SupplyId = r.Str(), Packs = r.VarInt(), ArriveAt = r.Float(), Express = r.Bool() });
        }

        Today.Read(r);
        History.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            var h = new DayLedger();
            h.Read(r);
            History.Add(h);
        }
    }
}

public sealed class ProgressState
{
    public int Xp;
    public HashSet<string> Upgrades = new();
    public HashSet<string> Achievements = new();
    public Dictionary<string, long> Stats = new();
    public bool CatMascot;
    public int TutorialStep;
    public bool TutorialEnabled = true;
    public string Paint = "";

    public bool Has(string upgrade) => Upgrades.Contains(upgrade);

    public long Stat(string key) => Stats.TryGetValue(key, out var v) ? v : 0;

    public void AddStat(string key, long amount = 1) => Stats[key] = Stat(key) + amount;

    public void MaxStat(string key, long value)
    {
        if (value > Stat(key))
        {
            Stats[key] = value;
        }
    }

    public static int LevelFor(int xp, int[] levels)
    {
        var lv = 1;
        for (var i = 1; i < levels.Length; i++)
        {
            if (xp >= levels[i])
            {
                lv = i + 1;
            }
        }

        return lv;
    }

    public void Write(NetWriter w)
    {
        w.Int(Xp);
        w.VarInt(Upgrades.Count);
        foreach (var u in Upgrades)
        {
            w.String(u);
        }

        w.VarInt(Achievements.Count);
        foreach (var a in Achievements)
        {
            w.String(a);
        }

        w.VarInt(Stats.Count);
        foreach (var (k, v) in Stats)
        {
            w.String(k);
            w.Long(v);
        }

        w.Bool(CatMascot);
        w.VarInt(TutorialStep);
        w.Bool(TutorialEnabled);
        w.String(Paint);
    }

    public void Read(NetReader r)
    {
        Xp = r.Int();
        Upgrades.Clear();
        var n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Upgrades.Add(r.Str());
        }

        Achievements.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Achievements.Add(r.Str());
        }

        Stats.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Stats[r.Str()] = r.Long();
        }

        CatMascot = r.Bool();
        TutorialStep = r.VarInt();
        TutorialEnabled = r.Bool();
        Paint = r.Str();
    }
}

public sealed class ReputationState
{
    /// <summary>0..5 yildiz.</summary>
    public float Stars = 2.5f;
    /// <summary>Son memnuniyetler (0..100), en fazla 60.</summary>
    public List<float> Recent = new();
    /// <summary>Nokta basina taninirlik 0..1 (orada satis yaptikca artar).</summary>
    public Dictionary<string, float> Fame = new();

    public float FameOf(string spot) => Fame.TryGetValue(spot, out var v) ? v : 0f;

    public void Write(NetWriter w)
    {
        w.Float(Stars);
        w.VarInt(Recent.Count);
        foreach (var v in Recent)
        {
            w.Byte((byte)Math.Clamp(v, 0, 100));
        }

        w.VarInt(Fame.Count);
        foreach (var (k, v) in Fame)
        {
            w.String(k);
            w.Float(v);
        }
    }

    public void Read(NetReader r)
    {
        Stars = r.Float();
        Recent.Clear();
        var n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Recent.Add(r.Byte());
        }

        Fame.Clear();
        n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            Fame[r.Str()] = r.Float();
        }
    }
}

public sealed class EventsState
{
    public bool ZabitaActive;
    public string ZabitaSpot = "";
    /// <summary>Mutlak oyun dakikasi.</summary>
    public float ZabitaDeadline;
    public int ZabitaVanId;
    public float NextFerry;
    public int FerryId;
    public bool MatchToday;
    /// <summary>Gun basi haberleri (Loc anahtari|arguman).</summary>
    public List<string> News = new();
    public int CatId;
    public int CatPetsToday;

    public void Write(NetWriter w)
    {
        w.Bool(ZabitaActive);
        w.String(ZabitaSpot);
        w.Float(ZabitaDeadline);
        w.Int(ZabitaVanId);
        w.Float(NextFerry);
        w.Int(FerryId);
        w.Bool(MatchToday);
        w.VarInt(News.Count);
        foreach (var n in News)
        {
            w.String(n);
        }

        w.Int(CatId);
        w.Int(CatPetsToday);
    }

    public void Read(NetReader r)
    {
        ZabitaActive = r.Bool();
        ZabitaSpot = r.Str();
        ZabitaDeadline = r.Float();
        ZabitaVanId = r.Int();
        NextFerry = r.Float();
        FerryId = r.Int();
        MatchToday = r.Bool();
        News.Clear();
        var n = r.VarInt();
        for (var i = 0; i < n; i++)
        {
            News.Add(r.Str());
        }

        CatId = r.Int();
        CatPetsToday = r.Int();
    }
}
