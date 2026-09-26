// Bu dosya yalnızca Google Mobile Ads Unity eklentisi projeye eklendiğinde
// derlenir. Eklentinin 9.x sürümüyle doğrulandı (UMP rıza API'leri
// CanRequestAds ve gizlilik seçenekleri formu için 9.x gerekir). Etkinleştirmek için:
//   1. https://github.com/googleads/googleads-mobile-unity/releases adresinden
//      eklentiyi içe aktarın.
//   2. Assets → Google Mobile Ads → Settings'e AdMob App ID'lerinizi girin.
//   3. Player Settings → Other Settings → Scripting Define Symbols'e
//      ADMOB_ENABLED ekleyin (Android ve iOS için ayrı ayrı).
// Tanım yokken AdManager otomatik olarak MockAdService'e düşer.
#if ADMOB_ENABLED
using System;
using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// <see cref="IAdService"/>'in Google AdMob uygulaması.
    ///
    /// ── İş parçacığı ────────────────────────────────────────────────────────
    /// AdMob geri çağrıları Android'de Unity ana iş parçacığında GELMEZ; oradan
    /// Unity API'sine (Text, Transform, PlayerPrefs) dokunmak çöker. Her geri
    /// çağrı <see cref="MainThreadContext"/> ile ana iş parçacığına gönderiliyor.
    ///
    /// ── Rıza ────────────────────────────────────────────────────────────────
    /// Initialize, AdManager tarafından ancak UMP rızası reklam istemeye izin
    /// verdikten sonra çağrılır. Sonraki her yükleme (gösterimden sonra,
    /// yeniden deneme) de <c>canRequestAds</c> koşuluna bakar.
    ///
    /// ── Ödül ve kapanma sırası ──────────────────────────────────────────────
    /// Ödül geri çağrısı genelde kapanmadan önce gelir ama bazı aracı ağlar
    /// tersini yapar. Kapanmada ödül henüz gelmediyse kısa bir süre beklenir;
    /// aksi halde reklamı sonuna kadar izleyen oyuncu ödülsüz kalabilirdi.
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        private const float RewardGraceSeconds = 0.5f;
        private const float MaxRetryDelaySeconds = 64f;

        private readonly MonoBehaviour _coroutineHost;
        private readonly string _rewardedAdUnitId;
        private readonly Func<bool> _canRequestAds;
        private readonly MainThreadContext _mainThread = new MainThreadContext();

        private RewardedAd _rewardedAd;
        private bool _isLoading;
        private int _loadRetryAttempt;

        private Action _pendingReward;
        private Action _pendingFailure;
        private bool _rewardEarned;
        private bool _adClosed;
        private bool _showResolved = true;

        /// <param name="canRequestAds">Rıza reklam istemeye izin veriyor mu; her yüklemeden önce sorulur.</param>
        public AdMobAdService(MonoBehaviour coroutineHost, string rewardedAdUnitId, Func<bool> canRequestAds)
        {
            _coroutineHost = coroutineHost;
            _rewardedAdUnitId = rewardedAdUnitId;
            _canRequestAds = canRequestAds;
        }

        public event Action<bool> onRewardedAdReadyChanged;

        public string ServiceName => "AdMob";
        public bool IsInitialized { get; private set; }
        public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();

        public void Initialize(Action<bool> onInitialized)
        {
            if (IsInitialized)
            {
                onInitialized?.Invoke(true);
                return;
            }

            MobileAds.Initialize(status => RunOnMainThread(() =>
            {
                IsInitialized = true;
                onInitialized?.Invoke(true);
                LoadRewardedAd();
            }));
        }

        public void LoadRewardedAd()
        {
            // Gösterim sürerken ekrandaki reklam DestroyCurrentAd ile yok edilmemeli.
            if (!IsInitialized || _isLoading || !_showResolved || IsRewardedAdReady)
            {
                return;
            }

            if (_canRequestAds != null && !_canRequestAds())
            {
                return;
            }

            if (string.IsNullOrEmpty(_rewardedAdUnitId))
            {
                Debug.LogError("[AdMobAdService] Ödüllü reklam birimi kimliği boş.");
                return;
            }

            DestroyCurrentAd();
            _isLoading = true;

            RewardedAd.Load(_rewardedAdUnitId, new AdRequest(), (ad, error) => RunOnMainThread(() =>
            {
                _isLoading = false;

                if (error != null || ad == null)
                {
                    string message = error != null ? error.GetMessage() : "bilinmeyen hata";
                    Debug.LogWarning($"[AdMobAdService] Ödüllü reklam yüklenemedi: {message}");
                    ad?.Destroy();
                    ScheduleRetry();
                    return;
                }

                _loadRetryAttempt = 0;
                _rewardedAd = ad;
                onRewardedAdReadyChanged?.Invoke(true);
            }));
        }

        public void ShowRewardedAd(Action onRewardEarned, Action onAdFailed)
        {
            if (!_showResolved || !IsRewardedAdReady)
            {
                onAdFailed?.Invoke();
                LoadRewardedAd();
                return;
            }

            RewardedAd ad = _rewardedAd;
            _pendingReward = onRewardEarned;
            _pendingFailure = onAdFailed;
            _rewardEarned = false;
            _adClosed = false;
            _showResolved = false;

            ad.OnAdFullScreenContentClosed += () => RunOnMainThread(OnAdClosed);
            ad.OnAdFullScreenContentFailed += adError => RunOnMainThread(() =>
            {
                Debug.LogWarning($"[AdMobAdService] Ödüllü reklam gösterilemedi: {adError.GetMessage()}");
                _adClosed = true;
                ResolveShow();
            });

            onRewardedAdReadyChanged?.Invoke(false);
            ad.Show(reward => RunOnMainThread(OnUserEarnedReward));
        }

        private void OnUserEarnedReward()
        {
            _rewardEarned = true;

            // Kapanma önce geldiyse ödeme bekleme süresinin bitmesini beklemesin.
            if (_adClosed)
            {
                ResolveShow();
            }
        }

        private void OnAdClosed()
        {
            _adClosed = true;

            if (_rewardEarned || !CanRunCoroutines())
            {
                ResolveShow();
                return;
            }

            _coroutineHost.StartCoroutine(ResolveAfterGrace());
        }

        private IEnumerator ResolveAfterGrace()
        {
            yield return new WaitForSecondsRealtime(RewardGraceSeconds);
            ResolveShow();
        }

        private void ResolveShow()
        {
            if (_showResolved)
            {
                return;
            }

            _showResolved = true;
            Action callback = _rewardEarned ? _pendingReward : _pendingFailure;
            _pendingReward = null;
            _pendingFailure = null;

            // Bir RewardedAd tek kullanımlık; bir sonrakini hemen yüklemeye başla.
            DestroyCurrentAd();
            LoadRewardedAd();

            callback?.Invoke();
        }

        private void ScheduleRetry()
        {
            if (!CanRunCoroutines())
            {
                return;
            }

            // Üstel geri çekilme: 2, 4, 8 ... 64 sn. Doldurma oranı düşük bir
            // ağda sürekli istek atmak hesabı kısıtlatabilir.
            _loadRetryAttempt++;
            float delay = Mathf.Min(MaxRetryDelaySeconds, Mathf.Pow(2f, _loadRetryAttempt));
            _coroutineHost.StartCoroutine(RetryAfter(delay));
        }

        private IEnumerator RetryAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            LoadRewardedAd();
        }

        private void DestroyCurrentAd()
        {
            if (_rewardedAd == null)
            {
                return;
            }

            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        private void RunOnMainThread(Action action)
        {
            _mainThread.Run(action);
        }

        private bool CanRunCoroutines()
        {
            return _coroutineHost != null && _coroutineHost.isActiveAndEnabled;
        }
    }
}
#endif
