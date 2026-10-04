using System.Numerics;
using Starfall.Core;
using Starfall.Render;

namespace Starfall.Gameplay;

/// <summary>Ara sahne kamerasi: konuma yumusak gecis + bakis noktasi.</summary>
public sealed record CamOverride(Vector3 Pos, Vector3 Look, float Speed = 2);

/// <summary>
/// Ucuncu sahis kamerasi: fare/sag cubukla doner, carpismayla yaklasir, istege bagli olarak
/// hareket ederken yavasca karakterin arkasina gecer.
/// </summary>
public sealed class CameraRig
{
    private readonly Game _g;
    public float Yaw = MathX.Pi, Pitch = 0.32f, Distance = 7, TargetDistance = 7;
    public Vector3 Focus;
    public float IdleLook, Shake, FovBoost;
    public CamOverride? Override;
    private readonly Random _r = new(5);

    public CameraRig(Game g) => _g = g;

    private Camera Cam => _g.Env.Camera;

    public void Snap(Vector3 target, float? yaw = null)
    {
        Focus = target + new Vector3(0, 0.8f, 0);
        if (yaw is float y) Yaw = y;
        Update(0, null, null, true);
    }

    public void Update(float dt, Input? input, Player? player, bool instant = false)
    {
        var S = _g.Settings.V;
        var cam = Cam;
        if (Override is { } o)
        {
            cam.Position = Vector3.Lerp(cam.Position, o.Pos, instant ? 1 : MathF.Min(1, dt * o.Speed));
            cam.Target = o.Look;
            return;
        }
        if (input != null)
        {
            var look = input.Look();
            float sens = S.Sensitivity;
            float inv = S.InvertY ? -1 : 1;
            Yaw -= look.X * sens;
            Pitch += look.Y * sens * inv;
            Pitch = Math.Clamp(Pitch, -0.5f, 1.25f);
            if (MathF.Abs(look.X) + MathF.Abs(look.Y) > 0.0005f) IdleLook = 0;
            else IdleLook += dt;
            if (input.Wheel != 0) TargetDistance = Math.Clamp(TargetDistance + input.Wheel * 0.8f, 3.5f, 12);
        }

        float extraDist = 0;
        if (player != null)
        {
            var target = player.Pos + new Vector3(0, 0.8f, 0);
            if (player.Swimming) target.Y = MathF.Max(target.Y, player.WaterLevelAt() + 0.5f);
            if (player.Boating) { target.Y += 0.4f; extraDist = 2.5f; }
            float k = instant ? 1 : 1 - MathF.Exp(-dt * 10);
            Focus = Vector3.Lerp(Focus, target, k);
            // otomatik kamera: oyuncu kamerayla oynamiyorsa arkaya gec
            float hs = MathX.Hypot(player.Vel.X, player.Vel.Z);
            if ((S.AutoCamera || player.Boating) && IdleLook > 1.2f && hs > 2 && !instant)
            {
                float behind = player.Yaw + MathX.Pi;
                float d = MathX.WrapAngle(behind - Yaw);
                Yaw += d * MathF.Min(1, dt * 0.9f) * MathF.Min(1, hs / 8);
            }
            float speedFov = player.Gliding ? 8 : player.Vel.Length() > 9 ? 4 : 0;
            FovBoost += (speedFov - FovBoost) * MathF.Min(1, dt * 2);
        }

        float dk = dt > 0 ? MathF.Min(1, dt * 6) : 1;
        Distance += (TargetDistance + extraDist - Distance) * dk;
        var dir = new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));
        float dist = Distance;
        if (player != null && _g.World.Physics.Raycast(Focus, dir, dist + 0.3f, out var hit, player.IgnoreShape))
            dist = MathF.Max(1.2f, hit.Distance - 0.35f);
        var pos = Focus + dir * dist;
        float gh = _g.World.Height(pos.X, pos.Z) + 0.5f;
        if (pos.Y < gh) pos.Y = gh;
        float wl = _g.World.WaterLevel(pos.X, pos.Z) + 0.35f;
        if (pos.Y < wl) pos.Y = wl;
        if (Shake > 0 && S.CameraShake)
        {
            Shake = MathF.Max(0, Shake - dt * 3);
            pos.X += ((float)_r.NextDouble() - 0.5f) * Shake * 0.3f;
            pos.Y += ((float)_r.NextDouble() - 0.5f) * Shake * 0.3f;
        }
        cam.Position = pos;
        cam.Target = Focus;
        cam.Fov = 60 + FovBoost;
    }
}
