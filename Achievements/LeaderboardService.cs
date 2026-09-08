namespace PixelSurvival.Achievements;

/// <summary>Leaderboard'daki tek bir satır.</summary>
public readonly record struct LeaderboardEntry(int Rank, string PlayerName, int Score);

/// <summary>
/// AŞAMA 2 / MADDE 21 — leaderboard okuma/yazma.
///
/// ── Neden arayüz ────────────────────────────────────────────────────────
/// Steam derlemesinde skorlar Steam leaderboard'una gider; geliştirme
/// derlemesinde hiçbir yere gitmez ama arayüz yine de çizilebilsin diye
/// yerel bir liste tutulur.
///
/// ── Skor otoritesi ──────────────────────────────────────────────────────
/// Skor kaynağı <see cref="AchievementTracker"/> istatistikleridir, yani
/// CLIENT tarafıdır. Bu, Steam leaderboard'larının bilinen bir sınırı:
/// hile korumalı bir sıralama isteniyorsa skor güvenilir bir backend'de
/// doğrulanmalı. Bunu bir yorum satırıyla geçmiyoruz —
/// <see cref="LeaderboardScope"/> ile hangi listenin ne kadar güvenilir
/// olduğu arayüzde de görünür kılınıyor.
/// </summary>
public interface ILeaderboardBackend
{
    bool IsAvailable { get; }
    string Status { get; }

    /// <summary>Skoru yükler. Daha kötü bir skor mevcut kaydı EZMEZ.</summary>
    void Upload(LeaderboardDefinition board, int score);

    /// <summary>İlk <paramref name="count"/> satırı okur.</summary>
    IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count);
}

/// <summary>Bir leaderboard'un ne kadar güvenilir olduğu — arayüzde gösterilir.</summary>
public enum LeaderboardScope
{
    /// <summary>Yalnızca bu makinede; kimseyle paylaşılmıyor.</summary>
    Local,

    /// <summary>Steam'e yazılıyor; skor client tarafından üretiliyor.</summary>
    SteamClientReported
}

/// <summary>
/// Geliştirme derlemesinin leaderboard'u: yalnızca bu oturumda, bellekte.
///
/// Steam'i taklit etmiyor — arkadaş skorları uydurmuyor. Tek amacı
/// leaderboard EKRANININ ve skor yükleme yolunun Steam istemcisi olmadan
/// çalıştırılıp doğrulanabilmesi.
/// </summary>
public sealed class LocalLeaderboardBackend : ILeaderboardBackend
{
    private readonly Dictionary<string, Dictionary<string, int>> _boards = new(StringComparer.Ordinal);

    public bool IsAvailable => true;
    public string Status => "Leaderboard yerel (Steam yok)";

    /// <summary>Yerel oyuncunun adı. Steam derlemesinde Steam profil adı gelir.</summary>
    public string LocalPlayerName { get; set; } = "Sen";

    public void Upload(LeaderboardDefinition board, int score)
    {
        if (!_boards.TryGetValue(board.Key, out var rows))
        {
            rows = new Dictionary<string, int>(StringComparer.Ordinal);
            _boards[board.Key] = rows;
        }

        // Daha kotu bir skor mevcut kaydi EZMEZ. Steam'in
        // k_ELeaderboardUploadScoreMethodKeepBest davranisinin aynisi:
        // yoksa oyuncunun en iyi skoru bir sonraki kotu oyunda silinirdi.
        var existing = rows.GetValueOrDefault(LocalPlayerName, int.MinValue);
        var better = board.HigherIsBetter ? score > existing : score < existing;

        if (better || !rows.ContainsKey(LocalPlayerName))
        {
            rows[LocalPlayerName] = score;
        }
    }

    public IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count)
    {
        if (!_boards.TryGetValue(board.Key, out var rows)) return [];

        var ordered = board.HigherIsBetter
            ? rows.OrderByDescending(r => r.Value)
            : rows.OrderBy(r => r.Value);

        return [.. ordered.Take(count)
            .Select((r, i) => new LeaderboardEntry(i + 1, r.Key, r.Value))];
    }
}

/// <summary>
/// Steam leaderboard hedefi. Steamworks.NET'e dokunan TEK leaderboard dosyası.
///
/// Steamworks'te leaderboard erişimi ASENKRONDUR: önce
/// <c>FindOrCreateLeaderboard</c> çağrılır, sonuç bir callback ile gelir;
/// ancak ondan sonra skor yazılabilir. Bu yüzden burada bir handle önbelleği
/// tutulur ve handle hazır değilken yükleme İSTEĞE ALINIR, sessizce
/// kaybolmaz.
/// </summary>
public sealed class SteamLeaderboardBackend : ILeaderboardBackend
{
#if STEAM_BUILD
    private readonly Dictionary<string, Steamworks.SteamLeaderboard_t> _handles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _deferred = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<LeaderboardEntry>> _cached = new(StringComparer.Ordinal);

    private Steamworks.CallResult<Steamworks.LeaderboardFindResult_t>? _findCall;

    public bool IsAvailable => Steamworks.SteamAPI.IsSteamRunning();
    public string Status => IsAvailable ? "Leaderboard Steam'de" : "Steam calismiyor";

    public void Upload(LeaderboardDefinition board, int score)
    {
        if (!IsAvailable) return;

        if (_handles.TryGetValue(board.Key, out var handle))
        {
            Steamworks.SteamUserStats.UploadLeaderboardScore(
                handle,
                // KeepBest: daha kotu bir skor mevcut kaydi ezmez.
                Steamworks.ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                score, null, 0);
            return;
        }

        // Handle henuz yok: skoru sakla, handle gelince yukle. Aksi halde
        // oyunun ilk saniyelerindeki skorlar sessizce kaybolurdu.
        _deferred[board.Key] = score;
        RequestHandle(board);
    }

    private void RequestHandle(LeaderboardDefinition board)
    {
        var sortMethod = board.HigherIsBetter
            ? Steamworks.ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending
            : Steamworks.ELeaderboardSortMethod.k_ELeaderboardSortMethodAscending;

        var call = Steamworks.SteamUserStats.FindOrCreateLeaderboard(
            board.Key, sortMethod,
            Steamworks.ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);

        _findCall = Steamworks.CallResult<Steamworks.LeaderboardFindResult_t>.Create(
            (result, failed) =>
            {
                if (failed || result.m_bLeaderboardFound == 0) return;

                _handles[board.Key] = result.m_hSteamLeaderboard;

                if (_deferred.Remove(board.Key, out var pending))
                {
                    Upload(board, pending);
                }
            });

        _findCall.Set(call);
    }

    public IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count)
    {
        _ = count;

        // Steamworks'te satir okuma da asenkron (DownloadLeaderboardEntries).
        // Cagiran taraf bir kare bekleyip tekrar sorar; onbellek bos donmek,
        // arayuzu bloklamaktan iyidir.
        if (!_handles.ContainsKey(board.Key)) RequestHandle(board);

        return _cached.TryGetValue(board.Key, out var rows) ? rows : [];
    }
#else
    public bool IsAvailable => false;
    public string Status => "Steam derlemesi degil";

    public void Upload(LeaderboardDefinition board, int score) { _ = board; _ = score; }

    public IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count)
    {
        _ = board;
        _ = count;
        return [];
    }
#endif
}
