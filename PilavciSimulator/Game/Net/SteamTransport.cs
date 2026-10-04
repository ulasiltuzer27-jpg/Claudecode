#if STEAM_BUILD
using System.Runtime.InteropServices;
using PilavciSimulator.Core;
using Steamworks;
using Steamworks.Data;

namespace PilavciSimulator.Net;

/// <summary>
/// Steam relay tasimasi (ISteamNetworkingSockets, Facepunch sarmalayicisi).
/// Host bir "relay soket" acar; istemci host'un SteamId'sine baglanir.
/// NAT gecisi ve sifreleme Valve'in relay agindan bedava gelir.
/// Kalip PixelSurvival'daki Networking/SteamNetworkingTransport.cs.
///
/// Adres olarak host'un SteamId'si (ondalik) verilir; lobi sistemi
/// (SteamLobby) bunu davet/katilma isteginden bulur.
/// </summary>
public sealed class SteamTransport : INetTransport
{
    private const int VirtualPort = 0;

    private sealed class HostSocket : SocketManager
    {
        public SteamTransport Owner = null!;

        public override void OnConnecting(Connection connection, ConnectionInfo info)
        {
            if (Owner._peers.Count >= Owner.MaxPeers)
            {
                connection.Close();
                return;
            }

            connection.Accept();
        }

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            var id = Owner._nextPeer++;
            Owner._peers[id] = connection;
            Owner._byConn[connection.Id] = id;
            Owner._queue.Enqueue(new NetEvent(NetEventType.Connected, id, []));
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            if (Owner._byConn.Remove(connection.Id, out var id))
            {
                Owner._peers.Remove(id);
                Owner._queue.Enqueue(new NetEvent(NetEventType.Disconnected, id, [], info.EndReason.ToString()));
            }
        }

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            if (Owner._byConn.TryGetValue(connection.Id, out var id))
            {
                var bytes = new byte[size];
                Marshal.Copy(data, bytes, 0, size);
                Owner._queue.Enqueue(new NetEvent(NetEventType.Data, id, bytes));
            }
        }
    }

    private sealed class ClientConnection : ConnectionManager
    {
        public SteamTransport Owner = null!;

        public override void OnConnected(ConnectionInfo info) => Owner._queue.Enqueue(new NetEvent(NetEventType.Connected, 0, []));

        public override void OnDisconnected(ConnectionInfo info) =>
            Owner._queue.Enqueue(new NetEvent(NetEventType.Disconnected, 0, [], info.EndReason.ToString()));

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, size);
            Owner._queue.Enqueue(new NetEvent(NetEventType.Data, 0, bytes));
        }
    }

    private readonly Dictionary<int, Connection> _peers = new();
    private readonly Dictionary<uint, int> _byConn = new();
    private readonly Queue<NetEvent> _queue = new();
    private readonly List<NetEvent> _events = new();
    private HostSocket? _socket;
    private ClientConnection? _client;
    private int _nextPeer = 1;
    public int MaxPeers { get; set; } = 3;

    public bool IsRunning => _socket is not null || _client is not null;
    public int PeerCount => _socket is not null ? _peers.Count : _client is null ? 0 : 1;
    // Facepunch 2.3.3 hizli durum (ping) API'sini acmiyor; yalnizca metin halinde DetailedStatus var.
    public int PingMs => -1;

    public void StartHost(int port)
    {
        SteamNetworkingUtils.InitRelayNetworkAccess();
        _socket = SteamNetworkingSockets.CreateRelaySocket<HostSocket>(VirtualPort);
        _socket.Owner = this;
        Log.Info("Steam relay soketi acildi");
    }

    public void Connect(string address, int port)
    {
        if (!ulong.TryParse(address, out var steamId))
        {
            throw new ArgumentException("Steam baglantisi icin host SteamId'si gerekli");
        }

        SteamNetworkingUtils.InitRelayNetworkAccess();
        _client = SteamNetworkingSockets.ConnectRelay<ClientConnection>(steamId, VirtualPort);
        _client.Owner = this;
    }

    private static SendType Map(Delivery d) => d == Delivery.Reliable ? SendType.Reliable : SendType.Unreliable;

    public void Send(int peerId, ReadOnlySpan<byte> payload, Delivery delivery)
    {
        var bytes = payload.ToArray();
        if (_socket is not null && _peers.TryGetValue(peerId, out var c))
        {
            c.SendMessage(bytes, Map(delivery));
        }
        else
        {
            _client?.Connection.SendMessage(bytes, Map(delivery));
        }
    }

    public void Broadcast(ReadOnlySpan<byte> payload, Delivery delivery, int exceptPeer = -1)
    {
        var bytes = payload.ToArray();
        if (_socket is not null)
        {
            foreach (var (id, c) in _peers)
            {
                if (id != exceptPeer)
                {
                    c.SendMessage(bytes, Map(delivery));
                }
            }
        }
        else
        {
            _client?.Connection.SendMessage(bytes, Map(delivery));
        }
    }

    public void Kick(int peerId)
    {
        if (_peers.TryGetValue(peerId, out var c))
        {
            c.Close();
        }
    }

    public IReadOnlyList<NetEvent> Poll()
    {
        _events.Clear();
        _socket?.Receive();
        _client?.Receive();
        while (_queue.Count > 0)
        {
            _events.Add(_queue.Dequeue());
        }

        return _events;
    }

    public void Stop()
    {
        _socket?.Close();
        _client?.Close();
        _socket = null;
        _client = null;
        _peers.Clear();
        _byConn.Clear();
    }

    public void Dispose() => Stop();
}

/// <summary>
/// Steam lobisi: host lobi kurar, arkadaslar davetle ya da "Oyuna katil"
/// ile gelir. Lobi verisinde host'un SteamId'si ve oyun surumu var.
/// </summary>
public sealed class SteamLobby : IDisposable
{
    public Lobby? Current { get; private set; }
    public event Action<ulong>? JoinRequested;

    public SteamLobby()
    {
        SteamFriends.OnGameLobbyJoinRequested += OnJoinRequested;
        SteamMatchmaking.OnLobbyEntered += lobby => Current = lobby;
    }

    private void OnJoinRequested(Lobby lobby, SteamId friend)
    {
        _ = JoinAsync(lobby);
    }

    public async Task CreateAsync(int maxPlayers, string version)
    {
        var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        if (lobby is not { } l)
        {
            Log.Warn("Steam lobisi kurulamadi");
            return;
        }

        l.SetFriendsOnly();
        l.SetJoinable(true);
        l.SetData("host", SteamClient.SteamId.Value.ToString());
        l.SetData("version", version);
        l.SetData("game", "pilavci");
        Current = l;
        Log.Info($"Steam lobisi kuruldu: {l.Id}");
    }

    public async Task JoinAsync(Lobby lobby)
    {
        var result = await lobby.Join();
        if (result != RoomEnter.Success)
        {
            Log.Warn($"lobiye girilemedi: {result}");
            return;
        }

        Current = lobby;
        if (ulong.TryParse(lobby.GetData("host"), out var host))
        {
            JoinRequested?.Invoke(host);
        }
    }

    public void OpenInvite()
    {
        if (Current is { } l)
        {
            SteamFriends.OpenGameInviteOverlay(l.Id);
        }
    }

    public void Dispose()
    {
        SteamFriends.OnGameLobbyJoinRequested -= OnJoinRequested;
        Current?.Leave();
        Current = null;
    }
}
#endif
