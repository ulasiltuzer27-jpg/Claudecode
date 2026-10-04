using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Net;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.Client;

/// <summary>
/// Yerel oyuncu: birinci sahis hareket ve bakis, hedefleme, tuslarin
/// eylemlere cevrilmesi, araba itme, el modeli. Hareket istemcide simule
/// edilir (tepkisellik); eylemler oturuma (host) istek olarak gider.
/// </summary>
public sealed class LocalPlayer
{
    public const float WalkSpeed = 4.0f;
    public const float SprintSpeed = 6.4f;
    public const float CrouchSpeed = 2.0f;
    public const float PushSpeed = 3.0f;
    public const float EyeHeight = 1.62f;
    public const float CrouchEye = 1.05f;

    private readonly PilavciGame _game;
    public CharacterMotor Motor { get; } = new();
    public float Yaw;
    public float Pitch;
    public bool Crouching;

    private float _eye = EyeHeight;
    private float _bobPhase;
    private float _bobAmount;
    private float _stepDistance;
    private int _stepIndex;
    private float _useAnim;
    private float _dropHeld;
    private Vector2 _sway;
    private byte _teleportSeq;
    private float _landKick;

    public Target Target { get; private set; } = Target.None;
    public List<InteractionOption> Options { get; private set; } = new();
    /// <summary>Basili tutulan eylemin ilerleme gostergesi (0..1) ya da -1.</summary>
    public float HoldProgress { get; private set; } = -1;

    public LocalPlayer(PilavciGame game) => _game = game;

    public void Spawn(PlayerEntity p)
    {
        Motor.Position = p.Position;
        Motor.Velocity = Vector3.Zero;
        Yaw = p.Yaw;
        Pitch = 0;
        _teleportSeq = p.TeleportSeq;
    }

    public CameraView Camera(float fov)
    {
        var bobY = MathF.Sin(_bobPhase * 2) * 0.035f * _bobAmount;
        var bobX = MathF.Cos(_bobPhase) * 0.02f * _bobAmount;
        var right = Entity.Right(Yaw);
        return new CameraView
        {
            Position = Motor.Position + new Vector3(0, _eye + bobY - _landKick, 0) + right * bobX,
            Yaw = Yaw,
            Pitch = Pitch,
            Roll = -_sway.X * 0.015f,
            FovDegrees = fov + (Motor.Velocity.LengthSquared() > 30 ? 4 : 0),
            Near = 0.05f,
            Far = 900f,
        };
    }

    /// <summary>Bir kare. <paramref name="inputEnabled"/> false iken (menu acik) yalnizca yercekimi isler.</summary>
    public void Update(float dt, GameSession session, bool inputEnabled)
    {
        var w = session.World;
        var p = session.LocalPlayer;
        if (p is null)
        {
            return;
        }

        var input = _game.Input;
        var settings = _game.Settings;

        // Host bizi isinladiysa (gun basi) uy.
        if (p.TeleportSeq != _teleportSeq)
        {
            _teleportSeq = p.TeleportSeq;
            Motor.Position = p.Position;
            Motor.Velocity = Vector3.Zero;
            Yaw = p.Yaw;
        }

        // ── Bakis ────────────────────────────────────────────────────
        if (inputEnabled)
        {
            var look = input.Look;
            Yaw -= look.X;
            Pitch = Math.Clamp(Pitch + look.Y, -1.45f, 1.45f);
            _sway = Vector2.Lerp(_sway, new Vector2(look.X, look.Y) * 8f, MathF.Min(1, dt * 10));
        }
        else
        {
            _sway = Vector2.Lerp(_sway, Vector2.Zero, MathF.Min(1, dt * 10));
        }

        Yaw %= MathF.Tau;

        // ── Hareket ──────────────────────────────────────────────────
        var cart = w.Get<StationEntity>(p.PushingCartId);
        var held = w.HeldBy(p);
        var move = inputEnabled ? input.Move : Vector2.Zero;
        Crouching = inputEnabled && input.Down(GameAction.Crouch) && cart is null;
        var sprint = inputEnabled && input.Down(GameAction.Sprint) && !Crouching && cart is null && move.Y > 0.1f;
        var speed = cart is not null ? PushSpeed : Crouching ? CrouchSpeed : sprint ? SprintSpeed : WalkSpeed;
        if (held is not null)
        {
            speed *= held.Info.CarrySpeed;
        }

        var fwd = Entity.Forward(Yaw);
        var right = Entity.Right(Yaw);
        var wish = fwd * move.Y + right * move.X;
        var wishXZ = new Vector2(wish.X, wish.Z) * speed;

        var before = Motor.Position;
        Motor.Height = Crouching ? 1.15f : 1.75f;
        Motor.IgnoreOwner = cart?.Id ?? 0;
        var wasGrounded = Motor.Grounded;
        var fallSpeed = Motor.Velocity.Y;
        Motor.Move(w.Collision, wishXZ, inputEnabled && input.Pressed(GameAction.Jump) && cart is null, dt);
        if (!wasGrounded && Motor.Grounded && fallSpeed < -4f)
        {
            _landKick = MathF.Min(0.12f, -fallSpeed * 0.012f);
            _game.Audio.PlayAt("step1", Motor.Position, 0.9f, 0.8f);
        }

        _landKick = MathF.Max(0, _landKick - dt * 0.6f);

        if (cart is not null)
        {
            PushCart(w, session, cart, before, dt);
        }

        // Goz yuksekligi yumusak (basamak cikarken kamera sicramasin)
        var targetEye = Crouching ? CrouchEye : EyeHeight;
        _eye += (targetEye - _eye) * MathF.Min(1, dt * 10);
        var moved = new Vector2(Motor.Position.X - before.X, Motor.Position.Z - before.Z).Length();
        if (Motor.Grounded && moved > 0.001f)
        {
            _bobPhase += moved * (sprint ? 2.2f : 2.8f);
            _bobAmount = MathF.Min(1, _bobAmount + dt * 4) * (settings.HeadBob ? 1 : 0);
            _stepDistance += moved;
            if (_stepDistance > (sprint ? 0.85f : 0.7f))
            {
                _stepDistance = 0;
                _stepIndex = (_stepIndex + 1) % 4;
                _game.Audio.PlayAt("step" + (_stepIndex + 1), Motor.Position, Crouching ? 0.25f : 0.55f, 0.9f + _stepIndex * 0.05f);
            }
        }
        else
        {
            _bobAmount = MathF.Max(0, _bobAmount - dt * 3);
        }

        // Varliga yaz (host'ta dogrudan, istemcide ag ile gidecek)
        p.SetMotion(Motor.Position, Yaw);
        p.Pitch = Pitch;
        p.Crouch = Crouching;

        // ── Hedefleme ve eylemler ────────────────────────────────────
        var cam = Camera(settings.Fov);
        var dir = cam.Forward;
        Target = Targeting.Find(w, p, cam.Position, dir, held);
        Options = Interactions.Options(w, p, Target);
        if (cart is not null)
        {
            Options = [new InteractionOption(ActionId.ReleaseCart, InputSlot.Interact, "act.release_cart")];
        }

        if (inputEnabled)
        {
            HandleInput(session, p, held, dt);
        }
        else if (p.HoldAction != ActionId.None)
        {
            p.HoldAction = ActionId.None;
        }

        HoldProgress = ComputeHoldProgress(w, p, held);
        _useAnim = MathF.Max(0, _useAnim - dt * 4);
        session.SendLocalState(p, cart);
    }

    private void PushCart(GameWorld w, GameSession session, StationEntity cart, Vector3 before, float dt)
    {
        // Araba oyuncunun onunde, tutamak ellerde.
        var offset = StationDefs.PushOffset(cart.Tier);
        var cartYaw = Yaw + MathF.PI / 2;
        var cartPos = Motor.Position - Entity.LocalToWorld(Vector3.Zero, cartYaw, offset);
        cartPos.Y = w.Collision.GroundHeight(new Vector2(cartPos.X, cartPos.Z), 0.4f, cartPos.Y + 0.4f, cart.Id);
        var candidate = cart;
        var body = StationDefs.Bodies(candidate)[0];
        var box = new BoxCollider(Entity.LocalToWorld(cartPos, cartYaw, body.Center), body.Half, cartYaw, body.Flags, cart.Id);
        // Kaldirim gibi alcak engeller (0.25 m'den alcak) arabayi durdurmaz: tekerler cikar.
        if (w.Collision.BoxBlocked(box, cart.Id, cartPos.Y + 0.25f))
        {
            // Geri al: oyuncu ve araba eski yerinde
            Motor.Position = new Vector3(before.X, Motor.Position.Y, before.Z);
            return;
        }

        session.SetCartPose(cart, cartPos, cartYaw);
    }

    private void HandleInput(GameSession session, PlayerEntity p, ItemEntity? held, float dt)
    {
        var input = _game.Input;
        InteractionOption? Find(InputSlot slot)
        {
            foreach (var o in Options)
            {
                if (o.Slot == slot)
                {
                    return o;
                }
            }

            return null;
        }

        void Trigger(InteractionOption? o)
        {
            if (o is not { Enabled: true } opt || opt.Action == ActionId.None)
            {
                if (o is { Enabled: false })
                {
                    _game.Audio.Play("ui_error", 0.4f);
                }

                return;
            }

            _useAnim = 1f;
            switch (opt.Action)
            {
                case ActionId.OpenLaptop:
                    _game.Screens.Push(new Screens.LaptopScreen());
                    return;
                case ActionId.OpenPriceBoard:
                    _game.Screens.Push(new Screens.PriceBoardScreen());
                    return;
                case ActionId.OpenCashBox:
                {
                    var cust = Target.Kind == TargetKind.Customer
                        ? session.World.Get<CustomerEntity>(Target.EntityId)
                        : session.World.Customers.FirstOrDefault(c => c.State == CustomerState.Paying && c.CartId == Target.EntityId);
                    if (cust is not null)
                    {
                        _game.Screens.Push(new Screens.CashScreen(cust.Id));
                    }

                    return;
                }
            }

            session.SendAction(ActionRequest.From(opt.Action, Target));
        }

        if (input.Pressed(GameAction.Interact))
        {
            Trigger(Find(InputSlot.Interact));
        }

        if (input.Pressed(GameAction.Secondary))
        {
            Trigger(Find(InputSlot.Secondary));
        }

        if (input.Pressed(GameAction.Use))
        {
            Trigger(Find(InputSlot.Use));
        }

        if (input.Pressed(GameAction.AltUse))
        {
            Trigger(Find(InputSlot.AltUse));
        }

        // Basili tutulan eylemler
        var hold = ActionId.None;
        if (input.Down(GameAction.Interact) && Find(InputSlot.HoldInteract) is { Enabled: true } hi)
        {
            hold = hi.Action;
        }
        else if (input.Down(GameAction.Use) && Find(InputSlot.HoldUse) is { Enabled: true } hu)
        {
            hold = hu.Action;
        }

        if (hold != ActionId.None)
        {
            p.HoldAction = hold;
            p.HoldTargetId = Target.EntityId;
            p.HoldPart = Target.Part;
            _useAnim = 0.6f + MathF.Sin(_game.Time * 14) * 0.4f;
        }
        else
        {
            p.HoldAction = ActionId.None;
        }

        // Birak / firlat: kisa bas birak, uzun bas firlat
        if (held is not null)
        {
            if (input.Down(GameAction.Drop))
            {
                _dropHeld += dt;
            }
            else if (_dropHeld > 0)
            {
                session.SendAction(new ActionRequest { Action = _dropHeld > 0.45f ? ActionId.Throw : ActionId.Drop, Text = "" });
                _dropHeld = 0;
            }
        }
        else
        {
            _dropHeld = 0;
        }
    }

    private static float ComputeHoldProgress(GameWorld w, PlayerEntity p, ItemEntity? held) => p.HoldAction switch
    {
        ActionId.HoldWash when held is not null => held.Wash,
        ActionId.HoldFill when held?.Type == ItemType.OlcuKabi => held.WaterL / Interactions.JugCapacity,
        ActionId.HoldFill when held?.Pot is { } pot => pot.WaterL / Interactions.PotWaterCapacity(held),
        ActionId.HoldStir when w.Get<ItemEntity>(p.HoldTargetId)?.Pot is { } pot2 => Math.Clamp(pot2.Toast, 0f, 1f),
        ActionId.HoldShred when w.Get<StationEntity>(p.HoldTargetId) is { } board => board.BoardShred,
        ActionId.HoldWashDishes when w.Get<StationEntity>(p.HoldTargetId)?.Cart is { } c => c.DirtyPlates > 0 ? 0.5f : 1f,
        _ => -1,
    };

    /// <summary>El modeli: tutulan esya ve kollar (ayri gecis).</summary>
    public void DrawViewModel(GameSession session, WorldRenderer wr, in CameraView cam)
    {
        var w = session.World;
        var p = session.LocalPlayer;
        if (p is null)
        {
            return;
        }

        var held = w.HeldBy(p);
        var fwd = cam.Forward;
        var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
        var up = Vector3.Cross(right, fwd);
        var bob = new Vector3(MathF.Cos(_bobPhase) * 0.012f, MathF.Sin(_bobPhase * 2) * 0.01f, 0) * _bobAmount;
        var sway = new Vector3(-_sway.X * 0.01f, -_sway.Y * 0.01f, 0);
        var use = _useAnim;

        if (held is not null)
        {
            var twoHanded = held.Info.TwoHanded || held.Type == ItemType.Tencere;
            var local = twoHanded ? new Vector3(0, -0.42f, 0.62f) : new Vector3(0.24f, -0.3f, 0.5f);
            if (held.Type is ItemType.Tabak or ItemType.PaketKap)
            {
                local = new Vector3(0.05f, -0.32f, 0.48f);
            }

            local += bob + sway + new Vector3(0, use * 0.03f, use * 0.06f);
            var pos = cam.Position + right * local.X + up * local.Y + fwd * local.Z;
            var yaw = cam.Yaw;
            // Genis tek elli esyalar (suzgec, tencere) goz hizasinda ekrani kaplamasin.
            var scale = held.Type == ItemType.Kazan ? 0.75f : !twoHanded && held.Info.HalfSize.X > 0.12f ? 0.65f : 1f;
            wr.DrawItem(w, held, pos, yaw, DrawFlags.ViewModel, scale);
        }

        // Kollar: iki on kol, ekranin altindan esyaya dogru
        var sleeve = Gfx.Hex(0xF4F6F7);
        var skin = Gfx.Hex(0xE0AC69);
        foreach (var side in new[] { -1f, 1f })
        {
            // Bos elde kol cizilmez: ekranin altinda yarim gorunen bir el
            // kirpintisindan iyidir. Araba iterken iki kol da gorunur.
            if (held is null && p.PushingCartId == 0)
            {
                continue;
            }

            var reach = held is null ? 0.42f : 0.5f;
            var spread = held?.Info.TwoHanded == true || held?.Type == ItemType.Tencere ? 0.24f : 0.18f;
            if (held is not null && !(held.Info.TwoHanded || held.Type == ItemType.Tencere) && side < 0)
            {
                continue;
            }

            var hx = held is null ? 0.22f : side * spread + (held.Info.TwoHanded ? 0 : 0.06f);
            if (p.PushingCartId != 0)
            {
                hx = side * 0.22f;
            }

            var handLocal = new Vector3(hx, -0.36f, reach) + bob + sway + new Vector3(0, use * 0.03f, use * 0.08f);
            var shoulderLocal = new Vector3(side * 0.3f, -0.62f, 0.05f);
            var hand = cam.Position + right * handLocal.X + up * handLocal.Y + fwd * handLocal.Z;
            var shoulder = cam.Position + right * shoulderLocal.X + up * shoulderLocal.Y + fwd * shoulderLocal.Z;
            var m = Matrix4x4.CreateWorld(shoulder, Vector3.Normalize(hand - shoulder), up);
            var len = Vector3.Distance(hand, shoulder);
            wr.Models.Get("vm_arm", b => Shapes.Box(b[Content.M.Fabric], Matrix4x4.CreateTranslation(0, 0, -0.5f), new Vector3(0.09f, 0.09f, 1f), Color.White));
            var arm = wr.Models.Get("vm_arm", _ => { });
            _game.Renderer.Submit(arm, Matrix4x4.CreateScale(1, 1, len * 0.75f) * m, sleeve, DrawFlags.ViewModel);
            var handModel = wr.Models.Get("vm_hand", b => Shapes.Box(b[Content.M.Skin], Matrix4x4.CreateTranslation(0, 0, -0.04f), new Vector3(0.075f, 0.05f, 0.11f), Color.White));
            _game.Renderer.Submit(handModel, Matrix4x4.CreateWorld(hand, Vector3.Normalize(hand - shoulder), up), skin, DrawFlags.ViewModel);
        }
    }
}
