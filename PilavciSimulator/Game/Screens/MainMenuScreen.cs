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
        Logo(new Vector2(x, ui.S(52)));

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

    /// <summary>Esnaf tabelasi gibi logo: ahsap pano, kazan ikonu ve tutup cikan buhar, alt kurdele.</summary>
    private void Logo(Vector2 at)
    {
        var ui = Game.Ui;
        var title = Loc.T("game.title");
        var tw = ui.Measure(title, 72, true).X;
        var r = new Rectangle(at.X, at.Y, tw + ui.S(170), ui.S(130));
        // Asili zincirler
        foreach (var cx in new[] { r.X + ui.S(60), r.X + r.Width - ui.S(60) })
        {
            for (var k = 0; k < 4; k++)
            {
                Raylib.DrawRing(new Vector2(cx, at.Y - ui.S(10) - k * ui.S(12)), ui.S(4), ui.S(6.5f), 0, 360, 12, new Color(70, 60, 50, 220));
            }
        }

        ui.SignBoard(r, "", Icon.None);
        Icons.Draw(Icon.Kazan, new Vector2(r.X + ui.S(72), r.Y + ui.S(70)), ui.S(84), Theme.Cream, Theme.Primary);
        for (var i = 0; i < 3; i++)
        {
            var ph = (_t * 0.6f + i * 0.33f) % 1f;
            var sx = r.X + ui.S(52 + i * 20) + MathF.Sin(_t * 2 + i) * ui.S(4);
            Raylib.DrawCircleV(new Vector2(sx, r.Y + ui.S(30) - ph * ui.S(40)), ui.S(5 + ph * 6), new Color(255, 255, 255, (int)(120 * (1 - ph))));
        }

        ui.TextOutlined(title, new Vector2(r.X + ui.S(138), r.Y + ui.S(22)), 72, Theme.Cream, new Color(60, 30, 10, 230), true, thickness: 3);
        // Kurdele
        var sub = Loc.T("menu.subtitle");
        var sw = ui.Measure(sub, 26, true).X + ui.S(60);
        var rib = new Rectangle(r.X + ui.S(120), r.Y + r.Height - ui.S(14), sw, ui.S(42));
        Raylib.DrawTriangle(new Vector2(rib.X - ui.S(18), rib.Y + ui.S(4)), new Vector2(rib.X, rib.Y + rib.Height + ui.S(4)), new Vector2(rib.X, rib.Y + ui.S(4)), Theme.PrimaryDark);
        Raylib.DrawTriangle(new Vector2(rib.X + rib.Width, rib.Y + ui.S(4)), new Vector2(rib.X + rib.Width, rib.Y + rib.Height + ui.S(4)), new Vector2(rib.X + rib.Width + ui.S(18), rib.Y + ui.S(4)), Theme.PrimaryDark);
        ui.SoftShadow(rib, 6, 6, 90);
        Raylib.DrawRectangleRec(rib, Theme.Primary);
        ui.TextIn(rib, sub, 26, Theme.White, true, Align.Center, 0);
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
        var y = ui.S(280);
        var w = ui.S(440);
        var h = ui.S(64);
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
            if (ui.Button(Row(), Loc.T("menu.continue"), ButtonStyle.Primary, true, 28, Icon.Play))
            {
                LoadSlot(latest);
            }

            // Son kaydin ozet karti
            var card = new Rectangle(x + w + ui.S(20), y - h - ui.S(14), ui.S(330), h);
            ui.Card(card, Theme.Primary, dark: true);
            ui.IconText(Icon.Calendar, Loc.T("hud.day", m.Day, ""), new Vector2(card.X + ui.S(22), card.Y + ui.S(8)), 20, Theme.Cream, Theme.Yellow, true);
            ui.IconText(Icon.Money, Fmt.Money(m.Money), new Vector2(card.X + ui.S(22), card.Y + ui.S(34)), 20, Theme.Yellow, null, true);
            Client.Hud.DrawStar(new Vector2(card.X + card.Width - ui.S(80), card.Y + ui.S(32)), ui.S(12), Theme.Yellow);
            ui.Text(m.Stars.ToString("0.0", CultureInfo.InvariantCulture), new Vector2(card.X + card.Width - ui.S(62), card.Y + ui.S(20)), 22, Theme.Cream, true);
        }

        if (ui.Button(Row(), Loc.T("menu.new_game"), latest >= 0 ? ButtonStyle.Normal : ButtonStyle.Primary, true, 28, Icon.Plus))
        {
            Go(Page.NewGame);
        }

        if (ui.Button(Row(), Loc.T("menu.load_game"), ButtonStyle.Normal, _metas.Any(m => m is not null), 28, Icon.Save, _metas.Any(m => m is not null) ? null : Loc.T("menu.no_saves")))
        {
            Go(Page.Load);
        }

        if (ui.Button(Row(), Loc.T("menu.join"), ButtonStyle.Normal, true, 28, Icon.Group))
        {
            Go(Page.Join);
        }

        if (ui.Button(Row(), Loc.T("menu.settings"), ButtonStyle.Normal, true, 28, Icon.Gear))
        {
            Game.Screens.Push(new SettingsScreen());
        }

        if (ui.Button(Row(), Loc.T("menu.credits"), ButtonStyle.Normal, true, 28, Icon.Info))
        {
            Go(Page.Credits);
        }

        if (ui.Button(Row(), Loc.T("menu.quit"), ButtonStyle.Danger, true, 28, Icon.Exit))
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
            ui.Card(r, m is not null ? Theme.Primary : new Color(120, 110, 100, 255), dark: true);
            // Yuva numarasi rozeti
            var badge = new Vector2(r.X + ui.S(52), r.Y + r.Height / 2);
            Raylib.DrawCircleV(badge, ui.S(30), m is not null ? Theme.Primary : new Color(255, 255, 255, 30));
            ui.TextOutlined((i + 1).ToString(CultureInfo.InvariantCulture), new Vector2(badge.X, badge.Y - ui.S(20)), 34, Theme.White, new Color(0, 0, 0, 120), true, Align.Center);
            var tx = r.X + ui.S(100);
            if (m is not null)
            {
                ui.Text(m.Name.Length > 0 ? m.Name : Loc.T("menu.slot", i + 1), new Vector2(tx, r.Y + ui.S(12)), 24, Theme.Yellow, true);
                var cx = tx;
                cx += ui.IconText(Icon.Calendar, Loc.T("hud.day", m.Day, "").TrimEnd(' ', '·'), new Vector2(cx, r.Y + ui.S(46)), 20, Theme.Cream, Theme.CreamDark) + ui.S(18);
                cx += ui.IconText(Icon.Money, Fmt.Money(m.Money), new Vector2(cx, r.Y + ui.S(46)), 20, Theme.Cream, Theme.Yellow) + ui.S(18);
                cx += ui.IconText(Icon.Xp, Loc.T("hud.level", m.Level), new Vector2(cx, r.Y + ui.S(46)), 20, Theme.Cream, Theme.Green) + ui.S(18);
                for (var k = 0; k < 5; k++)
                {
                    Client.Hud.DrawStar(new Vector2(tx + ui.S(10 + k * 24), r.Y + ui.S(88)), ui.S(9), k < (int)MathF.Round(m.Stars) ? Theme.Yellow : new Color(255, 255, 255, 50));
                }

                ui.Text(m.SavedAt, new Vector2(tx + ui.S(140), r.Y + ui.S(78)), 18, Theme.CreamDark);
            }
            else
            {
                ui.Text(Loc.T("menu.slot", i + 1), new Vector2(tx, r.Y + ui.S(20)), 24, Theme.CreamDark, true);
                ui.Text(Loc.T("menu.slot_empty"), new Vector2(tx, r.Y + ui.S(58)), 22, Theme.CreamDark);
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

                if (m is not null && ui.Button(new Rectangle(bx - ui.S(70), r.Y + ui.S(28), ui.S(56), ui.S(56)), "", ButtonStyle.Ghost, true, 26, Icon.Cross, Loc.T("menu.delete_tip")))
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
        ui.Card(panel, Theme.Primary, dark: true);
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
        ui.Card(panel, Theme.Primary, dark: true);
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
        ui.Card(panel, Theme.Primary, dark: true);
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
