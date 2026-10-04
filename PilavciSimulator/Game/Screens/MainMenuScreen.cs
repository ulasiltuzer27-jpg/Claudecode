using System.Globalization;
using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Persistence;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Screens;

/// <summary>
/// Ana menu. Arka planda mahallenin kendisi yasar: oyuncusuz bir dunya
/// simule edilir (trafik, vapur, martilar, yoldan gecenler), iskelede acik
/// bir pilav arabasi durur ve kamera gun batiminda planlar arasinda gezer.
/// </summary>
public sealed class MainMenuScreen : Screen
{
    private enum Page
    {
        Main,
        NewGame,
        Load,
        ConfirmNew,
        Join,
        Credits,
    }

    private readonly string? _message;
    private Page _page;
    private GameWorld _world = null!;
    private Simulation _sim = null!;
    private float _t;
    private float _fade = 1f;
    private float _shotTime;
    private int _shot;
    private int _selectedSlot;
    private bool _tutorial = true;
    private int _confirmDelete = -1;
    private string _address = "127.0.0.1";
    private string _port = "27015";
    private string? _error;
    private SaveMeta?[] _metas = new SaveMeta?[SaveSystem.SlotCount];

    public MainMenuScreen(string? message = null) => _message = message;

    public override void Enter()
    {
        Game.Session = null;
        Game.WorldView.ResetSession();
        Game.WorldView.Upload(Game.Layout);
        Game.Renderer.Fade = 1f;
        Game.Renderer.Tired = 0f;
        Game.Ui.ResetFocus();
        _port = Game.Options.Port.ToString(CultureInfo.InvariantCulture);
        RefreshSlots();
        BuildBackground();
        Game.Platform.SetRichPresence("status", Loc.T("presence.menu"));
        if (_message is not null)
        {
            Game.Audio.Play("ui_error");
        }
    }

    public override void Exit()
    {
        Game.Audio.ResetLoopTargets();
    }

    private void RefreshSlots()
    {
        for (var i = 0; i < SaveSystem.SlotCount; i++)
        {
            _metas[i] = SaveSystem.Meta(i);
        }
    }

    /// <summary>Arka plan dunyasi: iskelede acik araba, sicak pilav, gun batimi.</summary>
    private void BuildBackground()
    {
        _world = new GameWorld(Game.Data, Game.Layout, isHost: true, seed: 7);
        Simulation.InitNewGame(_world);
        _world.Progress.TutorialEnabled = false;
        _world.Events.News.Clear();
        _world.Weather.Kind = WeatherKind.Clear;
        _world.Weather.Rain = 0;
        _world.Weather.Cloud = 0.25f;
        _world.Clock.Minute = MenuMinute(0);
        _sim = new Simulation(_world);

        var cart = _world.Carts.First();
        var zone = _world.Layout.Spot("iskele");
        cart.SetMotion(zone.CartPos, zone.CartYaw);
        CartLogic.UpdateSpot(_world, cart);
        CartLogic.SetOpen(_world, cart, true);
        if (Progression.MainKazan(_world) is { Pot: { } pot } k)
        {
            pot.Clear();
            pot.RiceKg = 3;
            pot.RiceWash = 1;
            pot.ButterG = 300;
            pot.Toast = 0.9f;
            pot.WaterAbsorbed = 4.4f;
            pot.WaterAdded = 4.5f;
            pot.SaltG = 42;
            pot.Rest = 15;
            pot.Temp = 85;
            CookingModel.UpdateResult(pot);
            k.Attach = Attach.Socket;
            k.ParentId = cart.Id;
            k.SocketIndex = 0;
        }

        // Arabanin onu bos kalmasin: birkac musteri isinsin.
        _world.Economy.Money = 0;
        for (var i = 0; i < 40 * 30; i++)
        {
            _sim.Tick(1 / 30f);
            _world.Clock.Minute = MenuMinute(0);
        }
    }

    private static float MenuMinute(float t) => 18 * 60 + 25 + MathF.Min(t * 0.25f, 80f);

    // ── Guncelleme ──────────────────────────────────────────────────
    public override void Update(float dt, bool focused)
    {
        _t += dt;
        _shotTime += dt;
        _sim.Tick(dt);
        _world.Clock.Minute = MenuMinute(_t);
        if (_world.DayOver)
        {
            // Olmamali (saat sabit) ama ne olur ne olmaz.
            BuildBackground();
        }

        foreach (var c in _world.Customers)
        {
            // Arka plan musterileri sinirlenip gitmesin.
            c.Patience = 1f;
        }

        if (_shotTime > ShotLength - 1.2f)
        {
            _fade = MathF.Min(1, _fade + dt / 1.2f);
        }
        else
        {
            _fade = MathF.Max(0, _fade - dt / 1.5f);
        }

        if (_shotTime > ShotLength)
        {
            _shotTime = 0;
            _shot = (_shot + 1) % 3;
        }

        Game.Renderer.Fade = _fade;

        var a = Game.Audio;
        var cam = Camera();
        a.ListenerPosition = cam.Position;
        a.ListenerForward = cam.Forward;
        a.ResetLoopTargets();
        a.SetLoop("amb_city", 0.35f);
        a.SetLoop("amb_sea", 0.45f);

        if (!focused)
        {
            return;
        }

        if (Game.Input.MenuBack && !Game.Ui.IsEditingText)
        {
            if (_page == Page.Main)
            {
                return;
            }

            _page = _page == Page.ConfirmNew ? Page.NewGame : Page.Main;
            _confirmDelete = -1;
            _error = null;
            Game.Ui.ResetFocus();
            Game.Audio.Play("ui_close", 0.5f);
        }
    }

    private const float ShotLength = 15f;

    private CameraView Camera()
    {
        var u = _shotTime / ShotLength;
        var cart = _world.Carts.First();
        Vector3 pos, look;
        switch (_shot)
        {
            case 0:
            {
                // Arabanin etrafinda yavas yay
                var c = cart.Position + new Vector3(0, 1.0f, 0);
                var ang = -0.9f + u * 1.1f;
                pos = c + new Vector3(MathF.Sin(ang) * 6.5f, 1.4f, MathF.Cos(ang) * 6.5f);
                look = c + new Vector3(0, -0.1f, 0);
                break;
            }
            case 1:
            {
                // Kaldirim boyunca, binalara bakarak
                pos = new Vector3(40 + u * 26, 3.4f, 7.5f);
                look = pos + new Vector3(4, -1.2f, -10);
                break;
            }
            default:
            {
                // Iskeleden denize: vapur, karsi kiyi, minareler
                pos = new Vector3(96 - u * 8, 4.2f, 20 + u * 6);
                look = pos + new Vector3(-6, -0.6f, 14);
                break;
            }
        }

        var cam = new CameraView { Position = pos, FovDegrees = 62, Near = 0.08f, Far = 900 };
        var dir = Vector3.Normalize(look - pos);
        cam.Yaw = MathF.Atan2(-dir.X, -dir.Z);
        cam.Pitch = MathF.Asin(dir.Y);
        return cam;
    }

    public override void Draw3D()
    {
        var cam = Camera();
        var env = DayNight.Compute(_world, Game.Time);
        var r = Game.Renderer;
        r.Begin(cam, env);
        Game.WorldView.Draw(_world, cam, -1, Raylib.GetFrameTime());
        r.Particles.Update(Math.Min(Raylib.GetFrameTime(), 0.05f));
        r.Render(Game.ScreenWidth, Game.ScreenHeight);
    }

    // ── Arayuz ──────────────────────────────────────────────────────
    public override void DrawUi()
    {
        var ui = Game.Ui;

        // Sol tarafta okunurluk icin yumusak karartma
        Raylib.DrawRectangleGradientH(0, 0, (int)ui.S(900), ui.Height, new Color(16, 10, 6, 215), new Color(16, 10, 6, 0));

        var x = ui.S(90);
        ui.Text(Loc.T("game.title"), new Vector2(x, ui.S(70)), 84, Theme.Cream, true, shadow: true);
        ui.Text(Loc.T("menu.subtitle"), new Vector2(x + ui.S(4), ui.S(168)), 28, Theme.Yellow, false, shadow: true);

        if (_message is not null && _page == Page.Main)
        {
            var mr = new Rectangle(x, ui.Height - ui.S(150), ui.S(760), ui.S(70));
            ui.Panel(mr, Theme.Red.WithAlpha(0.85f));
            ui.TextIn(mr, _message, 24, Theme.White, true);
        }

        switch (_page)
        {
            case Page.Main:
                MainPage(x);
                break;
            case Page.NewGame:
            case Page.Load:
                SlotsPage(x);
                break;
            case Page.ConfirmNew:
                ConfirmNewPage(x);
                break;
            case Page.Join:
                JoinPage(x);
                break;
            case Page.Credits:
                CreditsPage(x);
                break;
        }

        var version = "v" + (typeof(MainMenuScreen).Assembly.GetName().Version?.ToString(3) ?? "0.1.0") + (Game.Platform.IsSteam ? " · Steam" : "");
        ui.Text(version, new Vector2(ui.Width - ui.S(20), ui.Height - ui.S(36)), 18, Theme.Cream.WithAlpha(0.6f), false, Align.Right);
    }

    private void Go(Page p)
    {
        _page = p;
        _error = null;
        _confirmDelete = -1;
        Game.Ui.ResetFocus();
        RefreshSlots();
    }

    private int LatestSlot()
    {
        var best = -1;
        string? bestAt = null;
        for (var i = 0; i < _metas.Length; i++)
        {
            if (_metas[i] is { } m && (bestAt is null || string.CompareOrdinal(m.SavedAt, bestAt) > 0))
            {
                best = i;
                bestAt = m.SavedAt;
            }
        }

        return best;
    }

    private void MainPage(float x)
    {
        var ui = Game.Ui;
        var y = ui.S(270);
        var w = ui.S(440);
        var h = ui.S(66);
        Rectangle Row()
        {
            var r = new Rectangle(x, y, w, h);
            y += h + ui.S(14);
            return r;
        }

        var latest = LatestSlot();
        if (latest >= 0)
        {
            var m = _metas[latest]!;
            if (ui.Button(Row(), Loc.T("menu.continue"), ButtonStyle.Primary))
            {
                LoadSlot(latest);
            }

            ui.Text(Loc.T("menu.slot_summary", m.Day, Fmt.Money(m.Money), m.Level), new Vector2(x + w + ui.S(20), y - h - ui.S(14) + ui.S(20)), 22, Theme.Cream, false, shadow: true);
        }

        if (ui.Button(Row(), Loc.T("menu.new_game"), latest >= 0 ? ButtonStyle.Normal : ButtonStyle.Primary))
        {
            Go(Page.NewGame);
        }

        if (ui.Button(Row(), Loc.T("menu.load_game"), ButtonStyle.Normal, _metas.Any(m => m is not null)))
        {
            Go(Page.Load);
        }

        if (ui.Button(Row(), Loc.T("menu.join")))
        {
            Go(Page.Join);
        }

        if (ui.Button(Row(), Loc.T("menu.settings")))
        {
            Game.Screens.Push(new SettingsScreen());
        }

        if (ui.Button(Row(), Loc.T("menu.credits")))
        {
            Go(Page.Credits);
        }

        if (ui.Button(Row(), Loc.T("menu.quit"), ButtonStyle.Danger))
        {
            Game.QuitRequested = true;
        }
    }

    private void SlotsPage(float x)
    {
        var ui = Game.Ui;
        var isNew = _page == Page.NewGame;
        var y = ui.S(250);
        ui.Text(Loc.T(isNew ? "menu.choose_slot_new" : "menu.choose_slot_load"), new Vector2(x, y), 32, Theme.Cream, true, shadow: true);
        y += ui.S(60);
        var w = ui.S(640);
        for (var i = 0; i < SaveSystem.SlotCount; i++)
        {
            var m = _metas[i];
            var r = new Rectangle(x, y, w, ui.S(112));
            ui.Panel(r, Theme.HudBgStrong);
            ui.Text(Loc.T("menu.slot", i + 1), new Vector2(r.X + ui.S(22), r.Y + ui.S(14)), 28, Theme.Yellow, true);
            if (m is not null)
            {
                ui.Text(Loc.T("menu.slot_summary", m.Day, Fmt.Money(m.Money), m.Level) + "  ·  " + Stars(m.Stars), new Vector2(r.X + ui.S(22), r.Y + ui.S(52)), 22, Theme.Cream);
                ui.Text(m.SavedAt, new Vector2(r.X + ui.S(22), r.Y + ui.S(80)), 18, Theme.CreamDark);
            }
            else
            {
                ui.Text(Loc.T("menu.slot_empty"), new Vector2(r.X + ui.S(22), r.Y + ui.S(56)), 22, Theme.CreamDark);
            }

            var bx = r.X + r.Width - ui.S(200);
            if (_confirmDelete == i)
            {
                if (ui.Button(new Rectangle(bx - ui.S(150), r.Y + ui.S(28), ui.S(160), ui.S(56)), Loc.T("menu.delete_yes"), ButtonStyle.Danger, true, 22))
                {
                    SaveSystem.Delete(i);
                    _confirmDelete = -1;
                    RefreshSlots();
                }

                if (ui.Button(new Rectangle(bx + ui.S(20), r.Y + ui.S(28), ui.S(160), ui.S(56)), Loc.T("common.cancel"), ButtonStyle.Normal, true, 22))
                {
                    _confirmDelete = -1;
                }
            }
            else
            {
                var label = isNew ? Loc.T(m is null ? "menu.start" : "menu.overwrite") : Loc.T("menu.load");
                if (ui.Button(new Rectangle(bx, r.Y + ui.S(28), ui.S(180), ui.S(56)), label, ButtonStyle.Primary, isNew || m is not null, 22))
                {
                    if (isNew)
                    {
                        _selectedSlot = i;
                        Go(Page.ConfirmNew);
                    }
                    else
                    {
                        LoadSlot(i);
                    }
                }

                if (m is not null && ui.Button(new Rectangle(bx - ui.S(70), r.Y + ui.S(28), ui.S(56), ui.S(56)), "×", ButtonStyle.Ghost, true, 30))
                {
                    _confirmDelete = i;
                }
            }

            y += ui.S(128);
        }

        if (_error is not null)
        {
            ui.Text(_error, new Vector2(x, y), 24, Theme.Red, true, shadow: true);
            y += ui.S(40);
        }

        if (ui.Button(new Rectangle(x, y + ui.S(10), ui.S(220), ui.S(60)), Loc.T("common.back")))
        {
            Go(Page.Main);
        }
    }

    private static string Stars(float s) => Loc.T("menu.slot_rep", s.ToString("0.0", CultureInfo.InvariantCulture));

    private void ConfirmNewPage(float x)
    {
        var ui = Game.Ui;
        var y = ui.S(250);
        var w = ui.S(640);
        ui.Text(Loc.T("menu.new_game"), new Vector2(x, y), 32, Theme.Cream, true, shadow: true);
        y += ui.S(64);
        var panel = new Rectangle(x, y, w, ui.S(330));
        ui.Panel(panel, Theme.HudBgStrong);
        var py = y + ui.S(24);
        py += ui.Paragraph(Loc.T("menu.new_game_intro"), new Vector2(x + ui.S(24), py), 22, w - ui.S(48), Theme.Cream) + ui.S(16);
        ui.Toggle(new Rectangle(x + ui.S(16), py, w - ui.S(32), ui.S(56)), Loc.T("menu.tutorial"), ref _tutorial);
        py += ui.S(70);
        if (_metas[_selectedSlot] is not null)
        {
            ui.Paragraph(Loc.T("menu.overwrite_warning", _selectedSlot + 1), new Vector2(x + ui.S(24), py), 20, w - ui.S(48), Theme.Yellow);
        }

        y += ui.S(350);
        if (ui.Button(new Rectangle(x, y, ui.S(300), ui.S(64)), Loc.T("menu.start"), ButtonStyle.Primary))
        {
            GameFlow.StartNew(Game, _selectedSlot, _tutorial);
        }

        if (ui.Button(new Rectangle(x + ui.S(320), y, ui.S(220), ui.S(64)), Loc.T("common.back")))
        {
            Go(Page.NewGame);
        }
    }

    private void LoadSlot(int slot)
    {
        if (GameFlow.Load(Game, slot, out var err) is null)
        {
            _error = Loc.T(err ?? "menu.load_failed");
            Game.Audio.Play("ui_error");
        }
    }

    private void JoinPage(float x)
    {
        var ui = Game.Ui;
        var y = ui.S(250);
        var w = ui.S(700);
        ui.Text(Loc.T("menu.join"), new Vector2(x, y), 32, Theme.Cream, true, shadow: true);
        y += ui.S(64);
        var panel = new Rectangle(x, y, w, ui.S(Game.Platform.IsSteam ? 400 : 300));
        ui.Panel(panel, Theme.HudBgStrong);
        var py = y + ui.S(22);
        if (Game.Platform.IsSteam)
        {
            py += ui.Paragraph(Loc.T("menu.join_steam"), new Vector2(x + ui.S(24), py), 22, w - ui.S(48), Theme.Cream) + ui.S(18);
        }

        py += ui.Paragraph(Loc.T("menu.join_ip"), new Vector2(x + ui.S(24), py), 22, w - ui.S(48), Theme.CreamDark) + ui.S(16);
        var row = new Rectangle(x + ui.S(24), py, w - ui.S(48), ui.S(56));
        ui.TextIn(row with { Width = ui.S(170) }, Loc.T("menu.address"), 24, Theme.Cream);
        ui.TextField(row with { X = row.X + ui.S(170), Width = row.Width - ui.S(170) }, ref _address, 64, "192.168.1.20");
        py += ui.S(70);
        row = new Rectangle(x + ui.S(24), py, w - ui.S(48), ui.S(56));
        ui.TextIn(row with { Width = ui.S(170) }, Loc.T("coop.port"), 24, Theme.Cream);
        ui.TextField(row with { X = row.X + ui.S(170), Width = ui.S(180) }, ref _port, 5, "27015");
        py += ui.S(76);
        ui.Text(Loc.T("menu.join_as", Game.PlayerName), new Vector2(x + ui.S(24), py), 20, Theme.CreamDark);

        y += panel.Height + ui.S(20);
        if (_error is not null)
        {
            ui.Text(_error, new Vector2(x, y), 22, Theme.Red, true, shadow: true);
            y += ui.S(40);
        }

        if (ui.Button(new Rectangle(x, y, ui.S(300), ui.S(64)), Loc.T("menu.connect"), ButtonStyle.Primary))
        {
            if (_address.Trim().Length == 0 || !int.TryParse(_port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
            {
                _error = Loc.T("menu.bad_address");
            }
            else
            {
                GameFlow.JoinIp(Game, _address.Trim(), port);
            }
        }

        if (ui.Button(new Rectangle(x + ui.S(320), y, ui.S(220), ui.S(64)), Loc.T("common.back")))
        {
            Go(Page.Main);
        }
    }

    private void CreditsPage(float x)
    {
        var ui = Game.Ui;
        var y = ui.S(250);
        var w = ui.S(760);
        ui.Text(Loc.T("menu.credits"), new Vector2(x, y), 32, Theme.Cream, true, shadow: true);
        y += ui.S(64);
        var panel = new Rectangle(x, y, w, ui.S(340));
        ui.Panel(panel, Theme.HudBgStrong);
        ui.Paragraph(Loc.T("credits.body"), new Vector2(x + ui.S(26), y + ui.S(24)), 22, w - ui.S(52), Theme.Cream);
        if (ui.Button(new Rectangle(x, y + panel.Height + ui.S(20), ui.S(220), ui.S(60)), Loc.T("common.back")))
        {
            Go(Page.Main);
        }
    }

    // ── Otomatik dogrulama ──────────────────────────────────────────
    public override string Annotate()
    {
        var slots = string.Join(",", _metas.Select(m => m is null ? "-" : "gun" + m.Day));
        return $"menu: sayfa={_page} yuvalar=[{slots}] musteri={_world.Customers.Count} arac={_world.Vehicles.Count} plan={_shot}";
    }

    public override bool Command(string[] a)
    {
        switch (a[0])
        {
            case "menu-page":
                Go(Enum.Parse<Page>(a[1], ignoreCase: true));
                return true;
            case "menu-shot":
                _shot = int.Parse(a[1], CultureInfo.InvariantCulture) % 3;
                _shotTime = a.Length > 2 ? float.Parse(a[2], CultureInfo.InvariantCulture) : 3f;
                _fade = 0;
                return true;
            case "menu-new":
                GameFlow.StartNew(Game, int.Parse(a[1], CultureInfo.InvariantCulture), a.Length < 3 || a[2] != "notutorial");
                return true;
            case "menu-load":
                LoadSlot(int.Parse(a[1], CultureInfo.InvariantCulture));
                return true;
            case "menu-join":
                GameFlow.JoinIp(Game, a[1], a.Length > 2 ? int.Parse(a[2], CultureInfo.InvariantCulture) : Game.Options.Port);
                return true;
        }

        return false;
    }
}
