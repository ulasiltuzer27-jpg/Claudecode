using System;
using System.Collections.Generic;
using IdleRestaurant.Ads;
using IdleRestaurant.Audio;
using IdleRestaurant.Data;
using IdleRestaurant.Gameplay;
using IdleRestaurant.Gameplay.Customers;
using IdleRestaurant.Gameplay.Quests;
using IdleRestaurant.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleRestaurant.Core
{
    /// <summary>
    /// Oyunu başlatan ve parçaları birbirine bağlayan ana koordinatör.
    ///
    /// ── Başlatma sırası ─────────────────────────────────────────────────────
    /// Tüm başlatma tek bir yerde, <see cref="Start"/>'ta ve sabit sırayla:
    /// kayıt oku → bakiyeyi kur → istasyonları seviyeleriyle başlat →
    /// prestij, görev ve ses sistemlerini bağla ve geri yükle → kayıt
    /// sağlayıcısını bağla → UI'ı bağla → ses olaylarını ve müşteri akışını
    /// başlat → çevrimdışı kazancı ver → hemen kaydet. Parçalar
    /// birbirinin Awake/Start'ına güvenmiyor; Unity'nin bileşenler arası
    /// çağrı sırası tanımsız.
    ///
    /// ── Hız ve gelir çarpanları ─────────────────────────────────────────────
    /// İstasyonlara giden hız = reklam hızlandırıcısı × kalıcı hız (prestij);
    /// gelir = kalıcı gelir çarpanı. İkisini birleştiren tek yer
    /// <see cref="RefreshStationMultipliers"/>; çarpanı üreten sistemler
    /// (reklam, prestij) istasyonlara doğrudan dokunmaz.
    ///
    /// ── Sahne ömrü ──────────────────────────────────────────────────────────
    /// DontDestroyOnLoad KULLANILMIYOR: GameManager sahnedeki istasyonlara
    /// bağlı, sahne değişince o referanslar zaten ölür. Tek sahneli bir idle
    /// oyunda singleton'ın görevi yalnızca her yerden erişim.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Yöneticiler (boşsa aynı nesnede, sonra sahnede aranır)")]
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private AdManager adManager;
        [SerializeField] private UIManager uiManager;

        [Tooltip("İsteğe bağlı. Yoksa prestij ve kalıcı çarpanlar devre dışı.")]
        [SerializeField] private PrestigeManager prestigeManager;

        [Tooltip("İsteğe bağlı. Yoksa görev sistemi devre dışı.")]
        [SerializeField] private QuestManager questManager;

        [Tooltip("İsteğe bağlı. Yoksa ses ve titreşim yok.")]
        [SerializeField] private AudioManager audioManager;

        [Tooltip("İsteğe bağlı. Oyun olaylarını AudioManager'a bağlar; yoksa ses yalnızca elle çalınır.")]
        [SerializeField] private AudioEventBinder audioEventBinder;

        [Tooltip("İsteğe bağlı. Yoksa müşteri akışı yok.")]
        [SerializeField] private CustomerSpawner customerSpawner;

        [Header("İstasyonlar")]
        [Tooltip("Boş bırakılırsa sahnedeki tüm Station bileşenleri (pasifler dahil) otomatik bulunur.")]
        [SerializeField] private List<Station> stations = new List<Station>();

        [Header("Ödüllü reklam: hızlandırıcı")]
        [SerializeField, Min(1f)] private float speedBoostMultiplier = 2f;
        [SerializeField, Min(1f)] private float speedBoostDuration = 30f;

        [Tooltip("Arka arkaya izlenen reklamlarla birikebilecek en uzun hızlandırıcı süresi.")]
        [SerializeField, Min(1f)] private float maxSpeedBoostDuration = 300f;

        [Header("Mobil")]
        [SerializeField] private bool forcePortrait = true;

        [Tooltip("Mobilde varsayılan 30 FPS'tir; ilerleme çubukları 60'ta akıcı görünür. 0 = dokunma.")]
        [SerializeField, Min(0)] private int targetFrameRate = 60;

        /// <summary>
        /// Kayıtta olup sahnede karşılığı olmayan istasyonlar. Geçici olarak
        /// kaldırılan bir istasyonun seviyesi, bir sonraki kayıtta sessizce
        /// silinmesin diye aynen geri yazılıyor.
        /// </summary>
        private readonly List<StationSaveData> _orphanedStationEntries = new List<StationSaveData>();

        private QuestSignalHub _questSignals;
        private double _totalIncomePerSecond;
        private float _speedBoostRemaining;
        private double _pendingOfflineBonus;
        private double _pendingOfflineSeconds;
        private bool _isInitialized;
        private bool _isDuplicate;

        /// <summary>
        /// <see cref="TotalIncomePerSecond"/> değiştiğinde tetiklenir (seviye,
        /// kalıcı çarpan veya prestij sıfırlaması).
        /// </summary>
        public event Action<double> onIncomePerSecondChanged;

        /// <summary>
        /// Çevrimdışı kazanç bakiyeye eklendiğinde tetiklenir: (toplam tutar,
        /// hesaba katılan saniye). Popup henüz kapatılmadan ikinci bir dönüş
        /// olursa değerler birikir.
        /// </summary>
        public event Action<double, double> onOfflineEarningsGranted;

        /// <summary>Hızlandırıcı başladığında (true) veya bittiğinde (false) tetiklenir.</summary>
        public event Action<bool> onSpeedBoostChanged;

        /// <summary>Başlatma tamamlandığında bir kez tetiklenir.</summary>
        public event Action onGameInitialized;

        public CurrencyManager Currency => currencyManager;
        public SaveManager SaveSystem => saveManager;
        public AdManager Ads => adManager;
        public UIManager UI => uiManager;

        /// <summary>Sahnede prestij sistemi yoksa null.</summary>
        public PrestigeManager Prestige => prestigeManager;

        /// <summary>Sahnede görev sistemi yoksa null.</summary>
        public QuestManager Quests => questManager;

        /// <summary>Sahnede ses sistemi yoksa null.</summary>
        public AudioManager Audio => audioManager;

        /// <summary>Sahnede müşteri akışı yoksa null.</summary>
        public CustomerSpawner Customers => customerSpawner;

        public IReadOnlyList<Station> Stations => stations;
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Saniye başı toplam gelir: kalıcı (prestij) çarpanlar dahil, reklam
        /// hızlandırıcısı hariç. Çevrimdışı hesabın tabanı.
        /// </summary>
        public double TotalIncomePerSecond => _totalIncomePerSecond;

        /// <summary>Prestij yükseltmelerinden gelen kalıcı hız çarpanı; sistem yoksa 1.</summary>
        public float PermanentSpeedMultiplier => prestigeManager != null ? prestigeManager.SpeedMultiplier : 1f;

        /// <summary>Prestij yükseltmelerinden gelen kalıcı gelir çarpanı; sistem yoksa 1.</summary>
        public double PermanentIncomeMultiplier => prestigeManager != null ? prestigeManager.IncomeMultiplier : 1d;

        /// <summary>Hızlandırıcı dahil, şu an gerçekte kazanılan saniye başı gelir.</summary>
        public double EffectiveIncomePerSecond => _totalIncomePerSecond * CurrentSpeedMultiplier;

        public bool IsSpeedBoostActive => _speedBoostRemaining > 0f;
        public float SpeedBoostRemaining => _speedBoostRemaining;
        public float SpeedBoostDuration => speedBoostDuration;
        public float SpeedBoostMultiplier => speedBoostMultiplier;
        public float CurrentSpeedMultiplier => IsSpeedBoostActive ? speedBoostMultiplier : 1f;

        /// <summary>Bir reklam daha izlenirse hızlandırıcı süresine ekleme yapılabilir mi.</summary>
        public bool CanExtendSpeedBoost => _speedBoostRemaining < maxSpeedBoostDuration - 1f;

        /// <summary>Ödüllü reklamla ikinci kez eklenebilecek çevrimdışı kazanç; yoksa 0.</summary>
        public double PendingOfflineBonus => _pendingOfflineBonus;
        public double PendingOfflineSeconds => _pendingOfflineSeconds;

        // ── Unity yaşam döngüsü ────────────────────────────────────────────────

        /// <summary>
        /// "Enter Play Mode Options" ile domain reload kapatıldığında statik
        /// alanlar oturumlar arasında yaşar; önceki oturumun yok edilmiş
        /// GameManager'ı Instance'ta kalmasın.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[GameManager] Sahnede ikinci bir GameManager var ('{name}'); bu kopya kaldırılıyor.", this);
                _isDuplicate = true;
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            ApplyMobileSettings();
            ResolveReferences();
        }

        private void Start()
        {
            if (_isDuplicate)
            {
                return;
            }

            InitializeGame();
        }

        private void Update()
        {
            TickSpeedBoost(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_isDuplicate)
            {
                return;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null)
                {
                    stations[i].onLevelChanged -= HandleStationLevelChanged;
                }
            }

            if (prestigeManager != null)
            {
                prestigeManager.onMultipliersChanged -= HandleMultipliersChanged;
                prestigeManager.onPrestigePerformed -= HandlePrestigePerformed;
                prestigeManager.onUpgradePurchased -= HandlePermanentUpgradePurchased;
            }

            if (_questSignals != null)
            {
                _questSignals.Dispose();
                _questSignals = null;
            }

            if (audioManager != null)
            {
                audioManager.onSettingsChanged -= HandleAudioSettingsChanged;
            }

            if (saveManager != null)
            {
                // Yok olmuş sahneden kayıt alınmasın: OnDestroy sırası tanımsız,
                // istasyonlar bizden önce gitmiş olabilir.
                saveManager.SetStateProvider(null);
                saveManager.onResumedAfterPause -= HandleResumedAfterPause;
                saveManager.UnregisterSaveable(prestigeManager);
                saveManager.UnregisterSaveable(questManager);
                saveManager.UnregisterSaveable(audioManager);
            }
        }

        // ── Başlatma ───────────────────────────────────────────────────────────

        private void ApplyMobileSettings()
        {
            if (targetFrameRate > 0)
            {
                Application.targetFrameRate = targetFrameRate;
            }

            if (!forcePortrait)
            {
                return;
            }

            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private void ResolveReferences()
        {
            currencyManager = Resolve(currencyManager);
            saveManager = Resolve(saveManager);
            adManager = Resolve(adManager);
            uiManager = Resolve(uiManager);
            prestigeManager = Resolve(prestigeManager);
            questManager = Resolve(questManager);
            audioManager = Resolve(audioManager);
            audioEventBinder = Resolve(audioEventBinder);
            customerSpawner = Resolve(customerSpawner);

            stations.RemoveAll(station => station == null);
            if (stations.Count == 0)
            {
                stations.AddRange(FindAllStationsInScene());
            }
        }

        private void InitializeGame()
        {
            if (currencyManager == null || saveManager == null)
            {
                Debug.LogError("[GameManager] CurrencyManager ve SaveManager zorunlu; oyun başlatılamadı.", this);
                enabled = false;
                return;
            }

            if (adManager == null)
            {
                Debug.LogWarning("[GameManager] AdManager bulunamadı; ödüllü reklam butonları çalışmayacak.", this);
            }

            WarnAboutDuplicateStationIds();

            SaveData save;
            bool hasSave = saveManager.TryLoad(out save);

            SaveData state = hasSave ? save : null;

            currencyManager.SetBalance(hasSave ? save.currency : currencyManager.StartingBalance);
            currencyManager.SetLifetimeEarnings(hasSave ? save.lifetimeEarnings : 0d);
            InitializeStations(state);
            InitializeProgressionSystems();

            // Görev ödülleri geri yüklenirken o anki geliri okur; gelir,
            // prestij çarpanları geri yüklenmeden önce de geçerli olmalı.
            RefreshStationMultipliers();
            RecalculateIncomePerSecond();

            // Kayıt sırası: prestij (çarpanları duyurur) → görevler → ses.
            saveManager.RestoreSaveables(state);

            if (audioManager != null)
            {
                // Geri yüklemeden SONRA: yüklemenin kendi ayar olayı kayıt tetiklemesin.
                audioManager.onSettingsChanged += HandleAudioSettingsChanged;
            }

            if (hasSave && save.speedBoostRemainingSeconds > 0f)
            {
                ActivateSpeedBoost(save.speedBoostRemainingSeconds);
            }

            saveManager.SetStateProvider(CaptureSaveData);
            saveManager.onResumedAfterPause += HandleResumedAfterPause;

            if (uiManager != null)
            {
                uiManager.Initialize(this);
            }

            StartPresentationSystems();

            _isInitialized = true;
            onGameInitialized?.Invoke();

            if (hasSave)
            {
                GrantOfflineEarnings(SaveManager.GetSecondsSince(save.lastExitUtcTicks));
            }

            // Hemen kaydet: çıkış zamanı şimdiye çekilir. Aksi halde bir çökme,
            // bir sonraki açılışta aynı çevrimdışı süreyi İKİNCİ kez ödetirdi.
            saveManager.Save();
        }

        private void InitializeStations(SaveData save)
        {
            HashSet<string> sceneIds = new HashSet<string>();

            for (int i = 0; i < stations.Count; i++)
            {
                Station station = stations[i];
                int level = station.StartingLevel;

                if (save != null && save.TryGetStationLevel(station.StationId, out int savedLevel))
                {
                    level = savedLevel;
                }

                station.onLevelChanged -= HandleStationLevelChanged;
                station.onLevelChanged += HandleStationLevelChanged;
                station.Initialize(currencyManager, level);
                station.SetSpeedMultiplier(1f);
                sceneIds.Add(station.StationId);
            }

            _orphanedStationEntries.Clear();
            if (save == null)
            {
                return;
            }

            for (int i = 0; i < save.stations.Count; i++)
            {
                StationSaveData entry = save.stations[i];
                if (!sceneIds.Contains(entry.stationId))
                {
                    _orphanedStationEntries.Add(entry);
                }
            }
        }

        /// <summary>
        /// Prestij ve görev sistemlerine bağımlılıklarını verir, olaylarına
        /// abone olur ve onları (ses ayarlarıyla birlikte) kayda katar. Kayıt
        /// sırası geri yükleme sırasıdır: görevler prestij çarpanlarını içeren
        /// geliri okur; ses bağımsız, sona kaydolur.
        /// </summary>
        private void InitializeProgressionSystems()
        {
            if (prestigeManager != null)
            {
                prestigeManager.Initialize(currencyManager, stations);
                prestigeManager.onMultipliersChanged += HandleMultipliersChanged;
                prestigeManager.onPrestigePerformed += HandlePrestigePerformed;
                prestigeManager.onUpgradePurchased += HandlePermanentUpgradePurchased;
                saveManager.RegisterSaveable(prestigeManager);
            }

            if (questManager != null)
            {
                _questSignals = new QuestSignalHub(currencyManager, stations, prestigeManager);
                questManager.Initialize(currencyManager, _questSignals, () => _totalIncomePerSecond);
                saveManager.RegisterSaveable(questManager);
            }

            if (audioManager != null)
            {
                saveManager.RegisterSaveable(audioManager);
            }
        }

        /// <summary>
        /// Oyun durumu tamamen kurulduktan sonra başlayan sistemler: ses
        /// olayları (yükleme sırasındaki seviye/bakiye ayarları ses
        /// çıkarmasın) ve müşteri akışı (masalar istasyon seviyesine bakar).
        /// Çevrimdışı kazançtan önce çağrılır ki "para toplama" sesi duyulsun.
        /// </summary>
        private void StartPresentationSystems()
        {
            if (audioEventBinder != null)
            {
                if (audioManager != null)
                {
                    audioEventBinder.Bind(audioManager, this);
                }
                else
                {
                    Debug.LogWarning("[GameManager] AudioEventBinder var ama AudioManager yok; oyun olayları ses çıkarmayacak.", this);
                }
            }

            if (customerSpawner != null)
            {
                customerSpawner.Initialize(currencyManager);
            }
        }

        private void HandleAudioSettingsChanged()
        {
            // Ayar değişikliği seyrek; hemen yazılır ki uygulama kapanırsa kaybolmasın.
            saveManager.Save();
        }

        private void WarnAboutDuplicateStationIds()
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < stations.Count; i++)
            {
                Station station = stations[i];
                if (station.Data == null)
                {
                    continue;
                }

                if (!seen.Add(station.StationId))
                {
                    Debug.LogError($"[GameManager] '{station.StationId}' kimliği birden fazla istasyonda kullanılıyor; " +
                                   "kayıtta bu istasyonların seviyeleri birbirine karışır.", station);
                }
            }
        }

        // ── Kayıt ──────────────────────────────────────────────────────────────

        private SaveData CaptureSaveData()
        {
            // Prestij ve görev bölümlerini SaveManager, kaydolan ISaveable'lardan ekler.
            SaveData data = new SaveData
            {
                currency = currencyManager.Balance,
                lifetimeEarnings = currencyManager.LifetimeEarnings,
                speedBoostRemainingSeconds = _speedBoostRemaining
            };

            for (int i = 0; i < stations.Count; i++)
            {
                Station station = stations[i];
                if (station == null || !station.IsInitialized)
                {
                    continue;
                }

                data.stations.Add(new StationSaveData(station.StationId, station.Level));
            }

            data.stations.AddRange(_orphanedStationEntries);
            return data;
        }

        /// <summary>Kayıt dosyalarını siler ve sahneyi baştan yükler.</summary>
        [ContextMenu("Reset Progress")]
        public void ResetProgress()
        {
            if (saveManager == null)
            {
                return;
            }

            // Sağlayıcı önce çözülmeli: sahne kapanırken gelen bir kayıt,
            // silinen ilerlemeyi geri yazardı.
            saveManager.SetStateProvider(null);
            saveManager.DeleteSave();

            if (!Application.isPlaying)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError($"[GameManager] '{scene.name}' sahnesi Build Settings'te değil; kayıt silindi ama " +
                               "sahne yeniden yüklenemedi. Sahneyi File → Build Settings'e ekleyin.", this);
                return;
            }

            SceneManager.LoadScene(scene.buildIndex);
        }

        // ── Gelir ──────────────────────────────────────────────────────────────

        private void HandleStationLevelChanged(Station station)
        {
            RecalculateIncomePerSecond();
        }

        private void RecalculateIncomePerSecond()
        {
            double total = 0d;

            for (int i = 0; i < stations.Count; i++)
            {
                Station station = stations[i];
                if (station != null && station.IsInitialized)
                {
                    total += station.IncomePerSecond;
                }
            }

            // İstasyonun IncomePerSecond'ı hız çarpanı içermez; kalıcı olanı
            // burada uygulanıyor, geçici reklam hızlandırıcısı bilerek dışarıda.
            _totalIncomePerSecond = total * PermanentSpeedMultiplier;
            onIncomePerSecondChanged?.Invoke(_totalIncomePerSecond);
        }

        // ── Prestij ────────────────────────────────────────────────────────────

        private void HandleMultipliersChanged()
        {
            RefreshStationMultipliers();
            RecalculateIncomePerSecond();
        }

        private void HandlePrestigePerformed(double gemsEarned)
        {
            // Sıfırlanan turun çevrimdışı kazancı yeni turda 2x'lenmesin.
            ClearPendingOfflineEarnings();
            saveManager.Save();
        }

        private void HandlePermanentUpgradePurchased(PermanentUpgradeDefinition upgrade, int newLevel)
        {
            // Gem harcaması hemen yazılır; çökme satın alınanı geri almasın.
            saveManager.Save();
        }

        /// <summary>
        /// İstasyonlara giden çarpanları yeniden kurar: hız = reklam
        /// hızlandırıcısı × kalıcı hız, gelir = kalıcı gelir.
        /// </summary>
        private void RefreshStationMultipliers()
        {
            float speed = CurrentSpeedMultiplier * PermanentSpeedMultiplier;
            double income = PermanentIncomeMultiplier;

            for (int i = 0; i < stations.Count; i++)
            {
                Station station = stations[i];
                if (station != null)
                {
                    station.SetSpeedMultiplier(speed);
                    station.SetIncomeMultiplier(income);
                }
            }
        }

        // ── Çevrimdışı kazanç ──────────────────────────────────────────────────

        private void HandleResumedAfterPause(double secondsAway)
        {
            if (!_isInitialized)
            {
                return;
            }

            // Android'de tam ekran reklam ayrı bir Activity açar ve Unity
            // duraklatılır. Reklamdan dönüş bir "yokluk" değildir. Reklamın
            // kapanma geri çağrısı dönüşten önce de gelebildiği için yalnızca
            // IsShowingAd'e bakmak yetmiyor.
            if (adManager != null && (adManager.IsShowingAd || adManager.WasShowingAdWhenPaused))
            {
                return;
            }

            if (GrantOfflineEarnings(secondsAway))
            {
                saveManager.Save();
            }
        }

        /// <summary>
        /// Çevrimdışı kazancı hesaplar ve HEMEN bakiyeye ekler. Popup yalnızca
        /// bilgi ve 2x fırsatı sunar; oyuncu popup açıkken uygulamayı
        /// kapatsa bile kazancını kaybetmez.
        /// </summary>
        private bool GrantOfflineEarnings(double secondsAway)
        {
            double amount = saveManager.CalculateOfflineEarnings(secondsAway, _totalIncomePerSecond);
            if (amount <= 0d)
            {
                return false;
            }

            currencyManager.AddCurrency(amount);
            _pendingOfflineBonus = Math.Min(_pendingOfflineBonus + amount, CurrencyManager.MaxBalance);
            _pendingOfflineSeconds += saveManager.ClampOfflineSeconds(secondsAway);
            onOfflineEarningsGranted?.Invoke(_pendingOfflineBonus, _pendingOfflineSeconds);
            return true;
        }

        /// <summary>
        /// Ödüllü reklam izletir; izlenirse çevrimdışı kazancın bir katı daha
        /// eklenir (toplamda 2x). Sonuç <paramref name="onComplete"/> ile döner.
        /// </summary>
        public void RequestDoubleOfflineEarnings(Action<bool> onComplete)
        {
            if (_pendingOfflineBonus <= 0d || adManager == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            adManager.ShowRewardedAd(
                () =>
                {
                    double bonus = _pendingOfflineBonus;
                    ClearPendingOfflineEarnings();

                    bool granted = bonus > 0d && currencyManager.AddCurrency(bonus);
                    if (granted)
                    {
                        saveManager.Save();
                    }

                    onComplete?.Invoke(granted);
                },
                () => onComplete?.Invoke(false));
        }

        /// <summary>Oyuncu popup'ı 2x almadan kapattı; ek ödül hakkı düşer.</summary>
        public void ClearPendingOfflineEarnings()
        {
            _pendingOfflineBonus = 0d;
            _pendingOfflineSeconds = 0d;
        }

        // ── Hızlandırıcı ───────────────────────────────────────────────────────

        /// <summary>
        /// Ödüllü reklam izletir; izlenirse tüm istasyonlar
        /// <see cref="SpeedBoostDuration"/> saniye boyunca
        /// <see cref="SpeedBoostMultiplier"/> kat hızlı üretir.
        /// </summary>
        public void RequestSpeedBoost(Action<bool> onComplete)
        {
            if (adManager == null || !CanExtendSpeedBoost)
            {
                onComplete?.Invoke(false);
                return;
            }

            adManager.ShowRewardedAd(
                () =>
                {
                    ActivateSpeedBoost(speedBoostDuration);
                    onComplete?.Invoke(true);
                },
                () => onComplete?.Invoke(false));
        }

        /// <summary>
        /// Hızlandırıcıyı başlatır veya süresini uzatır (üst sınır
        /// maxSpeedBoostDuration). Reklamsız ödüller (görev, IAP) de bunu
        /// doğrudan çağırabilir.
        /// </summary>
        public void ActivateSpeedBoost(float duration)
        {
            if (float.IsNaN(duration) || duration <= 0f)
            {
                return;
            }

            bool wasActive = IsSpeedBoostActive;
            _speedBoostRemaining = Mathf.Min(_speedBoostRemaining + duration, maxSpeedBoostDuration);

            if (!wasActive)
            {
                RefreshStationMultipliers();
                onSpeedBoostChanged?.Invoke(true);
            }
        }

        private void TickSpeedBoost(float deltaTime)
        {
            if (_speedBoostRemaining <= 0f)
            {
                return;
            }

            _speedBoostRemaining -= deltaTime;
            if (_speedBoostRemaining > 0f)
            {
                return;
            }

            _speedBoostRemaining = 0f;
            RefreshStationMultipliers();
            onSpeedBoostChanged?.Invoke(false);
        }

        // ── Geliştirici kısayolları (Inspector'da bileşen menüsünden) ──────────

        [ContextMenu("Debug/Add 1K Currency")]
        private void DebugAddThousand()
        {
            if (currencyManager != null)
            {
                currencyManager.AddCurrency(1_000d);
            }
        }

        [ContextMenu("Debug/Add 1M Currency")]
        private void DebugAddMillion()
        {
            if (currencyManager != null)
            {
                currencyManager.AddCurrency(1_000_000d);
            }
        }

        [ContextMenu("Debug/Simulate 1h Offline")]
        private void DebugSimulateOneHourOffline()
        {
            if (_isInitialized)
            {
                GrantOfflineEarnings(3600d);
            }
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        /// <summary>
        /// Atanmamış referansı önce aynı nesnede, sonra sahnede arar. `??`
        /// KULLANILMIYOR: Unity editörde eksik bileşen için "sahte null" bir
        /// nesne döndürür; `==` onu null sayar ama `??` saymaz.
        /// </summary>
        private T Resolve<T>(T assigned) where T : Component
        {
            if (assigned != null)
            {
                return assigned;
            }

            T onSelf = GetComponent<T>();
            if (onSelf != null)
            {
                return onSelf;
            }

#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }

        private static Station[] FindAllStationsInScene()
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<Station>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
#else
            return FindObjectsOfType<Station>(true);
#endif
        }
    }
}
