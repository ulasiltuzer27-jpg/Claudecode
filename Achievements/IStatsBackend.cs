namespace PixelSurvival.Achievements;

/// <summary>
/// Achievement ve istatistiklerin kalıcı olarak yazıldığı yer.
///
/// Neden arayüz? Hedef derlemeye göre değişiyor:
///   • Steam derlemesi → Steamworks kullanıcı istatistikleri
///   • Geliştirme derlemesi → bellek içi, konsola raporlayan sahte hedef
///
/// Tetikleme mantığı (<see cref="AchievementTracker"/>) hangisinin aktif
/// olduğunu bilmez — bu sayede achievement mantığı Steam istemcisi
/// olmadan da test edilebiliyor.
/// </summary>
public interface IStatsBackend
{
    /// <summary>Hedef kullanılabilir mi (Steam çalışıyor mu).</summary>
    bool IsAvailable { get; }

    /// <summary>Arayüzde gösterilecek durum metni.</summary>
    string Status { get; }

    /// <summary>Bir istatistiğin güncel değerini yazar.</summary>
    void SetStat(string key, int value);

    /// <summary>Bir achievement'i açar. Zaten açıksa çağrı zararsızdır.</summary>
    void Unlock(string achievementId);

    /// <summary>
    /// Biriken değişiklikleri hedefe gönderir.
    ///
    /// Steamworks'te <c>StoreStats</c> bir ağ çağrısıdır; her istatistik
    /// değişiminde çağrılmaz, kare sonunda toplu gönderilir.
    /// </summary>
    void Flush();
}

/// <summary>
/// Geliştirme derlemesinin hedefi: hiçbir yere göndermez, bellekte tutar
/// ve konsola yazar.
///
/// Bu bir "sahte Steam" değil — Steam'in söylediğini taklit etmiyor.
/// Yalnızca achievement TETİKLEME MANTIĞININ Steam istemcisi olmadan
/// çalıştırılıp doğrulanabilmesi için var. Steam derlemesinde bu sınıfın
/// yerini <see cref="SteamStatsBackend"/> alır.
/// </summary>
public sealed class LocalStatsBackend : IStatsBackend
{
    private readonly Dictionary<string, int> _stats = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

    public bool IsAvailable => false;
    public string Status => "Basarimlar yerel (Steam yok)";

    /// <summary>Açılmış achievement kimlikleri — arayüz ve doğrulama için.</summary>
    public IReadOnlyCollection<string> Unlocked => _unlocked;

    public void SetStat(string key, int value) => _stats[key] = value;

    public void Unlock(string achievementId)
    {
        if (_unlocked.Add(achievementId))
        {
            Console.WriteLine($"[basarim] ACILDI: {achievementId}");
        }
    }

    public void Flush()
    {
        // Yerel hedefte gonderilecek bir yer yok.
    }
}
