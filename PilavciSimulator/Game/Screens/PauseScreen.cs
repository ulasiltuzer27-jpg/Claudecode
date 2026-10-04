using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Persistence;

namespace PilavciSimulator.Screens;

public sealed class PauseScreen : OverlayScreen
{
    private bool _confirmQuit;

    /// <summary>Tek oyunculuda oyun durur; co-op'ta dunya akmaya devam eder.</summary>
    public override bool PausesBelow => Game.Session is { IsMultiplayer: false };

    public override void DrawUi()
    {
        var ui = Game.Ui;
        var area = Window(520, _confirmQuit ? 360 : 640, Loc.T("pause.title"));
        var y = area.Y;
        var bw = area.Width;
        var bh = ui.S(64);
        Rectangle Row()
        {
            var r = new Rectangle(area.X, y, bw, bh);
            y += bh + ui.S(14);
            return r;
        }

        if (_confirmQuit)
        {
            ui.Paragraph(Loc.T("pause.confirm_quit"), new Vector2(area.X, y), 24, bw, Theme.Cream);
            y += ui.S(100);
            if (ui.Button(Row(), Loc.T("pause.quit_yes"), ButtonStyle.Danger))
            {
                Game.Screens.ReplaceAll(new MainMenuScreen());
            }

            if (ui.Button(Row(), Loc.T("common.back")))
            {
                _confirmQuit = false;
                ui.ResetFocus();
            }

            return;
        }

        if (ui.Button(Row(), Loc.T("pause.resume"), ButtonStyle.Primary))
        {
            Close();
        }

        var gs = Game.Screens.Find<GameplayScreen>();
        var canSave = gs is { Session.IsHost: true, SaveSlot: >= 0 };
        if (ui.Button(Row(), Loc.T("pause.save"), ButtonStyle.Normal, canSave))
        {
            SaveSystem.Save(gs!.SaveSlot, gs.Session.World, Game.Platform.PlayerName);
            Game.Toasts.Show(Loc.T("toast.saved"), Theme.Green);
        }

        if (ui.Button(Row(), Loc.T("menu.settings")))
        {
            Game.Screens.Push(new SettingsScreen());
        }

        var host = gs?.Session as Net.HostSession;
        if (ui.Button(Row(), Loc.T(host?.Transport is null ? "pause.open_coop" : "pause.invite"), ButtonStyle.Normal, host is not null))
        {
            Game.Screens.Push(new CoopHostScreen(host!));
        }

        if (ui.Button(Row(), Loc.T("pause.mainmenu"), ButtonStyle.Danger))
        {
            _confirmQuit = true;
            ui.ResetFocus();
        }

        ui.Text(Loc.T("pause.hint"), new Vector2(area.X, area.Y + area.Height - ui.S(30)), 18, Theme.CreamDark);
    }
}
