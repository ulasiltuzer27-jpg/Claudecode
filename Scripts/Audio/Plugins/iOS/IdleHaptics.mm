// iOS dokunsal geri bildirim köprüsü.
// IOSHapticService.cs, IdleHaptics_Impact'i [DllImport("__Internal")] ile çağırır.
// Stil: 0 = hafif, 1 = orta, 2 = güçlü (UIImpactFeedbackStyle).
//
// Unity bu dosyayı iOS derlemesinde Xcode projesine ekler. Inspector'da
// "Select platforms for plugin" altında yalnızca iOS'un işaretli olduğunu
// doğrulayın.

#import <UIKit/UIKit.h>

// Üreteçler bir kez oluşturulup saklanıyor: her dokunuşta yeni üreteç
// kurmak ilk titreşimde gecikmeye yol açar.
static UIImpactFeedbackGenerator *idleHapticsLight = nil;
static UIImpactFeedbackGenerator *idleHapticsMedium = nil;
static UIImpactFeedbackGenerator *idleHapticsHeavy = nil;

static UIImpactFeedbackGenerator *IdleHapticsGeneratorFor(int style)
{
    if (style <= 0)
    {
        if (idleHapticsLight == nil)
        {
            idleHapticsLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        }
        return idleHapticsLight;
    }

    if (style == 1)
    {
        if (idleHapticsMedium == nil)
        {
            idleHapticsMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        }
        return idleHapticsMedium;
    }

    if (idleHapticsHeavy == nil)
    {
        idleHapticsHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    }
    return idleHapticsHeavy;
}

extern "C" void IdleHaptics_Impact(int style)
{
    UIImpactFeedbackGenerator *generator = IdleHapticsGeneratorFor(style);
    [generator impactOccurred];

    // Bir sonraki dokunuş gecikmesiz gelsin diye motoru birkaç saniye hazır tut.
    [generator prepare];
}
