using UnityEngine;

namespace IdleRestaurant.Audio.Haptics
{
    /// <summary>
    /// Platforma özgü titreşim uygulaması. Oyun kodu yalnızca bu arayüzü
    /// görür; Android/iOS ayrıntısı <see cref="HapticServiceFactory"/>'nin
    /// seçtiği sınıfta kalır.
    /// </summary>
    public interface IHapticService
    {
        /// <summary>Cihaz bu servisle titreşim verebiliyor mu.</summary>
        bool IsSupported { get; }

        /// <summary>Titreşimi tetikler. Desteklenmiyorsa veya <see cref="HapticType.None"/> ise hiçbir şey yapmaz; asla istisna atmaz.</summary>
        void Play(HapticType type);
    }

    /// <summary>
    /// Editör ve masaüstü için titreşimsiz uygulama. İstenirse istekleri
    /// Console'a yazar; böylece titreşimin doğru anda tetiklendiği cihaz
    /// olmadan da görülebilir.
    /// </summary>
    public sealed class NullHapticService : IHapticService
    {
        private readonly bool _log;

        public NullHapticService(bool log)
        {
            _log = log;
        }

        public bool IsSupported => false;

        public void Play(HapticType type)
        {
            if (_log && type != HapticType.None)
            {
                Debug.Log($"[Haptic] {type}");
            }
        }
    }

    /// <summary>
    /// Derlenen platforma uygun titreşim servisini seçer. Platform sınıfları
    /// yalnızca kendi platformlarında ve editör DIŞINDA derlenir; editörde
    /// JNI veya native çağrı hiç yapılmaz.
    /// </summary>
    public static class HapticServiceFactory
    {
        /// <param name="logWhenUnsupported">Editör/masaüstünde istekleri Console'a yaz.</param>
        public static IHapticService Create(bool logWhenUnsupported)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidHapticService android = new AndroidHapticService();
            if (android.IsSupported)
            {
                return android;
            }

            android.Dispose();
            return new NullHapticService(false);
#elif UNITY_IOS && !UNITY_EDITOR
            return new IOSHapticService();
#else
            return new NullHapticService(logWhenUnsupported);
#endif
        }
    }
}
