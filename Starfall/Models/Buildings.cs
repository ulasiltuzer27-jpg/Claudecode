using System.Numerics;
using Starfall.Core;
using Starfall.Physics;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>
/// Yapi kurucularinin ciktisi:
///  - Group: sahneye eklenecek dugumler (yerel uzayda, kapi +Z'ye bakar)
///  - Colliders: fizik tanimlari (yerel uzayda); World.Place bunlari dunyaya tasir
///  - Glow: gece yanan pencere/fener geometrileri; tum ada icin tek bir "gece isigi"
///    meshinde birlestirilir (tek draw call, tek parlaklik ayari)
///  - Anchors: esyalarin oturdugu yerel noktalar
/// </summary>
public sealed class BuildResult
{
    public readonly Node Group = new();
    public readonly List<ShapeDef> Colliders = new();
    public readonly List<Geo> Glow = new();
    public readonly Dictionary<string, Vector3> Anchors = new();
    public List<Shape> Placed = new();
    public Node? Lamp, Sails, Flag;
    public Material? LampMat;

    public Node Add(Geo g, Material? mat = null, bool flat = true)
    {
        var n = new Node(MeshData.From(g, flat), mat ?? Material.Std());
        Group.Add(n);
        return n;
    }

    public Node Add(IEnumerable<Geo> parts, Material? mat = null, bool flat = true) => Add(Merge(parts), mat, flat);
}

public static class Buildings
{
    /// <summary>Ucgen sarimini ters cevir (ic yuzey) ve tek renge boya.</summary>
    public static Geo FlipFaces(Geo g, string color)
    {
        var c = MathX.Hex(color);
        for (int i = 0; i + 2 < g.Count; i += 3)
        {
            (g.P[i + 1], g.P[i + 2]) = (g.P[i + 2], g.P[i + 1]);
            (g.N[i + 1], g.N[i + 2]) = (g.N[i + 2], g.N[i + 1]);
        }
        for (int i = 0; i < g.Count; i++)
        {
            g.N[i] = -g.N[i];
            g.C[i] = c;
        }
        return g;
    }

    private static Vector3 Tri(Geo g, int i) => (g.P[i] + g.P[i + 1] + g.P[i + 2]) / 3f;

    public static BuildResult Cottage(string? wall = null, string? roof = null, float seed = 1, float w = 4.2f, float d = 3.6f, float wallH = 2.5f, bool chimney = true)
    {
        wall ??= P.Wall;
        roof ??= P.RoofRed;
        var res = new BuildResult();
        const float bas = 0.3f;
        float top = bas + wallH;
        const float roofH = 1.7f;
        float ang = MathF.Atan2(roofH, d / 2 + 0.1f);
        float slabLen = (d / 2 + 0.5f) / MathF.Cos(ang);
        var parts = new List<Geo>
        {
            Box(P.Stone, w + 0.3f, 1.3f, d + 0.3f, V(0, bas - 0.65f, 0)),
            Box(wall, w, wallH, d, V(0, bas + wallH / 2, 0)),
            // ucgen duvarlar
            Prism(wall, d, roofH, w, V(0, top, 0), V(0, MathX.Pi / 2, 0)),
        };
        // ahsap iskelet
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                parts.Add(Box(P.Timber, 0.18f, wallH, 0.18f, V(sx * (w / 2 - 0.02f), bas + wallH / 2, sz * (d / 2 - 0.02f))));
        parts.Add(Box(P.Timber, w + 0.1f, 0.16f, 0.2f, V(0, top - 0.05f, d / 2)));
        parts.Add(Box(P.Timber, w + 0.1f, 0.16f, 0.2f, V(0, top - 0.05f, -d / 2)));
        // cati kiremit levhalari
        foreach (var s in new[] { -1f, 1f })
        {
            var slab = Part(BoxGeo(w + 0.8f, 0.16f, slabLen, 1, 1, 4), roof, V(0, top + roofH / 2 + 0.05f, s * (d / 4 + 0.1f)), V(s * ang, 0, 0));
            parts.Add(slab.FaceTint(0.18f, Rng.ToUint32(seed + (s > 0 ? 1 : 2))));
        }
        parts.Add(Box(P.Timber, w + 0.9f, 0.18f, 0.22f, V(0, top + roofH + 0.06f, 0)));
        // kapi
        parts.Add(Box(P.Door, 0.95f, 1.65f, 0.1f, V(0, bas + 0.82f, d / 2 + 0.04f)));
        parts.Add(Box(P.Timber, 1.15f, 0.12f, 0.14f, V(0, bas + 1.7f, d / 2 + 0.05f)));
        parts.Add(Sphere(P.Gold, 0.05f, V(0.3f, bas + 0.85f, d / 2 + 0.11f), null, 6, 4));
        parts.Add(Box(P.Stone, 1.2f, 0.18f, 0.5f, V(0, bas - 0.05f, d / 2 + 0.3f)));
        // pencereler
        var winPos = new (float x, float z, float ry)[]
        {
            (-w / 2 + 0.85f, d / 2, 0), (w / 2 - 0.85f, d / 2, 0),
            (-w / 2, 0, MathX.Pi / 2), (w / 2, 0, MathX.Pi / 2),
        };
        string[] flowerCols = { "#ff7aa2", "#ffd84a", "#ffffff", "#c58bff" };
        foreach (var (x, z, ry) in winPos)
        {
            bool side = ry != 0;
            float ox = side ? x + MathF.Sign(x) * 0.04f : x;
            float oz = side ? 0 : z + 0.04f;
            float y = bas + 1.45f;
            float fw = side ? 0.12f : 0.85f;
            float fd = side ? 0.85f : 0.12f;
            parts.Add(Box(P.Timber, fw, 0.85f, fd, V(ox, y, oz)));
            res.Glow.Add(Box(P.WindowGlow, side ? 0.14f : 0.62f, 0.62f, side ? 0.62f : 0.14f, V(side ? ox + MathF.Sign(x) * 0.01f : ox, y, side ? oz : oz + 0.01f)));
            if (!side)
            {
                parts.Add(Box(roof, 0.22f, 0.75f, 0.06f, V(ox - 0.55f, y, oz + 0.04f)));
                parts.Add(Box(roof, 0.22f, 0.75f, 0.06f, V(ox + 0.55f, y, oz + 0.04f)));
                parts.Add(Box(P.WoodDark, 0.8f, 0.18f, 0.22f, V(ox, y - 0.52f, oz + 0.12f)));
                var rng = Rng.Js(seed + x * 10);
                for (int i = 0; i < 4; i++)
                    parts.Add(Sphere(flowerCols[(int)Math.Floor(rng.NextD() * 4)], 0.08f, V(ox - 0.28f + i * 0.19f, y - 0.38f, oz + 0.12f), null, 5, 3));
            }
        }
        if (chimney)
        {
            parts.Add(Box(P.Stone, 0.55f, 1.6f, 0.55f, V(w * 0.28f, top + roofH * 0.55f, -d * 0.18f)));
            parts.Add(Box(P.RockDark, 0.68f, 0.14f, 0.68f, V(w * 0.28f, top + roofH * 0.55f + 0.85f, -d * 0.18f)));
            res.Anchors["smoke"] = V(w * 0.28f, top + roofH * 0.55f + 1.0f, -d * 0.18f);
        }
        res.Add(parts);

        res.Colliders.Add(ShapeDef.Box(w / 2 + 0.15f, (wallH + 1.3f) / 2, d / 2 + 0.15f, V(0, (wallH + bas - 1) / 2, 0)));
        float hw = w / 2 + 0.4f;
        float hd = d / 2 + 0.5f;
        float rY = top - MathF.Tan(ang) * 0.4f;
        res.Colliders.Add(ShapeDef.HullOf(new[]
        {
            V(-hw, rY, -hd), V(hw, rY, -hd), V(-hw, rY, hd), V(hw, rY, hd),
            V(-hw, top + roofH + 0.1f, 0), V(hw, top + roofH + 0.1f, 0),
        }));
        res.Anchors["roofTop"] = V(0, top + roofH + 0.15f, 0);
        res.Anchors["door"] = V(0, 0, d / 2 + 0.8f);
        return res;
    }

    public static BuildResult ShopStall(string awningA = "#ff6b6b", string awningB = "#ffffff")
    {
        var res = new BuildResult();
        var parts = new List<Geo>
        {
            Box(P.Wood, 2.8f, 1.0f, 1.0f, V(0, 0.5f, 0)),
            Box(P.WoodDark, 2.9f, 0.1f, 1.1f, V(0, 1.02f, 0)),
            Box(P.WoodDark, 2.8f, 0.12f, 0.05f, V(0, 0.3f, 0.51f)),
            Box(P.WoodDark, 2.8f, 0.12f, 0.05f, V(0, 0.7f, 0.51f)),
        };
        foreach (var sx in new[] { -1.35f, 1.35f })
            foreach (var sz in new[] { -0.45f, 0.45f })
                parts.Add(Cyl(P.WoodDark, 0.06f, 0.06f, 2.6f, V(sx, 1.3f, sz), null, 6));
        // cizgili tente
        const int stripes = 9;
        for (int i = 0; i < stripes; i++)
        {
            string c = i % 2 == 1 ? awningB : awningA;
            float x = -1.6f + (i + 0.5f) * (3.2f / stripes);
            parts.Add(Box(c, 3.2f / stripes, 0.06f, 1.6f, V(x, 2.55f, 0.15f), V(0.28f, 0, 0)));
            parts.Add(Cone(c, 0.18f, 0.25f, V(x, 2.27f, 0.95f), V(MathX.Pi, 0, 0), 3));
        }
        // tezgahtaki mallar
        string[] fruit = { "#ff5a4f", "#ffd84a", "#7fd060", "#ff9a3c" };
        for (int i = 0; i < 8; i++) parts.Add(Sphere(fruit[i % 4], 0.1f, V(-1.1f + i * 0.3f, 1.16f, 0.05f + (i % 2) * 0.15f), null, 6, 4));
        parts.Add(Box(P.Wood, 0.6f, 0.45f, 0.5f, V(1.8f, 0.23f, 0.4f), V(0, 0.3f, 0)));
        parts.Add(Box(P.WoodDark, 0.5f, 0.4f, 0.45f, V(-1.85f, 0.2f, 0.5f), V(0, -0.2f, 0)));
        // tabela: deniz kabugu
        parts.Add(Box(P.WoodDark, 1.2f, 0.5f, 0.08f, V(0, 3.05f, 0.2f)));
        parts.Add(Sphere(P.Shell, 0.18f, V(0, 3.05f, 0.27f), V(1, 0.9f, 0.3f), 8, 6));
        res.Add(parts);
        res.Colliders.Add(ShapeDef.Box(1.45f, 0.55f, 0.55f, V(0, 0.5f, 0)));
        // tente ve tabela (catiya cikilabilsin)
        res.Colliders.Add(ShapeDef.Box(1.6f, 0.15f, 0.85f, V(0, 2.5f, 0.15f), V(0.28f, 0, 0)));
        res.Colliders.Add(ShapeDef.Box(0.6f, 0.25f, 0.06f, V(0, 3.05f, 0.2f)));
        return res;
    }

    public static BuildResult Lighthouse()
    {
        var res = new BuildResult();
        const float H = 14;
        var tower = CylinderGeo(1.8f, 2.6f, H, 18, 10).Color(P.LhWhite).Translate(0, H / 2 + 0.9f, 0);
        var red = MathX.Hex(P.LhRed);
        var white = MathX.Hex(P.LhWhite);
        for (int i = 0; i + 2 < tower.Count; i += 3)
        {
            float y = Tri(tower, i).Y;
            int band = (int)MathF.Floor((y - 0.9f) / 2.8f) % 2;
            var c = band != 0 ? red : white;
            tower.C[i] = tower.C[i + 1] = tower.C[i + 2] = c;
        }
        var parts = new List<Geo>
        {
            Cyl(P.Stone, 3.5f, 3.8f, 1.9f, V(0, -0.05f, 0), null, 14),
            tower,
            Cyl(P.Metal, 2.75f, 2.75f, 0.25f, V(0, H + 1.0f, 0), null, 20),
            Cyl(P.Metal, 1.4f, 1.4f, 0.2f, V(0, H + 2.95f, 0), null, 16),
            Cone(P.LhRed, 1.75f, 1.5f, V(0, H + 3.8f, 0), null, 16),
            Sphere(P.Gold, 0.22f, V(0, H + 4.65f, 0), null, 8, 6),
            Box(P.Door, 1.1f, 1.9f, 0.3f, V(0, 1.85f, 2.42f), V(-0.06f, 0, 0)),
            Box(P.Timber, 1.35f, 0.15f, 0.35f, V(0, 2.85f, 2.38f)),
        };
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * MathX.TwoPi;
            parts.Add(Cyl(P.Metal, 0.03f, 0.03f, 0.8f, V(MathF.Cos(a) * 2.65f, H + 1.5f, MathF.Sin(a) * 2.65f), null, 4));
        }
        parts.Add(Part(TorusGeo(2.65f, 0.05f, 4, 32), P.Metal, V(0, H + 1.9f, 0), V(MathX.Pi / 2, 0, 0)));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * MathX.TwoPi;
            parts.Add(Box(P.Metal, 0.1f, 1.8f, 0.1f, V(MathF.Cos(a) * 1.3f, H + 2.0f, MathF.Sin(a) * 1.3f)));
        }
        foreach (var y in new[] { 5f, 9.5f }) parts.Add(Box("#3b4b5c", 0.6f, 0.8f, 0.2f, V(0, y, 2.2f - (y - 1) * 0.055f)));
        res.Add(parts);

        // lamba odasi: ayri malzeme, final aninda parlakligi canlandiriliyor
        res.LampMat = new Material
        {
            Tint = MathX.Hex("#fff6d8"), Emissive = MathX.Hex("#ffd27a"), EmissiveIntensity = 0,
            Blend = Blend.Alpha, Opacity = 0.85f, CastShadow = false,
        };
        res.Lamp = new Node(MeshData.From(CylinderGeo(1.2f, 1.2f, 1.75f, 16), false), res.LampMat) { Position = V(0, H + 2.0f, 0) };
        res.Group.Add(res.Lamp);
        res.Anchors["lamp"] = V(0, H + 2.0f, 0);
        res.Anchors["door"] = V(0, 0.9f, 3.4f);
        res.Colliders.Add(ShapeDef.Cyl(3.7f, 0.95f, V(0, -0.05f, 0)));
        res.Colliders.Add(ShapeDef.Cyl(2.35f, H / 2, V(0, H / 2 + 0.9f, 0)));
        return res;
    }

    public static BuildResult Windmill()
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Cyl(P.Stone, 2.6f, 2.9f, 1.2f, V(0, -0.2f, 0), null, 8),
            Cyl("#f6efe2", 1.5f, 2.3f, 7.5f, V(0, 4.0f, 0), null, 8),
            Cone(P.RoofBlue, 2.0f, 1.9f, V(0, 8.7f, 0), null, 8),
            Box(P.Door, 0.9f, 1.6f, 0.2f, V(0, 1.2f, 2.2f), V(-0.1f, 0, 0)),
            Box("#3b4b5c", 0.5f, 0.6f, 0.15f, V(0, 4.5f, 1.92f), V(-0.1f, 0, 0)),
            Box("#3b4b5c", 0.5f, 0.6f, 0.15f, V(1.65f, 3.2f, 0), V(0, 0, 0.1f)),
            Cyl(P.WoodDark, 0.22f, 0.22f, 0.9f, V(0, 7.6f, 1.85f), V(MathX.Pi / 2, 0, 0), 8),
        });
        var sp = new List<Geo> { Sphere(P.WoodDark, 0.3f, null, null, 8, 6) };
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * MathX.TwoPi;
            var arm = new List<Geo> { Box(P.WoodDark, 0.15f, 4.4f, 0.1f, V(0, 2.3f, 0)) };
            for (int k = 0; k < 5; k++) arm.Add(Box(P.Sail, 0.9f, 0.7f, 0.04f, V(0.5f, 1.0f + k * 0.75f, 0)));
            sp.Add(Merge(arm).RotateZ(a));
        }
        res.Sails = new Node { Position = V(0, 7.6f, 2.35f) };
        res.Sails.Add(new Node(MeshData.From(Merge(sp), true), Material.Std()));
        res.Group.Add(res.Sails);
        res.Colliders.Add(ShapeDef.Cyl(2.2f, 4.2f, V(0, 3.8f, 0)));
        return res;
    }

    public static BuildResult Dock(float length = 26, float width = 2.6f)
    {
        var res = new BuildResult();
        var parts = new List<Geo>();
        int planks = (int)MathF.Floor(length / 0.5f);
        for (int i = 0; i < planks; i++)
            parts.Add(Box(i % 3 == 0 ? P.WoodDark : P.Wood, width, 0.12f, 0.44f, V(0, 0, i * 0.5f + 0.25f)));
        for (float i = 0; i <= length; i += 3)
        {
            foreach (var s in new[] { -1f, 1f }) parts.Add(Cyl(P.WoodDark, 0.12f, 0.12f, 4, V(s * (width / 2 - 0.1f), -1.9f, i), null, 6));
            foreach (var s in new[] { -1f, 1f }) parts.Add(Cyl(P.WoodDark, 0.09f, 0.09f, 0.8f, V(s * (width / 2 - 0.1f), 0.4f, i), null, 6));
        }
        res.Add(parts);
        res.Colliders.Add(ShapeDef.Box(width / 2, 0.1f, length / 2, V(0, -0.02f, length / 2)));
        res.Anchors["end"] = V(0, 0.1f, length - 0.5f);
        return res;
    }

    /// <summary>A ve B arasinda halat kopru (yerel = dunya, donusum yok).</summary>
    public static BuildResult Bridge(Vector3 a, Vector3 b, float width = 2.2f)
    {
        var res = new BuildResult();
        var dir = b - a;
        float len = dir.Length();
        float yaw = MathF.Atan2(dir.X, dir.Z);
        float pitch = -MathF.Asin(dir.Y / len);
        int n = (int)MathF.Floor(len / 0.55f);
        var parts = new List<Geo>();
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            float sag = MathF.Sin(t * MathX.Pi) * 0.35f;
            var p = Vector3.Lerp(a, b, t);
            p.Y -= sag;
            parts.Add(Box(i % 2 == 1 ? P.Wood : "#c08a5c", width, 0.1f, 0.46f, null, V(pitch, 0, 0)).RotateY(yaw).Translate(p.X, p.Y, p.Z));
        }
        foreach (var s in new[] { -1f, 1f })
        {
            var side = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw)) * (s * (width / 2));
            var pts = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                var p = Vector3.Lerp(a, b, t) + side;
                p.Y += 0.85f - MathF.Sin(t * MathX.Pi) * 0.45f;
                pts.Add(p);
            }
            parts.Add(Tube(pts, 24, 0.04f, 4).Color("#d8c49a"));
            foreach (var t in new[] { 0f, 1f })
            {
                var p = Vector3.Lerp(a, b, t) + side;
                parts.Add(Cyl(P.WoodDark, 0.1f, 0.12f, 1.6f, V(p.X, p.Y + 0.2f, p.Z), null, 6));
            }
        }
        res.Add(parts);
        // fizik: sarkmayi takip eden 4 egimli kutu
        const int segs = 4;
        for (int i = 0; i < segs; i++)
        {
            float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
            var p0 = Vector3.Lerp(a, b, t0); p0.Y -= MathF.Sin(t0 * MathX.Pi) * 0.35f;
            var p1 = Vector3.Lerp(a, b, t1); p1.Y -= MathF.Sin(t1 * MathX.Pi) * 0.35f;
            var mid = (p0 + p1) * 0.5f;
            var d = p1 - p0;
            float l = d.Length();
            res.Colliders.Add(ShapeDef.Box(width / 2, 0.08f, l / 2 + 0.05f, V(mid.X, mid.Y - 0.03f, mid.Z), V(-MathF.Asin(d.Y / l), yaw, 0)));
        }
        return res;
    }

    public static BuildResult Shipwreck()
    {
        var res = new BuildResult();
        var hull = SphereGeo(1, 14, 8, 0, MathX.TwoPi, MathX.Pi / 2, MathX.Pi / 2).Color(P.WoodDark);
        hull.Scale(2.3f, 1.7f, 6.2f);
        hull.Jitter(0.12f, 9);
        for (int i = 0; i + 2 < hull.Count; i += 3)
        {
            float y = Tri(hull, i).Y;
            int band = (int)MathF.Floor((y + 2) * 2.2f) % 2;
            var c = MathX.Hex(band != 0 ? P.WoodDark : "#6e4a32");
            hull.C[i] = hull.C[i + 1] = hull.C[i + 2] = c;
        }
        var parts = new List<Geo>
        {
            hull,
            Box(P.Wood, 4.2f, 0.15f, 11, V(0, -0.05f, 0)),
            Box("#222222", 0.8f, 0.6f, 0.1f, V(2.1f, -0.7f, 1.5f), V(0, MathX.Pi / 2, 0.2f)),
            Box("#222222", 0.6f, 0.4f, 0.1f, V(-2.0f, -0.9f, -2.0f), V(0, MathX.Pi / 2, -0.1f)),
            Box(P.WoodDark, 4.4f, 0.9f, 0.2f, V(0, 0.35f, -5.2f)),
            Box(P.Wood, 1.1f, 0.8f, 0.9f, V(-1.0f, 0.45f, 2.5f), V(0, 0.4f, 0)),
            Box(P.Wood, 0.9f, 0.7f, 0.8f, V(1.1f, 0.4f, 3.4f), V(0, -0.3f, 0)),
            Cyl("#8c6a4a", 0.4f, 0.4f, 0.9f, V(0.6f, 0.45f, -2.2f), null, 8),
            Cyl(P.WoodDark, 0.18f, 0.22f, 8, V(0, 4.0f, 0.5f), null, 8),
            Box(P.WoodDark, 4.2f, 0.16f, 0.16f, V(0, 5.6f, 0.5f)),
            Cyl(P.WoodDark, 0.85f, 0.75f, 0.6f, V(0, 7.4f, 0.5f), null, 10),
        };
        var sail = new Shape2D().MoveTo(-1.9f, 0).LineTo(1.9f, 0).LineTo(1.7f, -1.4f).LineTo(1.1f, -1.9f).LineTo(0.6f, -1.5f)
            .LineTo(0.0f, -2.4f).LineTo(-0.7f, -1.7f).LineTo(-1.3f, -2.1f).LineTo(-1.8f, -1.2f).Points();
        parts.Add(Part(Extrude(sail, 0.03f), P.Sail, V(0, 5.5f, 0.62f)));
        var g = Merge(parts).RotateZ(0.22f).RotateX(-0.06f);
        res.Add(g);
        // carpisma: govde + gozcu yuvasi; geometriyle ayni egimde
        var tiltM = MathX.EulerXYZ(-0.06f, 0, 0.22f);
        Vector3 Tilt(float x, float y, float z) => Vector3.Transform(V(x, y, z), tiltM);
        var tiltR = V(-0.06f, 0, 0.22f);
        var deck = Tilt(0, -0.05f, 0);
        res.Colliders.Add(ShapeDef.Box(2.1f, 0.6f, 5.6f, V(deck.X, deck.Y - 0.5f, deck.Z), tiltR));
        var mastP = Tilt(0, 4.0f, 0.5f);
        res.Colliders.Add(ShapeDef.Cyl(0.22f, 4, mastP, tiltR));
        var nest = Tilt(0, 7.15f, 0.5f);
        res.Colliders.Add(ShapeDef.Cyl(0.85f, 0.3f, nest, tiltR));
        res.Anchors["nest"] = Tilt(0, 7.9f, 0.5f);
        return res;
    }

    public static BuildResult Campfire()
    {
        var res = new BuildResult();
        var parts = new List<Geo>();
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * MathX.TwoPi;
            parts.Add(Nature.Rock((uint)(i + 40), false).Scale(0.22f, 0.22f, 0.22f).Translate(MathF.Cos(a) * 0.75f, 0, MathF.Sin(a) * 0.75f));
        }
        for (int i = 0; i < 4; i++)
            parts.Add(Cyl(P.Bark, 0.08f, 0.08f, 0.9f, V(0, 0.18f, 0), V(0.5f, i / 4f * MathX.TwoPi, MathX.Pi / 2 - 0.4f), 6));
        // kutuk oturaklar
        foreach (var (x, z, ry) in new[] { (2.2f, 0f, 0f), (-2.2f, 0.2f, 0.2f), (0.2f, -2.3f, MathX.Pi / 2) })
            parts.Add(Cyl(P.Bark, 0.25f, 0.25f, 1.6f, V(x, 0.25f, z), V(0, ry, MathX.Pi / 2), 8));
        res.Add(parts);
        res.Anchors["fire"] = V(0, 0.25f, 0);
        return res;
    }

    public static BuildResult Lantern()
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Cyl(P.Metal, 0.06f, 0.08f, 2.4f, V(0, 1.2f, 0), null, 6),
            Box(P.Metal, 0.5f, 0.06f, 0.06f, V(0.2f, 2.35f, 0)),
            Box(P.Metal, 0.34f, 0.06f, 0.34f, V(0.4f, 2.2f, 0)),
            Cone(P.Metal, 0.26f, 0.2f, V(0.4f, 2.0f + 0.32f, 0), null, 4),
        });
        res.Glow.Add(Box(P.WindowGlow, 0.26f, 0.34f, 0.26f, V(0.4f, 2.0f, 0)));
        res.Colliders.Add(ShapeDef.Cyl(0.1f, 1.2f, V(0, 1.2f, 0)));
        return res;
    }

    public static BuildResult Signpost(params string[] colors)
    {
        if (colors.Length == 0) colors = new[] { "#f2c94c", "#7fd0ff" };
        var res = new BuildResult();
        var parts = new List<Geo> { Cyl(P.WoodDark, 0.08f, 0.1f, 2.2f, V(0, 1.1f, 0), null, 6) };
        for (int i = 0; i < colors.Length; i++)
        {
            float dir = i % 2 == 1 ? -1 : 1;
            parts.Add(Box(colors[i], 1.1f, 0.3f, 0.06f, V(dir * 0.45f, 1.8f - i * 0.42f, 0.06f), V(0, 0, dir * 0.05f)));
            parts.Add(Cone(colors[i], 0.15f, 0.22f, V(dir * 1.1f, 1.8f - i * 0.42f, 0.06f), V(0, 0, -dir * MathX.Pi / 2), 3, V(1, 1, 0.3f)));
        }
        res.Add(parts);
        res.Colliders.Add(ShapeDef.Cyl(0.12f, 1.1f, V(0, 1.1f, 0)));
        return res;
    }

    public static Geo RowboatGeo(string trim = "#3f7fc4", bool withSail = false)
    {
        var hull = SphereGeo(1, 12, 6, 0, MathX.TwoPi, MathX.Pi / 2, MathX.Pi / 2).Color("#e8f1f7");
        hull.Scale(0.9f, 0.5f, 1.9f);
        var trimC = MathX.Hex(trim);
        var white = MathX.Hex("#e8f1f7");
        for (int i = 0; i + 2 < hull.Count; i += 3)
        {
            var c = Tri(hull, i).Y > -0.12f ? trimC : white;
            hull.C[i] = hull.C[i + 1] = hull.C[i + 2] = c;
        }
        // Kase yukaridan bakilinca ic yuzunden gorunur: ters sarimli bir ic kabuk ekle.
        var inner = hull.Clone().Scale(0.94f, 0.94f, 0.96f);
        FlipFaces(inner, P.Wood);
        var parts = new List<Geo>
        {
            hull,
            inner,
            Box(P.Wood, 1.5f, 0.06f, 0.3f, V(0, -0.1f, 0.3f)),
            Box(P.Wood, 1.3f, 0.06f, 0.3f, V(0, -0.1f, -0.9f)),
        };
        if (!withSail)
        {
            parts.Add(Cyl(P.Wood, 0.03f, 0.03f, 2.0f, V(0.75f, 0.05f, 0), V(0.2f, 0, MathX.Pi / 2 - 0.3f), 5));
            parts.Add(Box(P.Wood, 0.14f, 0.02f, 0.4f, V(1.55f, -0.2f, 0.2f)));
        }
        else
        {
            // yelkenli: direk + uckek yelken + dumen
            parts.Add(Cyl(P.WoodDark, 0.045f, 0.055f, 2.6f, V(0, 1.15f, 0.35f), null, 6));
            parts.Add(Cyl(P.WoodDark, 0.03f, 0.03f, 1.5f, V(0, 0.25f, -0.35f), V(MathX.Pi / 2, 0, 0), 5));
            var sail = new List<Vector2> { new(0, 0), new(0, 2.1f), new(-1.25f, 0.1f) };
            parts.Add(Part(Extrude(sail, 0.02f), P.Sail, V(0.02f, 0.3f, 0.35f), V(0, -MathX.Pi / 2, 0)));
            parts.Add(Box("#ff7a5a", 0.025f, 0.22f, 0.36f, V(0.035f, 2.2f, 0.33f)));
            parts.Add(Box(P.WoodDark, 0.04f, 0.42f, 0.32f, V(0, -0.22f, -1.9f)));
        }
        return Merge(parts);
    }

    public static BuildResult Rowboat()
    {
        var res = new BuildResult();
        res.Add(RowboatGeo());
        res.Colliders.Add(ShapeDef.Box(0.8f, 0.25f, 1.7f, V(0, -0.3f, 0)));
        return res;
    }

    public static BuildResult Crate()
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Box(P.Wood, 0.8f, 0.8f, 0.8f, V(0, 0.4f, 0)),
            Box(P.WoodDark, 0.84f, 0.1f, 0.84f, V(0, 0.75f, 0)),
            Box(P.WoodDark, 0.84f, 0.1f, 0.84f, V(0, 0.05f, 0)),
            Box(P.WoodDark, 0.1f, 0.8f, 0.84f, V(0, 0.4f, 0), V(0.78f, 0, 0)),
        });
        res.Colliders.Add(ShapeDef.Box(0.42f, 0.4f, 0.42f, V(0, 0.4f, 0)));
        return res;
    }

    public static BuildResult Barrel()
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Cyl("#a8744a", 0.35f, 0.35f, 0.9f, V(0, 0.45f, 0), null, 10),
            Cyl(P.Metal, 0.37f, 0.37f, 0.06f, V(0, 0.2f, 0), null, 10),
            Cyl(P.Metal, 0.37f, 0.37f, 0.06f, V(0, 0.7f, 0), null, 10),
        });
        res.Colliders.Add(ShapeDef.Cyl(0.37f, 0.45f, V(0, 0.45f, 0)));
        return res;
    }

    public static BuildResult Bench()
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Box(P.Wood, 1.6f, 0.08f, 0.45f, V(0, 0.45f, 0)),
            Box(P.Wood, 1.6f, 0.35f, 0.06f, V(0, 0.75f, -0.2f), V(-0.15f, 0, 0)),
            Box(P.WoodDark, 0.08f, 0.45f, 0.4f, V(-0.7f, 0.22f, 0)),
            Box(P.WoodDark, 0.08f, 0.45f, 0.4f, V(0.7f, 0.22f, 0)),
        });
        res.Colliders.Add(ShapeDef.Box(0.8f, 0.25f, 0.25f, V(0, 0.25f, 0)));
        return res;
    }

    public static BuildResult Fence(IReadOnlyList<Vector2> points, Func<float, float, float> height, string color = "#f3e6cf")
    {
        var res = new BuildResult();
        var parts = new List<Geo>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            float ax = points[i].X, az = points[i].Y, bx = points[i + 1].X, bz = points[i + 1].Y;
            float len = MathX.Hypot(bx - ax, bz - az);
            int n = Math.Max(1, (int)MathF.Round(len / 1.6f));
            for (int k = 0; k <= n; k++)
            {
                if (k == n && i < points.Count - 2) continue;
                float x = ax + (bx - ax) * ((float)k / n);
                float z = az + (bz - az) * ((float)k / n);
                float y = height(x, z);
                parts.Add(Box(color, 0.14f, 1.0f, 0.14f, V(x, y + 0.4f, z)));
                parts.Add(Cone(color, 0.1f, 0.16f, V(x, y + 0.98f, z), null, 4));
            }
            float yaw = MathF.Atan2(bx - ax, bz - az);
            float ym = height((ax + bx) / 2, (az + bz) / 2);
            foreach (var h in new[] { 0.35f, 0.7f })
                parts.Add(Box(color, 0.06f, 0.1f, len, null, V(0, yaw, 0)).Translate((ax + bx) / 2, ym + h, (az + bz) / 2));
            res.Colliders.Add(ShapeDef.Box(0.08f, 0.5f, len / 2, V((ax + bx) / 2, ym + 0.5f, (az + bz) / 2), V(0, yaw, 0)));
        }
        res.Add(parts);
        return res;
    }

    public static BuildResult FlagPole(string color = "#ff5a5a")
    {
        var res = new BuildResult();
        res.Add(new[]
        {
            Cyl("#e8e8e8", 0.05f, 0.06f, 3.2f, V(0, 1.6f, 0), null, 6),
            Sphere(P.Gold, 0.09f, V(0, 3.25f, 0), null, 6, 4),
        });
        res.Flag = new Node { Position = V(0, 2.8f, 0) };
        var shape = new List<Vector2> { new(0, 0), new(1.1f, -0.3f), new(0, -0.65f) };
        res.Flag.Add(new Node(MeshData.From(Part(Extrude(shape, 0.02f), color), true), new Material { DoubleSided = true }));
        res.Group.Add(res.Flag);
        res.Colliders.Add(ShapeDef.Cyl(0.08f, 1.6f, V(0, 1.6f, 0)));
        return res;
    }

    /// <summary>Gizli magara: halka seklinde dev kayalar + cati, bir yanda giris (+X).</summary>
    public static BuildResult RockDome(string crystalGlow = "#5fdfff", string? c1 = null, string? c2 = null, bool snowy = false)
    {
        var res = new BuildResult();
        var parts = new List<Geo>();
        const int n = 9;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathX.TwoPi;
            if (i == 0) continue; // giris (+X yonu)
            var r = snowy ? Nature.SnowRock((uint)(70 + i)) : Nature.Rock((uint)(70 + i), true);
            // girisin iki yanindaki kayalar kucuk: tilki rahat gecsin
            float s = i == 1 || i == n - 1 ? 1.7f : 2.4f + (i % 3) * 0.3f;
            r.Scale(s, s * 1.5f, s).Translate(MathF.Cos(a) * 5.2f, -0.8f, MathF.Sin(a) * 5.2f);
            parts.Add(r);
            res.Colliders.Add(ShapeDef.HullOf(r.P));
        }
        var roof = snowy ? Nature.SnowRock(99) : Nature.Rock(99, true);
        roof.Scale(7.2f, 2.2f, 7.2f).Translate(0, 5.6f, 0);
        parts.Add(roof);
        res.Colliders.Add(ShapeDef.HullOf(roof.P));
        res.Add(parts);
        var crystals = new List<Geo>();
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * MathX.TwoPi + 0.6f;
            crystals.Add(Nature.Crystal((uint)(i + 5), c1, c2).Translate(MathF.Cos(a) * 3.0f, 0, MathF.Sin(a) * 3.0f));
        }
        res.Add(Merge(crystals), Material.Glow(crystalGlow, 0.9f));
        res.Anchors["inside"] = V(0, 0.6f, 0);
        return res;
    }

    /// <summary>Kumdan kale (kumsal).</summary>
    public static BuildResult Sandcastle()
    {
        var res = new BuildResult();
        var corners = new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) };
        var parts = new List<Geo> { Box(P.Sand, 2.4f, 0.9f, 2.4f, V(0, 0.45f, 0)) };
        foreach (var (a, b) in corners) parts.Add(Part(CylinderGeo(0.4f, 0.45f, 1.5f, 8), P.Sand, V(a * 1.1f, 0.75f, b * 1.1f)));
        foreach (var (a, b) in corners) parts.Add(Cone("#e8c98a", 0.45f, 0.5f, V(a * 1.1f, 1.75f, b * 1.1f), null, 8));
        parts.Add(Part(CylinderGeo(0.55f, 0.65f, 1.4f, 8), P.Sand, V(0, 1.6f, 0)));
        parts.Add(Part(CylinderGeo(0.02f, 0.02f, 0.8f, 4), "#ffffff", V(0, 2.6f, 0)));
        parts.Add(Box("#ff5a5a", 0.4f, 0.25f, 0.02f, V(0.2f, 2.85f, 0)));
        res.Add(parts);
        res.Colliders.Add(ShapeDef.Box(1.4f, 0.45f, 1.4f, V(0, 0.45f, 0)));
        res.Colliders.Add(ShapeDef.Cyl(0.65f, 0.7f, V(0, 1.6f, 0)));
        foreach (var (a, b) in corners) res.Colliders.Add(ShapeDef.Cyl(0.45f, 0.75f, V(a * 1.1f, 0.75f, b * 1.1f)));
        res.Anchors["top"] = V(0, 2.6f, 0);
        return res;
    }
}
