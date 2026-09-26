// Google Mobile Ads Unity eklentisiyle gelen User Messaging Platform (UMP)
// SDK'sını kullanır; yalnızca ADMOB_ENABLED tanımlıyken derlenir. Tanım yokken
// AdManager MockConsentService'e düşer. Kurulum için AdMobAdService.cs'nin
// başındaki adımlara bakın.
#if ADMOB_ENABLED
using System;
using System.Collections.Generic;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// <see cref="IConsentService"/>'in Google UMP uygulaması.
    ///
    /// Akış (Google'ın önerdiği sıra):
    /// <list type="number">
    /// <item>ConsentInformation.Update ile rıza durumunu sorgula (her açılışta).</item>
    /// <item>ConsentForm.LoadAndShowConsentFormIfRequired ile gerekiyorsa formu göster.</item>
    /// <item>ConsentInformation.CanRequestAds() true ise reklam SDK'sını başlat.</item>
    /// </list>
    ///
    /// Güncelleme başarısız olursa (ağ yok) akış yine tamamlanır: önceki
    /// oturumda alınmış rıza CanRequestAds'i zaten true yapıyor olabilir.
    /// Formun hangi bölgede çıkacağına, hangi seçenekleri içereceğine AdMob
    /// konsolundaki "Gizlilik ve mesajlaşma" ayarları karar verir; kod bölge
    /// bilmez.
    /// </summary>
    internal sealed class UmpConsentService : IConsentService
    {
        private readonly ConsentOptions _options;
        private readonly MainThreadContext _mainThread = new MainThreadContext();

        public UmpConsentService(ConsentOptions options)
        {
            _options = options ?? new ConsentOptions();
        }

        public string ServiceName => "UMP";
        public bool CanRequestAds => ConsentInformation.CanRequestAds();

        public bool IsPrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public bool LastUpdateSucceeded { get; private set; }

        public void GatherConsent(Action<bool> onComplete)
        {
            ConsentRequestParameters request = new ConsentRequestParameters
            {
                TagForUnderAgeOfConsent = _options.tagForUnderAgeOfConsent,
                ConsentDebugSettings = BuildDebugSettings()
            };

            ConsentInformation.Update(request, updateError => _mainThread.Run(() =>
            {
                if (updateError != null)
                {
                    LastUpdateSucceeded = false;
                    Debug.LogWarning($"[UmpConsentService] Rıza bilgisi güncellenemedi ({updateError.ErrorCode}): {updateError.Message}");
                    onComplete?.Invoke(CanRequestAds);
                    return;
                }

                LastUpdateSucceeded = true;
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => _mainThread.Run(() =>
                {
                    if (formError != null)
                    {
                        // Form yüklenemedi veya gösterilemedi; mevcut rıza durumuyla devam.
                        Debug.LogWarning($"[UmpConsentService] Rıza formu gösterilemedi ({formError.ErrorCode}): {formError.Message}");
                    }

                    onComplete?.Invoke(CanRequestAds);
                }));
            }));
        }

        public void ShowPrivacyOptionsForm(Action<bool> onComplete)
        {
            ConsentForm.ShowPrivacyOptionsForm(formError => _mainThread.Run(() =>
            {
                if (formError != null)
                {
                    Debug.LogWarning($"[UmpConsentService] Gizlilik seçenekleri formu gösterilemedi ({formError.ErrorCode}): {formError.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            }));
        }

        public void ResetConsent()
        {
            ConsentInformation.Reset();
        }

        /// <summary>
        /// Test coğrafyası ve cihazları yalnızca Development Build'de ve
        /// editörde gönderilir. Yayın derlemesinde her zaman "Disabled"
        /// gider: unutulan bir test ayarı gerçek kullanıcıların formunu
        /// değiştiremez.
        /// </summary>
        private ConsentDebugSettings BuildDebugSettings()
        {
            ConsentDebugSettings settings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.Disabled,
                TestDeviceHashedIds = new List<string>()
            };

            if (!_options.DebugSettingsActive)
            {
                if (_options.debugGeography != ConsentDebugGeography.Disabled)
                {
                    Debug.LogWarning("[UmpConsentService] Debug Geography yayın derlemesinde yok sayıldı.");
                }

                return settings;
            }

            settings.DebugGeography = ToUmpGeography(_options.debugGeography);

            for (int i = 0; i < _options.testDeviceHashedIds.Count; i++)
            {
                string id = _options.testDeviceHashedIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    settings.TestDeviceHashedIds.Add(id.Trim());
                }
            }

            return settings;
        }

        private static DebugGeography ToUmpGeography(ConsentDebugGeography geography)
        {
            switch (geography)
            {
                case ConsentDebugGeography.EEA:
                    return DebugGeography.EEA;
                case ConsentDebugGeography.NotEEA:
                    return DebugGeography.NotEEA;
                default:
                    return DebugGeography.Disabled;
            }
        }
    }
}
#endif
