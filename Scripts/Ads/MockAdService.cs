using System;
using System.Collections;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>Mock reklamın davranışı; <see cref="AdManager"/> Inspector'ında düzenlenir.</summary>
    [Serializable]
    public sealed class MockAdSettings
    {
        [Tooltip("Bir reklamın 'yüklenmesi' için beklenen süre (saniye).")]
        [Min(0f)] public float loadDelay = 1f;

        [Tooltip("Reklamın ekranda kaldığı süre (saniye).")]
        [Min(0f)] public float adDuration = 3f;

        [Tooltip("Gösterimin başarısız sayılma olasılığı. Hata yolunu test etmek için 1 yapın.")]
        [Range(0f, 1f)] public float failureChance;
    }

    /// <summary>
    /// SDK olmadan editörde ve test build'lerinde çalışan sahte ödüllü reklam.
    /// Yükleme ve izleme süresini gerçek zamanlı bekler, böylece "reklam
    /// hazır değil" ve "reklam sürüyor" durumları da gerçekçi biçimde
    /// test edilebilir. Ekrandaki sayaç <see cref="AdManager"/> tarafından
    /// çizilir.
    /// </summary>
    public sealed class MockAdService : IAdService
    {
        private readonly MonoBehaviour _coroutineHost;
        private readonly MockAdSettings _settings;
        private bool _isReady;
        private bool _isLoading;

        public MockAdService(MonoBehaviour coroutineHost, MockAdSettings settings)
        {
            _coroutineHost = coroutineHost;
            _settings = settings ?? new MockAdSettings();
        }

        public event Action<bool> onRewardedAdReadyChanged;

        public string ServiceName => "Mock";
        public bool IsInitialized { get; private set; }
        public bool IsRewardedAdReady => _isReady;
        public bool IsShowing { get; private set; }

        /// <summary>Gösterilen mock reklamın kalan süresi (saniye).</summary>
        public float RemainingAdTime { get; private set; }

        public void Initialize(Action<bool> onInitialized)
        {
            IsInitialized = true;
            onInitialized?.Invoke(true);
            LoadRewardedAd();
        }

        public void LoadRewardedAd()
        {
            if (!IsInitialized || _isReady || _isLoading || !CanRunCoroutines())
            {
                return;
            }

            _coroutineHost.StartCoroutine(LoadRoutine());
        }

        public void ShowRewardedAd(Action onRewardEarned, Action onAdFailed)
        {
            if (!_isReady || IsShowing || !CanRunCoroutines())
            {
                onAdFailed?.Invoke();
                return;
            }

            _coroutineHost.StartCoroutine(ShowRoutine(onRewardEarned, onAdFailed));
        }

        private IEnumerator LoadRoutine()
        {
            _isLoading = true;
            yield return new WaitForSecondsRealtime(_settings.loadDelay);
            _isLoading = false;
            SetReady(true);
        }

        private IEnumerator ShowRoutine(Action onRewardEarned, Action onAdFailed)
        {
            SetReady(false);
            IsShowing = true;
            RemainingAdTime = _settings.adDuration;

            // unscaled: timeScale 0 iken de reklam bitmeli.
            while (RemainingAdTime > 0f)
            {
                RemainingAdTime -= Time.unscaledDeltaTime;
                yield return null;
            }

            RemainingAdTime = 0f;
            IsShowing = false;

            bool succeeded = UnityEngine.Random.value >= _settings.failureChance;
            Debug.Log($"[MockAdService] Ödüllü reklam bitti: {(succeeded ? "ödül verildi" : "başarısız")}.");

            if (succeeded)
            {
                onRewardEarned?.Invoke();
            }
            else
            {
                onAdFailed?.Invoke();
            }

            LoadRewardedAd();
        }

        private void SetReady(bool ready)
        {
            if (_isReady == ready)
            {
                return;
            }

            _isReady = ready;
            onRewardedAdReadyChanged?.Invoke(ready);
        }

        private bool CanRunCoroutines()
        {
            return _coroutineHost != null && _coroutineHost.isActiveAndEnabled;
        }
    }
}
