using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>Mobilya tanimi: kaplama (hucre) + kaynak. Ad i18n'de furn.&lt;id&gt;.</summary>
public sealed record FurnitureDef(string Id, int W, int D, string Source, bool Wall = false, bool Rug = false, bool Light = false);

/// <summary>Mina'nin evi icin ~25 mobilya; hepsi kodla modellenmis (0.8 m hucre, taban y=0).</summary>
public static class Furniture
{
    public static readonly FurnitureDef[] All =
    {
        new("bed", 2, 3, "start"), new("table", 2, 2, "start"), new("chair", 1, 1, "start"), new("rug", 3, 2, "start", Rug: true),
        new("plant", 1, 1, "start"), new("lamp", 1, 1, "shop", Light: true), new("stool", 1, 1, "shop"), new("painting", 2, 1, "shop", Wall: true),
        new("bookshelf", 2, 1, "shop"), new("sofa", 3, 1, "shop"), new("chest", 2, 1, "treasure"), new("globe", 1, 1, "treasure"),
        new("penguinplush", 1, 1, "treasure"), new("telescope", 1, 1, "treasure"), new("photoframe", 1, 1, "quest", Wall: true),
        new("fireplace", 2, 1, "quest", Light: true), new("stove", 1, 1, "wintershop", Light: true), new("snowglobe", 1, 1, "wintershop"),
        new("rugwinter", 2, 2, "wintershop", Rug: true), new("foxplush", 1, 1, "achievement"), new("trophy", 1, 1, "race"),
        new("fishtank", 2, 1, "fish"), new("starlamp", 1, 1, "finale", Light: true), new("shelllamp", 1, 1, "shells", Light: true),
        new("banner", 1, 1, "quest", Wall: true), new("cactus", 1, 1, "shop"),
    };

    public static FurnitureDef? Get(string? id) => id == null ? null : All.FirstOrDefault(f => f.Id == id);

    private static readonly Dictionary<string, MeshData> Cache = new();

    public static MeshData Mesh(string id)
    {
        if (Cache.TryGetValue(id, out var m)) return m;
        return Cache[id] = MeshData.From(Build(id), true);
    }

    /// <summary>Yerel geometri: kaplamanin merkezi orijinde, taban y=0, on yuz +Z.</summary>
    public static Geo Build(string id)
    {
        switch (id)
        {
            case "bed":
                return Merge(
                    Box(P.Wood, 1.5f, 0.35f, 2.3f, V(0, 0.2f, 0)),
                    Box("#f4f1ea", 1.4f, 0.22f, 2.1f, V(0, 0.48f, 0.02f)),
                    Box("#7fb8ff", 1.42f, 0.12f, 1.3f, V(0, 0.6f, 0.4f)),
                    Box("#ffffff", 0.9f, 0.16f, 0.45f, V(0, 0.66f, -0.8f)),
                    Box(P.WoodDark, 1.55f, 0.9f, 0.12f, V(0, 0.45f, -1.15f)));
            case "table":
                return Merge(
                    Cyl(P.Wood, 0.75f, 0.75f, 0.08f, V(0, 0.74f, 0), null, 16),
                    Cyl(P.WoodDark, 0.08f, 0.12f, 0.7f, V(0, 0.36f, 0), null, 8),
                    Cyl(P.WoodDark, 0.4f, 0.45f, 0.06f, V(0, 0.03f, 0), null, 12),
                    Cyl("#ffffff", 0.12f, 0.1f, 0.12f, V(0.25f, 0.84f, 0.1f), null, 8),
                    Sphere("#ff8fb1", 0.06f, V(0.25f, 0.95f, 0.1f), null, 6, 4));
            case "chair":
                return Merge(
                    Box(P.Wood, 0.55f, 0.07f, 0.55f, V(0, 0.46f, 0)),
                    Box(P.Wood, 0.55f, 0.55f, 0.07f, V(0, 0.75f, -0.24f)),
                    Box(P.WoodDark, 0.06f, 0.46f, 0.06f, V(-0.22f, 0.23f, 0.22f)), Box(P.WoodDark, 0.06f, 0.46f, 0.06f, V(0.22f, 0.23f, 0.22f)),
                    Box(P.WoodDark, 0.06f, 0.46f, 0.06f, V(-0.22f, 0.23f, -0.22f)), Box(P.WoodDark, 0.06f, 0.46f, 0.06f, V(0.22f, 0.23f, -0.22f)),
                    Box("#ef6b8a", 0.48f, 0.05f, 0.48f, V(0, 0.52f, 0.02f)));
            case "rug":
                return Merge(Box("#e86a5a", 2.3f, 0.02f, 1.5f, V(0, 0.01f, 0)), Box("#ffd36b", 2.0f, 0.025f, 1.2f, V(0, 0.012f, 0)), Box("#e86a5a", 1.6f, 0.03f, 0.8f, V(0, 0.014f, 0)));
            case "rugwinter":
                return Merge(Cyl("#4a7bd0", 0.78f, 0.78f, 0.02f, V(0, 0.01f, 0), null, 24), Cyl("#ffffff", 0.6f, 0.6f, 0.025f, V(0, 0.012f, 0), null, 24), Cyl("#7fd0ff", 0.35f, 0.35f, 0.03f, V(0, 0.014f, 0), null, 24));
            case "plant":
                return Merge(Cyl("#d9825b", 0.2f, 0.15f, 0.3f, V(0, 0.15f, 0), null, 10), Blob("#4fa64a", 0.28f, 0.3f, 0.28f, 1).Translate(0, 0.52f, 0), Blob("#62b856", 0.18f, 0.22f, 0.18f, 1).Translate(0.1f, 0.74f, 0.05f));
            case "cactus":
                return Merge(Cyl("#d9825b", 0.16f, 0.13f, 0.22f, V(0, 0.11f, 0), null, 10), Cyl("#5fae5a", 0.1f, 0.1f, 0.5f, V(0, 0.45f, 0), null, 8), Sphere("#5fae5a", 0.1f, V(0, 0.7f, 0), null, 8, 6),
                    Cyl("#5fae5a", 0.06f, 0.06f, 0.2f, V(0.13f, 0.48f, 0), V(0, 0, -0.9f), 6), Sphere("#ff8fb1", 0.05f, V(0, 0.8f, 0), null, 6, 4));
            case "lamp":
                return Merge(Cyl(P.WoodDark, 0.15f, 0.18f, 0.05f, V(0, 0.025f, 0), null, 10), Cyl(P.Metal, 0.025f, 0.025f, 1.3f, V(0, 0.68f, 0), null, 6),
                    Cyl("#fff1c9", 0.18f, 0.28f, 0.32f, V(0, 1.42f, 0), null, 12));
            case "starlamp":
                return Merge(Cyl("#2b3346", 0.16f, 0.2f, 0.12f, V(0, 0.06f, 0), null, 10), Part(Extrude(StarShape(0.25f, 0.11f), 0.08f), "#ffe680", V(0, 0.45f, -0.04f)));
            case "shelllamp":
                return Merge(Cyl(P.WoodDark, 0.14f, 0.16f, 0.06f, V(0, 0.03f, 0), null, 10), Cyl(P.Metal, 0.02f, 0.02f, 0.4f, V(0, 0.25f, 0), null, 6),
                    Part(SphereGeo(0.22f, 12, 6, 0, MathX.TwoPi, 0, MathX.Pi / 2), P.Shell, V(0, 0.45f, 0), V(MathX.Pi, 0, 0), V(1, 0.8f, 1)));
            case "stool":
                return Merge(Cyl(P.Wood, 0.22f, 0.22f, 0.06f, V(0, 0.45f, 0), null, 10), Cyl(P.WoodDark, 0.04f, 0.04f, 0.42f, V(0.12f, 0.21f, 0.12f), null, 5),
                    Cyl(P.WoodDark, 0.04f, 0.04f, 0.42f, V(-0.12f, 0.21f, 0.12f), null, 5), Cyl(P.WoodDark, 0.04f, 0.04f, 0.42f, V(0, 0.21f, -0.15f), null, 5));
            case "painting":
                return Merge(Box(P.WoodDark, 1.3f, 0.9f, 0.06f, V(0, 1.6f, -0.37f)), Box("#8fd3ff", 1.15f, 0.75f, 0.07f, V(0, 1.6f, -0.36f)),
                    Box("#79c25a", 1.15f, 0.25f, 0.075f, V(0, 1.35f, -0.355f)), Box("#ffffff", 0.08f, 0.35f, 0.08f, V(0.25f, 1.55f, -0.35f)), Box("#e2584b", 0.1f, 0.08f, 0.085f, V(0.25f, 1.75f, -0.35f)));
            case "photoframe":
                return Merge(Box(P.Gold, 0.6f, 0.75f, 0.05f, V(0, 1.55f, -0.37f)), Box("#ffd36b", 0.5f, 0.65f, 0.055f, V(0, 1.55f, -0.365f)), Sphere(P.FoxOrange, 0.13f, V(0, 1.55f, -0.33f), V(1, 1, 0.3f), 8, 6));
            case "banner":
                return Merge(Cyl(P.WoodDark, 0.02f, 0.02f, 0.7f, V(0, 1.95f, -0.36f), V(0, 0, MathX.Pi / 2), 5),
                    Part(Extrude(new List<Vector2> { new(-0.3f, 0), new(0.3f, 0), new(0.3f, -0.7f), new(0, -0.55f), new(-0.3f, -0.7f) }, 0.02f), "#2fb5a8", V(0, 1.95f, -0.37f)),
                    Part(Extrude(StarShape(0.1f, 0.045f), 0.01f), "#ffe680", V(0, 1.65f, -0.345f)));
            case "bookshelf":
                return Merge(Box(P.Wood, 1.4f, 1.8f, 0.4f, V(0, 0.9f, -0.15f)), Box(P.WoodDark, 1.3f, 0.04f, 0.38f, V(0, 0.6f, -0.13f)), Box(P.WoodDark, 1.3f, 0.04f, 0.38f, V(0, 1.2f, -0.13f)),
                    Box("#e2584b", 0.12f, 0.4f, 0.3f, V(-0.5f, 0.82f, -0.1f)), Box("#4b8fe2", 0.1f, 0.36f, 0.3f, V(-0.36f, 0.8f, -0.1f)), Box("#55b073", 0.14f, 0.42f, 0.3f, V(-0.2f, 0.83f, -0.1f)),
                    Box("#ffd36b", 0.12f, 0.38f, 0.3f, V(0.3f, 1.41f, -0.1f)), Box("#c58bff", 0.1f, 0.34f, 0.3f, V(0.45f, 1.39f, -0.1f)), Sphere("#ffffff", 0.1f, V(0.3f, 0.72f, -0.1f), null, 8, 6));
            case "sofa":
                return Merge(Box("#4fb4ff", 2.2f, 0.4f, 0.75f, V(0, 0.25f, 0)), Box("#4fb4ff", 2.2f, 0.55f, 0.2f, V(0, 0.6f, -0.3f)),
                    Box("#3a9ae0", 0.2f, 0.55f, 0.75f, V(-1.05f, 0.4f, 0)), Box("#3a9ae0", 0.2f, 0.55f, 0.75f, V(1.05f, 0.4f, 0)), Box("#ffd36b", 0.4f, 0.3f, 0.12f, V(-0.6f, 0.62f, -0.15f), V(-0.2f, 0, 0)));
            case "chest":
                return Merge(Box("#9c6a3e", 1.2f, 0.55f, 0.6f, V(0, 0.28f, 0)), Part(CylinderGeo(0.3f, 0.3f, 1.2f, 10, 1, false, 0, MathX.Pi), "#b07a4f", V(0, 0.55f, 0), V(0, 0, MathX.Pi / 2)),
                    Box(P.Gold, 0.08f, 0.62f, 0.64f, V(-0.4f, 0.4f, 0)), Box(P.Gold, 0.08f, 0.62f, 0.64f, V(0.4f, 0.4f, 0)), Box("#ffe680", 0.14f, 0.16f, 0.04f, V(0, 0.48f, 0.31f)));
            case "globe":
                return Merge(Cyl(P.WoodDark, 0.18f, 0.22f, 0.06f, V(0, 0.03f, 0), null, 10), Cyl(P.WoodDark, 0.03f, 0.03f, 0.5f, V(0, 0.3f, 0), null, 5),
                    Part(TorusGeo(0.24f, 0.015f, 4, 16), P.Gold, V(0, 0.75f, 0), V(0, 0, 0.4f)), Sphere("#4fb4ff", 0.21f, V(0, 0.75f, 0), null, 14, 10), Blob("#79c25a", 0.1f, 0.08f, 0.06f, 1).Translate(0.1f, 0.8f, 0.15f));
            case "penguinplush":
                return Merge(Blob("#2b3346", 0.2f, 0.26f, 0.18f, 2).Translate(0, 0.26f, 0), Blob("#f4f1ea", 0.14f, 0.2f, 0.1f, 2).Translate(0, 0.24f, 0.1f),
                    Sphere("#2b3346", 0.15f, V(0, 0.56f, 0), null, 10, 8), Cone("#ffb02e", 0.04f, 0.1f, V(0, 0.54f, 0.16f), V(MathX.Pi / 2, 0, 0), 5), Sphere("#ff5a5a", 0.16f, V(0, 0.64f, 0), V(1, 0.6f, 1), 10, 6));
            case "foxplush":
                return Merge(Blob(P.FoxOrange, 0.2f, 0.22f, 0.2f, 2).Translate(0, 0.22f, 0), Sphere(P.FoxOrange, 0.17f, V(0, 0.5f, 0.05f), null, 10, 8),
                    Cone(P.FoxOrange, 0.06f, 0.14f, V(-0.09f, 0.68f, 0.03f), null, 5), Cone(P.FoxOrange, 0.06f, 0.14f, V(0.09f, 0.68f, 0.03f), null, 5),
                    Blob(P.FoxCream, 0.08f, 0.07f, 0.06f, 1).Translate(0, 0.45f, 0.18f), Blob(P.FoxOrange, 0.1f, 0.1f, 0.2f, 1).RotateX(-0.6f).Translate(0, 0.25f, -0.25f));
            case "telescope":
                return Merge(Cyl(P.WoodDark, 0.02f, 0.02f, 0.9f, V(0.15f, 0.42f, 0), V(0, 0, 0.18f), 5), Cyl(P.WoodDark, 0.02f, 0.02f, 0.9f, V(-0.15f, 0.42f, 0), V(0, 0, -0.18f), 5),
                    Cyl(P.WoodDark, 0.02f, 0.02f, 0.9f, V(0, 0.42f, -0.15f), V(0.18f, 0, 0), 5), Cyl("#5a6475", 0.07f, 0.1f, 0.8f, V(0, 0.95f, 0.1f), V(-0.9f, 0, 0), 10), Cyl(P.Gold, 0.11f, 0.11f, 0.05f, V(0, 1.2f, 0.4f), V(-0.9f, 0, 0), 10));
            case "fireplace":
                return Merge(Box("#b5aea2", 1.5f, 1.2f, 0.5f, V(0, 0.6f, -0.15f)), Box("#2b2420", 0.8f, 0.6f, 0.52f, V(0, 0.38f, -0.14f)), Box("#8f877c", 1.65f, 0.12f, 0.6f, V(0, 1.24f, -0.12f)),
                    Box("#ff9a3c", 0.4f, 0.25f, 0.2f, V(0, 0.2f, 0.0f)), Cone("#ffd27a", 0.12f, 0.3f, V(0, 0.35f, 0.02f), null, 5));
            case "stove":
                return Merge(Cyl("#3a3a44", 0.3f, 0.32f, 0.7f, V(0, 0.4f, 0), null, 12), Cyl("#2b2b33", 0.08f, 0.08f, 1.0f, V(0, 1.2f, -0.05f), null, 8),
                    Box("#ff9a3c", 0.2f, 0.15f, 0.05f, V(0, 0.35f, 0.3f)), Cyl("#3a3a44", 0.05f, 0.05f, 0.1f, V(0.2f, 0.05f, 0.2f), null, 5), Cyl("#3a3a44", 0.05f, 0.05f, 0.1f, V(-0.2f, 0.05f, 0.2f), null, 5));
            case "snowglobe":
                return Merge(Cyl(P.WoodDark, 0.16f, 0.18f, 0.1f, V(0, 0.05f, 0), null, 12), Sphere("#dff3ff", 0.16f, V(0, 0.24f, 0), null, 14, 10), Cone("#3f7f58", 0.07f, 0.16f, V(0, 0.22f, 0), null, 6), Sphere("#ffffff", 0.05f, V(0.06f, 0.15f, 0.04f), null, 6, 4));
            case "trophy":
                return Merge(Box(P.WoodDark, 0.3f, 0.12f, 0.3f, V(0, 0.06f, 0)), Cyl(P.Gold, 0.04f, 0.06f, 0.2f, V(0, 0.22f, 0), null, 8),
                    Part(SphereGeo(0.16f, 12, 6, 0, MathX.TwoPi, MathX.Pi / 2, MathX.Pi / 2), P.Gold, V(0, 0.45f, 0)), Part(TorusGeo(0.08f, 0.015f, 4, 10), P.Gold, V(0.17f, 0.4f, 0)), Part(TorusGeo(0.08f, 0.015f, 4, 10), P.Gold, V(-0.17f, 0.4f, 0)));
            case "fishtank":
                return Merge(Box(P.WoodDark, 1.5f, 0.5f, 0.6f, V(0, 0.25f, 0)), Box("#9fe3ff", 1.4f, 0.6f, 0.5f, V(0, 0.8f, 0)), Box("#e8d9a8", 1.38f, 0.08f, 0.48f, V(0, 0.54f, 0)),
                    Blob("#ff9a3c", 0.08f, 0.05f, 0.03f, 1).Translate(-0.3f, 0.85f, 0.1f), Blob("#ffd84a", 0.07f, 0.04f, 0.03f, 1).Translate(0.25f, 0.95f, -0.05f), Cone("#4fa64a", 0.06f, 0.3f, V(0.5f, 0.72f, 0), null, 4));
            default:
                return Box("#ff00ff", 0.5f, 0.5f, 0.5f, V(0, 0.25f, 0));
        }
    }
}
