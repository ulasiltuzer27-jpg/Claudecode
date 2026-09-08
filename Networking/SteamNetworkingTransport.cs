namespace PixelSurvival.Networking;

/// <summary>
/// AŞAMA 2 / MADDE 19 — Steam build'inin taşıma katmanı.
///
/// ════════════════════════════════════════════════════════════════════════
/// ISteamNetworkingSockets ÜZERİNDEN
/// ════════════════════════════════════════════════════════════════════════
/// Eski <c>ISteamNetworking</c> API'si DEPRECATED ve KULLANILMIYOR.
/// Yeni API ile NAT traversal ve Valve relay ağı bedavaya geliyor:
/// oyuncular port yönlendirme yapmadan birbirine bağlanabiliyor.
///
/// P2P adresleme SteamID üzerinden: host, <c>CreateListenSocketP2P</c> ile
/// dinler; istemci <c>ConnectP2P</c> ile host'un SteamID'sine bağlanır.
/// IP adresi hiç görünmez — oyuncular birbirinin IP'sini öğrenmez.
/// ════════════════════════════════════════════════════════════════════════
///
/// Gameplay kodu bu sınıfı GÖRMEZ; <see cref="INetworkTransport"/> arkasında.
/// LiteNetLibTransport'tan geçiş tek satırlık bir örnekleme değişikliği.
///
/// Derleme: yalnızca <c>STEAM_BUILD</c> sembolüyle gerçek gövde derlenir.
/// Sembol yokken sınıf var ama çağrıldığında açıklayıcı bir hata verir —
/// geliştirme derlemesi Steamworks.NET'e ve Steam istemcisine ihtiyaç
/// duymadan çalışsın diye.
/// </summary>
public sealed class SteamNetworkingTransport : INetworkTransport
{
#if STEAM_BUILD
    private readonly List<NetworkEvent> _events = [];
    private readonly List<byte[]> _buffers = [];
    private readonly Dictionary<int, Steamworks.HSteamNetConnection> _peers = [];

    private Steamworks.HSteamListenSocket _listenSocket;
    private Steamworks.HSteamNetPollGroup _pollGroup;
    private Steamworks.Callback<Steamworks.SteamNetConnectionStatusChangedCallback_t>? _statusChanged;
    private bool _running;
    private int _nextPeerId = 1;

    public bool IsRunning => _running;
    public int PeerCount => _peers.Count;

    public void StartHost(int port)
    {
        // port yok sayılır: P2P adresleme SteamID üzerinden yapılır,
        // dinlenecek bir UDP portu yoktur.
        _ = port;

        Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();
        _listenSocket = Steamworks.SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
        _pollGroup = Steamworks.SteamNetworkingSockets.CreatePollGroup();

        HookStatusCallback();
        _running = true;
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

        Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();

        var identity = new Steamworks.SteamNetworkingIdentity();
        identity.SetSteamID64(steamId);

        _pollGroup = Steamworks.SteamNetworkingSockets.CreatePollGroup();
        HookStatusCallback();

        Steamworks.SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
        _running = true;
    }

    private void HookStatusCallback()
    {
        _statusChanged ??= Steamworks.Callback<Steamworks.SteamNetConnectionStatusChangedCallback_t>
            .Create(OnConnectionStatusChanged);
    }

    private void OnConnectionStatusChanged(Steamworks.SteamNetConnectionStatusChangedCallback_t data)
    {
        switch (data.m_info.m_eState)
        {
            case Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                // Host tarafı: gelen bağlantıyı kabul et.
                Steamworks.SteamNetworkingSockets.AcceptConnection(data.m_hConn);
                Steamworks.SteamNetworkingSockets.SetConnectionPollGroup(data.m_hConn, _pollGroup);
                break;

            case Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
            {
                var peerId = _nextPeerId++;
                _peers[peerId] = data.m_hConn;
                _events.Add(new NetworkEvent(NetworkEventType.Connected, peerId, default));
                break;
            }

            case Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case Steamworks.ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
            {
                var entry = _peers.FirstOrDefault(p => p.Value.m_HSteamNetConnection ==
                                                      data.m_hConn.m_HSteamNetConnection);
                if (entry.Value.m_HSteamNetConnection != 0)
                {
                    _peers.Remove(entry.Key);
                    _events.Add(new NetworkEvent(NetworkEventType.Disconnected, entry.Key, default));
                }

                Steamworks.SteamNetworkingSockets.CloseConnection(data.m_hConn, 0, null, false);
                break;
            }
        }
    }

    public void Send(int peerId, ReadOnlySpan<byte> payload)
    {
        if (!_peers.TryGetValue(peerId, out var connection))
        {
            return;
        }

        SendTo(connection, payload);
    }

    public void Broadcast(ReadOnlySpan<byte> payload)
    {
        foreach (var connection in _peers.Values)
        {
            SendTo(connection, payload);
        }
    }

    private static unsafe void SendTo(Steamworks.HSteamNetConnection connection,
                                      ReadOnlySpan<byte> payload)
    {
        // Şu an her mesaj güvenilir gidiyor. Teslim modu ayrımı
        // (pozisyon=unreliable, saldırı=reliable) INetworkTransport'ta
        // ileride eklenecek — bu aşamada kodlanmadı.
        fixed (byte* pointer = payload)
        {
            Steamworks.SteamNetworkingSockets.SendMessageToConnection(
                connection, (IntPtr)pointer, (uint)payload.Length,
                Steamworks.Constants.k_nSteamNetworkingSend_Reliable, out _);
        }
    }

    public IReadOnlyList<NetworkEvent> PollEvents()
    {
        _buffers.Clear();
        _events.Clear();

        Steamworks.SteamAPI.RunCallbacks();

        var messages = new IntPtr[32];
        var count = Steamworks.SteamNetworkingSockets.ReceiveMessagesOnPollGroup(
            _pollGroup, messages, messages.Length);

        for (var i = 0; i < count; i++)
        {
            var message = Steamworks.SteamNetworkingMessage_t.FromIntPtr(messages[i]);
            var buffer = new byte[message.m_cbSize];
            System.Runtime.InteropServices.Marshal.Copy(message.m_pData, buffer, 0, buffer.Length);

            var entry = _peers.FirstOrDefault(p => p.Value.m_HSteamNetConnection ==
                                                  message.m_conn.m_HSteamNetConnection);

            _buffers.Add(buffer);
            _events.Add(new NetworkEvent(NetworkEventType.Data, entry.Key, buffer));

            Steamworks.SteamNetworkingMessage_t.Release(messages[i]);
        }

        return _events;
    }

    public void Stop()
    {
        foreach (var connection in _peers.Values)
        {
            Steamworks.SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
        }

        _peers.Clear();

        if (_listenSocket.m_HSteamListenSocket != 0)
        {
            Steamworks.SteamNetworkingSockets.CloseListenSocket(_listenSocket);
        }

        _events.Clear();
        _buffers.Clear();
        _running = false;
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
