using System.Globalization;
using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Net;
using PilavciSimulator.Persistence;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Screens;

/// <summary>
/// Oyun ekrani: oturumu calistirir, yerel oyuncuyu gunceller, dunyayi
/// cizer, olaylari (ses, bildirim, para ustu, gun sonu) isler.
/// </summary>
public sealed class GameplayScreen : Screen
{
    public GameSession Session { get; }
    public LocalPlayer Local { get; private set; } = null!;
    public WorldRenderer World { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    /// <summary>Host: otomatik kayit yuvasi (-1 = kaydetme).</summary>
    public int SaveSlot { get; }

    private float _dt;
    private bool _spawned;
    private float _newsTimer;
    private bool _chatOpen;
    private string _chatText = "";
    private float _playTime;
    private CameraView _lastCam;

    public override bool ShowCursor => _chatOpen;

    public GameplayScreen(GameSession session, int saveSlot)
    {
        Session = session;
        SaveSlot = saveSlot;
    }

    public override void Enter()
    {
        Game.Session = Session;
        World = Game.WorldView;
        World.ResetSession();
        World.Upload(Game.Layout);
        Local = new LocalPlayer(Game);
        Hud = new Hud(Game);
        Session.EventReceived += OnEvent;
        if (Session is ClientSession cs)
        {
            cs.WorldReplaced += () => _spawned = false;
        }

        Game.Renderer.Fade = 1f;
        Game.Platform.SetRichPresence("status", Loc.T("presence.playing"));
    }

    public override void Exit()
    {
        Session.EventReceived -= OnEvent;
        if (Game.Session == Session)
        {
            Game.Session = null;
        }

        Game.Audio.ResetLoopTargets();
        Session.Dispose();
        Game.Platform.SetRichPresence("status", null);
    }

    // ── Olaylar ─────────────────────────────────────────────────────
    private void OnEvent(WorldEvent e)
    {
        var w = Session.World;
        switch (e.Type)
        {
            case WorldEventType.Sound:
                Game.Audio.PlayAt(e.Key, e.Position);
                break;
            case WorldEventType.Toast:
            {
                var args = e.Arg.Length > 0 ? e.Arg.Split('|').Select(a => a.Contains('.') && Loc.Has(a) ? Loc.T(a) : a).Cast<object>().ToArray() : [];
                var color = e.Color switch
                {
                    1 => Theme.Red,
                    2 => Theme.Green,
                    3 => Theme.Yellow,
                    _ => Theme.Blue,
                };
                Game.Toasts.Show(Loc.T(e.Key, args), color);
                break;
            }
            case WorldEventType.Coin:
                World.CoinBurst(e.Position);
                Hud.Popup("+" + Fmt.Money(e.Value), e.Position, Theme.Yellow);
                break;
            case WorldEventType.OpenCash:
                if (e.PlayerId == Session.LocalPlayerId && Game.Screens.Find<CashScreen>() is null)
                {
                    Game.Screens.Push(new CashScreen(e.Value));
                }

                break;
            case WorldEventType.LevelUp:
                Hud.LevelFlash();
                Game.Audio.Play("levelup");
                break;
            case WorldEventType.Achievement:
                Game.Platform.UnlockAchievement(e.Key);
                Game.Toasts.Show(Loc.T("toast.achievement", Loc.T("ach." + e.Key)), Theme.Yellow, 5f);
                Game.Audio.Play("achievement");
                break;
            case WorldEventType.Burst:
                if (e.Key == "smoke")
                {
                    World.SmokeBurst(e.Position);
                }

                break;
            case WorldEventType.DayEnded:
                if (Game.Screens.Find<DayReportScreen>() is null)
                {
                    Game.Screens.Push(new DayReportScreen(e.Key == "passout"));
                }

                Game.Platform.SubmitLeaderboard("en_iyi_gun", w.Economy.History.LastOrDefault()?.Profit ?? 0);
                PushStats(w);
                break;
            case WorldEventType.DayStarted:
                if (Game.Screens.Find<DayReportScreen>() is { } rep)
                {
                    Game.Screens.Remove(rep);
                }

                _newsTimer = 9f;
                Game.Renderer.Fade = 1f;
                if (Session.IsHost && SaveSlot >= 0)
                {
                    try
                    {
                        SaveSystem.Save(SaveSlot, w, Game.Platform.PlayerName);
                        Game.Toasts.Show(Loc.T("toast.saved"), Theme.Green);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("otomatik kayit basarisiz", ex);
                        Game.Toasts.Show(Loc.T("toast.save_failed"), Theme.Red);
                    }
                }

                break;
            case WorldEventType.Chat:
                Hud.Chat(e.Key, e.Arg);
                Game.Audio.Play("ui_click", 0.5f);
                break;
            case WorldEventType.Zabita:
                if (e.Value == 1)
                {
                    Game.Audio.Play("whistle", 1f);
                }

                break;
        }
    }

    private void PushStats(GameWorld w)
    {
        foreach (var key in new[] { "served", "days_played", "correct_change", "kazan_cooked" })
        {
            Game.Platform.SetStat(key, (int)Math.Min(int.MaxValue, w.Progress.Stat(key)));
        }

        Game.Platform.StoreStats();
    }

    // ── Guncelleme ──────────────────────────────────────────────────
    public override void Update(float dt, bool focused)
    {
        _dt = dt;
        _playTime += dt;
        Session.Update(dt);
        if (Session.FatalError is { } err)
        {
            Game.Screens.ReplaceAll(new MainMenuScreen(Loc.T(err)));
            return;
        }

        if (!Session.Ready)
        {
            return;
        }

        var p = Session.LocalPlayer!;
        if (!_spawned)
        {
            Local.Spawn(p);
            _spawned = true;
            _newsTimer = 9f;
            if (Session.IsHost)
            {
                // Steam kapaliyken acilmis basarimlar kayitta durur; simdi esitle
                // (Steam tarafinda zaten acik olanlar icin islem yapilmaz).
                foreach (var id in Session.World.Progress.Achievements)
                {
                    Game.Platform.UnlockAchievement(id);
                }

                PushStats(Session.World);
            }
        }

        var input = Game.Input;
        if (focused && !_chatOpen)
        {
            if (input.MenuBack)
            {
                Game.Screens.Push(new PauseScreen());
            }
            else if (input.Pressed(GameAction.Phone))
            {
                Game.Screens.Push(new PhoneScreen());
            }
            else if (input.Pressed(GameAction.Chat) && Session.IsMultiplayer)
            {
                _chatOpen = true;
                _chatText = "";
                Game.Ui.ResetFocus();
            }
        }

        Local.Update(dt, Session, focused && !_chatOpen);
        Hud.Update(dt);
        _newsTimer = MathF.Max(0, _newsTimer - dt);
        Game.Renderer.Fade = MathF.Max(0, Game.Renderer.Fade - dt * 1.2f);
        var w = Session.World;
        var late = w.Clock.Minute - w.Data.Balance.ClosingMinute;
        Game.Renderer.Tired = late > 0 ? Math.Clamp(late / (w.Data.Balance.PassOutMinute - w.Data.Balance.ClosingMinute), 0f, 0.8f) : 0f;
        UpdateAudio(w);
    }

    private void UpdateAudio(GameWorld w)
    {
        var a = Game.Audio;
        var cam = _lastCam;
        a.ListenerPosition = cam.Position;
        a.ListenerForward = cam.Forward;
        a.ResetLoopTargets();
        var indoor = w.Layout.DepotArea.Contains(cam.Position) || w.Layout.ShopArea.Contains(cam.Position);
        var hour = w.Clock.Minute / 60f;
        var night = DayNight.LampLevel(hour);
        a.SetLoop("amb_city", (indoor ? 0.25f : 0.55f) * (1 - night * 0.5f));
        a.SetLoop("amb_night", night * (indoor ? 0.1f : 0.35f));
        a.SetLoop("amb_rain", w.Weather.Rain * (indoor ? 0.35f : 0.8f));
        var seaDist = MathF.Max(0, 16 - cam.Position.Z);
        a.SetLoop("amb_sea", Math.Clamp(1 - seaDist / 25f, 0f, 1f) * 0.6f);
        var crowd = w.Customers.Count(c => Vector3.DistanceSquared(c.Position, cam.Position) < 15 * 15);
        a.SetLoop("amb_crowd", Math.Clamp(crowd / 12f, 0f, 0.5f));
        foreach (var it in w.Items)
        {
            if (it.Pot is not { } pot || pot.IsEmpty)
            {
                continue;
            }

            var pos = World.ItemPos(w, it);
            if (pot.WaterL > 0.05f && pot.Temp >= 97)
            {
                a.SetLoopAt("loop_boil", pos, 0.8f);
            }
            else if (pot.Temp > 110)
            {
                a.SetLoopAt("loop_sizzle", pos, 0.7f);
            }
        }

        foreach (var s in w.Stations)
        {
            if (s.Type is StationType.KazanOcagi or StationType.Stovetop && s.Heat.Any(h => h > 0))
            {
                a.SetLoopAt("loop_flame", s.Position, 0.5f, 8f);
            }
        }

        foreach (var p in w.Players)
        {
            switch (p.HoldAction)
            {
                case ActionId.HoldWash or ActionId.HoldFill or ActionId.HoldWashDishes:
                    a.SetLoopAt("loop_water", p.Position + new Vector3(0, 1, 0), 0.8f);
                    break;
                case ActionId.HoldStir or ActionId.HoldShred:
                    a.SetLoopAt("loop_stir", p.Position + new Vector3(0, 1, 0), 0.7f);
                    break;
            }
        }
    }

    // ── Cizim ───────────────────────────────────────────────────────
    public override void Draw3D()
    {
        var r = Game.Renderer;
        if (!Session.Ready)
        {
            r.Begin(_lastCam, SceneEnvironment.Default);
            r.Render(Game.ScreenWidth, Game.ScreenHeight);
            return;
        }

        var w = Session.World;
        var cam = Local.Camera(Game.Settings.Fov);
        _lastCam = cam;
        var env = DayNight.Compute(w, Game.Time);
        r.Begin(cam, env);
        World.Draw(w, cam, Session.LocalPlayerId, _dt);
        Local.DrawViewModel(Session, World, cam);
        TutorialMarker(w);
        r.Particles.Update(_dt);
        r.Render(Game.ScreenWidth, Game.ScreenHeight);
    }

    /// <summary>Egitim adiminin hedefi uzerinde suzulen isaret.</summary>
    private void TutorialMarker(GameWorld w)
    {
        if (!w.Progress.TutorialEnabled || w.Progress.TutorialStep >= Progression.TutorialSteps)
        {
            return;
        }

        Vector3? at = w.Progress.TutorialStep switch
        {
            1 => w.Items.FirstOrDefault(i => i.Type == ItemType.Suzgec) is { } s ? World.ItemPos(w, s) : null,
            2 => PartPos(w, "pantry", StationDefs.PartRice),
            3 => PartPos(w, "sink", StationDefs.PartFaucet),
            4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 => Progression.MainKazan(w) is { } k ? World.ItemPos(w, k) + new Vector3(0, 0.5f, 0) : null,
            15 or 16 or 18 => w.Carts.FirstOrDefault()?.Position + new Vector3(0, 1.5f, 0),
            17 => w.Layout.Spots.First(s => s.Id == "okul").Center + new Vector3(0, 1.2f, 0),
            _ => null,
        };
        if (w.Progress.TutorialStep == 6)
        {
            at = PartPos(w, "fridge", StationDefs.PartButter);
        }

        if (at is not { } pos)
        {
            return;
        }

        var bob = MathF.Sin(Game.Time * 3) * 0.08f;
        Game.Renderer.Submit(World.Models.Marker(), Matrix4x4.CreateRotationY(Game.Time * 2) * Matrix4x4.CreateTranslation(pos + new Vector3(0, 0.45f + bob, 0)), Color.White, DrawFlags.NoShadow, new Vector3(0.9f, 0.7f, 0.1f));
    }

    private static Vector3? PartPos(GameWorld w, string tag, byte part)
    {
        if (w.StationByTag(tag) is not { } s)
        {
            return null;
        }

        foreach (var pd in StationDefs.Parts(s))
        {
            if (pd.Id == part)
            {
                return GameWorld.PartWorld(s, pd) + new Vector3(0, pd.Half.Y + 0.15f, 0);
            }
        }

        return null;
    }

    public override void DrawUi()
    {
        var ui = Game.Ui;
        if (!Session.Ready)
        {
            ui.Panel(new Rectangle(0, 0, ui.Width, ui.Height), new Color(20, 14, 10, 230), 0);
            ui.Text(Loc.T(Session.Status.Length > 0 ? Session.Status : "net.connecting"), new Vector2(ui.Width / 2f, ui.Height / 2f), 40, Theme.Cream, true, Align.Center);
            return;
        }

        var top = Game.Screens.Top == this;
        Hud.Draw(Session, Local, _lastCam, showPrompts: top && !_chatOpen);
        if (_newsTimer > 0)
        {
            News(Session.World);
        }

        if (_chatOpen)
        {
            var r = new Rectangle(ui.S(24), ui.Height - ui.S(120), ui.S(700), ui.S(56));
            if (ui.TextField(r, ref _chatText, 80, Loc.T("chat.placeholder")))
            {
                if (_chatText.Trim().Length > 0)
                {
                    Session.SendAction(new ActionRequest { Action = ActionId.Chat, Text = _chatText.Trim() });
                }

                _chatOpen = false;
            }

            if (Game.Input.KeyPressed(KeyboardKey.Escape))
            {
                _chatOpen = false;
            }
        }
    }

    private void News(GameWorld w)
    {
        var ui = Game.Ui;
        var a = MathF.Min(1, _newsTimer);
        var lines = new List<string> { Loc.T("news.header", w.Clock.Day, Loc.T(DayLogic.DayKeys[w.Clock.DayOfWeek])) };
        foreach (var n in w.Events.News)
        {
            var parts = n.Split('|');
            lines.Add("• " + Loc.T(parts[0], parts.Length > 1 ? parts[1] : ""));
        }

        var width = ui.S(640);
        var r = new Rectangle(ui.Width / 2f - width / 2, ui.Height * 0.16f, width, ui.S(30) + lines.Count * ui.S(34));
        ui.Panel(r, Theme.HudBgStrong.WithAlpha(0.85f * a));
        var y = r.Y + ui.S(14);
        for (var i = 0; i < lines.Count; i++)
        {
            ui.Text(lines[i], new Vector2(r.X + ui.S(22), y), i == 0 ? 28 : 22, (i == 0 ? Theme.Yellow : Theme.Cream).WithAlpha(a), i == 0);
            y += ui.S(34);
        }
    }

    // ── Otomatik dogrulama ──────────────────────────────────────────
    public override string Annotate()
    {
        if (!Session.Ready)
        {
            return "oyun: hazir degil " + Session.Status;
        }

        var w = Session.World;
        var p = Session.LocalPlayer!;
        var held = w.HeldBy(p);
        var kazan = Progression.MainKazan(w);
        var opts = string.Join(",", Local.Options.Select(o => o.Action + (o.Enabled ? "" : "(x)")));
        return string.Create(CultureInfo.InvariantCulture,
            $"gun={w.Clock.Day} saat={w.Clock.Clock} para={w.Economy.Money} pos=({p.Position.X:F1},{p.Position.Y:F2},{p.Position.Z:F1}) el={held?.Type.ToString() ?? "-"} hedef={Local.Target.Kind}:{Local.Target.EntityId}:{Local.Target.Part} secenek=[{opts}] kazan={kazan?.Pot?.Food}/{kazan?.Pot?.Quality:F0} musteri={w.Customers.Count} siparis={w.Customers.Count(c => c.State == CustomerState.Ordered)} servis={w.Economy.Today.Served} odeme={w.Progress.Stat("payments")} egitim={w.Progress.TutorialStep} oyuncu={w.Players.Count}");
    }

    /// <summary>Gelistirici ve senaryo komutlari.</summary>
    public override bool Command(string[] a)
    {
        if (!Session.Ready)
        {
            return a[0] == "wait-ready";
        }

        var w = Session.World;
        var p = Session.LocalPlayer!;
        float F(int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
        switch (a[0])
        {
            case "tp":
                Local.Motor.Position = new Vector3(F(1), F(2), F(3));
                p.SetMotion(Local.Motor.Position, p.Yaw);
                if (a.Length > 4)
                {
                    Local.Yaw = F(4) * MathF.PI / 180f;
                }

                if (a.Length > 5)
                {
                    Local.Pitch = F(5) * MathF.PI / 180f;
                }

                return true;
            case "aim":
            {
                // Senaryo yardimcisi: kamerayi bir hedefe cevir.
                //   aim part <istasyon etiketi> <parca adi>   (orn. aim part cart plates)
                //   aim item <ItemType>                       (en yakin esya)
                //   aim socket <istasyon etiketi> <yuva>       (orn. aim socket cart 0)
                //   aim customer                              (en yakin musteri)
                Vector3? target = a[1] switch
                {
                    "part" when w.StationByTag(a[2]) is { } st => StationDefs.Parts(st).Where(pd => pd.NameKey == "part." + a[3]).Select(pd => (Vector3?)GameWorld.PartWorld(st, pd)).FirstOrDefault(),
                    "item" => w.Items.Where(i => i.Type.ToString().Equals(a[2], StringComparison.OrdinalIgnoreCase) && i.Attach != Attach.Held)
                        .Select(i => World.ItemPos(w, i)).OrderBy(pos => Vector3.DistanceSquared(pos, p.Position)).Select(pos => (Vector3?)pos).FirstOrDefault(),
                    "socket" when w.StationByTag(a[2]) is { } st2 => StationDefs.Sockets(st2).Where(so => so.Index == int.Parse(a[3], CultureInfo.InvariantCulture))
                        .Select(so => (Vector3?)(GameWorld.SocketWorld(st2, so) + new Vector3(0, 0.08f, 0))).FirstOrDefault(),
                    "customer" => w.Customers.OrderBy(c => Vector3.DistanceSquared(c.Position, p.Position)).Select(c => (Vector3?)(c.Position + new Vector3(0, 1.1f, 0))).FirstOrDefault(),
                    _ => null,
                };
                if (target is not { } tgt)
                {
                    return false;
                }

                var eye = Local.Camera(Game.Settings.Fov).Position;
                var d = Vector3.Normalize(tgt - eye);
                Local.Yaw = MathF.Atan2(-d.X, -d.Z);
                Local.Pitch = MathF.Asin(Math.Clamp(d.Y, -1f, 1f));
                return true;
            }
            case "stand-cart":
            {
                // Senaryo yardimcisi: arabanin satici tarafina gec, tezgaha bak.
                var cart = w.Carts.First();
                Local.Motor.Position = Entity.LocalToWorld(cart.Position, cart.Yaw, new Vector3(0, 0, 1.35f)) with { Y = cart.Position.Y };
                p.SetMotion(Local.Motor.Position, p.Yaw);
                Local.Yaw = cart.Yaw;
                Local.Pitch = -0.3f;
                return true;
            }
            case "look":
                Local.Yaw = F(1) * MathF.PI / 180f;
                Local.Pitch = F(2) * MathF.PI / 180f;
                return true;
            case "time" when Session.IsHost:
            {
                var parts = a[1].Split(':');
                w.Clock.Minute = int.Parse(parts[0]) * 60 + (parts.Length > 1 ? int.Parse(parts[1]) : 0);
                w.GlobalsDirty = true;
                return true;
            }
            case "money" when Session.IsHost:
                w.Economy.Money = long.Parse(a[1]);
                return true;
            case "xp" when Session.IsHost:
                Progression.AddXp(w, int.Parse(a[1]));
                return true;
            case "rain" when Session.IsHost:
                w.Weather.Kind = WeatherKind.Rain;
                w.Weather.RainStart = 0;
                w.Weather.RainEnd = 1440;
                w.Weather.Rain = 1;
                w.Weather.Cloud = 0.95f;
                return true;
            case "tutorial" when Session.IsHost:
                w.Progress.TutorialEnabled = a[1] == "on";
                if (a.Length > 2)
                {
                    w.Progress.TutorialStep = int.Parse(a[2]);
                }

                return true;
            case "upgrade" when Session.IsHost:
                w.Economy.Money += 1_000_000;
                w.Progress.Xp = Math.Max(w.Progress.Xp, 20000);
                return EconomyLogic.BuyUpgrade(w, a[1]);
            case "cook" when Session.IsHost:
            {
                // Hizli test: ana kazani hazir pilavla doldur.
                var k = Progression.MainKazan(w)!;
                var pot = k.Pot!;
                pot.Clear();
                pot.RiceKg = 3;
                pot.RiceWash = 1;
                pot.ButterG = 300;
                pot.Toast = 0.9f;
                pot.WaterAbsorbed = 4.4f;
                pot.WaterAdded = 4.5f;
                pot.SaltG = 42;
                pot.Rest = 15;
                pot.Temp = 80;
                Sim.Cooking.CookingModel.UpdateResult(pot);
                k.MarkState();
                return true;
            }
            case "cart" when Session.IsHost:
            {
                // Arabayi bir noktaya koy ve ac
                var cart = w.Carts.First();
                var zone = w.Layout.Spot(a[1]);
                cart.SetMotion(zone.CartPos, zone.CartYaw);
                CartLogic.UpdateSpot(w, cart);
                CartLogic.SetOpen(w, cart, a.Length < 3 || a[2] != "closed");
                if (a.Length > 2 && a[2] == "kazan" || a.Length > 3)
                {
                    var k = Progression.MainKazan(w)!;
                    k.Attach = Attach.Socket;
                    k.ParentId = cart.Id;
                    k.SocketIndex = 0;
                    k.MarkState();
                }

                return true;
            }
            case "kazan-to-cart" when Session.IsHost:
            {
                var k = Progression.MainKazan(w)!;
                k.Attach = Attach.Socket;
                k.ParentId = w.Carts.First().Id;
                k.SocketIndex = 0;
                k.MarkState();
                return true;
            }
            case "customers" when Session.IsHost:
            {
                // Arabanin onune n musteri dogur
                var n = int.Parse(a[1]);
                var cart = w.Carts.First();
                for (var i = 0; i < n; i++)
                {
                    var type = w.Data.Customers[i % w.Data.Customers.Count].Id;
                    var c = w.Spawn(new CustomerEntity
                    {
                        TypeId = type, Seed = (uint)(1000 + i * 77), Position = StationDefs.QueueSlot(cart, i), Speed = 1.3f, State = CustomerState.Approach,
                        CartId = cart.Id, SpotId = cart.Cart!.Spot, Patience = 1,
                    });
                    c.Yaw = cart.Yaw;
                }

                return true;
            }
            case "endday" when Session.IsHost:
                DayLogic.EndDay(w, false);
                return true;
            case "act":
            {
                // Hedefe bir eylem uygula: act Interact|Secondary|Use
                var slot = Enum.Parse<InputSlot>(a[1]);
                var opt = Local.Options.FirstOrDefault(o => o.Slot == slot && o.Enabled);
                if (opt.Action == ActionId.None)
                {
                    return false;
                }

                Session.SendAction(ActionRequest.From(opt.Action, Local.Target));
                return true;
            }
            case "hold":
            {
                var opt = Local.Options.FirstOrDefault(o => o.Slot is InputSlot.HoldInteract or InputSlot.HoldUse && o.Enabled);
                if (opt.Action == ActionId.None)
                {
                    return false;
                }

                p.HoldAction = opt.Action;
                p.HoldTargetId = Local.Target.EntityId;
                p.HoldPart = Local.Target.Part;
                return true;
            }
            case "fov":
                Game.Settings.Fov = F(1);
                return true;
        }

        return false;
    }
}
