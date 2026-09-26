#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace IdleRestaurant.Audio.Haptics
{
    /// <summary>
    /// Android titreşimi, JNI üzerinden android.os.Vibrator ile.
    ///
    /// API seviyesine göre üç yol:
    /// <list type="bullet">
    /// <item>29+: VibrationEffect.createPredefined — sistemin kendi "tık"
    ///       efektleri; cihaza göre ayarlı ve en doğal his.</item>
    /// <item>26-28: VibrationEffect.createOneShot — süre + şiddet (şiddet
    ///       kontrolü olmayan cihazda varsayılan şiddet).</item>
    /// <item>26 altı: vibrate(long) — yalnızca süre.</item>
    /// </list>
    ///
    /// İzin: android.permission.VIBRATE gerekir. Unity bu izni, derlenen
    /// kodda Handheld.Vibrate çağrısı gördüğünde manifest'e kendisi ekler;
    /// aşağıdaki son çare yolu bu yüzden aynı zamanda izni garanti ediyor.
    /// </summary>
    internal sealed class AndroidHapticService : IHapticService, IDisposable
    {
        // android.os.VibrationEffect sabitleri (API 29).
        private const int EffectClick = 0;
        private const int EffectTick = 2;
        private const int EffectHeavyClick = 5;
        private const int DefaultAmplitude = -1;

        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaClass _vibrationEffect;
        private readonly int _sdkInt;
        private readonly bool _hasAmplitudeControl;
        private bool _failed;

        public AndroidHapticService()
        {
            try
            {
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdkInt = version.GetStatic<int>("SDK_INT");
                }

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator"))
                {
                    _vibrator.Dispose();
                    _vibrator = null;
                }

                if (_vibrator != null && _sdkInt >= 26)
                {
                    _vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                    _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AndroidHapticService] Titreşim servisi alınamadı: {e.Message}");
                _vibrator = null;
            }
        }

        public bool IsSupported => _vibrator != null && !_failed;

        public void Play(HapticType type)
        {
            if (type == HapticType.None || !IsSupported)
            {
                return;
            }

            try
            {
                if (_sdkInt >= 29)
                {
                    using (AndroidJavaObject effect = _vibrationEffect.CallStatic<AndroidJavaObject>("createPredefined", PredefinedEffectFor(type)))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else if (_sdkInt >= 26)
                {
                    int amplitude = _hasAmplitudeControl ? AmplitudeFor(type) : DefaultAmplitude;
                    using (AndroidJavaObject effect = _vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", DurationFor(type), amplitude))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", DurationFor(type));
                }
            }
            catch (Exception e)
            {
                // Üreticiye özel bir engel: bir daha JNI denenmez, en azından
                // güçlü olaylarda Unity'nin kendi titreşimi çalışsın.
                _failed = true;
                Debug.LogWarning($"[AndroidHapticService] Titreşim başarısız, bu oturumda kapatıldı: {e.Message}");

                if (type == HapticType.Heavy)
                {
                    Handheld.Vibrate();
                }
            }
        }

        public void Dispose()
        {
            if (_vibrator != null)
            {
                _vibrator.Dispose();
            }

            if (_vibrationEffect != null)
            {
                _vibrationEffect.Dispose();
            }
        }

        private static int PredefinedEffectFor(HapticType type)
        {
            switch (type)
            {
                case HapticType.Light:
                    return EffectTick;
                case HapticType.Medium:
                    return EffectClick;
                default:
                    return EffectHeavyClick;
            }
        }

        private static long DurationFor(HapticType type)
        {
            switch (type)
            {
                case HapticType.Light:
                    return 12L;
                case HapticType.Medium:
                    return 25L;
                default:
                    return 45L;
            }
        }

        private static int AmplitudeFor(HapticType type)
        {
            switch (type)
            {
                case HapticType.Light:
                    return 60;
                case HapticType.Medium:
                    return 140;
                default:
                    return 255;
            }
        }
    }
}
#endif
