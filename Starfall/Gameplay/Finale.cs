using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Core.Loc;

namespace Starfall.Gameplay;

/// <summary>
/// Final: yildiz parcalari fenere akar, lamba yanar, havai fisekler, jenerik. Sonrasinda fener
/// her gece donen isigiyla adayi aydinlatir.
/// </summary>
public sealed class Finale
{
    private readonly Game _g;
    public bool Active, Lit;
    private Beam? _beam;
    private PointLight? _lampLight;
    private Material? _lampMat;
    private float _t, _t3, _ignite, _beamYaw;
    public int Stage;
    private bool _titleShown;
    private Vector3 _lamp;
    private readonly Random _r = new(31);

    public Finale(Game g) => _g = g;

    public static MeshData BeamMesh()
    {
        // Silindirin ust yaricapi genis, alti dar: kaydirinca dar uc lambaya (y=0) oturur.
        // aColor.x = v (dar uc 0, en parlak).
        var g = Geo.CylinderGeo(9, 0.5f, 90, 24, 1, true);
        for (int i = 0; i < g.Count; i++) g.C[i] = new Vector3((g.P[i].Y + 45) / 90f, 0, 0);
        g.Translate(0, 45, 0).RotateZ(-MathX.Pi / 2);
        var g2 = g.Clone().RotateY(MathX.Pi);
        return MeshData.From(Geo.Merge(g, g2), false);
    }

    private void EnsureBeam()
    {
        if (_beam != null) return;
        _lamp = _g.World.Anchor("lighthouse.lamp");
        _g.Env.BeamMesh ??= BeamMesh();
        _beam = new Beam { Visible = false };
        _g.Env.Beams.Add(_beam);
        _lampLight = new PointLight { Position = _lamp, Color = MathX.Hex("#ffd27a"), Intensity = 0, Distance = 40, Decay = 1.4f };
        _g.Env.Scene.Lights.Add(_lampLight);
        _lampMat = _g.World.Lighthouse.LampMat;
    }

    public void SetLit(bool on)
    {
        EnsureBeam();
        Lit = on;
        _beam!.Visible = on;
        _lampMat!.EmissiveIntensity = on ? 3.5f : 0;
        _lampLight!.Intensity = on ? 30 : 0;
    }

    public void Start()
    {
        EnsureBeam();
        Active = true;
        _t = 0;
        Stage = 0;
        _titleShown = false;
        _g.SetState(GameState.Cutscene);
        _g.Player.Frozen = true;
        _g.Audio.Music.SetMood("finale");
    }

    public void Update(float dt)
    {
        if (Lit && _beam != null)
        {
            _beamYaw += dt * 0.5f;
            _beam.Transform = Matrix4x4.CreateRotationY(_beamYaw) * Matrix4x4.CreateTranslation(_lamp);
            if (!Active) _beam.Intensity = 0.25f + _g.Env.Sky.Night * 0.9f;
        }
        if (!Active) return;
        _t += dt;
        float time = _t;
        var post = _g.Env.Post;

        // 0-0.8: kararma, saati aksama al
        if (Stage == 0)
        {
            post.Fade = MathF.Min(1, time / 0.8f);
            if (time >= 0.8f)
            {
                Stage = 1;
                _g.SetHour(20.4f);
                _g.Hud.Visible = false;
            }
            return;
        }
        // kamera fenerin etrafinda doner
        float ang = 0.6f + (time - 0.8f) * 0.18f;
        var camPos = new Vector3(_lamp.X + MathF.Sin(ang) * 26, _lamp.Y + 3, _lamp.Z + MathF.Cos(ang) * 26);
        _g.CameraRig.Override = new CamOverride(camPos, _lamp + new Vector3(0, -2, 0), 6);

        if (Stage == 1)
        {
            post.Fade = MathF.Max(0, 1 - (time - 0.8f) / 0.8f);
            // parcalar oyuncudan lambaya akar
            if (_r.NextDouble() < dt * 30)
            {
                var from = _g.Player.Pos + new Vector3(0, 1, 0);
                var v = (_lamp - from) * (1 / 1.6f);
                _g.Env.Additive.Emit(Emit.At(from, 1, "#fff2a8", "#ffd76a").Sp(0.3f).V(v.X, v.Y, v.Z).L(1.6f).S(0.45f, 0.2f).A(1));
            }
            if (time > 4.2f)
            {
                Stage = 2;
                _ignite = 0;
                _g.Audio.Sfx("ignite");
            }
            return;
        }
        if (Stage == 2)
        {
            _ignite = MathF.Min(1, _ignite + dt);
            _lampMat!.EmissiveIntensity = _ignite * 3.5f;
            _lampLight!.Intensity = _ignite * 30;
            _beam!.Visible = true;
            Lit = true;
            _beam.Intensity = _ignite * 1.15f;
            if (time > 5.4f && !_titleShown)
            {
                _titleShown = true;
                _g.Hud.TitleCard(T("finale.title"), T("finale.subtitle"));
            }
            // havai fisekler
            if (_r.NextDouble() < dt * 2.2f && time < 14) Firework();
            if (time > 15)
            {
                Stage = 3;
                _t3 = 0;
            }
            return;
        }
        if (Stage == 3)
        {
            _t3 += dt;
            post.Fade = MathF.Min(1, _t3 / 1.0f);
            if (_t3 >= 1.0f)
            {
                Stage = 4;
                _g.Hud.TitleCard(null, null);
                _g.Credits.Show(End);
            }
        }
    }

    public void Firework(Vector3? center = null)
    {
        var lamp = center ?? (_beam != null ? _lamp : _g.World.Anchor("lighthouse.lamp"));
        float a = (float)_r.NextDouble() * MathX.TwoPi;
        float r = 18 + (float)_r.NextDouble() * 30;
        var pos = new Vector3(lamp.X + MathF.Cos(a) * r, lamp.Y + 18 + (float)_r.NextDouble() * 25, lamp.Z + MathF.Sin(a) * r);
        string[][] palettes =
        {
            new[] { "#ff6b6b", "#ffd0d0" }, new[] { "#ffd84a", "#fff4b0" }, new[] { "#7fd0ff", "#d0f0ff" },
            new[] { "#b98aff", "#efe0ff" }, new[] { "#9be36a", "#e0ffd0" },
        };
        var col = palettes[_r.Next(palettes.Length)];
        _g.Env.Additive.Emit(Emit.At(pos, 90, col).R(13).G(-4).D(1.2f).L(1.8f).S(0.7f, 0.1f).A(1));
        _g.Audio.Sfx("firework", Vector3.Distance(pos, _g.Env.Camera.Position));
    }

    private void End()
    {
        var s = _g.Save!;
        Active = false;
        _g.CameraRig.Override = null;
        s.Flags["finale"] = true;
        s.FinaleTime ??= s.PlayTime;
        _g.Stats.Max("finale", 1);
        if (s.PlayTime < 30 * 60) _g.Stats.Max("speedrun", 1);
        _g.Wardrobe.Give("hat_crown", true);
        _g.Player.Frozen = false;
        _g.Hud.Visible = true;
        _g.Audio.Music.SetMood("auto");
        _g.SetState(GameState.Playing);
        _g.Progress.UpdateCompletion();
        _g.Autosave();
        _g.FadeIn(1 / 0.04f / 60f);
        _g.CameraRig.Snap(_g.Player.Pos, _g.Player.Yaw + MathX.Pi);
    }
}
