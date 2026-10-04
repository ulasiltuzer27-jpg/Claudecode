using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;

namespace PilavciSimulator.Screens;

/// <summary>Telefon (Tab): mahalle haritasi, satis noktalari, gunun ozeti.</summary>
public sealed class PhoneScreen : OverlayScreen
{
    public override void Update(float dt, bool focused)
    {
        base.Update(dt, focused);
        if (focused && Game.Input.Pressed(GameAction.Phone))
        {
            Close();
        }
    }

    public override void DrawUi()
    {
        if (Game.Session is not { } s)
        {
            return;
        }

        var w = s.World;
        var ui = Game.Ui;
        var area = Window(1200, 760, Loc.T("phone.title"), 0.45f);
        // Harita
        var map = new Rectangle(area.X, area.Y, ui.S(760), ui.S(360));
        ui.Panel(map, Gfx.Hex(0x1E2A30), 12);
        Vector2 M(Vector3 p) => new(map.X + (p.X + 135) / 270f * map.Width, map.Y + (p.Z + 30) / 75f * map.Height);
        void RectW(float x0, float z0, float x1, float z1, Color c)
        {
            var a = M(new Vector3(x0, 0, z0));
            var b = M(new Vector3(x1, 0, z1));
            Raylib.DrawRectangleRec(new Rectangle(a.X, a.Y, b.X - a.X, b.Y - a.Y), c);
        }

        RectW(-130, 16, 130, 45, Gfx.Hex(0x2E6F8E)); // deniz
        RectW(-130, -22, 130, -8.5f, Gfx.Hex(0x6B5E52)); // kuzey binalar
        RectW(-130, 8.5f, 55, 22, Gfx.Hex(0x6B5E52)); // guney binalar
        RectW(-130, -4, 130, 4, Gfx.Hex(0x3A3A3A)); // cadde
        RectW(-130, -8.5f, 130, -4, Gfx.Hex(0xA89F91));
        RectW(-130, 4, 55, 8.5f, Gfx.Hex(0xA89F91));
        RectW(55, 4, 130, 16, Gfx.Hex(0xC9C0B0));
        RectW(97, 16, 107, 44, Gfx.Hex(0x8B6B4A));
        RectW(-112, -22, -96, -8.5f, Theme.Primary); // depo
        ui.Text(Loc.T("phone.depot"), M(new Vector3(-111, 0, -21)), 16, Theme.White, true);
        foreach (var spot in w.Layout.Spots)
        {
            if (spot.Id == "dukkan" && !w.Progress.Has("dukkan"))
            {
                continue;
            }

            var def = w.Data.SpotById[spot.Id];
            var licensed = def.License is null || w.Progress.Has(def.License);
            var c = licensed ? Theme.Green : Theme.Yellow;
            var p = M(spot.Center);
            Raylib.DrawCircleV(p, ui.S(9), c);
            ui.Text(Loc.T(def.NameKey), p + new Vector2(ui.S(12), -ui.S(10)), 16, Theme.White, true, shadow: true);
        }

        foreach (var cart in w.Carts)
        {
            var p = M(cart.Position);
            Raylib.DrawRectangleRec(new Rectangle(p.X - ui.S(7), p.Y - ui.S(5), ui.S(14), ui.S(10)), Theme.White);
        }

        foreach (var pl in w.Players)
        {
            var p = M(pl.Position);
            Raylib.DrawCircleV(p, ui.S(6), Gfx.Hex(Sim.Entities.PlayerEntity.Colors[pl.ColorIndex % 4]));
        }

        // Nokta bilgileri
        var y = map.Y + map.Height + ui.S(16);
        ui.Text(Loc.T("phone.spots"), new Vector2(area.X, y), 22, Theme.Yellow, true);
        y += ui.S(34);
        var hour = w.Clock.Hour;
        foreach (var def in w.Data.Spots)
        {
            if (def.Id == "dukkan" && !w.Progress.Has("dukkan"))
            {
                continue;
            }

            var now = def.Hours[hour] * (def.MatchDayOnly ? (w.Events.MatchToday ? 1 : 0.06f) : def.Days[w.Clock.DayOfWeek]);
            var licensed = def.License is null || w.Progress.Has(def.License);
            ui.Text(Loc.T(def.NameKey), new Vector2(area.X, y), 20, Theme.Cream, true);
            ui.Bar(new Rectangle(area.X + ui.S(170), y + ui.S(6), ui.S(200), ui.S(12)), Math.Clamp(now, 0, 1), Theme.Primary);
            ui.Text(licensed ? Loc.T("phone.licensed") : Loc.T("phone.risk", (int)(def.ZabitaRisk * 100)), new Vector2(area.X + ui.S(390), y), 18, licensed ? Theme.Green : Theme.Yellow);
            y += ui.S(30);
        }

        // Sag: gunun ozeti
        var rx = area.X + ui.S(800);
        var ry = area.Y;
        var led = w.Economy.Today;
        void Row(string k, string v)
        {
            ui.Text(Loc.T(k), new Vector2(rx, ry), 20, Theme.CreamDark);
            ui.Text(v, new Vector2(area.X + area.Width, ry), 22, Theme.Cream, true, Align.Right);
            ry += ui.S(32);
        }

        Row("report.served", $"{led.Served}");
        Row("report.revenue", Fmt.Money(led.Revenue + led.Tips));
        Row("report.satisfaction", $"{led.AvgSatisfaction:0}%");
        Row("report.angry", $"{led.Angry}");
        Row("phone.weather", Loc.T("weather." + w.Weather.Kind.ToString().ToLowerInvariant()));
        Row("phone.match", w.Events.MatchToday ? Loc.T("common.yes") : Loc.T("common.no"));
        ry += ui.S(10);
        ui.Text(Loc.T("phone.news"), new Vector2(rx, ry), 22, Theme.Yellow, true);
        ry += ui.S(32);
        foreach (var n in w.Events.News)
        {
            var parts = n.Split('|');
            ry += ui.Paragraph("• " + Loc.T(parts[0], parts.Length > 1 ? parts[1] : ""), new Vector2(rx, ry), 18, area.X + area.Width - rx, Theme.Cream) + ui.S(6);
        }

        ui.Text(Loc.T("phone.close_hint"), new Vector2(area.X, area.Y + area.Height - ui.S(26)), 17, Theme.CreamDark);
    }
}
