using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Net;

namespace PilavciSimulator.Screens;

/// <summary>
/// Calisan oyunu co-op'a acma ve arkadas davet etme. Steam derlemesinde
/// Steam lobisi + relay (port acmak gerekmez); diger durumlarda IP + UDP
/// portu gosterilir.
/// </summary>
public sealed class CoopHostScreen : OverlayScreen
{
    private readonly HostSession _host;
    private string _port = "27015";
    private string? _error;
    private string[] _addresses = [];

    public CoopHostScreen(HostSession host) => _host = host;

    public override void Enter()
    {
        base.Enter();
        _port = Game.Options.Port.ToString(CultureInfo.InvariantCulture);
        _addresses = LocalAddresses();
    }

    private bool UsesSteam
    {
        get
        {
#if STEAM_BUILD
            return Game.Platform.IsSteam && Game.Lobby is not null;
#else
            return false;
#endif
        }
    }

    public override void DrawUi()
    {
        var ui = Game.Ui;
        var area = Window(820, 640, Loc.T("coop.title"));
        var y = area.Y;
        if (_host.Transport is null)
        {
            y += ui.Paragraph(Loc.T(UsesSteam ? "coop.intro_steam" : "coop.intro_ip"), new Vector2(area.X, y), 24, area.Width, Theme.Cream) + ui.S(20);
            if (!UsesSteam)
            {
                var row = new Rectangle(area.X, y, area.Width, ui.S(58));
                ui.TextIn(row with { Width = ui.S(200) }, Loc.T("coop.port"), 26, Theme.Cream);
                ui.TextField(row with { X = area.X + ui.S(200), Width = ui.S(200) }, ref _port, 5, "27015");
                y += ui.S(80);
            }

            if (ui.Button(new Rectangle(area.X, y, ui.S(360), ui.S(64)), Loc.T("coop.open"), ButtonStyle.Primary))
            {
                if (!int.TryParse(_port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1024 or > 65535)
                {
                    _error = Loc.T("coop.bad_port");
                }
                else
                {
                    var err = GameFlow.OpenCoop(Game, _host, port);
                    _error = err is null ? null : Loc.T(err);
                    if (err is null)
                    {
                        Game.Toasts.Show(Loc.T("toast.coop_open"), Theme.Green);
                    }
                }
            }

            y += ui.S(90);
        }
        else
        {
            if (UsesSteam)
            {
                y += ui.Paragraph(Loc.T("coop.open_steam"), new Vector2(area.X, y), 24, area.Width, Theme.Cream) + ui.S(16);
                if (ui.Button(new Rectangle(area.X, y, ui.S(360), ui.S(64)), Loc.T("coop.invite"), ButtonStyle.Primary))
                {
#if STEAM_BUILD
                    Game.Lobby!.OpenInvite();
#endif
                }

                y += ui.S(86);
            }
            else
            {
                y += ui.Paragraph(Loc.T("coop.open_ip"), new Vector2(area.X, y), 24, area.Width, Theme.Cream) + ui.S(12);
                foreach (var a in _addresses.Take(3))
                {
                    ui.Text($"{a} : {_port}", new Vector2(area.X + ui.S(20), y), 30, Theme.Yellow, true);
                    y += ui.S(42);
                }

                y += ui.Paragraph(Loc.T("coop.port_forward"), new Vector2(area.X, y + ui.S(6)), 20, area.Width, Theme.CreamDark) + ui.S(20);
            }

            ui.Text(Loc.T("coop.players", _host.World.Players.Count(p => p.Connected)), new Vector2(area.X, y), 26, Theme.Yellow, true);
            y += ui.S(42);
            foreach (var p in _host.World.Players.Where(p => p.Connected))
            {
                var color = Engine.Rendering.Gfx.Hex(Sim.Entities.PlayerEntity.Colors[p.ColorIndex % Sim.Entities.PlayerEntity.Colors.Length]);
                Raylib.DrawCircleV(new Vector2(area.X + ui.S(14), y + ui.S(16)), ui.S(10), color);
                ui.Text(p.Name + (p.Id == _host.LocalPlayerId ? "  " + Loc.T("coop.you") : ""), new Vector2(area.X + ui.S(36), y), 24, Theme.Cream);
                y += ui.S(36);
            }
        }

        if (_error is not null)
        {
            ui.Paragraph(_error, new Vector2(area.X, area.Y + area.Height - ui.S(100)), 22, area.Width, Theme.Red);
        }

        if (ui.Button(new Rectangle(area.X + area.Width - ui.S(220), area.Y + area.Height - ui.S(58), ui.S(220), ui.S(58)), Loc.T("common.close")))
        {
            Close();
        }
    }

    private static string[] LocalAddresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .Distinct()
                .ToArray();
        }
        catch
        {
            return ["127.0.0.1"];
        }
    }

    public override string Annotate() => $"coop: acik={_host.Transport is not null} oyuncu={_host.World.Players.Count}";
}
