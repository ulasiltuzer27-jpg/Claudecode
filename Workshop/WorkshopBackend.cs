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
/// Steam Workshop hedefi — Facepunch'in Ugc API'sine dokunan TEK dosya.
///
/// ── Abonelik akışı ──────────────────────────────────────────────────────
/// Steam abone olunan öğeleri kendisi indirir ve diske açar. Oyunun işi
/// yalnızca <c>GetSubscribedItems</c> ile kimlikleri almak ve
/// <c>GetItemInstallInfo</c> ile kurulum klasörünü sormaktır. İndirme
/// tamamlanmamış öğeler ATLANIR — yarım klasörü taramak bozuk manifest
/// uyarıları üretirdi.
///
/// ── Yayınlama akışı ─────────────────────────────────────────────────────
/// <c>Ugc.Editor</c> bütün akışı tek zincirde veriyor:
/// <c>NewCommunityFile.WithTitle(...).WithContent(...).SubmitAsync()</c>.
/// </summary>
public sealed class SteamWorkshopBackend : IWorkshopBackend
{
#if STEAM_BUILD
    /// <summary>
    /// Abonelik listesinin beklenebileceği en uzun süre.
    ///
    /// Süre dolarsa oyun Workshop modları OLMADAN açılır — Steam'in
    /// yanıt vermemesi yüzünden oyunun hiç açılmaması kabul edilemez.
    /// </summary>
    private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(5);

    public bool IsAvailable => Steamworks.SteamClient.IsValid;

    public string Status => Localization.Loc.T(
        IsAvailable ? "workshop.status.steam" : "steam.notRunning");

    /// <summary>
    /// Taranacak mod kökleri.
    ///
    /// ── Neden burada BEKLİYORUZ ─────────────────────────────────────────
    /// Facepunch'ta abonelik listesi asenkron geliyor. Ama mod kümesi
    /// içerik tabloları yüklenmeden ÖNCE bilinmek zorunda: modlar item,
    /// tarif ve tile tablolarının üstüne biniyor (bkz. ModdedContent) ve
    /// parmak izi de mod kümesinden türüyor. Liste sonradan gelseydi
    /// Workshop modları o açılışta etkisiz kalır, üstelik parmak izi
    /// değişip ağ bağlantılarını da reddettirirdi.
    ///
    /// Bu yüzden sonuç SINIRLI bir süre bekleniyor. Beklerken geri
    /// çağrıları BİZ pompalıyoruz: <c>SteamClient.Init</c> asenkron
    /// pompalama KAPALI kurulduğu için (bkz. Game1) bu noktada başka
    /// kimse pompalamıyor ve düz bir <c>Wait()</c> kilitlenirdi.
    /// </summary>
    public IEnumerable<(string Path, bool FromWorkshop)> ContentRoots()
    {
        // Yerel klasor Steam derlemesinde de taranir: mod yazari
        // yayinlamadan once kendi modunu denemeli.
        yield return (ModRegistry.LocalModsDirectory, false);

        if (!IsAvailable) yield break;

        foreach (var root in ResolveSubscribedRoots()) yield return (root, true);
    }

    private static List<string> ResolveSubscribedRoots()
    {
        var roots = new List<string>();
        var task = FetchSubscribedRootsAsync();

        var deadline = DateTime.UtcNow + SubscriptionTimeout;

        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            // Pompalamayi biz yapiyoruz: oyun dongusu henuz baslamadi.
            Steamworks.SteamClient.RunCallbacks();
            Thread.Sleep(16);
        }

        if (!task.IsCompleted)
        {
            Console.WriteLine("[workshop] abonelik listesi zaman asimina ugradi; " +
                              "Workshop modlari bu acilista atlandi.");
            return roots;
        }

        if (task.IsFaulted)
        {
            Console.WriteLine($"[workshop] abonelik listesi alinamadi: " +
                              $"{task.Exception?.GetBaseException().Message}");
            return roots;
        }

        return task.Result;
    }

    private static async Task<List<string>> FetchSubscribedRootsAsync()
    {
        var roots = new List<string>();

        var query = Steamworks.Ugc.Query.Items
            .WhereUserSubscribed(Steamworks.SteamClient.SteamId);

        var page = await query.GetPageAsync(1);
        if (page is not { } result) return roots;

        foreach (var item in result.Entries)
        {
            // Yalnizca KURULU ve guncel olanlar. Indirme surerken klasor
            // yarim olabilir; taramak bozuk manifest uyarilari uretirdi.
            if (!item.IsInstalled || item.NeedsUpdate) continue;
            if (string.IsNullOrEmpty(item.Directory)) continue;

            // Steam her ogeyi KENDI klasorune aciyor; ModRegistry ise
            // "icinde mod klasorleri olan bir kok" bekliyor. Bu yuzden
            // ogenin kendisi degil UST klasoru veriliyor.
            var parent = Path.GetDirectoryName(item.Directory);
            if (!string.IsNullOrEmpty(parent)) roots.Add(parent);
        }

        return roots;
    }

    /// <summary>
    /// Modu Workshop'a yayınlar ya da günceller.
    ///
    /// Facepunch'ın <c>Ugc.Editor</c>'ü bütün akışı tek bir zincirde
    /// veriyor: Steamworks.NET'te <c>CreateItem</c> → callback →
    /// <c>StartItemUpdate</c> → <c>SetItemTitle/Content</c> →
    /// <c>SubmitItemUpdate</c> olarak elle örülen dört adım burada tek
    /// <c>await</c>.
    ///
    /// Sonuç <see cref="WorkshopOutcome.Pending"/>: gönderim arka planda
    /// sürüyor ve yeni öğenin kimliği <c>mod.json</c>'a yazılmak üzere
    /// konsola raporlanıyor.
    /// </summary>
    public WorkshopOutcome Publish(LoadedMod mod, out string message)
    {
        if (!IsAvailable)
        {
            message = Localization.Loc.T("steam.notRunning");
            return WorkshopOutcome.Unavailable;
        }

        _ = PublishAsync(mod);

        message = Localization.Loc.T(
            mod.Manifest.IsPublished ? "workshop.updateStarted" : "workshop.createStarted");

        return WorkshopOutcome.Pending;
    }

    private static async Task PublishAsync(LoadedMod mod)
    {
        try
        {
            var editor = mod.Manifest.IsPublished
                ? new Steamworks.Ugc.Editor(mod.Manifest.PublishedFileId)
                : Steamworks.Ugc.Editor.NewCommunityFile;

            var result = await editor
                .WithTitle(mod.Manifest.Name)
                .WithDescription(mod.Manifest.Description)
                .WithContent(Path.GetFullPath(mod.RootDirectory))
                .WithChangeLog($"v{mod.Manifest.Version}")
                .SubmitAsync();

            if (!result.Success)
            {
                Console.WriteLine($"[workshop] '{mod.Manifest.Id}' yayinlanamadi: {result.Result}");
                return;
            }

            // Yeni ogenin kimligi mod.json'a yazilmali; oyun kendi
            // manifestini DEGISTIRMIYOR (mod yazarinin dosyasi) —
            // yalnizca raporluyor.
            Console.WriteLine($"[workshop] '{mod.Manifest.Id}' yayinlandi. " +
                              $"mod.json'a yazin: \"publishedFileId\": {result.FileId.Value}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[workshop] yayinlama hatasi: {ex.Message}");
        }
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
