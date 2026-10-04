namespace PilavciSimulator.Net;

public enum NetEventType
{
    Connected,
    Disconnected,
    Data,
}

public readonly record struct NetEvent(NetEventType Type, int PeerId, byte[] Payload, string Reason = "");

public enum Delivery
{
    Reliable,
    Unreliable,
}

/// <summary>
/// Ag tasima soyutlamasi. Oyun kodu LiteNetLib'i de Steamworks'u de gormez.
/// Kalip PixelSurvival'daki Networking/INetworkTransport.cs'den alindi;
/// teslim modu (guvenilir/guvenilmez) eklendi.
///
///   - LiteNetLibTransport: dogrudan IP (gelistirme, LAN)
///   - SteamTransport: Steam relay agi (SteamRelease), NAT sorunu yok
/// </summary>
public interface INetTransport : IDisposable
{
    bool IsRunning { get; }
    int PeerCount { get; }
    /// <summary>Gidis-donus suresi (ms), istemcide host'a; bilinmiyorsa -1.</summary>
    int PingMs { get; }

    void StartHost(int port);
    void Connect(string address, int port);
    void Send(int peerId, ReadOnlySpan<byte> payload, Delivery delivery);
    void Broadcast(ReadOnlySpan<byte> payload, Delivery delivery, int exceptPeer = -1);
    void Kick(int peerId);
    /// <summary>Biriken olaylar. Her kare cagrilmali.</summary>
    IReadOnlyList<NetEvent> Poll();
    void Stop();
}
