using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

public enum OutfitSlot { Hat, Face, Back, Scarf }

/// <summary>
/// Kiyafet katalogu. Her ogenin kimligi kayitta saklanir (sahip olunanlar + giyilenler);
/// ad ve aciklamasi i18n'de outfit.&lt;id&gt; / outfit.&lt;id&gt;.desc. Kaynak: dukkan, hazine,
/// gorev odulu veya basarim.
/// </summary>
public sealed record OutfitDef(string Id, OutfitSlot Slot, string Source, int Price = 0, string? Color = null);

public static class Outfits
{
    public static readonly OutfitDef[] All =
    {
        // sapkalar
        new("hat_straw", OutfitSlot.Hat, "shop", 20),
        new("hat_beanie", OutfitSlot.Hat, "wintershop", 18),
        new("hat_flowers", OutfitSlot.Hat, "quest"),
        new("hat_explorer", OutfitSlot.Hat, "treasure"),
        new("hat_crown", OutfitSlot.Hat, "finale"),
        new("hat_party", OutfitSlot.Hat, "achievement"),
        new("hat_sailor", OutfitSlot.Hat, "quest"),
        new("hat_earmuffs", OutfitSlot.Hat, "wintershop", 15),
        // gozlukler
        new("face_round", OutfitSlot.Face, "shop", 12),
        new("face_sun", OutfitSlot.Face, "treasure"),
        new("face_star", OutfitSlot.Face, "quest"),
        new("face_goggles", OutfitSlot.Face, "wintershop", 22),
        new("face_monocle", OutfitSlot.Face, "treasure"),
        // sirt
        new("back_leaf", OutfitSlot.Back, "shop", 15),
        new("back_explorer", OutfitSlot.Back, "treasure"),
        new("back_shell", OutfitSlot.Back, "quest"),
        new("back_balloon", OutfitSlot.Back, "achievement"),
        new("back_lantern", OutfitSlot.Back, "wintershop", 25),
        // atki renkleri (renk degisimi; model yok)
        new("scarf_mina", OutfitSlot.Scarf, "start", 0, P.Scarf),
        new("scarf_blue", OutfitSlot.Scarf, "shop", 6, "#3f8fe8"),
        new("scarf_green", OutfitSlot.Scarf, "shop", 6, "#4fbf6a"),
        new("scarf_yellow", OutfitSlot.Scarf, "shop", 6, "#ffcf3a"),
        new("scarf_purple", OutfitSlot.Scarf, "wintershop", 8, "#a46bff"),
        new("scarf_pink", OutfitSlot.Scarf, "treasure", 0, "#ff8fc1"),
        new("scarf_white", OutfitSlot.Scarf, "wintershop", 8, "#f4f6fb"),
        new("scarf_red", OutfitSlot.Scarf, "quest", 0, "#e5484d"),
    };

    public static OutfitDef? Get(string? id) => id == null ? null : All.FirstOrDefault(o => o.Id == id);

    private static readonly Dictionary<string, MeshData> _cache = new();

    /// <summary>Kiyafetin yerel geometrisi (yuvanin uzayinda). Atki icin null.</summary>
    public static MeshData? Mesh(string id)
    {
        if (_cache.TryGetValue(id, out var m)) return m;
        var g = Build(id);
        if (g == null) return null;
        return _cache[id] = MeshData.From(g, false);
    }

    private static Geo? Build(string id)
    {
        switch (id)
        {
            case "hat_straw":
                return Merge(
                    Cyl("#f2d58a", 0.3f, 0.3f, 0.025f, null, null, 16),
                    Cyl("#f2d58a", 0.15f, 0.17f, 0.13f, V(0, 0.07f, 0), null, 14),
                    Cyl("#e2584b", 0.172f, 0.172f, 0.04f, V(0, 0.03f, 0), null, 14),
                    Sphere("#fff3a8", 0.04f, V(0.16f, 0.05f, 0.06f), null, 8, 6),
                    Sphere("#ff8fb1", 0.03f, V(0.19f, 0.06f, 0.02f), null, 6, 4));
            case "hat_beanie":
                return Merge(
                    Part(SphereGeo(0.2f, 14, 8, 0, MathX.TwoPi, 0, MathX.Pi / 2), "#3f8fe8", V(0, -0.02f, 0), null, V(1, 0.9f, 1)),
                    Cyl("#f4f6fb", 0.205f, 0.205f, 0.06f, V(0, -0.01f, 0), null, 16),
                    Sphere("#f4f6fb", 0.065f, V(0, 0.2f, 0), null, 8, 6));
            case "hat_earmuffs":
                return Merge(
                    Part(TorusGeo(0.2f, 0.018f, 5, 16, MathX.Pi), "#e8e8ee", V(0, -0.05f, 0), V(0, 0, 0)),
                    Sphere("#ff8fb1", 0.075f, V(-0.2f, -0.06f, 0), V(0.7f, 1, 1), 10, 8),
                    Sphere("#ff8fb1", 0.075f, V(0.2f, -0.06f, 0), V(0.7f, 1, 1), 10, 8));
            case "hat_flowers":
            {
                var parts = new List<Geo> { Part(TorusGeo(0.17f, 0.025f, 5, 18), "#5fbf4a", V(0, 0.01f, 0), V(MathX.Pi / 2, 0, 0)) };
                string[] cols = { "#ff7aa2", "#ffd84a", "#ffffff", "#c58bff", "#ff9a3c" };
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * MathX.TwoPi;
                    parts.Add(Sphere(cols[i % cols.Length], 0.045f, V(MathF.Cos(a) * 0.17f, 0.03f, MathF.Sin(a) * 0.17f), V(1, 0.6f, 1), 6, 4));
                }
                return Merge(parts);
            }
            case "hat_explorer":
                return Merge(
                    Cyl("#d9c08a", 0.32f, 0.32f, 0.02f, null, null, 18),
                    Part(SphereGeo(0.17f, 14, 8, 0, MathX.TwoPi, 0, MathX.Pi / 2), "#d9c08a", V(0, 0.0f, 0), null, V(1, 0.85f, 1)),
                    Cyl("#7a5230", 0.172f, 0.172f, 0.035f, V(0, 0.025f, 0), null, 16));
            case "hat_crown":
            {
                var parts = new List<Geo> { Cyl(P.Gold, 0.15f, 0.15f, 0.08f, V(0, 0.04f, 0), null, 12) };
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * MathX.TwoPi;
                    parts.Add(Cone(P.Gold, 0.04f, 0.09f, V(MathF.Cos(a) * 0.14f, 0.12f, MathF.Sin(a) * 0.14f), null, 4));
                    parts.Add(Sphere(i % 2 == 0 ? "#ff5a7a" : "#5fd0ff", 0.02f, V(MathF.Cos(a) * 0.152f, 0.045f, MathF.Sin(a) * 0.152f), null, 6, 4));
                }
                return Merge(parts);
            }
            case "hat_party":
                return Merge(
                    Cone("#b98aff", 0.12f, 0.32f, V(0, 0.15f, 0), null, 12),
                    Part(TorusGeo(0.105f, 0.012f, 4, 12), "#ffd84a", V(0, 0.06f, 0), V(MathX.Pi / 2, 0, 0)),
                    Part(TorusGeo(0.06f, 0.012f, 4, 12), "#ff7aa2", V(0, 0.18f, 0), V(MathX.Pi / 2, 0, 0)),
                    Sphere("#ffd84a", 0.04f, V(0, 0.32f, 0), null, 8, 6));
            case "hat_sailor":
                return Merge(
                    Cyl("#f4f6fb", 0.19f, 0.2f, 0.09f, V(0, 0.03f, 0), null, 16),
                    Cyl("#2b4f8f", 0.205f, 0.205f, 0.03f, V(0, 0.0f, 0), null, 16),
                    Sphere("#f2c94c", 0.03f, V(0, 0.04f, 0.19f), V(1, 1, 0.4f), 6, 4));
            case "face_round":
                return Merge(
                    Part(TorusGeo(0.055f, 0.009f, 5, 16), "#3a3a44", V(-0.095f, 0, 0.02f)),
                    Part(TorusGeo(0.055f, 0.009f, 5, 16), "#3a3a44", V(0.095f, 0, 0.02f)),
                    Box("#3a3a44", 0.05f, 0.01f, 0.01f, V(0, 0.01f, 0.02f)));
            case "face_sun":
                return Merge(
                    Part(SphereGeo(0.06f, 12, 8), "#20232c", V(-0.095f, 0, 0.02f), null, V(1.1f, 0.8f, 0.25f)),
                    Part(SphereGeo(0.06f, 12, 8), "#20232c", V(0.095f, 0, 0.02f), null, V(1.1f, 0.8f, 0.25f)),
                    Box("#20232c", 0.26f, 0.015f, 0.015f, V(0, 0.035f, 0.02f)));
            case "face_star":
            {
                var star = Extrude(StarShape(0.07f, 0.035f), 0.015f);
                return Merge(
                    Part(star.Clone(), "#ffd84a", V(-0.095f, 0, 0.02f)),
                    Part(star, "#ffd84a", V(0.095f, 0, 0.02f)),
                    Box("#ffb627", 0.05f, 0.01f, 0.01f, V(0, 0.01f, 0.025f)));
            }
            case "face_goggles":
                return Merge(
                    Part(TorusGeo(0.06f, 0.016f, 6, 14), "#ffb02e", V(-0.075f, 0.02f, 0.02f)),
                    Part(TorusGeo(0.06f, 0.016f, 6, 14), "#ffb02e", V(0.075f, 0.02f, 0.02f)),
                    Sphere("#7fd1ff", 0.055f, V(-0.075f, 0.02f, 0.02f), V(1, 1, 0.3f), 8, 6),
                    Sphere("#7fd1ff", 0.055f, V(0.075f, 0.02f, 0.02f), V(1, 1, 0.3f), 8, 6),
                    Part(TorusGeo(0.2f, 0.012f, 4, 18, MathX.Pi * 1.2f), "#3a3a44", V(0, 0.02f, -0.06f), V(MathX.Pi / 2, 0, -MathX.Pi * 0.1f)));
            case "face_monocle":
                return Merge(
                    Part(TorusGeo(0.055f, 0.01f, 6, 16), P.Gold, V(0.095f, 0, 0.025f)),
                    Sphere("#cfe8ff", 0.05f, V(0.095f, 0, 0.023f), V(1, 1, 0.2f), 8, 6),
                    Cyl(P.Gold, 0.004f, 0.004f, 0.18f, V(0.14f, -0.09f, 0.0f), V(0, 0, 0.4f), 4));
            case "back_leaf":
                return Merge(
                    Blob("#4fa64a", 0.13f, 0.14f, 0.07f, 1).Translate(0, 0.02f, -0.04f),
                    Blob("#62b856", 0.09f, 0.08f, 0.04f, 1).Translate(0, -0.02f, -0.1f),
                    Box("#7a5230", 0.025f, 0.2f, 0.02f, V(-0.08f, 0.02f, 0.03f)),
                    Box("#7a5230", 0.025f, 0.2f, 0.02f, V(0.08f, 0.02f, 0.03f)));
            case "back_explorer":
                return Merge(
                    Box("#a8744a", 0.22f, 0.22f, 0.12f, V(0, 0.02f, -0.06f)),
                    Box("#8a5a34", 0.23f, 0.06f, 0.13f, V(0, 0.11f, -0.06f)),
                    Cyl("#e6d3a8", 0.04f, 0.04f, 0.26f, V(0, 0.16f, -0.08f), V(0, 0, MathX.Pi / 2), 8),
                    Box(P.Gold, 0.03f, 0.04f, 0.01f, V(0, 0.07f, 0.0f)));
            case "back_shell":
                return Merge(
                    Part(SphereGeo(0.13f, 12, 8, 0, MathX.TwoPi, 0, MathX.Pi / 2), P.Shell, V(0, 0, -0.06f), V(-MathX.Pi / 2, 0, 0), V(1, 1, 0.7f)),
                    Part(TorusGeo(0.13f, 0.012f, 4, 16), P.ShellDark, V(0, 0, -0.06f)));
            case "back_balloon":
                return Merge(
                    Cyl("#f4f6fb", 0.004f, 0.004f, 0.7f, V(0.02f, 0.32f, -0.06f), V(0, 0, -0.08f), 3),
                    Sphere("#ff5a7a", 0.14f, V(0.05f, 0.75f, -0.06f), V(1, 1.15f, 1), 12, 10),
                    Cone("#ff5a7a", 0.03f, 0.05f, V(0.05f, 0.6f, -0.06f), V(MathX.Pi, 0, 0), 5));
            case "back_lantern":
                return Merge(
                    Cyl(P.Metal, 0.012f, 0.012f, 0.35f, V(0.06f, 0.15f, -0.08f), V(0, 0, -0.2f), 4),
                    Box(P.Metal, 0.1f, 0.02f, 0.1f, V(0.1f, 0.32f, -0.08f)),
                    Box(P.WindowGlow, 0.08f, 0.1f, 0.08f, V(0.1f, 0.26f, -0.08f)),
                    Cone(P.Metal, 0.07f, 0.05f, V(0.1f, 0.35f, -0.08f), null, 4));
            default:
                return null;
        }
    }
}
