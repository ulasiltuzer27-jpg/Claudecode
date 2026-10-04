namespace PilavciSimulator.Platform;

/// <summary>
/// Platform hizmetleri (Steam ya da yerel). Oyun kodu Steamworks tiplerini
/// ASLA dogrudan gormez; yalnizca bu arayuzu. Kalip PixelSurvival'daki
/// IStatsBackend + fabrika duzeninden alindi.
/// </summary>
public interface IPlatform : IDisposable
{
    bool IsSteam { get; }
    bool Ready { get; }
    string PlayerName { get; }
    ulong PlayerId { get; }
    /// <summary>Steam istemci dili ("turkish", "english") ya da null.</summary>
    string? GameLanguage { get; }

    void RunCallbacks();

    // ── Basarim / istatistik ─────────────────────────────────────────
    void UnlockAchievement(string id);
    bool IsAchievementUnlocked(string id);
    void SetStat(string name, int value);
    void StoreStats();
    void SubmitLeaderboard(string board, int score);

    // ── Zengin durum ve davet ────────────────────────────────────────
    void SetRichPresence(string key, string? value);
    void OpenInviteOverlay();
    bool OverlayActive { get; }
}

/// <summary>Steam'siz calisma: basarimlar yerel dosyada tutulur (bkz. AchievementTracker).</summary>
public sealed class LocalPlatform : IPlatform
{
    private readonly HashSet<string> _unlocked = new();

    public LocalPlatform(string playerName) => PlayerName = playerName;

    public bool IsSteam => false;
    public bool Ready => true;
    public string PlayerName { get; }
    public ulong PlayerId => 0;
    public string? GameLanguage => null;
    public bool OverlayActive => false;

    public void RunCallbacks()
    {
    }

    public void UnlockAchievement(string id) => _unlocked.Add(id);
    public bool IsAchievementUnlocked(string id) => _unlocked.Contains(id);

    public void SetStat(string name, int value)
    {
    }

    public void StoreStats()
    {
    }

    public void SubmitLeaderboard(string board, int score)
    {
    }

    public void SetRichPresence(string key, string? value)
    {
    }

    public void OpenInviteOverlay()
    {
    }

    public void Dispose()
    {
    }
}
