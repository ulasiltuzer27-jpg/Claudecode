namespace PixelSurvival.Networking;

/// <summary>
/// AŞAMA 2 / MADDE 21 — arkadaş daveti ve rich presence.
///
/// ── Davet nasıl çalışır ─────────────────────────────────────────────────
/// Steam'de davet iki yoldan gelir:
///   1. Oyuncu overlay'den arkadaşını davet eder (<c>ActivateGameOverlay</c>
///      + "invite" sayfası).
///   2. Arkadaş Steam arayüzünden "Katıl" der; Steam oyunu
///      <c>+connect_lobby</c> / <c>connect</c> parametresiyle başlatır ya da
///      çalışan oyuna <c>SteamFriends.OnGameRichPresenceJoinRequested</c>
///      olayı düşer.
///
/// İkisi de aynı "connect string"e dayanır. Bu yüzden rich presence'a
/// <c>connect</c> anahtarı yazılır: değeri host'un SteamID'sidir ve
/// <see cref="SteamNetworkingTransport"/> tam olarak bunu bekliyor.
/// Adres biçimini iki yerde ayrı tanımlamak, davetin sessizce çalışmadığı
/// klasik hatadır.
///
/// ── Kapsam ──────────────────────────────────────────────────────────────
/// Bu sınıf yalnızca DAVET yolunu kurar. Bağlantının kendisi
/// <see cref="INetworkTransport"/> işidir ve değişmez.
/// </summary>
public sealed class SteamFriendsService
{
    /// <summary>
    /// Davet kabul edildiğinde çağrılır; parametre bağlanılacak adres
    /// (host'un SteamID'si). Oyun kabuğu bunu <c>Connect</c>'e verir.
    /// </summary>
    public event Action<string>? JoinRequested;

#if STEAM_BUILD
    public bool IsAvailable => Steamworks.SteamClient.IsValid;

    public string Status =>
        Localization.Loc.T(IsAvailable ? "steam.friendsReady" : "steam.notRunning");

    /// <summary>Yerel oyuncunun Steam profil adı.</summary>
    public string LocalPlayerName => IsAvailable ? Steamworks.SteamClient.Name : "Sen";

    /// <summary>Yerel oyuncunun SteamID'si — host olurken yayınlanan adres.</summary>
    public ulong LocalSteamId => IsAvailable ? Steamworks.SteamClient.SteamId : 0UL;

    /// <summary>
    /// Davet olayını bağlar. Oyun açılışında BİR KEZ çağrılmalı; bağlanmazsa
    /// "Katıl" diyen arkadaş sessizce hiçbir şey yaşamaz.
    ///
    /// Steamworks.NET'te bunun için bir <c>Callback&lt;T&gt;</c> nesnesi
    /// oluşturulup ALANDA TUTULMASI gerekiyordu — referans düşerse çöp
    /// toplayıcı onu alır ve davet sessizce çalışmaz olurdu. Facepunch aynı
    /// şeyi normal bir C# olayı olarak veriyor; saklanacak bir tutamak yok.
    /// </summary>
    public void Initialize()
    {
        if (!IsAvailable) return;

        Steamworks.SteamFriends.OnGameRichPresenceJoinRequested += OnJoinRequested;
    }

    /// <summary>Olay aboneliğini bırakır — iki kez abone olunmasın.</summary>
    public void Shutdown()
    {
        if (!IsAvailable) return;

        Steamworks.SteamFriends.OnGameRichPresenceJoinRequested -= OnJoinRequested;
    }

    private void OnJoinRequested(Steamworks.Friend friend, string connectString)
    {
        _ = friend;
        JoinRequested?.Invoke(connectString);
    }

    /// <summary>
    /// Oyuncu bir dünya açtığında çağrılır: arkadaşlar "Katıl" görebilsin.
    /// </summary>
    public void PublishHosting(ulong hostSteamId)
    {
        if (!IsAvailable) return;

        // 'connect' Steam'in ozel anahtaridir: dolu oldugunda arkadas
        // listesinde "Katil" dugmesi belirir. Deger, transport'un
        // bekledigi adresin AYNISI olmali.
        Steamworks.SteamFriends.SetRichPresence("connect", hostSteamId.ToString());
        Steamworks.SteamFriends.SetRichPresence("status", "Dunyasini paylasiyor");
    }

    /// <summary>Oyuncu oturumdan ayrıldığında "Katıl" düğmesini kaldırır.</summary>
    public void ClearHosting()
    {
        if (!IsAvailable) return;

        Steamworks.SteamFriends.ClearRichPresence();
    }

    /// <summary>Steam overlay'inin davet ekranını açar.</summary>
    public bool OpenInviteOverlay()
    {
        if (!IsAvailable) return false;

        // Facepunch davet ekranini DOGRUDAN aciyor: Steamworks.NET'te
        // "friends" sayfasi acilip oyuncunun davet sekmesini kendisi
        // bulmasi gerekiyordu.
        Steamworks.SteamFriends.OpenGameInviteOverlay(Steamworks.SteamClient.SteamId);
        return true;
    }
#else
    // Steam'siz derleme: sinif var, davet yolu kapali. Cagiran taraf
    // #if ile dallanmiyor; yalnizca IsAvailable false donuyor.
    public bool IsAvailable => false;
    public string Status => Localization.Loc.T("steam.notSteamBuild");
    public string LocalPlayerName => "Sen";

    public ulong LocalSteamId => 0UL;

    public void Initialize() { }
    public void Shutdown() { }
    public void PublishHosting(ulong hostSteamId) { _ = hostSteamId; }
    public void ClearHosting() { }
    public bool OpenInviteOverlay() => false;
#endif

    /// <summary>
    /// Davet olayını dışarıdan tetikler. Yalnızca otomatik doğrulama içindir:
    /// Steam istemcisi olmadan "davet kabul edildi" yolunun çalıştığı
    /// gösterilebilsin diye. Gerçek daveti Steam callback'i üretir.
    /// </summary>
    internal void SimulateJoinRequest(string address) => JoinRequested?.Invoke(address);
}
