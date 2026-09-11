using Microsoft.Xna.Framework;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Bir tick'lik input'un anlık görüntüsü.
///
/// Neden ayrı bir struct?
/// Player doğrudan Keyboard/GamePad okumaz; kendisine verilen bu snapshot'a göre
/// hareket eder. Bu ayrım ileride (madde 10) client-side prediction'ın dayandığı
/// noktadır: aynı input snapshot'ı ağdan gönderilip host'ta yeniden oynatılabilir.
///
/// Şimdilik ağ kodu YOK — burada yapılan tek şey doğru dikişi baştan atmak.
/// </summary>
public readonly struct PlayerInput
{
    /// <summary>
    /// Hareket yönü. Uzunluğu 0..1 arası.
    /// Klavyede her zaman 0 veya 1; analog stick'te kısmi eğim (yavaş yürüme) korunur.
    /// </summary>
    public readonly Vector2 Move;

    /// <summary>
    /// Toplama tuşu BASILI TUTULUYOR mu. Anlık basış değil basılı tutma:
    /// toplama süre gerektiriyor, tuşa spam yapmak avantaj sağlamamalı.
    /// </summary>
    public readonly bool Gather;

    /// <summary>Saldırı tuşu basılı tutuluyor mu (madde 11).</summary>
    public readonly bool Attack;

    /// <summary>İnşa tuşuna bu karede BASILDI mı — kenar tespitli (madde 9).</summary>
    public readonly bool Build;

    /// <summary>
    /// Nişan yönü (birim vektör) — fare nişan alıyorsa dolu, yoksa <c>null</c>.
    ///
    /// ── Neden hareketten AYRI bir alan ──────────────────────────────────
    /// Karakterin baktığı yön şimdiye kadar hep gittiği yöndü. Fare ile
    /// nişan alınca ikisi AYRIŞIYOR: oyuncu sağa koşup sola vurabilmeli.
    /// Toplama, inşa ve saldırı hedeflerini bakılan yönden türettiği için
    /// bu tek alan üçünü birden fareye bağlıyor — hiçbirinin kodu
    /// değişmeden.
    ///
    /// <c>null</c> olması "nişan yok" demek, "sıfır yön" değil: fare
    /// kullanılmadığında yön yine hareketten türemeli.
    /// </summary>
    public readonly Vector2? Aim;

    public PlayerInput(Vector2 move, bool gather = false, bool attack = false,
                       bool build = false, Vector2? aim = null)
    {
        Move = move;
        Gather = gather;
        Attack = attack;
        Build = build;
        Aim = aim;
    }

    /// <summary>Ölü bölgeden büyük bir hareket girdisi var mı.</summary>
    public bool IsMoving => Move.LengthSquared() > 0f;

    public static PlayerInput None => new(Vector2.Zero);
}
