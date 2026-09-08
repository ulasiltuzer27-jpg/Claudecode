namespace PixelSurvival.Inventory.Steam;

/// <summary>Bir grant talebinin sonucu.</summary>
public enum GrantOutcome
{
    /// <summary>Backend item'ı verdi.</summary>
    Granted,

    /// <summary>Backend yapılandırılmamış — geliştirme durumu.</summary>
    NotConfigured,

    /// <summary>Backend talebi reddetti (doğrulama başarısız, hız sınırı vb.).</summary>
    Rejected
}

/// <summary>
/// AŞAMA 2 / MADDE 19 — değerli Steam item'ı verme yetkisi.
///
/// ════════════════════════════════════════════════════════════════════════
/// BU ARAYÜZ NEDEN VAR
/// ════════════════════════════════════════════════════════════════════════
/// Steamworks'te item üretmek (GenerateItems) bir Web API çağrısıdır ve
/// PUBLISHER / ECONOMY API KEY ister.
///
/// O anahtar HİÇBİR KOŞULDA oyun client'ına veya player-host'a gömülmez.
/// Gömülseydi, dünyayı açan herhangi bir oyuncu kendine sınırsız
/// marketable item basabilir ve ekonomiyi bitirebilirdi.
///
/// Bu yüzden grant yolu <see cref="SteamInventory"/> içinde DEĞİL, bu
/// arayüzün arkasında. Tek meşru implementasyonu, anahtarı kendi
/// tarafında tutan güvenilir bir backend'dir.
/// ════════════════════════════════════════════════════════════════════════
///
/// AKIŞ (uygulandığında):
///   1. Client, Steam'den bir oturum bileti alır (GetAuthSessionTicket).
///   2. Bileti ve talebi backend'e gönderir.
///   3. Backend bileti Steam ile DOĞRULAR (AuthenticateUserTicket).
///   4. Backend, talebin oyun kurallarına uyduğunu KENDİ kayıtlarından
///      doğrular — client'ın gönderdiği "ben bunu hak ettim" iddiasına
///      güvenmez.
///   5. Backend, publisher key ile Web API üzerinden item'ı verir.
///
/// 4. adım kritik: client'ın raporuna güvenilirse anahtarı backend'de
/// tutmanın hiçbir anlamı kalmaz.
/// </summary>
public interface ISteamItemGrantAuthority
{
    /// <summary>Bu yetki şu an kullanılabilir mi.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Bir item talebini güvenilir tarafa iletir.
    /// </summary>
    /// <param name="defId">Talep edilen ItemDef.</param>
    /// <param name="reason">Backend'in doğrulayacağı gerekçe anahtarı (örn. "boss_first_kill").</param>
    GrantOutcome RequestGrant(SteamItemDefId defId, string reason);
}

/// <summary>
/// Henüz kurulmamış backend'in yer tutucusu.
///
/// KASTEN HİÇBİR ŞEY YAPMAZ. "Şimdilik client'tan verelim, sonra backend
/// yazarız" yolu bilerek kapalı: o geçici çözüm kalıcı olur ve ekonomiyi
/// açık bırakır. Backend hazır olana kadar değerli item verilmiyor.
/// </summary>
public sealed class UnconfiguredGrantAuthority : ISteamItemGrantAuthority
{
    public bool IsConfigured => false;

    public GrantOutcome RequestGrant(SteamItemDefId defId, string reason)
    {
        // Talep sessizce yutulmaz; günlüğe düşer ki backend yazılırken
        // hangi grant noktalarının beklediği görülebilsin.
        Console.WriteLine(
            $"[steam-grant] ATLANDI {defId} sebep='{reason}' — " +
            $"guvenilir backend yapilandirilmamis. " +
            $"Publisher key client'a GOMULMEZ; grant backend'den gelmeli.");

        return GrantOutcome.NotConfigured;
    }
}
