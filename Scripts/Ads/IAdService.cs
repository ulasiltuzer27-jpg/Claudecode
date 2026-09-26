using System;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// Ödüllü reklam sağlayıcısının oyuna görünen yüzü. Oyun kodu yalnızca
    /// bu arayüzü tanır; AdMob, editör mock'u veya ileride eklenecek başka
    /// bir ağ (Unity Ads, AppLovin) aynı sözleşmeyle takılıp çıkarılır.
    ///
    /// Sözleşme: <see cref="ShowRewardedAd"/> çağrısı başına iki geri
    /// çağrıdan TAM OLARAK BİRİ, Unity ana iş parçacığında çağrılır.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Günlük kayıtları ve hata ayıklama için okunabilir ad.</summary>
        string ServiceName { get; }

        bool IsInitialized { get; }

        /// <summary>Şu an gösterilmeye hazır, yüklenmiş bir ödüllü reklam var mı.</summary>
        bool IsRewardedAdReady { get; }

        /// <summary>
        /// Hazır reklam durumu değiştiğinde tetiklenir (yüklendi → true,
        /// gösterildi/süresi doldu → false). UI butonlarını açıp kapatmak için.
        /// </summary>
        event Action<bool> onRewardedAdReadyChanged;

        /// <summary>SDK'yı başlatır ve ilk reklamı yüklemeye başlar.</summary>
        void Initialize(Action<bool> onInitialized);

        /// <summary>Hazır reklam yoksa ve yükleme sürmüyorsa yeni bir reklam ister.</summary>
        void LoadRewardedAd();

        /// <summary>
        /// Ödüllü reklamı gösterir. Oyuncu reklamı sonuna kadar izlerse
        /// <paramref name="onRewardEarned"/>; reklam hazır değilse, gösterilemezse
        /// veya oyuncu erken kapatırsa <paramref name="onAdFailed"/> çağrılır.
        /// </summary>
        void ShowRewardedAd(Action onRewardEarned, Action onAdFailed);
    }
}
