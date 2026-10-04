using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;
using static Starfall.Render.Geo;

namespace Starfall.Gameplay;

/// <summary>
/// Mina'nin evi: koydeki kulubenin kapisindan girilen ic mekan (dunyanin disinda bir oda).
/// Zemin 10x8 hucrelik izgara; mobilyalar yerlestirilir, dondurulur, kaldirilir. Kilim
/// katmani ayridir (mobilyanin altina serilebilir); duvar esyalari yalnizca arka duvara asilir.
/// </summary>
public sealed class House
{
    private readonly Game _g;
    public static Vector3 O => WD2.HouseOrigin;
    public const int CX = WD2.HouseCellsX, CZ = WD2.HouseCellsZ;
    public const float Cell = WD2.HouseCell;
    public static float HalfW => CX * Cell / 2;
    public static float HalfD => CZ * Cell / 2;
    private readonly Node _root = new() { Name = "house" };
    private readonly List<(PlacedFurniture P, Node N, PointLight? L)> _items = new();
    private readonly List<PointLight> _roomLights = new();
    /// <summary>Kesit gorunumu: kameranin tarafindaki duvarlar ve (ustten bakinca) tavan gizlenir. Sira: -Z, +Z, -X, +X.</summary>
    private readonly Node[] _walls = new Node[4];
    private Node? _ceiling;
    public const float RoomHeight = 3.0f;
    public bool Inside => _g.InsideHouse;
    public Node? Ghost;

    public static readonly string[] Starter = { "bed", "table", "chair", "rug", "plant" };

    public House(Game g)
    {
        _g = g;
        BuildRoom();
    }

    public static Vector3 CellPos(float cx, float cz) => O + new Vector3((cx + 0.5f) * Cell - HalfW, 0, (cz + 0.5f) * Cell - HalfD);

    public Vector3 DoorInside => O + new Vector3(0, 0.05f, HalfD - 0.9f);
    public Vector3 MirrorPos => O + new Vector3(-HalfW + 0.6f, 0, -0.6f);
    public Vector3 BoardPos => O + new Vector3(HalfW - 0.6f, 0, 0.8f);

    private void BuildRoom()
    {
        var res = new BuildResult();
        float w = CX * Cell, d = CZ * Cell, h = RoomHeight;
        var parts = new List<Geo>();
        // zemin: tahta seritler
        int strips = 16;
        for (int i = 0; i < strips; i++)
            parts.Add(Box(i % 2 == 0 ? "#c9955f" : "#bb8752", w / strips, 0.1f, d, V(-w / 2 + (i + 0.5f) * w / strips, -0.05f, 0)));
        // kesit gorunumunde odanin cevresi: koyu, sakin bir zemin (deniz/gokyuzu gorunmesin)
        parts.Add(Box("#2c2738", 90, 0.1f, 90, V(0, -0.6f, 0)));
        parts.Add(Box("#8a5a3a", w + 0.4f, 0.5f, d + 0.4f, V(0, -0.35f, 0)));
        // duvarlar (ic yuz krem, alt lambri) + tavan: kameraya gore gizlenebilsin diye ayri dugumler
        var walls = new[] { (0f, -d / 2 - 0.1f, w + 0.4f, 0.2f), (0f, d / 2 + 0.1f, w + 0.4f, 0.2f), (-w / 2 - 0.1f, 0f, 0.2f, d), (w / 2 + 0.1f, 0f, 0.2f, d) };
        for (int wi = 0; wi < walls.Length; wi++)
        {
            var (x, z, ww, dd) = walls[wi];
            var wp = new List<Geo>
            {
                Box("#fff4e0", ww, h, dd, V(x, h / 2, z)),
                Box("#d9b98a", ww + 0.02f, 0.9f, dd + 0.02f, V(x, 0.45f, z)),
            };
            res.Colliders.Add(ShapeDef.Box(ww / 2, h / 2, dd / 2, V(x, h / 2, z)));
            var node = new Node();
            if (wi == 1)
            {
                // kapi (on duvar ortasi)
                wp.Add(Box(P.Door, 1.0f, 2.0f, 0.08f, V(0, 1.0f, d / 2 - 0.02f)));
                wp.Add(Sphere(P.Gold, 0.05f, V(0.35f, 1.0f, d / 2 - 0.08f), null, 6, 4));
            }
            if (wi >= 2)
            {
                // pencereler (yan duvarlar)
                float sx = wi == 2 ? -1 : 1;
                wp.Add(Box(P.Timber, 0.1f, 1.2f, 1.4f, V(sx * (w / 2 - 0.02f), 1.7f, -1.0f)));
                node.Add(new Node(MeshData.From(Box("#bfe6ff", 0.12f, 1.0f, 1.2f, V(sx * (w / 2 - 0.03f), 1.7f, -1.0f)), true),
                    new Material { Emissive = MathX.Hex("#bfe6ff"), EmissiveIntensity = 0.6f }));
            }
            node.Add(new Node(MeshData.From(Merge(wp), true), Material.Std()));
            res.Group.Add(node);
            _walls[wi] = node;
        }
        var ceil = new List<Geo> { Box("#e8d9c0", w + 0.4f, 0.12f, d + 0.4f, V(0, h + 0.06f, 0)) };
        for (int i = 0; i < 4; i++) ceil.Add(Box(P.Timber, w + 0.4f, 0.18f, 0.2f, V(0, h - 0.09f, -d / 2 + (i + 0.5f) * d / 4)));
        _ceiling = res.Add(ceil);
        // ayna (gardirop) + duzenleme panosu
        var m = MirrorPos - O;
        parts.Add(Box(P.WoodDark, 0.12f, 1.9f, 0.9f, V(-w / 2 + 0.07f, 1.1f, m.Z)));
        parts.Add(Box("#cfe8ff", 0.13f, 1.6f, 0.7f, V(-w / 2 + 0.08f, 1.15f, m.Z)));
        var b = BoardPos - O;
        parts.Add(Box("#b07a4f", 0.1f, 0.9f, 1.2f, V(w / 2 - 0.06f, 1.5f, b.Z)));
        parts.Add(Box("#d9b98a", 0.11f, 0.75f, 1.05f, V(w / 2 - 0.07f, 1.5f, b.Z)));
        foreach (var (py, pz, col) in new[] { (1.65f, -0.25f, "#ffffff"), (1.4f, 0.2f, "#ffd36b"), (1.6f, 0.3f, "#7fd0ff") })
            parts.Add(Box(col, 0.12f, 0.22f, 0.2f, V(w / 2 - 0.08f, py, b.Z + pz)));
        res.Add(parts);
        res.Colliders.Add(ShapeDef.Box(w / 2 + 0.3f, 0.1f, d / 2 + 0.3f, V(0, -0.1f, 0)));
        res.Colliders.Add(ShapeDef.Box(w / 2 + 0.3f, 0.1f, d / 2 + 0.3f, V(0, h + 0.1f, 0)));
        _g.World.Place(res, O.X, O.Y, O.Z, 0, "house");
        foreach (var lp in new[] { new Vector3(-1.6f, 2.5f, -0.5f), new Vector3(1.8f, 2.5f, 1.0f) })
        {
            var l = new PointLight { Position = O + lp, Color = MathX.Hex("#ffd9a0"), Intensity = 7, Distance = 9, Decay = 1.4f };
            _g.Env.Scene.Lights.Add(l);
            _roomLights.Add(l);
        }
        _g.Env.Scene.Add(_root);
    }

    // ------------------------------------------------------------------ izgara
    public static (int W, int D) Footprint(FurnitureDef f, int rot) => rot % 2 == 0 ? (f.W, f.D) : (f.D, f.W);

    public bool CanPlace(string id, int x, int z, int rot, PlacedFurniture? ignore = null)
    {
        var f = Furniture.Get(id);
        if (f == null) return false;
        if (f.Wall) rot = 0;
        var (w, d) = Footprint(f, rot);
        if (x < 0 || z < 0 || x + w > CX || z + d > CZ) return false;
        if (f.Wall && z != 0) return false;
        // kapinin onu bos kalsin
        if (!f.Rug && !f.Wall && z + d > CZ - 2 && x < CX / 2 + 1 && x + w > CX / 2 - 1) return false;
        foreach (var p in _g.Save!.House)
        {
            if (p == ignore) continue;
            var o = Furniture.Get(p.Id);
            if (o == null) continue;
            if (o.Rug != f.Rug || o.Wall != f.Wall) continue; // farkli katmanlar ust uste binebilir
            var (ow, od) = Footprint(o, o.Wall ? 0 : p.Rot);
            if (x < p.X + ow && x + w > p.X && z < p.Z + od && z + d > p.Z) return false;
        }
        return true;
    }

    public PlacedFurniture? At(int x, int z)
    {
        // once mobilya, sonra duvar, en son kilim
        PlacedFurniture? best = null;
        int bestRank = -1;
        foreach (var p in _g.Save!.House)
        {
            var f = Furniture.Get(p.Id);
            if (f == null) continue;
            var (w, d) = Footprint(f, f.Wall ? 0 : p.Rot);
            if (x >= p.X && x < p.X + w && z >= p.Z && z < p.Z + d)
            {
                int rank = f.Rug ? 0 : f.Wall ? 1 : 2;
                if (rank > bestRank) { bestRank = rank; best = p; }
            }
        }
        return best;
    }

    public int OwnedCount(string id) => _g.Save!.Furniture.Count(x => x == id);
    public int PlacedCount(string id) => _g.Save!.House.Count(x => x.Id == id);

    public bool Place(string id, int x, int z, int rot)
    {
        if (PlacedCount(id) >= OwnedCount(id) || !CanPlace(id, x, z, rot)) return false;
        var f = Furniture.Get(id)!;
        _g.Save!.House.Add(new PlacedFurniture { Id = id, X = x, Z = z, Rot = f.Wall ? 0 : rot });
        Rebuild();
        _g.Stats.Max("furniture_placed", _g.Save.House.Count);
        _g.Audio.Sfx("place");
        return true;
    }

    public void Remove(PlacedFurniture p)
    {
        _g.Save!.House.Remove(p);
        Rebuild();
        _g.Audio.Sfx("pickup");
    }

    public static Matrix4x4 ItemMatrix(FurnitureDef f, int x, int z, int rot)
    {
        if (f.Wall) rot = 0;
        var (w, d) = Footprint(f, rot);
        var c = CellPos(x + (w - 1) / 2f, z + (d - 1) / 2f);
        return Matrix4x4.CreateRotationY(rot * MathX.Pi / 2) * Matrix4x4.CreateTranslation(c);
    }

    /// <summary>Kayittaki yerlesimden dugumleri yeniden kur.</summary>
    public void Rebuild()
    {
        foreach (var (_, n, l) in _items)
        {
            _root.Remove(n);
            if (l != null) _g.Env.Scene.Lights.Remove(l);
        }
        _items.Clear();
        var s = _g.Save;
        if (s == null) return;
        foreach (var p in s.House)
        {
            var f = Furniture.Get(p.Id);
            if (f == null) continue;
            var node = new Node(Furniture.Mesh(p.Id), Material.Std()) { LocalOverride = ItemMatrix(f, p.X, p.Z, p.Rot) };
            _root.Add(node);
            PointLight? l = null;
            if (f.Light && _items.Count(i => i.L != null) < 3)
            {
                l = new PointLight { Position = ItemMatrix(f, p.X, p.Z, p.Rot).Translation + new Vector3(0, 1.2f, 0), Color = MathX.Hex("#ffcf8a"), Intensity = 4, Distance = 6, Decay = 1.6f };
                _g.Env.Scene.Lights.Add(l);
            }
            _items.Add((p, node, l));
        }
    }

    // ------------------------------------------------------------------ giris/cikis
    public void ApplySave(SaveData s) => Rebuild();

    /// <summary>Her karede: kamera odanin disindaysa o taraftaki duvari, ustundeyse tavani gizle.</summary>
    public void UpdateCutaway(Vector3 cam)
    {
        var c = cam - O;
        if (_ceiling != null) _ceiling.Visible = c.Y < RoomHeight - 0.05f;
        _walls[0].Visible = c.Z > -HalfD;
        _walls[1].Visible = c.Z < HalfD;
        _walls[2].Visible = c.X > -HalfW;
        _walls[3].Visible = c.X < HalfW;
    }

    private void GiveStarter()
    {
        var s = _g.Save!;
        if (s.Flag("houseStarter")) return;
        s.Flags["houseStarter"] = true;
        foreach (var id in Starter) s.Furniture.Add(id);
        // hazir yerlesim: yatak sol arka, kilim orta, masa+sandalye, bitki kosede
        s.House.Add(new PlacedFurniture { Id = "bed", X = 0, Z = 0, Rot = 0 });
        s.House.Add(new PlacedFurniture { Id = "rug", X = 4, Z = 3, Rot = 0 });
        s.House.Add(new PlacedFurniture { Id = "table", X = 4, Z = 3, Rot = 0 });
        s.House.Add(new PlacedFurniture { Id = "chair", X = 6, Z = 3, Rot = 3 });
        s.House.Add(new PlacedFurniture { Id = "plant", X = 9, Z = 0, Rot = 0 });
        _g.Stats.Max("furniture_placed", s.House.Count);
    }

    public void Enter()
    {
        _g.Transition(() =>
        {
            GiveStarter();
            Rebuild();
            _g.InsideHouse = true;
            _g.Player.Spawn(DoorInside.X, DoorInside.Y, DoorInside.Z - 0.6f, MathX.Pi);
            _g.CameraRig.Snap(_g.Player.Pos, 0);
            _g.CameraRig.TargetDistance = 4.6f;
            _g.CameraRig.Pitch = 0.62f;
            _g.Audio.Sfx("door");
            if (!_g.Flag("hintHouse")) { _g.SetFlag("hintHouse"); _g.After(0.8f, () => _g.Hint(T("hint.house"), 8)); }
        });
    }

    public void Exit()
    {
        _g.Transition(() =>
        {
            _g.InsideHouse = false;
            var door = _g.World.Anchor("hut.door");
            float yaw = WD.Village.Cottages[0].Yaw;
            _g.Player.Spawn(door.X + MathF.Sin(yaw) * 0.8f, door.Y + 0.1f, door.Z + MathF.Cos(yaw) * 0.8f, yaw);
            _g.CameraRig.Snap(_g.Player.Pos, yaw + MathX.Pi);
            _g.CameraRig.TargetDistance = 7;
            _g.Audio.Sfx("door");
            _g.Autosave();
        });
    }

    public Game.Interaction? Interaction()
    {
        var p = _g.Player.Pos;
        if (_g.InsideHouse)
        {
            if (Vector3.Distance(p, DoorInside) < 1.6f) return new Game.Interaction(T("prompt.exitHouse"), Exit);
            if (MathX.Hypot(p.X - MirrorPos.X, p.Z - MirrorPos.Z) < 1.5f) return new Game.Interaction(T("prompt.wardrobe"), () => _g.OpenScreen(new UI.WardrobeScreen(_g.Ui)));
            if (MathX.Hypot(p.X - BoardPos.X, p.Z - BoardPos.Z) < 1.6f) return new Game.Interaction(T("prompt.decorate"), () => _g.OpenScreen(new UI.HouseScreen(_g.Ui)));
            return null;
        }
        if (!_g.World.Anchors.TryGetValue("hut.door", out var door)) return null;
        if (MathX.Hypot(p.X - door.X, p.Z - door.Z) < 1.8f) return new Game.Interaction(T("prompt.enterHouse"), Enter);
        return null;
    }
}
