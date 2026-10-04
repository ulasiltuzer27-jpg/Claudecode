using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.World;

namespace Starfall.Gameplay;

/// <summary>
/// Hareket sabitleri. Sayilar tasarim kararidir: ziplama tepe yuksekligi v^2/2g = 10^2/56 ~ 1.79 m,
/// her kanat cirpma ~1.45 m ekler. Fener zirvesinin son yari 6.8 m oldugu icin 4 tuy gerekir
/// (1.79 + 4x1.45 = 7.6 m). --verify bunu denetler.
/// </summary>
public static class Move
{
    public const float Gravity = -28;
    public const float JumpVel = 10;
    public const float FlapVel = 9;
    public const float Walk = 6.5f;
    public const float Sprint = 10.5f;
    public const float Swim = 3.6f;
    public const float SwimSprint = 5.4f;
    public const float GlideSpeed = 9.5f;
    public const float GlideSink = -2.2f;
    public const float UpdraftVel = 7;
    public const float BounceVel = 17;
    public const float GroundAccel = 45;
    public const float AirAccel = 16;
    public const float Coyote = 0.13f;
    public const float JumpBuffer = 0.14f;
    public const float Radius = 0.3f;
    public const float HalfHeight = 0.25f;
    public const float IceAccel = 7;
    public const float ClimbSpeed = 2.4f;
    public const float ClimbBase = 3.0f;   // dayaniklilik (sn); her altin tuy +1 sn
}

/// <summary>
/// Mina'nin hareketi. CharacterMotor carpismayi cozer; hiz, yercekimi, ziplama, kanat cirpma,
/// suzulme, yuzme (ve Asama B'de tirmanma, tekne, buz) burada.
/// </summary>
public sealed partial class Player
{
    private readonly Game _g;
    public readonly FoxModel Model = new();
    public readonly CharacterMotor Motor;
    public Vector3 Pos, Vel;
    public float Yaw = MathX.Pi;
    public bool Grounded, Gliding, Swimming, Frozen;
    public float Coyote, JumpBuf, AirTime, SpeedNorm;
    public int FlapsUsed;
    public Vector3 LastSafe;
    public string? GroundTag;
    public Shape? GroundShape;
    private float _safeTimer, _stepTimer, _swimAcc, _currentGlide;
    public float FallStartY;
    private bool _warned;
    public float PeakY = float.MinValue;

    public Player(Game g)
    {
        _g = g;
        Motor = new CharacterMotor(g.World.Physics) { Radius = Move.Radius, HalfHeight = Move.HalfHeight };
        g.Env.Scene.Add(Model.Root);
    }

    public int Feathers => _g.Progress.FeatherCount();

    public void Spawn(float x, float y, float z, float yaw)
    {
        Motor.Teleport(new Vector3(x, y, z));
        Pos = Motor.Feet;
        Vel = Vector3.Zero;
        Yaw = yaw;
        LastSafe = Pos;
        Swimming = false;
        Gliding = false;
        ExitSpecialStates();
        Model.Root.Position = Pos;
        Model.Root.Rotation = new Vector3(0, Yaw, 0);
    }

    public float WaterLevelAt() => _g.World.WaterLevel(Pos.X, Pos.Z);

    public float Update(float dt, Input input, float camYaw)
    {
        var ev = _g.Events;
        float prevY = Pos.Y;
        if (UpdateSpecial(dt, input, camYaw)) return prevY;

        // --- girdi -> istenen yon (kamera eksenine gore)
        var mv = Frozen ? Vector2.Zero : input.Move();
        bool sprint = !Frozen && input.Held("sprint");
        var fwd = new Vector3(-MathF.Sin(camYaw), 0, -MathF.Cos(camYaw));
        var right = new Vector3(-fwd.Z, 0, fwd.X);
        var wish = fwd * mv.Y + right * mv.X;
        float wishLen = MathF.Min(1, wish.Length());
        if (wishLen > 0.001f) wish = Vector3.Normalize(wish);

        float wl = WaterLevelAt();
        bool submerged = Pos.Y < wl - 0.42f && !_g.World.IsFrozenWater(Pos.X, Pos.Z);
        if (submerged && !Swimming)
        {
            Swimming = true;
            ev.Emit("splash", Pos, MathF.Min(1, MathF.Abs(Vel.Y) / 15));
            Vel.Y *= 0.2f;
            FlapsUsed = 0;
            EndGlide();
        }
        else if (Swimming && Pos.Y > wl - 0.25f && Grounded) Swimming = false;

        // --- yatay hiz
        float maxSpeed = sprint ? Move.Sprint : Move.Walk;
        if (Swimming) maxSpeed = sprint ? Move.SwimSprint : Move.Swim;
        if (Gliding) maxSpeed = Move.GlideSpeed;
        bool onIce = Grounded && GroundTag == "ice";
        if (onIce) maxSpeed *= 1.15f;
        var target = wish * (maxSpeed * wishLen);
        float accel = Grounded || Swimming ? (onIce ? Move.IceAccel : Move.GroundAccel) : Move.AirAccel * (Gliding ? 1.4f : 1);
        var hv = new Vector3(Vel.X, 0, Vel.Z);
        var diff = target - hv;
        float maxDelta = accel * dt;
        if (diff.Length() > maxDelta) diff = Vector3.Normalize(diff) * maxDelta;
        hv += diff;
        if (Gliding && wishLen < 0.1f)
        {
            // suzulurken birakilirsa da bakilan yone suzulmeye devam et
            var f = new Vector3(MathF.Sin(Yaw), 0, MathF.Cos(Yaw)) * (Move.GlideSpeed * 0.75f);
            hv = Vector3.Lerp(hv, f, MathF.Min(1, dt * 1.5f));
        }
        Vel.X = hv.X;
        Vel.Z = hv.Z;

        // --- ziplama / cirpma / suzulme
        if (!Frozen && input.Pressed("jump")) JumpBuf = Move.JumpBuffer;
        else JumpBuf = MathF.Max(0, JumpBuf - dt);
        Coyote = Grounded ? Move.Coyote : MathF.Max(0, Coyote - dt);

        bool jumpHeld = !Frozen && input.Held("jump");
        if (Swimming)
        {
            float tgt = wl - 0.48f;
            Vel.Y += ((tgt - Pos.Y) * 9 - Vel.Y * 4) * dt;
            if (JumpBuf > 0 && Pos.Y > wl - 0.7f)
            {
                Vel.Y = 8.2f;
                JumpBuf = 0;
                Swimming = false;
                ev.Emit("jump", Pos, 1);
            }
        }
        else
        {
            if (JumpBuf > 0 && Coyote > 0)
            {
                Vel.Y = Move.JumpVel;
                JumpBuf = 0;
                Coyote = 0;
                Grounded = false;
                Model.Jump();
                ev.Emit("jump", Pos);
            }
            else if (JumpBuf > 0 && !Grounded && TryGrabWall()) JumpBuf = 0;
            else if (JumpBuf > 0 && !Grounded && FlapsUsed < Feathers)
            {
                Vel.Y = MathF.Max(Vel.Y, Move.FlapVel);
                FlapsUsed++;
                JumpBuf = 0;
                Model.DoFlip();
                EndGlide();
                ev.Emit("flap", Pos, FlapsUsed, Feathers);
            }

            var inUpdraft = UpdraftAt();
            bool canGlide = !Grounded && jumpHeld && (Vel.Y < 0 || (inUpdraft != null && Gliding)) && Model.Flip <= 0.05f;
            if (canGlide && !Gliding)
            {
                Gliding = true;
                ev.Emit("glideStart", Pos);
            }
            else if (Gliding && (!jumpHeld || Grounded)) EndGlide();

            if (Gliding)
            {
                if (inUpdraft != null) Vel.Y += (Move.UpdraftVel - Vel.Y) * MathF.Min(1, dt * 2.5f);
                else Vel.Y += (Move.GlideSink - Vel.Y) * MathF.Min(1, dt * 5);
            }
            else
            {
                Vel.Y += Move.Gravity * dt;
                if (Vel.Y < -32) Vel.Y = -32;
            }
        }

        // --- sinir: acik denizde akinti geri iter
        var push = _g.World.BoundaryPush(Pos);
        if (push != Vector2.Zero)
        {
            Vel.X += push.X * dt * 4;
            Vel.Z += push.Y * dt * 4;
            if (!_warned)
            {
                _warned = true;
                ev.Emit("toast", key: "hint.current");
            }
        }
        else if (_g.World.InsideBoundary(Pos, -10)) _warned = false;

        // --- motor ile hareket
        bool wasGrounded = Grounded;
        var moved = Motor.Move(Vel * dt, Vel.Y <= 0.1f);
        Grounded = Motor.Grounded;
        GroundShape = Motor.GroundShape;
        GroundTag = Motor.GroundShape?.Tag;
        if (Motor.HitCeiling && Vel.Y > 0) Vel.Y = 0;
        if (GroundTag == null && Grounded && _g.World.BounceShapes.Count > 0)
        {
            if (_g.World.Physics.GroundAt(Pos.X, Pos.Z, Pos.Y + 0.5f, out float gy, out var gs) && Pos.Y - gy < 0.3f && gs != null)
            {
                GroundShape = gs;
                GroundTag = gs.Tag;
            }
        }
        Pos = Motor.Feet;
        AutoGrab(wishLen);
        if (Climbing) return prevY;

        if (dt > 0)
        {
            // engele takilan yatay hizi sifirla (duvara yapisma olmasin)
            float realVx = moved.X / dt, realVz = moved.Z / dt;
            if (MathF.Abs(realVx) < MathF.Abs(Vel.X) * 0.5f) Vel.X = realVx;
            if (MathF.Abs(realVz) < MathF.Abs(Vel.Z) * 0.5f) Vel.Z = realVz;
        }

        // --- inis
        if (Grounded)
        {
            if (!wasGrounded)
            {
                float impact = MathF.Max(0, -Vel.Y);
                Model.Land(impact);
                ev.Emit("land", Pos, impact, key: GroundTag);
                EndGlide();
                RecordAir();
            }
            if (GroundTag == "bounce" && Vel.Y <= 0.5f)
            {
                Vel.Y = Move.BounceVel;
                Grounded = false;
                FlapsUsed = 0;
                Model.Jump();
                ev.Emit("bounce", Pos, data: GroundShape);
            }
            else if (Vel.Y < 0) Vel.Y = 0;
            FlapsUsed = 0;
            AirTime = 0;
        }
        else if (!Swimming)
        {
            AirTime += dt;
            if (wasGrounded && Vel.Y <= 0) FallStartY = Pos.Y;
        }
        else
        {
            AirTime = 0;
            FlapsUsed = 0;
        }

        // --- guvenli nokta (dusme/sikisma kurtarmasi)
        _safeTimer -= dt;
        if (Grounded && !Swimming && _safeTimer <= 0 && GroundTag != "bounce")
        {
            _safeTimer = 1;
            LastSafe = Pos;
        }
        if (Pos.Y < -25 || !float.IsFinite(Pos.Y))
        {
            Spawn(LastSafe.X, LastSafe.Y + 0.5f, LastSafe.Z, Yaw);
            ev.Emit("toast", key: "hint.rescued");
        }

        // --- yonelim
        float hs = MathX.Hypot(Vel.X, Vel.Z);
        if (hs > 0.4f && wishLen > 0.05f)
        {
            float targetYaw = MathF.Atan2(Vel.X, Vel.Z);
            float d = MathX.WrapAngle(targetYaw - Yaw);
            Yaw += d * MathF.Min(1, dt * (Gliding ? 4 : onIce ? 6 : 12));
        }
        SpeedNorm = hs / Move.Walk;

        // --- istatistik izleri
        if (Swimming)
        {
            _swimAcc += hs * dt;
            if (_swimAcc >= 1)
            {
                int m = (int)MathF.Floor(_swimAcc);
                _swimAcc -= m;
                _g.Stats.Add("swim_m", m);
            }
        }
        // suzulme mesafesi: katedilen yatay yol (akinti geri itse de sayilir)
        if (Gliding)
        {
            _currentGlide += hs * dt;
            if (_currentGlide >= 1) _g.Stats.Max("glide_max", MathF.Floor(_currentGlide));
        }
        if (!Grounded && !Swimming && AirTime > 1) _g.Stats.Max("air_max", MathF.Floor(AirTime));
        if (onIce && hs > 1) _g.Session.IceSlide += hs * dt;

        // adim sesi
        if (Grounded && hs > 1)
        {
            _stepTimer -= dt * hs;
            if (_stepTimer <= 0)
            {
                _stepTimer = 1.6f;
                ev.Emit("step", Pos, key: SurfaceType());
            }
        }

        PeakY = MathF.Max(PeakY, Pos.Y);
        UpdateModel(dt, mv, wl);
        return prevY;
    }

    private void UpdateModel(float dt, Vector2 mv, float wl)
    {
        var m = Model;
        var p = Pos;
        if (Swimming) p.Y = MathF.Max(Pos.Y, wl - 0.35f);
        m.Root.Position = p;
        m.Root.Rotation = new Vector3(0, Yaw, Gliding ? Math.Clamp(-mv.X * 0.35f, -0.35f, 0.35f) : 0);
        m.Update(dt, new FoxPose { Speed = SpeedNorm, Grounded = Grounded || Swimming, Vy = Vel.Y, Gliding = Gliding, Swimming = Swimming });
    }

    public void EndGlide()
    {
        if (!Gliding) return;
        Gliding = false;
        if (_currentGlide > 0) _g.Stats.Max("glide_max", MathF.Floor(_currentGlide));
        _currentGlide = 0;
    }

    private void RecordAir()
    {
        if (AirTime > 1) _g.Stats.Max("air_max", MathF.Floor(AirTime));
    }

    public UpdraftDef? UpdraftAt()
    {
        foreach (var u in _g.World.Updrafts)
        {
            float d = MathX.Hypot(Pos.X - u.X, Pos.Z - u.Z);
            if (d < u.R && Pos.Y < u.Top) return u;
        }
        return null;
    }

    public string SurfaceType()
    {
        if (GroundTag == "bounce") return "mushroom";
        if (GroundTag == "ice") return "ice";
        float h = _g.World.Height(Pos.X, Pos.Z);
        if (Pos.Y - h > 0.4f) return "wood";
        if (_g.World.IsSnow(Pos.X, Pos.Z)) return "snow";
        if (h < 2.0f) return "sand";
        return "grass";
    }
}
