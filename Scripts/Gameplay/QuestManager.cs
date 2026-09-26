using System;
using System.Collections.Generic;
using IdleRestaurant.Core;
using IdleRestaurant.Data;
using IdleRestaurant.Gameplay.Quests;
using UnityEngine;

namespace IdleRestaurant.Gameplay
{
    /// <summary>
    /// Sıralı görev zinciri: tek bir etkin görev, tamamlanınca para ödülü,
    /// sonra bir sonraki görev. Zincir bitince (repeatChain açıksa) başa
    /// döner ve her turda hedef ile ödül <see cref="QuestDefinition"/>'daki
    /// katsayılarla büyür; döngü sonsuzdur.
    ///
    /// ── Sorumluluklar ───────────────────────────────────────────────────────
    /// QuestManager yalnızca akışı yönetir (başlat → tamamla → ödüllendir →
    /// ilerle). Neyin ilerleme sayıldığını her türün <see cref="QuestObjective"/>'i
    /// bilir; yeni bir tür eklemek bu sınıfı değiştirmez.
    ///
    /// ── Tamamlanma neden Update'te ──────────────────────────────────────────
    /// Hedef, bir olayın (ör. para kazanıldı) içinde dolar. Ödülü orada
    /// vermek, para olayının içinden yeniden para eklemek ve sonraki görevi
    /// aynı çağrı yığınında başlatmak demekti. Tamamlanma bu yüzden bir
    /// sonraki Update'te işlenir; karede en fazla bir görev biter ve hatalı
    /// yapılandırılmış bir zincir sonsuz özyinelemeye giremez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestManager : MonoBehaviour, ISaveable
    {
        [Tooltip("Görevler bu sırayla verilir. Create → Idle Restaurant → Quests ile oluşturun.")]
        [SerializeField] private List<QuestDefinition> questChain = new List<QuestDefinition>();

        [Tooltip("Son görevden sonra zincir başa döner; her turda hedef ve ödül ölçeklenir.")]
        [SerializeField] private bool repeatChain = true;

        [Tooltip("İşaretliyse hedefe ulaşılınca ödül otomatik verilir ve sonraki göreve geçilir. " +
                 "Kapalıysa ClaimActiveQuest ('Topla' butonu) beklenir.")]
        [SerializeField] private bool autoClaimRewards = true;

        private CurrencyManager _currency;
        private IQuestSignals _signals;
        private Func<double> _incomePerSecondProvider;
        private Quest _activeQuest;
        private QuestObjective _activeObjective;
        private int _chainIndex;
        private int _cycle;
        private int _completedCount;
        private bool _chainFinished;

        /// <summary>
        /// Etkin görevin ilerlemesi veya durumu değiştiğinde, ya da yeni görev
        /// başladığında tetiklenir. Argüman null ise verilecek görev kalmadı
        /// (tekrarsız zincir bitti veya zincir boş).
        /// </summary>
        public event Action<Quest> onQuestUpdated;

        /// <summary>Yeni bir görev etkinleştiğinde tetiklenir.</summary>
        public event Action<Quest> onQuestStarted;

        /// <summary>Görevin hedefine ulaşıldığında, ödülden önce tetiklenir.</summary>
        public event Action<Quest> onQuestCompleted;

        /// <summary>Ödül bakiyeye eklendiğinde (görev, verilen tutar) ile tetiklenir.</summary>
        public event Action<Quest, double> onQuestRewarded;

        public Quest ActiveQuest => _activeQuest;
        public bool IsInitialized => _currency != null && _signals != null;
        public bool AutoClaimRewards => autoClaimRewards;
        public int CompletedQuestCount => _completedCount;
        public int CurrentCycle => _cycle;
        public IReadOnlyList<QuestDefinition> QuestChain => questChain;

        /// <summary>
        /// Bağımlılıkları verir. <see cref="RestoreState"/>'ten önce çağrılmalı;
        /// görevler ancak durum geri yüklenince başlar.
        /// </summary>
        /// <param name="incomePerSecondProvider">Gelire bağlı ödüller için; null ise o kısım 0 sayılır.</param>
        public void Initialize(CurrencyManager currency, IQuestSignals signals, Func<double> incomePerSecondProvider)
        {
            if (currency == null || signals == null)
            {
                Debug.LogError("[QuestManager] CurrencyManager ve IQuestSignals zorunlu; görevler çalışmayacak.", this);
                return;
            }

            _currency = currency;
            _signals = signals;
            _incomePerSecondProvider = incomePerSecondProvider;

            questChain.RemoveAll(definition => definition == null);
            WarnAboutDuplicateQuestIds();
        }

        // ── Akış ───────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_activeQuest == null)
            {
                return;
            }

            if (_activeQuest.IsActive && _activeQuest.IsTargetReached)
            {
                CompleteActiveQuest();
            }

            if (autoClaimRewards && _activeQuest != null && _activeQuest.State == QuestState.Completed)
            {
                ClaimActiveQuest();
            }
        }

        /// <summary>
        /// Tamamlanmış görevin ödülünü verir ve sonraki göreve geçer.
        /// autoClaimRewards kapalıyken "Topla" butonu bunu çağırır.
        /// </summary>
        /// <returns>Etkin görev tamamlanmamışsa false.</returns>
        public bool ClaimActiveQuest()
        {
            if (!IsInitialized || _activeQuest == null || _activeQuest.State != QuestState.Completed)
            {
                return false;
            }

            Quest quest = _activeQuest;
            quest.SetState(QuestState.Claimed);

            // Hedef nesnesi tamamlanırken çözüldü; ödül parası bir sonraki
            // "para kazan" görevinin ilerlemesine sayılmaz.
            double reward = quest.Reward;
            if (reward > 0d)
            {
                _currency.AddCurrency(reward);
            }

            _completedCount++;
            onQuestRewarded?.Invoke(quest, reward);
            onQuestUpdated?.Invoke(quest);

            AdvanceChain();
            return true;
        }

        private void CompleteActiveQuest()
        {
            StopObjective();
            _activeQuest.SetState(QuestState.Completed);
            onQuestCompleted?.Invoke(_activeQuest);
            onQuestUpdated?.Invoke(_activeQuest);
        }

        private void AdvanceChain()
        {
            int nextIndex = _chainIndex + 1;
            int cycle = _cycle;

            if (nextIndex >= questChain.Count)
            {
                if (!repeatChain)
                {
                    FinishChain();
                    return;
                }

                nextIndex = 0;
                cycle++;
            }

            StartQuest(nextIndex, cycle, 0d, 0d, false);
        }

        private void FinishChain()
        {
            StopObjective();
            _chainFinished = true;
            _activeQuest = null;
            onQuestUpdated?.Invoke(null);
        }

        /// <param name="savedReward">0 veya altıysa ödül şimdi hesaplanır.</param>
        private void StartQuest(int index, int cycle, double progress, double savedReward, bool awaitingClaim)
        {
            StopObjective();

            QuestDefinition definition = questChain[index];
            _chainIndex = index;
            _cycle = cycle;
            _chainFinished = false;

            double target = definition.GetTarget(cycle);
            double reward = savedReward > 0d ? savedReward : definition.GetReward(cycle, CurrentIncomePerSecond());
            QuestState state = awaitingClaim ? QuestState.Completed : QuestState.Active;
            _activeQuest = new Quest(definition, cycle, target, reward, progress, state);

            if (_activeQuest.IsActive)
            {
                _activeObjective = definition.CreateObjective();
                if (_activeObjective != null)
                {
                    _activeObjective.Bind(_activeQuest, _signals, HandleObjectiveProgress);
                }
                else
                {
                    Debug.LogError($"[QuestManager] '{definition.name}' bir hedef nesnesi üretmedi; görev ilerleyemez.", definition);
                }
            }

            onQuestStarted?.Invoke(_activeQuest);
            onQuestUpdated?.Invoke(_activeQuest);
        }

        private void HandleObjectiveProgress()
        {
            if (_activeQuest != null)
            {
                onQuestUpdated?.Invoke(_activeQuest);
            }
        }

        private void StopObjective()
        {
            if (_activeObjective == null)
            {
                return;
            }

            _activeObjective.Unbind();
            _activeObjective = null;
        }

        private double CurrentIncomePerSecond()
        {
            if (_incomePerSecondProvider == null)
            {
                return 0d;
            }

            double value = _incomePerSecondProvider();
            return double.IsNaN(value) || value < 0d ? 0d : value;
        }

        // ── Kayıt (ISaveable) ──────────────────────────────────────────────────

        public void CaptureState(SaveData data)
        {
            QuestSaveData state = new QuestSaveData
            {
                chainIndex = _chainIndex,
                cycle = _cycle,
                completedCount = _completedCount,
                chainFinished = _chainFinished
            };

            if (_activeQuest != null)
            {
                state.questId = _activeQuest.QuestId;
                state.progress = _activeQuest.Progress;
                state.reward = _activeQuest.Reward;
                state.awaitingClaim = _activeQuest.State == QuestState.Completed;
            }

            data.quests = state;
        }

        public void RestoreState(SaveData data)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[QuestManager] Initialize çağrılmadan RestoreState yok sayıldı.", this);
                return;
            }

            StopObjective();
            _activeQuest = null;
            _chainIndex = 0;
            _cycle = 0;
            _chainFinished = false;

            QuestSaveData state = data != null ? data.quests : null;
            _completedCount = state != null ? state.completedCount : 0;

            if (questChain.Count == 0)
            {
                onQuestUpdated?.Invoke(null);
                return;
            }

            if (state == null || !state.HasPosition)
            {
                StartQuest(0, 0, 0d, 0d, false);
                return;
            }

            int cycle = state.cycle;

            if (state.chainFinished)
            {
                if (!repeatChain)
                {
                    _chainIndex = questChain.Count - 1;
                    _cycle = cycle;
                    FinishChain();
                    return;
                }

                // Zincir bitmişken tekrar sonradan açıldı: bir sonraki turdan devam.
                StartQuest(0, cycle + 1, 0d, 0d, false);
                return;
            }

            int index = FindQuestIndex(state.questId);
            bool sameQuest = index >= 0;

            // Görev silinmiş veya kimliği değişmiş: aynı konumdaki görevle, sıfır ilerlemeyle devam.
            if (!sameQuest)
            {
                index = Mathf.Clamp(state.chainIndex, 0, questChain.Count - 1);
            }

            StartQuest(index, cycle,
                sameQuest ? state.progress : 0d,
                sameQuest ? state.reward : 0d,
                sameQuest && state.awaitingClaim);
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        private int FindQuestIndex(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return -1;
            }

            for (int i = 0; i < questChain.Count; i++)
            {
                if (questChain[i].QuestId == questId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void WarnAboutDuplicateQuestIds()
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < questChain.Count; i++)
            {
                QuestDefinition definition = questChain[i];
                if (!seen.Add(definition.QuestId ?? string.Empty))
                {
                    Debug.LogWarning($"[QuestManager] '{definition.QuestId}' kimliği zincirde birden fazla kez var; " +
                                     "kayıttan dönüşte ilk konumuna gidilir. Aynı görevi tekrar kullanmak " +
                                     "istiyorsanız varlığı kopyalayıp kimliğini değiştirin.", definition);
                }
            }
        }

        private void OnDestroy()
        {
            StopObjective();
        }

        [ContextMenu("Debug/Complete Active Quest")]
        private void DebugCompleteActiveQuest()
        {
            if (_activeQuest != null && _activeQuest.IsActive)
            {
                _activeQuest.SetProgress(_activeQuest.Target);
                onQuestUpdated?.Invoke(_activeQuest);
            }
        }
    }
}
