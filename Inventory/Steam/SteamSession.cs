namespace PixelSurvival.Inventory.Steam;

/// <summary>Arayüzde gösterilecek, sahip olunan tek bir kozmetik.</summary>
public readonly record struct OwnedCosmetic(string Name, string Kind, string Rarity);

/// <summary>
/// AŞAMA 2 / MADDE 19 — Steam tarafının TEK toplanma noktası.
///
/// ════════════════════════════════════════════════════════════════════════
/// NEDEN BU SINIF VAR
/// ════════════════════════════════════════════════════════════════════════
/// Oyun kabuğu (Game1) hem dünya envanterine hem Steam envanterine
/// dokunuyordu. İkisi aynı dosyada buluştuğu anda aralarında "kısa yoldan"
/// bir köprü kurmak bir satırlık iş haline geliyor — ve o köprü tam olarak
/// madde 19'un yasakladığı şey.
///
/// Bu sınıf sınırı gerçek kılıyor: Game1 artık <see cref="SteamInventory"/>
/// tipini HİÇ görmüyor, yalnızca burayı görüyor. Ve bu sınıf dışarıya
/// SALT GÖSTERİM bilgisi veriyor — sahiplik listesi, durum metni. Dünya
/// verisi alan tek bir metodu bile yok.
///
/// Denetim <c>Tools/verify_content.py</c> içinde: hiçbir dosyada
/// WorldInventory ile SteamInventory birlikte geçemez.
/// ════════════════════════════════════════════════════════════════════════
///
/// KAPSAM DIŞI: kozmetiklerin karaktere UYGULANMASI (madde 20 — layered
/// sprite sistemi), achievement/leaderboard (madde 21).
/// </summary>
public sealed class SteamSession
{
    private readonly SteamItemCatalog _catalog;
    private readonly SteamInventory _inventory;
    private readonly ISteamItemGrantAuthority _grantAuthority;

    public SteamSession(SteamItemCatalog catalog, ISteamItemGrantAuthority grantAuthority)
    {
        _catalog = catalog;
        _inventory = new SteamInventory(catalog);
        _grantAuthority = grantAuthority;
    }

    /// <summary>HUD'da gösterilen durum metni.</summary>
    public string Status => _inventory.Status;

    public bool IsAvailable => _inventory.IsAvailable;

    /// <summary>Steam'den sahiplik listesini tazeler.</summary>
    public void Refresh() => _inventory.Refresh();

    /// <summary>
    /// Sahip olunan kozmetikler — yalnızca gösterim için.
    ///
    /// Bu liste Steam'den gelir. Oyun onu ÜRETMEZ; buradaki bir kaydın
    /// varlığı sahipliğin kanıtı değil, Steam'in söylediğinin aynasıdır.
    /// </summary>
    public IReadOnlyList<OwnedCosmetic> OwnedCosmetics()
    {
        var result = new List<OwnedCosmetic>();

        foreach (var instance in _inventory.Items)
        {
            if (_catalog.TryGet(instance.DefId, out var definition))
            {
                result.Add(new OwnedCosmetic(definition.Name, definition.Kind, definition.Rarity));
            }
        }

        return result;
    }

    /// <summary>
    /// Bir oyun içi başarımın karşılığında Steam item'ı talep eder.
    ///
    /// DİKKAT: burada bir <c>WorldInventory</c> parametresi YOKTUR ve
    /// olmayacak. Talebin geçerliliğine oyun değil, güvenilir backend
    /// karar verir — client'ın "bunu hak ettim" iddiasına güvenilmez.
    ///
    /// Backend kurulana kadar bu çağrı <see cref="GrantOutcome.NotConfigured"/>
    /// döner ve hiçbir şey verilmez. "Şimdilik client'tan verelim" yolu
    /// bilerek kapalı: o geçici çözüm kalıcı olur.
    /// </summary>
    public GrantOutcome RequestReward(SteamItemDefId defId, string reason) =>
        _grantAuthority.RequestGrant(defId, reason);

    /// <summary>
    /// Steam'in doğrulayabildiği bir takas: girdiler DE Steam item'ıdır.
    /// Oyun içi malzeme girdi olarak verilemez — imza buna izin vermiyor.
    /// </summary>
    public bool TryExchange(int outputDefId, IReadOnlyList<ulong> inputInstanceIds)
    {
        var recipe = _catalog.Exchanges.FirstOrDefault(e => e.OutputDefId == outputDefId);
        return recipe is not null && _inventory.TryExchange(recipe, inputInstanceIds);
    }
}
