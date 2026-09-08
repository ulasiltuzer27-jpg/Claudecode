namespace PixelSurvival.Workshop;

/// <summary>Bir Workshop yükleme/güncelleme isteğinin sonucu.</summary>
public enum WorkshopOutcome
{
    Success,

    /// <summary>Steam çalışmıyor veya bu bir Steam derlemesi değil.</summary>
    Unavailable,

    /// <summary>Manifest veya içerik geçersiz.</summary>
    InvalidContent,

    /// <summary>Steam isteği reddetti (kota, izin, ağ).</summary>
    Rejected,

    /// <summary>İşlem başlatıldı; sonuç asenkron gelecek.</summary>
    Pending
}

/// <summary>
/// AŞAMA 2 / MADDE 25 — Workshop erişiminin soyutlaması.
///
/// <see cref="ModRegistry"/> hangi kaynaktan geldiğini bilmez; yalnızca
/// klasör listesi alır. Bu arayüz o klasörlerin NEREDEN geldiğini
/// belirler:
///   • Steam derlemesi → abone olunan Workshop öğelerinin kurulum yolları
///   • Geliştirme derlemesi → yerel <c>mods/</c> klasörü
///
/// Aynı ayrım <see cref="Networking.TransportFactory"/> ve
/// <see cref="Achievements.StatsBackendFactory"/> ile tutarlı: somut
/// Steamworks tipleri tek bir dosyada kalıyor.
/// </summary>
public interface IWorkshopBackend
{
    bool IsAvailable { get; }
    string Status { get; }

    /// <summary>
    /// Taranacak mod kökleri: (klasör, Workshop'tan mı geldiği).
    /// </summary>
    IEnumerable<(string Path, bool FromWorkshop)> ContentRoots();

    /// <summary>
    /// Bir klasörü Workshop'a yükler/günceller.
    ///
    /// Yükleme Steam tarafında ASENKRONDUR; bu çağrı isteği başlatır ve
    /// <see cref="WorkshopOutcome.Pending"/> döner. Senkron bir sonuç
    /// vaat etmek, arayüzü kilitlemek veya yalan söylemek olurdu.
    /// </summary>
    WorkshopOutcome Publish(LoadedMod mod, out string message);
}

/// <summary>
/// Geliştirme derlemesinin Workshop'u: yalnızca yerel <c>mods/</c> klasörü.
///
/// Steam'i taklit ETMEZ — abonelik listesi uydurmaz. Amacı mod yükleme
/// yolunun ve içerik doğrulamasının Steam istemcisi olmadan
/// çalıştırılabilmesi.
/// </summary>
public sealed class LocalWorkshopBackend : IWorkshopBackend
{
    public bool IsAvailable => true;

    public string Status => Localization.Loc.T("workshop.status.local");

    public IEnumerable<(string Path, bool FromWorkshop)> ContentRoots() =>
        [(ModRegistry.LocalModsDirectory, false)];

    public WorkshopOutcome Publish(LoadedMod mod, out string message)
    {
        _ = mod;

        // Yerel derlemede yayinlama yolu KAPALI. "Simdilik bir yere
        // kaydedelim" gibi bir sahte basari, mod yazarina yayinladigini
        // dusundururdu.
        message = Localization.Loc.T("workshop.publishNeedsSteam");
        return WorkshopOutcome.Unavailable;
    }
}

/// <summary>
/// Steam Workshop hedefi — Steamworks.NET'e (ISteamUGC) dokunan TEK dosya.
///
/// ── Abonelik akışı ──────────────────────────────────────────────────────
/// Steam abone olunan öğeleri kendisi indirir ve diske açar. Oyunun işi
/// yalnızca <c>GetSubscribedItems</c> ile kimlikleri almak ve
/// <c>GetItemInstallInfo</c> ile kurulum klasörünü sormaktır. İndirme
/// tamamlanmamış öğeler ATLANIR — yarım klasörü taramak bozuk manifest
/// uyarıları üretirdi.
///
/// ── Yayınlama akışı ─────────────────────────────────────────────────────
/// <c>CreateItem</c> → (callback) → <c>StartItemUpdate</c> →
/// <c>SetItemTitle/SetItemContent</c> → <c>SubmitItemUpdate</c>.
/// Hepsi asenkron; bu sınıf çağrı noktalarını kuruyor.
/// </summary>
public sealed class SteamWorkshopBackend : IWorkshopBackend
{
#if STEAM_BUILD
    public bool IsAvailable => Steamworks.SteamAPI.IsSteamRunning();

    public string Status => Localization.Loc.T(
        IsAvailable ? "workshop.status.steam" : "steam.notRunning");

    public IEnumerable<(string Path, bool FromWorkshop)> ContentRoots()
    {
        // Yerel klasor Steam derlemesinde de taranir: mod yazari
        // yayinlamadan once kendi modunu denemeli.
        yield return (ModRegistry.LocalModsDirectory, false);

        if (!IsAvailable) yield break;

        var count = Steamworks.SteamUGC.GetNumSubscribedItems();
        if (count == 0) yield break;

        var ids = new Steamworks.PublishedFileId_t[count];
        Steamworks.SteamUGC.GetSubscribedItems(ids, count);

        foreach (var id in ids)
        {
            var state = (Steamworks.EItemState)Steamworks.SteamUGC.GetItemState(id);

            // Yalnizca KURULU ve guncel olanlar. Indirme surerken klasor
            // yarim olabilir; taramak bozuk manifest uyarilari uretirdi.
            if (!state.HasFlag(Steamworks.EItemState.k_EItemStateInstalled)) continue;
            if (state.HasFlag(Steamworks.EItemState.k_EItemStateNeedsUpdate)) continue;

            if (Steamworks.SteamUGC.GetItemInstallInfo(id, out _, out var folder, 1024, out _))
            {
                // Steam her ogeyi KENDI klasorune acar; ModRegistry ise
                // "icinde mod klasorleri olan bir kok" bekliyor. Bu yuzden
                // ust klasor degil, ogenin kendisi bir mod klasoru olarak
                // veriliyor - kokun kendisi olarak degil.
                var parent = Path.GetDirectoryName(folder);
                if (!string.IsNullOrEmpty(parent)) yield return (parent, true);
            }
        }
    }

    public WorkshopOutcome Publish(LoadedMod mod, out string message)
    {
        if (!IsAvailable)
        {
            message = Localization.Loc.T("steam.notRunning");
            return WorkshopOutcome.Unavailable;
        }

        var appId = Steamworks.SteamUtils.GetAppID();

        if (mod.Manifest.IsPublished)
        {
            var handle = Steamworks.SteamUGC.StartItemUpdate(
                appId, new Steamworks.PublishedFileId_t(mod.Manifest.PublishedFileId));

            Steamworks.SteamUGC.SetItemTitle(handle, mod.Manifest.Name);
            Steamworks.SteamUGC.SetItemDescription(handle, mod.Manifest.Description);
            Steamworks.SteamUGC.SetItemContent(handle, Path.GetFullPath(mod.RootDirectory));

            Steamworks.SteamUGC.SubmitItemUpdate(handle, $"v{mod.Manifest.Version}");

            message = Localization.Loc.T("workshop.updateStarted");
            return WorkshopOutcome.Pending;
        }

        // Yeni oge: CreateItem asenkron; donen kimlik callback ile gelir ve
        // mod.json'a yazilmalidir. Bu yuzden burada yalnizca istek
        // baslatiliyor.
        Steamworks.SteamUGC.CreateItem(
            appId, Steamworks.EWorkshopFileType.k_EWorkshopFileTypeCommunity);

        message = Localization.Loc.T("workshop.createStarted");
        return WorkshopOutcome.Pending;
    }
#else
    // Steam'siz derleme: sinif var, Workshop yolu kapali.
    public bool IsAvailable => false;
    public string Status => Localization.Loc.T("steam.notSteamBuild");

    public IEnumerable<(string Path, bool FromWorkshop)> ContentRoots() =>
        [(ModRegistry.LocalModsDirectory, false)];

    public WorkshopOutcome Publish(LoadedMod mod, out string message)
    {
        _ = mod;
        message = Localization.Loc.T("workshop.publishNeedsSteam");
        return WorkshopOutcome.Unavailable;
    }
#endif
}

/// <summary>
/// Workshop hedefi seçiminin TEK noktası — TransportFactory ve
/// StatsBackendFactory ile aynı disiplin.
/// </summary>
public static class WorkshopFactory
{
    public static IWorkshopBackend Create() =>
#if STEAM_BUILD
        new SteamWorkshopBackend();
#else
        new LocalWorkshopBackend();
#endif
}
