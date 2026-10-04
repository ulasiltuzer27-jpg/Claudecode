using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.Render;
using static Starfall.Render.Geo;
using L = Starfall.World.Isle1Shape.L;

namespace Starfall.World;

/// <summary>
/// Adayi kurar: yapilar, bitki ortusu, carpisma ve "capalar" (yildiz/tuy gibi esyalarin
/// oturdugu noktalar). Ayrica dunyadaki surekli animasyonlari (yel degirmeni, bayrak,
/// kayik, duman, ates bocekleri) isletir.
/// </summary>
public sealed partial class GameWorld
{
    public CollisionWorld Physics = null!;
    public readonly Dictionary<string, Vector3> Anchors = new();
    public readonly List<Geo> GlowParts = new();
    /// <summary>Surekli animasyonlar; true donerse listeden cikar.</summary>
    public readonly List<Func<float, float, bool>> Animated = new();
    public readonly List<Vector3> SmokeSources = new();
    public readonly HashSet<Shape> BounceShapes = new();
    public readonly Dictionary<Shape, MushroomRuntime> Mushrooms = new();
    public BuildResult Lighthouse = null!;
    public bool BridgeBuilt;
    public (Vector3 A, Vector3 B) BridgeEnds;
    public Node BridgeStubs = null!;
    public Node Boat = null!;
    public Vector3 BoatHome;
    public Vector3 CampfirePos;
    public PointLight FireLight = null!;
    public Material GlowMat = null!;
    public readonly List<Clouds> CloudSets = new();
    public int TreeCount;
    public Vector3 RaceFlagPos;
    private float _smokeT, _ffT;
    private readonly Random _fx = new(11);

    public sealed class MushroomRuntime
    {
        public Node Node = null!;
        public float Squish;
        public string Id = "";
    }

    public Vector3 Anchor(string name) => Anchors.TryGetValue(name, out var v) ? v : throw new KeyNotFoundException("capa yok: " + name);

    public float GroundY(float x, float z) => Height(x, z);

    public float FootprintY(float x, float z, float r)
    {
        float m = float.MaxValue;
        foreach (var (dx, dz) in new[] { (0f, 0f), (r, 0f), (-r, 0f), (0f, r), (0f, -r) }) m = MathF.Min(m, Height(x + dx, z + dz));
        return m;
    }

    /// <summary>Isle 1'in yerlesim dislamalari: cimen/bitki bu dairelere girmez.</summary>
    public void CollectExclusions()
    {
        var ex = Exclusions;
        foreach (var c in WD.Village.Cottages) ex.Add(new(c.X, c.Z, 5.5f));
        ex.Add(new(WD.Village.Shop.X, WD.Village.Shop.Z, 2.8f));
        ex.Add(new(WD.Village.Campfire.X, WD.Village.Campfire.Y, 11));
        ex.Add(new(WD.Landmarks.Lighthouse.X, WD.Landmarks.Lighthouse.Z, 5));
        ex.Add(new(WD.Landmarks.Windmill.X, WD.Landmarks.Windmill.Z, 4));
        ex.Add(new(WD.Landmarks.Dome.X, WD.Landmarks.Dome.Z, 7));
        ex.Add(new(WD.Landmarks.RaceFlag.X, WD.Landmarks.RaceFlag.Y, 2));
        foreach (var m in WD.GiantMushrooms) ex.Add(new(m.X, m.Z, m.Cap * 0.5f));
        foreach (var r in WD.PlatformRocks) ex.Add(new(r.X, r.Z, r.S * 1.2f));
        foreach (var n in WD.Npcs) ex.Add(new(n.X, n.Z, 1.6f));
    }

    /// <summary>Bir yapiyi dunyaya yerlestir: mesh + carpisma + parilti + capalar.</summary>
    public BuildResult Place(BuildResult res, float x, float y, float z, float yaw = 0, string? name = null, string? tag = null)
    {
        res.Group.Position = new Vector3(x, y, z);
        res.Group.Rotation = new Vector3(0, yaw, 0);
        Scene.Add(res.Group);
        res.Group.UpdateWorld(Matrix4x4.Identity);
        var at = new Vector3(x, y, z);
        foreach (var d in res.Colliders) res.Placed.Add(Physics.Add(d, at, yaw, tag));
        foreach (var g in res.Glow) GlowParts.Add(g.Transform(res.Group.World));
        if (name != null)
            foreach (var (k, v) in res.Anchors) Anchors[$"{name}.{k}"] = Vector3.Transform(v, res.Group.World);
        return res;
    }

    private Node AddMesh(Geo g, Vector3 pos, Material? mat = null, bool flat = true)
    {
        var n = new Node(MeshData.From(g, flat), mat ?? Material.Std()) { Position = pos };
        Scene.Add(n);
        return n;
    }

    public void BuildIsle1Structures()
    {
        BuildVillage();
        BuildLandmarks();
        BuildVegetation();
    }

    private void BuildVillage()
    {
        foreach (var c in WD.Village.Cottages)
        {
            var res = Buildings.Cottage(c.Wall, c.Roof, c.X * 3 + c.Z);
            float y = FootprintY(c.X, c.Z, 2.4f);
            Place(res, c.X, y, c.Z, c.Yaw, c.Id);
            if (res.Anchors.ContainsKey("smoke")) SmokeSources.Add(Anchors[$"{c.Id}.smoke"]);
        }
        Anchors["hutRoof"] = Anchors["hut.roofTop"] + new Vector3(0, 0.8f, 0);

        var shopDef = WD.Village.Shop;
        var shop = Buildings.ShopStall();
        float sy = FootprintY(shopDef.X, shopDef.Z, 1.5f);
        Place(shop, shopDef.X, sy, shopDef.Z, shopDef.Yaw, "shop");
        Anchors["shopRoof"] = new Vector3(shopDef.X, sy + 3.75f, shopDef.Z);

        var cfp = WD.Village.Campfire;
        var cf = Buildings.Campfire();
        float cy = GroundY(cfp.X, cfp.Y);
        Place(cf, cfp.X, cy, cfp.Y, 0, "campfire");
        CampfirePos = new Vector3(cfp.X, cy + 0.3f, cfp.Y);
        FireLight = new PointLight { Position = new Vector3(cfp.X, cy + 1.2f, cfp.Y), Color = MathX.Hex("#ff9a4a"), Intensity = 0, Distance = 14, Decay = 1.6f };
        Scene.Lights.Add(FireLight);

        foreach (var p in WD.Village.Lanterns)
            Place(Buildings.Lantern(), p.X, GroundY(p.X, p.Y), p.Y, MathF.Atan2(cfp.X - p.X, cfp.Y - p.Y) - MathX.Pi / 2);
        foreach (var (x, z, yaw) in WD.Village.Crates) Place(Buildings.Crate(), x, GroundY(x, z), z, yaw);
        // kulube catisina tirmanmak icin ust uste sandiklar
        var (hx, hz, _) = WD.Village.Crates[2];
        Place(Buildings.Crate(), hx, GroundY(hx, hz) + 0.8f, hz, 0.8f);
        Place(Buildings.Barrel(), hx + 0.9f, GroundY(hx + 0.9f, hz - 0.9f), hz - 0.9f, 0);
        foreach (var p in WD.Village.Barrels) Place(Buildings.Barrel(), p.X, GroundY(p.X, p.Y), p.Y, 0);
        foreach (var (x, z, yaw) in WD.Village.Benches) Place(Buildings.Bench(), x, GroundY(x, z), z, yaw);
        foreach (var (x, z, yaw) in WD.Village.Signs) Place(Buildings.Signpost(), x, GroundY(x, z), z, yaw);
        var hs = WD.HintSign;
        Place(Buildings.Signpost("#b98aff", "#7fd0ff"), hs.X, GroundY(hs.X, hs.Z), hs.Z, hs.Yaw);
        foreach (var pts in WD.Village.Fences) Place(Buildings.Fence(pts, Height), 0, 0, 0, 0);

        // iskele + kayik
        var D = WD.DockDef;
        Place(Buildings.Dock(D.Length), D.X, D.Y, D.Z0, 0, "dock");
        var boat = Buildings.Rowboat();
        BoatHome = new Vector3(D.X + 2.9f, 0.38f, D.Z0 + D.Length - 4);
        Place(boat, BoatHome.X, BoatHome.Y, BoatHome.Z, 0.15f, "boat");
        Boat = boat.Group;
        Anchors["boat"] = new Vector3(D.X + 2.9f, 1.6f, D.Z0 + D.Length - 4);
        Animated.Add((t, dt) =>
        {
            if (Boat.Parent == null) return false;
            Boat.Position = Boat.Position with { Y = 0.38f + MathF.Sin(t * 1.3f) * 0.06f };
            Boat.Rotation = Boat.Rotation with { Z = MathF.Sin(t * 1.1f) * 0.04f };
            return false;
        });

        // kumdan kale
        var sc = WD.Landmarks.Sandcastle;
        float sy2 = GroundY(sc.X, sc.Y);
        Place(Buildings.Sandcastle(), sc.X, sy2 - 0.15f, sc.Y, 0.3f, "castle");
        Anchors["sandcastle"] = new Vector3(sc.X, sy2 + 2.5f, sc.Y);
    }

    private void BuildLandmarks()
    {
        // fener
        var lhd = WD.Landmarks.Lighthouse;
        Lighthouse = Buildings.Lighthouse();
        float ly = FootprintY(lhd.X, lhd.Z, 3);
        Place(Lighthouse, lhd.X, ly, lhd.Z, lhd.Yaw, "lighthouse");

        // yel degirmeni
        var wmd = WD.Landmarks.Windmill;
        var wm = Buildings.Windmill();
        float wy = FootprintY(wmd.X, wmd.Z, 2.4f);
        Place(wm, wmd.X, wy, wmd.Z, wmd.Yaw, "windmill");
        Animated.Add((t, dt) => { wm.Sails!.Rotation += new Vector3(0, 0, dt * 0.6f); return false; });

        // kopru: once yarim (kazik), kunduz gorevi bitince tamamlaniyor
        var ba = WD.Landmarks.BridgeA; var bb = WD.Landmarks.BridgeB;
        var a = new Vector3(ba.X, GroundY(ba.X, ba.Y) + 0.15f, ba.Y);
        var b = new Vector3(bb.X, GroundY(bb.X, bb.Y) + 0.15f, bb.Y);
        BridgeEnds = (a, b);
        var stubs = new List<Geo>();
        foreach (var p in new[] { a, b })
            foreach (var s in new[] { -1.1f, 1.1f })
                stubs.Add(Part(CylinderGeo(0.1f, 0.12f, 1.6f, 6), P.WoodDark, V(p.X, p.Y + 0.2f, p.Z + s)));
        BridgeStubs = AddMesh(Merge(stubs), Vector3.Zero);

        // batik gemi
        var swd = WD.Landmarks.Shipwreck;
        var sw = Buildings.Shipwreck();
        float swy = MathF.Max(-0.4f, GroundY(swd.X, swd.Z) + 0.6f);
        Place(sw, swd.X, swy, swd.Z, swd.Yaw, "ship");
        Anchors["nest"] = Anchors["ship.nest"];
        Anchors["deck"] = new Vector3(swd.X, swy + 1.3f, swd.Z);

        // gizli magara
        var dd = WD.Landmarks.Dome;
        var dome = Buildings.RockDome();
        float dy = GroundY(dd.X, dd.Z);
        Place(dome, dd.X, dy - 0.3f, dd.Z, dd.Yaw, "dome");
        Anchors["domeInside"] = new Vector3(dd.X, dy + 1.0f, dd.Z);
        Scene.Lights.Add(new PointLight { Position = new Vector3(dd.X, dy + 2, dd.Z), Color = MathX.Hex("#7ef0ff"), Intensity = 14, Distance = 12, Decay = 1.5f });

        // yaris bayragi
        var rf = WD.Landmarks.RaceFlag;
        var flag = Buildings.FlagPole("#ffcf4a");
        float fy = GroundY(rf.X, rf.Y);
        Place(flag, rf.X, fy, rf.Y, 0, "flag");
        RaceFlagPos = new Vector3(rf.X, fy, rf.Y);
        Animated.Add((t, dt) =>
        {
            flag.Flag!.Rotation = new Vector3(0, MathF.Sin(t * 2.2f) * 0.25f, 0);
            flag.Flag.Scale = new Vector3(1 + MathF.Sin(t * 5) * 0.05f, 1, 1);
            return false;
        });

        // tavsanin bahcesi (cit + havuc sirasi)
        var g = WD.Landmarks.RabbitGarden;
        float gx = g.X, gz = g.Y;
        Place(Buildings.Fence(new[] { new Vector2(gx - 4, gz - 3), new Vector2(gx + 4, gz - 3), new Vector2(gx + 4, gz + 3), new Vector2(gx + 1, gz + 3) }, Height), 0, 0, 0, 0);
        Place(Buildings.Fence(new[] { new Vector2(gx - 2, gz + 3), new Vector2(gx - 4, gz + 3), new Vector2(gx - 4, gz - 3) }, Height), 0, 0, 0, 0);
        var rows = new List<Geo>();
        for (int i = 0; i < 4; i++)
        {
            for (int k = 0; k < 6; k++)
            {
                float x = gx - 2.8f + k * 1.1f;
                float z = gz - 1.8f + i * 1.2f;
                float y = GroundY(x, z);
                rows.Add(Part(CylinderGeo(0, 0.05f, 0.25f, 3), "#5fbf4a", V(x, y + 0.12f, z)));
                rows.Add(Part(SphereGeo(0.12f, 5, 3), "#8a6a4a", V(x, y + 0.02f, z), null, V(1.4f, 0.4f, 1.4f)));
            }
        }
        AddMesh(Merge(rows), Vector3.Zero, new Material { CastShadow = false });

        // kutuk (orman)
        var st = WD.Landmarks.Stump;
        float sty = GroundY(st.X, st.Y);
        var stump = new BuildResult();
        stump.Colliders.Add(ShapeDef.Cyl(1.5f, 1.6f, V(0, 1.4f, 0)));
        var sg = CylinderGeo(1.45f, 1.8f, 3.2f, 12, 2).Color(P.Bark);
        sg.Jitter(0.12f, 5);
        sg.Translate(0, 1.4f, 0);
        var stumpParts = new List<Geo>
        {
            sg,
            Part(CylinderGeo(1.35f, 1.35f, 0.05f, 12), "#d9b98a", V(0, 3.02f, 0)),
            Part(TorusGeo(0.9f, 0.04f, 4, 16), "#b8925f", V(0, 3.05f, 0), V(MathX.Pi / 2, 0, 0)),
            Part(TorusGeo(0.5f, 0.04f, 4, 16), "#b8925f", V(0, 3.05f, 0), V(MathX.Pi / 2, 0, 0)),
        };
        foreach (var ang in new[] { 0f, 1.6f, 3.4f, 4.8f })
            stumpParts.Add(Part(CylinderGeo(0.1f, 0.35f, 1.6f, 6), P.BarkDark, V(MathF.Cos(ang) * 1.7f, 0.1f, MathF.Sin(ang) * 1.7f), V(MathF.Sin(ang) * 1.2f, 0, -MathF.Cos(ang) * 1.2f)));
        stump.Add(stumpParts);
        Place(stump, st.X, sty - 0.2f, st.Y, 0, "stump");
        Anchors["stump"] = new Vector3(st.X, sty + 3.8f, st.Y);

        // cayirdaki kucuk kutuk (ilk tuy)
        var ms = WD.Landmarks.MeadowStump;
        float msy = GroundY(ms.X, ms.Y);
        var mstump = new BuildResult();
        mstump.Colliders.Add(ShapeDef.Cyl(0.7f, 0.5f, V(0, 0.4f, 0)));
        mstump.Add(new[]
        {
            Part(CylinderGeo(0.65f, 0.8f, 1.0f, 10), P.Bark, V(0, 0.4f, 0)),
            Part(CylinderGeo(0.6f, 0.6f, 0.04f, 10), "#d9b98a", V(0, 0.91f, 0)),
        });
        Place(mstump, ms.X, msy - 0.1f, ms.Y, 0);
        Anchors["meadowStump"] = new Vector3(ms.X, msy + 1.9f, ms.Y);

        // platform kayalari
        foreach (var r in WD.PlatformRocks) PlaceRockStack(r);

        // dev mantarlar (ziplatan)
        for (int i = 0; i < WD.GiantMushrooms.Length; i++)
        {
            var m = WD.GiantMushrooms[i];
            float y = GroundY(m.X, m.Z) - 0.2f;
            var node = AddMesh(Nature.GiantMushroom(m.Color, m.Cap, m.H, (uint)(i + 3)), new Vector3(m.X, y, m.Z), Material.Std(), false);
            var at = new Vector3(m.X, y, m.Z);
            Physics.Add(ShapeDef.Cyl(m.Cap * 0.26f, m.H / 2, V(0, m.H / 2, 0)), at);
            var cap = Physics.Add(ShapeDef.Cyl(m.Cap * 0.95f, m.Cap * 0.22f, V(0, m.H - 0.1f + m.Cap * 0.22f, 0)), at, 0, "bounce");
            BounceShapes.Add(cap);
            Anchors[m.Id] = new Vector3(m.X, y + m.H + m.Cap * 0.5f + 0.9f, m.Z);
            var rt = new MushroomRuntime { Node = node, Id = m.Id };
            Mushrooms[cap] = rt;
            Animated.Add((t, dt) =>
            {
                rt.Squish = MathF.Max(0, rt.Squish - dt * 3);
                float s = MathF.Sin(rt.Squish * MathX.Pi * 3) * rt.Squish * 0.25f;
                rt.Node.Scale = new Vector3(1 + s, 1 - s, 1 + s);
                return false;
            });
        }

        // ruzgar sutunu kayasi (ruzgarli kayaliklar)
        var sp = WD.Landmarks.Spire;
        float spy = GroundY(sp.X, sp.Y);
        var spire = CylinderGeo(1.4f, 2.6f, 15, 8, 5).Color(P.Rock);
        spire.Jitter(0.6f, 21);
        spire.Translate(0, 7.5f, 0);
        var spireTop = CylinderGeo(1.6f, 1.5f, 0.6f, 8).Color(P.GrassMoss).Translate(0, 15.1f, 0);
        AddMesh(Merge(spire, spireTop), new Vector3(sp.X, spy - 0.5f, sp.Y));
        Physics.Add(ShapeDef.Cyl(1.9f, 7.7f, V(0, 7.6f, 0)), new Vector3(sp.X, spy - 0.5f, sp.Y));
        Anchors["spire"] = new Vector3(sp.X, spy - 0.5f + 16.3f, sp.Y);
    }

    private void PlaceRockStack(RockStackDef r)
    {
        var stack = r.Stack;
        float y = GroundY(r.X, r.Z) - stack[0] * 0.25f;
        float ox = 0;
        for (int i = 0; i < stack.Length; i++)
        {
            var g = Nature.Rock((uint)Math.Floor(r.X * 7 + r.Z * 3 + i), true);
            float s = stack[i];
            g.Scale(s * 1.1f, s, s * 1.1f);
            ox = i > 0 ? 0.3f : 0;
            var node = AddMesh(g, new Vector3(r.X + ox, y, r.Z));
            node.Rotation = new Vector3(0, i * 1.3f, 0);
            Physics.Add(ShapeDef.HullOf(g.P, default, V(0, i * 1.3f, 0)), new Vector3(r.X + ox, y, r.Z));
            var (_, mx) = g.Bounds();
            y += mx.Y * 0.82f;
        }
        float ax = r.X + (stack.Length > 1 ? 0.3f : 0);
        float top = Physics.GroundAt(ax, r.Z, y + 10, out var gy, out _) ? gy : y;
        Anchors[r.Id] = new Vector3(ax, top + 0.95f, r.Z);
    }

    public void BuildBridge(bool animate = false)
    {
        if (BridgeBuilt) return;
        BridgeBuilt = true;
        var res = Buildings.Bridge(BridgeEnds.A, BridgeEnds.B);
        Place(res, 0, 0, 0, 0);
        if (animate)
        {
            res.Group.Position = new Vector3(0, 8, 0);
            float t0 = 0;
            Animated.Add((_, dt) =>
            {
                t0 += dt;
                float yy = MathF.Max(0, 8 - t0 * t0 * 9);
                res.Group.Position = new Vector3(0, yy, 0);
                return yy <= 0;
            });
        }
    }

    private InstanceBatch Batch(MeshData mesh, Material mat, float cull = 0)
    {
        var b = new InstanceBatch(mesh, mat) { CullDistance = cull };
        Scene.AddBatch(b);
        return b;
    }

    private void BuildVegetation()
    {
        var rng = new Rng(9001);
        float R() => (float)rng.NextD();
        var types = new Dictionary<string, List<(float x, float y, float z, float s, float yaw)>>
        {
            ["round"] = new(), ["blossom"] = new(), ["pine"] = new(), ["birch"] = new(), ["palm"] = new(),
        };

        void AddTree(string type, float x, float z, float s, float yaw)
        {
            float y = Height(x, z) - 0.1f;
            types[type].Add((x, y, z, s, yaw));
            float r = type == "palm" ? 0.2f : type == "pine" ? 0.28f : 0.32f;
            Physics.Add(ShapeDef.Cyl(r * s, 1.6f * s, V(0, 1.6f * s, 0)), new Vector3(x, y, z));
        }

        float lakeR = L.LakeR + 8;
        for (float gx = -230; gx < 230; gx += 3.6f)
        {
            for (float gz = -230; gz < 230; gz += 3.6f)
            {
                float x = gx + (R() - 0.5f) * 3.2f;
                float z = gz + (R() - 0.5f) * 3.2f;
                float h = Height(x, z);
                if (h < WaterLevel(x, z) + 1.3f) continue;
                if (Normal(x, z).Y < 0.86f) continue;
                if (PathDistance(x, z) < 3.2f) continue;
                if (MathX.Hypot(x - L.Lake.X, z - L.Lake.Y) < lakeR) continue;
                if (IsExcluded(x, z, 2.5f)) continue;
                var tint = Isle1Paint.Tints(x, z);
                float dV = MathX.Hypot(x - L.Village.X, z - L.Village.Y);
                float dP = MathX.Hypot(x - L.Peak.X, z - L.Peak.Y);
                if (dP < 34) continue; // zirve teraslari acik kalsin
                float dens = 0.035f + tint.forest * 0.55f + tint.meadow * 0.03f + tint.hollow * 0.1f;
                dens += MathX.Smoothstep(36, 48, dP) * (1 - MathX.Smoothstep(60, 80, dP)) * 0.2f;
                if (dV < 50) dens *= 0.12f;
                if (tint.windy > 0.5f) dens *= 0.4f;
                if (R() > dens) continue;
                string type;
                float r = R();
                if (tint.forest > 0.4f) type = r < 0.4f ? "pine" : r < 0.65f ? "birch" : "round";
                else if (dP < 80) type = r < 0.8f ? "pine" : "round";
                else if (tint.meadow > 0.35f) type = r < 0.55f ? "blossom" : "round";
                else if (tint.hollow > 0.4f) type = r < 0.5f ? "birch" : "round";
                else type = r < 0.55f ? "round" : r < 0.85f ? "pine" : "birch";
                float s = 0.8f + R() * 0.6f;
                AddTree(type, x, z, s, R() * MathX.TwoPi);
            }
        }
        foreach (var p in WD.Village.Palms) { float s = 0.9f + R() * 0.3f; AddTree("palm", p.X, p.Y, s, R() * 6); }
        // kumsallarda birkac palmiye daha
        for (int i = 0; i < 220 && types["palm"].Count < 30; i++)
        {
            float a = R() * MathX.TwoPi;
            float rr = 130 + R() * 40;
            float x = MathF.Cos(a) * rr;
            float z = MathF.Sin(a) * rr;
            float h = Height(x, z);
            if (h < 1.0f || h > 2.6f || IsExcluded(x, z, 3) || PathDistance(x, z) < 3) continue;
            float s = 0.9f + R() * 0.3f;
            AddTree("palm", x, z, s, R() * 6);
        }

        var geos = new Dictionary<string, Geo[]>
        {
            ["round"] = new[] { Nature.RoundTree(1), Nature.RoundTree(2) },
            ["blossom"] = new[] { Nature.RoundTree(3, true) },
            ["pine"] = new[] { Nature.PineTree(1), Nature.PineTree(2) },
            ["birch"] = new[] { Nature.BirchTree(1) },
            ["palm"] = new[] { Nature.PalmTree(1) },
        };
        var treeMat = new Material { Sway = 1 };
        TreeCount = 0;
        foreach (var (type, list) in types)
        {
            var variants = geos[type];
            for (int vi = 0; vi < variants.Length; vi++)
            {
                var items = list.Where((_, i) => i % variants.Length == vi).ToList();
                if (items.Count == 0) continue;
                var bt = Batch(MeshData.From(variants[vi], true), treeMat);
                foreach (var t in items)
                {
                    float k = 0.9f + R() * 0.2f;
                    bt.Add(MathX.TRS(new Vector3(t.x, t.y, t.z), t.yaw, t.s), new Vector4(k, k, k, 1));
                }
                TreeCount += items.Count;
            }
        }

        // kayalar
        var rocks = new List<(float x, float z, float y, float s, float yaw, int v)>();
        for (float gx = -230; gx < 230; gx += 8)
        {
            for (float gz = -230; gz < 230; gz += 8)
            {
                float x = gx + (R() - 0.5f) * 7;
                float z = gz + (R() - 0.5f) * 7;
                float h = Height(x, z);
                if (h < -1.5f) continue;
                var n = Normal(x, z);
                if (PathDistance(x, z) < 2.5f || IsExcluded(x, z, 2)) continue;
                if (MathX.Hypot(x - L.Lake.X, z - L.Lake.Y) < L.LakeR - 2) continue;
                float p = 0.03f;
                if (n.Y < 0.86f && n.Y > 0.55f) p += 0.25f;
                if (h > -1.5f && h < 2.2f) p += 0.06f;
                if (MathX.Hypot(x - L.Village.X, z - L.Village.Y) < 40) p *= 0.2f;
                if (R() > p) continue;
                float s = 0.4f + R() * 1.5f;
                float yaw = R() * 6;
                int v = (int)MathF.Floor(R() * 4);
                rocks.Add((x, z, h - 0.25f, s, yaw, v));
            }
        }
        var rockMat = Material.Std();
        for (int vi = 0; vi < 4; vi++)
        {
            var geo = Nature.Rock((uint)(vi + 11), true);
            var items = rocks.Where(r => r.v == vi).ToList();
            if (items.Count == 0) continue;
            var bt = Batch(MeshData.From(geo, true), rockMat);
            var hull = ConvexHull.Build(geo.P);
            foreach (var r in items)
            {
                bt.Add(MathX.TRS(new Vector3(r.x, r.y, r.z), r.yaw, r.s));
                if (r.s > 0.7f) Physics.Add(ShapeDef.FromHull(hull.Scaled(r.s), default, V(0, r.yaw, 0)), new Vector3(r.x, r.y, r.z));
            }
        }

        // calilar, cicekler, kucuk mantarlar (carpismasiz)
        var bushes = new List<(float x, float z, float y, float s, float yaw)>();
        var flowers = new List<(float x, float z, float y, float s, float yaw, int c)>();
        var shrooms = new List<(float x, float z, float y, float s, float yaw, int v)>();
        float flowerQ = Env.QualityFlowers;
        for (float gx = -230; gx < 230; gx += 1.6f)
        {
            for (float gz = -230; gz < 230; gz += 1.6f)
            {
                float x = gx + (R() - 0.5f) * 1.5f;
                float z = gz + (R() - 0.5f) * 1.5f;
                float h = Height(x, z);
                if (h < WaterLevel(x, z) + 0.6f) continue;
                float r = R();
                var tint = Isle1Paint.Tints(x, z);
                float pd = PathDistance(x, z);
                if (pd < 1.8f) continue;
                if (Normal(x, z).Y < 0.84f) continue;
                if (IsExcluded(x, z, 0.5f)) continue;
                if (h < 1.9f) continue;
                float fp = (0.004f + tint.meadow * 0.16f + tint.peak * 0.02f) * flowerQ;
                if (r < fp)
                {
                    float s = 0.8f + R() * 0.6f; float yaw = R() * 6; int c = (int)MathF.Floor(R() * 6);
                    flowers.Add((x, z, h, s, yaw, c));
                }
                else if (r < fp + 0.006f + tint.forest * 0.02f)
                {
                    float s = 0.6f + R() * 0.8f; float yaw = R() * 6;
                    bushes.Add((x, z, h - 0.1f, s, yaw));
                }
                else if (r < fp + 0.006f + tint.forest * 0.02f + (tint.forest + tint.hollow) * 0.03f)
                {
                    float s = 0.6f + R() * 1.2f; float yaw = R() * 6; int v = R() < 0.7f ? 0 : 1;
                    shrooms.Add((x, z, h, s, yaw, v));
                }
            }
        }
        var bushB = Batch(MeshData.From(Nature.Bush(1), true), new Material { Sway = 1 });
        foreach (var o in bushes) bushB.Add(MathX.TRS(new Vector3(o.x, o.y, o.z), o.yaw, o.s));
        var petal = new[] { "#ff7aa2", "#ffd84a", "#ffffff", "#c58bff", "#ff6b5a", "#7fc8ff" }.Select(c => { var v = MathX.Hex(c); return new Vector4(v, 1); }).ToArray();
        var flowerB = Batch(MeshData.From(Nature.Flower(), false), new Material { CastShadow = false }, 140);
        foreach (var o in flowers) flowerB.Add(MathX.TRS(new Vector3(o.x, o.y, o.z), o.yaw, o.s), petal[o.c]);
        var sh0 = Batch(MeshData.From(Nature.Mushroom(P.MushroomRed, 1), false), new Material { CastShadow = false }, 140);
        var sh1 = Batch(MeshData.From(Nature.Mushroom(P.MushroomBlue, 2), false), new Material { CastShadow = false }, 140);
        foreach (var o in shrooms) (o.v == 0 ? sh0 : sh1).Add(MathX.TRS(new Vector3(o.x, o.y, o.z), o.yaw, o.s));

        // ormandaki peri halkasi (s07 icinde)
        var ring = Batch(MeshData.From(Nature.Mushroom("#ff8fb1", 7), false), new Material { CastShadow = false });
        var rc = new Vector2(-102, 22);
        for (int i = 0; i < 11; i++)
        {
            float a = i / 11f * MathX.TwoPi;
            float x = rc.X + MathF.Cos(a) * 2.4f;
            float z = rc.Y + MathF.Sin(a) * 2.4f;
            ring.Add(MathX.TRS(new Vector3(x, Height(x, z), z), a, 1.3f + (i % 3) * 0.3f));
        }
    }

    public void BuildGlow()
    {
        GlowMat = new Material { Tint = MathX.Hex("#cfe8ff"), Emissive = MathX.Hex("#ffc965"), EmissiveIntensity = 0 };
        if (GlowParts.Count > 0) AddMesh(Merge(GlowParts), Vector3.Zero, GlowMat);
    }

    /// <summary>Gece etkisi: pencereler ve fenerler yanar, kamp atesi isik verir; parcacik kaynaklari.</summary>
    public void Update(float dt, float t, SkyState sky, Vector3 playerPos)
    {
        Time = t;
        for (int i = Animated.Count - 1; i >= 0; i--)
            if (Animated[i](t, dt)) Animated.RemoveAt(i);
        float night = sky.Night;
        float lit = MathX.Smoothstep(0.15f, 0.6f, night);
        GlowMat.EmissiveIntensity = lit * 2.2f;
        GlowMat.Tint = new Vector3(0.81f - lit * 0.3f, 0.9f - lit * 0.35f, 1 - lit * 0.5f);
        FireLight.Intensity = (1.5f + lit * 5) * (0.85f + MathF.Sin(t * 13) * 0.08f + MathF.Sin(t * 7.3f) * 0.07f);
        foreach (var c in CloudSets) c.Update(dt, sky);

        _smokeT -= dt;
        if (_smokeT <= 0)
        {
            _smokeT = 0.35f;
            foreach (var s in SmokeSources)
            {
                if (Vector3.DistanceSquared(s, playerPos) > 120 * 120) continue;
                Env.Normal.Emit(Emit.At(s, 1, "#eef0f2", "#dfe3e8").Sp(0.15f).V(0.3f, 1.0f, 0.1f, 0.15f).L(4).S(0.7f, 2.2f).A(0.45f).D(0.2f));
            }
        }
        // kamp atesi
        if (Vector3.DistanceSquared(CampfirePos, playerPos) < 80 * 80)
            Env.Additive.Emit(Emit.At(CampfirePos, 2, "#ffb347", "#ff7a2a", "#ffd27a").Sp(0.25f).V(0, 1.6f, 0, 0.3f).L(0.8f).S(0.5f, 0.05f).A(0.9f));
        // ruzgar sutunlari
        foreach (var u in WD.Updrafts)
        {
            if ((u.X - playerPos.X) * (u.X - playerPos.X) + (u.Z - playerPos.Z) * (u.Z - playerPos.Z) > 90 * 90) continue;
            float y0 = GroundY(u.X, u.Z);
            Env.Normal.Emit(Emit.At(new Vector3(u.X, y0 + 1 + (float)_fx.NextDouble() * 6, u.Z), 1, "#ffffff")
                .Sp(u.R * 0.8f, 0.5f, u.R * 0.8f).V(0, 9, 0, 0.6f).L(2.4f).S(0.18f, 0.05f).A(0.7f));
        }
        // ates bocekleri (gece, cayir ve orman)
        if (night > 0.5f)
        {
            _ffT -= dt;
            if (_ffT <= 0)
            {
                _ffT = 0.12f;
                float a = (float)_fx.NextDouble() * MathX.TwoPi;
                float r = 4 + (float)_fx.NextDouble() * 18;
                float x = playerPos.X + MathF.Cos(a) * r;
                float z = playerPos.Z + MathF.Sin(a) * r;
                if (TerrainAt(x, z) == Isle1)
                {
                    var tint = Isle1Paint.Tints(x, z);
                    if (tint.meadow + tint.forest + tint.hollow > 0.25f && Height(x, z) > 1)
                        Env.Additive.Emit(Emit.At(new Vector3(x, Height(x, z) + 0.6f + (float)_fx.NextDouble() * 1.6f, z), 1, "#d8ff6a", "#fff27a")
                            .V(0, 0.1f, 0).L(4).S(0.22f).A(1).W(2.5f));
                }
            }
        }
    }
}
