using System.Text.Json.Serialization;
using PilavciSimulator.Core;

namespace PilavciSimulator.Sim.Data;

/// <summary>Toptancidan alinabilen malzeme.</summary>
public sealed class SupplyDef
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    /// <summary>Kilerdeki stok anahtari (ayni stoga birden cok urun dusebilir).</summary>
    public string Stock { get; set; } = "";
    public string Unit { get; set; } = "kg";
    /// <summary>Bir kolinin icindeki miktar (Unit cinsinden).</summary>
    public float Pack { get; set; } = 1;
    public int Price { get; set; }
    public int Level { get; set; } = 1;
    public string Category { get; set; } = "temel";
}

/// <summary>Menudeki bir kalem. Tabak kalemleri pilav + ustu; ekstralar yan urun.</summary>
public sealed class MenuItemDef
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    /// <summary>"plate" ya da "extra".</summary>
    public string Kind { get; set; } = "plate";
    /// <summary>pirinc / bulgur / etli</summary>
    public string? Base { get; set; }
    public List<string> Toppings { get; set; } = new();
    public int DefaultPrice { get; set; }
    /// <summary>Musterinin "normal" kabul ettigi fiyat (enflasyonla artar).</summary>
    public int RefPrice { get; set; }
    public int Level { get; set; } = 1;
}

public sealed class UpgradeDef
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string DescKey { get; set; } = "";
    public int Price { get; set; }
    public int Level { get; set; } = 1;
    /// <summary>ekipman / araba / ruhsat / personel / dukkan / kozmetik</summary>
    public string Category { get; set; } = "ekipman";
    /// <summary>Gunluk sabit gider (ciragin yevmiyesi, dukkan kirasi).</summary>
    public int Daily { get; set; }
    public string? Requires { get; set; }
    /// <summary>Kozmetikler icin renk (hex) ya da varyant indeksi.</summary>
    public string? Value { get; set; }
}

public sealed class SpotDef
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    /// <summary>En yogun saatte saatte gelen musteri (itibar 2.5'ta).</summary>
    public float BaseDemand { get; set; } = 20;
    /// <summary>24 saatlik talep carpani.</summary>
    public float[] Hours { get; set; } = new float[24];
    /// <summary>Haftanin gunu carpani (Pazartesi = 0).</summary>
    public float[] Days { get; set; } = [1, 1, 1, 1, 1, 1, 1];
    public float PriceSensitivity { get; set; } = 1f;
    public Dictionary<string, float> Mix { get; set; } = new();
    /// <summary>Bu noktanin ruhsat yukseltmesi; null ise ruhsat gerekmez (dukkan).</summary>
    public string? License { get; set; }
    /// <summary>Ruhsatsizken oyun saati basina zabita olasiligi.</summary>
    public float ZabitaRisk { get; set; }
    /// <summary>Stadyum gibi yalnizca etkinlik gunu dolan noktalar.</summary>
    public bool MatchDayOnly { get; set; }
}

public sealed class CustomerLooks
{
    public float Mustache { get; set; }
    public float Beard { get; set; }
    public float Headscarf { get; set; }
    public float Cap { get; set; }
    public float Hat { get; set; }
    public float HardHat { get; set; }
    public float Glasses { get; set; }
    public float Backpack { get; set; }
    public float Tie { get; set; }
    public float[] Height { get; set; } = [0.95f, 1.05f];
    public List<string> Shirts { get; set; } = new();
    public List<string> Pants { get; set; } = new();
    public bool Child { get; set; }
    public bool Elderly { get; set; }
}

public sealed class CustomerTypeDef
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    /// <summary>Kuyrukta bekleme sabri (oyun dakikasi).</summary>
    public float Patience { get; set; } = 6;
    public float PriceSensitivity { get; set; } = 1;
    public Dictionary<string, float> Prefs { get; set; } = new();
    /// <summary>Yarim / tam / duble agirliklari.</summary>
    public float[] Sizes { get; set; } = [0.2f, 0.65f, 0.15f];
    public float Package { get; set; } = 0.2f;
    public float Ayran { get; set; } = 0.4f;
    public float Tursu { get; set; } = 0.3f;
    public float Pepper { get; set; } = 0.3f;
    public float Tip { get; set; } = 0.2f;
    public float Card { get; set; } = 0.3f;
    public float Speed { get; set; } = 1.35f;
    /// <summary>Konusma tarzi: formal / casual / tourist / kid</summary>
    public string Voice { get; set; } = "casual";
    public CustomerLooks Looks { get; set; } = new();
}

public sealed class AchievementDef
{
    public string Id { get; set; } = "";
    public string Stat { get; set; } = "";
    public int Threshold { get; set; } = 1;
    public bool Hidden { get; set; }
}

/// <summary>Genel denge sabitleri (balance.json).</summary>
public sealed class BalanceDef
{
    public int StartMoney { get; set; } = 3000;
    public float DayStartMinute { get; set; } = 360;
    public float ClosingMinute { get; set; } = 1320;
    public float PassOutMinute { get; set; } = 1439;
    public float SleepAllowedMinute { get; set; } = 1020;
    /// <summary>Gercek saniye basina oyun dakikasi.</summary>
    public float MinutesPerSecond { get; set; } = 0.75f;
    public int[] XpLevels { get; set; } = [0, 120, 320, 650, 1100, 1700, 2500, 3500, 4800, 6500, 8800, 12000];
    public int ZabitaFine { get; set; } = 900;
    public int CartTowFine { get; set; } = 600;
    public float InflationMin { get; set; } = 0.04f;
    public float InflationMax { get; set; } = 0.12f;
    public int StartPlates { get; set; } = 16;
    public int PlatesUpgradeAmount { get; set; } = 16;
    public float ExpressDeliveryMarkup { get; set; } = 0.25f;
    public float ExpressDeliveryMinutes { get; set; } = 25;
    public float GasPerBurnerMinute { get; set; } = 0.18f;
}

/// <summary>Tum veri tanimlari. Data/*.json'dan okunur; host ve istemci ayni dosyalari kullanir.</summary>
public sealed class GameData
{
    public List<SupplyDef> Supplies { get; set; } = new();
    public List<MenuItemDef> Menu { get; set; } = new();
    public List<UpgradeDef> Upgrades { get; set; } = new();
    public List<SpotDef> Spots { get; set; } = new();
    public List<CustomerTypeDef> Customers { get; set; } = new();
    public List<AchievementDef> Achievements { get; set; } = new();
    public BalanceDef Balance { get; set; } = new();

    [JsonIgnore] public Dictionary<string, SupplyDef> SupplyById { get; private set; } = new();
    [JsonIgnore] public Dictionary<string, MenuItemDef> MenuById { get; private set; } = new();
    [JsonIgnore] public Dictionary<string, UpgradeDef> UpgradeById { get; private set; } = new();
    [JsonIgnore] public Dictionary<string, SpotDef> SpotById { get; private set; } = new();
    [JsonIgnore] public Dictionary<string, CustomerTypeDef> CustomerById { get; private set; } = new();

    public static GameData Load(string dataDir)
    {
        var d = new GameData
        {
            Supplies = JsonUtil.Read<List<SupplyDef>>(Path.Combine(dataDir, "supplies.json")),
            Menu = JsonUtil.Read<List<MenuItemDef>>(Path.Combine(dataDir, "menu.json")),
            Upgrades = JsonUtil.Read<List<UpgradeDef>>(Path.Combine(dataDir, "upgrades.json")),
            Spots = JsonUtil.Read<List<SpotDef>>(Path.Combine(dataDir, "spots.json")),
            Customers = JsonUtil.Read<List<CustomerTypeDef>>(Path.Combine(dataDir, "customers.json")),
            Achievements = JsonUtil.Read<List<AchievementDef>>(Path.Combine(dataDir, "achievements.json")),
            Balance = JsonUtil.Read<BalanceDef>(Path.Combine(dataDir, "balance.json")),
        };
        d.Index();
        d.Validate();
        return d;
    }

    public void Index()
    {
        SupplyById = Supplies.ToDictionary(s => s.Id);
        MenuById = Menu.ToDictionary(m => m.Id);
        UpgradeById = Upgrades.ToDictionary(u => u.Id);
        SpotById = Spots.ToDictionary(s => s.Id);
        CustomerById = Customers.ToDictionary(c => c.Id);
    }

    /// <summary>Veri tutarliligi: bozuk bir JSON oyunun ortasinda degil acilista patlasin.</summary>
    public void Validate()
    {
        foreach (var s in Spots)
        {
            if (s.Hours.Length != 24)
            {
                throw new InvalidDataException($"spots.json: {s.Id} 24 saat degeri tasimali");
            }

            if (s.Days.Length != 7)
            {
                throw new InvalidDataException($"spots.json: {s.Id} 7 gun degeri tasimali");
            }

            foreach (var t in s.Mix.Keys)
            {
                if (!CustomerById.ContainsKey(t))
                {
                    throw new InvalidDataException($"spots.json: {s.Id} bilinmeyen musteri tipi {t}");
                }
            }

            if (s.License is not null && !UpgradeById.ContainsKey(s.License))
            {
                throw new InvalidDataException($"spots.json: {s.Id} ruhsati {s.License} upgrades.json'da yok");
            }
        }

        foreach (var c in Customers)
        {
            foreach (var p in c.Prefs.Keys)
            {
                if (!MenuById.ContainsKey(p))
                {
                    throw new InvalidDataException($"customers.json: {c.Id} bilinmeyen menu kalemi {p}");
                }
            }
        }

        foreach (var u in Upgrades)
        {
            if (u.Requires is not null && !UpgradeById.ContainsKey(u.Requires))
            {
                throw new InvalidDataException($"upgrades.json: {u.Id} -> {u.Requires} yok");
            }
        }
    }
}
