using System.Reflection;
using PilavciSimulator.Core;
#if STEAM_BUILD
using Steamworks;
#endif

namespace PilavciSimulator.Platform;

/// <summary>
/// Steam hizmetleri (Facepunch.Steamworks). Yalnizca <c>SteamRelease</c>
/// yapilandirmasinda derlenir; diger derlemelerde bu dosyanin govdesi bos.
///
/// AppId tek kaynaktan gelir: csproj'daki <c>SteamAppId</c> ozelligi
/// AssemblyMetadata olarak buraya tasinir.
/// </summary>
public static class SteamService
{
    public static uint AppId
    {
        get
        {
            var attr = typeof(SteamService).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "SteamAppId");
            return attr?.Value is { } v && uint.TryParse(v, out var id) ? id : 480u;
        }
    }

    /// <summary>
    /// Oyun Steam disindan baslatildiysa Steam uzerinden yeniden baslatir.
    /// steam_appid.txt varsa (gelistirme) atlanir.
    /// </summary>
    public static bool RestartIfNecessary()
    {
#if STEAM_BUILD
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "steam_appid.txt")))
        {
            return false;
        }

        try
        {
            return SteamClient.RestartAppIfNecessary(AppId);
        }
        catch (Exception ex)
        {
            // steam_api64.dll yoksa (Linux, eksik kopya) burada patlar; oyun Steam'siz acilsin.
            Log.Warn($"Steam yeniden baslatma kontrolu yapilamadi: {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }
}

#if STEAM_BUILD
public sealed class SteamPlatform : IPlatform
{
    private readonly Dictionary<string, Task<Steamworks.Data.Leaderboard?>> _boards = new(StringComparer.Ordinal);
    private bool _overlay;

    private SteamPlatform()
    {
        SteamFriends.OnGameOverlayActivated += active => _overlay = active;
    }

    public static IPlatform? TryCreate()
    {
        try
        {
            // asyncCallbacks: false -> geri cagirimlar ana iş parçacığında,
            // RunCallbacks icinde gelir; oyun durumu tek iş parçacığından degisir.
            SteamClient.Init(SteamService.AppId, asyncCallbacks: false);
            Log.Info($"Steam baslatildi: AppId {SteamClient.AppId}, kullanici {SteamClient.Name}");
            SteamUserStats.RequestCurrentStats();
            return new SteamPlatform();
        }
        catch (Exception ex)
        {
            Log.Warn($"Steam baslatilamadi, yerel modda devam: {ex.Message}");
            return null;
        }
    }

    public bool IsSteam => true;
    public bool Ready => SteamClient.IsValid;
    public string PlayerName => SteamClient.Name;
    public ulong PlayerId => SteamClient.SteamId;
    public string? GameLanguage => SteamApps.GameLanguage;
    public bool OverlayActive => _overlay;

    public void RunCallbacks()
    {
        if (SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    public void UnlockAchievement(string id)
    {
        try
        {
            new Steamworks.Data.Achievement(id).Trigger();
        }
        catch (Exception ex)
        {
            Log.Warn($"basarim acilamadi ({id}): {ex.Message}");
        }
    }

    public bool IsAchievementUnlocked(string id)
    {
        try
        {
            return new Steamworks.Data.Achievement(id).State;
        }
        catch
        {
            return false;
        }
    }

    public void SetStat(string name, int value) => SteamUserStats.SetStat(name, value);

    public void StoreStats() => SteamUserStats.StoreStats();

    public void SubmitLeaderboard(string board, int score)
    {
        if (!_boards.TryGetValue(board, out var task))
        {
            task = SteamUserStats.FindOrCreateLeaderboardAsync(board,
                Steamworks.Data.LeaderboardSort.Descending, Steamworks.Data.LeaderboardDisplay.Numeric);
            _boards[board] = task;
        }

        task.ContinueWith(t =>
        {
            if (t.Result is { } lb)
            {
                _ = lb.SubmitScoreAsync(score);
            }
        }, TaskScheduler.Default);
    }

    public void SetRichPresence(string key, string? value)
    {
        if (value is null)
        {
            SteamFriends.SetRichPresence(key, null);
        }
        else
        {
            SteamFriends.SetRichPresence(key, value);
        }
    }

    public void OpenInviteOverlay() => SteamFriends.OpenOverlay("friends");

    public void Dispose()
    {
        try
        {
            SteamUserStats.StoreStats();
            SteamClient.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warn($"Steam kapatilirken: {ex.Message}");
        }
    }
}
#endif

public static class PlatformFactory
{
    public static IPlatform Create(LaunchOptions options, GameSettings settings)
    {
#if STEAM_BUILD
        var steam = SteamPlatform.TryCreate();
        if (steam is not null)
        {
            return steam;
        }
#endif
        var name = options.PlayerName ?? (settings.PlayerName.Length > 0 ? settings.PlayerName : Environment.UserName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Pilavci";
        }

        return new LocalPlatform(name.Length > 20 ? name[..20] : name);
    }
}
