using System.Numerics;
using Starfall.Core;

namespace Starfall.Gameplay;

/// <summary>Photo mode: oyun durur, arayuz gizlenir, kamera serbest kalir.</summary>
public sealed class PhotoMode
{
    public static readonly string[] Filters = { "none", "warm", "sepia", "mono", "dream", "poster" };
    private readonly Game _g;
    public bool Active;
    public int Filter;
    public Vector3 Pos;
    public float Yaw, Pitch;
    public bool HidePlayer;
    public float Flash;
    public bool HideUi;
    private GameState _prev;

    public PhotoMode(Game g) => _g = g;

    public void Enter()
    {
        Active = true;
        _prev = _g.State;
        _g.SetState(GameState.Photo);
        var cam = _g.Env.Camera;
        Pos = cam.Position;
        var dir = cam.Forward;
        Yaw = MathF.Atan2(-dir.X, -dir.Z);
        Pitch = MathF.Asin(Math.Clamp(dir.Y, -1, 1));
        HidePlayer = false;
        _g.Hud.Visible = false;
    }

    public void Exit()
    {
        Active = false;
        _g.Env.Post.Filter = 0;
        _g.Player.Model.Root.Visible = true;
        _g.Hud.Visible = true;
        _g.SetState(GameState.Playing);
    }

    public string FilterName => Loc.T($"photo.filter.{Filters[Filter]}");

    public void Shoot()
    {
        HideUi = true;
        _g.RequestScreenshot(path =>
        {
            HideUi = false;
            Flash = 1;
            _g.Audio.Sfx("shutter");
            _g.Stats.Add("photos", 1);
            _g.Events.Emit("photo", Pos, key: path);
            _g.Events.Emit("toast", key: "photo.saved", data: new (string, object)[] { ("where", path) });
        });
    }

    public void Update(float dt, Input input)
    {
        if (!Active) return;
        Flash = MathF.Max(0, Flash - dt * 3);
        if (input.Pressed("back") || input.Pressed("photo")) { Exit(); return; }
        if (input.Pressed("filter"))
        {
            Filter = (Filter + 1) % Filters.Length;
            _g.Env.Post.Filter = Filter;
            _g.Audio.Sfx("uiMove");
        }
        if (input.Pressed("hide"))
        {
            HidePlayer = !HidePlayer;
            _g.Player.Model.Root.Visible = !HidePlayer;
        }
        if (input.Pressed("shot")) { Shoot(); return; }
        var look = input.Look();
        float sens = _g.Settings.V.Sensitivity;
        Yaw -= look.X * sens;
        Pitch -= look.Y * sens;
        Pitch = Math.Clamp(Pitch, -1.4f, 1.4f);
        var mv = input.Move();
        float speed = input.Held("sprint") ? 14 : 5;
        var fwd = new Vector3(-MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));
        var right = new Vector3(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));
        Pos += fwd * (mv.Y * speed * dt) + right * (mv.X * speed * dt);
        if (input.Held("rise")) Pos.Y += speed * dt;
        if (input.Held("sink")) Pos.Y -= speed * dt;
        // oyuncudan cok uzaklasmasin
        var off = Pos - _g.Player.Pos;
        if (off.Length() > 25) Pos = _g.Player.Pos + Vector3.Normalize(off) * 25;
        var W = _g.World;
        Pos.Y = MathF.Max(Pos.Y, MathF.Max(W.Height(Pos.X, Pos.Z) + 0.3f, W.WaterLevel(Pos.X, Pos.Z) + 0.2f));
        var cam = _g.Env.Camera;
        cam.Position = Pos;
        cam.Target = Pos + fwd;
    }
}
