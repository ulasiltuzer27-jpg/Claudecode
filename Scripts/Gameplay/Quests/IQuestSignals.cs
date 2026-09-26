using System;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// Görev hedeflerinin dinleyebildiği oyun olayları. Hedefler somut
    /// yöneticileri (CurrencyManager, PrestigeManager) değil bu arayüzü
    /// tanır; böylece test edilebilir kalır ve yöneticilerin iç yapısı
    /// değiştiğinde görevler etkilenmez.
    /// </summary>
    public interface IQuestSignals
    {
        /// <summary>Oyuncu para kazandığında (üretim, çevrimdışı, ödül), kazanılan tutarla.</summary>
        event Action<double> onMoneyEarned;

        /// <summary>Oyuncu bir istasyonu satın aldığında/yükselttiğinde. Kayıt yükleme ve sıfırlama sayılmaz.</summary>
        event Action<Station> onStationUpgraded;

        /// <summary>Restoran sıfırlandığında, kazanılan Gem'le.</summary>
        event Action<double> onPrestigePerformed;
    }
}
