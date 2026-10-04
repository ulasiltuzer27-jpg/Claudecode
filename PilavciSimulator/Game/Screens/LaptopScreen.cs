using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Screens;

/// <summary>Depodaki laptop: toptanci siparisi, yukseltmeler, kozmetik, istatistik.</summary>
public sealed class LaptopScreen : OverlayScreen
{
    private int _tab;
    private readonly Dictionary<string, int> _basket = new();
    private bool _express;
    private float _scroll;

    public override void DrawUi()
    {
        if (Game.Session is not { } s)
        {
            return;
        }

        var w = s.World;
        var ui = Game.Ui;
        var area = Window(1240, 860, Loc.T("laptop.title"));
        string[] tabs = ["laptop.tab_supplies", "laptop.tab_upgrades", "laptop.tab_cosmetics", "laptop.tab_stats"];
        var tx = area.X;
        for (var i = 0; i < tabs.Length; i++)
        {
            var r = new Rectangle(tx, area.Y, ui.S(270), ui.S(52));
            if (ui.Button(r, Loc.T(tabs[i]), i == _tab ? ButtonStyle.TabActive : ButtonStyle.Tab, true, 24))
            {
                _tab = i;
                _scroll = 0;
            }

            tx += ui.S(282);
        }

        ui.Text(Fmt.Money(w.Economy.Money), new Vector2(area.X + area.Width, area.Y + ui.S(10)), 30, Theme.Yellow, true, Align.Right);
        var body = new Rectangle(area.X, area.Y + ui.S(70), area.Width, area.Height - ui.S(70));
        switch (_tab)
        {
            case 0:
                Supplies(s, body);
                break;
            case 1:
                Upgrades(s, body, cosmetics: false);
                break;
            case 2:
                Upgrades(s, body, cosmetics: true);
                break;
            default:
                Stats(s, body);
                break;
        }
    }

    private void Supplies(Net.GameSession s, Rectangle body)
    {
        var w = s.World;
        var ui = Game.Ui;
        var y = body.Y;
        var total = 0;
        foreach (var def in w.Data.Supplies)
        {
            var locked = def.Level > w.Level;
            var price = EconomyLogic.SupplyPrice(w, def.Id, _express);
            var qty = _basket.GetValueOrDefault(def.Id);
            total += price * qty;
            var row = new Rectangle(body.X, y, body.Width - ui.S(380), ui.S(44));
            ui.Panel(row, new Color(255, 255, 255, qty > 0 ? 30 : 12), 8);
            ui.Text(Loc.T(def.NameKey), new Vector2(row.X + ui.S(12), y + ui.S(9)), 22, locked ? Theme.CreamDark : Theme.Cream, qty > 0);
            var stock = w.Economy.StockOf(def.Stock);
            ui.Text(Loc.T("laptop.stock", stock.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)), new Vector2(row.X + ui.S(360), y + ui.S(11)), 18, Theme.CreamDark);
            ui.Text(locked ? Loc.T("price.locked", def.Level) : Fmt.Money(price), new Vector2(row.X + ui.S(560), y + ui.S(10)), 20, locked ? Theme.CreamDark : Theme.Yellow, true);
            var bx = row.X + row.Width - ui.S(170);
            if (ui.Button(new Rectangle(bx, y + ui.S(3), ui.S(44), ui.S(38)), "-", ButtonStyle.Normal, qty > 0, 24))
            {
                _basket[def.Id] = qty - 1;
            }

            ui.TextIn(new Rectangle(bx + ui.S(48), y, ui.S(60), ui.S(44)), $"{qty}", 24, Theme.Cream, true, Align.Center);
            if (ui.Button(new Rectangle(bx + ui.S(112), y + ui.S(3), ui.S(44), ui.S(38)), "+", ButtonStyle.Normal, !locked && qty < 20, 24))
            {
                _basket[def.Id] = qty + 1;
            }

            y += ui.S(50);
        }

        // Sag panel: sepet
        var side = new Rectangle(body.X + body.Width - ui.S(360), body.Y, ui.S(360), ui.S(420));
        ui.Panel(side, new Color(255, 255, 255, 20), 12);
        ui.Text(Loc.T("laptop.basket"), new Vector2(side.X + ui.S(16), side.Y + ui.S(12)), 26, Theme.Cream, true);
        var express = _express;
        ui.Toggle(new Rectangle(side.X + ui.S(8), side.Y + ui.S(60), side.Width - ui.S(16), ui.S(50)), Loc.T("laptop.express"), ref express);
        _express = express;
        ui.Paragraph(Loc.T(_express ? "laptop.express_hint" : "laptop.normal_hint"), new Vector2(side.X + ui.S(16), side.Y + ui.S(120)), 18, side.Width - ui.S(32), Theme.CreamDark);
        ui.Text(Loc.T("laptop.total"), new Vector2(side.X + ui.S(16), side.Y + ui.S(230)), 22, Theme.CreamDark);
        ui.Text(Fmt.Money(total), new Vector2(side.X + ui.S(16), side.Y + ui.S(260)), 40, total > w.Economy.Money ? Theme.Red : Theme.Yellow, true);
        if (ui.Button(new Rectangle(side.X + ui.S(16), side.Y + ui.S(330), side.Width - ui.S(32), ui.S(64)), Loc.T("laptop.order"), ButtonStyle.Primary, total > 0 && total <= w.Economy.Money))
        {
            var basket = string.Join(",", _basket.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}"));
            s.SendAction(new ActionRequest { Action = ActionId.BuySupplies, Text = basket, Param = _express ? 1 : 0 });
            _basket.Clear();
            Game.Audio.Play("cash");
        }

        // Bekleyen teslimatlar
        var py = side.Y + side.Height + ui.S(20);
        ui.Text(Loc.T("laptop.pending"), new Vector2(side.X, py), 22, Theme.Cream, true);
        py += ui.S(32);
        foreach (var d in w.Economy.Pending.Take(8))
        {
            var when = d.ArriveAt - w.Clock.Absolute;
            var eta = when < 60 ? Loc.T("laptop.eta_min", (int)MathF.Max(0, when)) : Loc.T("laptop.eta_morning");
            ui.Text($"{d.Packs}x {Loc.T(w.Data.SupplyById[d.SupplyId].NameKey)} — {eta}", new Vector2(side.X, py), 18, Theme.CreamDark);
            py += ui.S(26);
        }
    }

    private void Upgrades(Net.GameSession s, Rectangle body, bool cosmetics)
    {
        var w = s.World;
        var ui = Game.Ui;
        var list = w.Data.Upgrades.Where(u => (u.Category == "kozmetik") == cosmetics).ToList();
        var cols = 3;
        var cw = (body.Width - ui.S(20) * (cols - 1)) / cols;
        var ch = ui.S(200);
        _scroll = Math.Clamp(_scroll - Game.Input.Wheel * ui.S(60), 0, MathF.Max(0, (list.Count + cols - 1) / cols * (ch + ui.S(16)) - body.Height));
        Raylib.BeginScissorMode((int)body.X, (int)body.Y, (int)body.Width, (int)body.Height);
        for (var i = 0; i < list.Count; i++)
        {
            var u = list[i];
            var r = new Rectangle(body.X + (i % cols) * (cw + ui.S(20)), body.Y + (i / cols) * (ch + ui.S(16)) - _scroll, cw, ch);
            if (r.Y > body.Y + body.Height || r.Y + r.Height < body.Y)
            {
                continue;
            }

            var owned = w.Progress.Has(u.Id);
            var can = EconomyLogic.CanBuyUpgrade(w, u.Id, out var reason);
            ui.Panel(r, owned ? Theme.Green.WithAlpha(0.25f) : new Color(255, 255, 255, 22), 12);
            if (u.Value is { Length: 6 } hex)
            {
                Raylib.DrawRectangleRounded(new Rectangle(r.X + r.Width - ui.S(60), r.Y + ui.S(14), ui.S(44), ui.S(44)), 0.3f, 4, Gfx.Hex(Convert.ToUInt32(hex, 16)));
            }

            ui.Text(Loc.T(u.NameKey), new Vector2(r.X + ui.S(16), r.Y + ui.S(12)), 24, Theme.Cream, true);
            ui.Paragraph(Loc.T(u.DescKey), new Vector2(r.X + ui.S(16), r.Y + ui.S(48)), 18, r.Width - ui.S(32), Theme.CreamDark);
            var by = r.Y + r.Height - ui.S(58);
            if (owned)
            {
                if (cosmetics)
                {
                    var active = w.Progress.Paint == u.Value;
                    if (ui.Button(new Rectangle(r.X + ui.S(16), by, r.Width - ui.S(32), ui.S(46)), Loc.T(active ? "laptop.active" : "laptop.select"), active ? ButtonStyle.TabActive : ButtonStyle.Normal, !active, 22))
                    {
                        s.SendAction(new ActionRequest { Action = ActionId.SelectCosmetic, Text = u.Id });
                    }
                }
                else
                {
                    ui.TextIn(new Rectangle(r.X + ui.S(16), by, r.Width - ui.S(32), ui.S(46)), Loc.T("laptop.owned"), 22, Theme.Green, true);
                }
            }
            else
            {
                var label = can ? Loc.T("laptop.buy", Fmt.Money(u.Price)) : Loc.T(reason, u.Level, Fmt.Money(u.Price));
                if (ui.Button(new Rectangle(r.X + ui.S(16), by, r.Width - ui.S(32), ui.S(46)), label, can ? ButtonStyle.Primary : ButtonStyle.Normal, can, 21))
                {
                    s.SendAction(new ActionRequest { Action = ActionId.BuyUpgrade, Text = u.Id });
                }

                if (u.Daily > 0)
                {
                    ui.Text(Loc.T("laptop.daily", Fmt.Money(u.Daily)), new Vector2(r.X + ui.S(16), by - ui.S(26)), 17, Theme.Yellow);
                }
            }
        }

        Raylib.EndScissorMode();
        if (cosmetics && w.Progress.Paint.Length > 0 && ui.Button(new Rectangle(body.X, body.Y + body.Height - ui.S(50), ui.S(280), ui.S(46)), Loc.T("laptop.paint_default"), ButtonStyle.Normal, true, 20))
        {
            s.SendAction(new ActionRequest { Action = ActionId.SelectCosmetic, Text = "" });
        }
    }

    private void Stats(Net.GameSession s, Rectangle body)
    {
        var w = s.World;
        var ui = Game.Ui;
        var p = w.Progress;
        var y = body.Y;
        void Row(string key, string value)
        {
            ui.Text(Loc.T(key), new Vector2(body.X, y), 22, Theme.CreamDark);
            ui.Text(value, new Vector2(body.X + ui.S(520), y), 22, Theme.Cream, true, Align.Right);
            y += ui.S(34);
        }

        Row("stats.days", $"{p.Stat("days_played")}");
        Row("stats.served", $"{p.Stat("served")}");
        Row("stats.kazan", $"{p.Stat("kazan_cooked")}");
        Row("stats.perfect", $"{p.Stat("perfect_pilav")}");
        Row("stats.change", $"{p.Stat("correct_change")}");
        Row("stats.best_day", Fmt.Money(p.Stat("best_day_profit")));
        Row("stats.total", Fmt.Money(p.Stat("total_revenue")));
        Row("stats.zabita", $"{p.Stat("zabita_escaped")} / {p.Stat("zabita_fined")}");
        Row("stats.cat", $"{p.Stat("cat_pets")}");
        Row("stats.inflation", $"{(w.Economy.SupplyIndex - 1) * 100:0}%");

        // Basarimlar listesi
        var ax = body.X + ui.S(600);
        var ay = body.Y;
        ui.Text(Loc.T("stats.achievements", p.Achievements.Count, w.Data.Achievements.Count), new Vector2(ax, ay), 24, Theme.Yellow, true);
        ay += ui.S(40);
        foreach (var a in w.Data.Achievements)
        {
            var done = p.Achievements.Contains(a.Id);
            var name = done || !a.Hidden ? Loc.T("ach." + a.Id) : "???";
            ui.Text((done ? "• " : "  ") + name, new Vector2(ax, ay), 19, done ? Theme.Green : Theme.CreamDark, done);
            ay += ui.S(26);
            if (ay > body.Y + body.Height - ui.S(30))
            {
                ax += ui.S(310);
                ay = body.Y + ui.S(40);
            }
        }
    }
}
