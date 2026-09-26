using System;
using System.Collections.Generic;

namespace IdleRestaurant.Data
{
    /// <summary>
    /// Diske yazılan oyun durumu. <see cref="UnityEngine.JsonUtility"/> ile
    /// serileştirildiği için alanlar public ve özellik (property) değil:
    /// JsonUtility yalnızca alanları görür, Dictionary'yi de desteklemez —
    /// istasyonlar ve yükseltmeler bu yüzden liste olarak tutuluyor.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Kayıt biçimi sürümü. Alan eklemek sürüm artırmayı gerektirmez
        /// (eksik alan varsayılan değerle okunur); anlamı değişen veya eski
        /// kayıtta türetilmesi gereken bir alan gerektirir.
        ///
        /// v1 → v2: <see cref="lifetimeEarnings"/>, <see cref="prestige"/>,
        /// <see cref="quests"/> eklendi. Bkz. <see cref="UpgradeToCurrentVersion"/>.
        /// v2 → v3: <see cref="audio"/> eklendi. Türetilecek bir şey yok: eksik
        /// bölüm <see cref="AudioSaveData.hasSettings"/> = false okunur ve ses
        /// sistemi kendi varsayılanlarıyla başlar.
        /// v3 içinde: müzik ayarları eklendi; sürüm artmadı, çünkü eksik alanlar
        /// <see cref="AudioSaveData.hasMusicSettings"/> = false okunur ve müzik
        /// yine Inspector varsayılanlarıyla başlar.
        /// </summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;

        /// <summary>Toplam para.</summary>
        public double currency;

        /// <summary>
        /// Oyuncunun oyun boyunca kazandığı toplam para. Harcamayla ve
        /// prestijle AZALMAZ; Gem formülünün girdisi.
        /// </summary>
        public double lifetimeEarnings;

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

        public PrestigeSaveData prestige = new PrestigeSaveData();

        public QuestSaveData quests = new QuestSaveData();

        public AudioSaveData audio = new AudioSaveData();

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
        /// Eski sürümden okunan bir kaydı bugünkü anlamlarına taşır.
        /// Yüklemeden hemen sonra, <see cref="Sanitize"/>'dan önce çağrılır.
        /// Daha YENİ bir sürümün kaydına dokunmaz.
        /// </summary>
        public void UpgradeToCurrentVersion()
        {
            if (version >= CurrentVersion)
            {
                return;
            }

            if (version < 2)
            {
                // v1 ömür boyu kazancı tutmuyordu. Eldeki para bunun kanıtlanabilir
                // alt sınırı; sıfırdan başlatmak eski oyuncuyu Gem'den mahrum ederdi.
                lifetimeEarnings = Math.Max(lifetimeEarnings, currency);
            }

            version = CurrentVersion;
        }

        /// <summary>
        /// Elle düzenlenmiş veya yarım kalmış bir dosyadan gelen geçersiz
        /// değerleri düzeltir. Yüklemeden hemen sonra çağrılır.
        /// </summary>
        public void Sanitize()
        {
            currency = SanitizeAmount(currency);
            lifetimeEarnings = SanitizeAmount(lifetimeEarnings);

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

            if (prestige == null)
            {
                prestige = new PrestigeSaveData();
            }

            prestige.Sanitize();

            if (quests == null)
            {
                quests = new QuestSaveData();
            }

            quests.Sanitize();

            if (audio == null)
            {
                audio = new AudioSaveData();
            }

            audio.Sanitize();
        }

        /// <summary>NaN, sonsuz ve negatif miktarları 0'a çeker.</summary>
        internal static double SanitizeAmount(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
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

    /// <summary>Prestij sisteminin kalıcı durumu.</summary>
    [Serializable]
    public sealed class PrestigeSaveData
    {
        /// <summary>Harcanabilir Gem bakiyesi.</summary>
        public double gems;

        /// <summary>
        /// Prestijle bugüne kadar alınan toplam Gem (harcamayla azalmaz).
        /// Bekleyen Gem = formül(ömür boyu kazanç) - bu değer; aynı kazancın
        /// iki kez ödenmesini bu alan engelliyor.
        /// </summary>
        public double totalGemsEarned;

        public int prestigeCount;

        public List<PermanentUpgradeSaveData> upgrades = new List<PermanentUpgradeSaveData>();

        public void Sanitize()
        {
            gems = SaveData.SanitizeAmount(gems);
            totalGemsEarned = SaveData.SanitizeAmount(totalGemsEarned);

            if (prestigeCount < 0)
            {
                prestigeCount = 0;
            }

            if (upgrades == null)
            {
                upgrades = new List<PermanentUpgradeSaveData>();
            }

            upgrades.RemoveAll(entry => entry == null || string.IsNullOrEmpty(entry.upgradeId) || entry.level <= 0);
        }
    }

    /// <summary>Satın alınmış tek bir kalıcı yükseltmenin seviyesi.</summary>
    [Serializable]
    public sealed class PermanentUpgradeSaveData
    {
        public string upgradeId;
        public int level;

        public PermanentUpgradeSaveData()
        {
        }

        public PermanentUpgradeSaveData(string upgradeId, int level)
        {
            this.upgradeId = upgradeId;
            this.level = level;
        }
    }

    /// <summary>Görev zincirinin kalıcı durumu.</summary>
    [Serializable]
    public sealed class QuestSaveData
    {
        /// <summary>Etkin görevin kimliği. Zincir sırası değişse bile doğru göreve dönmek için.</summary>
        public string questId;

        /// <summary>Kimlik bulunamazsa (görev silindi) kullanılan yedek konum.</summary>
        public int chainIndex;

        /// <summary>Zincirin kaçıncı kez tekrarlandığı; hedef ve ödül ölçeklemesinin girdisi.</summary>
        public int cycle;

        public double progress;

        /// <summary>
        /// Görev başlarken sabitlenen ödül. Ödül o anki gelire bağlı olabildiği
        /// için yeniden hesaplanmıyor; oyuncu uygulamayı kapatıp açınca ödül
        /// değişmesin.
        /// </summary>
        public double reward;

        /// <summary>Hedefe ulaşılmış, ödül henüz alınmamış.</summary>
        public bool awaitingClaim;

        /// <summary>Tekrarsız zincirin son görevi de bitti.</summary>
        public bool chainFinished;

        public int completedCount;

        /// <summary>Kayıtta bir görev konumu var mı (v1 kayıtlarında ve yeni oyunda yok).</summary>
        public bool HasPosition => chainFinished || chainIndex > 0 || cycle > 0 || !string.IsNullOrEmpty(questId);

        public void Sanitize()
        {
            if (chainIndex < 0)
            {
                chainIndex = 0;
            }

            if (cycle < 0)
            {
                cycle = 0;
            }

            if (completedCount < 0)
            {
                completedCount = 0;
            }

            progress = SaveData.SanitizeAmount(progress);
            reward = SaveData.SanitizeAmount(reward);
        }
    }

    /// <summary>
    /// Ses ve titreşim tercihleri. İlerlemeyle aynı dosyada durur; "İlerlemeyi
    /// sıfırla" bu tercihleri de varsayılana döndürür.
    /// </summary>
    [Serializable]
    public sealed class AudioSaveData
    {
        /// <summary>
        /// Bu bölüm gerçekten yazıldı mı. v3 öncesi kayıtlarda false okunur;
        /// o zaman ses sistemi Inspector'daki varsayılanları kullanır. Aksi
        /// halde eski oyuncular ses seviyesi 0 ile başlayabilirdi.
        /// </summary>
        public bool hasSettings;

        public float volume = 1f;
        public bool muted;
        public bool hapticsEnabled = true;

        /// <summary>
        /// Müzik alanları yazıldı mı. Müzik desteğinden önceki kayıtlarda false
        /// okunur; müzik o zaman Inspector'daki varsayılan seviye ve açık/kapalı
        /// ayarıyla başlar (JsonUtility'nin boş alan için verdiği değerle değil).
        /// </summary>
        public bool hasMusicSettings;

        public float musicVolume = 1f;
        public bool musicMuted;

        public void Sanitize()
        {
            volume = Clamp01OrDefault(volume);
            musicVolume = Clamp01OrDefault(musicVolume);
        }

        private static float Clamp01OrDefault(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }

            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
