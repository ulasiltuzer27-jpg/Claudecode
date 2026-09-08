using Microsoft.Xna.Framework;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
using PixelSurvival.Systems.Social;
using PixelSurvival.Trade;
using PixelSurvival.Systems.Animation;
using PixelSurvival.Systems.Climate;
using PixelSurvival.Systems.Combat;
using PixelSurvival.Systems.Gathering;
using PixelSurvival.Systems.Input;
using PixelSurvival.World;

namespace PixelSurvival.Networking;

public enum SessionMode
{
    /// <summary>Tek kişilik. Taşıma katmanı hiç başlatılmaz.</summary>
    Offline,

    /// <summary>Bu makine sunucu rolünde. Dünyayı açan oyuncu.</summary>
    Host,

    /// <summary>Başka bir oyuncunun makinesine bağlı.</summary>
    Client
}

/// <summary>
/// AŞAMA 1 / MADDE 10 — oturum yönetimi.
///
/// ════════════════════════════════════════════════════════════════════════
/// YETKİ DAĞILIMI — bu pasta ne otoriter, ne değil
/// ════════════════════════════════════════════════════════════════════════
/// HOST OTORİTER:
///   - Tüm oyuncuların konumu ve yönü (istemci tahmin eder, host düzeltir)
///   - Can, hasar, ölüm, yeniden doğma
///   - Dünya tile'ları (toplama sonucu değişimler)
///   - İstemcilerin toplama sonucu kazandığı item'lar (InventoryDelta ile verilir)
///
/// HENÜZ OTORİTER DEĞİL — kapatılacak açıklar:
///   - CRAFTING istemcide yerel çalışıyor, host doğrulamıyor.
///   - İNŞA yalnızca host'ta çalışıyor; istemci inşa edemiyor.
///   - İstemci envanteri host'ta ayna tutulmuyor, yalnızca delta gönderiliyor.
///
/// Bunlar PvP ve ekonomi anlam kazanmadan ÖNCE kapatılmalı. Madde 18
/// (PvP bölgesi) ve madde 22 (oyuncular arası trade) bu açıklar açıkken
/// yapılamaz. Buraya not düşülmesinin sebebi, sonradan "zaten çalışıyordu"
/// diye üstünden atlanmaması.
/// ════════════════════════════════════════════════════════════════════════
///
/// Tick tabanlı: saniyede <see cref="NetworkProtocol.TicksPerSecond"/> kez
/// simülasyon ilerler ve snapshot yayınlanır. Çizim bundan bağımsız olarak
/// ekranın hızında akar; aradaki farkı uzak oyuncularda interpolasyon
/// (<see cref="RemotePlayer"/>), yerel oyuncuda tahmin+düzeltme kapatır.
/// </summary>
public sealed class NetworkSession : IDisposable
{
    /// <summary>
    /// Yerel tahmin ile host'un söylediği konum bu kadar ayrışırsa düzeltilir.
    /// Daha küçük bir eşik her tick'te sarsıntı, daha büyüğü lastik bant hissi
    /// yaratır. 6 pixel ≈ yarım karakter genişliği.
    /// </summary>
    private const float ReconcileThreshold = 6f;

    private readonly SpriteSheet _playerSheet;
    private readonly ItemDatabase _items;
    private readonly ResourceTable _resources;

    private INetworkTransport? _transport;
    private float _tickAccumulator;
    private uint _tick;

    /// <summary>Dünya saati saniyede bir yayınlanır — her tick göndermenin faydası yok.</summary>
    private float _timeBroadcastAccumulator;

    // --- Host tarafı ---
    private readonly Dictionary<int, byte> _peerToPlayer = [];
    private readonly Dictionary<byte, int> _playerToPeer = [];
    private readonly Dictionary<byte, Player> _hostPlayers = [];
    private readonly Dictionary<byte, GatheringSystem> _hostGathering = [];
    private readonly Dictionary<byte, InputFrame> _pendingInput = [];

    /// <summary>
    /// MADDE 22 — host'un her istemci için tuttuğu envanter AYNASI.
    ///
    /// Bu, madde 10'da açık bırakılan ve README'de "madde 22 bu açık
    /// açıkken yapılamaz" diye işaretlenen boşluğun kapanışı. Takas,
    /// istemcinin "bende şu var" iddiasına DEĞİL, bu aynaya bakar.
    ///
    /// Ayna host tarafında ÜRETİLİR: item'lar buraya yalnızca host'un
    /// kendi çözdüğü olaylardan girer (toplama, doğrulanmış üretim,
    /// takas). İstemciden gelen bir mesaj aynayı doğrudan yazamaz.
    /// </summary>
    private readonly Dictionary<byte, WorldInventory> _hostInventories = [];

    private byte _nextPlayerId = 1;

    /// <summary>
    /// MADDE 22 — açık takas. Host tarafında YALNIZCA BİR takas olur.
    ///
    /// Bilinçli bir sadeleştirme: eşzamanlı çok takas, aynı item'ı iki
    /// takasa birden koymayı mümkün kılar ve doğrulamayı takas başına
    /// değil, oyuncu başına kilitlemeyi gerektirir. Tek takas bu sınıfın
    /// sorumluluğunu küçük tutuyor.
    /// </summary>
    private readonly TradeSession _trade = new();

    /// <summary>Takasa katılanların takas ANINDAKİ envanterleri host tarafında.</summary>
    public TradeSession Trade => _trade;

    // --- İstemci tarafı ---
    private readonly Dictionary<byte, RemotePlayer> _remotes = [];

    public SessionMode Mode { get; private set; } = SessionMode.Offline;

    /// <summary>Host her zaman 0'dır; istemciler 1'den başlar.</summary>
    public byte LocalPlayerId { get; private set; }

    public int WorldSeed { get; private set; }
    public IReadOnlyCollection<RemotePlayer> RemotePlayers => _remotes.Values;
    public int ConnectedCount => _transport?.PeerCount ?? 0;

    /// <summary>
    /// Host tarafinda bagli olan oyuncu kimlikleri (host'un kendisi HARIC).
    /// Takas ve klan daveti icin hedef secmekte kullanilir.
    /// </summary>
    public IReadOnlyCollection<byte> ConnectedPlayerIds => _hostPlayers.Keys;

    /// <summary>Host'tan dünya tohumu geldi — istemci dünyayı yeniden kurmalı.</summary>
    public event Action<int>? SeedReceived;

    /// <summary>Host bir tile değişikliği bildirdi.</summary>
    public event Action<Point, int>? TileChanged;

    /// <summary>Host bu istemciye item verdi (toplama sonucu).</summary>
    public event Action<string, int>? ItemGranted;

    /// <summary>Host dünya saatini ve havayı bildirdi (madde 12).</summary>
    public event Action<double, string>? WorldTimeReceived;

    /// <summary>Kullanıcıya gösterilecek durum mesajı.</summary>
    public event Action<string>? Notice;

    /// <summary>
    /// MADDE 22 — host'un yerel oyuncusunun (kendisinin) envanteri.
    ///
    /// Host kendi envanterini <see cref="_hostInventories"/> içinde
    /// tutmuyor; onu oyun kabuğu yönetiyor. Takas host'un kendi
    /// envanterine de dokunacağı için buradan erişiliyor.
    /// </summary>
    public WorldInventory? LocalInventory { get; set; }

    /// <summary>
    /// MADDE 25 — aktif mod kümesinin parmak izi.
    ///
    /// Karşılama mesajıyla gönderilir ve istemcide karşılaştırılır.
    /// Oyun kabuğu doldurur; oturum kodu <see cref="Workshop.ModRegistry"/>
    /// tipini görmez — hangi modların yüklü olduğu ağ katmanının işi değil.
    /// </summary>
    public string ModFingerprint { get; set; } = "modsuz";

    /// <summary>Takasın durumu değişti — arayüz yenilenmeli.</summary>
    public event Action? TradeChanged;

    /// <summary>
    /// MADDE 24 — bir oyuncu emote yaptı (kendi emote'un dahil).
    ///
    /// Host olayı ONAYLAYIP herkese yayınlar; oyun kabuğu yalnızca
    /// onaylanmış olayı görür. Yerel emote'u anında göstermek cazip
    /// olurdu ama o zaman spam koruması host'ta reddettiğinde oyuncu
    /// kendi ekranında olmayan bir emote görürdü.
    /// </summary>
    public event Action<byte, EmoteKind>? EmoteReceived;

    /// <summary>MADDE 24 — bir oyuncu ping koydu.</summary>
    public event Action<byte, PingKind, Vector2>? PingReceived;

    public NetworkSession(SpriteSheet playerSheet, ItemDatabase items, ResourceTable resources)
    {
        _playerSheet = playerSheet;
        _items = items;
        _resources = resources;
    }

    // ================= oturum kurulumu =================

    public void StartHost(int seed, int port = NetworkProtocol.DefaultPort)
    {
        Leave();

        // Somut taşıma adı BURADA GEÇMEZ: seçim TransportFactory'de,
        // derleme sembolüne göre. Oturum kodu hangisinin aktif olduğunu bilmez.
        _transport = TransportFactory.Create();
        _transport.StartHost(port);

        Mode = SessionMode.Host;
        LocalPlayerId = 0;
        WorldSeed = seed;
        _nextPlayerId = 1;

        Notice?.Invoke($"Sunucu acildi ({TransportFactory.Name}) — tohum {seed}");
    }

    public void Connect(string address, int port = NetworkProtocol.DefaultPort)
    {
        Leave();

        _transport = TransportFactory.Create();
        _transport.Connect(address, port);

        Mode = SessionMode.Client;
        Notice?.Invoke($"{address}:{port} adresine baglaniliyor...");
    }

    public void Leave()
    {
        _transport?.Dispose();
        _transport = null;

        Mode = SessionMode.Offline;
        LocalPlayerId = 0;
        _tick = 0;
        _tickAccumulator = 0f;

        _peerToPlayer.Clear();
        _playerToPeer.Clear();
        _hostPlayers.Clear();
        _hostGathering.Clear();
        _pendingInput.Clear();
        _remotes.Clear();
    }

    public void Dispose() => Leave();

    // ================= kare döngüsü =================

    /// <summary>
    /// Her karede çağrılır: olayları işler, tick zamanı geldiyse simülasyonu
    /// ilerletip snapshot yayınlar.
    /// </summary>
    public void Update(GameTime gameTime, Player localPlayer, PlayerInput localInput,
                       TileMap map, CombatSystem combat, Vector2 spawnPosition,
                       ClimateSystem climate)
    {
        if (Mode == SessionMode.Offline || _transport is null)
        {
            return;
        }

        ProcessEvents(map, localPlayer, spawnPosition);

        foreach (var remote in _remotes.Values)
        {
            remote.Update(gameTime);
        }

        _tickAccumulator += (float)gameTime.ElapsedGameTime.TotalSeconds;
        var tickLength = 1f / NetworkProtocol.TicksPerSecond;

        // while: bir kare uzun sürdüyse kaçan tick'ler telafi edilir,
        // simülasyon gerçek zamanın gerisinde kalmaz.
        while (_tickAccumulator >= tickLength)
        {
            _tickAccumulator -= tickLength;
            _tick++;

            if (Mode == SessionMode.Host)
            {
                HostTick(tickLength, localPlayer, map, combat, spawnPosition);

                _timeBroadcastAccumulator += tickLength;
                if (_timeBroadcastAccumulator >= 1f)
                {
                    _timeBroadcastAccumulator = 0f;
                    _transport.Broadcast(NetworkProtocol.WriteWorldTime(
                        climate.WorldSeconds, climate.Weather.Key));
                }
            }
            else
            {
                SendInput(localInput);
            }
        }
    }

    private void ProcessEvents(TileMap map, Player localPlayer, Vector2 spawnPosition)
    {
        foreach (var netEvent in _transport!.PollEvents())
        {
            switch (netEvent.Type)
            {
                case NetworkEventType.Connected when Mode == SessionMode.Host:
                    AcceptPlayer(netEvent.PeerId, spawnPosition);
                    break;

                case NetworkEventType.Connected:
                    Notice?.Invoke("Baglandi, host'un karsilamasi bekleniyor...");
                    break;

                case NetworkEventType.Disconnected when Mode == SessionMode.Host:
                    DropPlayer(netEvent.PeerId);
                    break;

                case NetworkEventType.Disconnected:
                    Notice?.Invoke("Host ile baglanti koptu.");
                    Leave();
                    return;

                case NetworkEventType.Data:
                    HandleData(netEvent, map, localPlayer);
                    break;
            }
        }
    }

    private void HandleData(NetworkEvent netEvent, TileMap map, Player localPlayer)
    {
        var data = netEvent.Payload.Span;

        switch (NetworkProtocol.PeekType(data))
        {
            case MessageType.ClientInput when Mode == SessionMode.Host:
                if (_peerToPlayer.TryGetValue(netEvent.PeerId, out var id) &&
                    NetworkProtocol.TryReadClientInput(data, out var frame))
                {
                    // Yalnızca EN SON girdi tutulur. Ağ birden fazla tick'lik
                    // girdiyi aynı anda getirebilir; hepsini uygulamak
                    // oyuncuyu ileri fırlatırdı.
                    _pendingInput[id] = frame;
                }

                break;

            case MessageType.Welcome when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadWelcome(data, out var assigned, out var seed,
                                                   out var hostMods))
                {
                    // MADDE 25: harita agdan GONDERILMIYOR; iki taraf ayni
                    // tohumdan uretiyor ve uretim modlanabilir veriden
                    // turuyor. Mod kumeleri farkliysa ayni tohum FARKLI
                    // dunya verir ve ekranlar sessizce ayrisir: oyuncu
                    // duvarin icinde yurur, kestigi agac digerinde durur.
                    // Baglantiyi reddetmek, sessizce ayrismis bir oyundan
                    // iyidir.
                    if (hostMods != ModFingerprint)
                    {
                        Notice?.Invoke(
                            $"Mod kumesi uyusmuyor (host: {hostMods}, sen: {ModFingerprint}). " +
                            "Ayni tohum farkli dunya uretirdi.");
                        Leave();
                        break;
                    }

                    LocalPlayerId = assigned;
                    WorldSeed = seed;
                    SeedReceived?.Invoke(seed);
                    Notice?.Invoke($"Baglanildi — oyuncu {assigned}, tohum {seed}");
                }
                else
                {
                    Notice?.Invoke("Protokol surumu uyusmuyor.");
                    Leave();
                }

                break;

            case MessageType.Snapshot when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadSnapshot(data, out _, out var states))
                {
                    ApplySnapshot(states, localPlayer);
                }

                break;

            case MessageType.TileChange when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadTileChange(data, out var tile, out var index))
                {
                    map.SetTile(tile.X, tile.Y, index);
                    TileChanged?.Invoke(tile, index);
                }

                break;

            case MessageType.PlayerLeft when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadPlayerLeft(data, out var gone))
                {
                    _remotes.Remove(gone);
                }

                break;

            case MessageType.WorldTime when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadWorldTime(data, out var seconds, out var weather))
                {
                    WorldTimeReceived?.Invoke(seconds, weather);
                }

                break;

            case MessageType.InventoryDelta when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadInventoryDelta(data, out var itemId, out var amount)
                    && _items.Contains(itemId))
                {
                    ItemGranted?.Invoke(itemId, amount);
                }

                break;

            // ---------- Madde 22: takas (host tarafi) ----------
            // Bu mesajlarin hicbiri istemcinin sahiplik iddiasini tasimaz;
            // host her seferinde KENDI aynasina bakar.

            case MessageType.TradeRequest when Mode == SessionMode.Host:
                if (_peerToPlayer.TryGetValue(netEvent.PeerId, out var requester) &&
                    NetworkProtocol.TryReadTradeRequest(data, out var target))
                {
                    HandleTradeRequest(requester, target);
                }

                break;

            case MessageType.TradeOffer when Mode == SessionMode.Host:
                if (_peerToPlayer.TryGetValue(netEvent.PeerId, out var offerer) &&
                    NetworkProtocol.TryReadTradeOffer(data, out var offerItem, out var offerAmount)
                    && _items.Contains(offerItem))
                {
                    _trade.Offer(offerer, offerItem, offerAmount);
                    BroadcastTradeState();
                }

                break;

            case MessageType.TradeAccept when Mode == SessionMode.Host:
                if (_peerToPlayer.TryGetValue(netEvent.PeerId, out var accepter) &&
                    NetworkProtocol.TryReadTradeAccept(data, out var accepted))
                {
                    if (accepted) _trade.Accept(accepter);
                    else _trade.Retract(accepter);

                    TryCompleteTrade();
                    BroadcastTradeState();
                }

                break;

            case MessageType.CraftRequest when Mode == SessionMode.Host:
                if (_peerToPlayer.TryGetValue(netEvent.PeerId, out var crafter) &&
                    NetworkProtocol.TryReadCraftRequest(data, out var recipeId))
                {
                    CraftRequested?.Invoke(crafter, recipeId);
                }

                break;

            // ---------- Madde 24: emote ve ping ----------
            case MessageType.Emote:
                if (NetworkProtocol.TryReadEmote(data, out var emotePlayer, out var emoteKind))
                {
                    if (Mode == SessionMode.Host)
                    {
                        // Host gonderenin KIMLIGINI kendisi koyar; istemcinin
                        // yazdigi kimlige guvenilmez, yoksa baskasi adina
                        // emote yapilabilirdi.
                        if (!_peerToPlayer.TryGetValue(netEvent.PeerId, out var sender)) break;

                        BroadcastEmote(sender, emoteKind);
                    }
                    else
                    {
                        EmoteReceived?.Invoke(emotePlayer, (EmoteKind)emoteKind);
                    }
                }

                break;

            case MessageType.Ping:
                if (NetworkProtocol.TryReadPing(data, out var pingPlayer, out var pingKind,
                                                out var pingPos))
                {
                    if (Mode == SessionMode.Host)
                    {
                        if (!_peerToPlayer.TryGetValue(netEvent.PeerId, out var sender)) break;

                        BroadcastPing(sender, pingKind, pingPos);
                    }
                    else
                    {
                        PingReceived?.Invoke(pingPlayer, (PingKind)pingKind, pingPos);
                    }
                }

                break;

            case MessageType.TradeState when Mode == SessionMode.Client:
                if (NetworkProtocol.TryReadTradeState(data, out var state, out var selfOk,
                                                      out var otherOk, out var partner))
                {
                    ClientTradeState = (TradeState)state;
                    ClientTradeAcceptedSelf = selfOk;
                    ClientTradeAcceptedOther = otherOk;
                    ClientTradePartner = partner;
                    TradeChanged?.Invoke();
                }

                break;
        }
    }

    // ================= Madde 24: emote ve ping =================

    /// <summary>
    /// Yerel oyuncunun emote'u. Host'ta anında yayınlanır, istemcide
    /// host'a istek olarak gider.
    /// </summary>
    public void SendEmote(EmoteKind kind)
    {
        if (Mode == SessionMode.Client)
        {
            // Kimlik alanina 0 yaziliyor; host onu KENDI bildigi kimlikle
            // degistiriyor.
            _transport?.Send(0, NetworkProtocol.WriteEmote(0, (byte)kind));
            return;
        }

        BroadcastEmote(LocalPlayerId, (byte)kind);
    }

    /// <summary>Yerel oyuncunun ping'i.</summary>
    public void SendPing(PingKind kind, Vector2 position)
    {
        if (Mode == SessionMode.Client)
        {
            _transport?.Send(0, NetworkProtocol.WritePing(0, (byte)kind, position.X, position.Y));
            return;
        }

        BroadcastPing(LocalPlayerId, (byte)kind, position);
    }

    private void BroadcastEmote(byte playerId, byte kind)
    {
        // Bilinmeyen tur sessizce dusurulur: eski bir istemci yeni bir
        // emote gonderirse oyun cokmemeli.
        if (!Enum.IsDefined((EmoteKind)kind)) return;

        EmoteReceived?.Invoke(playerId, (EmoteKind)kind);
        _transport?.Broadcast(NetworkProtocol.WriteEmote(playerId, kind));
    }

    private void BroadcastPing(byte playerId, byte kind, Vector2 position)
    {
        if (!Enum.IsDefined((PingKind)kind)) return;

        PingReceived?.Invoke(playerId, (PingKind)kind, position);
        _transport?.Broadcast(NetworkProtocol.WritePing(playerId, kind, position.X, position.Y));
    }

    // ================= Madde 22: takas =================

    /// <summary>
    /// İstemci bir üretim istedi. Oyun kabuğu tarifi doğrulayıp host'un
    /// aynası üzerinde uygular — <see cref="NetworkSession"/> tarif
    /// kitabını tanımıyor, bu yüzden karar dışarıya bırakılıyor.
    /// </summary>
    public event Action<byte, string>? CraftRequested;

    /// <summary>İstemcide görülen takas durumu (arayüz için).</summary>
    public TradeState ClientTradeState { get; private set; } = TradeState.Idle;
    public bool ClientTradeAcceptedSelf { get; private set; }
    public bool ClientTradeAcceptedOther { get; private set; }
    public byte ClientTradePartner { get; private set; }

    /// <summary>Host tarafında bir oyuncunun envanteri (host kendisi dahil).</summary>
    public WorldInventory? InventoryOf(byte playerId) =>
        playerId == LocalPlayerId ? LocalInventory : _hostInventories.GetValueOrDefault(playerId);

    /// <summary>Host: takası başlatır.</summary>
    public TradeOutcome BeginTrade(byte a, byte b)
    {
        if (Mode != SessionMode.Host) return TradeOutcome.NotAuthoritative;

        var outcome = _trade.Begin(a, b);
        if (outcome == TradeOutcome.Success) BroadcastTradeState();
        return outcome;
    }

    /// <summary>Host: kendi (yerel) teklifini değiştirir.</summary>
    public void OfferLocal(string itemId, int amount)
    {
        if (Mode == SessionMode.Host)
        {
            _trade.Offer(LocalPlayerId, itemId, amount);
            BroadcastTradeState();
        }
        else if (Mode == SessionMode.Client)
        {
            _transport?.Send(0, NetworkProtocol.WriteTradeOffer(itemId, amount));
        }
    }

    /// <summary>Host veya istemci: onay durumunu bildirir.</summary>
    public void SetLocalAccepted(bool accepted)
    {
        if (Mode == SessionMode.Host)
        {
            if (accepted) _trade.Accept(LocalPlayerId);
            else _trade.Retract(LocalPlayerId);

            TryCompleteTrade();
            BroadcastTradeState();
        }
        else if (Mode == SessionMode.Client)
        {
            _transport?.Send(0, NetworkProtocol.WriteTradeAccept(accepted));
        }
    }

    /// <summary>İstemci: host'tan takas açmasını ister.</summary>
    public void RequestTrade(byte targetPlayerId) =>
        _transport?.Send(0, NetworkProtocol.WriteTradeRequest(targetPlayerId));

    /// <summary>İstemci: üretim isteğini host'a yollar.</summary>
    public void RequestCraft(string recipeId) =>
        _transport?.Send(0, NetworkProtocol.WriteCraftRequest(recipeId));

    /// <summary>
    /// Host: istemciye envanter deltası gönderir ve AYNAYI da günceller.
    ///
    /// İkisi tek metotta: ayrı çağrılar olsaydı biri unutulduğunda ayna
    /// sessizce gerçekten ayrışırdı ve bu, takasın yanlış karar vermesi
    /// demek olurdu.
    /// </summary>
    public void GrantTo(byte playerId, string itemId, int amount)
    {
        if (Mode != SessionMode.Host || amount == 0) return;

        if (playerId == LocalPlayerId)
        {
            if (amount > 0) LocalInventory?.TryAdd(itemId, amount);
            else LocalInventory?.TryRemove(itemId, -amount);
            return;
        }

        if (_hostInventories.TryGetValue(playerId, out var mirror))
        {
            if (amount > 0) mirror.TryAdd(itemId, amount);
            else mirror.TryRemove(itemId, -amount);
        }

        if (_playerToPeer.TryGetValue(playerId, out var peer))
        {
            _transport?.Send(peer, NetworkProtocol.WriteInventoryDelta(itemId, amount));
        }
    }

    private void HandleTradeRequest(byte requester, byte target)
    {
        // Hedef gercekten oturumda mi? Olmayan bir oyuncuyla takas acmak,
        // istemcinin uydurdugu bir kimlikle envanter kilitlemesine yol acardi.
        var targetExists = target == LocalPlayerId || _hostPlayers.ContainsKey(target);

        if (!targetExists)
        {
            Notice?.Invoke($"Takas: oyuncu {target} oturumda yok");
            return;
        }

        var outcome = _trade.Begin(requester, target);

        Notice?.Invoke(outcome == TradeOutcome.Success
            ? $"Takas acildi: {requester} <-> {target}"
            : $"Takas acilamadi: {TradeSession.Describe(outcome)}");

        if (outcome == TradeOutcome.Success) BroadcastTradeState();
    }

    /// <summary>
    /// İki taraf da onayladıysa takası uygular.
    ///
    /// Envanterler host'un AYNALARIDIR. Uygulama atomiktir: sığmazsa veya
    /// item yoksa hiçbir şey değişmez ve takas pazarlığa geri döner.
    /// </summary>
    private void TryCompleteTrade()
    {
        if (_trade.State != TradeState.BothAccepted) return;

        var inventoryA = InventoryOf(_trade.PlayerA);
        var inventoryB = InventoryOf(_trade.PlayerB);

        if (inventoryA is null || inventoryB is null)
        {
            _trade.Cancel();
            Notice?.Invoke("Takas iptal: taraflardan birinin envanteri yok");
            return;
        }

        // Uygulamadan ONCEKI durum, deltalari cikarmak icin gerekli:
        // istemciye "sende artik su kadar var" degil, "su kadar degisti"
        // gonderiliyor.
        var beforeA = SnapshotCounts(inventoryA, _trade);
        var beforeB = SnapshotCounts(inventoryB, _trade);

        var outcome = _trade.TryExecute(inventoryA, inventoryB);

        if (outcome != TradeOutcome.Success)
        {
            Notice?.Invoke($"Takas basarisiz: {TradeSession.Describe(outcome)}");

            // Basarisiz takas iptal EDILMEZ: taraflar teklifi duzeltip
            // tekrar deneyebilsin. Yalnizca onaylar duser.
            _trade.Retract(_trade.PlayerA);
            _trade.Retract(_trade.PlayerB);
            return;
        }

        SendTradeDeltas(_trade.PlayerA, inventoryA, beforeA);
        SendTradeDeltas(_trade.PlayerB, inventoryB, beforeB);

        Notice?.Invoke("Takas tamamlandi");
        TradeChanged?.Invoke();
    }

    /// <summary>Takasa konu item'ların işlem ÖNCESİ adetleri.</summary>
    private static Dictionary<string, int> SnapshotCounts(WorldInventory inventory,
                                                          TradeSession trade)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var stack in trade.OfferA.Concat(trade.OfferB))
        {
            counts[stack.ItemId] = inventory.CountOf(stack.ItemId);
        }

        return counts;
    }

    /// <summary>Takas sonrası farkı ilgili istemciye bildirir.</summary>
    private void SendTradeDeltas(byte playerId, WorldInventory inventory,
                                 Dictionary<string, int> before)
    {
        if (playerId == LocalPlayerId) return;   // host kendi envanterini zaten degistirdi
        if (!_playerToPeer.TryGetValue(playerId, out var peer)) return;

        foreach (var (itemId, previous) in before)
        {
            var delta = inventory.CountOf(itemId) - previous;
            if (delta != 0)
            {
                _transport?.Send(peer, NetworkProtocol.WriteInventoryDelta(itemId, delta));
            }
        }
    }

    /// <summary>Takasın durumunu iki tarafa da bildirir.</summary>
    private void BroadcastTradeState()
    {
        TradeChanged?.Invoke();

        if (_transport is null) return;

        SendTradeStateTo(_trade.PlayerA, _trade.AcceptedA, _trade.AcceptedB, _trade.PlayerB);
        SendTradeStateTo(_trade.PlayerB, _trade.AcceptedB, _trade.AcceptedA, _trade.PlayerA);
    }

    private void SendTradeStateTo(byte playerId, bool acceptedSelf, bool acceptedOther,
                                  byte partnerId)
    {
        if (playerId == LocalPlayerId) return;
        if (!_playerToPeer.TryGetValue(playerId, out var peer)) return;

        _transport!.Send(peer, NetworkProtocol.WriteTradeState(
            (byte)_trade.State, acceptedSelf, acceptedOther, partnerId));
    }

    // ================= host =================

    private void AcceptPlayer(int peerId, Vector2 spawnPosition)
    {
        var id = _nextPlayerId++;

        _peerToPlayer[peerId] = id;
        _playerToPeer[id] = peerId;
        _hostPlayers[id] = new Player(_playerSheet, spawnPosition);
        _hostGathering[id] = new GatheringSystem(_resources);

        // Ayna bos baslar: istemci neye sahip oldugunu SOYLEYEMEZ, host
        // yalnizca kendi verdigini bilir.
        _hostInventories[id] = new WorldInventory(_items);

        _transport!.Send(peerId, NetworkProtocol.WriteWelcome(id, WorldSeed, ModFingerprint));
        Notice?.Invoke($"Oyuncu {id} katildi ({_transport.PeerCount} bagli)");
    }

    private void DropPlayer(int peerId)
    {
        if (!_peerToPlayer.Remove(peerId, out var id))
        {
            return;
        }

        _playerToPeer.Remove(id);
        _hostPlayers.Remove(id);
        _hostGathering.Remove(id);
        _hostInventories.Remove(id);
        _pendingInput.Remove(id);

        _transport!.Broadcast(NetworkProtocol.WritePlayerLeft(id));
        Notice?.Invoke($"Oyuncu {id} ayrildi");
    }

    private void HostTick(float tickLength, Player localPlayer, TileMap map,
                          CombatSystem combat, Vector2 spawnPosition)
    {
        // Tick uzunluğunda sahte bir GameTime: uzak oyuncular ekran hızında
        // değil, sabit tick adımıyla simüle edilir. Böylece host'un FPS'i
        // istemcilerin hızını etkilemez.
        var tickTime = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(tickLength));

        // Yerel oyuncu dahil herkes tek sözlükte: combat ve snapshot aynı
        // koddan geçsin, host kendini özel durum olarak ele almasın.
        var everyone = new Dictionary<byte, Player>(_hostPlayers) { [LocalPlayerId] = localPlayer };

        foreach (var (id, player) in _hostPlayers)
        {
            var frame = _pendingInput.GetValueOrDefault(id);
            var input = new PlayerInput(new Vector2(frame.MoveX, frame.MoveY), frame.Gather);

            var action = PlayerAction.None;

            // Toplama host'ta çözülür: tile değişimi ve verilen item
            // otoriter tarafta üretilir.
            var gathered = _hostGathering[id].Update(input, player, map, tickTime);
            if (gathered is { } result)
            {
                var tileIndex = map.GetTileIndex(result.Tile.X, result.Tile.Y);
                _transport!.Broadcast(
                    NetworkProtocol.WriteTileChange(result.Tile.X, result.Tile.Y, (ushort)tileIndex));

                // Aynaya da yazilir: istemciye verilen item host'un
                // defterinde de gorunmeli, yoksa takas dogrulamasi
                // istemcinin gercekten sahip oldugu seyi reddederdi.
                var granted = result.Amount;

                if (_hostInventories.TryGetValue(id, out var mirror))
                {
                    granted -= mirror.TryAdd(result.Resource, result.Amount);
                }

                if (granted > 0 && _playerToPeer.TryGetValue(id, out var peer))
                {
                    _transport.Send(peer,
                        NetworkProtocol.WriteInventoryDelta(result.Resource, granted));
                }
            }

            if (_hostGathering[id].IsGathering)
            {
                action = PlayerAction.Gathering;
            }

            if (frame.Attack)
            {
                var hits = combat.TryAttack(id, player, everyone);
                if (hits.Count > 0)
                {
                    action = PlayerAction.Attacking;
                }
            }

            if (combat.IsSwinging(id))
            {
                action = PlayerAction.Attacking;
            }

            player.Update(input, map, tickTime, action);
        }

        combat.UpdateRespawns(everyone, spawnPosition);

        BroadcastSnapshot(everyone, combat);
    }

    private void BroadcastSnapshot(Dictionary<byte, Player> everyone, CombatSystem combat)
    {
        var states = new List<PlayerState>(everyone.Count);

        foreach (var (id, player) in everyone)
        {
            byte flags = 0;
            if (player.IsDead) flags |= PlayerState.FlagDead;
            if (combat.IsSwinging(id)) flags |= PlayerState.FlagAttacking;

            states.Add(new PlayerState(
                id,
                player.Position.X,
                player.Position.Y,
                (byte)player.Facing,
                (byte)Math.Clamp(player.Health, 0, 255),
                flags));
        }

        _transport!.Broadcast(NetworkProtocol.WriteSnapshot(_tick, states));
    }

    /// <summary>Host'un kendi dünya değişikliğini istemcilere bildirir.</summary>
    public void NotifyTileChanged(int tileX, int tileY, int tileIndex)
    {
        if (Mode == SessionMode.Host)
        {
            _transport?.Broadcast(
                NetworkProtocol.WriteTileChange(tileX, tileY, (ushort)tileIndex));
        }
    }

    // ================= istemci =================

    private void SendInput(PlayerInput input)
    {
        byte flags = 0;
        if (input.Gather) flags |= InputFrame.FlagGather;
        if (input.Attack) flags |= InputFrame.FlagAttack;

        _transport!.Broadcast(NetworkProtocol.WriteClientInput(
            new InputFrame(_tick, input.Move.X, input.Move.Y, flags)));
    }

    /// <summary>
    /// Snapshot'ı uygular: uzak oyuncuları tazeler, yerel oyuncuyu uzlaştırır.
    /// </summary>
    private void ApplySnapshot(List<PlayerState> states, Player localPlayer)
    {
        var seen = new HashSet<byte>();

        foreach (var state in states)
        {
            seen.Add(state.PlayerId);

            if (state.PlayerId == LocalPlayerId)
            {
                ReconcileLocal(state, localPlayer);
                continue;
            }

            if (_remotes.TryGetValue(state.PlayerId, out var remote))
            {
                remote.Apply(state);
            }
            else
            {
                _remotes[state.PlayerId] = new RemotePlayer(_playerSheet, state);
            }
        }

        // Snapshot'ta olmayan oyuncular oturumdan çıkmıştır.
        foreach (var id in _remotes.Keys.ToArray())
        {
            if (!seen.Contains(id))
            {
                _remotes.Remove(id);
            }
        }
    }

    /// <summary>
    /// Sunucu uzlaştırması (server reconciliation), sade hali.
    ///
    /// Yerel oyuncu girdisine ANINDA tepki verir (client-side prediction) —
    /// yoksa her harekette bir gidiş-dönüş gecikmesi hissedilirdi. Host'un
    /// söylediği konum tahminden belirgin biçimde ayrışırsa oraya çekilir.
    ///
    /// Gerçek uzlaştırma, düzeltmeden sonra kaydedilmiş girdileri yeniden
    /// oynatır (input replay). Bu aşamada spec'in dediği gibi basit
    /// yumuşatma yeterli; replay Aşama 2'de gerekirse eklenir.
    /// </summary>
    private static void ReconcileLocal(PlayerState state, Player localPlayer)
    {
        localPlayer.ApplyNetworkHealth(state.Health);

        var authoritative = new Vector2(state.X, state.Y);
        var drift = Vector2.Distance(localPlayer.Position, authoritative);

        if (drift <= ReconcileThreshold)
        {
            return;
        }

        // Büyük sapmalarda (ölüm, ışınlanma) doğrudan atla, küçüklerde yumuşat.
        localPlayer.Position = drift > 64f
            ? authoritative
            : Vector2.Lerp(localPlayer.Position, authoritative, 0.35f);
    }
}
