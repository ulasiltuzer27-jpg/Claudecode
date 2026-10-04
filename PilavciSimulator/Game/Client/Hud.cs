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
        StatusBar(w);
        Tutorial(w);
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

        // Seviye atlama parlamasi
        if (_levelFlash > 0)
        {
            var a = MathF.Min(1, _levelFlash);
            ui.Text(Loc.T("hud.levelup", w.Level), new Vector2(sw / 2f, sh * 0.28f), 64, Theme.Yellow.WithAlpha(a), true, Align.Center, true);
        }

        // Para popuplari (dunyadan ekrana)
        foreach (var p in _popups)
        {
            if (cam.WorldToScreen(p.Pos + new Vector3(0, p.Age * 0.8f, 0), sw, sh, out var s))
            {
                ui.Text(p.Text, s, 34, p.Color.WithAlpha(1 - p.Age / 1.6f), true, Align.Center, true);
            }
        }

        if (session.IsMultiplayer && session.Status.Length > 0)
        {
            ui.Text(Loc.T("hud.players", session.Status), new Vector2(sw - ui.S(20), sh - ui.S(44)), 20, Theme.Cream.WithAlpha(0.7f), false, Align.Right, true);
        }
    }

    // ── Ust durum cubugu ────────────────────────────────────────────
    private void StatusBar(GameWorld w)
    {
        var ui = _game.Ui;
        var r = new Rectangle(ui.S(20), ui.S(18), ui.S(560), ui.S(92));
        ui.Panel(r, Theme.HudBg);
        var day = Loc.T(DayLogic.DayKeys[w.Clock.DayOfWeek]);
        ui.Text(Loc.T("hud.day", w.Clock.Day, day), new Vector2(r.X + ui.S(18), r.Y + ui.S(10)), 24, Theme.Cream, true, shadow: true);
        ui.Text(w.Clock.Clock, new Vector2(r.X + ui.S(18), r.Y + ui.S(40)), 40, Theme.White, true, shadow: true);
        WeatherIcon(new Vector2(r.X + ui.S(150), r.Y + ui.S(60)), w);
        // Para
        ui.Text(Fmt.Money(w.Economy.Money), new Vector2(r.X + ui.S(205), r.Y + ui.S(12)), 36, w.Economy.Money < 0 ? Theme.Red : Theme.Yellow, true, shadow: true);
        // Yildizlar
        Stars(new Vector2(r.X + ui.S(210), r.Y + ui.S(62)), w.Reputation.Stars, 13);
        // Seviye ve XP
        var lv = w.Level;
        var levels = w.Data.Balance.XpLevels;
        var cur = levels[Math.Min(lv - 1, levels.Length - 1)];
        var next = lv < levels.Length ? levels[lv] : cur + 1;
        var frac = lv < levels.Length ? (w.Progress.Xp - cur) / (float)(next - cur) : 1f;
        ui.Text(Loc.T("hud.level", lv), new Vector2(r.X + ui.S(400), r.Y + ui.S(14)), 22, Theme.Cream, true, shadow: true);
        ui.Text(Loc.T("level." + Math.Min(lv, 12)), new Vector2(r.X + ui.S(400), r.Y + ui.S(40)), 18, Theme.CreamDark, false, shadow: true);
        ui.Bar(new Rectangle(r.X + ui.S(400), r.Y + ui.S(68), ui.S(140), ui.S(10)), frac, Theme.Green);

        // Gun sonu yaklasirken uyari
        if (w.Clock.Minute >= w.Data.Balance.ClosingMinute)
        {
            var pulse = 0.6f + 0.4f * MathF.Sin(_game.Time * 4);
            ui.Text(Loc.T("hud.late"), new Vector2(r.X + ui.S(18), r.Y + r.Height + ui.S(6)), 22, Theme.Red.WithAlpha(pulse), true, shadow: true);
        }
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

    public static void DrawStar(Vector2 c, float r, Color color)
    {
        Span<Vector2> pts = stackalloc Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var a = -MathF.PI / 2 + i * MathF.PI / 5;
            var rr = i % 2 == 0 ? r : r * 0.45f;
            pts[i] = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rr;
        }

        for (var i = 0; i < 10; i++)
        {
            Raylib.DrawTriangle(c, pts[(i + 1) % 10], pts[i], color);
        }
    }

    private void WeatherIcon(Vector2 c, GameWorld w)
    {
        var ui = _game.Ui;
        var r = ui.S(13);
        var hour = w.Clock.Minute / 60f;
        var night = DayNight.LampLevel(hour) > 0.5f;
        if (w.Weather.Rain > 0.2f)
        {
            Raylib.DrawCircleV(c + new Vector2(-r * 0.4f, 0), r * 0.75f, Theme.CreamDark);
            Raylib.DrawCircleV(c + new Vector2(r * 0.4f, -r * 0.2f), r * 0.85f, Theme.CreamDark);
            for (var i = 0; i < 3; i++)
            {
                var x = c.X - r * 0.6f + i * r * 0.6f;
                Raylib.DrawLineEx(new Vector2(x, c.Y + r * 0.7f), new Vector2(x - r * 0.25f, c.Y + r * 1.3f), ui.S(2.5f), Theme.Blue);
            }
        }
        else if (night)
        {
            Raylib.DrawCircleV(c, r, Theme.Cream);
            Raylib.DrawCircleV(c + new Vector2(r * 0.45f, -r * 0.3f), r * 0.85f, Theme.HudBg with { A = 255 });
        }
        else if (w.Weather.Cloud > 0.55f)
        {
            Raylib.DrawCircleV(c + new Vector2(-r * 0.4f, 0), r * 0.75f, Theme.Cream);
            Raylib.DrawCircleV(c + new Vector2(r * 0.4f, -r * 0.2f), r * 0.85f, Theme.Cream);
        }
        else
        {
            Raylib.DrawCircleV(c, r * 0.65f, Theme.Yellow);
            for (var i = 0; i < 8; i++)
            {
                var a = i * MathF.PI / 4;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                Raylib.DrawLineEx(c + d * r * 0.85f, c + d * r * 1.2f, ui.S(2.5f), Theme.Yellow);
            }
        }
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
            float kw = 0;
            if (o.Enabled || o.Action != ActionId.None)
            {
                kw = ui.KeyCap(new Vector2(cx, y), key, 20) + ui.S(10);
            }

            ui.Text(label, new Vector2(cx + kw, y + ui.S(3)), 24, (o.Enabled ? Theme.White : Theme.CreamDark).WithAlpha(a), true, shadow: true);
            y += ui.S(36);
        }

        // Elde esya varsa birakma ipucu
        var held = _game.Session?.LocalPlayer is { } p ? _game.Session.World.HeldBy(p) : null;
        if (held is not null && _game.Settings.ShowHints)
        {
            var kw = ui.KeyCap(new Vector2(cx, y), Binding(GameAction.Drop), 18) + ui.S(10);
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
        var x = ui.Width / 2f + ui.S(40);
        var y = ui.Height / 2f - ui.S(40) - ui.S(30) * (lines.Count + 1);
        var width = ui.S(420);
        foreach (var (text, _) in lines)
        {
            width = MathF.Max(width, ui.Measure(text, 21).X + ui.S(40));
        }

        var r = new Rectangle(x, y, width, ui.S(30) * (lines.Count + 1) + ui.S(20));
        ui.Panel(r, Theme.HudBg);
        ui.Text(title, new Vector2(x + ui.S(16), y + ui.S(8)), 24, Theme.Yellow, true);
        var ly = y + ui.S(42);
        foreach (var (text, color) in lines)
        {
            ui.Text(text, new Vector2(x + ui.S(16), ly), 21, color);
            ly += ui.S(30);
        }
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
                    lines.Add((Loc.T("info.water", Fmt.Liters(p.WaterAdded), Fmt.Liters(ideal)), Math.Abs(p.WaterAdded - ideal) / ideal < 0.1f ? Theme.Green : Theme.White));
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
            var h = ui.S(46) + lines.Count * ui.S(26);
            var r = new Rectangle(x, y, ui.S(290), h);
            ui.Panel(r, new Color(255, 248, 230, 235), 6);
            Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width, ui.S(6)), Theme.Primary);
            ui.Text(Loc.T("cust." + c.TypeId), new Vector2(x + ui.S(12), y + ui.S(10)), 20, Theme.InkSoft, true);
            if (c.Order is { } o)
            {
                ui.Text(Fmt.Money(o.Price), new Vector2(x + r.Width - ui.S(12), y + ui.S(10)), 20, Theme.PrimaryDark, true, Align.Right);
            }

            var ly = y + ui.S(36);
            foreach (var l in lines)
            {
                ui.Text(l, new Vector2(x + ui.S(12), ly), 20, Theme.Ink);
                ly += ui.S(26);
            }

            ui.Bar(new Rectangle(x + ui.S(10), y + h - ui.S(12), r.Width - ui.S(20), ui.S(6)), c.Patience, c.Patience > 0.5f ? Theme.Green : c.Patience > 0.25f ? Theme.Yellow : Theme.Red);
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
        var r = new Rectangle(ui.Width / 2f - ui.S(330), ui.S(20), ui.S(660), ui.S(84));
        ui.Panel(r, Theme.Red.WithAlpha(0.85f * pulse));
        ui.Text(Loc.T("hud.zabita"), new Vector2(ui.Width / 2f, r.Y + ui.S(8)), 34, Theme.White, true, Align.Center, true);
        ui.Text(Loc.T("hud.zabita_sub", (int)left), new Vector2(ui.Width / 2f, r.Y + ui.S(48)), 22, Theme.Cream, false, Align.Center, true);
    }

    // ── Egitim ───────────────────────────────────────────────────────
    private void Tutorial(GameWorld w)
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
        var maxW = ui.S(520);
        var lines = ui.Wrap(text, 22, maxW - ui.S(32));
        var r = new Rectangle(ui.S(20), ui.S(124), maxW, ui.S(54) + lines.Count * ui.S(28));
        ui.Panel(r, Gfx.Lerp(Theme.HudBg, Theme.Primary.WithAlpha(0.8f), _stepFlash));
        ui.Text(Loc.T("tut.title", step + 1, Progression.TutorialSteps), new Vector2(r.X + ui.S(16), r.Y + ui.S(10)), 18, Theme.Yellow, true);
        var y = r.Y + ui.S(38);
        foreach (var l in lines)
        {
            ui.Text(l, new Vector2(r.X + ui.S(16), y), 22, Theme.Cream);
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
        foreach (var (name, text, age) in _chat)
        {
            var a = Math.Clamp(14f - age, 0f, 1f);
            ui.Text($"{name}: {text}", new Vector2(ui.S(24), y), 22, Theme.Cream.WithAlpha(a), false, shadow: true);
            y += ui.S(28);
        }
    }
}
