#if STEAM_BUILD
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
#endif

namespace PixelSurvival.Networking;

/// <summary>
/// AŞAMA 2 / MADDE 19 — Steam build'inin taşıma katmanı.
///
/// ════════════════════════════════════════════════════════════════════════
/// STEAM RELAY ÜZERİNDEN P2P
/// ════════════════════════════════════════════════════════════════════════
/// Adresleme SteamID üzerinden: host <c>CreateRelaySocket</c> ile dinler,
/// istemci <c>ConnectRelay</c> ile host'un SteamID'sine bağlanır. NAT
/// traversal ve Valve relay ağı bedavaya geliyor; oyuncular port
/// yönlendirme yapmıyor ve birbirinin IP'sini hiç görmüyor.
/// ════════════════════════════════════════════════════════════════════════
///
/// ── Facepunch modeli ────────────────────────────────────────────────────
/// Steamworks.NET'te bağlantı tutamakları (<c>HSteamNetConnection</c>),
/// poll grupları ve elle çözülen mesaj işaretçileri vardı; <c>SendMessage</c>
/// için <c>unsafe</c> blok gerekiyordu. Facepunch aynı işi iki soyut sınıfla
/// veriyor: host <see cref="SocketManager"/>, istemci
/// <see cref="ConnectionManager"/> türetiyor ve bağlantı olayları sanal
/// metot olarak geliyor. Poll grubu, mesaj serbest bırakma ve
/// <c>unsafe</c> tamamen ortadan kalktı.
///
/// Gameplay kodu bu sınıfı GÖRMEZ; <see cref="INetworkTransport"/> arkasında.
///
/// Derleme: yalnızca <c>STEAM_BUILD</c> sembolüyle gerçek gövde derlenir.
/// </summary>
public sealed class SteamNetworkingTransport : INetworkTransport
{
#if STEAM_BUILD
    /// <summary>
    /// İstemci tarafında host'un peer kimliği.
    ///
    /// SIFIR olması ŞART. <see cref="NetworkSession"/> istemciden host'a
    /// giden her mesajı <c>Send(0, ...)</c> ile yolluyor (takas, üretim,
    /// dünya eylemi) ve <see cref="LiteNetLibTransport"/> de aynı numarayı
    /// kullanıyor — orada host, LiteNetLib'in 0 numaralı peer'ı.
    ///
    /// Steamworks.NET sürümünde bu numara 1'den başlıyordu ve istemcinin
    /// host'a gönderdiği HİÇBİR mesaj karşı tarafa ulaşmıyordu; hata
    /// sessizdi çünkü <c>Send</c> bilinmeyen peer'da hiçbir şey yapmadan
    /// dönüyor.
    /// </summary>
    private const int HostPeerId = 0;

    /// <summary>Tek seferde okunacak en fazla mesaj.</summary>
    private const int ReceiveBatch = 32;

    /// <summary>Bu oyunun relay sanal portu. Tek soket yeterli.</summary>
    private const int VirtualPort = 0;

    /// <summary>
    /// Host tarafı — gelen bağlantıları kabul eder ve mesajları taşıyıcıya
    /// iletir.
    /// </summary>
    private sealed class HostSocket : SocketManager
    {
        // Parametresiz kurucu SART: Facepunch soketi kendisi ornekliyor
        // (CreateRelaySocket<T> where T : new()), bu yuzden tasiyici
        // referansi kurucudan degil olusturulduktan SONRA veriliyor.
        // Mesajlar ancak Receive() cagrilinca aktigi icin bu guvenli.
        public SteamNetworkingTransport Owner { get; set; } = null!;

        public override void OnConnecting(Connection connection, ConnectionInfo info)
        {
            // Kabul etmeden once base cagriliyor: Facepunch'in kendi
            // "Connecting" listesi guncel kalsin.
            base.OnConnecting(connection, info);
            connection.Accept();
        }

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            base.OnConnected(connection, info);
            Owner.RegisterPeer(connection);
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            base.OnDisconnected(connection, info);
            Owner.ForgetPeer(connection);
        }

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data,
                                       int size, long messageNum, long recvTime, int channel)
        {
            Owner.QueueData(connection, data, size);
        }
    }

    /// <summary>İstemci tarafı — tek bir host bağlantısı.</summary>
    private sealed class ClientConnection : ConnectionManager
    {
        public SteamNetworkingTransport Owner { get; set; } = null!;

        public override void OnConnected(ConnectionInfo info)
        {
            base.OnConnected(info);
            Owner.Enqueue(new NetworkEvent(NetworkEventType.Connected, HostPeerId, default));
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            base.OnDisconnected(info);
            Owner.Enqueue(new NetworkEvent(NetworkEventType.Disconnected, HostPeerId, default));
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime,
                                       int channel)
        {
            Owner.Enqueue(new NetworkEvent(NetworkEventType.Data, HostPeerId, Copy(data, size)));
        }
    }

    private readonly Dictionary<int, Connection> _peers = [];

    /// <summary>
    /// Biriken olaylar.
    /// </summary>
    /// <remarks>
    /// Kuyruk, liste değil: bağlantı olayları <c>SteamClient.RunCallbacks</c>
    /// sırasında geliyor ve o çağrı <c>Game1.Update</c>'in EN BAŞINDA, yani
    /// <see cref="PollEvents"/>'ten ÖNCE. <c>PollEvents</c> listeyi
    /// temizleseydi o karede gelen "bağlandı" olayı okunmadan silinirdi.
    /// Kuyruk biriktirip boşaltıldığı için sıra önemsiz.
    /// </remarks>
    private readonly Queue<NetworkEvent> _pending = new();

    /// <summary>Dışarı verilen tampon — kare başına yeni liste ayırmamak için.</summary>
    private readonly List<NetworkEvent> _drained = [];

    private HostSocket? _socket;
    private ClientConnection? _connection;
    private int _nextPeerId = 1;

    public bool IsRunning => _socket is not null || _connection is not null;

    public int PeerCount => _socket is not null ? _peers.Count : _connection is null ? 0 : 1;

    public void StartHost(int port)
    {
        // port yok sayılır: relay adreslemesi SteamID üzerinden yapılır,
        // dinlenecek bir UDP portu yoktur.
        _ = port;

        Stop();

        SteamNetworkingUtils.InitRelayNetworkAccess();
        _socket = SteamNetworkingSockets.CreateRelaySocket<HostSocket>(VirtualPort);
        _socket.Owner = this;
    }

    public void Connect(string address, int port)
    {
        _ = port;

        // address = host'un SteamID'si (ondalık dize).
        if (!ulong.TryParse(address, out var steamId))
        {
            throw new ArgumentException(
                "Steam taşımasında adres, host'un SteamID'si olmalı (IP değil).", nameof(address));
        }

        Stop();

        SteamNetworkingUtils.InitRelayNetworkAccess();
        _connection = SteamNetworkingSockets.ConnectRelay<ClientConnection>(steamId, VirtualPort);
        _connection.Owner = this;
    }

    public void Send(int peerId, ReadOnlySpan<byte> payload)
    {
        // Istemci: tek hedef var, o da host.
        if (_connection is not null)
        {
            if (peerId == HostPeerId) SendTo(_connection.Connection, payload);
            return;
        }

        if (_peers.TryGetValue(peerId, out var connection)) SendTo(connection, payload);
    }

    public void Broadcast(ReadOnlySpan<byte> payload)
    {
        if (_connection is not null)
        {
            SendTo(_connection.Connection, payload);
            return;
        }

        foreach (var connection in _peers.Values) SendTo(connection, payload);
    }

    /// <summary>
    /// Tek bir bağlantıya yazar.
    ///
    /// Facepunch <c>byte[]</c> alan bir aşırı yükleme sunuyor; Steamworks.NET
    /// sürümünde burada <c>fixed</c> bloğu ve projede
    /// <c>AllowUnsafeBlocks</c> gerekiyordu. İkisi de artık gereksiz.
    /// </summary>
    private static void SendTo(Connection connection, ReadOnlySpan<byte> payload)
    {
        // Su an her mesaj GUVENILIR gidiyor. Teslim modu ayrimi
        // (pozisyon=unreliable, saldiri=reliable) INetworkTransport'ta
        // ileride eklenecek — bu asamada kodlanmadi.
        connection.SendMessage(payload.ToArray(), SendType.Reliable);
    }

    public IReadOnlyList<NetworkEvent> PollEvents()
    {
        // Receive mesajlari okuyup OnMessage'i tetikliyor; baglanti
        // olaylari ise RunCallbacks sirasinda zaten kuyruga girdi.
        _socket?.Receive(ReceiveBatch);
        _connection?.Receive(ReceiveBatch);

        _drained.Clear();
        while (_pending.Count > 0) _drained.Add(_pending.Dequeue());

        return _drained;
    }

    public void Stop()
    {
        foreach (var connection in _peers.Values) connection.Close();
        _peers.Clear();

        _socket?.Close();
        _socket = null;

        _connection?.Close();
        _connection = null;

        _pending.Clear();
        _drained.Clear();
        _nextPeerId = 1;
    }

    // ---------------- ic yardimcilar ----------------

    private void RegisterPeer(Connection connection)
    {
        var peerId = _nextPeerId++;
        _peers[peerId] = connection;

        Enqueue(new NetworkEvent(NetworkEventType.Connected, peerId, default));
    }

    private void ForgetPeer(Connection connection)
    {
        // Connection.Id benzersiz; esitlik uzerinden aramak yerine onunla
        // esleniyor cunku struct esitligi baglanti durumu degistikce
        // guvenilir degil.
        foreach (var (peerId, candidate) in _peers)
        {
            if (candidate.Id != connection.Id) continue;

            _peers.Remove(peerId);
            Enqueue(new NetworkEvent(NetworkEventType.Disconnected, peerId, default));
            return;
        }
    }

    private void QueueData(Connection connection, IntPtr data, int size)
    {
        foreach (var (peerId, candidate) in _peers)
        {
            if (candidate.Id != connection.Id) continue;

            Enqueue(new NetworkEvent(NetworkEventType.Data, peerId, Copy(data, size)));
            return;
        }
    }

    private void Enqueue(NetworkEvent netEvent) => _pending.Enqueue(netEvent);

    /// <summary>Yerel olmayan mesaj tamponunu yönetilen diziye kopyalar.</summary>
    private static byte[] Copy(IntPtr data, int size)
    {
        var buffer = new byte[size];
        Marshal.Copy(data, buffer, 0, size);
        return buffer;
    }
#else
    private const string Unavailable =
        "SteamNetworkingTransport yalnizca STEAM_BUILD derlemesinde kullanilabilir. " +
        "Gelistirme derlemesinde LiteNetLibTransport kullanin " +
        "(dotnet build -c SteamRelease ile Steam derlemesi alinir).";

    public bool IsRunning => false;
    public int PeerCount => 0;

    public void StartHost(int port) => throw new InvalidOperationException(Unavailable);
    public void Connect(string address, int port) => throw new InvalidOperationException(Unavailable);
    public void Send(int peerId, ReadOnlySpan<byte> payload) { }
    public void Broadcast(ReadOnlySpan<byte> payload) { }
    public IReadOnlyList<NetworkEvent> PollEvents() => [];
    public void Stop() { }
#endif

    public void Dispose() => Stop();
}
