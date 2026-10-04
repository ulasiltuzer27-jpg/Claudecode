using Steamworks;

namespace Starfall.Achievements;

/// <summary>
/// Steamworks.NET koprusu. Steam istemcisi yoksa (veya steam_api kitapligi bulunamazsa)
/// sessizce yerel moda duser: oyun ayni sekilde oynanir, basarimlar profilde saklanir.
/// </summary>
public static class Steam
{
    public static bool Available { get; private set; }
    public static string Reason { get; private set; } = "kapali";

    public static void Init(bool enabled)
    {
        if (!enabled) { Reason = "devre disi"; return; }
        try
        {
            if (!Packsize.Test() || !DllCheck.Test()) { Reason = "steam_api uyumsuz"; return; }
            Available = SteamAPI.Init();
            Reason = Available ? "ok" : "Steam calismiyor";
            if (Available) SteamUserStats.RequestCurrentStats();
        }
        catch (Exception ex)
        {
            Available = false;
            Reason = ex is DllNotFoundException ? "steam_api kitapligi yok" : ex.GetType().Name;
        }
    }

    public static void RunCallbacks()
    {
        if (!Available) return;
        try { SteamAPI.RunCallbacks(); } catch { Available = false; }
    }

    public static void Activate(string id)
    {
        if (!Available) return;
        try { SteamUserStats.SetAchievement(id); } catch { /* yok say */ }
    }

    public static void SetStat(string name, double value)
    {
        if (!Available) return;
        try { SteamUserStats.SetStat(name, (int)Math.Floor(value)); } catch { /* yok say */ }
    }

    public static void Store()
    {
        if (!Available) return;
        try { SteamUserStats.StoreStats(); } catch { /* yok say */ }
    }

    public static void RichPresence(string key, string value)
    {
        if (!Available) return;
        try { SteamFriends.SetRichPresence(key, value); } catch { /* yok say */ }
    }

    public static void Shutdown()
    {
        if (!Available) return;
        try { SteamAPI.Shutdown(); } catch { /* yok say */ }
        Available = false;
    }
}
