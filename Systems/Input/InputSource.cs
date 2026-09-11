using Microsoft.Xna.Framework.Input;

namespace PixelSurvival.Systems.Input;

/// <summary>
/// Klavye durumunun TEK okuma noktası.
///
/// Neden var? Oyunun "gerçekten çalıştığı" ancak açılıp sürülerek
/// kanıtlanabiliyor, ama bir CI makinesinde kimse tuşa basamaz. Bu sınıf
/// klavyeyi bir seviye soyutlayarak <see cref="PixelSurvival.Diagnostics.CaptureHarness"/>
/// gibi otomatik sürücülerin sanal tuş durumu beslemesine izin verir.
///
/// Oyun kodu farkı görmez: <see cref="GetKeyboard"/> normalde doğrudan
/// <see cref="Keyboard.GetState"/> döndürür. Yalnızca bir yakalama script'i
/// aktifken araya giren bir kaynak devreye girer.
///
/// KURAL: Oyun kodunda başka hiçbir yerde <c>Keyboard.GetState()</c>
/// çağrılmaz — çağrılırsa o giriş otomasyona kapalı kalır.
/// </summary>
public static class InputSource
{
    /// <summary>
    /// Sanal klavye kaynağı. <c>null</c> ise gerçek donanım okunur.
    /// Yalnızca teşhis/otomasyon katmanı doldurur.
    /// </summary>
    public static Func<KeyboardState>? Override { get; set; }

    /// <summary>Bu tick'in klavye durumu.</summary>
    public static KeyboardState GetKeyboard() => Override?.Invoke() ?? Keyboard.GetState();

    /// <summary>
    /// Otomasyon aktif mi — yani girdiler gerçek donanımdan GELMİYOR.
    ///
    /// Fare nişanı bunu bilmek zorunda: gerçek fareyle çalışırken imleç
    /// her karede pencerenin ortasına geri çekiliyor (yoksa imleç pencere
    /// kenarına dayanır ve hareket ölçülemez olur). Bir yakalama
    /// koşumunda bunu yapmak, koşan makinenin imlecini kaçırmak ve
    /// ölçümü belirsizleştirmek olurdu.
    /// </summary>
    public static bool IsAutomated => Override is not null;

    /// <summary>
    /// Farenin bu karedeki hareketi (ekran pixel'i).
    ///
    /// Otomasyon altında HER ZAMAN sıfır: yakalama script'leri yalnızca
    /// klavye besliyor ve fare girdisi belirsiz olsaydı aynı script iki
    /// koşumda farklı sonuç verirdi. Doğrulama koşumlarının
    /// tekrarlanabilirliği, fare girdisini sınamaktan daha değerli —
    /// fare nişanının kuralları <c>--self-test</c>'te ayrıca sınanıyor.
    /// </summary>
    public static Microsoft.Xna.Framework.Vector2 ReadMouseDelta(
        int windowWidth, int windowHeight)
    {
        if (IsAutomated) return Microsoft.Xna.Framework.Vector2.Zero;

        var state = Mouse.GetState();
        var centerX = windowWidth / 2;
        var centerY = windowHeight / 2;

        var delta = new Microsoft.Xna.Framework.Vector2(
            state.X - centerX, state.Y - centerY);

        // Imleci ortaya geri cek ki bir sonraki karede olculen sey yine
        // SAF hareket olsun. Bu yapilmazsa imlec pencere kenarina dayanir
        // ve o yonde nisan alinamaz hale gelir.
        Mouse.SetPosition(centerX, centerY);

        return delta;
    }
}
