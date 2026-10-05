using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim;

namespace PilavciSimulator.Screens;

/// <summary>Gun sonu raporu: gelir, gider, kar, musteri, itibar. Host "Sonraki gun" der.</summary>
public sealed class DayReportScreen : OverlayScreen
{
    private readonly bool _passedOut;

    public DayReportScreen(bool passedOut) => _passedOut = passedOut;

    public override bool PausesBelow => false;

    protected override void Close()
    {
        // Rapor kapatilamaz; host devam deyince kendiliginden kapanir.
    }

    protected override bool Closable => false;

    public override bool Command(string[] args)
    {
        if (args[0] != "report-next" || Game.Session is not { IsHost: true } s)
        {
            return false;
        }

        s.ContinueAfterReport();
        return true;
    }

    public override void DrawUi()
    {
        if (Game.Session is not { } s)
        {
            return;
        }

        var w = s.World;
        var led = w.Economy.History.LastOrDefault() ?? w.Economy.Today;
        var ui = Game.Ui;
        var area = Window(900, 760, Loc.T("report.title", led.Day), 0.75f);
        var y = area.Y;
        if (_passedOut)
        {
            ui.Text(Loc.T("report.passout"), new Vector2(area.X, y), 22, Theme.Red, true);
            y += ui.S(36);
        }

        void Row(string label, string value, Color color)
        {
            ui.Text(label, new Vector2(area.X, y), 24, Theme.Cream);
            ui.Text(value, new Vector2(area.X + ui.S(400), y), 24, color, true, Align.Right);
            y += ui.S(34);
        }

        var colX = area.X + ui.S(460);
        Row(Loc.T("report.revenue"), Fmt.Money(led.Revenue), Theme.Green);
        Row(Loc.T("report.tips"), Fmt.Money(led.Tips), Theme.Green);
        Row(Loc.T("report.supplies"), Cost(led.Supplies), Theme.Red);
        Row(Loc.T("report.upgrades"), Cost(led.Upgrades), Theme.Red);
        Row(Loc.T("report.gas"), Cost(led.Gas), Theme.Red);
        if (led.Fines > 0)
        {
            Row(Loc.T("report.fines"), Cost(led.Fines), Theme.Red);
        }

        if (led.Wages + led.Rent > 0)
        {
            Row(Loc.T("report.wages"), Cost(led.Wages + led.Rent), Theme.Red);
        }

        if (led.ChangeLoss > 0)
        {
            Row(Loc.T("report.change_loss"), Cost(led.ChangeLoss), Theme.Red);
        }

        Raylib.DrawLineEx(new Vector2(area.X, y + ui.S(4)), new Vector2(area.X + ui.S(400), y + ui.S(4)), ui.S(2), Theme.CreamDark);
        y += ui.S(14);
        ui.Text(Loc.T("report.profit"), new Vector2(area.X, y), 32, Theme.Cream, true);
        ui.Text(Fmt.Money(led.Profit), new Vector2(area.X + ui.S(400), y), 32, led.Profit >= 0 ? Theme.Yellow : Theme.Red, true, Align.Right);

        // Sag sutun: musteriler
        var ry = area.Y;
        void RRow(string label, string value)
        {
            ui.Text(label, new Vector2(colX, ry), 22, Theme.CreamDark);
            ui.Text(value, new Vector2(area.X + area.Width, ry), 24, Theme.Cream, true, Align.Right);
            ry += ui.S(34);
        }

        RRow(Loc.T("report.served"), $"{led.Served}");
        RRow(Loc.T("report.satisfaction"), $"{led.AvgSatisfaction:0}%");
        RRow(Loc.T("report.angry"), $"{led.Angry}");
        RRow(Loc.T("report.price_refused"), $"{led.PriceRefused}");
        RRow(Loc.T("report.refused"), $"{led.Refused}");
        RRow(Loc.T("report.xp"), $"+{led.Xp}");
        RRow(Loc.T("report.rep"), $"{led.RepStart:0.0} » {led.RepEnd:0.0}");
        RRow(Loc.T("report.money"), Fmt.Money(w.Economy.Money));

        // Ipucu: en buyuk sorun
        var tip = led.PriceRefused > led.Served / 3 && led.PriceRefused > 2 ? "report.tip_price"
            : led.Angry > 3 ? "report.tip_slow"
            : led.Served < 10 ? "report.tip_more"
            : led.AvgSatisfaction < 70 ? "report.tip_quality"
            : "report.tip_good";
        ry += ui.S(10);
        ui.Paragraph(Loc.T(tip), new Vector2(colX, ry), 20, area.X + area.Width - colX, Theme.Yellow);

        // Son gunlerin kari (kucuk cubuk grafik)
        var hist = w.Economy.History.TakeLast(7).ToList();
        var gy = area.Y + area.Height - ui.S(200);
        ui.Text(Loc.T("report.history"), new Vector2(area.X, gy), 20, Theme.CreamDark);
        var max = Math.Max(1, hist.Count == 0 ? 1 : hist.Max(h => Math.Abs(h.Profit)));
        var bx = area.X;
        foreach (var h in hist)
        {
            var bh = ui.S(90) * Math.Abs(h.Profit) / (float)max;
            var baseY = gy + ui.S(130);
            var r = h.Profit >= 0 ? new Rectangle(bx, baseY - bh, ui.S(46), bh) : new Rectangle(bx, baseY, ui.S(46), bh * 0.4f);
            Raylib.DrawRectangleRounded(r, 0.2f, 4, h.Profit >= 0 ? Theme.Green : Theme.Red);
            ui.Text($"{h.Day}", new Vector2(bx + ui.S(23), baseY + ui.S(4)), 16, Theme.CreamDark, false, Align.Center);
            bx += ui.S(56);
        }

        var by = area.Y + area.Height - ui.S(64);
        if (s.IsHost)
        {
            if (ui.Button(new Rectangle(area.X + area.Width - ui.S(340), by, ui.S(340), ui.S(64)), Loc.T("report.next"), ButtonStyle.Primary))
            {
                s.ContinueAfterReport();
            }
        }
        else
        {
            ui.Text(Loc.T("report.wait_host"), new Vector2(area.X + area.Width, by + ui.S(18)), 22, Theme.CreamDark, false, Align.Right);
        }

        if (!w.DayOver && Game.Session is { })
        {
            Game.Screens.Remove(this);
        }
    }

    /// <summary>Gider: sifirsa "-0 ₺" yerine "0 ₺".</summary>
    private static string Cost(long v) => v > 0 ? "-" + Fmt.Money(v) : Fmt.Money(0);
}
