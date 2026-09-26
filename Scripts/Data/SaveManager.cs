using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleRestaurant.Data
{
    /// <summary>
    /// Kaydı diske yazar/okur, uygulama yaşam döngüsünde otomatik kaydeder
    /// ve çevrimdışı kazancı hesaplar.
    ///
    /// ── Durumu kim veriyor ──────────────────────────────────────────────────
    /// SaveManager oyunun parçalarını tanımaz; kaydedilecek anlık görüntüyü
    /// <see cref="SetStateProvider"/> ile verilen bir fonksiyondan ister.
    /// Böylece OnApplicationPause gibi Unity mesajları nereden gelirse
    /// gelsin, kayıt her zaman o anki gerçek durumu yazar.
    ///
    /// Sağlayıcının kurduğu temel kayda, <see cref="RegisterSaveable"/> ile
    /// kaydolan sistemler (prestij, görevler, ...) kendi bölümlerini ekler.
    /// Yeni bir sistem bu sınıfa veya GameManager'ın kayıt koduna dokunmadan
    /// kayda katılır.
    ///
    /// ── Atomik yazım ────────────────────────────────────────────────────────
    /// Kayıt önce <c>.tmp</c> dosyasına yazılır, eski kayıt <c>.bak</c>
    /// olarak kopyalanır, sonra geçici dosya yerine taşınır. Yazımın
    /// ortasında uygulama öldürülürse (mobilde sık) elde her zaman okunabilir
    /// bir dosya kalır; <see cref="TryLoad"/> sırayla ana, geçici ve yedek
    /// dosyayı dener.
    ///
    /// ── Mobilde kayıt anı ───────────────────────────────────────────────────
    /// Android ve iOS arka plandaki uygulamayı haber vermeden öldürür;
    /// OnApplicationQuit o durumda HİÇ çağrılmaz. Güvenilir tek an
    /// OnApplicationPause(true). Quit yine de masaüstü/editör için dinleniyor,
    /// ve çökmelere karşı periyodik otomatik kayıt da var.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveManager : MonoBehaviour
    {
        private const string FileName = "savegame.json";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool PrettyPrint = true;
#else
        private const bool PrettyPrint = false;
#endif

        [Header("Otomatik kayıt")]
        [Tooltip("Oyun açıkken kaç saniyede bir kaydedileceği. 0 = yalnızca duraklatma/çıkışta.")]
        [SerializeField, Min(0f)] private float autoSaveInterval = 30f;

        [Header("Çevrimdışı kazanç")]
        [Tooltip("Çevrimdışı gelir = geçen saniye * saniye başı gelir * bu oran.")]
        [SerializeField, Range(0f, 1f)] private float offlineEarningsRate = 0.5f;

        [Tooltip("Bundan kısa yokluklar çevrimdışı kazanç vermez (ör. reklam izlerken veya " +
                 "bildirime bakarken uygulamanın kısa süre arka plana düşmesi).")]
        [SerializeField, Min(0f)] private float minOfflineSeconds = 60f;

        [Tooltip("Hesaba katılan en uzun yokluk (saat). Cihaz saatini ileri alarak sınırsız " +
                 "para kazanmayı engeller. 0 = sınırsız.")]
        [SerializeField, Min(0f)] private float maxOfflineHours = 12f;

        private readonly List<ISaveable> _saveables = new List<ISaveable>();
        private Func<SaveData> _stateProvider;
        private float _autoSaveTimer;
        private long _pausedAtUtcTicks;
        private string _savePath;
        private string _tempPath;
        private string _backupPath;

        /// <summary>
        /// Uygulama arka plandan döndüğünde, arka planda geçen saniyeyle
        /// tetiklenir. Açık kalan (öldürülmeyen) bir uygulama da çevrimdışı
        /// kazanç almalı; aksi halde oyuncu uygulamayı kapatıp açmaya
        /// zorlanırdı.
        /// </summary>
        public event Action<double> onResumedAfterPause;

        public string SavePath
        {
            get
            {
                EnsurePaths();
                return _savePath;
            }
        }

        public bool HasSaveFile
        {
            get
            {
                EnsurePaths();
                return File.Exists(_savePath) || File.Exists(_tempPath) || File.Exists(_backupPath);
            }
        }

        public float OfflineEarningsRate => offlineEarningsRate;
        public float MinOfflineSeconds => minOfflineSeconds;

        /// <summary>Hesaba katılan en uzun yokluk (saniye); sınır yoksa PositiveInfinity.</summary>
        public double MaxOfflineSeconds => maxOfflineHours > 0f ? maxOfflineHours * 3600d : double.PositiveInfinity;

        /// <summary>
        /// Kaydedilecek durumu üreten fonksiyonu bağlar. Bağlanana kadar hiçbir
        /// kayıt yazılmaz: yükleme bitmeden gelen bir OnApplicationPause'un
        /// boş bir durumu gerçek kaydın üstüne yazması böyle engelleniyor.
        /// </summary>
        public void SetStateProvider(Func<SaveData> provider)
        {
            _stateProvider = provider;
            _autoSaveTimer = 0f;
        }

        /// <summary>
        /// Bir sistemi kayda katar. Kayıt sırası geri yükleme sırasıdır:
        /// başka bir sistemin durumuna bağlı olan sistem sonra kaydolmalı.
        /// </summary>
        public void RegisterSaveable(ISaveable saveable)
        {
            if (saveable != null && !_saveables.Contains(saveable))
            {
                _saveables.Add(saveable);
            }
        }

        public void UnregisterSaveable(ISaveable saveable)
        {
            _saveables.Remove(saveable);
        }

        /// <summary>
        /// Kaydolan tüm sistemlerin durumunu kayıt sırasıyla geri yükler.
        /// <paramref name="data"/> null ise (ilk açılış) her sistem varsayılanla başlar.
        /// </summary>
        public void RestoreSaveables(SaveData data)
        {
            for (int i = 0; i < _saveables.Count; i++)
            {
                try
                {
                    _saveables[i].RestoreState(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] {_saveables[i].GetType().Name} geri yüklenemedi: {e}");
                }
            }
        }

        // ── Kaydetme ───────────────────────────────────────────────────────────

        /// <summary>Anlık durumu diske yazar. Durum sağlayıcı yoksa veya yazım başarısızsa false.</summary>
        public bool Save()
        {
            if (_stateProvider == null)
            {
                return false;
            }

            SaveData data;
            try
            {
                data = _stateProvider();
                if (data == null)
                {
                    return false;
                }

                // Bir sistem hata verirse kayıt YAZILMAZ: o sistemin bölümü
                // varsayılanla diske gidip önceki geçerli veriyi ezerdi.
                for (int i = 0; i < _saveables.Count; i++)
                {
                    _saveables[i].CaptureState(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Kaydedilecek durum alınamadı: {e}");
                return false;
            }

            data.version = SaveData.CurrentVersion;
            data.lastExitUtcTicks = DateTime.UtcNow.Ticks;
            data.Sanitize();

            _autoSaveTimer = 0f;
            return WriteToDisk(data);
        }

        private bool WriteToDisk(SaveData data)
        {
            EnsurePaths();

            try
            {
                string json = JsonUtility.ToJson(data, PrettyPrint);
                File.WriteAllText(_tempPath, json);

                if (File.Exists(_savePath))
                {
                    File.Copy(_savePath, _backupPath, true);
                    File.Delete(_savePath);
                }

                File.Move(_tempPath, _savePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Kayıt yazılamadı ({_savePath}): {e}");
                return false;
            }
        }

        // ── Yükleme ────────────────────────────────────────────────────────────

        /// <summary>
        /// Kaydı okur. Ana dosya bozuk veya eksikse geçici dosyayı, o da
        /// olmazsa yedeği dener.
        /// </summary>
        /// <returns>Okunabilir bir kayıt bulunduysa true; ilk açılışta veya tüm dosyalar bozuksa false.</returns>
        public bool TryLoad(out SaveData data)
        {
            EnsurePaths();

            if (TryRead(_savePath, out data))
            {
                return true;
            }

            if (TryRead(_tempPath, out data))
            {
                Debug.LogWarning("[SaveManager] Ana kayıt okunamadı; yarım kalan yazımın geçici dosyasından kurtarıldı.");
                return true;
            }

            if (TryRead(_backupPath, out data))
            {
                Debug.LogWarning("[SaveManager] Ana kayıt okunamadı; yedekten yüklendi.");
                return true;
            }

            if (File.Exists(_savePath))
            {
                PreserveCorruptFile();
            }

            data = null;
            return false;
        }

        private static bool TryRead(string path, out SaveData data)
        {
            data = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                SaveData parsed = JsonUtility.FromJson<SaveData>(json);
                if (parsed == null)
                {
                    return false;
                }

                if (parsed.version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"[SaveManager] Kayıt daha yeni bir sürümden (v{parsed.version}); " +
                                     "tanınmayan alanlar bir sonraki kayıtta kaybolacak.");
                }

                parsed.UpgradeToCurrentVersion();
                parsed.Sanitize();
                data = parsed;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] '{path}' okunamadı: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hiçbir kopya okunamadıysa bozuk dosyayı kenara alır. Aksi halde bir
        /// sonraki kayıt onu .bak'ın üstüne kopyalar ve destek için elde hiçbir
        /// iz kalmaz.
        /// </summary>
        private void PreserveCorruptFile()
        {
            try
            {
                string corruptPath = _savePath + ".corrupt";
                File.Copy(_savePath, corruptPath, true);
                Debug.LogError($"[SaveManager] Kayıt bozuk, yeni oyun başlatılıyor. Bozuk dosya: {corruptPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Bozuk kayıt kenara alınamadı: {e.Message}");
            }
        }

        /// <summary>Tüm kayıt dosyalarını siler. Geliştirme ve "ilerlemeyi sıfırla" için.</summary>
        [ContextMenu("Delete Save Files")]
        public void DeleteSave()
        {
            EnsurePaths();

            try
            {
                DeleteIfExists(_savePath);
                DeleteIfExists(_tempPath);
                DeleteIfExists(_backupPath);
                Debug.Log("[SaveManager] Kayıt dosyaları silindi.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Kayıt silinemedi: {e.Message}");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // ── Çevrimdışı kazanç ──────────────────────────────────────────────────

        /// <summary>
        /// Verilen UTC tick'ten bu yana geçen saniye. Cihaz saati geri
        /// alındıysa (şimdi &lt; kayıt anı) 0 döner; negatif bir süre para
        /// SİLERDİ.
        /// </summary>
        public static double GetSecondsSince(long utcTicks)
        {
            if (utcTicks <= 0L)
            {
                return 0d;
            }

            long delta = DateTime.UtcNow.Ticks - utcTicks;
            return delta > 0L ? delta / (double)TimeSpan.TicksPerSecond : 0d;
        }

        /// <summary>Yokluk süresini hesaba katılan aralığa kırpar.</summary>
        public double ClampOfflineSeconds(double secondsAway)
        {
            if (double.IsNaN(secondsAway) || secondsAway <= 0d)
            {
                return 0d;
            }

            return Math.Min(secondsAway, MaxOfflineSeconds);
        }

        /// <summary>
        /// Çevrimdışı kazanç = geçen saniye * saniye başı toplam gelir *
        /// <see cref="OfflineEarningsRate"/> (varsayılan 0.5). Yokluk
        /// <see cref="MinOfflineSeconds"/>'tan kısaysa 0.
        /// </summary>
        public double CalculateOfflineEarnings(double secondsAway, double incomePerSecond)
        {
            if (secondsAway < minOfflineSeconds)
            {
                return 0d;
            }

            if (double.IsNaN(incomePerSecond) || double.IsInfinity(incomePerSecond) || incomePerSecond <= 0d)
            {
                return 0d;
            }

            double earnings = ClampOfflineSeconds(secondsAway) * incomePerSecond * offlineEarningsRate;

            if (double.IsNaN(earnings) || earnings <= 0d)
            {
                return 0d;
            }

            return double.IsInfinity(earnings) ? double.MaxValue : earnings;
        }

        // ── Unity yaşam döngüsü ────────────────────────────────────────────────

        private void Awake()
        {
            EnsurePaths();
        }

        private void Update()
        {
            if (_stateProvider == null || autoSaveInterval <= 0f)
            {
                return;
            }

            // unscaled: timeScale 0 (duraklatma menüsü) otomatik kaydı durdurmamalı.
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _pausedAtUtcTicks = DateTime.UtcNow.Ticks;
                Save();
                return;
            }

            // Unity açılışta da OnApplicationPause(false) gönderir; önceden bir
            // duraklatma kaydedilmediyse bu bir dönüş değildir.
            if (_pausedAtUtcTicks <= 0L)
            {
                return;
            }

            double secondsAway = GetSecondsSince(_pausedAtUtcTicks);
            _pausedAtUtcTicks = 0L;
            _autoSaveTimer = 0f;
            onResumedAfterPause?.Invoke(secondsAway);
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        /// <summary>
        /// Application.persistentDataPath alan başlatıcılarında çağrılamaz;
        /// Awake'ten önce gelen bir çağrı (başka bir bileşenin Awake'i) için
        /// yollar ilk kullanımda hesaplanıyor.
        /// </summary>
        private void EnsurePaths()
        {
            if (_savePath != null)
            {
                return;
            }

            _savePath = Path.Combine(Application.persistentDataPath, FileName);
            _tempPath = _savePath + ".tmp";
            _backupPath = _savePath + ".bak";
        }
    }
}
