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
    /// Servis seçimi (reklam ve rıza birlikte):
    /// <list type="bullet">
    /// <item>Editör veya <c>forceMockAds</c> → <see cref="MockAdService"/> + <see cref="MockConsentService"/></item>
    /// <item>Cihaz + <c>ADMOB_ENABLED</c> tanımı → <c>AdMobAdService</c> + <c>UmpConsentService</c></item>
    /// <item>Cihaz, tanım yok → mock (uyarıyla)</item>
    /// </list>
    ///
    /// ── Rıza önce gelir ─────────────────────────────────────────────────────
    /// Açılışta önce rıza toplanır (<see cref="IConsentService.GatherConsent"/>);
    /// reklam SDK'sı ancak <see cref="CanRequestAds"/> true olunca başlatılır.
    /// Rıza güncellemesi başarısız olursa (ağ yok) uygulama arka plandan
    /// döndüğünde yeniden denenir. Ayarlar'daki "Gizlilik Tercihleri"
    /// butonu <see cref="IPrivacyOptionsProvider"/> üzerinden buraya gelir.
    ///
    /// Ödülün NE olduğu (2x çevrimdışı kazanç, 30 sn hızlandırıcı) burada
    /// değil <see cref="Core.GameManager"/>'da; AdManager yalnızca "reklam
    /// izlendi mi" sorusunu yanıtlar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdManager : MonoBehaviour, IPrivacyOptionsProvider
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

        [Header("Rıza (UMP)")]
        [SerializeField] private ConsentOptions consentOptions = new ConsentOptions();

        [Tooltip("Mock reklam kullanılırken (editör) rızanın davranışı.")]
        [SerializeField] private MockConsentSettings mockConsentSettings = new MockConsentSettings();

        private IAdService _service;
        private MockAdService _mockService;
        private IConsentService _consent;
        private bool _gatheringConsent;
        private bool _adsInitializeRequested;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _mockOverlayStyle;
#endif

        /// <summary>Gösterilebilir reklam durumu değiştiğinde tetiklenir; ödüllü butonları açıp kapatmak için.</summary>
        public event Action<bool> onRewardedAdReadyChanged;

        /// <summary>Bir reklam gösterimi başladığında tetiklenir.</summary>
        public event Action onAdStarted;

        /// <summary>Gösterim bittiğinde, ödül verilip verilmediğiyle tetiklenir.</summary>
        public event Action<bool> onAdFinished;

        /// <summary>
        /// Rıza akışı bittiğinde veya gizlilik seçenekleri formu kapandığında
        /// tetiklenir. Ayarlar paneli "Gizlilik Tercihleri" butonunu buna göre
        /// gösterir/gizler.
        /// </summary>
        public event Action onPrivacyOptionsChanged;

        public bool IsShowingAd { get; private set; }

        /// <summary>Rıza formu veya gizlilik seçenekleri formu ekranda (ya da rıza akışı sürüyor).</summary>
        public bool IsShowingConsentForm { get; private set; }

        /// <summary>
        /// Uygulama son kez arka plana düştüğünde bir reklam veya rıza formu
        /// gösteriliyor muydu. Android'de reklam Unity'yi duraklatır; o dönüşün
        /// çevrimdışı kazanç olarak sayılmaması için <see cref="Core.GameManager"/> buna bakar.
        /// </summary>
        public bool WasShowingAdWhenPaused { get; private set; }

        /// <summary>Rıza reklam istemeye izin veriyor mu (rıza alındı veya bu bölgede gerekmiyor).</summary>
        public bool CanRequestAds => _consent != null && _consent.CanRequestAds;

        /// <summary>Ayarlar'da "Gizlilik Tercihleri" butonu gösterilmeli mi.</summary>
        public bool IsPrivacyOptionsRequired => _consent != null && _consent.IsPrivacyOptionsRequired;

        public bool IsRewardedAdReady => _service != null && !IsShowingAd && CanRequestAds && _service.IsRewardedAdReady;
        public string ServiceName => _service != null ? _service.ServiceName : "None";
        public string ConsentServiceName => _consent != null ? _consent.ServiceName : "None";

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
            CreateServices();
            _service.onRewardedAdReadyChanged += HandleReadyChanged;
            GatherConsent();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                WasShowingAdWhenPaused = IsShowingAd || IsShowingConsentForm;
                return;
            }

            // Açılıştaki güncelleme ağ yüzünden başarısız olduysa dönüşte yeniden dene.
            if (_consent != null && !_gatheringConsent && !_consent.LastUpdateSucceeded)
            {
                GatherConsent();
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

        private void CreateServices()
        {
            bool useMock = forceMockAds || Application.isEditor;

#if ADMOB_ENABLED
            if (!useMock)
            {
                _consent = new UmpConsentService(consentOptions);
                _service = new AdMobAdService(this, RewardedAdUnitId, () => CanRequestAds);
                return;
            }
#else
            if (!useMock)
            {
                Debug.LogWarning("[AdManager] ADMOB_ENABLED tanımlı değil; cihazda mock reklam ve rıza kullanılıyor.");
            }
#endif

            _consent = new MockConsentService(mockConsentSettings);
            _mockService = new MockAdService(this, mockSettings);
            _service = _mockService;
        }

        // ── Rıza ───────────────────────────────────────────────────────────────

        private void GatherConsent()
        {
            if (_gatheringConsent)
            {
                return;
            }

            _gatheringConsent = true;
            IsShowingConsentForm = true;

            _consent.GatherConsent(canRequestAds =>
            {
                _gatheringConsent = false;
                IsShowingConsentForm = false;
                HandleConsentChanged();

                if (!canRequestAds)
                {
                    Debug.Log($"[AdManager] {_consent.ServiceName}: rıza yok, reklam istenmeyecek.");
                }
            });
        }

        /// <summary>
        /// Gizlilik seçenekleri formunu gösterir. Form gerekmiyorsa, bir reklam
        /// veya başka bir form ekrandaysa <paramref name="onComplete"/> hemen false ile çağrılır.
        /// </summary>
        public void ShowPrivacyOptionsForm(Action<bool> onComplete)
        {
            if (_consent == null || !_consent.IsPrivacyOptionsRequired || IsShowingAd || IsShowingConsentForm)
            {
                onComplete?.Invoke(false);
                return;
            }

            IsShowingConsentForm = true;
            _consent.ShowPrivacyOptionsForm(shown =>
            {
                IsShowingConsentForm = false;
                HandleConsentChanged();
                onComplete?.Invoke(shown);
            });
        }

        /// <summary>
        /// Rıza durumu değişti: reklam izni yeni geldiyse SDK'yı başlat, butonlara
        /// ve ayarlar paneline haber ver.
        /// </summary>
        private void HandleConsentChanged()
        {
            if (CanRequestAds)
            {
                InitializeAdsOnce();
            }

            onPrivacyOptionsChanged?.Invoke();
            onRewardedAdReadyChanged?.Invoke(IsRewardedAdReady);
        }

        private void InitializeAdsOnce()
        {
            if (_adsInitializeRequested)
            {
                return;
            }

            _adsInitializeRequested = true;
            _service.Initialize(success =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[AdManager] {_service.ServiceName} başlatılamadı.");
                }
            });
        }

        /// <summary>Kayıtlı rızayı siler ve akışı yeniden başlatır. Yalnızca test içindir.</summary>
        [ContextMenu("Debug/Reset Consent")]
        public void ResetConsentForTesting()
        {
            if (_consent == null || _gatheringConsent)
            {
                return;
            }

            _consent.ResetConsent();
            Debug.Log("[AdManager] Rıza sıfırlandı; akış yeniden başlatılıyor.");
            GatherConsent();
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

            if (_service == null || !CanRequestAds || !_service.IsRewardedAdReady)
            {
                PreloadRewardedAd();
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

        /// <summary>Hazır reklam yoksa ve rıza izin veriyorsa yeni bir yükleme başlatır.</summary>
        public void PreloadRewardedAd()
        {
            if (_service != null && CanRequestAds)
            {
                _service.LoadRewardedAd();
            }
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
            onRewardedAdReadyChanged?.Invoke(ready && !IsShowingAd && CanRequestAds);
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
