#if STEAM_BUILD
using Steamworks;
using Steamworks.Data;
#endif

namespace PixelSurvival.Achievements;

/// <summary>
/// AŞAMA 2 / MADDE 21 — achievement/istatistiklerin Steam hedefi.
///
/// Facepunch.Steamworks'e DOKUNAN TEK achievement dosyası budur.
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

    /// <summary>
    /// Steam hazır mı.
    ///
    /// <c>SteamClient.IsValid</c>, <c>SteamClient.Init</c>'in başarılı olup
    /// olmadığını söylüyor. Eski Steamworks.NET karşılığı
    /// <c>SteamAPI.IsSteamRunning()</c> idi ve o, istemcinin çalışıp
    /// çalışmadığına bakıyordu — init edilmemiş bir oyunda da <c>true</c>
    /// dönebiliyordu. <c>IsValid</c> daha doğru soru: "biz bağlandık mı".
    /// </summary>
    public bool IsAvailable => SteamClient.IsValid;

    public string Status => Localization.Loc.T(
        IsAvailable ? "ach.status.steam" : "steam.notRunning");

    /// <summary>
    /// Kullanıcının mevcut istatistiklerini Steam'den ister.
    ///
    /// İstatistik yazmadan ÖNCE çağrılmalı; yoksa <c>SetStat</c> sessizce
    /// başarısız olur ve bu, canlıda fark edilmesi çok geç olan bir hata.
    /// </summary>
    public void RequestCurrentStats()
    {
        if (_requested || !IsAvailable) return;
        _requested = SteamUserStats.RequestCurrentStats();
    }

    public void SetStat(string key, int value)
    {
        if (!IsAvailable) return;

        RequestCurrentStats();
        SteamUserStats.SetStat(key, value);
    }

    public void Unlock(string achievementId)
    {
        if (!IsAvailable) return;

        RequestCurrentStats();

        // Facepunch achievement'i bir DEGER TIPI olarak modelliyor:
        // new Achievement(id).Trigger(). Steamworks.NET'teki
        // SetAchievement(string) ile ayni isi yapiyor ama kimlik bir tipe
        // sarildigi icin "hangi string neydi" sorusu ortadan kalkiyor.
        //
        // Zaten aciksa tekrar tetiklemek zararsiz; Steam yok sayar.
        new Achievement(achievementId).Trigger();
    }

    public void Flush()
    {
        if (!IsAvailable) return;

        // StoreStats basarim bildirimi ekranini da tetikleyen cagridir.
        SteamUserStats.StoreStats();
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
