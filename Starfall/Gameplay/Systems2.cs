using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;
using static Starfall.Render.Geo;

namespace Starfall.Gameplay;

/// <summary>Tekne: iki iskeleden birinde bagli; kunduz yelkeni takinca Kar Adasi'na yelken acilir.</summary>
public sealed class Boats
{
    private readonly Game _g;
    public Node Node => _g.World.Boat;
    public bool AtIsle2;
    private float _t;
    private MeshData? _sailMesh;

    public Boats(Game g) => _g = g;

    public bool Usable => _g.Flag("sail");

    public Vector3 DockEnd(bool isle2) => _g.World.Anchor(isle2 ? "dock2.end" : "dock.end");

    /// <summary>Teknenin bagli oldugu yer: iskele ucunun yaninda.</summary>
    public (Vector3 Pos, float Yaw) Mooring(bool isle2)
    {
        if (!isle2) return (_g.World.BoatHome, 0.15f);
        var end = DockEnd(true);
        var dir = L2.DockDir;
        var side = new Vector2(-dir.Y, dir.X);
        var p = new Vector2(end.X, end.Z) + side * 2.9f - dir * 3.5f;
        return (new Vector3(p.X, 0.38f, p.Y), MathF.Atan2(dir.X, dir.Y));
    }

    public void ApplySave(SaveData s)
    {
        AtIsle2 = s.Flag("boatAtIsle2");
        if (s.Flag("sail")) RigSail();
        Moor(AtIsle2);
    }

    public void RigSail()
    {
        _sailMesh ??= MeshData.From(Buildings.RowboatGeo("#3f7fc4", true), true);
        var n = Node.Children.FirstOrDefault(c => c.Mesh != null);
        if (n != null) n.Mesh = _sailMesh;
    }

    public void Moor(bool isle2)
    {
        AtIsle2 = isle2;
        var (p, yaw) = Mooring(isle2);
        Node.Position = p;
        Node.Rotation = new Vector3(0, yaw, 0);
        _g.World.Anchors["boat"] = p + new Vector3(0, 1.22f, 0);
        _g.SetFlag("boatAtIsle2", isle2);
    }

    public void Update(float dt)
    {
        _t += dt;
        if (_g.Player.Boating) return;
        var (p, yaw) = Mooring(AtIsle2);
        Node.Position = p with { Y = 0.38f + MathF.Sin(_t * 1.3f) * 0.06f };
        Node.Rotation = new Vector3(0, yaw, MathF.Sin(_t * 1.1f) * 0.04f);
    }

    public Game.Interaction? Interaction()
    {
        var p = _g.Player;
        if (p.Boating)
        {
            foreach (var isle2 in new[] { false, true })
            {
                var end = DockEnd(isle2);
                if (MathX.Hypot(p.Pos.X - end.X, p.Pos.Z - end.Z) < 8)
                    return new Game.Interaction(T("prompt.boatOut"), () => Disembark(isle2));
            }
            return null;
        }
        if (!Usable) return null;
        var bp = Node.Position;
        if (MathX.Hypot(p.Pos.X - bp.X, p.Pos.Z - bp.Z) < 3.4f && MathF.Abs(p.Pos.Y - bp.Y) < 2.5f)
            return new Game.Interaction(T("prompt.boatIn"), Board);
        return null;
    }

    public void Board()
    {
        var (pos, yaw) = Mooring(AtIsle2);
        // iskeleden uzaga bak
        float outYaw = AtIsle2 ? MathF.Atan2(L2.DockDir.X, L2.DockDir.Y) : MathX.Pi * 0 + 0.15f;
        _g.Player.BoardBoat(Node, pos, AtIsle2 ? outYaw : 0.15f);
        _g.CameraRig.Snap(_g.Player.Pos, _g.Player.Yaw + MathX.Pi);
        if (!_g.Flag("hintBoat"))
        {
            _g.SetFlag("hintBoat");
            _g.Hint(T("hint.boat"), 7);
        }
    }

    public void Disembark(bool isle2)
    {
        var end = DockEnd(isle2);
        Moor(isle2);
        _g.Player.LeaveBoat(end + new Vector3(0, 0.1f, 0), _g.Player.Yaw + MathX.Pi);
        _g.CameraRig.Snap(_g.Player.Pos, _g.Player.Yaw + MathX.Pi);
        _g.Audio.Sfx("boatIn");
        _g.Autosave(true);
    }
}

/// <summary>Kazi: parlayan kazi noktalari (kabuk) + hazine haritalarinin gosterdigi sandiklar.</summary>
public sealed class Digging
{
    private readonly Game _g;
    private readonly Dictionary<string, Node> _mounds = new();
    private float _sparkT;
    public bool HasShovel => _g.Flag("shovel");

    public Digging(Game g)
    {
        _g = g;
        foreach (var d in WD2.DigSpots)
        {
            bool snowy = g.World.IsSnow(d.X, d.Z);
            float y = g.World.Height(d.X, d.Z);
            var n = new Node(MeshData.From(Buildings2.DigMound(snowy), true), Material.Std()) { Position = new Vector3(d.X, y, d.Z) };
            g.Env.Scene.Add(n);
            _mounds[d.Id] = n;
        }
    }

    public void ApplySave(SaveData s)
    {
        foreach (var (id, n) in _mounds) n.Visible = !s.Collected.Contains(id);
    }

    public int TreasuresFound() => WD2.Treasures.Count(t => _g.Save?.Collected.Contains(t.Id) == true);

    public bool HasMap(string mapId) => _g.Save?.Rewards.Contains(mapId) == true;

    public Game.Interaction? Interaction()
    {
        if (!HasShovel || _g.Save == null) return null;
        var p = _g.Player.Pos;
        foreach (var d in WD2.DigSpots)
        {
            if (_g.Save.Collected.Contains(d.Id)) continue;
            if (MathX.Hypot(p.X - d.X, p.Z - d.Z) < 1.8f) return new Game.Interaction(T("prompt.dig"), () => DigSpot(d));
        }
        foreach (var t in WD2.Treasures)
        {
            if (_g.Save.Collected.Contains(t.Id) || !HasMap(t.MapId)) continue;
            if (MathX.Hypot(p.X - t.X, p.Z - t.Z) < 2.4f) return new Game.Interaction(T("prompt.dig"), () => DigTreasure(t));
        }
        return null;
    }

    private void DigFx(Vector3 at)
    {
        bool snowy = _g.World.IsSnow(at.X, at.Z);
        _g.Audio.Sfx("dig");
        _g.Env.Normal.Emit(Emit.At(at, 18, snowy ? "#f4f8ff" : "#9a7a55", snowy ? "#dfe9f5" : "#7a5a3a").R(2.5f).G(-9).L(0.8f).S(0.18f, 0.08f).A(0.9f));
        _g.Player.Model.Jump();
    }

    public void DigSpot(DigDef d)
    {
        var s = _g.Save!;
        s.Collected.Add(d.Id);
        _mounds[d.Id].Visible = false;
        DigFx(new Vector3(d.X, _g.World.Height(d.X, d.Z), d.Z));
        s.Shells += d.Shells;
        _g.Hud.Toast(T("toast.digShells", ("n", d.Shells)));
        if (d.Id == "dig3") _g.Wardrobe.Give("hat_flowers", true);
        _g.Autosave();
    }

    public void DigTreasure(TreasureDef t)
    {
        var s = _g.Save!;
        s.Collected.Add(t.Id);
        var at = new Vector3(t.X, _g.World.Height(t.X, t.Z), t.Z);
        DigFx(at);
        // sandik cikar ve acilir
        var chest = new Node(CollectibleModels.Chest, Material.Std()) { Position = at + new Vector3(0, -0.6f, 0), Rotation = new Vector3(0, _g.Player.Yaw + MathX.Pi, 0) };
        _g.Env.Scene.Add(chest);
        float k = 0;
        _g.World.Animated.Add((_, dt) =>
        {
            k += dt;
            chest.Position = at + new Vector3(0, MathF.Min(0, -0.6f + k * 1.2f), 0);
            if (k > 6) { chest.Parent?.Remove(chest); return true; }
            return false;
        });
        _g.After(0.6f, () =>
        {
            _g.Audio.Sfx("chest");
            _g.Env.Additive.Emit(Emit.At(at + new Vector3(0, 0.6f, 0), 40, "#ffe680", "#fff6b0", "#ffffff").R(4).G(-3).L(1.2f).S(0.3f, 0.05f));
            foreach (var item in t.Contents) _g.GiveItem(item);
            _g.Stats.Max("treasures_max", TreasuresFound());
            _g.Hud.Toast(T("toast.treasure", ("n", TreasuresFound()), ("total", WD2.Treasures.Length)));
            _g.Progress.RefreshStats();
            _g.Autosave();
        });
    }

    public void Update(float dt)
    {
        _sparkT -= dt;
        if (_sparkT > 0 || _g.Save == null) return;
        _sparkT = 0.3f;
        var p = _g.Player.Pos;
        foreach (var d in WD2.DigSpots)
        {
            if (_g.Save.Collected.Contains(d.Id)) continue;
            var at = new Vector3(d.X, _g.World.Height(d.X, d.Z) + 0.2f, d.Z);
            if (Vector3.DistanceSquared(at, p) > 40 * 40) continue;
            _g.Env.Additive.Emit(Emit.At(at, 1, "#fff6b0", "#ffffff").Sp(0.4f, 0.1f, 0.4f).V(0, 0.8f, 0, 0.2f).L(0.9f).S(0.14f).A(0.9f));
        }
    }

    /// <summary>Hazine haritasi karti: hedefin cevresi + X isareti (gunlukte gosterilir).</summary>
    public static CpuImage MapCard(GameWorld w, TreasureDef t)
    {
        var terr = w.Terrains.First(x => x.Id == t.Island);
        var full = UI.MapImage.For(w, terr);
        const int S = 192;
        var img = new CpuImage(S, S);
        float span = 90; // metre
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float wx = t.X - span / 2 + (x + 0.5f) / S * span, wz = t.Z - span / 2 + (y + 0.5f) / S * span;
            int px = Math.Clamp((int)((wx - terr.MinX) / terr.Size * full.Width), 0, full.Width - 1);
            int py = Math.Clamp((int)((wz - terr.MinZ) / terr.Size * full.Height), 0, full.Height - 1);
            int o = (py * full.Width + px) * 4, d = (y * S + x) * 4;
            // eski kagit tonu (sepya)
            float r = full.Rgba[o], g = full.Rgba[o + 1], b = full.Rgba[o + 2];
            float l = (r * 0.3f + g * 0.55f + b * 0.15f) / 255f;
            img.Rgba[d] = (byte)(MathX.Lerp(170, 245, l));
            img.Rgba[d + 1] = (byte)(MathX.Lerp(130, 225, l));
            img.Rgba[d + 2] = (byte)(MathX.Lerp(80, 180, l));
            img.Rgba[d + 3] = 255;
        }
        var red = MathX.HexSrgb("#c9443d");
        var c = new Vector3(red.X, red.Y, red.Z);
        for (int i = -14; i <= 14; i++)
        for (int k = -2; k <= 2; k++)
        {
            img.Blend(S / 2 + i + k, S / 2 + i, c, 1);
            img.Blend(S / 2 + i + k, S / 2 - i, c, 1);
        }
        return img;
    }
}

/// <summary>Kedi Foto'nun gorevi: belirli konulari fotografla (kadrajda ve yakinda olmali).</summary>
public sealed class PhotoQuest
{
    private readonly Game _g;
    public PhotoQuest(Game g) => _g = g;

    public int Count => _g.Save?.Photos.Count(id => WD2.PhotoSubjects.Any(s => s.Id == id)) ?? 0;

    /// <summary>Fotograf cekilince: kadrajdaki konulari kaydet.</summary>
    public void OnPhoto()
    {
        var s = _g.Save;
        if (s == null) return;
        var cam = _g.Env.Camera;
        cam.Aspect = cam.Aspect <= 0 ? 16 / 9f : cam.Aspect;
        cam.Update();
        var vp = cam.View * cam.Proj;
        var found = new List<string>();
        foreach (var sub in WD2.PhotoSubjects)
        {
            Vector3 p;
            try { p = sub.Pos(_g.World); } catch { continue; }
            float d = Vector3.Distance(p, cam.Position);
            if (d > sub.MaxDist) continue;
            var clip = Vector4.Transform(new Vector4(p, 1), vp);
            if (clip.W <= 0) continue;
            float nx = clip.X / clip.W, ny = clip.Y / clip.W;
            if (MathF.Abs(nx) > 0.8f || MathF.Abs(ny) > 0.85f) continue;
            if (!s.Photos.Contains(sub.Id)) { s.Photos.Add(sub.Id); found.Add(sub.Id); }
        }
        if (found.Count > 0 && s.Quest("cat") == "active")
            _g.After(0.6f, () => _g.Hud.Toast(T("toast.photoSubject", ("name", T($"photo.subject.{found[0]}")), ("n", Count), ("total", WD2.PhotoSubjects.Length))));
        _g.Stats.Max("photo_subjects", Count);
    }
}

/// <summary>
/// Rasathane finali: kristaller teleskoba akar, kubbe doner, kuzey isiklari parlar, gokyuzunde
/// tilki takimyildizi belirir; ardindan jenerik.
/// </summary>
public sealed class Finale2
{
    private readonly Game _g;
    public bool Active;
    private float _t;
    public int Stage;
    private Vector3 _scope;
    private readonly Random _r = new(53);
    private bool _title;
    private readonly List<Vector3> _stars = new();

    public Finale2(Game g) => _g = g;

    public void Start()
    {
        Active = true;
        _t = 0;
        Stage = 0;
        _title = false;
        _scope = _g.World.Anchor("observatory.scope");
        _g.SetState(GameState.Cutscene);
        _g.Player.Frozen = true;
        _g.Audio.Music.SetMood("finale2");
        // tilki takimyildizi: zirvenin uzerinde, kuzeye dogru gokyuzunde
        _stars.Clear();
        var c = _scope + new Vector3(-30, 110, -160);
        Vector2[] fox = { new(-40, 0), new(-25, 18), new(-10, 30), new(0, 48), new(8, 30), new(22, 22), new(40, 26), new(52, 10), new(36, -6), new(10, -10), new(-18, -8) };
        foreach (var p in fox) _stars.Add(c + new Vector3(p.X, p.Y, 0) * 1.2f);
    }

    public void Update(float dt)
    {
        if (!Active) return;
        _t += dt;
        var post = _g.Env.Post;
        var dome = _g.World.Observatory.Sails;
        var scope = _g.World.Observatory.Flag;
        if (Stage == 0)
        {
            post.Fade = MathF.Min(1, _t / 0.8f);
            if (_t >= 0.8f) { Stage = 1; _g.SetHour(22.5f); _g.Hud.Visible = false; }
            return;
        }
        float ang = 2.2f + (_t - 0.8f) * 0.12f;
        var camPos = _scope + new Vector3(MathF.Sin(ang) * 24, 4, MathF.Cos(ang) * 24);
        _g.CameraRig.Override = new CamOverride(camPos, _scope + new Vector3(0, 6 + MathF.Min(30, (_t - 5) * 4) * (_t > 5 ? 1 : 0), 0), 4);
        if (Stage == 1)
        {
            post.Fade = MathF.Max(0, 1 - (_t - 0.8f) / 0.8f);
            if (_r.NextDouble() < dt * 30)
            {
                var from = _g.Player.Pos + new Vector3(0, 1, 0);
                var v = (_scope - from) / 1.6f;
                _g.Env.Additive.Emit(Emit.At(from, 1, "#7dffd8", "#b18cff", "#8fe9ff").Sp(0.3f).V(v.X, v.Y, v.Z).L(1.6f).S(0.45f, 0.2f).A(1));
            }
            if (dome != null) dome.Rotation += new Vector3(0, dt * 0.5f, 0);
            if (_t > 4.5f) { Stage = 2; _g.Audio.Sfx("telescope"); }
            return;
        }
        if (Stage == 2)
        {
            float k = MathF.Min(1, (_t - 4.5f) / 3);
            if (scope != null) scope.Rotation = new Vector3(-0.7f - k * 0.45f, 0, 0);
            _g.Env.Sky.AuroraBoost = k * 1.4f;
            // takimyildizi: yildizlar birer birer yanar, aralarina cizgi parcaciklari
            int lit = (int)MathF.Min(_stars.Count, (_t - 5.5f) / 0.5f);
            for (int i = 0; i < lit; i++)
            {
                if (_r.NextDouble() < dt * 6)
                    _g.Env.Additive.Emit(Emit.At(_stars[i], 1, "#ffffff", "#e8fbff").L(0.6f).S(3.2f, 2.4f).A(1));
                if (i > 0 && _r.NextDouble() < dt * 12)
                {
                    float f = (float)_r.NextDouble();
                    _g.Env.Additive.Emit(Emit.At(Vector3.Lerp(_stars[i - 1], _stars[i], f), 1, "#9fe9ff").L(0.5f).S(1.2f, 0.8f).A(0.7f));
                }
            }
            if (_t > 9 && !_title)
            {
                _title = true;
                _g.Hud.TitleCard(T("finale2.title"), T("finale2.subtitle"));
                _g.Audio.Sfx("aurora");
            }
            if (_r.NextDouble() < dt * 1.5f && _t > 8 && _t < 17) _g.Finale.Firework(_scope + new Vector3(0, -10, 0));
            if (_t > 18) { Stage = 3; }
            return;
        }
        if (Stage == 3)
        {
            post.Fade = MathF.Min(1, (_t - 18) / 1.0f);
            if (_t >= 19)
            {
                Stage = 4;
                _g.Hud.TitleCard(null, null);
                _g.Credits.Show(End);
            }
        }
    }

    private void End()
    {
        var s = _g.Save!;
        Active = false;
        _g.Env.Sky.AuroraBoost = 0;
        _g.CameraRig.Override = null;
        s.Flags["finale2"] = true;
        s.Finale2Time ??= s.PlayTime;
        _g.Stats.Max("finale2", 1);
        _g.Wardrobe.Give("hat_party", true);
        _g.Wardrobe.Give("back_balloon", true);
        _g.GiveItem("furn:starlamp");
        _g.Player.Frozen = false;
        _g.Hud.Visible = true;
        _g.Audio.Music.SetMood("auto");
        _g.SetState(GameState.Playing);
        _g.Progress.RefreshStats();
        _g.Autosave();
        _g.FadeIn(0.5f);
        _g.CameraRig.Snap(_g.Player.Pos, _g.Player.Yaw + MathX.Pi);
    }
}
