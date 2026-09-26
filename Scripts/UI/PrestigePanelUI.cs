using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Prestij paneli: Gem bakiyesi, bekleyen Gem, sıfırlama butonu ve kalıcı
    /// yükseltme satırları. <see cref="UIManager"/> tarafından başlatılır.
    ///
    /// Sıfırlama geri alınamaz; buton iki dokunuş ister (ilk dokunuş
    /// "Emin misin?" gösterir). Ayrı bir onay penceresi kurmadan yanlışlıkla
    /// sıfırlamayı önler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrestigePanelUI : MonoBehaviour
    {
        /// <summary>Tek bir kalıcı yükseltmenin arayüz referansları.</summary>
        [Serializable]
        public sealed class UpgradeRow
        {
            [Tooltip("PrestigeManager'daki yükseltmenin upgradeId'si.")]
            public string upgradeId;

            public TMP_Text nameText;
            public TMP_Text levelText;
            public TMP_Text bonusText;
            public TMP_Text costText;
            public Button buyButton;

            [NonSerialized] public UnityAction buyAction;
        }

        [Serializable]
        public sealed class Texts
        {
            public string gemsFormat = "{0} Gem";
            public string pendingGemsFormat = "Sıfırlarsan +{0} Gem";
            public string nextGemFormat = "Sonraki Gem için {0} daha kazan";

            [Tooltip("{0} = hız çarpanı, {1} = gelir çarpanı")]
            public string multipliersFormat = "Hız x{0}  ·  Gelir x{1}";

            public string prestigeButton = "Restoranı Sıfırla";
            public string prestigeConfirm = "Emin misin? Tekrar dokun";
            public string prestigeDoneFormat = "Restoran sıfırlandı! +{0} Gem";
            public string levelFormat = "Sv. {0}";

            [Tooltip("{0} = toplam bonus yüzdesi")]
            public string bonusFormat = "+%{0}";

            public string costFormat = "{0} Gem";
            public string maxed = "MAKS";
            public string notEnoughGems = "Yeterli Gem yok";
        }

        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text pendingGemsText;
        [SerializeField] private TMP_Text nextGemText;
        [SerializeField] private TMP_Text multipliersText;
        [SerializeField] private Button prestigeButton;
        [SerializeField] private TMP_Text prestigeButtonText;

        [Tooltip("İkinci dokunuşun sıfırlamayı onaylayacağı süre (sn).")]
        [SerializeField, Min(0.5f)] private float confirmWindowSeconds = 3f;

        [Tooltip("Bekleyen Gem metninin en sık yenilenme aralığı (sn). Ömür boyu kazanç her üretimde değişir.")]
        [SerializeField, Min(0f)] private float refreshInterval = 0.25f;

        [SerializeField] private List<UpgradeRow> upgradeRows = new List<UpgradeRow>();
        [SerializeField] private Texts texts = new Texts();

        private PrestigeManager _prestige;
        private CurrencyManager _currency;
        private UIManager _ui;
        private bool _earningsDirty;
        private float _nextRefreshTime;
        private float _confirmUntil;
        private bool _confirming;

        /// <param name="prestige">Sahnede prestij sistemi yoksa null; panel pasif kalır.</param>
        public void Initialize(PrestigeManager prestige, CurrencyManager currency, UIManager ui)
        {
            Unbind();
            _prestige = prestige;
            _currency = currency;
            _ui = ui;

            if (_prestige == null)
            {
                if (prestigeButton != null)
                {
                    prestigeButton.interactable = false;
                }

                return;
            }

            _prestige.onGemsChanged += HandleGemsChanged;
            _prestige.onMultipliersChanged += HandleMultipliersChanged;
            _prestige.onPrestigePerformed += HandlePrestigePerformed;

            if (_currency != null)
            {
                _currency.onCurrencyEarned += HandleCurrencyEarned;
            }

            BindButton(prestigeButton, OnPrestigeClicked);

            for (int i = 0; i < upgradeRows.Count; i++)
            {
                UpgradeRow row = upgradeRows[i];
                if (row == null || row.buyButton == null)
                {
                    continue;
                }

                UpgradeRow captured = row;
                row.buyAction = () => OnBuyClicked(captured);
                row.buyButton.onClick.AddListener(row.buyAction);
            }

            RefreshAll();
        }

        private void LateUpdate()
        {
            if (_prestige == null)
            {
                return;
            }

            if (_confirming && Time.unscaledTime > _confirmUntil)
            {
                _confirming = false;
                SetText(prestigeButtonText, texts.prestigeButton);
            }

            if (_earningsDirty && Time.unscaledTime >= _nextRefreshTime)
            {
                _earningsDirty = false;
                _nextRefreshTime = Time.unscaledTime + refreshInterval;
                RefreshPrestigeProgress();
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_prestige != null)
            {
                _prestige.onGemsChanged -= HandleGemsChanged;
                _prestige.onMultipliersChanged -= HandleMultipliersChanged;
                _prestige.onPrestigePerformed -= HandlePrestigePerformed;
            }

            if (_currency != null)
            {
                _currency.onCurrencyEarned -= HandleCurrencyEarned;
            }

            UnbindButton(prestigeButton, OnPrestigeClicked);

            for (int i = 0; i < upgradeRows.Count; i++)
            {
                UpgradeRow row = upgradeRows[i];
                if (row != null && row.buyButton != null && row.buyAction != null)
                {
                    row.buyButton.onClick.RemoveListener(row.buyAction);
                }

                if (row != null)
                {
                    row.buyAction = null;
                }
            }

            _prestige = null;
            _currency = null;
            _ui = null;
            _confirming = false;
        }

        // ── Olay işleyicileri ──────────────────────────────────────────────────

        private void HandleCurrencyEarned(double amount)
        {
            _earningsDirty = true;
        }

        private void HandleGemsChanged(double gems)
        {
            RefreshGems();
            RefreshUpgradeRows();
        }

        private void HandleMultipliersChanged()
        {
            RefreshMultipliers();
            RefreshUpgradeRows();
        }

        private void HandlePrestigePerformed(double gemsEarned)
        {
            _confirming = false;
            RefreshAll();

            if (_ui != null)
            {
                _ui.ShowToast(Format(texts.prestigeDoneFormat, CurrencyManager.FormatNumber(gemsEarned)));
            }
        }

        // ── Buton tıklamaları ──────────────────────────────────────────────────

        private void OnPrestigeClicked()
        {
            if (!_prestige.CanPrestige)
            {
                return;
            }

            if (!_confirming || Time.unscaledTime > _confirmUntil)
            {
                _confirming = true;
                _confirmUntil = Time.unscaledTime + confirmWindowSeconds;
                SetText(prestigeButtonText, texts.prestigeConfirm);
                return;
            }

            _confirming = false;
            SetText(prestigeButtonText, texts.prestigeButton);
            _prestige.ResetRestaurant();
        }

        private void OnBuyClicked(UpgradeRow row)
        {
            if (_prestige == null || _prestige.IsUpgradeMaxed(row.upgradeId))
            {
                return;
            }

            if (!_prestige.TryPurchaseUpgrade(row.upgradeId) && _ui != null)
            {
                _ui.ShowToast(texts.notEnoughGems);
            }
        }

        // ── Yenileme ───────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (!_confirming)
            {
                SetText(prestigeButtonText, texts.prestigeButton);
            }

            RefreshGems();
            RefreshMultipliers();
            RefreshPrestigeProgress();
            RefreshUpgradeRows();
        }

        private void RefreshGems()
        {
            SetText(gemsText, Format(texts.gemsFormat, CurrencyManager.FormatNumber(_prestige.Gems)));
        }

        private void RefreshMultipliers()
        {
            SetText(multipliersText, Format(texts.multipliersFormat,
                _prestige.SpeedMultiplier.ToString("0.##", CultureInfo.InvariantCulture),
                _prestige.IncomeMultiplier.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        private void RefreshPrestigeProgress()
        {
            SetText(pendingGemsText, Format(texts.pendingGemsFormat, CurrencyManager.FormatNumber(_prestige.PendingGems)));
            SetText(nextGemText, Format(texts.nextGemFormat, CurrencyManager.FormatNumber(_prestige.EarningsUntilNextGem)));

            if (prestigeButton != null)
            {
                prestigeButton.interactable = _prestige.CanPrestige;
            }
        }

        private void RefreshUpgradeRows()
        {
            for (int i = 0; i < upgradeRows.Count; i++)
            {
                UpgradeRow row = upgradeRows[i];
                if (row != null)
                {
                    RefreshUpgradeRow(row);
                }
            }
        }

        private void RefreshUpgradeRow(UpgradeRow row)
        {
            PermanentUpgradeDefinition definition = _prestige.FindUpgrade(row.upgradeId);
            if (definition == null)
            {
                SetText(row.nameText, row.upgradeId);
                if (row.buyButton != null)
                {
                    row.buyButton.interactable = false;
                }

                return;
            }

            int level = _prestige.GetUpgradeLevel(definition.upgradeId);
            bool maxed = _prestige.IsUpgradeMaxed(definition.upgradeId);
            double bonusPercent = definition.GetTotalBonus(level) * 100d;

            SetText(row.nameText, definition.displayName);
            SetText(row.levelText, Format(texts.levelFormat, level));
            SetText(row.bonusText, Format(texts.bonusFormat, bonusPercent.ToString("0.#", CultureInfo.InvariantCulture)));
            SetText(row.costText, maxed
                ? texts.maxed
                : Format(texts.costFormat, CurrencyManager.FormatNumber(_prestige.GetUpgradeCost(definition.upgradeId))));

            if (row.buyButton != null)
            {
                row.buyButton.interactable = _prestige.CanPurchaseUpgrade(definition.upgradeId);
            }
        }
    }
}
