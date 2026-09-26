using System;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// Ayarlar ekranının "Gizlilik Tercihleri" butonuna gereken tek şey.
    /// <see cref="AdManager"/> uygular; ayarlar paneli reklam sistemini
    /// bütünüyle değil yalnızca bu arayüzü tanır.
    /// </summary>
    public interface IPrivacyOptionsProvider
    {
        /// <summary>Buton gösterilmeli mi (kullanıcı rıza gereken bir bölgede).</summary>
        bool IsPrivacyOptionsRequired { get; }

        /// <summary>Rıza durumu veya gereksinimi değiştiğinde tetiklenir (butonu gizle/göster).</summary>
        event Action onPrivacyOptionsChanged;

        /// <param name="onComplete">Form kapandığında; gösterilemediyse false.</param>
        void ShowPrivacyOptionsForm(Action<bool> onComplete);
    }
}
