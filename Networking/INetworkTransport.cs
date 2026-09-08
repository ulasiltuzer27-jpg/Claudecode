namespace PixelSurvival.Networking;

/// <summary>Bağlantı olaylarının türü.</summary>
public enum NetworkEventType
{
    Connected,
    Disconnected,
    Data
}

/// <summary>
/// <see cref="INetworkTransport.PollEvents"/>'ten dönen tek bir olay.
/// </summary>
/// <param name="Type">Olay türü.</param>
/// <param name="PeerId">Olayın sahibi bağlantı. Host tarafında istemciyi tanımlar.</param>
/// <param name="Payload">
/// Data olaylarında mesajın baytları. Diğer türlerde boş.
/// Dizi ÇAĞRIDAN SONRA geçersizdir — saklanacaksa kopyalanmalı.
/// </param>
public readonly record struct NetworkEvent(
    NetworkEventType Type,
    int PeerId,
    ReadOnlyMemory<byte> Payload);

/// <summary>
/// AŞAMA 1 / MADDE 10 — ağ taşıma katmanı soyutlaması.
///
/// ════════════════════════════════════════════════════════════════════════
/// NEDEN BU ARAYÜZ VAR
/// ════════════════════════════════════════════════════════════════════════
/// Gameplay kodu LiteNetLib'i de Steamworks'ü de ASLA doğrudan görmez.
/// İki taşıma teknolojisi aynı anda karışık kullanılmaz; hangisinin aktif
/// olduğunu üst katman bilmez.
///
///   - Geliştirme/test:  LiteNetLibTransport      (madde 10, şimdi)
///   - Steam build'i:    SteamNetworkingTransport (madde 19, sonra)
///
/// Steam tarafı ISteamNetworkingSockets üzerinden yazılacak.
/// Eski ISteamNetworking API'si deprecated — KULLANILMAYACAK.
/// NAT traversal ve Valve relay ağı bu şekilde bedavaya gelir.
///
/// ── İleride genişleyecek, ŞİMDİ DEĞİL ───────────────────────────────────
/// <see cref="Send"/> ileride bir teslim modu (Reliable/Unreliable) parametresi
/// alacak şekilde tasarlandı: pozisyon güncellemeleri unreliable, saldırı ve
/// envanter işlemleri reliable gitmeli. Bu AŞAMADA KODLANMADI — arayüz
/// genişletilebilir bırakıldı, o kadar. Şu an her şey güvenilir gider.
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public interface INetworkTransport : IDisposable
{
    /// <summary>Bu taşıma şu an bir oturum yürütüyor mu.</summary>
    bool IsRunning { get; }

    /// <summary>Bağlı karşı taraf sayısı (host'ta istemci sayısı).</summary>
    int PeerCount { get; }

    /// <summary>Sunucu rolünü başlatır. Dünyayı açan oyuncunun makinesi bunu çağırır.</summary>
    void StartHost(int port);

    /// <summary>Bir host'a bağlanır.</summary>
    void Connect(string address, int port);

    /// <summary>
    /// Tek bir karşı tarafa veri gönderir.
    /// <paramref name="peerId"/> <see cref="Broadcast"/> için değil, hedefli gönderim içindir.
    /// </summary>
    void Send(int peerId, ReadOnlySpan<byte> payload);

    /// <summary>Bağlı herkese gönderir.</summary>
    void Broadcast(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Biriken ağ olaylarını döndürür. Her karede çağrılmalı.
    /// Dönen olayların <c>Payload</c>'ları bir sonraki çağrıya kadar geçerlidir.
    /// </summary>
    IReadOnlyList<NetworkEvent> PollEvents();

    /// <summary>Oturumu kapatır ve kaynakları bırakır.</summary>
    void Stop();
}
