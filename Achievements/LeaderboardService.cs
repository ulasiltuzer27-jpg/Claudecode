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
    public string Status => Localization.Loc.T("lb.status.local");

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
/// Steam leaderboard hedefi. Facepunch.Steamworks'e dokunan TEK leaderboard
/// dosyası.
///
/// ── Neden asenkron ──────────────────────────────────────────────────────
/// Leaderboard erişimi doğası gereği asenkron: önce board bulunur/açılır,
/// sonra skor yazılır, satırlar ayrıca indirilir. Steamworks.NET'te bu üç
/// adım <c>CallResult</c> geri çağrılarıyla elle örülüyordu ve satır okuma
/// hiç bitirilmemişti — <c>Top</c> her zaman boş dönüyordu çünkü önbelleği
/// dolduran kod yoktu. Facepunch aynı işi <c>Task</c> döndürerek veriyor;
/// üç adım tek bir <c>async</c> metotta okunur hâlde duruyor ve satır
/// okuma da gerçekten çalışıyor.
///
/// ── Oyun döngüsü bloklanmıyor ───────────────────────────────────────────
/// <see cref="Upload"/> ve <see cref="Top"/> SENKRON kalıyor: çağıran
/// oyun döngüsü ve orada <c>await</c> etmek kareyi dondururdu. İkisi de
/// arka planda bir görev başlatıp hemen dönüyor; sonuç geldiğinde
/// önbelleğe yazılıyor ve bir sonraki kare onu görüyor.
///
/// ── İş parçacığı ────────────────────────────────────────────────────────
/// Görev devamları iş parçacığı havuzunda koşuyor (MonoGame'in
/// <c>SynchronizationContext</c>'i yok), yani önbelleğe ARKA PLANDAN
/// yazılıyor ve oyun döngüsü ondan okuyor. Bu yüzden paylaşılan durum
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// ve yazılan değer her seferinde YENİ bir liste: okuyan taraf hiçbir
/// zaman yarı dolmuş bir koleksiyon görmüyor.
/// </summary>
public sealed class SteamLeaderboardBackend : ILeaderboardBackend
{
#if STEAM_BUILD
    /// <summary>Bir listeden kaç satır çekileceği.</summary>
    private const int RowsToFetch = 10;

    // Facepunch'in kendi LeaderboardEntry'si var ve bu dosyada bizim
    // LeaderboardEntry'miz de tanimli. `using Steamworks.Data;` yazilsaydi
    // ad belirsiz kalirdi; o yuzden Facepunch tipleri TAM ADIYLA geciyor.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        Task<Steamworks.Data.Leaderboard?>> _boards = new(StringComparer.Ordinal);

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        IReadOnlyList<LeaderboardEntry>> _cached = new(StringComparer.Ordinal);

    /// <summary>Satirlari su an indirilmekte olan listeler — ayni istegi iki kez acmamak icin.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool>
        _fetching = new(StringComparer.Ordinal);

    public bool IsAvailable => Steamworks.SteamClient.IsValid;

    public string Status =>
        Localization.Loc.T(IsAvailable ? "lb.status.steam" : "steam.notRunning");

    /// <summary>
    /// Skoru yükler.
    ///
    /// Board henüz açılmamışsa bu çağrı onu açar ve skoru AÇILDIKTAN SONRA
    /// yazar; eski sürümde bunun için ayrı bir "ertelenmiş skor" sözlüğü
    /// tutuluyordu. Tek bir <c>await</c> zinciri o defteri gereksiz
    /// kılıyor.
    /// </summary>
    public void Upload(LeaderboardDefinition board, int score)
    {
        if (!IsAvailable) return;

        _ = UploadAsync(board, score);
    }

    private async Task UploadAsync(LeaderboardDefinition board, int score)
    {
        try
        {
            var leaderboard = await ResolveAsync(board);
            if (leaderboard is not { } target) return;

            // SubmitScoreAsync Steam tarafinda KeepBest davranisi
            // uyguluyor: daha kotu bir skor mevcut kaydi EZMEZ. Zorla
            // degistirmek isteyen ayri bir metot var (ReplaceScore), yani
            // bu davranis kazara degil.
            await target.SubmitScoreAsync(score);
        }
        catch (Exception ex)
        {
            // Arka plan gorevinde yakalanmayan bir istisna sureci
            // dusurebilir. Leaderboard, oyunun calismasi icin kritik
            // degil: raporla ve devam et.
            Console.WriteLine($"[steam] leaderboard yukleme basarisiz ({board.Key}): {ex.Message}");
        }
    }

    /// <summary>
    /// Önbellekteki satırları döndürür ve gerekiyorsa tazelemeyi başlatır.
    ///
    /// İlk çağrıda boş dönmesi normal: satırlar ağdan geliyor. Çağıran
    /// arayüz her karede soruyor, veri gelince kendiliğinden dolacak.
    /// </summary>
    public IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count)
    {
        if (!IsAvailable) return [];

        if (!_cached.ContainsKey(board.Key)) _ = RefreshAsync(board);

        return _cached.TryGetValue(board.Key, out var rows)
            ? rows.Take(count).ToArray()
            : [];
    }

    private async Task RefreshAsync(LeaderboardDefinition board)
    {
        // Ayni liste icin ikinci bir indirme acilmasin: arayuz her karede
        // Top() cagiriyor ve koruma olmasa saniyede 60 istek giderdi.
        if (!_fetching.TryAdd(board.Key, true)) return;

        try
        {
            var leaderboard = await ResolveAsync(board);
            if (leaderboard is not { } target) return;

            var entries = await target.GetScoresAsync(RowsToFetch);
            if (entries is null) return;

            // YENI liste yaziliyor: okuyan oyun dongusu hicbir zaman
            // yari dolmus bir koleksiyon gormuyor.
            _cached[board.Key] =
            [
                .. entries.Select(e => new LeaderboardEntry(
                    e.GlobalRank,
                    e.User.Name ?? "?",
                    e.Score))
            ];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[steam] leaderboard okuma basarisiz ({board.Key}): {ex.Message}");
        }
        finally
        {
            _fetching.TryRemove(board.Key, out _);
        }
    }

    /// <summary>
    /// Board'u bulur/açar ve SONUCU ÖNBELLEKLER.
    ///
    /// Önbelleklenen şey <c>Leaderboard</c> değil <c>Task</c>: aynı board
    /// için eşzamanlı iki istek geldiğinde ikisi de aynı görevi bekliyor,
    /// yani Steam'e tek çağrı gidiyor.
    /// </summary>
    private Task<Steamworks.Data.Leaderboard?> ResolveAsync(LeaderboardDefinition board) =>
        _boards.GetOrAdd(board.Key, _ => Steamworks.SteamUserStats.FindOrCreateLeaderboardAsync(
            board.Key,
            board.HigherIsBetter
                ? Steamworks.Data.LeaderboardSort.Descending
                : Steamworks.Data.LeaderboardSort.Ascending,
            Steamworks.Data.LeaderboardDisplay.Numeric));
#else
    public bool IsAvailable => false;
    public string Status => Localization.Loc.T("steam.notSteamBuild");

    public void Upload(LeaderboardDefinition board, int score) { _ = board; _ = score; }

    public IReadOnlyList<LeaderboardEntry> Top(LeaderboardDefinition board, int count)
    {
        _ = board;
        _ = count;
        return [];
    }
#endif
}
