namespace PixelSurvival.Networking;

/// <summary>
/// AŞAMA 2 / MADDE 19 — taşıma seçiminin TEK noktası.
///
/// ════════════════════════════════════════════════════════════════════════
/// NEDEN BURASI VAR
/// ════════════════════════════════════════════════════════════════════════
/// Spec'in kuralı: "iki networking teknolojisi aynı anda karışık
/// kullanılmaz, gameplay kodu hangi transport'un aktif olduğundan habersiz
/// çalışır."
///
/// <see cref="NetworkSession"/> daha önce doğrudan
/// <c>new LiteNetLibTransport()</c> yazıyordu. Bu, oturum yönetiminin
/// somut bir kütüphaneyi tanıması demekti ve Steam'e geçiş oturum kodunu
/// düzenlemeyi gerektirirdi.
///
/// Artık seçim yalnızca burada, derleme sembolüyle yapılıyor. Steam
/// derlemesine geçmek için değiştirilen tek şey derleme yapılandırması:
///
///     dotnet build                    -> LiteNetLibTransport
///     dotnet build -c SteamRelease    -> SteamNetworkingTransport
/// ════════════════════════════════════════════════════════════════════════
/// </summary>
public static class TransportFactory
{
    /// <summary>Bu derleme için uygun taşımayı örnekler.</summary>
    public static INetworkTransport Create() =>
#if STEAM_BUILD
        new SteamNetworkingTransport();
#else
        new LiteNetLibTransport();
#endif

    /// <summary>Aktif taşımanın adı — HUD ve günlük için.</summary>
    public static string Name =>
#if STEAM_BUILD
        "Steam (ISteamNetworkingSockets)";
#else
        "LiteNetLib (gelistirme)";
#endif

    /// <summary>
    /// "Bağlan" için varsayılan hedef.
    ///
    /// LiteNetLib'de bir IP/host adı, Steam'de host'un SteamID'si beklenir.
    /// Steam derlemesinde boş dönülüyor: hedef, arkadaş listesinden gelen
    /// davetle belirlenir (madde 21), elle yazılmaz.
    /// </summary>
    public static string DefaultConnectTarget =>
#if STEAM_BUILD
        "";
#else
        "localhost";
#endif
}
