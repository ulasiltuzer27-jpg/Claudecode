using PilavciSimulator.Core;
using PilavciSimulator.Localization;
using PilavciSimulator.Net;
using PilavciSimulator.Persistence;
using PilavciSimulator.Sim;

namespace PilavciSimulator.Screens;

/// <summary>
/// Oturum kurma akislari: yeni oyun, kayit yukleme, co-op'a acma, katilma.
/// Menu, duraklatma ekrani, komut satiri ve Steam davetleri hep buradan
/// gecer; bir oturumu iki farkli yerde iki farkli bicimde kurmayalim.
/// </summary>
public static class GameFlow
{
    private static int Seed(PilavciGame g) => g.Options.Seed ?? Environment.TickCount;

    /// <summary>Yeni dunya + host oturumu (tek oyunculu da bu).</summary>
    public static HostSession CreateNewHost(PilavciGame g, bool tutorial)
    {
        var w = new GameWorld(g.Data, g.Layout, isHost: true, Seed(g));
        Simulation.InitNewGame(w);
        w.Progress.TutorialEnabled = tutorial && !g.Options.SkipTutorial;
        return new HostSession(w, g.PlayerName);
    }

    /// <summary>Yeni oyun baslatir; yuva hemen kaydedilir ki menude dolu gorunsun.</summary>
    public static HostSession StartNew(PilavciGame g, int slot, bool tutorial)
    {
        var host = CreateNewHost(g, tutorial);
        if (slot >= 0)
        {
            TrySave(g, slot, host.World);
        }

        g.Screens.ReplaceAll(new GameplayScreen(host, slot));
        Log.Info($"yeni oyun: yuva {slot}, egitim {(host.World.Progress.TutorialEnabled ? "acik" : "kapali")}");
        return host;
    }

    /// <summary>Kaydi yukleyip oyunu baslatir. Basarisizsa hata anahtari doner.</summary>
    public static HostSession? Load(PilavciGame g, int slot, out string? error)
    {
        error = null;
        try
        {
            var w = SaveSystem.Load(slot, g.Data, g.Layout);
            var host = new HostSession(w, g.PlayerName);
            g.Screens.ReplaceAll(new GameplayScreen(host, slot));
            Log.Info($"kayit yuklendi: yuva {slot}, gun {w.Clock.Day}");
            return host;
        }
        catch (Exception ex)
        {
            Log.Error($"yuva {slot} yuklenemedi", ex);
            error = "menu.load_failed";
            return null;
        }
    }

    public static void TrySave(PilavciGame g, int slot, GameWorld w)
    {
        try
        {
            SaveSystem.Save(slot, w, g.PlayerName);
        }
        catch (Exception ex)
        {
            Log.Error("kayit basarisiz", ex);
            g.Toasts.Show(Loc.T("toast.save_failed"), Engine.Ui.Theme.Red);
        }
    }

    /// <summary>
    /// Calisan host oturumunu arkadaslara acar. Steam varsa relay + lobi
    /// (NAT derdi yok), yoksa dogrudan UDP portu. Hata anahtari ya da null.
    /// </summary>
    public static string? OpenCoop(PilavciGame g, HostSession host, int port)
    {
        if (host.Transport is not null)
        {
            return null;
        }

        try
        {
#if STEAM_BUILD
            if (g.Platform.IsSteam && g.Lobby is not null)
            {
                host.OpenToNetwork(new SteamTransport(), port);
                _ = g.Lobby.CreateAsync(4, Protocol.Version.ToString());
                g.Platform.SetRichPresence("steam_player_group", g.Platform.PlayerId.ToString());
                return null;
            }
#endif
            host.OpenToNetwork(new LiteNetLibTransport(), port);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("co-op acilamadi", ex);
            return "coop.open_failed";
        }
    }

    /// <summary>IP ile katil (LAN ya da port yonlendirmeli internet).</summary>
    public static void JoinIp(PilavciGame g, string address, int port)
    {
        try
        {
            var client = new ClientSession(new LiteNetLibTransport(), g.Data, g.Layout, address, port, g.PlayerName);
            g.Screens.ReplaceAll(new GameplayScreen(client, -1));
        }
        catch (Exception ex)
        {
            Log.Error("baglanti kurulamadi", ex);
            g.Screens.ReplaceAll(new MainMenuScreen(Loc.T("net.connect_failed")));
        }
    }

#if STEAM_BUILD
    /// <summary>Steam davetini kabul etti: host'un SteamId'sine relay ile baglan.</summary>
    public static void JoinSteam(PilavciGame g, ulong hostId)
    {
        if (g.Session is { } current)
        {
            // Oyundayken davet: mevcut oturum ekranla birlikte kapanir (host ise kayit).
            if (current is HostSession h && g.Screens.Find<GameplayScreen>() is { SaveSlot: >= 0 } gs)
            {
                TrySave(g, gs.SaveSlot, h.World);
            }
        }

        try
        {
            var client = new ClientSession(new SteamTransport(), g.Data, g.Layout, hostId.ToString(), 0, g.PlayerName);
            g.Screens.ReplaceAll(new GameplayScreen(client, -1));
        }
        catch (Exception ex)
        {
            Log.Error("Steam baglantisi kurulamadi", ex);
            g.Screens.ReplaceAll(new MainMenuScreen(Loc.T("net.connect_failed")));
        }
    }
#endif
}
