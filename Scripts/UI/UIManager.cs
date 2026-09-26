using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using IdleRestaurant.Ads;
using IdleRestaurant.Core;
using IdleRestaurant.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static IdleRestaurant.UI.UIUtility;

namespace IdleRestaurant.UI
{
    /// <summary>
    /// Ekrandaki tüm metin ve butonları oyun durumuna bağlar.
    ///
    /// ── Olay güdümlü, kare başı değil ───────────────────────────────────────
    /// Metinler yalnızca ilgili olay geldiğinde yeniden yazılır; TMP metni
    /// değiştirmek mesh'i yeniden kurar ve her karede yapılırsa mobilde
    /// ölçülebilir maliyeti olur. Tek istisna ilerleme çubukları (yalnızca
    /// bir float) ve saniyede bir değişen hızlandırıcı sayacı.
    ///
    /// Para olayı bir karede birçok kez gelebilir (her istasyon kendi
    /// döngüsünü tamamlar). Bakiye metni ve yükseltme butonları bu yüzden
    /// "kirli" işaretlenip karenin sonunda BİR kez yenilenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour, IUIFeedback
    {
        /// <summary>Tek bir istasyonun arayüz referansları. Boş bırakılan alanlar atlanır.</summary>
        [Serializable]
        public sealed class StationView
        {
            public Station station;
            public TMP_Text nameText;
            public TMP_Text levelText;
            public TMP_Text incomeText;
            public TMP_Text upgradeCostText;
            public Button upgradeButton;

            [Tooltip("Image Type = Filled olmalı; fillAmount döngü ilerlemesini gösterir.")]
            public Image progressFill;

            [NonSerialized] public UnityAction upgradeAction;
        }

        /// <summary>Arayüz metinleri; koda dokunmadan çevrilebilsin diye Inspector'da.</summary>
        [Serializable]
        public sealed class UITexts
        {
            public string levelFormat = "Sv. {0}";
            public string incomeFormat = "+{0} / {1}sn";
            public string incomePerSecondFormat = "{0} / sn";
            public string buyFormat = "Satın Al\n{0}";
            public string upgradeFormat = "Yükselt\n{0}";
            public string maxLevel = "MAKS";
            public string lockedLevel = "Kilitli";
            public string notEnoughMoney = "Yeterli para yok";
            public string offlineAmountFormat = "+{0}";
            public string offlineDurationFormat = "{0} boyunca kazandın";
            public string doubleOffline = "2x (Reklam)";
            [Tooltip("{0} = süre (sn), {1} = hız çarpanı")]
            public string speedBoostIdle = "{1}x Hız\n{0}sn (Reklam)";

            [Tooltip("{0} = kalan süre, {1} = hız çarpanı")]
            public string speedBoostActiveFormat = "{1}x Hız\n{0}";

            [Tooltip("{0} = kalan süre, {1} = hız çarpanı")]
            public string speedBoostMax = "{1}x Hız\n{0} (Dolu)";
            public string adNotReady = "Reklam şu an hazır değil";
            public string adFailed = "Reklam tamamlanmadı, ödül verilmedi";
            public string offlineDoubled = "Çevrimdışı kazanç 2 katına çıktı!";
            public string speedBoostStarted = "Hızlandırıcı aktif!";
            public string hourShort = "sa";
            public string minuteShort = "dk";
            public string secondShort = "sn";
        }

        [Header("Para")]
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text incomePerSecondText;

        [Header("İstasyonlar")]
        [SerializeField] private List<StationView> stationViews = new List<StationView>();

        [Header("Çevrimdışı kazanç popup'ı")]
        [Tooltip("Açılıp kapatılan kök nesne. UIManager'ın kendi nesnesi veya bir üst nesnesi OLMAMALI; " +
                 "aksi halde popup kapanınca UIManager da devre dışı kalır.")]
        [SerializeField] private GameObject offlinePopup;
        [SerializeField] private TMP_Text offlineAmountText;
        [SerializeField] private TMP_Text offlineDurationText;
        [SerializeField] private Button offlineCollectButton;
        [SerializeField] private Button offlineDoubleButton;
        [SerializeField] private TMP_Text offlineDoubleButtonText;

        [Header("Hızlandırıcı")]
        [SerializeField] private Button speedBoostButton;
        [SerializeField] private TMP_Text speedBoostText;

        [Header("Bildirim")]
        [Tooltip("Kısa mesajlar için metin. Nesnesi gizlenip gösterilir; UIManager ile aynı nesnede olmamalı.")]
        [SerializeField] private TMP_Text toastText;
        [SerializeField, Min(0.5f)] private float toastDuration = 2f;

        [Header("Paneller (isteğe bağlı)")]
        [SerializeField] private QuestPanelUI questPanel;
        [SerializeField] private PrestigePanelUI prestigePanel;

        [Header("Ayarlar")]
        [Tooltip("Ayarlar panelini açıp kapatan buton (ör. ekran köşesindeki dişli).")]
        [SerializeField] private Button settingsButton;

        [SerializeField] private SettingsPanelUI settingsPanel;

        [Header("Metinler")]
        [SerializeField] private UITexts texts = new UITexts();

        private readonly Dictionary<Station, StationView> _viewsByStation = new Dictionary<Station, StationView>();

        private GameManager _game;
        private CurrencyManager _currency;
        private AdManager _ads;
        private bool _currencyDirty;
        private bool _adRequestInFlight;
        private int _lastBoostSecondsShown = -1;
        private Coroutine _toastRoutine;

        /// <summary>
        /// Oyuncu UIManager'ın veya panellerinin bir butonuna bastığında
        /// tetiklenir. Ses/titreşim bunu dinler; UI ses sistemini tanımaz.
        /// </summary>
        public event Action onButtonClicked;

        /// <summary>
        /// Paneller kendi butonları için çağırır; böylece tüm tıklamalar tek
        /// bir olaydan geçer.
        /// </summary>
        public void NotifyButtonClicked()
        {
            onButtonClicked?.Invoke();
        }

        // ── Unity yaşam döngüsü ────────────────────────────────────────────────

        private void Awake()
        {
            SetActive(offlinePopup, false);
            SetActive(toastText != null ? toastText.gameObject : null, false);
        }

        private void LateUpdate()
        {
            if (_game == null)
            {
                return;
            }

            if (_currencyDirty)
            {
                _currencyDirty = false;
                RefreshCurrency();
                RefreshAllUpgradeButtons();
            }

            UpdateProgressBars();
            UpdateSpeedBoostCountdown();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ── Bağlama ────────────────────────────────────────────────────────────

        /// <summary>
        /// <see cref="GameManager"/> tarafından, oyun durumu kurulduktan sonra
        /// çağrılır. Tekrar çağrılırsa önceki bağlantılar önce çözülür.
        /// </summary>
        public void Initialize(GameManager game)
        {
            Unbind();

            _game = game;
            _currency = game.Currency;
            _ads = game.Ads;

            _currency.onCurrencyChanged += HandleCurrencyChanged;
            _game.onIncomePerSecondChanged += HandleIncomePerSecondChanged;
            _game.onOfflineEarningsGranted += HandleOfflineEarningsGranted;
            _game.onSpeedBoostChanged += HandleSpeedBoostChanged;

            if (_ads != null)
            {
                _ads.onRewardedAdReadyChanged += HandleAdReadyChanged;
            }

            BindButton(offlineCollectButton, OnOfflineCollectClicked);
            BindButton(offlineDoubleButton, OnOfflineDoubleClicked);
            BindButton(speedBoostButton, OnSpeedBoostClicked);
            BindButton(settingsButton, OnSettingsClicked);

            BindStationViews();
            RefreshAll();

            // Paneller kendi sistemlerinin olaylarını kendileri dinler; UIManager
            // yalnızca başlatır. Sistem sahnede yoksa panel boş durumunu gösterir.
            if (questPanel != null)
            {
                questPanel.Initialize(game.Quests, this);
            }

            if (prestigePanel != null)
            {
                prestigePanel.Initialize(game.Prestige, game.Currency, this);
            }

            if (settingsPanel != null)
            {
                settingsPanel.Initialize(game.Audio, game.Ads, game.ResetProgress, this);
            }
            else if (settingsButton != null)
            {
                settingsButton.interactable = false;
                Debug.LogWarning("[UIManager] Ayarlar butonu var ama SettingsPanelUI atanmamış.", this);
            }
        }

        private void BindStationViews()
        {
            _viewsByStation.Clear();

            for (int i = 0; i < stationViews.Count; i++)
            {
                StationView view = stationViews[i];
                if (view == null || view.station == null)
                {
                    continue;
                }

                if (_viewsByStation.ContainsKey(view.station))
                {
                    Debug.LogWarning($"[UIManager] '{view.station.name}' için birden fazla görünüm var; ilki kullanılıyor.", this);
                    continue;
                }

                _viewsByStation.Add(view.station, view);
                view.station.onLevelChanged += HandleStationLevelChanged;

                if (view.upgradeButton != null)
                {
                    StationView captured = view;
                    view.upgradeAction = () => OnUpgradeClicked(captured);
                    view.upgradeButton.onClick.AddListener(view.upgradeAction);
                }
            }
        }

        private void Unbind()
        {
            if (_currency != null)
            {
                _currency.onCurrencyChanged -= HandleCurrencyChanged;
            }

            if (_game != null)
            {
                _game.onIncomePerSecondChanged -= HandleIncomePerSecondChanged;
                _game.onOfflineEarningsGranted -= HandleOfflineEarningsGranted;
                _game.onSpeedBoostChanged -= HandleSpeedBoostChanged;
            }

            if (_ads != null)
            {
                _ads.onRewardedAdReadyChanged -= HandleAdReadyChanged;
            }

            UnbindButton(offlineCollectButton, OnOfflineCollectClicked);
            UnbindButton(offlineDoubleButton, OnOfflineDoubleClicked);
            UnbindButton(speedBoostButton, OnSpeedBoostClicked);
            UnbindButton(settingsButton, OnSettingsClicked);

            foreach (KeyValuePair<Station, StationView> pair in _viewsByStation)
            {
                StationView view = pair.Value;
                if (pair.Key != null)
                {
                    pair.Key.onLevelChanged -= HandleStationLevelChanged;
                }

                if (view.upgradeButton != null && view.upgradeAction != null)
                {
                    view.upgradeButton.onClick.RemoveListener(view.upgradeAction);
                }

                view.upgradeAction = null;
            }

            _viewsByStation.Clear();
            _game = null;
            _currency = null;
            _ads = null;
        }

        // ── Olay işleyicileri ──────────────────────────────────────────────────

        private void HandleCurrencyChanged(double balance)
        {
            _currencyDirty = true;
        }

        private void HandleIncomePerSecondChanged(double incomePerSecond)
        {
            RefreshIncomePerSecond();

            // Toplam gelir seviye dışında kalıcı çarpanlarla da değişir (prestij
            // yükseltmesi); istasyon satırlarındaki gelir ve süre de yenilenmeli.
            foreach (StationView view in _viewsByStation.Values)
            {
                RefreshStationView(view);
            }
        }

        private void HandleStationLevelChanged(Station station)
        {
            StationView view;
            if (_viewsByStation.TryGetValue(station, out view))
            {
                RefreshStationView(view);
            }
        }

        private void HandleSpeedBoostChanged(bool active)
        {
            _lastBoostSecondsShown = -1;
            RefreshIncomePerSecond();
            RefreshSpeedBoost();

            // İstasyon satırlarındaki döngü süresi hızlandırıcıyla değişiyor.
            foreach (StationView view in _viewsByStation.Values)
            {
                RefreshStationView(view);
            }
        }

        private void HandleAdReadyChanged(bool ready)
        {
            RefreshAdButtons();
        }

        private void HandleOfflineEarningsGranted(double amount, double seconds)
        {
            SetText(offlineAmountText, Format(texts.offlineAmountFormat, CurrencyManager.FormatNumber(amount)));
            SetText(offlineDurationText, Format(texts.offlineDurationFormat, FormatDuration(seconds)));
            SetText(offlineDoubleButtonText, texts.doubleOffline);

            SetActive(offlinePopup, true);
            RefreshAdButtons();
        }

        // ── Buton tıklamaları ──────────────────────────────────────────────────

        private void OnUpgradeClicked(StationView view)
        {
            NotifyButtonClicked();

            if (view.station == null || view.station.IsMaxLevel)
            {
                return;
            }

            if (!view.station.TryUpgrade())
            {
                ShowToast(texts.notEnoughMoney);
            }
        }

        private void OnSettingsClicked()
        {
            NotifyButtonClicked();

            if (settingsPanel != null)
            {
                settingsPanel.Toggle();
            }
        }

        private void OnOfflineCollectClicked()
        {
            NotifyButtonClicked();

            // Para zaten eklendi; kapatmak yalnızca 2x hakkından vazgeçmek.
            _game.ClearPendingOfflineEarnings();
            SetActive(offlinePopup, false);
        }

        private void OnOfflineDoubleClicked()
        {
            NotifyButtonClicked();

            if (_adRequestInFlight)
            {
                return;
            }

            if (_ads == null || !_ads.IsRewardedAdReady)
            {
                ShowToast(texts.adNotReady);
                PreloadAd();
                return;
            }

            _adRequestInFlight = true;
            RefreshAdButtons();

            _game.RequestDoubleOfflineEarnings(success =>
            {
                _adRequestInFlight = false;

                if (success)
                {
                    SetActive(offlinePopup, false);
                    ShowToast(texts.offlineDoubled);
                }
                else
                {
                    ShowToast(texts.adFailed);
                }

                RefreshAdButtons();
            });
        }

        private void OnSpeedBoostClicked()
        {
            NotifyButtonClicked();

            if (_adRequestInFlight || !_game.CanExtendSpeedBoost)
            {
                return;
            }

            if (_ads == null || !_ads.IsRewardedAdReady)
            {
                ShowToast(texts.adNotReady);
                PreloadAd();
                return;
            }

            _adRequestInFlight = true;
            RefreshAdButtons();

            _game.RequestSpeedBoost(success =>
            {
                _adRequestInFlight = false;
                ShowToast(success ? texts.speedBoostStarted : texts.adFailed);
                RefreshSpeedBoost();
            });
        }

        // ── Yenileme ───────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            _currencyDirty = false;
            _lastBoostSecondsShown = -1;

            RefreshCurrency();
            RefreshIncomePerSecond();

            foreach (StationView view in _viewsByStation.Values)
            {
                RefreshStationView(view);
            }

            RefreshSpeedBoost();
        }

        private void RefreshCurrency()
        {
            SetText(currencyText, CurrencyManager.FormatNumber(_currency.Balance));
        }

        private void RefreshIncomePerSecond()
        {
            if (_game == null)
            {
                return;
            }

            SetText(incomePerSecondText, Format(texts.incomePerSecondFormat,
                CurrencyManager.FormatNumber(_game.EffectiveIncomePerSecond)));
        }

        private void RefreshStationView(StationView view)
        {
            Station station = view.station;
            if (station == null)
            {
                return;
            }

            SetText(view.nameText, station.DisplayName);
            SetText(view.levelText, station.IsUnlocked ? Format(texts.levelFormat, station.Level) : texts.lockedLevel);

            // Kilitliyken "ne kazandıracağını" göster: seviye 1'in geliri.
            double income = station.IsUnlocked ? station.CurrentIncome : station.NextLevelIncome;
            SetText(view.incomeText, Format(texts.incomeFormat, CurrencyManager.FormatNumber(income),
                station.CurrentCycleTime.ToString("0.##", CultureInfo.InvariantCulture)));

            if (station.IsMaxLevel)
            {
                SetText(view.upgradeCostText, texts.maxLevel);
            }
            else
            {
                string format = station.IsUnlocked ? texts.upgradeFormat : texts.buyFormat;
                SetText(view.upgradeCostText, Format(format, CurrencyManager.FormatNumber(station.UpgradeCost)));
            }

            RefreshUpgradeButton(view);

            if (view.progressFill != null)
            {
                view.progressFill.fillAmount = station.CycleProgress;
            }
        }

        private void RefreshAllUpgradeButtons()
        {
            foreach (StationView view in _viewsByStation.Values)
            {
                RefreshUpgradeButton(view);
            }
        }

        private static void RefreshUpgradeButton(StationView view)
        {
            if (view.upgradeButton != null && view.station != null)
            {
                view.upgradeButton.interactable = view.station.CanAffordUpgrade;
            }
        }

        private void UpdateProgressBars()
        {
            foreach (StationView view in _viewsByStation.Values)
            {
                if (view.progressFill != null && view.station != null)
                {
                    view.progressFill.fillAmount = view.station.CycleProgress;
                }
            }
        }

        private void UpdateSpeedBoostCountdown()
        {
            if (!_game.IsSpeedBoostActive)
            {
                return;
            }

            int seconds = Mathf.CeilToInt(_game.SpeedBoostRemaining);
            if (seconds != _lastBoostSecondsShown)
            {
                _lastBoostSecondsShown = seconds;
                RefreshSpeedBoost();
            }
        }

        private void RefreshSpeedBoost()
        {
            if (_game == null)
            {
                return;
            }

            string multiplier = _game.SpeedBoostMultiplier.ToString("0.#", CultureInfo.InvariantCulture);
            string label;
            if (!_game.IsSpeedBoostActive)
            {
                label = Format(texts.speedBoostIdle, Mathf.RoundToInt(_game.SpeedBoostDuration), multiplier);
            }
            else
            {
                string remaining = FormatDuration(Mathf.Ceil(_game.SpeedBoostRemaining));
                label = Format(_game.CanExtendSpeedBoost ? texts.speedBoostActiveFormat : texts.speedBoostMax,
                    remaining, multiplier);
            }

            SetText(speedBoostText, label);
            RefreshAdButtons();
        }

        private void RefreshAdButtons()
        {
            if (_game == null)
            {
                return;
            }

            bool adAvailable = _ads != null && !_adRequestInFlight && _ads.IsRewardedAdReady;

            if (speedBoostButton != null)
            {
                speedBoostButton.interactable = adAvailable && _game.CanExtendSpeedBoost;
            }

            if (offlineDoubleButton != null)
            {
                offlineDoubleButton.interactable = adAvailable && _game.PendingOfflineBonus > 0d;
            }
        }

        private void PreloadAd()
        {
            if (_ads != null)
            {
                _ads.PreloadRewardedAd();
            }
        }

        // ── Bildirim ───────────────────────────────────────────────────────────

        public void ShowToast(string message)
        {
            if (toastText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(toastDuration);
            toastText.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        /// <summary>Saniyeyi "2sa 15dk", "4dk 05sn" veya "30sn" biçiminde yazar.</summary>
        private string FormatDuration(double totalSeconds)
        {
            if (double.IsNaN(totalSeconds) || totalSeconds < 0d)
            {
                totalSeconds = 0d;
            }

            long seconds = (long)Math.Floor(Math.Min(totalSeconds, long.MaxValue / 2d));
            long hours = seconds / 3600L;
            long minutes = seconds % 3600L / 60L;
            long secs = seconds % 60L;

            if (hours > 0L)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}{1} {2:00}{3}",
                    hours, texts.hourShort, minutes, texts.minuteShort);
            }

            if (minutes > 0L)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}{1} {2:00}{3}",
                    minutes, texts.minuteShort, secs, texts.secondShort);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}{1}", secs, texts.secondShort);
        }
    }
}
