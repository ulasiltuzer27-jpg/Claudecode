using System.Numerics;
using Starfall.Core;
using Starfall.Physics;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>Kar Adasi ve yeni mekaniklerin yapilari.</summary>
public static class Buildings2
{
    private static Vector3 Tri(Geo g, int i) => (g.P[i] + g.P[i + 1] + g.P[i + 2]) / 3f;

    /// <summary>Ust yuzleri karla ortulu yapar (yukari bakan ucgenler beyaz).</summary>
    public static Geo SnowCap(Geo g, float minNy = 0.55f, string snow = "#f4f8ff")
    {
        var c = MathX.Hex(snow);
        for (int i = 0; i + 2 < g.Count; i += 3)
        {
            var n = Vector3.Normalize(Vector3.Cross(g.P[i + 1] - g.P[i], g.P[i + 2] - g.P[i]));
            if (n.Y > minNy) g.C[i] = g.C[i + 1] = g.C[i + 2] = c;
        }
        return g;
    }

    /// <summary>
    /// Sarmasikli tirmanma duvari: kaya levha + sarmasik halatlari + yapraklar. Yuz +Z'ye bakar,
    /// taban y=0. Carpisma etiketi "climb". depth: yuzden geriye kalinlik (derin olursa tepesi
    /// dinlenme cikintisi olur).
    /// </summary>
    public static BuildResult VineWall(float w, float h, uint seed, bool frosty = false, float depth = 1.2f)
    {
        var res = new BuildResult();
        var rng = new Rng(seed);
        var parts = new List<Geo>();
        var slab = BoxGeo(w, h, depth, 3, (int)MathF.Max(2, h / 2.5f), Math.Max(1, (int)(depth / 1.2f))).Color(frosty ? "#a3aebd" : P.Rock);
        slab.Jitter(0.18f, seed);
        slab.Translate(0, h / 2, -depth / 2);
        if (frosty) SnowCap(slab, 0.6f);
        parts.Add(slab.FaceTint(0.12f, seed));
        string vine = frosty ? "#4c8c62" : "#3e9a48";
        string leaf = frosty ? "#5fa87a" : "#62b856";
        int strands = Math.Max(2, (int)(w / 0.9f));
        for (int i = 0; i < strands; i++)
        {
            float x0 = -w / 2 + (i + 0.5f) * w / strands + ((float)rng.NextD() - 0.5f) * 0.3f;
            var pts = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = k / 8f;
                pts.Add(new Vector3(x0 + MathF.Sin(t * 7 + i) * 0.18f, t * h, 0.06f));
            }
            parts.Add(Tube(pts, 20, 0.045f, 4).Color(vine));
            for (int k = 0; k < (int)(h * 1.6f); k++)
            {
                float y = (float)rng.NextD() * h;
                float x = x0 + MathF.Sin(y / h * 7 + i) * 0.18f + ((float)rng.NextD() - 0.5f) * 0.25f;
                parts.Add(Part(Icosahedron(0.12f, 0), (float)rng.NextD() < 0.5f ? leaf : vine, V(x, y, 0.1f), null, V(1.2f, 0.8f, 0.5f)));
            }
        }
        res.Add(parts);
        res.Colliders.Add(new ShapeDef(ShapeKind.Box, V(0, h / 2, -depth / 2), V(w / 2, h / 2, depth / 2)));
        res.Anchors["top"] = V(0, h + 0.2f, -depth / 2);
        res.Anchors["base"] = V(0, 0, 0.8f);
        return res;
    }

    /// <summary>Karli kulube: duvar ahsap, cati karla kapli, sacaklarda buz sarkitlari.</summary>
    public static BuildResult SnowCabin(string wall, float seed)
    {
        var res = Buildings.Cottage(wall, "#eef3fa", seed);
        // sacak buz sarkitlari
        var ice = new List<Geo>();
        var rng = Rng.Js(seed);
        for (int s = -1; s <= 1; s += 2)
        for (int i = 0; i < 9; i++)
        {
            float x = -2.4f + i * 0.6f + ((float)rng.NextD() - 0.5f) * 0.2f;
            float len = 0.2f + (float)rng.NextD() * 0.35f;
            ice.Add(Cone("#cfeeff", 0.05f, len, V(x, 2.8f - len / 2 + 0.02f, s * 2.32f), V(MathX.Pi, 0, 0), 4));
        }
        res.Add(ice, new Material { Emissive = MathX.Hex("#9fdcff"), EmissiveIntensity = 0.15f });
        return res;
    }

    public static BuildResult Igloo(float seed)
    {
        var res = new BuildResult();
        var dome = SphereGeo(2.2f, 14, 8, 0, MathX.TwoPi, 0, MathX.Pi / 2).Color("#f2f7fd");
        // kar tuglasi cizgileri
        var line = MathX.Hex("#d5e3f1");
        for (int i = 0; i + 2 < dome.Count; i += 3)
        {
            var c = Tri(dome, i);
            if ((int)MathF.Floor(c.Y * 2.2f) % 2 == 0 && MathF.Abs(MathF.Sin(MathF.Atan2(c.Z, c.X) * 9)) < 0.18f) dome.C[i] = dome.C[i + 1] = dome.C[i + 2] = line;
        }
        res.Add(new[]
        {
            dome,
            Part(CylinderGeo(0.8f, 0.8f, 1.6f, 10, 1, false, 0, MathX.Pi), "#e8f0f9", V(0, 0, 2.0f), V(MathX.Pi / 2, 0, 0)),
            Part(CircleGeo(0.62f, 12, 0, MathX.Pi), "#2b3346", V(0, 0.01f, 2.81f)),
        });
        res.Colliders.Add(ShapeDef.Ball(2.2f, V(0, 0, 0)));
        return res;
    }

    /// <summary>Isildayan buz sivrisi (gece hafif parlar).</summary>
    public static Geo IceSpire(uint seed, float h)
    {
        var rng = new Rng(seed);
        var parts = new List<Geo>();
        for (int i = 0; i < 4; i++)
        {
            float hh = h * (0.5f + (float)rng.NextD() * 0.5f);
            parts.Add(Part(CylinderGeo(0, 0.5f + (float)rng.NextD() * 0.4f, hh, 5), i % 2 == 0 ? "#bfe9ff" : "#9fd8f5",
                V(((float)rng.NextD() - 0.5f) * 1.2f, hh / 2, ((float)rng.NextD() - 0.5f) * 1.2f),
                V(((float)rng.NextD() - 0.5f) * 0.3f, (float)rng.NextD() * 3, ((float)rng.NextD() - 0.5f) * 0.3f)));
        }
        return Merge(parts).FaceTint(0.1f, seed);
    }

    /// <summary>Rasathane: tas silindir govde, donebilen kubbe + yarik, teleskop, merdiven.</summary>
    public static BuildResult Observatory()
    {
        var res = new BuildResult();
        var body = CylinderGeo(4.2f, 4.6f, 6, 18, 3).Color("#c9c3b8").Translate(0, 3, 0);
        body.Jitter(0.05f, 3);
        var stones = MathX.Hex("#b5aea2");
        for (int i = 0; i + 2 < body.Count; i += 3)
            if ((int)MathF.Floor(Tri(body, i).Y * 1.4f + MathF.Atan2(Tri(body, i).Z, Tri(body, i).X) * 3) % 3 == 0)
                body.C[i] = body.C[i + 1] = body.C[i + 2] = stones;
        res.Add(new[]
        {
            Cyl("#a8a196", 5.2f, 5.6f, 1.0f, V(0, -0.3f, 0), null, 18),
            body,
            Cyl("#8f877c", 4.5f, 4.5f, 0.35f, V(0, 6.1f, 0), null, 20),
            Box(P.Door, 1.3f, 2.3f, 0.3f, V(0, 1.4f, 4.4f), V(-0.05f, 0, 0)),
            Box(P.Timber, 1.6f, 0.18f, 0.4f, V(0, 2.6f, 4.38f)),
            Box("#3b4b5c", 0.7f, 0.9f, 0.2f, V(3.0f, 3.6f, 3.1f), V(0, 0.78f, 0)),
            Box("#3b4b5c", 0.7f, 0.9f, 0.2f, V(-3.0f, 3.6f, 3.1f), V(0, -0.78f, 0)),
        });
        res.Glow.Add(Box(P.WindowGlow, 0.5f, 0.7f, 0.22f, V(3.02f, 3.6f, 3.12f), V(0, 0.78f, 0)));
        res.Glow.Add(Box(P.WindowGlow, 0.5f, 0.7f, 0.22f, V(-3.02f, 3.6f, 3.12f), V(0, -0.78f, 0)));
        // kubbe ayri dugum (final aninda doner/acilir)
        var dome = Part(SphereGeo(4.3f, 20, 10, 0, MathX.TwoPi, 0, MathX.Pi / 2), "#dfe7ef", V(0, 0, 0));
        // yarik: koyu serit
        var slit = MathX.Hex("#2b3346");
        for (int i = 0; i + 2 < dome.Count; i += 3)
        {
            var c = Tri(dome, i);
            if (MathF.Abs(c.X) < 0.75f && c.Z > 0) dome.C[i] = dome.C[i + 1] = dome.C[i + 2] = slit;
        }
        dome = SnowCap(dome, 0.85f);
        var domeNode = new Node(MeshData.From(Merge(dome, Sphere(P.Gold, 0.25f, V(0, 4.35f, 0), null, 8, 6)), true), Material.Std()) { Position = V(0, 6.25f, 0), Name = "dome" };
        res.Group.Add(domeNode);
        res.Sails = domeNode; // donen parca
        // teleskop (kubbe yarigindan disari bakar)
        var scope = new Node { Position = V(0, 7.6f, 0.6f), Rotation = V(-0.7f, 0, 0), Name = "telescope" };
        scope.Add(new Node(MeshData.From(Merge(
            Cyl("#5a6475", 0.35f, 0.45f, 4.2f, V(0, 1.8f, 0), null, 12),
            Cyl(P.Gold, 0.47f, 0.47f, 0.12f, V(0, 0.4f, 0), null, 12),
            Cyl(P.Gold, 0.38f, 0.38f, 0.12f, V(0, 3.6f, 0), null, 12),
            Cyl("#8fd3ff", 0.32f, 0.32f, 0.02f, V(0, 3.92f, 0), null, 12)), true), Material.Std()));
        res.Group.Add(scope);
        res.Flag = scope;
        // basamaklar
        for (int i = 0; i < 3; i++) res.Add(Box("#b5aea2", 2.2f, 0.25f, 0.6f, V(0, 0.12f + i * 0.25f - 0.6f, 5.6f - i * 0.5f)));
        res.Colliders.Add(ShapeDef.Cyl(5.6f, 0.5f, V(0, -0.3f, 0)));
        res.Colliders.Add(ShapeDef.Cyl(4.6f, 3.2f, V(0, 3.0f, 0)));
        res.Colliders.Add(ShapeDef.Ball(4.4f, V(0, 6.25f, 0)));
        res.Anchors["door"] = V(0, 0.2f, 6.4f);
        res.Anchors["scope"] = V(0, 10.5f, 2.6f);
        res.Anchors["top"] = V(0, 10.9f, 0);
        return res;
    }

    /// <summary>Sicak su kaynagi kenari: kaya halkasi + tahta oturak.</summary>
    public static List<Geo> SpringRocks(Vector2 c, float r, Func<float, float, float> height, uint seed)
    {
        var parts = new List<Geo>();
        var rng = new Rng(seed);
        int n = (int)(r * 2.2f);
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathX.TwoPi + ((float)rng.NextD() - 0.5f) * 0.2f;
            float x = c.X + MathF.Cos(a) * (r + 0.4f), z = c.Y + MathF.Sin(a) * (r + 0.4f);
            float s = 0.35f + (float)rng.NextD() * 0.35f;
            var rk = Nature.Rock((uint)(seed + i), true, "#7f8794", "#9fb38f").Scale(s, s * 0.8f, s);
            parts.Add(rk.RotateY((float)rng.NextD() * 6).Translate(x, height(x, z) - 0.15f, z));
        }
        return parts;
    }

    /// <summary>Kizak (oturma yeri + kivrik kizak demirleri).</summary>
    public static Geo Sled(string color = "#e2463f") => Merge(
        Box(color, 0.62f, 0.08f, 1.1f, V(0, 0.22f, 0)),
        Box(P.WoodDark, 0.62f, 0.05f, 0.08f, V(0, 0.29f, -0.45f)),
        Box(P.WoodDark, 0.62f, 0.05f, 0.08f, V(0, 0.29f, 0.3f)),
        Tube(new List<Vector3> { V(-0.27f, 0.12f, -0.55f), V(-0.27f, 0.02f, -0.2f), V(-0.27f, 0.02f, 0.4f), V(-0.27f, 0.14f, 0.62f), V(-0.27f, 0.3f, 0.55f) }, 12, 0.025f, 4).Color(P.Metal),
        Tube(new List<Vector3> { V(0.27f, 0.12f, -0.55f), V(0.27f, 0.02f, -0.2f), V(0.27f, 0.02f, 0.4f), V(0.27f, 0.14f, 0.62f), V(0.27f, 0.3f, 0.55f) }, 12, 0.025f, 4).Color(P.Metal),
        Box(P.Metal, 0.03f, 0.18f, 0.03f, V(-0.27f, 0.12f, -0.3f)),
        Box(P.Metal, 0.03f, 0.18f, 0.03f, V(0.27f, 0.12f, -0.3f)),
        Box(P.Metal, 0.03f, 0.18f, 0.03f, V(-0.27f, 0.12f, 0.3f)),
        Box(P.Metal, 0.03f, 0.18f, 0.03f, V(0.27f, 0.12f, 0.3f)));

    /// <summary>Kazi noktasi: hafif kabarik toprak + kuçuk taslar (parilti parcacikla verilir).</summary>
    public static Geo DigMound(bool snowy) => Merge(
        Part(SphereGeo(0.55f, 10, 4, 0, MathX.TwoPi, 0, MathX.Pi / 2), snowy ? "#e6eef6" : "#9a7a55", V(0, -0.05f, 0), null, V(1, 0.35f, 1)),
        Sphere(snowy ? "#b9c3cf" : "#8a7d6a", 0.08f, V(0.3f, 0.05f, 0.1f), null, 6, 4),
        Sphere(snowy ? "#b9c3cf" : "#8a7d6a", 0.06f, V(-0.25f, 0.04f, -0.2f), null, 6, 4));
}
