using System;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>Mock rızanın davranışı; <see cref="AdManager"/> Inspector'ında düzenlenir.</summary>
    [Serializable]
    public sealed class MockConsentSettings
    {
        [Tooltip("Kullanıcı AEA/Birleşik Krallık'taymış gibi davran: ilk açılışta 'form' çıkar ve Ayarlar'da " +
                 "Gizlilik Tercihleri butonu görünür.")]
        public bool simulateRegulatedRegion;

        [Tooltip("Mock formda kullanıcının kararı. Kapalıysa rıza alınmamış sayılır ve reklam istenmez. " +
                 "Değiştirip Ayarlar → Gizlilik Tercihleri'ne basarak rızanın değişmesini deneyebilirsiniz.")]
        public bool grantConsent = true;

        [Tooltip("Rıza bilgisi güncellemesi başarısız olmuş gibi davran (ağ yok). Uygulama arka plandan dönünce yeniden denenir.")]
        public bool simulateUpdateFailure;
    }

    /// <summary>
    /// Editör ve mock reklam derlemeleri için rıza. Formu gerçekten göstermez;
    /// karar anında <see cref="MockConsentSettings"/>'ten okunur. Böylece
    /// "form çıktı / çıkmadı", "rıza verilmedi" ve "gizlilik butonu" akışları
    /// cihaz ve eklenti olmadan denenebilir.
    /// </summary>
    public sealed class MockConsentService : IConsentService
    {
        private readonly MockConsentSettings _settings;
        private bool _decided;
        private bool _granted;

        public MockConsentService(MockConsentSettings settings)
        {
            _settings = settings ?? new MockConsentSettings();
        }

        public string ServiceName => "Mock";

        public bool CanRequestAds => !_settings.simulateRegulatedRegion || (_decided && _granted);

        public bool IsPrivacyOptionsRequired => _settings.simulateRegulatedRegion && _decided;

        public bool LastUpdateSucceeded { get; private set; }

        public void GatherConsent(Action<bool> onComplete)
        {
            if (_settings.simulateUpdateFailure)
            {
                LastUpdateSucceeded = false;
                Debug.Log("[MockConsentService] Rıza bilgisi güncellenemedi (simülasyon).");
                onComplete?.Invoke(CanRequestAds);
                return;
            }

            LastUpdateSucceeded = true;

            if (_settings.simulateRegulatedRegion && !_decided)
            {
                Decide("Rıza formu");
            }

            onComplete?.Invoke(CanRequestAds);
        }

        public void ShowPrivacyOptionsForm(Action<bool> onComplete)
        {
            if (!IsPrivacyOptionsRequired)
            {
                onComplete?.Invoke(false);
                return;
            }

            Decide("Gizlilik seçenekleri formu");
            onComplete?.Invoke(true);
        }

        public void ResetConsent()
        {
            _decided = false;
            _granted = false;
        }

        private void Decide(string formName)
        {
            _decided = true;
            _granted = _settings.grantConsent;
            Debug.Log($"[MockConsentService] {formName}: {(_granted ? "rıza verildi" : "rıza verilmedi")}.");
        }
    }
}
