namespace Starfall.Core;

/// <summary>Bir kayit yuvasinin icerigi (slot_N.json).</summary>
public sealed class SaveData
{
    public int Version { get; set; } = SaveManager.Version;
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long Updated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public double PlayTime { get; set; }
    public float[]? Pos { get; set; }
    public float Yaw { get; set; } = MathF.PI;
    public float Hour { get; set; } = 8.5f;
    public string Island { get; set; } = "isle1";
    public List<string> Collected { get; set; } = new();
    public List<string> Rewards { get; set; } = new();
    public int Shells { get; set; }
    public int ShellsTotal { get; set; }
    public Dictionary<string, string> Quests { get; set; } = new();
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, int> Fish { get; set; } = new();
    public Dictionary<string, float> FishBest { get; set; } = new();
    public List<string> Regions { get; set; } = new();
    public List<string> Talked { get; set; } = new();
    public double? FinaleTime { get; set; }
    public double? Finale2Time { get; set; }
    // genisleme: kiyafetler, ev, hazine, fotograf konulari
    public List<string> Outfits { get; set; } = new() { "scarf_red" };
    public Dictionary<string, string> Worn { get; set; } = new() { ["Scarf"] = "scarf_red" };
    public List<string> Furniture { get; set; } = new();
    public List<PlacedFurniture> House { get; set; } = new();
    public List<string> Photos { get; set; } = new();
    public int Auroras { get; set; }
    public Dictionary<string, float> Best { get; set; } = new();

    public bool Flag(string k) => Flags.TryGetValue(k, out var v) && v;

    public string Quest(string id) => Quests.TryGetValue(id, out var v) ? v : "none";

    public static readonly string[] QuestIds = { "rabbit", "beaver", "frog", "bear" };

    public void Normalize()
    {
        foreach (var q in QuestIds) Quests.TryAdd(q, "none");
        if (Outfits.Count == 0) Outfits.Add("scarf_red");
        if (!Outfits.Contains("scarf_red")) Outfits.Add("scarf_red");
        Version = SaveManager.Version;
    }
}

public sealed class PlacedFurniture
{
    public string Id { get; set; } = "";
    public int X { get; set; }
    public int Z { get; set; }
    public int Rot { get; set; }
}

/// <summary>Profil: hesaba ait istatistikler + basarimlar + son yuva (yuva silinse de kalir).</summary>
public sealed class ProfileData
{
    public int Version { get; set; } = 1;
    public Dictionary<string, double> Stats { get; set; } = new();
    public Dictionary<string, long> Unlocked { get; set; } = new();
    public int? LastSlot { get; set; }
}

/// <summary>Kayit yuvalari (3) + profil.</summary>
public sealed class SaveManager
{
    public const int Version = 2;
    public const int Slots = 3;

    public static SaveData NewSave()
    {
        var s = new SaveData();
        s.Normalize();
        return s;
    }

    public ProfileData LoadProfile() => Storage.Read<ProfileData>("profile") ?? new ProfileData();

    public void SaveProfile(ProfileData p) => Storage.Write("profile", p);

    public SaveData? LoadSlot(int i)
    {
        var d = Storage.Read<SaveData>($"slot_{i}");
        d?.Normalize();
        return d;
    }

    public void SaveSlot(int i, SaveData d)
    {
        d.Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Storage.Write($"slot_{i}", d);
    }

    public void DeleteSlot(int i) => Storage.Remove($"slot_{i}");

    public List<(int Slot, SaveData? Data)> ListSlots()
    {
        var o = new List<(int, SaveData?)>();
        for (int i = 1; i <= Slots; i++) o.Add((i, LoadSlot(i)));
        return o;
    }
}
