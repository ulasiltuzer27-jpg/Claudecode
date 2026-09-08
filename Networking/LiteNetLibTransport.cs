using LiteNetLib;

namespace PixelSurvival.Networking;

/// <summary>
/// AŞAMA 1 / MADDE 10 — geliştirme/test taşıması.
///
/// LiteNetLib'e dokunan TEK dosya burasıdır. Gameplay kodu bu sınıfı değil
/// <see cref="INetworkTransport"/> arayüzünü görür; madde 19'da
/// SteamNetworkingTransport eklendiğinde tek satır gameplay kodu değişmez.
///
/// Şu an her mesaj güvenilir ve sıralı gider. Teslim modu ayrımı
/// (pozisyon=unreliable, saldırı=reliable) arayüzde İLERİDE eklenecek —
/// bu aşamada kodlanmadı.
/// </summary>
public sealed class LiteNetLibTransport : INetworkTransport
{
    /// <summary>Yanlış uygulamaların birbirine bağlanmasını engeller.</summary>
    private const string ConnectionKey = "pixelsurvival";

    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _manager;
    private readonly List<NetworkEvent> _events = [];

    /// <summary>
    /// Bağlı peer'lar. NetManager'ın kimlikten peer bulma yardımcısına
    /// güvenmek yerine kendi sözlüğümüzü tutuyoruz: kütüphane sürümleri
    /// arasında farklılık gösteren bir API'ye bağımlılık azalıyor.
    /// </summary>
    private readonly Dictionary<int, NetPeer> _peers = [];

    /// <summary>
    /// Payload'lar bir sonraki PollEvents'e kadar yaşamalı. LiteNetLib'in
    /// verdiği okuyucu çağrı biter bitmez geri dönüştürülüyor, o yüzden
    /// baytlar kopyalanıyor.
    /// </summary>
    private readonly List<byte[]> _buffers = [];

    public bool IsRunning => _manager.IsRunning;
    public int PeerCount => _peers.Count;

    public LiteNetLibTransport()
    {
        _manager = new NetManager(_listener)
        {
            // Olaylar arka planda değil, PollEvents çağrıldığında işlensin:
            // gameplay durumu tek iş parçacığından değişsin.
            UnsyncedEvents = false,
            AutoRecycle = true
        };

        _listener.ConnectionRequestEvent += request => request.AcceptIfKey(ConnectionKey);

        _listener.PeerConnectedEvent += peer =>
        {
            _peers[peer.Id] = peer;
            _events.Add(new NetworkEvent(NetworkEventType.Connected, peer.Id, default));
        };

        _listener.PeerDisconnectedEvent += (peer, _) =>
        {
            _peers.Remove(peer.Id);
            _events.Add(new NetworkEvent(NetworkEventType.Disconnected, peer.Id, default));
        };

        _listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            var copy = reader.GetRemainingBytes();
            _buffers.Add(copy);
            _events.Add(new NetworkEvent(NetworkEventType.Data, peer.Id, copy));
        };
    }

    public void StartHost(int port)
    {
        if (!_manager.Start(port))
        {
            throw new InvalidOperationException(
                $"{port} portu dinlenemedi. Başka bir örnek zaten çalışıyor olabilir.");
        }
    }

    public void Connect(string address, int port)
    {
        // İstemci de bir soket açar; 0 = işletim sistemi boş port seçsin.
        // Aynı makinede iki örnek çalıştırabilmek için şart.
        if (!_manager.Start(0))
        {
            throw new InvalidOperationException("İstemci soketi açılamadı.");
        }

        _manager.Connect(address, port, ConnectionKey);
    }

    public void Send(int peerId, ReadOnlySpan<byte> payload)
    {
        if (_peers.TryGetValue(peerId, out var peer))
        {
            peer.Send(payload.ToArray(), DeliveryMethod.ReliableOrdered);
        }
    }

    public void Broadcast(ReadOnlySpan<byte> payload)
    {
        if (_peers.Count == 0)
        {
            return;
        }

        var bytes = payload.ToArray();
        foreach (var peer in _peers.Values)
        {
            peer.Send(bytes, DeliveryMethod.ReliableOrdered);
        }
    }

    public IReadOnlyList<NetworkEvent> PollEvents()
    {
        // Önceki turun tamponları artık serbest.
        _buffers.Clear();
        _events.Clear();

        _manager.PollEvents();
        return _events;
    }

    public void Stop()
    {
        _manager.Stop();
        _peers.Clear();
        _events.Clear();
        _buffers.Clear();
    }

    public void Dispose() => Stop();
}
