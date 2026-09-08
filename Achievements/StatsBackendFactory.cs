namespace PixelSurvival.Achievements;

/// <summary>
/// AŞAMA 2 / MADDE 21 — achievement/leaderboard hedefi seçiminin TEK noktası.
///
/// <see cref="Networking.TransportFactory"/> ile aynı disiplin: hangi
/// hedefin kullanıldığı yalnızca burada, derleme sembolüyle belirlenir.
/// Oyun kabuğu <see cref="IStatsBackend"/> ve
/// <see cref="ILeaderboardBackend"/> arayüzlerini görür; Steamworks
/// tiplerini hiç görmez.
///
///     dotnet build                    -> LocalStatsBackend  (Steam gerekmez)
///     dotnet build -c SteamRelease    -> SteamStatsBackend
/// </summary>
public static class StatsBackendFactory
{
    public static IStatsBackend CreateStats() =>
#if STEAM_BUILD
        new SteamStatsBackend();
#else
        new LocalStatsBackend();
#endif

    public static ILeaderboardBackend CreateLeaderboard() =>
#if STEAM_BUILD
        new SteamLeaderboardBackend();
#else
        new LocalLeaderboardBackend();
#endif

    /// <summary>Aktif hedefin adı — HUD ve günlük için.</summary>
    public static string Name =>
#if STEAM_BUILD
        "Steam kullanici istatistikleri";
#else
        "Yerel (gelistirme)";
#endif
}
