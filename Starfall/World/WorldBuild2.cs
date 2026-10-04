using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.World;

/// <summary>Kar Adasi kurulumu + Ada 1'e eklenen Asama B yapilari (tirmanma, kazi noktalari).</summary>
public sealed partial class GameWorld
{
    public BuildResult Observatory = null!;
    public readonly List<(string Id, BuildResult Res, ClimbWallDef Def)> ClimbWalls = new();
    public readonly List<Vector3> SteamSources = new();
    public Node? Boat2;
    public readonly Dictionary<string, Node> DigMounds = new();
    public Vector3 SledFinishPos;
    private float _snowT, _steamT;

    /// <summary>Kar Adasi dislamalari (CollectExclusions'tan cagrilir).</summary>
    private void CollectExclusions2()
    {
        var ex = Exclusions;
        foreach (var c in WD2.Cabins) ex.Add(new(c.X, c.Z, 5.5f));
        ex.Add(new(WD2.BearShop.X, WD2.BearShop.Y, 3));
        ex.Add(new(L2.Summit.X, L2.Summit.Y, 9));
        ex.Add(new(L2.Cave.X, L2.Cave.Y, 7));
        foreach (var p in L2.Pools) ex.Add(new(p.X, p.Y, L2.PoolR + 1));
        foreach (var i in WD2.Igloos) ex.Add(new(i.X, i.Y, 3));
        foreach (var w in WD2.ClimbWalls) ex.Add(new(w.X, w.Z, w.W * 0.7f + 1));
        foreach (var n in WD2.Npcs) ex.Add(new(n.X, n.Z, 1.6f));
        ex.Add(new(L2.Training.X, L2.Training.Y, 5));
    }

    partial void BuildIsle2Impl()
    {
        float H(float x, float z) => Height(x, z);
        Regions.AddRange(WD2.Regions);
        foreach (var r in WD2.Regions) if (!r.Secret) MainRegions.Add(r.Id);
        AllFish.AddRange(WD2.Fish);
        NpcDefs.AddRange(WD2.Npcs);
        Boundaries.Add((L2.C, 215));
        Corridors.Add((new Vector2(0, 0), L2.C, 70));
        // --- limanin iskelesi + teknenin yanasma yeri
        var d0 = L2.DockStart;
        var dir = L2.DockDir;
        float dockYaw = MathF.Atan2(dir.X, dir.Y);
        Place(Buildings.Dock(L2.DockLength), d0.X, 1.25f, d0.Y, dockYaw, "dock2");

        foreach (var c in WD2.Cabins)
        {
            var res = Buildings2.SnowCabin(c.Wall, c.X * 3 + c.Z);
            float y = FootprintY(c.X, c.Z, 2.4f);
            Place(res, c.X, y, c.Z, c.Yaw, c.Id);
            if (res.Anchors.ContainsKey("smoke")) SmokeSources.Add(Anchors[$"{c.Id}.smoke"]);
        }
        var shop = Buildings.ShopStall("#4a7bd0", "#ffffff");
        var bs = WD2.BearShop;
        Place(shop, bs.X, FootprintY(bs.X, bs.Y, 1.5f), bs.Y, WD2.BearShopYaw, "bearshop");
        foreach (var p in WD2.Lanterns) Place(Buildings.Lantern(), p.X, H(p.X, p.Y), p.Y, MathF.Atan2(L2.Harbor.X - p.X, L2.Harbor.Y - p.Y) - MathX.Pi / 2);
        foreach (var (x, z, yaw, key) in WD2.Signs)
        {
            Place(Buildings.Signpost("#7fd0ff", "#ffffff"), x, H(x, z), z, yaw);
            ExtraSigns.Add((x, z, key));
        }
        foreach (var ig in WD2.Igloos) Place(Buildings2.Igloo(ig.X), ig.X, H(ig.X, ig.Y) - 0.15f, ig.Y, MathF.Atan2(L2.Harbor.X - ig.X, L2.Harbor.Y - ig.Y));

        // --- rasathane (zirve platosunun ortasi)
        Observatory = Buildings2.Observatory();
        float oy = FootprintY(L2.Summit.X, L2.Summit.Y, 5);
        float oyaw = MathF.Atan2(L2.Harbor.X - L2.Summit.X, L2.Harbor.Y - L2.Summit.Y);
        Place(Observatory, L2.Summit.X, oy, L2.Summit.Y, oyaw, "observatory");
        Anchors["observatoryTop"] = Anchors["observatory.top"] + new Vector3(0, 0.7f, 0);

        // --- tirmanma duvarlari
        foreach (var w in WD2.ClimbWalls) PlaceClimbWall(w);

        // --- donmus gol: buz yuzeyi (kaygan, uzerinde yurunur) + buz delikleri
        Physics.Add(ShapeDef.Cyl(L2.LakeR + 1.5f, 0.25f, new Vector3(0, -0.25f, 0)), new Vector3(L2.Lake.X, L2.LakeLevel + 0.02f, L2.Lake.Y), 0, "ice");
        FrozenLakes.Add((L2.Lake, L2.LakeR + 1.5f, L2.LakeLevel));
        IceHoles.AddRange(WD2.IceHoles);
        Anchors["lakeIslet"] = new Vector3(L2.Lake.X + 6, H(L2.Lake.X + 6, L2.Lake.Y - 4) + 1.0f, L2.Lake.Y - 4);
        var islet = Nature.SnowRock(311).Scale(1.6f, 1.1f, 1.6f);
        AddMesh(islet, new Vector3(L2.Lake.X + 6, H(L2.Lake.X + 6, L2.Lake.Y - 4) - 0.6f, L2.Lake.Y - 4));

        // --- kizak tepesi: baslangic bayragi + bitis bayragi
        float sty = H(L2.SledTop.X, L2.SledTop.Y);
        Place(Buildings.FlagPole("#3fa7ff"), L2.SledTop.X + 2.5f, sty, L2.SledTop.Y, 0, "sledStart");
        Anchors["sledTop"] = new Vector3(L2.SledTop.X + 2.5f, sty + 4.2f, L2.SledTop.Y);
        var fin = WD2.SledFinish;
        var flag2 = Buildings.FlagPole("#ff5a5a");
        Place(flag2, fin.X, H(fin.X, fin.Y), fin.Y, 0, "sledFinish");
        SledFinishPos = new Vector3(fin.X, H(fin.X, fin.Y), fin.Y);
        Animated.Add((tt, dt) => { flag2.Flag!.Rotation = new Vector3(0, MathF.Sin(tt * 2.4f) * 0.3f, 0); return false; });
        AddMesh(Buildings2.Sled().RotateY(0.4f), new Vector3(L2.SledTop.X - 1, sty + 0.02f, L2.SledTop.Y + 2));

        // --- sicak su: havuzlar (ilik su) + kaya halkalari + buhar
        foreach (var p in L2.Pools)
        {
            Env.Waters.Add(new WaterSurface
            {
                Mesh = MeshData.From(CircleGeo(L2.PoolR + 0.9f, 32).RotateX(-MathX.Pi / 2), false),
                Position = new Vector3(p.X, 0, p.Y), Level = L2.SpringLevel, Amp = 0.02f,
                Shallow = MathX.Hex("#7fe6d6"), Deep = MathX.Hex("#2f9fb0"),
            });
            AddMesh(Merge(Buildings2.SpringRocks(p, L2.PoolR, H, (uint)(p.X * 7))), Vector3.Zero);
            SteamSources.Add(new Vector3(p.X, L2.SpringLevel + 0.2f, p.Y));
        }
        // donmus gol yuzeyi + buz delikleri
        Env.Waters.Add(new WaterSurface
        {
            Mesh = MeshData.From(CircleGeo(L2.LakeR + 4, 64).RotateX(-MathX.Pi / 2), false),
            Position = new Vector3(L2.Lake.X, 0, L2.Lake.Y), Level = L2.LakeLevel, Amp = 0, Ice = 1,
            Shallow = MathX.Hex("#cfefff"), Deep = MathX.Hex("#7fb8de"),
        });
        foreach (var hole in WD2.IceHoles)
            Env.Waters.Add(new WaterSurface
            {
                Mesh = MeshData.From(CircleGeo(0.8f, 16).RotateX(-MathX.Pi / 2), false),
                Position = new Vector3(hole.X, 0, hole.Y), Level = L2.LakeLevel + 0.03f, Amp = 0.01f,
                Shallow = MathX.Hex("#2f7fb0"), Deep = MathX.Hex("#164a7a"),
            });

        // --- gizli buz magarasi (giris +X: denize bakar)
        var dome = Buildings.RockDome("#7fd8ff", "#bfe9ff", "#8fe0ff", true);
        float cy = H(L2.Cave.X, L2.Cave.Y);
        Place(dome, L2.Cave.X, cy - 0.3f, L2.Cave.Y, 0, "iceCave");
        Anchors["iceCaveInside"] = new Vector3(L2.Cave.X, cy + 1.0f, L2.Cave.Y);
        Anchors["iceCaveFloor"] = new Vector3(L2.Cave.X - 1.6f, H(L2.Cave.X - 1.6f, L2.Cave.Y + 1.2f) + 0.12f, L2.Cave.Y + 1.2f);
        Scene.Lights.Add(new PointLight { Position = new Vector3(L2.Cave.X, cy + 2, L2.Cave.Y), Color = MathX.Hex("#9ff0ff"), Intensity = 14, Distance = 12, Decay = 1.5f });

        // --- buz sivrisi (tepesinde kristal) ve kiyidaki kar kaya yigini
        var sp = L2.W(-10, 120);
        float spy = H(sp.X, sp.Y);
        AddMesh(Buildings2.IceSpire(77, 11), new Vector3(sp.X, spy - 0.3f, sp.Y), new Material { Emissive = MathX.Hex("#6fd0ff"), EmissiveIntensity = 0.12f });
        Physics.Add(ShapeDef.Cyl(1.1f, 5.6f, new Vector3(0, 5.6f, 0)), new Vector3(sp.X, spy - 0.3f, sp.Y));
        Anchors["spireTop"] = new Vector3(sp.X, spy - 0.3f + 12.3f, sp.Y);
        Updrafts.Add(new UpdraftDef(sp.X + 4.5f, sp.Y + 2, 2.6f, spy + 16));
        // kiyidaki kaya yigini: dogu kiyisinda sig suda (kiyi cizgisi aranir)
        {
            var dirc = Vector2.Normalize(new Vector2(1, 0.12f));
            var pc = L2.C + dirc * 140;
            for (float r = 100; r < 200; r += 0.5f)
            {
                var q = L2.C + dirc * r;
                if (H(q.X, q.Y) < -0.6f) { pc = q; break; }
            }
            PlaceRockStack(new RockStackDef("coastStack", pc.X, pc.Y, 2.2f, new[] { 2.6f, 2.0f, 1.4f }), true);
        }

        // --- kizak yolu kenar bayraklari
        for (int i = 1; i < WD2.SledPath.Length - 1; i++)
        {
            var p = WD2.SledPath[i];
            foreach (var s in new[] { -4.5f, 4.5f })
            {
                float x = p.X + s * 0.7f, z = p.Y - s * 0.7f;
                AddMesh(Merge(Cyl("#e8e8e8", 0.04f, 0.05f, 1.4f, V(0, 0.7f, 0), null, 5), Box(i % 2 == 0 ? "#3fa7ff" : "#ff5a5a", 0.5f, 0.3f, 0.02f, V(0.25f, 1.25f, 0))),
                    new Vector3(x, H(x, z), z));
            }
        }

        BuildIsle2Vegetation();

        // --- Kar Adasi bulutlari
        CloudSets.Add(new Clouds(Scene, L2.C, 14, 991, 120, 200));
    }

    private void PlaceRockStack(RockStackDef r, bool snowy)
    {
        if (!snowy) { PlaceRockStack(r); return; }
        var stack = r.Stack;
        float y = GroundY(r.X, r.Z) - stack[0] * 0.25f;
        for (int i = 0; i < stack.Length; i++)
        {
            var g = Nature.SnowRock((uint)Math.Floor(r.X * 7 + r.Z * 3 + i));
            float s = stack[i];
            g.Scale(s * 1.1f, s, s * 1.1f);
            float ox = i > 0 ? 0.3f : 0;
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

    /// <summary>
    /// Tirmanma duvarini yerlestir. Rasathane ucurumunda duvarin yeri araziden olculur: zirveden
    /// disari dogru isin atilir, ucurumun eteği bulunur, duvar yuzu ona yaslanir.
    /// </summary>
    private void PlaceClimbWall(ClimbWallDef w)
    {
        float x = w.X, z = w.Z, yaw = w.Yaw, h = w.H;
        float baseY = float.IsNaN(w.BaseY) ? Height(x, z) : float.NaN;
        if (w.Id.StartsWith("cliff"))
        {
            var dir = Vector2.Normalize(new Vector2(L2.Harbor.X - L2.Summit.X, L2.Harbor.Y - L2.Summit.Y));
            // A solda, B sagda (ucurum boyunca kaydir)
            var side = new Vector2(-dir.Y, dir.X);
            float lateral = w.Id == "cliffA" ? -2.4f : 2.4f;
            float top = Height(L2.Summit.X, L2.Summit.Y);
            float rBase = L2.SummitR + 6;
            for (float r = L2.SummitR - 4; r < L2.SummitR + 8; r += 0.1f)
            {
                var p = L2.Summit + dir * r + side * lateral;
                if (Height(p.X, p.Y) < top - L2.SummitCliff + 0.6f) { rBase = r; break; }
            }
            var pos = L2.Summit + dir * (rBase + 0.25f) + side * lateral;
            x = pos.X; z = pos.Y;
            yaw = MathF.Atan2(dir.X, dir.Y);
            float ground = Height(x, z);
            float cliffTop = top;
            if (w.Id == "cliffA")
            {
                baseY = ground;
                h = (cliffTop - ground) * 0.55f;
            }
            else
            {
                baseY = ground + (cliffTop - ground) * 0.55f - 0.6f;
                h = cliffTop - baseY + 0.35f;
                Anchors["summitLedge"] = new Vector3(x, cliffTop + 1.1f, z) - new Vector3(dir.X, 0, dir.Y) * 3.5f;
            }
        }
        else if (w.Id == "peakVine")
        {
            // Ada 1 zirvesinin ikinci basamagi
            var c = Isle1Shape.L.Peak;
            var dir = Vector2.Normalize(new Vector2(0.55f, 0.83f));
            float top = Height(c.X, c.Y);
            float rBase = Isle1Shape.L.PeakMesa2 + 4;
            for (float r = Isle1Shape.L.PeakMesa2 - 4; r < Isle1Shape.L.PeakMesa2 + 8; r += 0.1f)
            {
                var p = c + dir * r;
                if (Height(p.X, p.Y) < top - Isle1Shape.L.PeakCliff2 + 0.5f) { rBase = r; break; }
            }
            var pos = c + dir * (rBase + 0.25f);
            x = pos.X; z = pos.Y;
            yaw = MathF.Atan2(dir.X, dir.Y);
            baseY = Height(x, z);
            h = top - baseY + 0.35f;
        }
        else if (float.IsNaN(baseY)) baseY = w.BaseY;
        var res = Buildings2.VineWall(w.W, h, (uint)(w.Id.GetHashCode() & 0xffff), w.Frosty);
        Place(res, x, baseY, z, yaw, "climb_" + w.Id, "climb");
        ClimbWalls.Add((w.Id, res, w with { X = x, Z = z, Yaw = yaw, H = h, BaseY = baseY }));
    }

    private void BuildIsle2Vegetation()
    {
        var T = Isle2;
        var rng = new Rng(4242);
        float R() => (float)rng.NextD();
        var pines = new List<(float x, float y, float z, float s, float yaw)>();
        var rounds = new List<(float x, float y, float z, float s, float yaw)>();
        float x0 = T.MinX + 8, z0 = T.MinZ + 8;
        for (float gx = x0; gx < T.MinX + T.Size - 8; gx += 3.4f)
        {
            for (float gz = z0; gz < T.MinZ + T.Size - 8; gz += 3.4f)
            {
                float x = gx + (R() - 0.5f) * 3, z = gz + (R() - 0.5f) * 3;
                float h = Height(x, z);
                if (h < WaterLevel(x, z) + 1.6f) continue;
                if (Normal(x, z).Y < 0.84f) continue;
                if (PathDistance(x, z) < 3.4f) continue;
                if (IsExcluded(x, z, 2.5f)) continue;
                if (MathX.Hypot(x - L2.Lake.X, z - L2.Lake.Y) < L2.LakeR + 6) continue;
                if (Geo2D.DistSeg(new Vector2(x, z), L2.SledTop, L2.SledBottom) < 9) continue;
                float dS = MathX.Hypot(x - L2.Summit.X, z - L2.Summit.Y);
                if (dS < L2.SummitR + 8) continue;
                float dM = MathX.Hypot(x - L2.SlopeMid.X, z - L2.SlopeMid.Y);
                float dens = 0.05f + MathX.Smoothstep(80, 30, dM) * 0.35f + MathX.Smoothstep(70, 40, dS) * 0.15f;
                if (MathX.Hypot(x - L2.Harbor.X, z - L2.Harbor.Y) < 46) dens *= 0.15f;
                if (MathX.Hypot(x - L2.Spring.X, z - L2.Spring.Y) < 30) dens *= 0.4f;
                if (R() > dens) continue;
                float s = 0.8f + R() * 0.7f, yaw = R() * MathX.TwoPi;
                var list = R() < 0.82f ? pines : rounds;
                list.Add((x, h - 0.1f, z, s, yaw));
                float rr = list == pines ? 0.28f : 0.32f;
                Physics.Add(ShapeDef.Cyl(rr * s, 1.6f * s, V(0, 1.6f * s, 0)), new Vector3(x, h - 0.1f, z));
            }
        }
        var treeMat = new Material { Sway = 1 };
        var pineGeos = new[] { Nature.PineTree(11, true), Nature.PineTree(12, true) };
        for (int vi = 0; vi < 2; vi++)
        {
            var b = Batch(MeshData.From(pineGeos[vi], true), treeMat);
            foreach (var t in pines.Where((_, i) => i % 2 == vi)) b.Add(MathX.TRS(new Vector3(t.x, t.y, t.z), t.yaw, t.s), new Vector4(1, 1, 1, 1));
        }
        var roundGeo = Nature.RoundTree(13);
        Buildings2.SnowCap(roundGeo, 0.45f);
        var rb = Batch(MeshData.From(roundGeo, true), treeMat);
        foreach (var t in rounds) rb.Add(MathX.TRS(new Vector3(t.x, t.y, t.z), t.yaw, t.s));
        TreeCount += pines.Count + rounds.Count;

        // kayalar + buz kutleleri
        var rocks = new List<(float x, float z, float y, float s, float yaw, int v)>();
        for (float gx = x0; gx < T.MinX + T.Size - 8; gx += 8)
        {
            for (float gz = z0; gz < T.MinZ + T.Size - 8; gz += 8)
            {
                float x = gx + (R() - 0.5f) * 7, z = gz + (R() - 0.5f) * 7;
                float h = Height(x, z);
                if (h < -1.5f) continue;
                var n = Normal(x, z);
                if (PathDistance(x, z) < 2.5f || IsExcluded(x, z, 2)) continue;
                if (MathX.Hypot(x - L2.Lake.X, z - L2.Lake.Y) < L2.LakeR + 1) continue;
                if (Geo2D.DistSeg(new Vector2(x, z), L2.SledTop, L2.SledBottom) < 8) continue;
                float p = 0.04f;
                if (n.Y < 0.86f && n.Y > 0.55f) p += 0.25f;
                if (h > -1.5f && h < 2.2f) p += 0.08f;
                if (R() > p) continue;
                float s = 0.4f + R() * 1.4f, yaw = R() * 6;
                int v = R() < 0.25f ? 2 : (int)(R() * 2);
                rocks.Add((x, z, h - 0.25f, s, yaw, v));
            }
        }
        var rockGeos = new[] { Nature.SnowRock(21), Nature.SnowRock(22), Nature.IceChunk(23) };
        for (int vi = 0; vi < 3; vi++)
        {
            var items = rocks.Where(r => r.v == vi).ToList();
            if (items.Count == 0) continue;
            var b = Batch(MeshData.From(rockGeos[vi], true), vi == 2 ? new Material { Emissive = MathX.Hex("#7fd8ff"), EmissiveIntensity = 0.08f } : Material.Std());
            var hull = ConvexHull.Build(rockGeos[vi].P);
            foreach (var r in items)
            {
                b.Add(MathX.TRS(new Vector3(r.x, r.y, r.z), r.yaw, r.s));
                if (r.s > 0.7f) Physics.Add(ShapeDef.FromHull(hull.Scaled(r.s), default, V(0, r.yaw, 0)), new Vector3(r.x, r.y, r.z));
            }
        }

        // karli calilar
        var bushes = new List<(float x, float y, float z, float s, float yaw)>();
        for (float gx = x0; gx < T.MinX + T.Size - 8; gx += 2.2f)
        {
            for (float gz = z0; gz < T.MinZ + T.Size - 8; gz += 2.2f)
            {
                float x = gx + (R() - 0.5f) * 2, z = gz + (R() - 0.5f) * 2;
                float h = Height(x, z);
                if (h < WaterLevel(x, z) + 1.2f || Normal(x, z).Y < 0.84f || PathDistance(x, z) < 2 || IsExcluded(x, z, 0.5f)) continue;
                float dM = MathX.Hypot(x - L2.SlopeMid.X, z - L2.SlopeMid.Y);
                if (R() > 0.006f + MathX.Smoothstep(90, 30, dM) * 0.02f + Tint.Gauss(x, z, L2.Spring, 26) * 0.05f) continue;
                bushes.Add((x, h - 0.1f, z, 0.6f + R() * 0.7f, R() * 6));
            }
        }
        var bb = Batch(MeshData.From(Nature.Bush(31, true), true), new Material { Sway = 1 });
        foreach (var o in bushes) bb.Add(MathX.TRS(new Vector3(o.x, o.y, o.z), o.yaw, o.s));
    }

    /// <summary>Kar Adasi efektleri: kar yagisi, sicak su buhari.</summary>
    public void UpdateIsle2Fx(float dt, Vector3 camPos, Vector3 playerPos, float night)
    {
        float snow = Snowiness(playerPos);
        if (snow > 0.05f)
        {
            _snowT -= dt;
            while (_snowT <= 0)
            {
                _snowT += 0.03f / snow;
                float a = (float)_fx.NextDouble() * MathX.TwoPi, r = 2 + (float)_fx.NextDouble() * 22;
                var p = camPos + new Vector3(MathF.Cos(a) * r, 6 + (float)_fx.NextDouble() * 8, MathF.Sin(a) * r);
                Env.Normal.Emit(Emit.At(p, 1, "#ffffff", "#eef6ff").V(0.3f, -1.4f, 0.1f, 0.25f).L(7).S(0.09f + (float)_fx.NextDouble() * 0.06f).A(0.85f).W(1.2f));
            }
        }
        _steamT -= dt;
        if (_steamT <= 0)
        {
            _steamT = 0.12f;
            foreach (var s in SteamSources)
            {
                if (Vector3.DistanceSquared(s, playerPos) > 90 * 90) continue;
                Env.Normal.Emit(Emit.At(s + new Vector3(((float)_fx.NextDouble() - 0.5f) * 5, 0, ((float)_fx.NextDouble() - 0.5f) * 5), 1, "#ffffff", "#f2f6fa")
                    .V(0.1f, 0.8f, 0.05f, 0.15f).L(3.2f).S(0.9f, 2.6f).A(0.32f).D(0.3f));
            }
        }
    }
}

public static class Geo2D
{
    public static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }
}
