using PixelSurvival.Inventory;

namespace PixelSurvival.Trade;

/// <summary>Takas teklifindeki tek bir yığın.</summary>
public readonly record struct TradeStack(string ItemId, int Amount);

/// <summary>Takasın hangi aşamada olduğu.</summary>
public enum TradeState
{
    /// <summary>Kimse teklif etmedi / oturum yok.</summary>
    Idle,

    /// <summary>İki taraf da item ekliyor, kimse onaylamadı.</summary>
    Negotiating,

    /// <summary>Bir taraf onayladı, diğeri bekleniyor.</summary>
    OneAccepted,

    /// <summary>İki taraf da onayladı; host takası uyguluyor.</summary>
    BothAccepted,

    Completed,
    Cancelled
}

/// <summary>Takasın neden reddedildiği.</summary>
public enum TradeOutcome
{
    Success,
    NotNegotiating,
    ItemNotOwned,
    InventoryFull,
    NotAuthoritative,
    SelfTrade,
    AlreadyTrading
}

/// <summary>
/// AŞAMA 2 / MADDE 22 — iki oyuncu arasında takas.
///
/// ════════════════════════════════════════════════════════════════════════
/// HOST OTORİTER — VE BU SEFER GERÇEKTEN
/// ════════════════════════════════════════════════════════════════════════
/// README'nin madde 10 notu şunu söylüyordu: "İstemci envanteri host'ta
/// ayna tutulmuyor… Madde 22 (oyuncular arası trade) bu açık açıkken
/// YAPILAMAZ." Bu doğruydu ve bu sınıf o açığı kapatmadan anlamsız olurdu:
/// istemcinin "bende 99 altın var" demesine güvenen bir takas, ekonomiyi
/// ilk gün bitirir.
///
/// Bu yüzden takas HOST tarafında, host'un tuttuğu envanter AYNALARI
/// üzerinden çözülür (<see cref="NetworkSession"/> içindeki mirror).
/// İstemci yalnızca NİYET bildirir ("şunu teklif ediyorum", "onaylıyorum");
/// sahiplik iddiası ETMEZ ve etse de dinlenmez.
///
/// ── Atomiklik ───────────────────────────────────────────────────────────
/// Takas ya tamamen olur ya hiç olmaz. Önce İKİ tarafın da verdiklerine
/// sahip olduğu VE alacaklarına yer olduğu doğrulanır; ancak ondan sonra
/// envanterlere dokunulur. Aksi halde "A'nın item'ı alındı, B'nin
/// envanteri dolu olduğu için verilemedi" durumu item yok ederdi.
///
/// ── Onay sıfırlanır ─────────────────────────────────────────────────────
/// Teklif her değiştiğinde İKİ tarafın onayı da düşer. Bu, takas
/// arayüzlerinin en bilinen dolandırıcılığına karşı: karşı taraf onayladıktan
/// sonra teklifi sessizce değiştirip "kabul" bekleyen oyuncu.
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class TradeSession
{
    private readonly List<TradeStack> _offerA = [];
    private readonly List<TradeStack> _offerB = [];

    public byte PlayerA { get; private set; }
    public byte PlayerB { get; private set; }

    public bool AcceptedA { get; private set; }
    public bool AcceptedB { get; private set; }

    public TradeState State { get; private set; } = TradeState.Idle;

    public IReadOnlyList<TradeStack> OfferA => _offerA;
    public IReadOnlyList<TradeStack> OfferB => _offerB;

    /// <summary>
    /// Takas hala açık mı.
    ///
    /// <see cref="TradeState.BothAccepted"/> de AÇIK sayılır: iki taraf
    /// onaylamış olsa bile takas henüz uygulanmadı ve taraflar teklifi
    /// değiştirebilmeli. Bu durum dışarıda bırakıldığında iki gerçek hata
    /// oluşuyordu (ikisini de Diagnostics/SelfTest yakaladı):
    ///
    ///   1. Onaydan sonra teklif değiştirildiğinde <see cref="Offer"/>
    ///      erken dönüyor ve onayları SIFIRLAMIYORDU — yani takas
    ///      arayüzlerinin en bilinen dolandırıcılığı açık kalıyordu.
    ///   2. Başarısız bir <see cref="TryExecute"/> sonrası
    ///      <see cref="Retract"/> çağrıldığında durum BothAccepted'ta
    ///      takılı kalıyor, onay bayrakları ise düşmüş oluyordu. Bu
    ///      tutarsız durumda takas kimsenin onaylamadığı halde yeniden
    ///      uygulanabiliyordu.
    /// </summary>
    public bool IsActive =>
        State is TradeState.Negotiating or TradeState.OneAccepted or TradeState.BothAccepted;

    /// <summary>Takasa katılan biri mi.</summary>
    public bool Involves(byte playerId) => IsActive && (playerId == PlayerA || playerId == PlayerB);

    /// <summary>Yeni takas başlatır.</summary>
    public TradeOutcome Begin(byte a, byte b)
    {
        if (a == b) return TradeOutcome.SelfTrade;
        if (IsActive) return TradeOutcome.AlreadyTrading;

        PlayerA = a;
        PlayerB = b;

        _offerA.Clear();
        _offerB.Clear();
        AcceptedA = AcceptedB = false;
        State = TradeState.Negotiating;

        return TradeOutcome.Success;
    }

    /// <summary>
    /// Teklife item ekler/çıkarır. <paramref name="amount"/> negatifse azaltır.
    ///
    /// SAHİPLİK BURADA DOĞRULANMAZ — yalnızca <see cref="TryExecute"/>
    /// anında, host'un aynası üzerinden doğrulanır. Sebep: pazarlık
    /// sırasında oyuncu item'ını harcayabilir; tek gerçek kontrol noktası
    /// takasın uygulandığı andır.
    /// </summary>
    public TradeOutcome Offer(byte playerId, string itemId, int amount)
    {
        if (!IsActive) return TradeOutcome.NotNegotiating;
        if (playerId != PlayerA && playerId != PlayerB) return TradeOutcome.NotNegotiating;

        var list = playerId == PlayerA ? _offerA : _offerB;
        var index = list.FindIndex(s => s.ItemId == itemId);

        if (index < 0)
        {
            if (amount > 0) list.Add(new TradeStack(itemId, amount));
        }
        else
        {
            var total = list[index].Amount + amount;

            if (total <= 0) list.RemoveAt(index);
            else list[index] = new TradeStack(itemId, total);
        }

        // Teklif DEGISTI -> iki onay da duser. Karsi taraf onayladiktan
        // sonra teklifi degistirip "kabul" beklemek, takas arayuzlerinin
        // en bilinen dolandiriciligi.
        ResetAcceptance();
        return TradeOutcome.Success;
    }

    /// <summary>Bir tarafın onayı.</summary>
    public TradeOutcome Accept(byte playerId)
    {
        if (!IsActive) return TradeOutcome.NotNegotiating;

        if (playerId == PlayerA) AcceptedA = true;
        else if (playerId == PlayerB) AcceptedB = true;
        else return TradeOutcome.NotNegotiating;

        RecomputeState();
        return TradeOutcome.Success;
    }

    /// <summary>Onayı geri çeker.</summary>
    public void Retract(byte playerId)
    {
        if (!IsActive) return;

        if (playerId == PlayerA) AcceptedA = false;
        else if (playerId == PlayerB) AcceptedB = false;

        RecomputeState();
    }

    /// <summary>
    /// Durumu onay bayraklarından TÜRETİR.
    ///
    /// Durum ile bayrakların ayrı ayrı güncellendiği önceki sürümde ikisi
    /// birbirinden ayrışabiliyordu (bayraklar düşmüş, durum hâlâ
    /// BothAccepted). Tek türetme noktası bu ayrışmayı imkânsız kılıyor.
    /// </summary>
    private void RecomputeState() =>
        State = (AcceptedA, AcceptedB) switch
        {
            (true, true) => TradeState.BothAccepted,
            (false, false) => TradeState.Negotiating,
            _ => TradeState.OneAccepted
        };

    public void Cancel()
    {
        State = TradeState.Cancelled;
        _offerA.Clear();
        _offerB.Clear();
        AcceptedA = AcceptedB = false;
    }

    private void ResetAcceptance()
    {
        AcceptedA = AcceptedB = false;
        RecomputeState();
    }

    /// <summary>
    /// Takası uygular. YALNIZCA HOST çağırmalı.
    ///
    /// İki envanter de host'un AYNASIDIR; istemcinin gönderdiği bir liste
    /// değil. Bu yüzden "bende var" iddiası burada hiç geçmiyor.
    /// </summary>
    /// <param name="inventoryA">Host'un A oyuncusu için tuttuğu envanter.</param>
    /// <param name="inventoryB">Host'un B oyuncusu için tuttuğu envanter.</param>
    public TradeOutcome TryExecute(WorldInventory inventoryA, WorldInventory inventoryB)
    {
        if (State != TradeState.BothAccepted) return TradeOutcome.NotNegotiating;

        // --- 1. Sahiplik: iki taraf da verdiklerine SAHIP mi ---
        // Envanterlere DOKUNMADAN once kontrol; yoksa yarim uygulanmis bir
        // takas item yok eder.
        foreach (var stack in _offerA)
        {
            if (inventoryA.CountOf(stack.ItemId) < stack.Amount) return TradeOutcome.ItemNotOwned;
        }

        foreach (var stack in _offerB)
        {
            if (inventoryB.CountOf(stack.ItemId) < stack.Amount) return TradeOutcome.ItemNotOwned;
        }

        // --- 2. Yer: alacaklari sigar mi ---
        // Kopya envanter uzerinde deneme yapiliyor: gercek envanteri
        // degistirip geri almak, yigin bolunmesi yuzunden her zaman
        // birebir geri alinamaz.
        if (!FitsAfterTrade(inventoryA, _offerA, _offerB) ||
            !FitsAfterTrade(inventoryB, _offerB, _offerA))
        {
            return TradeOutcome.InventoryFull;
        }

        // --- 3. Uygula ---
        // Buraya gelindiginde iki kontrol de gecti; islem artik basarisiz
        // olamaz.
        foreach (var stack in _offerA) inventoryA.TryRemove(stack.ItemId, stack.Amount);
        foreach (var stack in _offerB) inventoryB.TryRemove(stack.ItemId, stack.Amount);

        foreach (var stack in _offerB) inventoryA.TryAdd(stack.ItemId, stack.Amount);
        foreach (var stack in _offerA) inventoryB.TryAdd(stack.ItemId, stack.Amount);

        State = TradeState.Completed;
        return TradeOutcome.Success;
    }

    /// <summary>
    /// Verilenler çıkıp alınanlar girdikten sonra envantere sığar mı.
    ///
    /// Gerçek envanterin bir KOPYASI üzerinde denenir: sığmazsa hiçbir şey
    /// değişmemiş olur.
    /// </summary>
    private static bool FitsAfterTrade(WorldInventory inventory,
                                       IReadOnlyList<TradeStack> given,
                                       IReadOnlyList<TradeStack> received)
    {
        var copy = inventory.Clone();

        foreach (var stack in given)
        {
            if (!copy.TryRemove(stack.ItemId, stack.Amount)) return false;
        }

        foreach (var stack in received)
        {
            if (copy.TryAdd(stack.ItemId, stack.Amount) > 0) return false;
        }

        return true;
    }

    public static string Describe(TradeOutcome outcome) => outcome switch
    {
        TradeOutcome.Success => "Tamam",
        TradeOutcome.NotNegotiating => "Acik bir takas yok",
        TradeOutcome.ItemNotOwned => "Teklif edilen item sende yok",
        TradeOutcome.InventoryFull => "Envanterde yer yok",
        TradeOutcome.NotAuthoritative => "Takasi yalnizca host uygular",
        TradeOutcome.SelfTrade => "Kendinle takas yapamazsin",
        TradeOutcome.AlreadyTrading => "Zaten bir takas acik",
        _ => "?"
    };
}
