using System.Numerics;
using Starfall.Core;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.Gameplay;

/// <summary>
/// Yaris: geri sayim, rakip rotada ziplayarak (veya kayarak) ilerler, bayraga ilk varan kazanir.
/// Kurbaga yarisi (Ada 1) ve penguen kizak yarisi (Kar Adasi) ayni sinifi kullanir.
/// </summary>
public sealed class Race
{
    public sealed record Course(string Id, Vector2[] Path, float Speed, Func<Vector3> Goal, string QuestId, string RewardId, string RewardKind, string Stat, bool Hop = true, float Limit = 70);

    private readonly Game _g;
    public string State = "idle";
    private float _timer, _frogDist, _length;
    public float Elapsed;
    private int _lastCount;
    private Npc? _npc;
    private List<Vector3> _pts = new();
    private float[] _arc = new float[201];

    /// <summary>Yay uzunluguna gore nokta (three.js getPointAt).</summary>
    private Vector3 PointAt(float u)
    {
        float target = Math.Clamp(u, 0, 1) * _length;
        int i = Array.BinarySearch(_arc, target);
        if (i < 0) i = ~i;
        i = Math.Clamp(i, 1, 200);
        float seg = _arc[i] - _arc[i - 1];
        float f = seg > 1e-6f ? (target - _arc[i - 1]) / seg : 0;
        return Geo2.Catmull(_pts, (i - 1 + f) / 200f);
    }
    private Vector3 _goal;
    public Course? Current;

    public Race(Game g) => _g = g;

    public Course FrogCourse => new("frog", WD.RacePath, WD.RaceFrogSpeed, () => _g.World.RaceFlagPos, "frog", "q_frog", "shard", "race");

    private void Build(Course c)
    {
        _pts = c.Path.Select(p => new Vector3(p.X, _g.World.Height(p.X, p.Y), p.Y)).ToList();
        _arc = new float[201];
        var prev = Geo2.Catmull(_pts, 0);
        for (int i = 1; i <= 200; i++)
        {
            var q = Geo2.Catmull(_pts, i / 200f);
            _arc[i] = _arc[i - 1] + Vector3.Distance(prev, q);
            prev = q;
        }
        _length = _arc[200];
        _goal = c.Goal();
    }

    public void Start(Npc npc, Course? course = null)
    {
        Current = course ?? FrogCourse;
        Build(Current);
        _npc = npc;
        npc.Racing = true;
        State = "countdown";
        _timer = 3.4f;
        Elapsed = 0;
        _frogDist = 0;
        _lastCount = 4;
        _g.Hud.RaceBanner(T("race.ready"));
        _g.Audio.Sfx("uiConfirm");
    }

    public void Cancel()
    {
        if (_npc != null) ResetRival();
        State = "idle";
        _g.Hud.RaceBanner(null);
        _g.Hud.RaceTimer(null);
    }

    private void ResetRival()
    {
        var npc = _npc!;
        npc.Racing = false;
        var d = npc.Def;
        _g.Npcs.PlaceAt(npc, d.X, d.Z, d.Yaw);
    }

    public void Update(float dt)
    {
        if (State == "idle") return;
        var hud = _g.Hud;
        if (State == "countdown")
        {
            _timer -= dt;
            int n = (int)MathF.Ceiling(_timer - 0.4f);
            if (n != _lastCount && n >= 1 && n <= 3)
            {
                _lastCount = n;
                hud.RaceBanner(n.ToString());
                _g.Audio.Sfx("beep");
            }
            if (_timer <= 0.4f && _lastCount != 0)
            {
                _lastCount = 0;
                hud.RaceBanner(T("race.go"));
                _g.Audio.Sfx("beepHigh");
            }
            if (_timer <= 0)
            {
                State = "running";
                _g.After(0.7f, () => { if (State == "running") hud.RaceBanner(null); });
            }
            return;
        }
        if (State != "running") return;
        var c = Current!;
        Elapsed += dt;
        hud.RaceTimer(Elapsed);
        _frogDist += c.Speed * dt;
        float u = MathF.Min(1, _frogDist / _length);
        var p = PointAt(u);
        var ahead = PointAt(MathF.Min(1, u + 0.01f));
        float hop = c.Hop ? MathF.Abs(MathF.Sin(Elapsed * 7)) * 0.7f : 0;
        var npc = _npc!;
        npc.Pos = new Vector3(p.X, _g.World.Height(p.X, p.Z) + hop, p.Z);
        npc.Model.Root.Position = npc.Pos;
        npc.Yaw = MathF.Atan2(ahead.X - p.X, ahead.Z - p.Z);
        npc.Model.Root.Rotation = new Vector3(0, npc.Yaw, 0);
        if (npc.Collider != null) _g.World.Physics.Move(npc.Collider, new Vector3(p.X, -50, p.Z));

        var pl = _g.Player.Pos;
        bool atGoal = MathX.Hypot(pl.X - _goal.X, pl.Z - _goal.Z) < 3.2f && MathF.Abs(pl.Y - _goal.Y) < 4;
        if (atGoal) { Finish(true); return; }
        if (u >= 1) { Finish(false); return; }
        if (Elapsed > c.Limit) Finish(false);
    }

    private void Finish(bool won)
    {
        var c = Current!;
        State = "idle";
        _g.Hud.RaceTimer(null);
        _g.Hud.RaceBanner(won ? T("race.win") : T("race.lose"));
        _g.After(2.2f, () => _g.Hud.RaceBanner(null));
        var npc = _npc!;
        float time = Elapsed;
        if (won)
        {
            _g.Audio.Sfx("fanfare");
            _g.Env.Additive.Emit(Render.Emit.At(_g.Player.Pos + new Vector3(0, 1.5f, 0), 60, "#ff6b6b", "#ffd84a", "#7fd0ff", "#9be36a", "#ff9df0").R(6).G(-6).L(1.6f).S(0.25f));
            _g.Stats.Max(c.Stat, 1);
            var key = "race_" + c.Id;
            if (!_g.Save!.Best.TryGetValue(key, out var best) || time < best) _g.Save.Best[key] = time;
        }
        else _g.Audio.Sfx("lose");
        bool firstWin = won && _g.Save!.Quest(c.QuestId) != "done";
        _g.After(1.4f, () =>
        {
            // rakip bayragin yaninda dursun, konusma bitince evine donsun
            _g.Npcs.PlaceAt(npc, _goal.X + 1.6f, _goal.Z + 1.2f, 0);
            npc.Racing = false;
            void Done()
            {
                ResetRival();
                if (firstWin) _g.Quests.FinishQuest(c.QuestId, c.RewardId, c.RewardKind);
            }
            string k = won ? (firstWin ? $"dlg.{c.Id}.win" : $"dlg.{c.Id}.winAgain") : $"dlg.{c.Id}.lose";
            _g.Dialogue.Start(npc, lines: Lines(k, ("time", time.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))), onEnd: Done);
        });
    }
}

/// <summary>Kucuk egri yardimcisi (three.js CatmullRomCurve3.getPointAt yaklasimi).</summary>
public static class Geo2
{
    public static Vector3 Catmull(List<Vector3> p, float t) => Render.Geo.CatmullRom(p, t);
}
