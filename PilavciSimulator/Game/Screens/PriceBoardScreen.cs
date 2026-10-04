using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim.Economy;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Screens;

/// <summary>Fiyat tabelasi: menu fiyatlari, piyasa referansi ve tahmini maliyet.</summary>
public sealed class PriceBoardScreen : OverlayScreen
{
    public override void DrawUi()
    {
        if (Game.Session is not { } s)
        {
            return;
        }

        var w = s.World;
        var ui = Game.Ui;
        var area = Window(860, 760, Loc.T("price.title"));
        var y = area.Y;
        ui.Text(Loc.T("price.col_item"), new Vector2(area.X, y), 20, Theme.CreamDark, true);
        ui.Text(Loc.T("price.col_ref"), new Vector2(area.X + ui.S(330), y), 20, Theme.CreamDark, true);
        ui.Text(Loc.T("price.col_price"), new Vector2(area.X + ui.S(520), y), 20, Theme.CreamDark, true);
        y += ui.S(36);
        foreach (var m in w.Data.Menu)
        {
            var locked = m.Level > w.Level;
            var price = w.Economy.PriceOf(m.Id);
            var refPrice = EconomyLogic.ReferencePrice(w, m.Id);
            var ratio = price / MathF.Max(1, refPrice);
            ui.Text(Loc.T(m.NameKey) + (locked ? "  " + Loc.T("price.locked", m.Level) : ""), new Vector2(area.X, y + ui.S(12)), 24, locked ? Theme.CreamDark : Theme.Cream, true);
            ui.Text(Fmt.Money((long)MathF.Round(refPrice)), new Vector2(area.X + ui.S(330), y + ui.S(12)), 22, Theme.CreamDark);
            var minus = new Rectangle(area.X + ui.S(500), y, ui.S(56), ui.S(52));
            var plus = new Rectangle(area.X + ui.S(700), y, ui.S(56), ui.S(52));
            if (ui.Button(minus, "-5", ButtonStyle.Normal, !locked && price > EconomyLogic.MinPrice, 22))
            {
                Set(m.Id, price - 5);
            }

            var pr = new Rectangle(area.X + ui.S(562), y, ui.S(132), ui.S(52));
            ui.Panel(pr, Theme.Cream.WithAlpha(locked ? 0.3f : 1f), 8);
            ui.TextIn(pr, Fmt.Money(price), 24, Theme.Ink, true, Align.Center);
            if (ui.Button(plus, "+5", ButtonStyle.Normal, !locked, 22))
            {
                Set(m.Id, price + 5);
            }

            // Talep gostergesi
            var (label, color) = ratio > 1.25f ? ("price.too_high", Theme.Red) : ratio > 1.05f ? ("price.high", Theme.Yellow) : ratio < 0.85f ? ("price.cheap", Theme.Blue) : ("price.fair", Theme.Green);
            if (!locked)
            {
                ui.Text(Loc.T(label), new Vector2(area.X + ui.S(770), y + ui.S(14)), 18, color, true);
            }

            y += ui.S(62);
        }

        ui.Paragraph(Loc.T("price.hint"), new Vector2(area.X, area.Y + area.Height - ui.S(60)), 19, area.Width, Theme.CreamDark);
    }

    private void Set(string id, int price)
    {
        Game.Session!.SendAction(new ActionRequest { Action = ActionId.SetPrice, Text = id, Param = price });
        Game.Audio.Play("ui_click", 0.5f);
    }
}
