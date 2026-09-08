namespace PixelSurvival.Achievements;

/// <summary>Yeni açılan bir achievement — arayüzde bildirim göstermek için.</summary>
public readonly record struct AchievementUnlock(AchievementDefinition Definition);

/// <summary>
/// AŞAMA 2 / MADDE 21 — achievement tetikleme mantığı.
///
/// ── Neden istatistik tabanlı ─────────────────────────────────────────────
/// Alternatif, oyun kodunun her yerine <c>Unlock("ACH_X")</c> serpmekti.
/// O yaklaşımda:
///   • yeni bir achievement her seferinde yeni bir kod değişikliği ister,
///   • eşik değiştirmek kod değiştirmek demektir,
///   • bir çağrı unutulduğunda achievement sessizce hiç açılmaz.
///
/// Burada oyun yalnızca OLAN BİTENİ rapor ediyor (<see cref="Add"/>,
/// <see cref="SetMax"/>); hangi achievement'in hangi eşikte açılacağına
/// veri karar veriyor. Yeni achievement eklemek artık JSON işi.
///
/// ── Kritik kural ────────────────────────────────────────────────────────
/// Bir achievement'in açılması TEK BAŞINA marketable/tradable bir Steam
/// item'ı grant EDEMEZ. Bu sınıfta ödül veren bir metot YOKTUR; ödül yolu
/// madde 19'daki <c>ISteamItemGrantAuthority</c> üzerinden güvenilir
/// backend'dir. Bu yüzden burada <c>SteamInventory</c> tipine referans yok.
/// </summary>
public sealed class AchievementTracker
{
    private readonly AchievementCatalog _catalog;
    private readonly IStatsBackend _backend;

    private readonly Dictionary<string, int> _stats = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

    /// <summary>Henüz arayüzde gösterilmemiş açılışlar.</summary>
    private readonly Queue<AchievementUnlock> _pending = new();

    /// <summary>İstatistikler değişti mi — kare sonunda tek Flush için.</summary>
    private bool _dirty;

    public AchievementTracker(AchievementCatalog catalog, IStatsBackend backend)
    {
        _catalog = catalog;
        _backend = backend;

        foreach (var stat in catalog.Stats)
        {
            _stats[stat.Key] = 0;
        }
    }

    public string Status => _backend.Status;

    /// <summary>Açılmış achievement sayısı / toplam — arayüzde ilerleme çubuğu.</summary>
    public int UnlockedCount => _unlocked.Count;
    public int TotalCount => _catalog.Achievements.Count;

    public bool IsUnlocked(AchievementDefinition definition) => _unlocked.Contains(definition.Id);

    public IReadOnlyList<AchievementDefinition> All => _catalog.Achievements;

    /// <summary>Bir istatistiğin güncel değeri (leaderboard bunu yazar).</summary>
    public int Value(StatKey key) => _stats.GetValueOrDefault(key.Value);

    /// <summary>Tüm istatistikler — kaydetmek için.</summary>
    public IReadOnlyDictionary<string, int> AllStats => _stats;

    /// <summary>Açılmış achievement kimlikleri — kaydetmek için.</summary>
    public IReadOnlyCollection<string> UnlockedIds => _unlocked;

    /// <summary>
    /// Kayittan gelen ilerlemeyi geri kurar.
    ///
    /// Acilmis achievement'lar BILDIRIM URETMEZ: yukleme aninda oyuncunun
    /// ekranini onlarca "basarim acildi" balonuyla doldurmak, kaydin
    /// yuklendigini degil hepsinin yeniden acildigini dusundururdu.
    /// Bu yuzden kuyruk beslenmiyor, yalnizca kume dolduruluyor.
    /// </summary>
    public void Restore(IReadOnlyDictionary<string, int> stats,
                        IEnumerable<string> unlocked)
    {
        foreach (var (key, value) in stats)
        {
            // Tanimsiz istatistik yok sayilir: kayit eski bir surumden
            // gelmis olabilir ve tek fazla anahtar yuklemeyi engellememeli.
            if (_stats.ContainsKey(key)) _stats[key] = value;
        }

        _unlocked.Clear();
        foreach (var id in unlocked) _unlocked.Add(id);

        // Hedefe de yaz: Steam tarafi bu oturumda dogru degeri gorsun.
        _dirty = true;
    }

    /// <summary>
    /// Biriken bir istatistiği artırır (toplanan odun, öldürülen düşman…).
    ///
    /// Tanımsız anahtar SESSİZCE yok sayılmaz: yazım hatası yüzünden hiç
    /// açılmayan bir achievement, fark edilmesi en zor hatalardan biri.
    /// </summary>
    public void Add(StatKey key, int amount = 1)
    {
        if (amount <= 0) return;

        if (!_stats.ContainsKey(key.Value))
        {
            throw new ArgumentException(
                $"'{key}' achievements.json'da tanimli bir istatistik degil.", nameof(key));
        }

        _stats[key.Value] += amount;
        _dirty = true;
        Evaluate(key);
    }

    /// <summary>
    /// Birikmeyen, "en yüksek değeri" tutan istatistik (hayatta kalınan gün).
    ///
    /// <see cref="Add"/> kullanılsaydı her karede gün sayısı tekrar
    /// eklenirdi. Burada yalnızca daha büyük bir değer geldiğinde yazılır.
    /// </summary>
    public void SetMax(StatKey key, int value)
    {
        if (!_stats.ContainsKey(key.Value))
        {
            throw new ArgumentException(
                $"'{key}' achievements.json'da tanimli bir istatistik degil.", nameof(key));
        }

        if (value <= _stats[key.Value]) return;

        _stats[key.Value] = value;
        _dirty = true;
        Evaluate(key);
    }

    /// <summary>Bu istatistiğe bağlı achievement'lardan eşiği geçenleri açar.</summary>
    private void Evaluate(StatKey key)
    {
        var current = _stats[key.Value];

        foreach (var achievement in _catalog.Achievements)
        {
            if (achievement.Stat != key.Value) continue;
            if (current < achievement.Threshold) continue;
            if (!_unlocked.Add(achievement.Id)) continue;

            _backend.Unlock(achievement.Id);
            _pending.Enqueue(new AchievementUnlock(achievement));
        }
    }

    /// <summary>
    /// Kare sonunda çağrılır: değişen istatistikleri hedefe TEK SEFERDE
    /// gönderir. Steamworks'te <c>StoreStats</c> bir ağ çağrısıdır;
    /// her artışta çağrılması gereksiz trafik olurdu.
    /// </summary>
    public void Flush()
    {
        if (!_dirty) return;

        foreach (var (key, value) in _stats)
        {
            _backend.SetStat(key, value);
        }

        _backend.Flush();
        _dirty = false;
    }

    /// <summary>
    /// Bekleyen bir açılışı kuyruktan alır. Arayüz her karede bunu çağırıp
    /// bildirim gösterir.
    /// </summary>
    public bool TryDequeueUnlock(out AchievementUnlock unlock)
    {
        if (_pending.Count > 0)
        {
            unlock = _pending.Dequeue();
            return true;
        }

        unlock = default;
        return false;
    }
}
