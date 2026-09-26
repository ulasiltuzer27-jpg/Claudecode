using System;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// Oyunun reklam giriş noktası. Platforma göre uygun <see cref="IAdService"/>
    /// uygulamasını seçer ve gösterimi oyun açısından güvenli hale getirir:
    /// aynı anda tek reklam, reklam sürerken ses kapalı, her çağrıya tam
    /// olarak bir geri dönüş.
    ///
    /// Servis seçimi:
    /// <list type="bullet">
    /// <item>Editör veya <c>forceMockAds</c> → <see cref="MockAdService"/></item>
    /// <item>Cihaz + <c>ADMOB_ENABLED</c> tanımı → <c>AdMobAdService</c></item>
    /// <item>Cihaz, tanım yok → mock (uyarıyla)</item>
    /// </list>
    ///
    /// Ödülün NE olduğu (2x çevrimdışı kazanç, 30 sn hızlandırıcı) burada
    /// değil <see cref="Core.GameManager"/>'da; AdManager yalnızca "reklam
    /// izlendi mi" sorusunu yanıtlar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdManager : MonoBehaviour
    {
        [Header("Servis seçimi")]
        [Tooltip("İşaretliyse cihazda da mock reklam kullanılır (iç test build'leri için).")]
        [SerializeField] private bool forceMockAds;

        [SerializeField] private MockAdSettings mockSettings = new MockAdSettings();

        [Header("AdMob (ADMOB_ENABLED tanımlıyken)")]
        [Tooltip("Varsayılan değer Google'ın resmi TEST birimidir. Yayından önce kendi biriminizle değiştirin.")]
        [SerializeField] private string androidRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";

        [Tooltip("Varsayılan değer Google'ın resmi TEST birimidir. Yayından önce kendi biriminizle değiştirin.")]
        [SerializeField] private string iosRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";

        private IAdService _service;
        private MockAdService _mockService;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _mockOverlayStyle;
#endif

        /// <summary>Gösterilebilir reklam durumu değiştiğinde tetiklenir; ödüllü butonları açıp kapatmak için.</summary>
        public event Action<bool> onRewardedAdReadyChanged;

        /// <summary>Bir reklam gösterimi başladığında tetiklenir.</summary>
        public event Action onAdStarted;

        /// <summary>Gösterim bittiğinde, ödül verilip verilmediğiyle tetiklenir.</summary>
        public event Action<bool> onAdFinished;

        public bool IsShowingAd { get; private set; }

        /// <summary>
        /// Uygulama son kez arka plana düştüğünde bir reklam gösteriliyor muydu.
        /// Android'de reklam Unity'yi duraklatır; o dönüşün çevrimdışı kazanç
        /// olarak sayılmaması için <see cref="Core.GameManager"/> buna bakar.
        /// </summary>
        public bool WasShowingAdWhenPaused { get; private set; }

        public bool IsRewardedAdReady => _service != null && !IsShowingAd && _service.IsRewardedAdReady;
        public string ServiceName => _service != null ? _service.ServiceName : "None";

        /// <summary>Şu anki platform için AdMob ödüllü reklam birimi.</summary>
        public string RewardedAdUnitId
        {
            get
            {
#if UNITY_IOS
                return iosRewardedAdUnitId;
#else
                return androidRewardedAdUnitId;
#endif
            }
        }

        private void Awake()
        {
            _service = CreateService();
            _service.onRewardedAdReadyChanged += HandleReadyChanged;
            _service.Initialize(success =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[AdManager] {_service.ServiceName} başlatılamadı.");
                }
            });
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                WasShowingAdWhenPaused = IsShowingAd;
            }
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.onRewardedAdReadyChanged -= HandleReadyChanged;
            }

            if (IsShowingAd)
            {
                AudioListener.pause = false;
            }
        }

        private IAdService CreateService()
        {
            bool useMock = forceMockAds || Application.isEditor;

#if ADMOB_ENABLED
            if (!useMock)
            {
                return new AdMobAdService(this, RewardedAdUnitId);
            }
#else
            if (!useMock)
            {
                Debug.LogWarning("[AdManager] ADMOB_ENABLED tanımlı değil; cihazda mock reklam kullanılıyor.");
            }
#endif

            _mockService = new MockAdService(this, mockSettings);
            return _mockService;
        }

        /// <summary>
        /// Ödüllü reklam gösterir. Başka bir reklam sürüyorsa veya hazır reklam
        /// yoksa <paramref name="onAdFailed"/> hemen çağrılır. İki geri
        /// çağrıdan yalnızca biri, yalnızca bir kez çağrılır.
        /// </summary>
        public void ShowRewardedAd(Action onRewardEarned, Action onAdFailed)
        {
            if (IsShowingAd)
            {
                onAdFailed?.Invoke();
                return;
            }

            if (_service == null || !_service.IsRewardedAdReady)
            {
                _service?.LoadRewardedAd();
                onAdFailed?.Invoke();
                return;
            }

            IsShowingAd = true;
            AudioListener.pause = true;
            onAdStarted?.Invoke();
            onRewardedAdReadyChanged?.Invoke(false);

            bool completed = false;
            _service.ShowRewardedAd(
                () =>
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    FinishAd(true);
                    onRewardEarned?.Invoke();
                },
                () =>
                {
                    if (completed)
                    {
                        return;
                    }

                    completed = true;
                    FinishAd(false);
                    onAdFailed?.Invoke();
                });
        }

        /// <summary>Hazır reklam yoksa yeni bir yükleme başlatır.</summary>
        public void PreloadRewardedAd()
        {
            _service?.LoadRewardedAd();
        }

        private void FinishAd(bool rewarded)
        {
            IsShowingAd = false;
            AudioListener.pause = false;
            onAdFinished?.Invoke(rewarded);
            onRewardedAdReadyChanged?.Invoke(IsRewardedAdReady);
        }

        private void HandleReadyChanged(bool ready)
        {
            onRewardedAdReadyChanged?.Invoke(ready && !IsShowingAd);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Mock reklam sürerken ekranı kaplayan bir sayaç çizer; editörde
        /// reklamın "ekranda" olduğu ve oyunun beklediği böylece görülür.
        /// Release build'de derlenmiyor: sahnede bir OnGUI bulunması, erken
        /// dönse bile her kare IMGUI olay döngüsünü çalıştırır.
        /// </summary>
        private void OnGUI()
        {
            if (_mockService == null || !_mockService.IsShowing)
            {
                return;
            }

            if (_mockOverlayStyle == null)
            {
                _mockOverlayStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(24, Screen.height / 30),
                    wordWrap = true
                };
                _mockOverlayStyle.normal.textColor = Color.white;
            }

            GUI.depth = -1000;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height),
                $"MOCK REWARDED AD\n\n{Mathf.CeilToInt(_mockService.RemainingAdTime)}", _mockOverlayStyle);
        }
#endif

        private void OnValidate()
        {
            androidRewardedAdUnitId = androidRewardedAdUnitId != null ? androidRewardedAdUnitId.Trim() : string.Empty;
            iosRewardedAdUnitId = iosRewardedAdUnitId != null ? iosRewardedAdUnitId.Trim() : string.Empty;
        }
    }
}
