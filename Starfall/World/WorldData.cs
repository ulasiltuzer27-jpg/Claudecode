using System.Numerics;
using Starfall.Core;
using L = Starfall.World.Isle1Shape.L;

namespace Starfall.World;

/// <summary>
/// Elle yerlestirilmis icerik konumu. Konum kurali: X/Z zorunlu (On verilmediyse). Y
/// verilmezse araziye oturtulur ve Dy kadar yukari kaldirilir. On verilirse ilgili yapinin
/// capasina gore konumlanir.
/// </summary>
public sealed record Spot(string Id, float X = 0, float Z = 0, float Dy = float.NaN, string? On = null, string? Kind = null);

public sealed record NpcDef(string Id, string Kind, float X, float Z, float Yaw, float Voice, string Island = "isle1");
public sealed record CottageDef(string Id, float X, float Z, float Yaw, string Wall, string Roof);
public sealed record MushroomDef(string Id, float X, float Z, float Cap, float H, string Color);
public sealed record UpdraftDef(float X, float Z, float R, float Top);
public sealed record RegionDef(string Id, float X, float Z, float R, float MinY = float.NegativeInfinity, bool Secret = false, string Island = "isle1");
public sealed record RockStackDef(string Id, float X, float Z, float S, float[] Stack);
public sealed record FishDef(string Id, string Water, string Time, float Weight, float Diff, float MinSize, float MaxSize);
public sealed record ShopItem(string Id, int Price, string Shop = "hedgehog");

/// <summary>Kayit dosyalari burada tanimlanan kimlikleri (s01, f2, sh_17...) saklar; bir
/// kimligi degistirmek eski kayitlardaki toplanmis esyayi "geri getirir".</summary>
public static class WD
{
    public static readonly (float X, float Z, float Yaw) Start = (8, 176, MathX.Pi);
    public static readonly (float X, float Z0, float Length, float Y) DockDef = (8, 157, 25, 1.25f);

    public static class Village
    {
        public static readonly Vector2 Campfire = new(8, 104);
        public static readonly CottageDef[] Cottages =
        {
            new("hut", -16, 126, MathX.Pi * 0.85f, "#fff4e0", "#e2584b"),
            new("c2", -20, 98, MathX.Pi * 0.42f, "#dcefff", "#4b8fe2"),
            new("c3", -4, 82, MathX.Pi * 0.12f, "#fff4e0", "#55b073"),
            new("c4", 22, 82, -MathX.Pi * 0.15f, "#ffe1dc", "#f2994a"),
            new("c5", 34, 102, -MathX.Pi * 0.48f, "#fff4e0", "#e2584b"),
            new("c6", 30, 124, -MathX.Pi * 0.8f, "#dcefff", "#4b8fe2"),
        };
        public static readonly (float X, float Z, float Yaw) Shop = (-6, 110, MathX.Pi / 2);
        public static readonly Vector2[] Lanterns = { new(0, 96), new(16, 96), new(16, 114), new(0, 114), new(8, 140), new(-8, 134), new(24, 134), new(8, 124) };
        public static readonly (float X, float Z, float Yaw)[] Crates = { (-13.4f, 128.6f, 0.2f), (-12.2f, 129.4f, 0.5f), (-12.8f, 129.0f, 0) };
        public static readonly Vector2[] Barrels = { new(-10, 112), new(-11, 113.5f), new(38, 108) };
        public static readonly (float X, float Z, float Yaw)[] Benches = { (20, 106, -MathX.Pi / 2), (-2, 100, MathX.Pi / 2) };
        public static readonly (float X, float Z, float Yaw)[] Signs = { (4, 92, 0.6f), (46, 106, -1.2f), (-30, 70, 0.9f) };
        public static readonly Vector2[][] Fences =
        {
            new[] { new Vector2(-28, 112), new Vector2(-34, 104), new Vector2(-32, 94) },
            new[] { new Vector2(40, 90), new Vector2(46, 96), new Vector2(46, 112) },
        };
        public static readonly Vector2[] Palms = { new(-26, 140), new(-12, 146), new(24, 146), new(38, 138), new(46, 132), new(-36, 134), new(56, 128), new(18, 152) };
    }

    public static class Landmarks
    {
        public static readonly (float X, float Z, float Yaw) Lighthouse = (L.Peak.X, L.Peak.Y - 2, 0);
        public static readonly (float X, float Z, float Yaw) Windmill = (98, -62, MathX.Pi * 0.75f);
        public static readonly Vector2 BridgeA = new(75, 104), BridgeB = new(99, 104);
        public static readonly (float X, float Z, float Yaw) Shipwreck = (146, 58, 0.9f);
        public static readonly (float X, float Z, float Yaw) Dome = (L.Islet.X, L.Islet.Y, 0); // giris +X: adaya bakar
        public static readonly Vector2 RaceFlag = new(-60, 30);
        public static readonly Vector2 RabbitGarden = new(-50, 50);
        public static readonly Vector2 Stump = new(-128, -22);
        public static readonly Vector2 Sandcastle = new(36, 142);
        public static readonly Vector2 MeadowStump = new(-40, 40);
        public static readonly Vector2 Spire = new(116, -38);
    }

    /// <summary>Gizli magara icin yon tabelasi: kiyidan adacigi gosterir.</summary>
    public static readonly (float X, float Z, float Yaw) HintSign = (-128, -96, -2.2f);

    public static readonly NpcDef[] Npcs =
    {
        new("owl", "owl", 8, 150, MathX.Pi, 0.8f),
        new("owlPeak", "owl", L.Peak.X + 3, L.Peak.Y + 6, 0.3f, 0.8f),
        new("hedgehog", "hedgehog", -4.2f, 112.4f, MathX.Pi / 2 + 0.35f, 1.3f),
        new("frog", "frog", 6, -6, -0.8f, 1.5f),
        new("bear", "bear", 132, 50, 0.9f, 0.6f),
        new("rabbit", "rabbit", -46, 54, 2.6f, 1.4f),
        new("beaver", "beaver", 70, 108, -2.0f, 1.1f),
    };

    public static NpcDef Npc(string id) => Npcs.First(n => n.Id == id);

    /// <summary>26 yerlestirilmis yildiz parcasi (+4 gorev odulu = 30).</summary>
    public static readonly Spot[] Shards =
    {
        new("s01", On: "hutRoof"),
        new("s02", On: "boat"),
        new("s03", On: "shopRoof"),
        new("s04", -74, 40, 1.0f, "meadowRock"),
        new("s05", -50, 50, 0.9f),
        new("s06", On: "meadowStack"),
        new("s07", -102, 22, 0.9f),
        new("s08", On: "stump"),
        new("s09", -142, 2, 0.9f),
        new("s10", 30, 2, 1.0f),
        new("s11", On: "lakeRock"),
        new("s12", Dy: 4.2f, On: "mushroom2"),
        new("s13", On: "mushroom5"),
        new("s14", -92, -84, 0.9f),
        new("s15", 112, -44, 0.9f),
        new("s16", On: "spire"),
        new("s17", 30, -108, 0.9f),
        new("s18", L.Peak.X - 6, L.Peak.Y + 8, 0.9f),
        new("s19", -10, -136, 0.9f),
        new("s20", On: "nest"),
        new("s21", On: "deck"),
        new("s22", On: "seaStack"),
        new("s23", On: "domeInside"),
        new("s24", -128, 72, 0.9f),
        new("s25", On: "inletStack"),
        new("s26", On: "sandcastle"),
    };

    public static readonly string[] QuestShards = { "q_beaver", "q_frog", "q_bear", "q_shop" };
    public const int ShardTotal = 30;
    public const int ShardsForFinale = 20;

    public static readonly Spot[] Feathers =
    {
        new("f1", On: "meadowStump"),
        new("f2", On: "forestLedge"),
        new("f3", Dy: 4.4f, On: "mushroom4"),
        new("f4", 104, -54, 1.0f),
    };
    public const int FeatherTotal = 8; // 4 dunyada + baykus + kirpi + tavsan + ayi

    public static readonly Spot[] Carrots =
    {
        new("carrot1", -104, 6),
        new("carrot2", -70, 64),
        new("carrot3", 14, 30),
    };

    public static readonly Spot[] Tools =
    {
        new("tool_hammer", -78, -72, Kind: "hammer"),
        new("tool_saw", 90, -46, Kind: "saw"),
        new("tool_shovel", 36, 136, Kind: "shovel"),
    };

    /// <summary>20 grup x 5 kabuk = 100. (x, z, yon derece)</summary>
    public static readonly (float X, float Z, float Deg)[] ShellGroups =
    {
        (30, 140, 0), (38, 130, 30), (-2, 150, 90), (20, 148, 0), (-24, 132, 120),
        (-40, 76, 40), (-80, 52, 0), (-112, 36, 70), (-124, 42, 90), (-128, -40, 80),
        (-104, -60, 20), (-62, -96, 150), (-20, -70, 60), (48, -24, 10), (70, -36, 40),
        (56, 60, 90), (86, 30, 120), (126, 44, 0), (118, 84, 60), (134, 22, 100),
    };

    public static readonly MushroomDef[] GiantMushrooms =
    {
        new("mushroom1", -82, -70, 2.2f, 2.4f, "#e8524a"),
        new("mushroom2", -90, -76, 2.0f, 3.6f, "#5aa8e8"),
        new("mushroom3", -78, -86, 2.4f, 2.8f, "#ff8fb1"),
        new("mushroom4", -96, -86, 2.0f, 4.6f, "#e8524a"),
        new("mushroom5", -88, -94, 1.8f, 8.6f, "#b98aff"),
        new("mushroom6", -72, -78, 1.6f, 3.2f, "#ffcf4a"),
    };

    public static readonly UpdraftDef[] Updrafts =
    {
        new(108, -46, 3.2f, 40),
        new(140, 52, 2.8f, 18),
    };

    /// <summary>Kurbaga yarisinin rotasi (kurbaga ayni yolu ziplaya ziplaya izler).</summary>
    public static readonly Vector2[] RacePath = { new(6, -6), new(-6, -2), new(-22, 4), new(-38, 14), new(-50, 22), new(-60, 30) };
    public const float RaceFrogSpeed = 6.6f;

    public static readonly RegionDef[] Regions =
    {
        new("cave", L.Islet.X, L.Islet.Y, 5.5f, Secret: true),
        new("peak", L.Peak.X, L.Peak.Y, 33, 26),
        new("windy", L.Windy.X, L.Windy.Y, 40, 12),
        new("hollow", L.Hollow.X, L.Hollow.Y, 34),
        new("lake", L.Lake.X, L.Lake.Y, 33),
        new("cove", L.Cove.X, L.Cove.Y, 38),
        new("forest", L.Forest.X, L.Forest.Y, 46),
        new("meadow", L.Meadow.X, L.Meadow.Y, 40),
        new("village", L.Village.X, L.Village.Y + 8, 52),
    };

    public static IEnumerable<string> MainRegions(string island = "isle1") =>
        Regions.Where(r => !r.Secret && r.Island == island).Select(r => r.Id);

    public static readonly ShopItem[] ShopItems =
    {
        new("rod", 10),
        new("feather", 25),
        new("shard", 40),
        new("hat", 20),
    };

    public static readonly FishDef[] Fish =
    {
        new("anchovy", "sea", "any", 5, 0.25f, 9, 16),
        new("seabass", "sea", "day", 3, 0.45f, 30, 60),
        new("trout", "lake", "any", 5, 0.3f, 20, 40),
        new("carp", "lake", "day", 3, 0.5f, 35, 70),
        new("moonfish", "any", "night", 3, 0.55f, 25, 45),
        new("goldfish", "lake", "any", 0.8f, 0.7f, 8, 14),
    };

    /// <summary>Ekstra kayalar (platform). Yildiz/tuy yerlesimleri bunlara bagli.</summary>
    public static readonly RockStackDef[] PlatformRocks =
    {
        new("meadowRock", -74, 40, 1.8f, new[] { 1.8f }),
        new("meadowStack", -36, 22, 1.6f, new[] { 1.6f, 1.25f, 0.95f }),
        new("lakeRock", 41, 6, 2.0f, new[] { 2.0f, 1.6f, 1.2f }),
        new("forestLedge", -132, -32, 1.5f, new[] { 1.5f, 1.1f }),
        new("seaStack", 158, 40, 2.2f, new[] { 2.4f, 1.9f }),
        new("inletStack", 80, 140, 2.6f, new[] { 2.6f, 2.1f, 1.6f }),
    };
}
