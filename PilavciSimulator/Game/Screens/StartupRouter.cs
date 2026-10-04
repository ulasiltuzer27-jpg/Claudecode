using PilavciSimulator.Core;

namespace PilavciSimulator.Screens;

/// <summary>
/// Acilista hangi ekranla baslanacagi. Komut satiri kisayollari
/// (--new-game / --host / --join) menuyu atlar; otomatik dogrulama ve
/// co-op testi bunlari kullanir. Steam, davetle baslatilan oyuna
/// <c>+connect_lobby &lt;id&gt;</c> ekler; o da burada yakalanir.
/// </summary>
public static class StartupRouter
{
    public static void Start(PilavciGame game)
    {
        var o = game.Options;
        if (o.JoinAddress is { } address)
        {
            GameFlow.JoinIp(game, address, o.Port);
            return;
        }

        if (o.Host || o.NewGame)
        {
            // Komut satiri oyunlari kayit yuvasina yazmaz: gercek kayitlar ezilmesin.
            var host = GameFlow.StartNew(game, -1, tutorial: !o.SkipTutorial);
            if (o.Host)
            {
                var err = GameFlow.OpenCoop(game, host, o.Port);
                if (err is not null)
                {
                    Log.Warn($"--host: co-op acilamadi ({err})");
                }
            }

            return;
        }

#if STEAM_BUILD
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, "+connect_lobby");
        if (i >= 0 && i + 1 < args.Length && ulong.TryParse(args[i + 1], out var lobbyId) && game.Lobby is not null)
        {
            Log.Info($"Steam daveti ile baslatildi: lobi {lobbyId}");
            game.Screens.ReplaceAll(new MainMenuScreen());
            _ = game.Lobby.JoinAsync(new Steamworks.Data.Lobby(lobbyId));
            return;
        }
#endif

        game.Screens.ReplaceAll(new MainMenuScreen());
    }
}
