using System;
using IdleRestaurant.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static IdleRestaurant.UI.UIUtility;

namespace IdleRestaurant.UI
{
    /// <summary>
    /// Ayarlar paneli: ses ve titreşim anahtarları, ilerlemeyi sıfırlama ve
    /// gizlilik politikası bağlantısı. <see cref="UIManager"/> başlatır ve
    /// ayarlar butonuyla açıp kapatır.
    ///
    /// ── Anahtar türü ────────────────────────────────────────────────────────
    /// Her anahtar ya bir Toggle ya da bir Button ile kurulabilir
    /// (<see cref="SettingSwitch"/>). Buton kullanılırsa her basış durumu
    /// tersine çevirir ve durum etiketinde yazar.
    ///
    /// ── Sıfırlama iki aşamalı ───────────────────────────────────────────────
    /// "İlerlemeyi Sıfırla" → açıklama + "Devam" → "geri alınamaz" uyarısı +
    /// "Evet, Sıfırla". Son onay butonu <see cref="finalConfirmDelay"/>
    /// saniye kilitli kalır: aynı yere hızlı iki dokunuş iki aşamayı birden
    /// geçemesin.
    ///
    /// Ayar değişiklikleri AudioManager üzerinden kaydedilir; bu panel kayıt
    /// sistemini tanımaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanelUI : MonoBehaviour
    {
        /// <summary>Toggle veya Button ile kurulabilen açık/kapalı anahtar.</summary>
        [Serializable]
        public sealed class SettingSwitch
        {
            [Tooltip("Toggle kullanılıyorsa. Button ile birlikte de atanabilir.")]
            public Toggle toggle;

            [Tooltip("Toggle yerine buton kullanılıyorsa; her basış durumu tersine çevirir.")]
            public Button button;

            [Tooltip("Durum metni (ör. 'Ses: Açık'). İsteğe bağlı.")]
            public TMP_Text label;

            [Tooltip("Satırın kök nesnesi; anahtar gizlenirken bu kapatılır. Boşsa toggle/buton nesnesi.")]
            public GameObject root;

            [NonSerialized] public UnityAction<bool> toggleAction;
            [NonSerialized] public UnityAction buttonAction;
        }

        [Serializable]
        public sealed class Texts
        {
            public string sfxOn = "Ses: Açık";
            public string sfxOff = "Ses: Kapalı";
            public string hapticsOn = "Titreşim: Açık";
            public string hapticsOff = "Titreşim: Kapalı";

            public string resetStep1Message = "Restoranın, paran, Gem'lerin, görevlerin ve ayarların silinecek. Devam etmek istiyor musun?";
            public string resetStep1Confirm = "Devam";
            public string resetStep2Message = "Bu işlem GERİ ALINAMAZ. Her şey ilk günkü haline dönecek.";
            public string resetStep2Confirm = "Evet, Sıfırla";

            [Tooltip("Son onay kilitliyken buton metni; {0} = kalan saniye.")]
            public string resetStep2Countdown = "Evet, Sıfırla ({0})";

            public string privacyUrlMissing = "Gizlilik politikası bağlantısı tanımlı değil";
            public string versionFormat = "Sürüm {0}";
        }

        [Header("Panel")]
        [Tooltip("Açılıp kapatılan kök. Boşsa bu nesne.")]
        [SerializeField] private GameObject panelRoot;

        [SerializeField] private Button closeButton;

        [Header("Ses ve titreşim")]
        [SerializeField] private SettingSwitch sfxSwitch = new SettingSwitch();
        [SerializeField] private SettingSwitch hapticsSwitch = new SettingSwitch();

        [Tooltip("Cihazda titreşim yoksa titreşim anahtarını gizle. Editörde hep görünür.")]
        [SerializeField] private bool hideHapticsWhenUnsupported = true;

        [Header("İlerlemeyi sıfırla")]
        [SerializeField] private Button resetButton;

        [Tooltip("İki aşamalı onay penceresinin kökü. Panelin içinde olabilir.")]
        [SerializeField] private GameObject resetConfirmPopup;

        [SerializeField] private TMP_Text resetConfirmMessage;
        [SerializeField] private Button resetConfirmButton;
        [SerializeField] private TMP_Text resetConfirmButtonText;
        [SerializeField] private Button resetCancelButton;

        [Tooltip("Son onay butonunun kilitli kaldığı süre (sn).")]
        [SerializeField, Min(0f)] private float finalConfirmDelay = 1.5f;

        [Header("Gizlilik")]
        [SerializeField] private Button privacyPolicyButton;

        [Tooltip("Gizlilik politikasının yayındaki adresi (https). Google Play ve AdMob için zorunlu; " +
                 "PRIVACY_POLICY.md'yi yayınladığınız sayfa. Play Console'a girilen adresle aynı olmalı.")]
        [SerializeField] private string privacyPolicyUrl = "";

        [Header("Diğer")]
        [Tooltip("Application.version'ı gösterir. İsteğe bağlı.")]
        [SerializeField] private TMP_Text versionText;

        [SerializeField] private Texts texts = new Texts();

        private AudioManager _audio;
        private Action _hardReset;
        private IUIFeedback _ui;
        private int _resetStage;
        private float _finalConfirmUnlockTime;
        private int _lastCountdownShown = -1;
        private bool _initialized;

        public bool IsOpen => Root.activeSelf;

        /// <summary>0 = onay penceresi kapalı, 1 = ilk uyarı, 2 = son onay.</summary>
        public int ResetStage => _resetStage;

        public string PrivacyPolicyUrl => privacyPolicyUrl;

        private GameObject Root => panelRoot != null ? panelRoot : gameObject;

        /// <summary>
        /// Paneli bağlar ve kapalı başlatır.
        /// </summary>
        /// <param name="audio">Sahnede ses sistemi yoksa null; ses ve titreşim anahtarları gizlenir.</param>
        /// <param name="hardReset">Son onaydan sonra çağrılır (GameManager.ResetProgress).</param>
        /// <param name="ui">Tıklama sesi ve kısa mesajlar için; null olabilir.</param>
        public void Initialize(AudioManager audio, Action hardReset, IUIFeedback ui)
        {
            Unbind();
            _audio = audio;
            _hardReset = hardReset;
            _ui = ui;

            if (_audio != null)
            {
                _audio.onSettingsChanged += RefreshSwitches;
                BindSwitch(sfxSwitch, OnSfxChanged, () => !_audio.IsMuted);
                BindSwitch(hapticsSwitch, OnHapticsChanged, () => _audio.HapticsEnabled);
            }

            BindButton(closeButton, OnCloseClicked);
            BindButton(resetButton, OnResetClicked);
            BindButton(resetConfirmButton, OnResetConfirmClicked);
            BindButton(resetCancelButton, OnResetCancelClicked);
            BindButton(privacyPolicyButton, OnPrivacyPolicyClicked);

            SetText(versionText, Format(texts.versionFormat, Application.version));

            if (!IsValidUrl(privacyPolicyUrl))
            {
                Debug.LogWarning("[SettingsPanelUI] Gizlilik politikası adresi boş veya geçersiz. " +
                                 "Google Play yayını için zorunlu; Inspector'dan privacyPolicyUrl'i doldurun.", this);
            }

            _initialized = true;
            HideResetPopup();
            RefreshSwitches();
            SetActive(Root, false);
        }

        // ── Açma / kapama ──────────────────────────────────────────────────────

        public void Open()
        {
            HideResetPopup();
            RefreshSwitches();
            SetActive(Root, true);
        }

        public void Close()
        {
            HideResetPopup();
            SetActive(Root, false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (_resetStage == 2)
            {
                UpdateFinalConfirmCountdown();
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            // Android geri tuşu Escape olarak gelir: önce onay penceresini, sonra paneli kapatır.
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_resetStage != 0)
                {
                    HideResetPopup();
                }
                else
                {
                    Close();
                }
            }
#endif
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_audio != null)
            {
                _audio.onSettingsChanged -= RefreshSwitches;
            }

            UnbindSwitch(sfxSwitch);
            UnbindSwitch(hapticsSwitch);
            UnbindButton(closeButton, OnCloseClicked);
            UnbindButton(resetButton, OnResetClicked);
            UnbindButton(resetConfirmButton, OnResetConfirmClicked);
            UnbindButton(resetCancelButton, OnResetCancelClicked);
            UnbindButton(privacyPolicyButton, OnPrivacyPolicyClicked);

            _audio = null;
            _hardReset = null;
            _ui = null;
            _initialized = false;
        }

        // ── Ses ve titreşim ────────────────────────────────────────────────────

        private void OnSfxChanged(bool on)
        {
            if (on)
            {
                // Önce sesi aç, sonra tıklama bildir: açılış tıklaması duyulsun.
                _audio.SetMuted(false);
                NotifyClick();
            }
            else
            {
                NotifyClick();
                _audio.SetMuted(true);
            }
        }

        private void OnHapticsChanged(bool on)
        {
            if (on)
            {
                _audio.SetHapticsEnabled(true);
                NotifyClick();
            }
            else
            {
                NotifyClick();
                _audio.SetHapticsEnabled(false);
            }
        }

        private void RefreshSwitches()
        {
            bool hasAudio = _audio != null;
            SetSwitchVisible(sfxSwitch, hasAudio);

            bool hapticsVisible = hasAudio && (Application.isEditor || !hideHapticsWhenUnsupported || _audio.HapticsSupported);
            SetSwitchVisible(hapticsSwitch, hapticsVisible);

            if (!hasAudio)
            {
                return;
            }

            ShowSwitchState(sfxSwitch, !_audio.IsMuted, texts.sfxOn, texts.sfxOff);
            ShowSwitchState(hapticsSwitch, _audio.HapticsEnabled, texts.hapticsOn, texts.hapticsOff);
        }

        private static void BindSwitch(SettingSwitch setting, Action<bool> apply, Func<bool> currentState)
        {
            if (setting == null)
            {
                return;
            }

            if (setting.toggle != null)
            {
                setting.toggleAction = value => apply(value);
                setting.toggle.onValueChanged.AddListener(setting.toggleAction);
            }

            if (setting.button != null)
            {
                setting.buttonAction = () => apply(!currentState());
                setting.button.onClick.AddListener(setting.buttonAction);
            }
        }

        private static void UnbindSwitch(SettingSwitch setting)
        {
            if (setting == null)
            {
                return;
            }

            if (setting.toggle != null && setting.toggleAction != null)
            {
                setting.toggle.onValueChanged.RemoveListener(setting.toggleAction);
            }

            if (setting.button != null && setting.buttonAction != null)
            {
                setting.button.onClick.RemoveListener(setting.buttonAction);
            }

            setting.toggleAction = null;
            setting.buttonAction = null;
        }

        private static void ShowSwitchState(SettingSwitch setting, bool on, string onText, string offText)
        {
            if (setting == null)
            {
                return;
            }

            // Bildirimsiz: kod tarafından yapılan güncelleme dinleyiciyi tetikleyip
            // ayarı yeniden yazmasın (ve tıklama sesi çalmasın).
            if (setting.toggle != null)
            {
                setting.toggle.SetIsOnWithoutNotify(on);
            }

            SetText(setting.label, on ? onText : offText);
        }

        private static void SetSwitchVisible(SettingSwitch setting, bool visible)
        {
            if (setting == null)
            {
                return;
            }

            if (setting.root != null)
            {
                SetActive(setting.root, visible);
                return;
            }

            SetActive(setting.toggle != null ? setting.toggle.gameObject : null, visible);
            SetActive(setting.button != null ? setting.button.gameObject : null, visible);
        }

        // ── İlerlemeyi sıfırla ─────────────────────────────────────────────────

        private void OnResetClicked()
        {
            NotifyClick();
            ShowResetStage(1);
        }

        private void OnResetConfirmClicked()
        {
            NotifyClick();

            if (_resetStage == 1)
            {
                ShowResetStage(2);
                return;
            }

            // Kilit süresi yalnızca butonun interactable'ına bırakılmıyor;
            // başka bir yoldan gelen çağrı da süreyi beklemek zorunda.
            if (_resetStage == 2 && Time.unscaledTime >= _finalConfirmUnlockTime)
            {
                Action reset = _hardReset;
                HideResetPopup();

                if (reset != null)
                {
                    reset();
                }
            }
        }

        private void OnResetCancelClicked()
        {
            NotifyClick();
            HideResetPopup();
        }

        private void ShowResetStage(int stage)
        {
            _resetStage = stage;
            SetActive(resetConfirmPopup, true);

            if (stage == 1)
            {
                SetText(resetConfirmMessage, texts.resetStep1Message);
                SetText(resetConfirmButtonText, texts.resetStep1Confirm);
                SetInteractable(resetConfirmButton, true);
                return;
            }

            SetText(resetConfirmMessage, texts.resetStep2Message);
            _finalConfirmUnlockTime = Time.unscaledTime + finalConfirmDelay;
            _lastCountdownShown = -1;
            UpdateFinalConfirmCountdown();
        }

        private void UpdateFinalConfirmCountdown()
        {
            float remaining = _finalConfirmUnlockTime - Time.unscaledTime;

            if (remaining <= 0f)
            {
                if (_lastCountdownShown != 0)
                {
                    _lastCountdownShown = 0;
                    SetText(resetConfirmButtonText, texts.resetStep2Confirm);
                    SetInteractable(resetConfirmButton, true);
                }

                return;
            }

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != _lastCountdownShown)
            {
                _lastCountdownShown = seconds;
                SetText(resetConfirmButtonText, Format(texts.resetStep2Countdown, seconds));
                SetInteractable(resetConfirmButton, false);
            }
        }

        private void HideResetPopup()
        {
            _resetStage = 0;
            SetActive(resetConfirmPopup, false);
        }

        // ── Gizlilik ───────────────────────────────────────────────────────────

        private void OnPrivacyPolicyClicked()
        {
            NotifyClick();

            if (!IsValidUrl(privacyPolicyUrl))
            {
                Debug.LogError("[SettingsPanelUI] Gizlilik politikası adresi boş veya geçersiz: '" + privacyPolicyUrl + "'", this);
                if (_ui != null)
                {
                    _ui.ShowToast(texts.privacyUrlMissing);
                }

                return;
            }

            Application.OpenURL(privacyPolicyUrl);
        }

        /// <summary>Yalnızca mutlak http/https adresleri kabul edilir.</summary>
        public static bool IsValidUrl(string url)
        {
            Uri uri;
            return !string.IsNullOrWhiteSpace(url)
                   && Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri)
                   && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        private void OnCloseClicked()
        {
            NotifyClick();
            Close();
        }

        private void NotifyClick()
        {
            if (_ui != null)
            {
                _ui.NotifyButtonClicked();
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
            }
        }

        private void OnValidate()
        {
            privacyPolicyUrl = privacyPolicyUrl != null ? privacyPolicyUrl.Trim() : string.Empty;
        }
    }
}
