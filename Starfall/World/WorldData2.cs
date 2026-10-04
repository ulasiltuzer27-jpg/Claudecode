using System.Numerics;
using Starfall.Core;

namespace Starfall.World;

public sealed record ClimbWallDef(string Id, float X, float Z, float Yaw, float W, float H, bool Frosty, float BaseY = float.NaN);
public sealed record TreasureDef(string Id, string MapId, float X, float Z, string[] Contents, string Island);
public sealed record DigDef(string Id, float X, float Z, int Shells);
public sealed record PhotoSubject(string Id, Func<GameWorld, Vector3> Pos, float Radius, float MaxDist);

/// <summary>Asama B icerigi: Kar Adasi + Ada 1'e eklenenler. Kimlikler kayitta saklanir.</summary>
public static class WD2
{
    static Vector2 W(float u, float v) => L2.W(u, v);

    public static readonly NpcDef[] Npcs =
    {
        new("penguin", "penguin", L2.SledTop.X - 3, L2.SledTop.Y - 2, 2.2f, 1.6f, "isle2"),
        new("seal", "seal", L2.Lake.X - 14, L2.Lake.Y + 6, -2.0f, 0.9f, "isle2"),
        new("goat", "goat", L2.Training.X - 1.0f, L2.Training.Y + 4.5f, -0.8f, 1.0f, "isle2"),
        new("polarbear", "polarbear", BearShop.X + BearDir.X * 2.0f, BearShop.Y + BearDir.Y * 2.0f, MathF.Atan2(BearDir.X, BearDir.Y), 0.55f, "isle2"),
        new("mole", "mole", -34, 62, 2.4f, 1.2f),
        new("cat", "cat", 24, 116, -2.2f, 1.45f),
    };

    /// <summary>Baykusun Kar Adasi'ndaki yeri (rasathane kapisinin onu).</summary>
    public static Vector2 OwlIsle2 => new(L2.Summit.X - 4, L2.Summit.Y + 11);

    public static readonly CottageDef[] Cabins =
    {
        new("cabin1", W(-80, 118).X, W(-80, 118).Y, MathX.Pi * 0.95f, "#d9a46b", "#eef3fa"),
        new("cabin2", W(-70, 86).X, W(-70, 86).Y, MathX.Pi * 0.6f, "#c98f5a", "#eef3fa"),
        new("cabin3", W(-112, 78).X, W(-112, 78).Y, MathX.Pi * 0.15f, "#e3b98a", "#eef3fa"),
    };

    public static Vector2 BearShop => W(-82, 100);
    /// <summary>Dukkanin yuzu: limanin meydanina bakar.</summary>
    public static Vector2 BearDir => Vector2.Normalize(L2.Harbor - BearShop);
    public static float BearShopYaw => MathF.Atan2(BearDir.X, BearDir.Y);
    public static readonly Vector2[] Lanterns = { W(-92, 104), W(-80, 98), W(-96, 86), W(-60, 64), W(-40, 36), W(-110, 20), W(52, 30), W(14, -60) };
    public static readonly (float X, float Z, float Yaw, string Key)[] Signs =
    {
        (W(-86, 102).X, W(-86, 102).Y, -2.4f, "sign2.0"),
        (W(-30, 20).X, W(-30, 20).Y, -2.0f, "sign2.1"),
        (W(-104, -18).X, W(-104, -18).Y, 0.4f, "sign2.2"),
    };
    public static readonly Vector2[] Igloos = { W(-125, 100), W(64, 6) };

    /// <summary>15 Aurora Kristali: 10 dunyada + 5 gorev odulu.</summary>
    public static readonly Spot[] Crystals =
    {
        new("au01", On: "cabin1.roofTop"),
        new("au02", W(-112, -46).X + 6, W(-112, -46).Y - 6, 0.9f),
        new("au03", On: "lakeIslet"),
        new("au04", On: "sledTop"),
        new("au05", On: "summitLedge"),
        new("au06", On: "iceCaveInside"),
        new("au07", On: "spireTop"),
        new("au08", W(-20, 60).X, W(-20, 60).Y, 1.0f),
        new("au09", On: "observatoryTop"),
        new("au10", On: "coastStack"),
    };
    public static readonly string[] QuestCrystals = { "q_au_penguin", "q_au_seal", "q_au_goat", "q_au_bear", "q_au_cat" };

    public static readonly RegionDef[] Regions =
    {
        new("icecave", L2.Cave.X, L2.Cave.Y, 6, Secret: true, Island: "isle2"),
        new("observatory", L2.Summit.X, L2.Summit.Y, 26, 38, Island: "isle2"),
        new("hotspring", L2.Spring.X, L2.Spring.Y, 24, Island: "isle2"),
        new("frozenlake", L2.Lake.X, L2.Lake.Y, 34, Island: "isle2"),
        new("harbor", L2.Harbor.X, L2.Harbor.Y, 38, Island: "isle2"),
        new("pineslope", L2.SlopeMid.X, L2.SlopeMid.Y, 70, Island: "isle2"),
    };

    public static readonly FishDef[] Fish =
    {
        new("icefish", "ice", "any", 5, 0.3f, 12, 22),
        new("cod", "ice", "day", 3, 0.45f, 40, 80),
        new("salmon", "ice", "any", 2.5f, 0.55f, 50, 90),
        new("auroraeel", "ice", "night", 1.5f, 0.7f, 60, 110),
    };

    public static readonly Vector2[] IceHoles = { new(L2.Lake.X - 9, L2.Lake.Y + 3), new(L2.Lake.X - 3, L2.Lake.Y + 12), new(L2.Lake.X + 9, L2.Lake.Y + 8) };

    /// <summary>Tirmanma duvarlari. Yaw: duvar yuzunun baktigi yon (+Z yerel).</summary>
    public static readonly ClimbWallDef[] ClimbWalls =
    {
        // Kaya'nin egitim duvari (liman yakini; sarmasikli yuzu limana bakar)
        new("train", L2.Training.X, L2.Training.Y, -MathX.Pi * 0.25f, 4, 6, true),
        // rasathane ucurumu: iki parca + dinlenme cikintisi
        new("cliffA", L2.Summit.X - 17.2f, L2.Summit.Y + 14.6f, MathX.Pi * 0.78f, 4.5f, 9.5f, true),
        new("cliffB", L2.Summit.X - 15.9f, L2.Summit.Y + 13.4f, MathX.Pi * 0.78f, 4.5f, 8.5f, true, 9.4f),
        // Ada 1: zirveye kestirme sarmasik (istege bagli)
        new("peakVine", 6 + 24, -112 + 14, 2.1f, 3.5f, 7.5f, false),
    };

    public static readonly TreasureDef[] Treasures =
    {
        new("tr1", "map1", -100, 40, new[] { "map2", "outfit:face_monocle", "shells:10" }, "isle1"),
        new("tr2", "map2", 120, -14, new[] { "map3", "furn:chest", "shells:10" }, "isle1"),
        new("tr3", "map3", -60, -128, new[] { "map4", "outfit:scarf_pink", "furn:globe" }, "isle1"),
        new("tr4", "map4", W(-95, -75).X, W(-95, -75).Y, new[] { "map5", "outfit:face_sun", "furn:penguinplush" }, "isle2"),
        new("tr5", "map5", W(95, -20).X, W(95, -20).Y, new[] { "outfit:back_explorer", "furn:telescope", "shells:20" }, "isle2"),
    };

    public static readonly DigDef[] DigSpots =
    {
        new("dig1", -20, 150, 3), new("dig2", 46, 128, 3), new("dig3", -96, 30, 2), new("dig4", 60, -20, 2),
        new("dig5", -118, -58, 3), new("dig6", 128, 30, 3), new("dig7", -30, -90, 2), new("dig8", 84, 80, 3),
        new("dig9", W(-88, 70).X, W(-88, 70).Y, 4), new("dig10", W(-40, -10).X, W(-40, -10).Y, 4),
        new("dig11", W(40, 60).X, W(40, 60).Y, 4), new("dig12", W(-120, -10).X, W(-120, -10).Y, 4),
    };

    public static readonly PhotoSubject[] PhotoSubjects =
    {
        new("lighthouse", w => w.Anchor("lighthouse.lamp"), 3, 140),
        new("windmill", w => w.Anchor("windmill.sails"), 4, 90),
        new("shipwreck", w => w.Anchor("nest") + new Vector3(0, -4, 0), 4, 80),
        new("mushroom", w => w.Anchor("mushroom5"), 3, 50),
        new("observatory", w => w.Anchor("observatory.top"), 5, 160),
        new("igloo", w => new Vector3(Igloos[0].X, w.Height(Igloos[0].X, Igloos[0].Y) + 1, Igloos[0].Y), 2.5f, 50),
    };

    /// <summary>Penguen kizak yarisi: kizak tepesinden gol kiyisindaki bayraga.</summary>
    public static readonly Vector2[] SledPath = { L2.SledTop, W(24, -14), W(36, 0), W(48, 10), L2.SledBottom };

    public static Vector2 SledFinish => new(L2.SledBottom.X + 4, L2.SledBottom.Y + 4);
    /// <summary>Batik geminin guvertesinde (geminin yerel uzayinda, ambar kapaginin yaninda).</summary>
    public static readonly Vector3 SailLocal = new(-0.9f, 0, 1.6f);
    public static readonly Spot Mitten = new("mitten", On: "iceCaveFloor");

    /// <summary>Mina'nin evinin ic mekani (dunyanin disinda, gokyuzunde bir oda).</summary>
    public static readonly Vector3 HouseOrigin = new(-700, 300, 700);
    public const int HouseCellsX = 10, HouseCellsZ = 8;
    public const float HouseCell = 0.8f;

    public static readonly ShopItem[] BearShop2 =
    {
        new("hat_beanie", 18, "polarbear"), new("hat_earmuffs", 15, "polarbear"), new("face_goggles", 22, "polarbear"),
        new("scarf_purple", 8, "polarbear"), new("scarf_white", 8, "polarbear"), new("back_lantern", 25, "polarbear"),
        new("furn:stove", 20, "polarbear"), new("furn:snowglobe", 12, "polarbear"), new("furn:rugwinter", 10, "polarbear"),
    };

    public static readonly ShopItem[] HedgehogExtras =
    {
        new("face_round", 12), new("back_leaf", 15), new("scarf_blue", 6), new("scarf_green", 6), new("scarf_yellow", 6),
        new("furn:plant", 6), new("furn:lamp", 10), new("furn:stool", 5), new("furn:painting", 14),
    };
}
