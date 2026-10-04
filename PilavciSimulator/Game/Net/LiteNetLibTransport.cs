using LiteNetLib;
using LiteNetLib.Utils;

namespace PilavciSimulator.Net;

/// <summary>
/// Dogrudan IP tasimasi (LiteNetLib). LiteNetLib'e dokunan tek dosya.
/// Kalip PixelSurvival'daki Networking/LiteNetLibTransport.cs.
/// </summary>
public sealed class LiteNetLibTransport : INetTransport
{
    private const string ConnectionKey = "pilavci-simulatoru";
    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _manager;
    private readonly List<NetEvent> _events = new();
    private readonly Dictionary<int, NetPeer> _peers = new();
    public int MaxPeers { get; set; } = 3;

    public bool IsRunning => _manager.IsRunning;
    public int PeerCount => _peers.Count;
    public int PingMs => _peers.Count == 1 ? _peers.Values.First().Ping : -1;

    public LiteNetLibTransport()
    {
        _manager = new NetManager(_listener)
        {
            UnsyncedEvents = false,
            AutoRecycle = true,
            DisconnectTimeout = 10000,
            ChannelsCount = 2,
        };
        _listener.ConnectionRequestEvent += req =>
        {
            if (_peers.Count >= MaxPeers)
            {
                req.Reject();
                return;
            }

            req.AcceptIfKey(ConnectionKey);
        };
        _listener.PeerConnectedEvent += peer =>
        {
            _peers[peer.Id] = peer;
            _events.Add(new NetEvent(NetEventType.Connected, peer.Id, []));
        };
        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            _peers.Remove(peer.Id);
            _events.Add(new NetEvent(NetEventType.Disconnected, peer.Id, [], info.Reason.ToString()));
        };
        _listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            _events.Add(new NetEvent(NetEventType.Data, peer.Id, reader.GetRemainingBytes()));
        };
    }

    public void StartHost(int port)
    {
        if (!_manager.Start(port))
        {
            throw new InvalidOperationException($"{port} portu dinlenemedi (baska bir oyun aciksa kapatin).");
        }
    }

    public void Connect(string address, int port)
    {
        if (!_manager.Start())
        {
            throw new InvalidOperationException("istemci soketi acilamadi");
        }

        _manager.Connect(address, port, ConnectionKey);
    }

    private static DeliveryMethod Map(Delivery d) => d == Delivery.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Sequenced;

    public void Send(int peerId, ReadOnlySpan<byte> payload, Delivery delivery)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Send(payload.ToArray(), delivery == Delivery.Reliable ? (byte)0 : (byte)1, Map(delivery));
        }
    }

    public void Broadcast(ReadOnlySpan<byte> payload, Delivery delivery, int exceptPeer = -1)
    {
        if (_peers.Count == 0)
        {
            return;
        }

        var bytes = payload.ToArray();
        foreach (var (id, peer) in _peers)
        {
            if (id != exceptPeer)
            {
                peer.Send(bytes, delivery == Delivery.Reliable ? (byte)0 : (byte)1, Map(delivery));
            }
        }
    }

    public void Kick(int peerId)
    {
        if (_peers.TryGetValue(peerId, out var p))
        {
            p.Disconnect();
        }
    }

    public IReadOnlyList<NetEvent> Poll()
    {
        _events.Clear();
        _manager.PollEvents();
        return _events;
    }

    public void Stop()
    {
        _manager.Stop();
        _peers.Clear();
    }

    public void Dispose() => Stop();
}
