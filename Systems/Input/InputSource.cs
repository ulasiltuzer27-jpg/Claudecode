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
}
