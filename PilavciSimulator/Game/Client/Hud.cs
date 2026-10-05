using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Net;
using PilavciSimulator.Sim;
using PilavciSimulator.Sim.Cooking;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Client;

/// <summary>
/// Oyun ici arayuz. Okunurluk icin yari saydam koyu paneller ve golgeli
/// yazi; 3B sahnenin uzerinde her isikta okunabilsin.
/// </summary>
public sealed class Hud
{
    private readonly PilavciGame _game;
    private readonly List<(string Text, Vector3 Pos, float Age, Color Color)> _popups = new();
    private readonly List<(string Name, string Text, float Age)> _chat = new();
    private float _levelFlash;
    private int _lastStep = -1;
    private float _stepFlash;

    public Hud(PilavciGame game) => _game = game;

    public void Popup(string text, Vector3 world, Color color) => _popups.Add((text, world, 0, color));

    public void Chat(string name, string text)
    {
        _chat.Add((name, text, 0));
        if (_chat.Count > 6)
        {
            _chat.RemoveAt(0);
        }
    }

    public void LevelFlash() => _levelFlash = 2.5f;

    public void Update(float dt)
    {
        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var p = _popups[i];
            p.Age += dt;
            if (p.Age > 1.6f)
            {
                _popups.RemoveAt(i);
            }
            else
            {
                _popups[i] = p;
            }
        }

        for (var i = _chat.Count - 1; i >= 0; i--)
        {
            var c = _chat[i];
            c.Age += dt;
            if (c.Age > 14f)
            {
                _chat.RemoveAt(i);
            }
            else
            {
                _chat[i] = c;
            }
        }

        _levelFlash = MathF.Max(0, _levelFlash - dt);
        _stepFlash = MathF.Max(0, _stepFlash - dt);
    }

    public void Draw(GameSession session, LocalPlayer local, in CameraView cam, bool showPrompts)
    {
        var ui = _game.Ui;
        var w = session.World;
        var sw = ui.Width;
        var sh = ui.Height;

        DrawWorldLabels(session, cam);
        var y = StatusBar(w);
        y = LateWarning(w, y);
        Tutorial(w, y);
        Compass(session, cam);
        OrderTickets(session);
        ZabitaBanner(w);

        if (showPrompts)
        {
            Crosshair(local);
            Prompts(local);
            TargetInfo(session, local);
            HoldRing(local);
        }

        ChatLog();

        // Seviye atlama: kupa ikonlu, buyuyerek gelen tabela
        if (_levelFlash > 0)
        {
            var a = MathF.Min(1, _levelFlash);
            var pop = 1f + 0.25f * MathF.Max(0, _levelFlash - 2.1f) / 0.4f;
            var text = Loc.T("hud.levelup", w.Level);
            var tw = ui.Measure(text, 56 * pop, true).X;
            var r = new Rectangle(sw / 2f - tw / 2 - ui.S(90), sh * 0.24f, tw + ui.S(180), ui.S(96 * pop));
            ui.SoftShadow(r, 18, 16, (byte)(120 * a));
            ui.Panel(r, Theme.PrimaryDark.WithAlpha(0.92f * a), 18);
            Icons.Draw(Icon.Trophy, new Vector2(r.X + ui.S(52), r.Y + r.Height / 2), ui.S(56 * pop), Theme.Yellow.WithAlpha(a), Theme.Cream.WithAlpha(a));
            ui.TextOutlined(text, new Vector2(r.X + ui.S(100), r.Y + (r.Height - ui.Measure(text, 56 * pop, true).Y) / 2), 56 * pop, Theme.Yellow.WithAlpha(a), new Color(60, 25, 5, (int)(200 * a)), true);
        }

        // Para popuplari (dunyadan ekrana, konturlu, yukari suzulur)
        foreach (var p in _popups)
        {
            if (cam.WorldToScreen(p.Pos + new Vector3(0, p.Age * 0.8f, 0), sw, sh, out var s))
            {
                var a = 1 - p.Age / 1.6f;
                ui.TextOutlined(p.Text, s, 34 + 8 * MathF.Max(0, 0.25f - p.Age) * 4, p.Color.WithAlpha(a), new Color(40, 20, 5, (int)(220 * a)), true, Align.Center, 2.5f);
            }
        }

        if (session.IsMultiplayer && session.Status.Length > 0)
        {
            ui.Text(Loc.T("hud.players", session.Status), new Vector2(sw - ui.S(20), sh - ui.S(44)), 20, Theme.Cream.WithAlpha(0.7f), false, Align.Right, true);
        }
    }

    // ── Ust durum karti ─────────────────────────────────────────────
    /// <summary>Gun halkasi, saat ve hava, para (sayac), yildizlar, seviye halkasi. Kartin alt y'sini dondurur.</summary>
    private float StatusBar(GameWorld w)
    {
        var ui = _game.Ui;
        var r = new Rectangle(ui.S(20), ui.S(18), ui.S(600), ui.S(108));
        ui.SoftShadow(r, 18, 10, 90);
        ui.Panel(r, Theme.HudBgStrong, 18);
        ui.PanelOutline(new Rectangle(r.X + ui.S(3), r.Y + ui.S(3), r.Width - ui.S(6), r.Height - ui.S(6)), new Color(255, 220, 170, 26), 1.5f, 16);
        var bal = w.Data.Balance;
        // Gun halkasi: acilistan kapanisa ilerleme
        var dayT = (w.Clock.Minute - bal.DayStartMinute) / (float)Math.Max(1, bal.ClosingMinute - bal.DayStartMinute);
        var rc = new Vector2(r.X + ui.S(62), r.Y + r.Height / 2);
        var late = w.Clock.Minute >= bal.ClosingMinute;
        ui.Ring(rc, 44, 8, Math.Clamp(dayT, 0f, 1f), late ? Theme.Red : Theme.Primary, new Color(255, 255, 255, 28));
        ui.Text(Loc.T("hud.day_short"), new Vector2(rc.X, rc.Y - ui.S(26)), 16, Theme.CreamDark, true, Align.Center);
        ui.TextOutlined(w.Clock.Day.ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(rc.X, rc.Y - ui.S(10)), 34, Theme.White, new Color(0, 0, 0, 150), true, Align.Center);
        // Gun adi, saat, hava
        var x = r.X + ui.S(122);
        ui.Text(Loc.T(DayLogic.DayKeys[w.Clock.DayOfWeek]), new Vector2(x, r.Y + ui.S(14)), 22, Theme.CreamDark, true);
        ui.TextOutlined(w.Clock.Clock, new Vector2(x, r.Y + ui.S(42)), 44, Theme.White, new Color(0, 0, 0, 160), true);
        var clockW = ui.Measure(w.Clock.Clock, 44, true).X;
        var (wIcon, wCol) = WeatherIconOf(w);
        Icons.Draw(wIcon, new Vector2(x + clockW + ui.S(30), r.Y + ui.S(66)), ui.S(32), wCol, Theme.Blue);
        // Para (yumusak sayac) ve yildizlar
        var mx = r.X + ui.S(300);
        var money = (float)w.Economy.Money;
        var shown = ui.Ease("hud:money", money, 6f);
        Icons.Draw(Icon.Money, new Vector2(mx + ui.S(16), r.Y + ui.S(32)), ui.S(32), Theme.Yellow, Theme.PrimaryDark);
        ui.TextOutlined(Fmt.Money((int)MathF.Round(shown)), new Vector2(mx + ui.S(40), r.Y + ui.S(12)), 38, w.Economy.Money < 0 ? Theme.Red : Theme.Yellow, new Color(0, 0, 0, 160), true);
        Stars(new Vector2(mx, r.Y + ui.S(64)), w.Reputation.Stars, 13);
        // Seviye halkasi
        var lv = w.Level;
        var levels = bal.XpLevels;
        var cur = levels[Math.Min(lv - 1, levels.Length - 1)];
        var next = lv < levels.Length ? levels[lv] : cur + 1;
        var frac = lv < levels.Length ? (w.Progress.Xp - cur) / (float)(next - cur) : 1f;
        var lc = new Vector2(r.X + r.Width - ui.S(52), r.Y + ui.S(46));
        ui.Ring(lc, 34, 7, ui.Ease("hud:xp", frac, 4f), Theme.Green, new Color(255, 255, 255, 28));
        ui.TextOutlined(lv.ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(lc.X, lc.Y - ui.S(18)), 30, Theme.White, new Color(0, 0, 0, 150), true, Align.Center);
        ui.Text(Loc.T("level." + Math.Min(lv, 12)), new Vector2(lc.X, r.Y + ui.S(84)), 16, Theme.CreamDark, true, Align.Center);
        return r.Y + r.Height;
    }

    /// <summary>Kapanis saatinden sonra yanip sonen uyari rozeti; altindaki ilk bos y'yi dondurur.</summary>
    private float LateWarning(GameWorld w, float y)
    {
        if (w.Clock.Minute < w.Data.Balance.ClosingMinute)
        {
            return y + _game.Ui.S(10);
        }

        var ui = _game.Ui;
        var pulse = 0.6f + 0.4f * MathF.Sin(_game.Time * 4);
        var text = Loc.T("hud.late");
        var r = new Rectangle(ui.S(20), y + ui.S(8), ui.Measure(text, 22, true).X + ui.S(66), ui.S(40));
        ui.Panel(r, Theme.Red.WithAlpha(0.55f + 0.35f * pulse), 12);
        Icons.Draw(Icon.Moon, new Vector2(r.X + ui.S(24), r.Y + r.Height / 2), ui.S(24), Theme.Cream);
        ui.TextIn(r with { X = r.X + ui.S(34) }, text, 22, Theme.White, true);
        return r.Y + r.Height + ui.S(10);
    }

    private (Icon Icon, Color Color) WeatherIconOf(GameWorld w)
    {
        var night = DayNight.LampLevel(w.Clock.Minute / 60f) > 0.5f;
        if (w.Weather.Rain > 0.2f)
        {
            return (Icon.Rain, Theme.CreamDark);
        }

        if (night)
        {
            return (Icon.Moon, Theme.Cream);
        }

        return w.Weather.Cloud > 0.55f ? (Icon.Cloud, Theme.Cream) : (Icon.Sun, Theme.Yellow);
    }

    private void Stars(Vector2 pos, float stars, float r)
    {
        var ui = _game.Ui;
        for (var i = 0; i < 5; i++)
        {
            var c = new Vector2(pos.X + ui.S(i * 30 + 12), pos.Y + ui.S(12));
            var fill = Math.Clamp(stars - i, 0f, 1f);
            DrawStar(c, ui.S(r), new Color(255, 255, 255, 50));
            if (fill > 0)
            {
                Raylib.BeginScissorMode((int)(c.X - ui.S(r)), (int)(c.Y - ui.S(r)), (int)(ui.S(r) * 2 * fill), (int)(ui.S(r) * 2));
                DrawStar(c, ui.S(r), Theme.Yellow);
                Raylib.EndScissorMode();
            }
        }
    }

    public static void DrawStar(Vector2 c, float r, Color color) => Icons.Draw(Icon.Star, c - new Vector2(0, r * 0.05f), r * 2.1f, color);

    // ── Pusula seridi ───────────────────────────────────────────────
    /// <summary>Ust ortada yon seridi: K/D/G/B, satis noktalari, araba ve depo isaretleri (mesafeyle).</summary>
    private void Compass(GameSession session, in CameraView cam)
    {
        var ui = _game.Ui;
        var w = session.World;
        var p = session.LocalPlayer;
        if (p is null)
        {
            return;
        }

        var width = ui.S(560);
        var r = new Rectangle(ui.Width / 2f - width / 2, ui.S(14), width, ui.S(44));
        ui.Panel(r, Theme.HudBg, 14);
        var camYaw = cam.Yaw;
        var span = 75f * MathF.PI / 180f;
        var pxPerRad = width / 2 / span;
        Raylib.BeginScissorMode((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height + (int)ui.S(30));
        float? Screen(float yaw)
        {
            var rel = yaw - camYaw;
            rel = MathF.IEEERemainder(rel, MathF.Tau);
            if (MathF.Abs(rel) > span)
            {
                return null;
            }

            return r.X + width / 2 - rel * pxPerRad;
        }

        // Cizgiler ve ana yonler
        for (var deg = 0; deg < 360; deg += 15)
        {
            if (Screen(deg * MathF.PI / 180f) is not { } sx)
            {
                continue;
            }

            var major = deg % 90 == 0;
            if (!major)
            {
                Raylib.DrawRectangleRec(new Rectangle(sx - ui.S(1), r.Y + ui.S(4), ui.S(2), ui.S(deg % 45 == 0 ? 10 : 6)), new Color(255, 244, 226, 110));
            }
        }

        void Marker(Vector3 target, Icon icon, Color color, bool showDist)
        {
            var d = target - p.Position;
            var dist = new Vector2(d.X, d.Z).Length();
            if (dist < 3f)
            {
                return;
            }

            if (Screen(MathF.Atan2(-d.X, -d.Z)) is not { } sx)
            {
                return;
            }

            Raylib.DrawCircleV(new Vector2(sx, r.Y + r.Height - ui.S(14)), ui.S(12), new Color(20, 14, 10, 220));
            Icons.Draw(icon, new Vector2(sx, r.Y + r.Height - ui.S(14)), ui.S(16), color);
            if (showDist)
            {
                ui.TextOutlined($"{dist:0}m", new Vector2(sx, r.Y + r.Height + ui.S(2)), 16, Theme.Cream, new Color(0, 0, 0, 170), true, Align.Center);
            }
        }

        foreach (var spot in w.Layout.Spots)
        {
            var locked = w.Data.SpotById.TryGetValue(spot.Id, out var sd) && sd.License is { } lic && !w.Progress.Has(lic);
            Marker(spot.CartPos, Icon.Pin, locked ? new Color(160, 150, 140, 255) : Theme.Yellow, false);
        }

        Marker(w.Layout.PlayerSpawn, Icon.Home, Theme.Cream, true);
        if (w.Carts.FirstOrDefault() is { } cart)
        {
            Marker(cart.Position, Icon.Cart, Theme.Primary, true);
        }

        // Ana yon harfleri en ustte (isaretlerin ustune)
        for (var deg = 0; deg < 360; deg += 90)
        {
            if (Screen(deg * MathF.PI / 180f) is { } lx)
            {
                ui.TextOutlined(Loc.T("compass." + deg), new Vector2(lx, r.Y + ui.S(2)), 22, deg == 0 ? Theme.Primary : Theme.Cream, new Color(0, 0, 0, 160), true, Align.Center);
            }
        }

        Raylib.EndScissorMode();
        // Ortadaki isaret
        Raylib.DrawTriangle(new Vector2(r.X + width / 2 - ui.S(7), r.Y), new Vector2(r.X + width / 2, r.Y + ui.S(9)), new Vector2(r.X + width / 2 + ui.S(7), r.Y), Theme.Primary);
    }

    // ── Nisangah ve ipuclari ─────────────────────────────────────────
    private void Crosshair(LocalPlayer local)
    {
        var ui = _game.Ui;
        var c = new Vector2(ui.Width / 2f, ui.Height / 2f);
        var has = local.Options.Count > 0;
        Raylib.DrawCircleV(c, ui.S(has ? 5 : 3), new Color(0, 0, 0, 120));
        Raylib.DrawCircleV(c, ui.S(has ? 3.5f : 2), has ? Theme.Yellow : Theme.White);
    }

    private void Prompts(LocalPlayer local)
    {
        var ui = _game.Ui;
        var y = ui.Height / 2f + ui.S(46);
        var cx = ui.Width / 2f + ui.S(24);
        foreach (var o in local.Options)
        {
            var label = OptionLabel(o);
            if (label.Length == 0)
            {
                continue;
            }

            var key = KeyName(o.Slot);
            var a = o.Enabled ? 1f : 0.55f;
            var showKey = o.Enabled || o.Action != ActionId.None;
            var keyW = showKey ? MathF.Max(ui.Measure(key, 20, true).X + ui.S(16), ui.S(30)) + ui.S(10) : 0;
            var plate = new Rectangle(cx - ui.S(6), y - ui.S(4), keyW + ui.Measure(label, 24, true).X + ui.S(22), ui.S(38));
            ui.Panel(plate, Theme.HudBg.WithAlpha(0.55f * a + 0.1f), 12);
            float kw = 0;
            if (showKey)
            {
                kw = ui.KeyCap(new Vector2(cx, y), key, 20) + ui.S(10);
            }

            ui.Text(label, new Vector2(cx + kw, y + ui.S(3)), 24, (o.Enabled ? Theme.White : Theme.CreamDark).WithAlpha(a), true, shadow: true);
            y += ui.S(44);
        }

        // Elde esya varsa birakma ipucu
        var held = _game.Session?.LocalPlayer is { } p ? _game.Session.World.HeldBy(p) : null;
        if (held is not null && _game.Settings.ShowHints)
        {
            var dropKey = Binding(GameAction.Drop);
            var plate = new Rectangle(cx - ui.S(6), y - ui.S(4), MathF.Max(ui.Measure(dropKey, 18, true).X + ui.S(16), ui.S(27)) + ui.S(20) + ui.Measure(Loc.T("act.drop"), 20).X, ui.S(34));
            ui.Panel(plate, Theme.HudBg.WithAlpha(0.35f), 10);
            var kw = ui.KeyCap(new Vector2(cx, y), dropKey, 18) + ui.S(10);
            ui.Text(Loc.T("act.drop"), new Vector2(cx + kw, y + ui.S(2)), 20, Theme.CreamDark, false, shadow: true);
        }
    }

    /// <summary>
    /// Metindeki {interact}, {use} gibi isaretleri oyuncunun GERCEK tus
    /// atamasiyla degistirir: tuslari degistiren oyuncu egitimde eski
    /// tuslari gormesin.
    /// </summary>
    public string WithKeys(string text)
    {
        if (!text.Contains('{'))
        {
            return text;
        }

        foreach (var a in GameActions.All)
        {
            text = text.Replace("{" + a.ToString().ToLowerInvariant() + "}", "[" + Binding(a) + "]", StringComparison.Ordinal);
        }

        return text;
    }

    private string Binding(GameAction a)
    {
        var b = _game.Input.BindingsOf(a);
        return b.Count > 0 ? b[0].Display : "?";
    }

    private string KeyName(InputSlot slot) => slot switch
    {
        InputSlot.Interact => Binding(GameAction.Interact),
        InputSlot.Secondary => Binding(GameAction.Secondary),
        InputSlot.Use => Binding(GameAction.Use),
        InputSlot.AltUse => Binding(GameAction.AltUse),
        InputSlot.HoldUse => Loc.T("key.hold", Binding(GameAction.Use)),
        InputSlot.HoldInteract => Loc.T("key.hold", Binding(GameAction.Interact)),
        _ => "?",
    };

    public static string OptionLabel(InteractionOption o)
    {
        var arg = o.Arg;
        // Arguman bir Loc anahtariysa cevir (esya adi, boyut).
        if (arg.Contains('.') && Loc.Has(arg))
        {
            arg = Loc.T(arg);
        }

        return Loc.T(o.LabelKey, arg);
    }

    private void HoldRing(LocalPlayer local)
    {
        if (local.HoldProgress < 0)
        {
            return;
        }

        var ui = _game.Ui;
        var c = new Vector2(ui.Width / 2f, ui.Height / 2f);
        Raylib.DrawRing(c, ui.S(18), ui.S(24), 0, 360, 32, new Color(0, 0, 0, 100));
        Raylib.DrawRing(c, ui.S(18), ui.S(24), -90, -90 + 360 * Math.Clamp(local.HoldProgress, 0, 1), 32, Theme.Green);
    }

    // ── Hedef bilgisi ────────────────────────────────────────────────
    private void TargetInfo(GameSession session, LocalPlayer local)
    {
        var w = session.World;
        var t = local.Target;
        var lines = new List<(string Text, Color Color)>();
        string? title = null;
        switch (t.Kind)
        {
            case TargetKind.Item when w.Get<ItemEntity>(t.EntityId) is { } it:
                title = Loc.T(Interactions.ItemNameKey(it));
                ItemLines(w, it, lines);
                break;
            case TargetKind.Part when w.Get<StationEntity>(t.EntityId) is { } st:
                title = Loc.T(StationDefs.Parts(st).FirstOrDefault(p => p.Id == t.Part).NameKey ?? "station");
                StationLines(w, st, t.Part, lines);
                break;
            case TargetKind.Customer when w.Get<CustomerEntity>(t.EntityId) is { } c:
                title = Loc.T("cust." + c.TypeId);
                if (c.Order is { } o && c.State is CustomerState.Ordered or CustomerState.Queue)
                {
                    foreach (var l in Fmt.OrderLines(o))
                    {
                        lines.Add((l, Theme.White));
                    }

                    lines.Add((Loc.T("info.price", Fmt.Money(o.Price)), Theme.Yellow));
                }
                else if (c.State == CustomerState.Paying)
                {
                    lines.Add((Loc.T("info.paying", Fmt.Money(c.PaidAmount), Fmt.Money(c.DueAmount)), Theme.Yellow));
                }

                break;
        }

        if (title is null || (lines.Count == 0 && t.Kind != TargetKind.Item))
        {
            return;
        }

        var ui = _game.Ui;
        var gauges = Gauges(w, t);
        var x = ui.Width / 2f + ui.S(40);
        var gaugeH = gauges.Count > 0 ? ui.S(44) : 0;
        var bodyH = ui.S(30) * lines.Count + gaugeH;
        // Ekranin ustune tasmasin (uzun kazan bilgisi): pusulanin altindan baslar
        var y = MathF.Max(ui.S(72), ui.Height / 2f - ui.S(60) - bodyH - ui.S(46));
        var width = MathF.Max(ui.S(420), ui.Measure(title, 24, true).X + ui.S(80));
        foreach (var (text, _) in lines)
        {
            width = MathF.Max(width, ui.Measure(text, 21).X + ui.S(40));
        }

        var r = new Rectangle(x, y, width, bodyH + ui.S(62));
        ui.SoftShadow(r, 14, 10, 80);
        ui.Panel(r, Theme.HudBgStrong, 14);
        var head = new Rectangle(r.X, r.Y, r.Width, ui.S(44));
        ui.Panel(head, Theme.PrimaryDark.WithAlpha(0.85f), 14);
        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y + ui.S(30), r.Width, ui.S(14)), Theme.PrimaryDark.WithAlpha(0.85f));
        Icons.Draw(TargetIcon(w, t), new Vector2(x + ui.S(26), y + ui.S(22)), ui.S(26), Theme.Cream, Theme.PrimaryDark);
        ui.Text(title, new Vector2(x + ui.S(48), y + ui.S(9)), 24, Theme.White, true);
        var ly = y + ui.S(54);
        foreach (var (text, color) in lines)
        {
            ui.Text(text, new Vector2(x + ui.S(16), ly), 21, color);
            ly += ui.S(30);
        }

        // Gostergeler: sicaklik, su, kalite cubuklari
        if (gauges.Count > 0)
        {
            var gw = (r.Width - ui.S(32) - ui.S(12) * (gauges.Count - 1)) / gauges.Count;
            for (var i = 0; i < gauges.Count; i++)
            {
                var (icon, value, color) = gauges[i];
                var gx = x + ui.S(16) + i * (gw + ui.S(12));
                Icons.Draw(icon, new Vector2(gx + ui.S(11), ly + ui.S(14)), ui.S(22), color);
                ui.Bar(new Rectangle(gx + ui.S(28), ly + ui.S(8), gw - ui.S(28), ui.S(12)), value, color);
            }
        }
    }

    private static Icon TargetIcon(GameWorld w, Target t) => t.Kind switch
    {
        TargetKind.Customer => Icon.Person,
        TargetKind.Item when w.Get<ItemEntity>(t.EntityId) is { } it => it.Type switch
        {
            ItemType.Kazan or ItemType.Tencere => Icon.Kazan,
            ItemType.Tabak => Icon.Plate,
            ItemType.PaketKap => Icon.Package,
            ItemType.Koli => Icon.Box,
            ItemType.OlcuKabi => Icon.Drop,
            _ => Icon.Ladle,
        },
        TargetKind.Part when w.Get<StationEntity>(t.EntityId) is { } st => st.Type switch
        {
            StationType.Cart => Icon.Cart,
            StationType.Laptop => Icon.Laptop,
            StationType.Fridge or StationType.Pantry => Icon.Box,
            StationType.KazanOcagi or StationType.Stovetop => Icon.Flame,
            StationType.Sink => Icon.Drop,
            StationType.Bed => Icon.Moon,
            _ => Icon.Gear,
        },
        _ => Icon.Info,
    };

    /// <summary>Tencere/kazan icin gosterge cubuklari: sicaklik, su (ideal orana yakinlik), kalite.</summary>
    private static List<(Icon Icon, float Value, Color Color)> Gauges(GameWorld w, Target t)
    {
        var list = new List<(Icon, float, Color)>();
        if (t.Kind != TargetKind.Item || w.Get<ItemEntity>(t.EntityId) is not { Pot: { } p })
        {
            return list;
        }

        list.Add((Icon.Thermo, Math.Clamp(p.Temp / 100f, 0f, 1f), p.Temp > 60 ? Theme.Red : Theme.Blue));
        var grain = CookingModel.Grain(p);
        if (grain > 0)
        {
            var ideal = (p.BulgurKg > p.RiceKg ? CookingModel.BulgurWaterRatio : CookingModel.RiceWaterRatio) * grain;
            var water = p.WaterL + p.WaterAbsorbed;
            var ratio = ideal > 0 ? water / ideal : 0f;
            list.Add((Icon.Drop, Math.Clamp(ratio / 1.25f, 0f, 1f), MathF.Abs(ratio - 1f) < 0.08f ? Theme.Green : Theme.Blue));
        }

        if (p.Food is not (Food.None or Food.Ruined) && (grain > 0 || p.ChickpeaKg > 0 || p.BeansKg > 0 || p.ChickenKg > 0))
        {
            var q = p.Quality / 100f;
            list.Add((Icon.Star, q, q >= 0.85f ? Theme.Green : q >= 0.6f ? Theme.Yellow : Theme.Red));
        }

        return list;
    }

    private static void ItemLines(GameWorld w, ItemEntity it, List<(string, Color)> lines)
    {
        switch (it.Type)
        {
            case ItemType.Kazan or ItemType.Tencere:
            {
                var p = it.Pot!;
                var heat = 0;
                if (it.Attach == Attach.Socket && w.Get<StationEntity>(it.ParentId) is { Type: StationType.KazanOcagi or StationType.Stovetop } st)
                {
                    heat = st.Type == StationType.KazanOcagi ? st.Heat[0] : st.Heat[Math.Min(it.SocketIndex, 3)];
                }

                lines.Add((Fmt.PotStatus(p, heat), Theme.Yellow));
                var grain = CookingModel.Grain(p);
                if (grain > 0)
                {
                    var ideal = (p.BulgurKg > p.RiceKg ? CookingModel.BulgurWaterRatio : CookingModel.RiceWaterRatio) * grain;
                    lines.Add((Loc.T(p.BulgurKg > p.RiceKg ? "info.bulgur" : "info.rice", Fmt.Kg(grain), Fmt.Pct(p.RiceWash)), Theme.White));
                    // Etkin su: kazanda kalan + taneye gecen. Buharlasan su sayilmaz;
                    // kapak acik kaynatan oyuncu degerin dustugunu gorup su ekleyebilsin.
                    var water = p.WaterL + p.WaterAbsorbed;
                    lines.Add((Loc.T("info.water", Fmt.Liters(water), Fmt.Liters(ideal)), Math.Abs(water - ideal) / ideal < 0.08f ? Theme.Green : Theme.White));
                    lines.Add((Loc.T("info.butter_salt", (int)p.ButterG, (int)(grain * 100), (int)p.SaltG, (int)(grain * 14)), Theme.White));
                    if (p.MeatKg > 0)
                    {
                        lines.Add((Loc.T("info.meat", Fmt.Kg(p.MeatKg), Fmt.Pct(MathF.Min(1, p.MeatCook))), Theme.White));
                    }
                }
                else if (p.ChickpeaKg > 0 || p.BeansKg > 0 || p.ChickenKg > 0)
                {
                    var kg = p.ChickpeaKg + p.BeansKg + p.ChickenKg;
                    lines.Add((Loc.T("info.boiled", Fmt.Kg(kg), Fmt.Liters(p.WaterL), (int)p.SaltG), Theme.White));
                }
                else if (p.WaterL > 0 || p.ButterG > 0)
                {
                    lines.Add((Loc.T("info.water_butter", Fmt.Liters(p.WaterL), (int)p.ButterG), Theme.White));
                }

                lines.Add((Loc.T("info.temp", (int)p.Temp, p.Lid ? Loc.T("info.lid_on") : Loc.T("info.lid_off")), p.Temp > 60 ? Theme.Yellow : Theme.CreamDark));
                if (p.Food is not (Food.None or Food.Ruined) && (grain > 0 || p.ChickpeaKg > 0 || p.BeansKg > 0 || p.ChickenKg > 0))
                {
                    var q = (int)p.Quality;
                    lines.Add((Loc.T(p.Locked || p.Food != Food.Raw ? "info.quality" : "info.quality_est", q), q >= 85 ? Theme.Green : q >= 60 ? Theme.Yellow : Theme.Red));
                }

                if (p.Hint is { } hint)
                {
                    lines.Add((Loc.T(hint), Theme.Red));
                }

                if (p.Stale)
                {
                    lines.Add((Loc.T("hint.stale"), Theme.Red));
                }

                break;
            }
            case ItemType.Suzgec:
                lines.Add(it.GrainKg > 0
                    ? (Loc.T(it.GrainIsBulgur ? "info.suzgec_bulgur" : "info.suzgec", Fmt.Kg(it.GrainKg), Fmt.Pct(it.Wash), Fmt.Pct(it.Soak)), Theme.White)
                    : (Loc.T("info.empty"), Theme.CreamDark));
                break;
            case ItemType.OlcuKabi:
                lines.Add((Loc.T("info.jug", Fmt.Liters(it.WaterL)), Theme.White));
                break;
            case ItemType.Tereyagi:
                lines.Add((Loc.T("info.grams", (int)it.Amount), Theme.White));
                break;
            case ItemType.TavukPaketi or ItemType.EtPaketi:
                lines.Add((Fmt.Kg(it.Amount), Theme.White));
                break;
            case ItemType.TavukTepsisi:
                lines.Add((Loc.T("info.tray", (int)it.Servings, (int)it.Quality, (int)it.Temp), Theme.White));
                break;
            case ItemType.Tabak or ItemType.PaketKap:
                lines.Add((Fmt.Serving(it.Serving!), Theme.White));
                if (!it.Serving!.IsEmpty)
                {
                    lines.Add((Loc.T("info.temp_short", (int)it.Serving.Temp), it.Serving.Temp >= 60 ? Theme.Green : Theme.Red));
                }

                break;
            case ItemType.Koli:
                if (w.Data.SupplyById.TryGetValue(it.SupplyId, out var def))
                {
                    lines.Add((Loc.T(def.NameKey), Theme.White));
                    lines.Add((Loc.T("info.box_hint"), Theme.CreamDark));
                }

                break;
        }
    }

    private static void StationLines(GameWorld w, StationEntity st, byte part, List<(string, Color)> lines)
    {
        var eco = w.Economy;
        switch (st.Type)
        {
            case StationType.Pantry:
                foreach (var (key, stock) in new[] { ("part.rice", "pirinc"), ("part.bulgur", "bulgur"), ("part.chickpea", "nohut"), ("part.canned", "konserve_nohut"), ("part.beans", "fasulye") })
                {
                    lines.Add(($"{Loc.T(key)}: {Fmt.Kg(eco.StockOf(stock))}", eco.StockOf(stock) > 0 ? Theme.White : Theme.CreamDark));
                }

                lines.Add(($"{Loc.T("stock.tuz")}: {Fmt.Kg(eco.StockOf("tuz"))}", Theme.White));
                lines.Add(($"{Loc.T("menu.ayran")}: {eco.StockOf("ayran"):0}  {Loc.T("menu.tursu")}: {eco.StockOf("tursu"):0}  {Loc.T("stock.paket")}: {eco.StockOf("paket"):0}", Theme.White));
                break;
            case StationType.Fridge:
                lines.Add(($"{Loc.T("part.butter")}: {Fmt.Kg(eco.StockOf("tereyagi"))}", Theme.White));
                lines.Add(($"{Loc.T("part.chicken")}: {Fmt.Kg(eco.StockOf("tavuk"))}", Theme.White));
                lines.Add(($"{Loc.T("part.meat")}: {Fmt.Kg(eco.StockOf("et"))}", Theme.White));
                break;
            case StationType.Cart:
            {
                var c = st.Cart!;
                lines.Add((Loc.T("info.cart_plates", c.CleanPlates, c.DirtyPlates, c.Packages), Theme.White));
                lines.Add((Loc.T("info.cart_extras", c.Ayran, c.Tursu, (int)c.PepperG), Theme.White));
                lines.Add((Loc.T("info.cart_gas", (int)c.Gas, c.HeaterOn ? Loc.T("info.on") : Loc.T("info.off")), c.Gas < 15 ? Theme.Red : Theme.White));
                var spot = c.Spot.Length > 0 ? Loc.T("spot." + c.Spot) : Loc.T("info.no_spot");
                lines.Add((Loc.T("info.cart_spot", spot, c.Open ? Loc.T("info.open") : Loc.T("info.closed")), c.Open ? Theme.Green : Theme.CreamDark));
                if (c.Spot.Length > 0 && w.Data.SpotById.TryGetValue(c.Spot, out var sd) && sd.License is { } lic && !w.Progress.Has(lic))
                {
                    lines.Add((Loc.T("info.no_license"), Theme.Red));
                }

                break;
            }
            case StationType.CuttingBoard when st.BoardChickenKg > 0:
                lines.Add((Loc.T("info.board", Fmt.Kg(st.BoardChickenKg), Fmt.Pct(st.BoardShred)), Theme.White));
                break;
        }
    }

    // ── Siparis fisleri ──────────────────────────────────────────────
    private void OrderTickets(GameSession session)
    {
        var w = session.World;
        var p = session.LocalPlayer;
        if (p is null)
        {
            return;
        }

        var ui = _game.Ui;
        var orders = w.Customers
            .Where(c => c.State is CustomerState.Ordered or CustomerState.Paying && Vector3.DistanceSquared(c.Position, p.Position) < 30 * 30)
            .OrderBy(c => c.QueueIndex)
            .Take(4)
            .ToList();
        var x = ui.Width - ui.S(310);
        var y = ui.S(120);
        foreach (var c in orders)
        {
            var lines = c.State == CustomerState.Paying
                ? new List<string> { Loc.T("ticket.change", Fmt.Money(c.PaidAmount - c.DueAmount)) }
                : Fmt.OrderLines(c.Order!).ToList();
            var h = ui.S(58) + lines.Count * ui.S(26);
            // Fis: hafif egik kagit, ust seritte musteri ve fiyat, satirlarda ikonlar
            var r = new Rectangle(x, y, ui.S(290), h);
            ui.Paper(r);
            Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width, ui.S(5)), c.State == CustomerState.Paying ? Theme.Green : Theme.Primary);
            Icons.Draw(Icon.Person, new Vector2(x + ui.S(22), y + ui.S(22)), ui.S(20), Theme.InkSoft);
            ui.Text(Loc.T("cust." + c.TypeId), new Vector2(x + ui.S(38), y + ui.S(11)), 20, Theme.InkSoft, true);
            if (c.Order is { } o)
            {
                ui.Text(Fmt.Money(o.Price), new Vector2(x + r.Width - ui.S(12), y + ui.S(11)), 20, Theme.PrimaryDark, true, Align.Right);
            }

            for (var dx = x + ui.S(10); dx < x + r.Width - ui.S(10); dx += ui.S(8))
            {
                Raylib.DrawRectangleRec(new Rectangle(dx, y + ui.S(38), ui.S(4), ui.S(1.5f)), new Color(150, 130, 110, 120));
            }

            var ly = y + ui.S(44);
            foreach (var l in lines)
            {
                var icon = c.State == CustomerState.Paying ? Icon.Money
                    : l.Contains(Loc.T("menu.ayran"), StringComparison.Ordinal) ? Icon.Cup
                    : l == Loc.T("order.package") ? Icon.Package
                    : l == Loc.T("order.plate") ? Icon.Plate
                    : l.StartsWith('+') ? Icon.Plus : Icon.Ladle;
                Icons.Draw(icon, new Vector2(x + ui.S(22), ly + ui.S(12)), ui.S(18), Theme.InkSoft);
                ui.Text(l.TrimStart('+', ' '), new Vector2(x + ui.S(38), ly), 20, Theme.Ink);
                ly += ui.S(26);
            }

            ui.Bar(new Rectangle(x + ui.S(10), y + h - ui.S(20), r.Width - ui.S(20), ui.S(6)), c.Patience, c.Patience > 0.5f ? Theme.Green : c.Patience > 0.25f ? Theme.Yellow : Theme.Red);
            y += h + ui.S(10);
        }
    }

    // ── Zabita ───────────────────────────────────────────────────────
    private void ZabitaBanner(GameWorld w)
    {
        if (!w.Events.ZabitaActive)
        {
            return;
        }

        var ui = _game.Ui;
        var left = MathF.Max(0, w.Events.ZabitaDeadline - w.Clock.Absolute);
        var pulse = 0.75f + 0.25f * MathF.Sin(_game.Time * 8);
        var r = new Rectangle(ui.Width / 2f - ui.S(330), ui.S(84), ui.S(660), ui.S(84));
        ui.SoftShadow(r, 14, 12, 110);
        ui.Panel(r, Theme.Red.WithAlpha(0.85f * pulse), 14);
        Icons.Draw(Icon.Warning, new Vector2(r.X + ui.S(48), r.Y + r.Height / 2), ui.S(48), Theme.Yellow, Theme.Ink);
        Icons.Draw(Icon.Warning, new Vector2(r.X + r.Width - ui.S(48), r.Y + r.Height / 2), ui.S(48), Theme.Yellow, Theme.Ink);
        ui.TextOutlined(Loc.T("hud.zabita"), new Vector2(ui.Width / 2f, r.Y + ui.S(8)), 34, Theme.White, new Color(80, 0, 0, 200), true, Align.Center);
        ui.Text(Loc.T("hud.zabita_sub", (int)left), new Vector2(ui.Width / 2f, r.Y + ui.S(48)), 22, Theme.Cream, false, Align.Center, true);
    }

    // ── Egitim ───────────────────────────────────────────────────────
    private void Tutorial(GameWorld w, float top)
    {
        if (!w.Progress.TutorialEnabled || w.Progress.TutorialStep >= Progression.TutorialSteps)
        {
            return;
        }

        var ui = _game.Ui;
        var step = w.Progress.TutorialStep;
        if (step != _lastStep)
        {
            _lastStep = step;
            _stepFlash = 1.2f;
        }

        var text = WithKeys(Loc.T($"tut.{step}"));
        var maxW = ui.S(600);
        var lines = ui.Wrap(text, 22, maxW - ui.S(32));
        var r = new Rectangle(ui.S(20), top, maxW, ui.S(62) + lines.Count * ui.S(28));
        ui.SoftShadow(r, 14, 10, 80);
        ui.Panel(r, Gfx.Lerp(Theme.HudBgStrong, Theme.Primary.WithAlpha(0.85f), _stepFlash), 14);
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, ui.S(8), r.Height), 1f, 6, Theme.Yellow);
        Icons.Draw(Icon.Task, new Vector2(r.X + ui.S(32), r.Y + ui.S(22)), ui.S(22), Theme.Yellow, Theme.PrimaryDark);
        ui.Text(Loc.T("tut.title", step + 1, Progression.TutorialSteps), new Vector2(r.X + ui.S(52), r.Y + ui.S(11)), 18, Theme.Yellow, true);
        ui.Bar(new Rectangle(r.X + r.Width - ui.S(170), r.Y + ui.S(17), ui.S(150), ui.S(8)), (step + 1f) / Progression.TutorialSteps, Theme.Yellow);
        var y = r.Y + ui.S(44);
        foreach (var l in lines)
        {
            ui.Text(l, new Vector2(r.X + ui.S(24), y), 22, Theme.Cream);
            y += ui.S(28);
        }
    }

    // ── Dunya etiketleri: konusma balonu, isimler ────────────────────
    private void DrawWorldLabels(GameSession session, in CameraView cam)
    {
        var w = session.World;
        var ui = _game.Ui;
        foreach (var c in w.Customers)
        {
            var d2 = Vector3.DistanceSquared(c.RenderPosition, cam.Position);
            if (d2 > 22 * 22)
            {
                continue;
            }

            var head = c.RenderPosition + new Vector3(0, 2.05f, 0);
            if (!cam.WorldToScreen(head, ui.Width, ui.Height, out var s))
            {
                continue;
            }

            string? text = null;
            if (c.BubbleKey.Length > 0 && (c.BubbleUntil > w.Time || c.State == CustomerState.Ordered))
            {
                var raw = Loc.T(c.BubbleKey, c.BubbleArg);
                if (c.BubbleKey.StartsWith("say.order", StringComparison.Ordinal) && c.Order is { } o)
                {
                    raw = Loc.T(c.BubbleKey, Fmt.Order(o).ToLowerInvariant());
                }

                text = raw;
            }

            var scale = Math.Clamp(9f / MathF.Sqrt(d2 + 1), 0.55f, 1.1f);
            if (text is not null && _game.Settings.Subtitles)
            {
                Bubble(s, text, scale, c.Mood);
            }

            if (c.State is CustomerState.Queue or CustomerState.Ordered && c.Patience < 0.999f)
            {
                var bw = ui.S(60) * scale;
                ui.Bar(new Rectangle(s.X - bw / 2, s.Y + ui.S(8), bw, ui.S(6) * scale), c.Patience, c.Patience > 0.5f ? Theme.Green : c.Patience > 0.25f ? Theme.Yellow : Theme.Red);
            }
        }

        foreach (var p in w.Players)
        {
            if (p.Id == session.LocalPlayerId || !p.Connected)
            {
                continue;
            }

            if (cam.WorldToScreen(p.RenderPosition + new Vector3(0, 2.05f, 0), ui.Width, ui.Height, out var s))
            {
                var color = Gfx.Hex(PlayerEntity.Colors[p.ColorIndex % PlayerEntity.Colors.Length]);
                ui.Text(p.Name, s, 22, color, true, Align.Center, true);
                if (p.ChatUntil > w.Time && p.Chat.Length > 0)
                {
                    Bubble(s - new Vector2(0, ui.S(30)), p.Chat, 0.9f, Mood.None);
                }
            }
        }
    }

    private void Bubble(Vector2 anchor, string text, float scale, Mood mood)
    {
        var ui = _game.Ui;
        var size = 21f * scale;
        var lines = ui.Wrap(text, size, ui.S(330));
        var width = 0f;
        foreach (var l in lines)
        {
            width = MathF.Max(width, ui.Measure(l, size).X);
        }

        var pad = ui.S(10) * scale;
        var lh = ui.S(size * 1.25f);
        var h = lines.Count * lh + pad * 2;
        var r = new Rectangle(anchor.X - width / 2 - pad, anchor.Y - h - ui.S(14) * scale, width + pad * 2, h);
        var bg = mood switch
        {
            Mood.Angry => new Color(255, 225, 220, 240),
            Mood.Happy or Mood.Love => new Color(230, 250, 225, 240),
            _ => new Color(255, 255, 255, 235),
        };
        ui.Panel(r, bg, 10);
        Raylib.DrawTriangle(new Vector2(anchor.X - ui.S(8) * scale, r.Y + r.Height - 1), new Vector2(anchor.X, r.Y + r.Height + ui.S(10) * scale), new Vector2(anchor.X + ui.S(8) * scale, r.Y + r.Height - 1), bg);
        var y = r.Y + pad;
        foreach (var l in lines)
        {
            ui.Fonts.Draw(l, new Vector2(anchor.X - ui.Measure(l, size).X / 2, y), ui.S(size), Theme.Ink);
            y += lh;
        }
    }

    private void ChatLog()
    {
        if (_chat.Count == 0)
        {
            return;
        }

        var ui = _game.Ui;
        var y = ui.Height - ui.S(220);
        var alpha = _chat.Max(c => Math.Clamp(14f - c.Age, 0f, 1f));
        var width = _chat.Max(c => ui.Measure($"{c.Name}: {c.Text}", 22).X) + ui.S(28);
        ui.Panel(new Rectangle(ui.S(14), y - ui.S(8), width, _chat.Count * ui.S(28) + ui.S(16)), Theme.HudBg.WithAlpha(0.5f * alpha), 12);
        foreach (var (name, text, age) in _chat)
        {
            var a = Math.Clamp(14f - age, 0f, 1f);
            var nw = ui.Measure(name + ": ", 22, true).X;
            ui.Text(name + ":", new Vector2(ui.S(26), y), 22, Theme.Yellow.WithAlpha(a), true, shadow: true);
            ui.Text(text, new Vector2(ui.S(26) + nw, y), 22, Theme.Cream.WithAlpha(a), false, shadow: true);
            y += ui.S(28);
        }
    }
}
