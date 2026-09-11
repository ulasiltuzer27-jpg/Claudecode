using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PixelSurvival.Systems;

/// <summary>
/// Düşük çözünürlüklü sanal tuval — pixel art'ın keskinliğini veren katman.
///
/// ── Sorun ───────────────────────────────────────────────────────────────
/// Dünya doğrudan 1280x720 arka tampona, kamera matrisinde 3x ölçekle
/// çiziliyordu. <see cref="SamplerState.PointClamp"/> texel'leri keskin
/// tutuyordu ama YETMİYORDU: karakterler, düşmanlar ve yaratıklar kesirli
/// dünya konumlarında duruyor (hız × kare süresi tam sayı vermez), dolayısıyla
/// ekranda da kesirli konuma düşüyorlardı. Sonuç, hareket hâlindeki her
/// sprite'ın kenarında bir pixel'lik titreme — pixel art'ta en çok göze
/// batan kusur.
///
/// ── Çözüm ───────────────────────────────────────────────────────────────
/// Dünya önce <see cref="BaseWidth"/>x<see cref="BaseHeight"/> bir dokuya
/// çiziliyor. O dokunun içinde bir dünya pixel'i BİR texel: kesirli konumlar
/// tuval ızgarasına oturuyor ve alt-pixel kayması yapısal olarak imkânsız
/// hâle geliyor. Doku sonra ekrana TAM SAYI katıyla büyütülüyor.
///
/// ── Neden tam sayı ölçek ────────────────────────────────────────────────
/// 1280/640 = 2 tam. Ölçek 2.5 olsaydı bazı tuval pixel'leri ekranda 2,
/// bazıları 3 pixel genişlik kaplardı: aynı sprite'ın içinde kalın ve ince
/// pixel'ler karışırdı. Bu, keskinleştirmek için yapılan işi tam tersine
/// çevirir. Bedeli kenarlarda letterbox şeridi — <see cref="Destination"/>
/// ortalanıyor ve artan yer temiz siyah kalıyor.
///
/// ── Arayüz neden İÇERİDE değil ──────────────────────────────────────────
/// Arayüz bu tuvale DEĞİL, ekranın kendi çözünürlüğüne çiziliyor. Sebep:
/// 640x360'lık bir tuvalde 5x7 bitmap font'un okunabilir kalması için
/// ölçeğin 1 olması gerekir, o da ekranda 2x büyütülünce mevcut görünümün
/// iki katı büyüklükte yazı demek — paneller taşardı (bu proje o taşmayı
/// bir kez yaşadı, bkz. README "yazı ölçeği"). Ayrıca yazının ekranın tam
/// çözünürlüğünde çizilmesi onu DAHA keskin yapıyor, daha bulanık değil.
/// Aynı ayrımı Celeste de yapıyor: oynanış 320x180, arayüz tam çözünürlük.
///
/// ── Bilinen tek istisna ─────────────────────────────────────────────────
/// Photo mode'un (madde 24) yakınlaştırması SÜREKLİ: oyuncu elle zoom
/// yaptığında kamera ölçeği 1.7 gibi kesirli bir değere gidiyor ve o
/// karede tuvalin içinde alt-pixel kayması geri geliyor. Varsayılan
/// photo mode ölçeği 1 olduğu için normal kullanımda görünmüyor;
/// tamamen kapatmak isteyen, ölçeği tam sayıya oturtan bir adım
/// eklemeli.
/// </summary>
public sealed class PixelCanvas : IDisposable
{
    /// <summary>
    /// Tuvalin genişliği.
    ///
    /// ── Neden 640x360 ───────────────────────────────────────────────────
    /// Yaygın 16:9 çözünürlüklerin HEPSİNE tam sayı katıyla oturuyor:
    /// 1280x720 = 2x, 1920x1080 = 3x, 2560x1440 = 4x, 3840x2160 = 6x.
    /// Hiçbirinde letterbox yok. Tam sayı ölçek şartı olduğu için bu
    /// özellik, taban çözünürlüğü seçerken en ağır basan ölçüt.
    ///
    /// ── Bedeli: kadraj değişti ──────────────────────────────────────────
    /// Eskiden 1280x720'a 3x kamera ölçeğiyle çiziliyordu, yani görünen
    /// alan 427x240 dünya pixel'i (≈26x15 tile) ve bir tile ekranda 48
    /// pixel'di. Şimdi görünen alan 640x360 (40x22.5 tile) ve tile 32
    /// ekran pixel'i: daha geniş bir alan, daha küçük sprite'lar.
    ///
    /// Eski kadrajı BİREBİR isteyen 426x240 yapabilir — 1280x720'da tam
    /// 3x olur (1278x720, iki yanda 1'er pixel şerit). Ama 1920x1080'de
    /// ölçek 4'te kalır (1704x960) ve kenarlarda kalın siyah şeritler
    /// çıkar. Tek bir monitör hedefleniyorsa 426x240, mağazaya çıkılacaksa
    /// 640x360 doğru tercih.
    /// </summary>
    public const int BaseWidth = 640;

    public const int BaseHeight = 360;

    private readonly GraphicsDevice _device;

    private RenderTarget2D _target;

    /// <summary>Tuvalin ekrandaki tam sayı büyütme katı.</summary>
    public int Scale { get; private set; } = 1;

    /// <summary>Tuvalin ekranda kapladığı, ortalanmış dikdörtgen.</summary>
    public Rectangle Destination { get; private set; }

    public PixelCanvas(GraphicsDevice device)
    {
        _device = device;

        // PreserveContents DEGIL: her kare tuval bastan cizildigi icin
        // eski icerigi saklamanin maliyeti bosuna. Depth/stencil de yok —
        // 2D siralama cizim sirasindan geliyor.
        _target = new RenderTarget2D(device, BaseWidth, BaseHeight);

        Refresh();
    }

    /// <summary>Çizilecek doku — teşhis görünümleri de buna bakabilir.</summary>
    public RenderTarget2D Target => _target;

    /// <summary>
    /// Ekran boyutuna göre ölçeği ve hedef dikdörtgeni yeniden hesaplar.
    ///
    /// Her karede çağrılıyor: pencere boyutu değişebilir (yeniden boyutlandırma,
    /// tam ekrana geçiş) ve bunu bir olaya bağlamak, olayın atlandığı
    /// durumlarda tuvalin yanlış yere çizilmesi demek olurdu. Hesap birkaç
    /// tam sayı işlemi, her karede yapılması serbest.
    /// </summary>
    public void Refresh()
    {
        var screenWidth = _device.PresentationParameters.BackBufferWidth;
        var screenHeight = _device.PresentationParameters.BackBufferHeight;

        // En az 1: pencere tuvalden kucukse bile cizim kaybolmamali.
        Scale = Math.Max(1, Math.Min(screenWidth / BaseWidth, screenHeight / BaseHeight));

        var width = BaseWidth * Scale;
        var height = BaseHeight * Scale;

        Destination = new Rectangle(
            (screenWidth - width) / 2,
            (screenHeight - height) / 2,
            width,
            height);
    }

    /// <summary>Çizimi tuvale yönlendirir.</summary>
    public void Begin()
    {
        _device.SetRenderTarget(_target);
        _device.Clear(ClearColor);
    }

    /// <summary>Tuvalin altında kalan renk — dünya her yeri kaplamazsa görünür.</summary>
    public static readonly Color ClearColor = new(28, 30, 40);

    /// <summary>Çizimi tekrar ekrana yönlendirir.</summary>
    public void End() => _device.SetRenderTarget(null);

    /// <summary>
    /// Tuvali ekrana büyüterek çizer.
    ///
    /// Kendi <c>Begin/End</c> çiftini açıyor: büyütmenin
    /// <see cref="SamplerState.PointClamp"/> ile yapılması ŞART ve bunu
    /// çağıran tarafın hatırlamasına bırakmak, bir gün birinin unutup
    /// bütün ekranı bulanıklaştırması demekti.
    /// </summary>
    public void Present(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_target, Destination, Color.White);
        spriteBatch.End();
    }

    /// <summary>
    /// Ekran koordinatını tuval koordinatına çevirir.
    ///
    /// Şu an fare kullanılmıyor ama tuval eklendiği anda ekran ve dünya
    /// koordinatları AYRIŞIYOR; dönüşümün yeri belli olmazsa ilk fare
    /// kodu sessizce yanlış yere tıklar.
    /// </summary>
    public Vector2 ScreenToCanvas(Point screenPosition) => new(
        (screenPosition.X - Destination.X) / (float)Scale,
        (screenPosition.Y - Destination.Y) / (float)Scale);

    public void Dispose()
    {
        _target.Dispose();
        _target = null!;
    }
}
