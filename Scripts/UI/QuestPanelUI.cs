using System;
using IdleRestaurant.Core;
using IdleRestaurant.Gameplay;
using IdleRestaurant.Gameplay.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static IdleRestaurant.UI.UIUtility;

namespace IdleRestaurant.UI
{
    /// <summary>
    /// Etkin görevi gösteren panel. <see cref="QuestManager.onQuestUpdated"/>'i
    /// dinler; <see cref="UIManager"/> tarafından başlatılır.
    ///
    /// "Para kazan" görevinin ilerlemesi her üretim döngüsünde değişir.
    /// Metinler bu yüzden olay başına değil, en fazla
    /// <see cref="refreshInterval"/>'da bir yenilenir; görev değiştiğinde veya
    /// tamamlandığında ise beklemeden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestPanelUI : MonoBehaviour
    {
        [Serializable]
        public sealed class Texts
        {
            public string rewardFormat = "Ödül: {0}";
            public string completed = "Tamamlandı!";
            public string noQuestTitle = "Tüm görevler tamamlandı";
            public string noQuestDescription = "Yeni görevler yakında";
            public string rewardToastFormat = "Görev tamamlandı! +{0}";
        }

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardText;

        [Tooltip("Image Type = Filled olmalı.")]
        [SerializeField] private Image progressFill;

        [Tooltip("QuestManager'da autoClaimRewards kapalıyken, görev tamamlanınca görünür.")]
        [SerializeField] private Button claimButton;

        [Tooltip("İlerleme metninin en sık yenilenme aralığı (sn).")]
        [SerializeField, Min(0f)] private float refreshInterval = 0.1f;

        [SerializeField] private Texts texts = new Texts();

        private QuestManager _quests;
        private UIManager _ui;
        private Quest _shownQuest;
        private QuestState _shownState;
        private bool _dirty;
        private float _nextRefreshTime;

        /// <param name="quests">Sahnede görev sistemi yoksa null; panel "görev yok" durumunu gösterir.</param>
        public void Initialize(QuestManager quests, UIManager ui)
        {
            Unbind();
            _quests = quests;
            _ui = ui;

            if (_quests != null)
            {
                _quests.onQuestUpdated += HandleQuestUpdated;
                _quests.onQuestRewarded += HandleQuestRewarded;
                BindButton(claimButton, OnClaimClicked);
            }

            Show(_quests != null ? _quests.ActiveQuest : null);
        }

        private void LateUpdate()
        {
            if (!_dirty || _quests == null || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _dirty = false;
            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            Show(_quests.ActiveQuest);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_quests != null)
            {
                _quests.onQuestUpdated -= HandleQuestUpdated;
                _quests.onQuestRewarded -= HandleQuestRewarded;
            }

            UnbindButton(claimButton, OnClaimClicked);
            _quests = null;
            _ui = null;
        }

        private void HandleQuestUpdated(Quest quest)
        {
            _dirty = true;

            // Yeni görev veya durum değişimi beklemeden gösterilsin; yalnızca
            // aynı görevin ilerlemesi seyreltiliyor.
            if (quest != _shownQuest || (quest != null && quest.State != _shownState))
            {
                _nextRefreshTime = 0f;
            }
        }

        private void HandleQuestRewarded(Quest quest, double reward)
        {
            if (_ui != null && reward > 0d)
            {
                _ui.ShowToast(Format(texts.rewardToastFormat, CurrencyManager.FormatNumber(reward)));
            }
        }

        private void OnClaimClicked()
        {
            if (_quests != null)
            {
                _quests.ClaimActiveQuest();
            }
        }

        private void Show(Quest quest)
        {
            _shownQuest = quest;
            _shownState = quest != null ? quest.State : QuestState.Claimed;

            if (quest == null)
            {
                SetText(titleText, texts.noQuestTitle);
                SetText(descriptionText, texts.noQuestDescription);
                SetText(progressText, string.Empty);
                SetText(rewardText, string.Empty);
                SetFill(1f);
                SetActive(claimButton != null ? claimButton.gameObject : null, false);
                return;
            }

            SetText(titleText, quest.Title);
            SetText(descriptionText, quest.Description);
            SetText(progressText, quest.IsActive ? quest.ProgressText : texts.completed);
            SetText(rewardText, Format(texts.rewardFormat, CurrencyManager.FormatNumber(quest.Reward)));
            SetFill(quest.NormalizedProgress);

            bool awaitingClaim = quest.State == QuestState.Completed && _quests != null && !_quests.AutoClaimRewards;
            SetActive(claimButton != null ? claimButton.gameObject : null, awaitingClaim);
        }

        private void SetFill(float value)
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = value;
            }
        }
    }
}
