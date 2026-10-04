using Starfall.Core;

namespace Starfall.Achievements;

/// <summary>
/// Basarimlari istatistiklere gore acar; yerel profilde saklar ve Steam'e bildirir.
/// Steam'e ulasilamasa da oyun icinde acilir; bir sonraki acilista yerel kayitlar
/// Steam'e yeniden gonderilir (idempotent).
/// </summary>
public sealed class AchievementTracker
{
    public readonly Stats Stats;
    public readonly Dictionary<string, long> Unlocked;
    private readonly Events _events;
    private readonly Dictionary<string, List<AchievementDef>> _byStat = new();
    private float _storeT = -1;

    public AchievementTracker(Stats stats, Dictionary<string, long>? unlocked, Events events)
    {
        Stats = stats;
        Unlocked = new Dictionary<string, long>(unlocked ?? new());
        _events = events;
        foreach (var a in AchievementData.All)
        {
            if (!_byStat.TryGetValue(a.Stat, out var l)) _byStat[a.Stat] = l = new();
            l.Add(a);
        }
        events.On("stat", e =>
        {
            Check(e.Key!, Stats.Get(e.Key!));
            Steam.SetStat(e.Key!, Stats.Get(e.Key!));
            _storeT = 2;
        });
    }

    public void Check(string key, double value)
    {
        if (!_byStat.TryGetValue(key, out var l)) return;
        foreach (var a in l)
            if (!Unlocked.ContainsKey(a.Id) && value >= a.Threshold) Unlock(a);
    }

    public void CheckAll()
    {
        foreach (var k in _byStat.Keys) Check(k, Stats.Get(k));
    }

    private void Unlock(AchievementDef a)
    {
        Unlocked[a.Id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Steam.Activate(a.Id);
        Steam.Store();
        _events.Emit("achievement", key: a.Id, data: a);
    }

    public void Update(float dt)
    {
        if (_storeT < 0) return;
        _storeT -= dt;
        if (_storeT < 0) Steam.Store();
    }

    /// <summary>Acilista: yerelde acik olan her seyi Steam'e tekrar bildir.</summary>
    public void SyncToSteam()
    {
        foreach (var id in Unlocked.Keys) Steam.Activate(id);
        foreach (var (k, v) in Stats.Values) Steam.SetStat(k, v);
        Steam.Store();
    }

    public bool IsUnlocked(string id) => Unlocked.ContainsKey(id);

    public float Progress(AchievementDef a) => (float)Math.Min(1, Stats.Get(a.Stat) / a.Threshold);

    public int Count => Unlocked.Keys.Count(id => AchievementData.All.Any(a => a.Id == id));

    public IEnumerable<string> Missing => AchievementData.All.Where(a => !IsUnlocked(a.Id)).Select(a => a.Id);
}
