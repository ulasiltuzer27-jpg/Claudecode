namespace PixelSurvival.Achievements;

/// <summary>
/// AŞAMA 2 / MADDE 21 — achievement/istatistiklerin Steamworks hedefi.
///
/// Steamworks.NET'e DOKUNAN TEK achievement dosyası budur.
/// <see cref="AchievementTracker"/> bu tipi hiç görmez; yalnızca
/// <see cref="IStatsBackend"/> arayüzünü görür. Böylece tetikleme mantığı
/// Steam istemcisi olmadan da çalıştırılıp doğrulanabiliyor.
///
/// Derleme: gerçek gövde yalnızca <c>STEAM_BUILD</c> sembolüyle derlenir.
/// Sembol yokken sınıf vardır ama hiçbir şey yapmaz — böylece Game1'de
/// koşullu derleme (#if) gerekmiyor.
/// </summary>
public sealed class SteamStatsBackend : IStatsBackend
{
#if STEAM_BUILD
    private bool _requested;

    public bool IsAvailable => Steamworks.SteamAPI.IsSteamRunning();

    public string Status => Localization.Loc.T(
        IsAvailable ? "ach.status.steam" : "steam.notRunning");

    /// <summary>
    /// Kullanıcının mevcut istatistiklerini Steam'den ister.
    ///
    /// Steamworks'te istatistik yazmadan ÖNCE <c>RequestCurrentStats</c>
    /// çağrılmalı; yoksa <c>SetStat</c> sessizce başarısız olur ve bu,
    /// canlıda fark edilmesi çok geç olan bir hatadır.
    /// </summary>
    public void RequestCurrentStats()
    {
        if (_requested || !IsAvailable) return;
        _requested = Steamworks.SteamUserStats.RequestCurrentStats();
    }

    public void SetStat(string key, int value)
    {
        if (!IsAvailable) return;

        RequestCurrentStats();
        Steamworks.SteamUserStats.SetStat(key, value);
    }

    public void Unlock(string achievementId)
    {
        if (!IsAvailable) return;

        RequestCurrentStats();

        // Zaten aciksa tekrar acmak zararsiz; Steam de yok sayar.
        Steamworks.SteamUserStats.SetAchievement(achievementId);
    }

    public void Flush()
    {
        if (!IsAvailable) return;

        // StoreStats basarim bildirimi ekranini da tetikleyen cagridir.
        Steamworks.SteamUserStats.StoreStats();
    }
#else
    // Steam'siz derleme: sinif var, gorevi yok. Bu sayede cagiran taraf
    // #if ile dallanmak zorunda kalmiyor.
    public bool IsAvailable => false;
    public string Status => Localization.Loc.T("steam.notSteamBuild");

    public void RequestCurrentStats() { }
    public void SetStat(string key, int value) { _ = key; _ = value; }
    public void Unlock(string achievementId) { _ = achievementId; }
    public void Flush() { }
#endif
}
