using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.Render;

namespace Starfall.Gameplay;

/// <summary>
/// Ozel hareket durumlari:
///  - Tirmanma: sarmasikli duvara (etiket "climb") havadayken ziplama tusuyla tutun; WASD ile
///    duvar boyunca hareket; dayaniklilik = 3 sn + her altin tuy icin 1 sn; ust kenarda otomatik
///    cikis; ziplama = duvardan geri sicra.
///  - Tekne: iskelede binilir; W itme, A/D dumen, S fren; kara/kayalara carpinca durur.
///  - Kizak: kizak tepesinde binilir; egim boyunca hizlanir, A/D ile yon, ziplama ile in.
/// </summary>
public sealed partial class Player
{
    public bool Boating, Climbing, Sledding;
    public float Stamina, StaminaMax = Move.ClimbBase, StaminaShow;
    /// <summary>Kamera isininin yok sayacagi sekil (tekne govdesi gibi).</summary>
    public Shape? IgnoreShape;
    public Shape? ClimbShape;
    private Vector3 _wallN;
    private float _climbPhase, _climbStepT, _climbedAcc, _regrab;
    private float _boatSpeed, _boatT, _paddleT, _sledSpeed, _sledSfxT;
    public Node? BoatNode;
    private Node? _sledNode;
    public bool CanClimb => _g.Flag("climb");

    // ------------------------------------------------------------------ giris/cikis
    public void ExitSpecialStates()
    {
        if (Climbing) StopClimb(false);
        if (Boating) LeaveBoat(Pos, Yaw);
        if (Sledding) StopSled();
    }

    private bool UpdateSpecial(float dt, Input input, float camYaw)
    {
        StaminaMax = Move.ClimbBase + Feathers;
        if (!Climbing)
        {
            if (Grounded) Stamina = MathF.Min(StaminaMax, Stamina + dt * StaminaMax * 0.8f);
            StaminaShow = MathF.Max(0, StaminaShow - dt * (Stamina >= StaminaMax ? 1.2f : 0.2f));
        }
        _regrab = MathF.Max(0, _regrab - dt);
        if (Climbing) { UpdateClimb(dt, input, camYaw); return true; }
        if (Boating) { UpdateBoat(dt, input, camYaw); return true; }
        if (Sledding) { UpdateSled(dt, input, camYaw); return true; }
        return false;
    }

    // ------------------------------------------------------------------ tirmanma
    /// <summary>Havadayken ziplama: onundeki tirmanilabilir duvara tutun.</summary>
    private bool TryGrabWall()
    {
        if (!CanClimb || Swimming || _regrab > 0 || Stamina < 0.4f) return false;
        var fwd = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        var c = Pos + new Vector3(0, Move.Radius + Move.HalfHeight, 0);
        foreach (var dir in new[] { fwd, Motor.HitWall ? -Motor.WallNormal : fwd })
        {
            if (_g.World.Physics.Raycast(c, dir, 1.1f, out var hit) && hit.Shape?.Tag == "climb" && MathF.Abs(hit.Normal.Y) < 0.5f)
            {
                StartClimb(hit);
                return true;
            }
        }
        return false;
    }

    /// <summary>Havada duvara dogru itiyorsa kendiliginden tutun (ziplayip duvara yapis).</summary>
    private void AutoGrab(float wishLen)
    {
        if (Climbing || Grounded || Swimming || wishLen < 0.3f || Vel.Y > 3) return;
        if (Motor.HitWall && Motor.WallShape?.Tag == "climb") TryGrabWall();
    }

    /// <summary>Duvara dogru yururken (yerdeyken) de tutunabilsin: ileri + ziplama.</summary>
    public bool NearClimbWall(out RayHit hit)
    {
        var fwd = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        var c = Pos + new Vector3(0, Move.Radius + Move.HalfHeight, 0);
        return _g.World.Physics.Raycast(c, fwd, 1.0f, out hit) && hit.Shape?.Tag == "climb" && MathF.Abs(hit.Normal.Y) < 0.5f;
    }

    private void StartClimb(RayHit hit)
    {
        Climbing = true;
        ClimbShape = hit.Shape;
        _wallN = Vector3.Normalize(new Vector3(hit.Normal.X, 0, hit.Normal.Z));
        Vel = Vector3.Zero;
        Gliding = false;
        Yaw = MathF.Atan2(-_wallN.X, -_wallN.Z);
        // duvara yasla
        var target = hit.Point + _wallN * (Move.Radius + 0.06f) - new Vector3(0, Move.Radius + Move.HalfHeight, 0);
        Motor.Teleport(target);
        Pos = Motor.Feet;
        StaminaShow = 1;
        _g.Audio.Sfx("climbGrab");
        _g.Events.Emit("climbStart", Pos);
    }

    private void StopClimb(bool jumpOff)
    {
        Climbing = false;
        ClimbShape = null;
        _regrab = 0.35f;
        if (jumpOff)
        {
            Vel = _wallN * 4.5f + new Vector3(0, 7.5f, 0);
            Model.Jump();
        }
    }

    private void UpdateClimb(float dt, Input input, float camYaw)
    {
        var mv = Frozen ? Vector2.Zero : input.Move();
        var right = Vector3.Cross(-_wallN, Vector3.UnitY); // duvara bakarken sag
        var delta = (Vector3.UnitY * mv.Y + right * mv.X) * Move.ClimbSpeed * dt;
        bool moving = mv.LengthSquared() > 0.01f;
        Stamina -= dt * (moving ? 1 : 0.35f);
        if (Stamina < StaminaMax * 0.25f && Stamina + dt >= StaminaMax * 0.25f) _g.Audio.Sfx("staminaLow");
        StaminaShow = 1;

        if (!Frozen && input.Pressed("jump"))
        {
            StopClimb(true);
            Grounded = false;
            return;
        }
        if (Stamina <= 0)
        {
            StopClimb(false);
            _g.Events.Emit("toast", key: "hint.climbTired");
            return;
        }
        // duvara hafif bastir (temas korunsun)
        var start = Pos;
        Motor.Move(delta - _wallN * 0.04f, false);
        Pos = Motor.Feet;
        float climbed = Pos.Y - start.Y;
        if (climbed > 0) { _climbedAcc += climbed; }
        if (_climbedAcc >= 1)
        {
            int m = (int)_climbedAcc;
            _climbedAcc -= m;
            _g.Stats.Add("climb_m", m);
        }
        if (moving)
        {
            _climbPhase += dt * 9;
            _climbStepT -= dt;
            if (_climbStepT <= 0) { _climbStepT = 0.32f; _g.Audio.Sfx("climbStep"); }
        }

        // duvar hala onumuzde mi? (bas hizasinda ve gogus hizasinda)
        var chest = Pos + new Vector3(0, Move.Radius + Move.HalfHeight, 0);
        var head = Pos + new Vector3(0, Move.Radius * 2 + Move.HalfHeight * 2 + 0.15f, 0);
        bool wallChest = _g.World.Physics.Raycast(chest, -_wallN, 0.8f, out var hc) && hc.Shape?.Tag == "climb";
        bool wallHead = _g.World.Physics.Raycast(head, -_wallN, 0.8f, out _);
        if (!wallChest && !wallHead)
        {
            StopClimb(false);
            return;
        }
        if (wallChest && !wallHead && mv.Y > 0.1f)
        {
            // ust kenar: cik (yukari + ileri)
            Motor.Move(new Vector3(0, 1.15f, 0), false);
            Motor.Move(-_wallN * 0.9f, false);
            Motor.Move(new Vector3(0, -0.6f, 0), true);
            Pos = Motor.Feet;
            StopClimb(false);
            _regrab = 0.6f;
            _g.Audio.Sfx("climbGrab");
            _g.Events.Emit("climbTop", Pos, key: hc.Shape?.Tag);
            return;
        }
        if (wallChest)
        {
            var n = Vector3.Normalize(new Vector3(hc.Normal.X, 0, hc.Normal.Z));
            if (n.LengthSquared() > 0.5f) _wallN = Vector3.Normalize(Vector3.Lerp(_wallN, n, MathF.Min(1, dt * 8)));
            Yaw = MathF.Atan2(-_wallN.X, -_wallN.Z);
        }
        // asagi inip zemine degdiyse birak
        if (Motor.Grounded && mv.Y < -0.1f) { StopClimb(false); return; }

        Grounded = false;
        Model.Root.Position = Pos;
        Model.Root.Rotation = new Vector3(0, Yaw, 0);
        Model.Update(dt, new FoxPose { Climbing = true, ClimbPhase = moving ? _climbPhase : 0, Grounded = false });
        PeakY = MathF.Max(PeakY, Pos.Y);
    }

    // ------------------------------------------------------------------ tekne
    public void BoardBoat(Node boat, Vector3 at, float yaw)
    {
        BoatNode = boat;
        Boating = true;
        _g.World.BoatMode = true;
        Yaw = yaw;
        Pos = at;
        Vel = Vector3.Zero;
        _boatSpeed = 0;
        Swimming = false;
        Gliding = false;
        _g.Audio.Sfx("boatIn");
        _g.Events.Emit("boatIn", Pos);
    }

    public void LeaveBoat(Vector3 to, float yaw)
    {
        Boating = false;
        _g.World.BoatMode = false;
        Spawn(to.X, to.Y, to.Z, yaw);
        _g.Events.Emit("boatOut", Pos);
    }

    public float BoatSpeed => _boatSpeed;

    private void UpdateBoat(float dt, Input input, float camYaw)
    {
        var mv = Frozen ? Vector2.Zero : input.Move();
        bool sail = _g.Flag("sail");
        float max = sail ? 16 : 7;
        float thrust = mv.Y > 0.1f ? mv.Y : 0;
        float brake = mv.Y < -0.1f ? -mv.Y : 0;
        _boatSpeed += (thrust * max - _boatSpeed) * MathF.Min(1, dt * (thrust > 0 ? 0.6f : 0.25f));
        _boatSpeed -= brake * dt * 8;
        if (_boatSpeed < -2.5f) _boatSpeed = -2.5f;
        float turn = mv.X * (0.9f + MathF.Min(1, MathF.Abs(_boatSpeed) / 6) * 0.5f);
        Yaw -= turn * dt * (_boatSpeed >= 0 ? 1 : -1);
        var fwd = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        var next = Pos + fwd * (_boatSpeed * dt);
        // kara/kaya: ileride sig zemin varsa dur
        var probe = next + fwd * (2.2f * MathF.Sign(_boatSpeed));
        float depthAhead = _g.World.WaterLevel(probe.X, probe.Z) - _g.World.Height(probe.X, probe.Z);
        bool blocked = depthAhead < 0.9f || _g.World.Physics.Raycast(Pos + new Vector3(0, 0.4f, 0), fwd * MathF.Sign(_boatSpeed), 2.4f, out var bh, IgnoreShape) && bh.Shape?.Tag != "npc";
        if (blocked && MathF.Abs(_boatSpeed) > 0.3f)
        {
            if (MathF.Abs(_boatSpeed) > 4) { _g.Audio.Sfx("creak"); _g.CameraRig.Shake = 0.3f; }
            _boatSpeed = -_boatSpeed * 0.2f;
            next = Pos;
        }
        // sinir: akinti geri iter
        var push = _g.World.BoundaryPush(next);
        if (push != Vector2.Zero) next += new Vector3(push.X, 0, push.Y) * dt * 2;
        _boatT += dt;
        float wl = _g.World.WaterLevel(next.X, next.Z);
        Pos = new Vector3(next.X, wl + 0.12f + MathF.Sin(_boatT * 1.4f) * 0.06f, next.Z);
        Vel = fwd * _boatSpeed;
        float hs = MathF.Abs(_boatSpeed);
        SpeedNorm = 0;
        if (hs > 1)
        {
            _paddleT -= dt;
            if (_paddleT <= 0)
            {
                _paddleT = sail ? 0.9f : 0.75f;
                _g.Audio.Sfx(sail ? "sail" : "paddle", MathF.Min(1, hs / 10));
                var back = Pos - fwd * 1.8f;
                _g.Env.Normal.Emit(Emit.At(new Vector3(back.X, wl + 0.05f, back.Z), 3, "#ffffff").Sp(0.6f, 0.05f, 0.6f).V(0, 0.4f, 0, 0.5f).L(0.9f).S(0.25f, 0.6f).A(0.6f).D(2));
            }
        }
        if (_g.World.IslandAt(Pos.X, Pos.Z) == "isle2" && _g.World.TerrainAt(Pos.X, Pos.Z) != null) _g.Session.ReachedIsle2ByBoat = true;
        // model: tilki teknede oturur
        if (BoatNode != null)
        {
            BoatNode.Position = Pos + new Vector3(0, -0.2f, 0);
            BoatNode.Rotation = new Vector3(MathF.Sin(_boatT * 1.1f) * 0.03f, Yaw, MathF.Sin(_boatT * 1.3f) * 0.05f - turn * 0.06f);
        }
        Model.Root.Position = Pos + new Vector3(0, 0.0f, -0.25f * 0);
        Model.Root.Rotation = new Vector3(0, Yaw, MathF.Sin(_boatT * 1.3f) * 0.05f);
        Model.Update(dt, new FoxPose { Boating = true, Grounded = true });
        Grounded = false;
        Swimming = false;
    }

    // ------------------------------------------------------------------ kizak
    public void StartSled(float yaw)
    {
        Sledding = true;
        Yaw = yaw;
        _sledSpeed = 2;
        _sledNode ??= new Node(MeshData.From(Buildings2.Sled(), true), Material.Std());
        if (_sledNode.Parent == null) _g.Env.Scene.Add(_sledNode);
        _sledNode.Visible = true;
        _g.Events.Emit("sledStart", Pos);
    }

    public void StopSled()
    {
        Sledding = false;
        if (_sledNode != null) _sledNode.Visible = false;
        Vel = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw)) * MathF.Min(_sledSpeed, 6);
        _g.Events.Emit("sledStop", Pos);
    }

    public float SledSpeed => _sledSpeed;

    private void UpdateSled(float dt, Input input, float camYaw)
    {
        var mv = Frozen ? Vector2.Zero : input.Move();
        if (!Frozen && input.Pressed("jump")) { StopSled(); return; }
        var n = _g.World.Normal(Pos.X, Pos.Z);
        var fwd = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        // egim boyunca yercekimi bileseni (ileri yonde)
        var downhill = new Vector3(n.X, 0, n.Z);
        float slopeAcc = Vector3.Dot(downhill, fwd) * 26;
        _sledSpeed += (slopeAcc - _sledSpeed * 0.12f - (mv.Y < -0.1f ? 6 : 0)) * dt;
        _sledSpeed = Math.Clamp(_sledSpeed, 0, 24);
        Yaw -= mv.X * dt * 1.6f;
        // yokus asagi kendiliginden don (yolda kalmaya yardim)
        if (downhill.LengthSquared() > 0.002f)
        {
            float want = MathF.Atan2(downhill.X, downhill.Z);
            Yaw += MathX.WrapAngle(want - Yaw) * MathF.Min(1, dt * 0.6f) * (MathF.Abs(mv.X) < 0.1f ? 1 : 0.2f);
        }
        fwd = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw));
        var moved = Motor.Move(fwd * (_sledSpeed * dt) + new Vector3(0, -6 * dt, 0), true);
        Pos = Motor.Feet;
        if (dt > 0 && moved.Length() / dt < _sledSpeed * 0.4f && _sledSpeed > 3) _sledSpeed *= 0.5f; // engel
        Grounded = Motor.Grounded;
        _g.Session.IceSlide += _sledSpeed * dt;
        _sledSfxT -= dt;
        if (_sledSfxT <= 0 && _sledSpeed > 2)
        {
            _sledSfxT = 0.35f;
            _g.Audio.Sfx("sled", MathF.Min(1, _sledSpeed / 18));
            _g.Env.Normal.Emit(Emit.At(Pos, 4, "#ffffff", "#eef6ff").Sp(0.4f, 0.05f, 0.4f).V(0, 1.2f, 0, 1.2f).L(0.6f).S(0.2f, 0.45f).A(0.7f).D(3));
        }
        if (_sledSpeed < 0.6f && slopeAcc < 0.5f) { StopSled(); return; }
        if (_sledNode != null)
        {
            _sledNode.Position = Pos;
            var right = Vector3.Cross(Vector3.UnitY, fwd);
            float pitch = -MathF.Asin(Math.Clamp(Vector3.Dot(n, fwd), -1, 1));
            _sledNode.Rotation = new Vector3(pitch, Yaw, 0);
        }
        Vel = fwd * _sledSpeed;
        PeakY = MathF.Max(PeakY, Pos.Y);
        Model.Root.Position = Pos + new Vector3(0, 0.18f, 0);
        Model.Root.Rotation = new Vector3(0, Yaw, -mv.X * 0.15f);
        Model.Update(dt, new FoxPose { Boating = true, Grounded = true });
    }
}
