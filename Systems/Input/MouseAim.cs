using Microsoft.Xna.Framework;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Fare ile nişan alma — sanal bir nişan imleci.
///
/// ── Neden bu sınıf VAR ──────────────────────────────────────────────────
/// "Fare hassasiyeti" ayarı istendi. Oyunda o an HİÇ fare girdisi yoktu
/// (bütün kod tabanında tek bir <c>Mouse</c> geçişi vardı, o da bir yorum
/// satırıydı). Hiçbir şeyi etkilemeyen bir hassasiyet çubuğu koymak,
/// ayarı olmamasından daha kötü olurdu: oyuncu çubuğu oynatır, oyun
/// değişmez, ayara bir daha güvenmez.
///
/// Bu yüzden hassasiyetin ölçtüğü şey de eklendi. Fare karakterin
/// baktığı yönü belirliyor; toplama, inşa ve saldırı hep o yöne gidiyor
/// (<see cref="PixelSurvival.Systems.Gathering.GatheringSystem"/> hedef
/// tile'ı yönden türetiyor).
///
/// ── Neden MUTLAK imleç konumu değil ─────────────────────────────────────
/// Masaüstü imlecinin ekrandaki yerine bakılsaydı "hassasiyet" diye bir
/// şey olmazdı: onu işletim sistemi belirler ve oyunun ayarı yalan söyler.
/// Burada farenin KARE BAŞINA HAREKETİ okunuyor ve bir çarpanla sanal
/// imlece uygulanıyor — hassasiyet o çarpan. FPS oyunlarının yaptığı şey.
///
/// ── İmleç neden bir yarıçapa sıkıştırılıyor ─────────────────────────────
/// Nişan bir YÖN; uzaklığın oynanışta karşılığı yok (menzil
/// <c>reachTiles</c>'dan geliyor). İmlecin serbest bırakılması, oyuncunun
/// onu ekranın dışına kaçırıp yönü kaybetmesi demekti. Yarıçapa
/// sıkıştırılınca imleç hep karakterin çevresinde ve hep görünür kalıyor.
/// </summary>
public sealed class MouseAim
{
    /// <summary>
    /// İmlecin karakter merkezinden en fazla uzaklığı (dünya pixel'i).
    ///
    /// 24 = 1.5 tile. Yönü okunur kılacak kadar uzak, ekranda karakterden
    /// kopmayacak kadar yakın.
    /// </summary>
    public const float Radius = 24f;

    /// <summary>
    /// Hassasiyet 1.0'da bir fare pixel'inin kaç dünya pixel'i ettiği.
    ///
    /// Tuval 2x ölçekle çizildiği için bir ekran pixel'i yarım dünya
    /// pixel'i eder; 0.5 bu yüzden "birebir" demek.
    /// </summary>
    private const float BaseScale = 0.5f;

    /// <summary>
    /// Fare bu süre boyunca hiç oynamazsa nişan bırakılır ve yön yeniden
    /// hareketten türer.
    ///
    /// Olmasaydı fareye bir kez dokunan oyuncu sonsuza dek o yöne bakardı
    /// ve klavyeyle oynamaya dönemezdi.
    /// </summary>
    private const float IdleSeconds = 2.5f;

    private Vector2 _offset;
    private float _idle = IdleSeconds;

    /// <summary>Nişan imlecinin karakter merkezine göre konumu.</summary>
    public Vector2 Offset => _offset;

    /// <summary>Fare yakın zamanda oynadı mı — nişan geçerli mi.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>Nişan yönü (birim vektör); nişan yoksa <c>null</c>.</summary>
    public Vector2? Direction =>
        IsAiming && _offset.LengthSquared() > 0.001f
            ? Vector2.Normalize(_offset)
            : null;

    /// <summary>
    /// Bir kareyi işler.
    /// </summary>
    /// <param name="delta">Geçen süre (saniye).</param>
    /// <param name="mouseDelta">Farenin bu karedeki hareketi (ekran pixel'i).</param>
    /// <param name="settings">Hassasiyet ve fare nişanının açık olup olmadığı.</param>
    public void Update(float delta, Vector2 mouseDelta, ControlSettings settings)
    {
        if (!settings.MouseAimEnabled)
        {
            IsAiming = false;
            _offset = Vector2.Zero;
            return;
        }

        if (mouseDelta != Vector2.Zero)
        {
            _offset += mouseDelta * BaseScale * settings.MouseSensitivity;

            // Yarıcapa sikistir: yon korunur, uzaklik kirpilir.
            var length = _offset.Length();
            if (length > Radius) _offset *= Radius / length;

            _idle = 0f;
            IsAiming = true;
        }
        else
        {
            _idle += delta;
            if (_idle >= IdleSeconds) IsAiming = false;
        }
    }

    /// <summary>Nişanı bırakır — menüye girerken ya da ölümde.</summary>
    public void Clear()
    {
        _offset = Vector2.Zero;
        _idle = IdleSeconds;
        IsAiming = false;
    }
}
