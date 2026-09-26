using System;
using System.Collections.Generic;

namespace IdleRestaurant.Data
{
    /// <summary>
    /// Diske yazılan oyun durumu. <see cref="UnityEngine.JsonUtility"/> ile
    /// serileştirildiği için alanlar public ve özellik (property) değil:
    /// JsonUtility yalnızca alanları görür, Dictionary'yi de desteklemez —
    /// istasyonlar bu yüzden liste olarak tutuluyor.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Kayıt biçimi sürümü. Alan eklemek sürüm artırmayı gerektirmez
        /// (eksik alan varsayılan değerle okunur); anlamı değişen bir alan
        /// gerektirir.
        /// </summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>Toplam para.</summary>
        public double currency;

        /// <summary>
        /// Son kaydın zamanı, <see cref="DateTime.UtcNow"/>.Ticks. UTC çünkü
        /// yerel saat yaz saati geçişinde ve saat dilimi değişiminde geri
        /// gidebilir; o da çevrimdışı süreyi negatif ya da şişik yapardı.
        /// </summary>
        public long lastExitUtcTicks;

        /// <summary>
        /// Reklamla kazanılan hızlandırıcının kalan süresi. Oyuncu reklam
        /// izleyip hemen uygulamayı kapatırsa ödülü kaybolmasın diye saklanıyor.
        /// </summary>
        public float speedBoostRemainingSeconds;

        public List<StationSaveData> stations = new List<StationSaveData>();

        /// <summary>Kimliği verilen istasyonun kayıtlı seviyesini arar.</summary>
        public bool TryGetStationLevel(string stationId, out int level)
        {
            if (stations != null && !string.IsNullOrEmpty(stationId))
            {
                for (int i = 0; i < stations.Count; i++)
                {
                    StationSaveData entry = stations[i];
                    if (entry != null && entry.stationId == stationId)
                    {
                        level = entry.level;
                        return true;
                    }
                }
            }

            level = 0;
            return false;
        }

        /// <summary>
        /// Elle düzenlenmiş veya yarım kalmış bir dosyadan gelen geçersiz
        /// değerleri düzeltir. Yüklemeden hemen sonra çağrılır.
        /// </summary>
        public void Sanitize()
        {
            if (double.IsNaN(currency) || double.IsInfinity(currency) || currency < 0d)
            {
                currency = 0d;
            }

            if (lastExitUtcTicks < 0L || lastExitUtcTicks > DateTime.MaxValue.Ticks)
            {
                lastExitUtcTicks = 0L;
            }

            if (float.IsNaN(speedBoostRemainingSeconds) || speedBoostRemainingSeconds < 0f)
            {
                speedBoostRemainingSeconds = 0f;
            }

            if (stations == null)
            {
                stations = new List<StationSaveData>();
            }

            stations.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.stationId));

            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].level < 0)
                {
                    stations[i].level = 0;
                }
            }
        }
    }

    /// <summary>Tek bir istasyonun kalıcı durumu.</summary>
    [Serializable]
    public sealed class StationSaveData
    {
        public string stationId;
        public int level;

        public StationSaveData()
        {
        }

        public StationSaveData(string stationId, int level)
        {
            this.stationId = stationId;
            this.level = level;
        }
    }
}
