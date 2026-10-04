using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>Toplanabilir esya modelleri (her biri bir kez uretilip paylasilir).</summary>
public static class CollectibleModels
{
    private static MeshData? _star, _feather, _shell, _carrot, _aurora, _chest, _map;
    private static readonly Dictionary<string, MeshData> _tools = new();

    public static readonly Material StarMat = new()
    {
        Tint = MathX.Hex("#ffe680"), Emissive = MathX.Hex("#ffc93d"), EmissiveIntensity = 1.1f,
    };

    public static readonly Material FeatherMat = new() { Emissive = MathX.Hex("#ffb020"), EmissiveIntensity = 0.6f };

    public static readonly Material AuroraMat = new() { Emissive = MathX.Hex("#5dffc8"), EmissiveIntensity = 1.2f };

    public static MeshData Star => _star ??= MeshData.From(
        Extrude(StarShape(0.42f, 0.19f), 0.12f, 0.08f, 0.06f).Center().Color(P.Star), true);

    public static MeshData Feather => _feather ??= BuildFeather();

    private static MeshData BuildFeather()
    {
        var shape = new Shape2D().MoveTo(0, -0.45f).QuadTo(0.24f, -0.1f, 0.06f, 0.5f).QuadTo(0, 0.56f, -0.06f, 0.5f).QuadTo(-0.24f, -0.1f, 0, -0.45f).Points();
        var blade = Extrude(shape, 0.03f, 0.015f, 0.015f).Translate(0, 0.05f, -0.015f).Color("#ffd54a");
        return MeshData.From(Merge(blade, Cyl("#fff1b0", 0.015f, 0.012f, 1.1f, V(0, 0, 0.03f), null, 4)), true);
    }

    public static MeshData Shell => _shell ??= BuildShell();

    private static MeshData BuildShell()
    {
        // tarak kabugu: yelpaze + dalgali kaburgalar
        const float R = 0.22f;
        var g = CircleGeo(R, 18, 0, MathX.Pi);
        for (int i = 0; i < g.Count; i++)
        {
            var p = g.P[i];
            float a = MathF.Atan2(p.Y, p.X);
            float r = MathX.Hypot(p.X, p.Y);
            float ridge = MathF.Cos(a * 18) * 0.012f * (r / R);
            g.P[i] = new Vector3(p.X, p.Y, ridge + MathF.Sqrt(MathF.Max(0, R * R - r * r)) * 0.4f);
        }
        g.SmoothNormals();
        var ca = MathX.Hex(P.Shell);
        var cb = MathX.Hex(P.ShellDark);
        for (int i = 0; i < g.Count; i++)
        {
            float r = MathX.Hypot(g.P[i].X, g.P[i].Y) / R;
            g.C[i] = Vector3.Lerp(ca, cb, r * 0.8f);
        }
        g.RotateX(-MathX.Pi / 2 + 0.5f);
        var m = Merge(g, Box(P.ShellDark, 0.1f, 0.05f, 0.06f, V(0, 0.02f, 0.02f))).Translate(0, 0.08f, 0);
        return MeshData.From(m, false);
    }

    public static MeshData Carrot => _carrot ??= MeshData.From(Merge(
        Cone(P.Carrot, 0.09f, 0.42f, V(0, 0.21f, 0), V(MathX.Pi, 0, 0), 7),
        Cone("#5fbf4a", 0.04f, 0.22f, V(0.03f, 0.5f, 0), V(0, 0, -0.3f), 4),
        Cone("#5fbf4a", 0.04f, 0.24f, V(-0.03f, 0.51f, 0), V(0, 0, 0.3f), 4),
        Cone("#4fa63a", 0.04f, 0.2f, V(0, 0.5f, 0.03f), V(0.3f, 0, 0), 4)), false);

    public static MeshData Tool(string kind)
    {
        if (_tools.TryGetValue(kind, out var m)) return m;
        Geo g;
        switch (kind)
        {
            case "hammer":
                g = Merge(Cyl("#b07a4f", 0.035f, 0.035f, 0.6f, V(0, 0.3f, 0), null, 6), Box(P.Metal, 0.3f, 0.12f, 0.12f, V(0, 0.62f, 0)));
                break;
            case "saw":
            {
                var parts = new List<Geo>
                {
                    Box("#cfd6dc", 0.6f, 0.18f, 0.02f, V(0.1f, 0.3f, 0)),
                    Box("#b07a4f", 0.16f, 0.2f, 0.05f, V(-0.28f, 0.32f, 0)),
                };
                for (int i = 0; i < 8; i++) parts.Add(Cone("#aeb6bd", 0.03f, 0.05f, V(-0.15f + i * 0.07f, 0.2f, 0), V(0, 0, MathX.Pi), 3));
                g = Merge(parts);
                break;
            }
            case "sail":
            {
                // katlanmis yelken bezi + ip (kunduzun yeni gorevi)
                g = Merge(
                    Box(P.Sail, 0.5f, 0.12f, 0.36f, V(0, 0.08f, 0), V(0, 0.2f, 0)),
                    Box("#efe6d2", 0.5f, 0.1f, 0.36f, V(0.02f, 0.19f, 0.01f), V(0, 0.1f, 0)),
                    Part(TorusGeo(0.14f, 0.02f, 4, 12), "#d8c49a", V(0.18f, 0.28f, 0), V(MathX.Pi / 2, 0, 0)));
                break;
            }
            default: // kurek
                g = Merge(
                    Cyl("#b07a4f", 0.03f, 0.03f, 0.7f, V(0, 0.45f, 0), null, 6),
                    Box(P.Metal, 0.22f, 0.26f, 0.03f, V(0, 0.08f, 0)),
                    Box("#b07a4f", 0.16f, 0.04f, 0.04f, V(0, 0.82f, 0)));
                break;
        }
        return _tools[kind] = MeshData.From(g, false);
    }

    /// <summary>Aurora Kristali (Kar Adasi): yesil-mor parlayan uclu kristal.</summary>
    public static MeshData Aurora => _aurora ??= MeshData.From(Merge(
        Part(Octahedron(0.22f), "#7dffd8", V(0, 0, 0), null, V(1, 2.1f, 1)),
        Part(Octahedron(0.13f), "#b18cff", V(0.16f, -0.12f, 0.05f), V(0, 0, -0.5f), V(1, 2f, 1)),
        Part(Octahedron(0.12f), "#8fe9ff", V(-0.15f, -0.14f, -0.04f), V(0, 0, 0.55f), V(1, 2f, 1))), true);

    /// <summary>Hazine sandigi (kazi): kapak ayri dugum degil, kapali halde.</summary>
    public static MeshData Chest => _chest ??= MeshData.From(Merge(
        Box("#9c6a3e", 0.9f, 0.5f, 0.6f, V(0, 0.25f, 0)),
        Part(CylinderGeo(0.3f, 0.3f, 0.9f, 10, 1, false, 0, MathX.Pi), "#b07a4f", V(0, 0.5f, 0), V(0, 0, MathX.Pi / 2)),
        Box(P.Gold, 0.94f, 0.06f, 0.08f, V(0, 0.5f, 0.27f)),
        Box(P.Gold, 0.08f, 0.62f, 0.64f, V(-0.3f, 0.36f, 0)),
        Box(P.Gold, 0.08f, 0.62f, 0.64f, V(0.3f, 0.36f, 0)),
        Box("#ffe680", 0.12f, 0.14f, 0.04f, V(0, 0.42f, 0.31f))), true);

    /// <summary>Hazine haritasi rulosu.</summary>
    public static MeshData TreasureMap => _map ??= MeshData.From(Merge(
        Cyl("#f3e3c0", 0.07f, 0.07f, 0.5f, V(0, 0.07f, 0), V(0, 0, MathX.Pi / 2), 10),
        Cyl("#c9443d", 0.075f, 0.075f, 0.04f, V(0, 0.07f, 0), V(0, 0, MathX.Pi / 2), 10)), false);
}
