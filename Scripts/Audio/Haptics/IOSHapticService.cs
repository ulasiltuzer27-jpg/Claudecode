#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace IdleRestaurant.Audio.Haptics
{
    /// <summary>
    /// iOS titreşimi, UIImpactFeedbackGenerator ile (Taptic Engine).
    ///
    /// Unity'nin kendi Handheld.Vibrate'i iOS'ta yalnızca uzun ve sert bir
    /// titreşim verir; hafif/orta "tık" için native kod gerekiyor. Karşılığı
    /// <c>Plugins/iOS/IdleHaptics.mm</c>; Unity onu Xcode projesine ekler.
    /// Taptic Engine'i olmayan cihazlarda (bazı iPad'ler) sessizce hiçbir şey olmaz.
    /// </summary>
    internal sealed class IOSHapticService : IHapticService
    {
        [DllImport("__Internal")]
        private static extern void IdleHaptics_Impact(int style);

        public bool IsSupported => true;

        public void Play(HapticType type)
        {
            switch (type)
            {
                case HapticType.Light:
                    IdleHaptics_Impact(0);
                    break;
                case HapticType.Medium:
                    IdleHaptics_Impact(1);
                    break;
                case HapticType.Heavy:
                    IdleHaptics_Impact(2);
                    break;
            }
        }
    }
}
#endif
