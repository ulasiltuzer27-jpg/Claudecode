using System;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// Reklam rızası sağlayıcısı (Google UMP veya editör mock'u).
    ///
    /// Sözleşme: <see cref="GatherConsent"/> ve <see cref="ShowPrivacyOptionsForm"/>
    /// geri çağrıları Unity ana iş parçacığında, çağrı başına tam olarak bir
    /// kez gelir. Reklam SDK'sı ancak <see cref="CanRequestAds"/> true olunca
    /// başlatılır ve reklam ister.
    /// </summary>
    public interface IConsentService
    {
        string ServiceName { get; }

        /// <summary>Rıza alındı veya gerekmiyor; reklam istenebilir.</summary>
        bool CanRequestAds { get; }

        /// <summary>Kullanıcının rızasını değiştirebileceği bir giriş noktası (Ayarlar'da buton) gerekli mi.</summary>
        bool IsPrivacyOptionsRequired { get; }

        /// <summary>Son rıza bilgisi güncellemesi başarılı oldu mu (ör. ağ yoksa false; yeniden denenir).</summary>
        bool LastUpdateSucceeded { get; }

        /// <summary>
        /// Rıza durumunu günceller ve gerekiyorsa rıza formunu gösterir.
        /// Uygulamanın her açılışında çağrılmalı.
        /// </summary>
        /// <param name="onComplete">Akış bittiğinde, <see cref="CanRequestAds"/> değeriyle.</param>
        void GatherConsent(Action<bool> onComplete);

        /// <summary>Gizlilik seçenekleri formunu gösterir.</summary>
        /// <param name="onComplete">Form kapandığında; gösterilemediyse false.</param>
        void ShowPrivacyOptionsForm(Action<bool> onComplete);

        /// <summary>Kayıtlı rızayı siler. Yalnızca test içindir.</summary>
        void ResetConsent();
    }
}
