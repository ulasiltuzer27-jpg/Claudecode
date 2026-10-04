using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;
using static Starfall.Render.Geo;

namespace Starfall.Gameplay;

/// <summary>
/// Balik tutma: olta at -> bekle -> vurdu! -> makara mini oyunu. Makara: dikey cubukta balik
/// kacar, oyuncu tusu basili tutarak yesil bolgeyi yukari iter; balik bolgenin icindeyken
/// ilerleme dolar. Kar Adasi'nda donmus golde buz deliginden de tutulur ("ice" suyu).
/// </summary>
public sealed class Fishing
{
    private readonly Game _g;
    public string State = "idle";
    private float _timer;
    public float Progress, Zone, ZoneVel, FishY, FishVel, FishTarget, ZoneH;
    public bool Inside;
    public string Hint = "";
    public FishDef? Fish;
    public (Vector3 Pos, string Water)? Spot;
    private readonly Node _bobber, _line, _rod;
    private readonly Random _r = new(23);
    private float _tt;

    public Fishing(Game g)
    {
        _g = g;
        _bobber = new Node(MeshData.From(Merge(
            Sphere("#ff4a4a", 0.09f, null, null, 10, 8),
            Part(SphereGeo(0.091f, 10, 4, 0, MathX.TwoPi, 0, MathX.Pi / 2), "#ffffff")), false), new Material()) { Visible = false };
        g.Env.Scene.Add(_bobber);
        _line = new Node(MeshData.From(BoxGeo(1, 1, 1).Color("#f5f5f5"), true), new Material { Unlit = true, CastShadow = false, Opacity = 0.8f, Blend = Blend.Alpha }) { Visible = false };
        g.Env.Scene.Add(_line);
        _rod = new Node(MeshData.From(CylinderGeo(0.012f, 0.02f, 1.3f, 5).Color("#8a5a3b").Translate(0, 0.65f, 0), false), new Material())
        {
            Position = new Vector3(0.15f, 0.1f, 0.25f), Rotation = new Vector3(0.9f, 0, 0), Visible = false,
        };
        g.Player.Model.Body.Add(_rod);
    }

    /// <summary>Oyuncunun onunde balik tutulabilir su var mi?</summary>
    public (Vector3 Pos, string Water)? WaterAhead()
    {
        var p = _g.Player.Pos;
        var W = _g.World;
        var dir = new Vector3(MathF.Sin(_g.Player.Yaw), 0, MathF.Cos(_g.Player.Yaw));
        foreach (var d in new[] { 2.5f, 3.5f, 4.5f })
        {
            float x = p.X + dir.X * d, z = p.Z + dir.Z * d;
            float wl = W.WaterLevel(x, z);
            if (W.IceHoleNear(x, z, out var hole) && p.Y >= wl - 0.3f) return (new Vector3(hole.X, wl + 0.02f, hole.Y), "ice");
            if (W.IsFrozenWater(x, z)) continue;
            if (W.Height(x, z) < wl - 0.5f && p.Y >= wl - 0.2f) return (new Vector3(x, wl, z), W.IsLake(x, z) ? "lake" : "sea");
        }
        return null;
    }

    public bool CanFish() => _g.Save?.Flag("rod") == true && _g.Player.Grounded && !_g.Player.Swimming && !_g.Player.Boating && WaterAhead() != null;

    public bool Start()
    {
        var spot = WaterAhead();
        if (spot == null) return false;
        Spot = spot;
        State = "wait";
        _timer = 2 + (float)_r.NextDouble() * 4;
        _g.Player.Frozen = true;
        _g.Player.Vel = Vector3.Zero;
        _bobber.Position = spot.Value.Pos;
        _bobber.Visible = _line.Visible = _rod.Visible = true;
        _g.Audio.Sfx("cast");
        Hint = T("fish.waiting", ("key", _g.Input.Glyph("back")));
        return true;
    }

    private FishDef PickFish()
    {
        bool night = _g.Env.Sky.Night > 0.5f;
        string water = Spot!.Value.Water;
        var valid = _g.World.AllFish.Where(f => (f.Water == "any" && water != "ice" || f.Water == water)
            && (f.Time == "any" || (f.Time == "night" && night) || (f.Time == "day" && !night))).ToList();
        if (valid.Count == 0) valid = _g.World.AllFish.Where(f => f.Water == water).ToList();
        float total = valid.Sum(f => f.Weight);
        float r = (float)_r.NextDouble() * total;
        foreach (var f in valid)
        {
            r -= f.Weight;
            if (r <= 0) return f;
        }
        return valid[0];
    }

    public void Stop()
    {
        State = "idle";
        _bobber.Visible = _line.Visible = _rod.Visible = false;
        _g.Player.Frozen = false;
    }

    public void Update(float dt, Input input)
    {
        if (State == "idle") return;
        _tt += dt;
        // olta ipi: uc -> samandira arasina gerilmis ince kutu
        _g.Player.Model.Root.UpdateWorld(Matrix4x4.Identity);
        var tip = _rod.LocalToWorld(new Vector3(0, 1.3f, 0));
        var bp = _bobber.Position;
        var d = bp - tip;
        float len = d.Length();
        if (len > 1e-3f)
        {
            var z = d / len;
            var x = Vector3.Normalize(Vector3.Cross(MathF.Abs(z.Y) > 0.95f ? Vector3.UnitX : Vector3.UnitY, z));
            var y = Vector3.Cross(z, x);
            var m = new Matrix4x4(x.X * 0.008f, x.Y * 0.008f, x.Z * 0.008f, 0, y.X * 0.008f, y.Y * 0.008f, y.Z * 0.008f, 0, z.X * len, z.Y * len, z.Z * len, 0, 0, 0, 0, 1);
            m.Translation = (tip + bp) * 0.5f;
            _line.LocalOverride = m;
        }

        if (input.Pressed("back") || input.Pressed("jump"))
        {
            Hint = "";
            Stop();
            return;
        }
        var spot = Spot!.Value;
        if (State == "wait")
        {
            _bobber.Position = spot.Pos with { Y = spot.Pos.Y + MathF.Sin(_tt * 2.5f) * 0.03f };
            _timer -= dt;
            if (_timer <= 0)
            {
                State = "bite";
                _timer = 1.0f;
                Fish = PickFish();
                _g.Audio.Sfx("bite");
                input.Rumble(0.6f, 200);
                Hint = T("fish.bite", ("key", _g.Input.Glyph("interact")));
                _g.Env.Normal.Emit(Emit.At(spot.Pos, 10, "#ffffff").R(1.5f).G(-6).L(0.6f).S(0.15f).A(0.8f));
            }
        }
        else if (State == "bite")
        {
            _bobber.Position = spot.Pos with { Y = spot.Pos.Y - 0.12f + MathF.Sin(_tt * 30) * 0.04f };
            _timer -= dt;
            if (input.Pressed("interact"))
            {
                State = "reel";
                Progress = 0.3f;
                Zone = 0.35f;
                ZoneVel = 0;
                FishY = 0.5f;
                FishVel = 0;
                FishTarget = 0.5f;
                Hint = T("fish.reel", ("key", _g.Input.Glyph("interact")));
            }
            else if (_timer <= 0)
            {
                Hint = T("fish.escaped");
                _g.Audio.Sfx("lose");
                State = "wait";
                _timer = 2.5f + (float)_r.NextDouble() * 4;
            }
        }
        else if (State == "reel")
        {
            float diff = Fish!.Diff;
            bool holding = input.Held("interact");
            ZoneVel += (holding ? 2.6f : -2.2f) * dt;
            ZoneVel *= 0.92f;
            Zone = Math.Clamp(Zone + ZoneVel * dt * 1.6f, 0, 0.72f);
            if (Zone == 0 || Zone == 0.72f) ZoneVel = 0;
            // balik: rastgele hedeflere kacar, zorluk hizi belirler
            if (_r.NextDouble() < dt * (0.8f + diff * 2.2f)) FishTarget = (float)_r.NextDouble();
            FishVel += (FishTarget - FishY) * dt * (3 + diff * 9);
            FishVel *= 0.9f;
            FishY = Math.Clamp(FishY + FishVel * dt * 3, 0, 1);
            ZoneH = 0.28f - diff * 0.06f;
            Inside = FishY >= Zone && FishY <= Zone + ZoneH;
            Progress += (Inside ? 0.32f : -0.22f * (0.6f + diff)) * dt;
            _bobber.Position = spot.Pos with { X = spot.Pos.X + MathF.Sin(_tt * 9) * 0.15f };
            if (Inside && _r.NextDouble() < dt * 8) _g.Audio.Sfx("reel");
            if (Progress >= 1) Catch();
            else if (Progress <= 0)
            {
                Hint = T("fish.lost");
                _g.Audio.Sfx("lose");
                State = "wait";
                _timer = 3 + (float)_r.NextDouble() * 3;
            }
        }
    }

    public void Catch()
    {
        var f = Fish!;
        var s = _g.Save!;
        int size = (int)MathF.Round(f.MinSize + (float)_r.NextDouble() * (f.MaxSize - f.MinSize));
        bool isNew = !(s.Fish.TryGetValue(f.Id, out var n) && n > 0);
        s.Fish[f.Id] = (s.Fish.TryGetValue(f.Id, out var c) ? c : 0) + 1;
        if (!s.FishBest.TryGetValue(f.Id, out var best) || size > best) s.FishBest[f.Id] = size;
        _g.Stats.Add("fish_caught", 1);
        _g.Stats.Max("species_max", _g.Quests.Species());
        _g.Audio.Sfx("fanfare");
        var at = Spot?.Pos ?? _g.Player.Pos;
        _g.Env.Normal.Emit(Emit.At(at, 20, "#ffffff").R(2.5f).G(-8).L(0.9f).S(0.18f).A(0.85f));
        Stop();
        _g.Hud.FishCard(T($"fish.{f.Id}"), size, isNew);
        _g.Events.Emit("fishCaught", key: f.Id, a: size, b: isNew ? 1 : 0, data: Spot?.Water);
        _g.Autosave();
    }

    /// <summary>Test kancasi: belirli baligi aninda yakala.</summary>
    public void TestCatch(string id, string water = "sea")
    {
        Fish = _g.World.AllFish.First(x => x.Id == id);
        Spot ??= (_g.Player.Pos, water);
        Catch();
    }
}
