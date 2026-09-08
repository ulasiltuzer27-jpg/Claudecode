using Microsoft.Xna.Framework;
using PixelSurvival.Entities;
using PixelSurvival.Inventory;
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
    private byte _nextPlayerId = 1;

    // --- İstemci tarafı ---
    private readonly Dictionary<byte, RemotePlayer> _remotes = [];

    public SessionMode Mode { get; private set; } = SessionMode.Offline;

    /// <summary>Host her zaman 0'dır; istemciler 1'den başlar.</summary>
    public byte LocalPlayerId { get; private set; }

    public int WorldSeed { get; private set; }
    public IReadOnlyCollection<RemotePlayer> RemotePlayers => _remotes.Values;
    public int ConnectedCount => _transport?.PeerCount ?? 0;

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
                if (NetworkProtocol.TryReadWelcome(data, out var assigned, out var seed))
                {
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
        }
    }

    // ================= host =================

    private void AcceptPlayer(int peerId, Vector2 spawnPosition)
    {
        var id = _nextPlayerId++;

        _peerToPlayer[peerId] = id;
        _playerToPeer[id] = peerId;
        _hostPlayers[id] = new Player(_playerSheet, spawnPosition);
        _hostGathering[id] = new GatheringSystem(_resources);

        _transport!.Send(peerId, NetworkProtocol.WriteWelcome(id, WorldSeed));
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

                if (_playerToPeer.TryGetValue(id, out var peer))
                {
                    _transport.Send(peer,
                        NetworkProtocol.WriteInventoryDelta(result.Resource, result.Amount));
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
