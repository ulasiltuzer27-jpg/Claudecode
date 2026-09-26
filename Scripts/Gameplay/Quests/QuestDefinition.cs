using System;
using System.Globalization;
using IdleRestaurant.Core;
using UnityEngine;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// Bir görevin tasarım verisi. Her görev türü bu sınıftan türeyen ayrı
    /// bir ScriptableObject'tir ve kendi <see cref="QuestObjective"/>'ini
    /// üretir.
    ///
    /// ── Yeni görev türü eklemek ─────────────────────────────────────────────
    /// 1. QuestDefinition'dan türeyen bir sınıf yazın (CreateAssetMenu ile).
    /// 2. CreateObjective'de, ihtiyaç duyduğu <see cref="IQuestSignals"/>
    ///    olaylarını dinleyen bir QuestObjective döndürün.
    /// QuestManager'a dokunmak gerekmez. Mevcut sinyallerin yetmediği bir
    /// tür (ör. "reklam izle") yalnızca IQuestSignals'a bir olay ekler.
    /// </summary>
    public abstract class QuestDefinition : ScriptableObject
    {
        [Tooltip("Kayıttaki benzersiz anahtar. Boşsa varlık adı kullanılır. Yayından sonra DEĞİŞTİRMEYİN.")]
        [SerializeField] private string questId;

        [Tooltip("Boşsa türün varsayılan başlığı kullanılır.")]
        [SerializeField] private string title = null;

        [Tooltip("Açıklama biçimi; {0} = hedef miktar. Boşsa türün varsayılanı kullanılır.")]
        [SerializeField] private string descriptionFormat = null;

        [Header("Hedef")]
        [Tooltip("İlk turdaki hedef. Sayma görevlerinde tam sayıya yukarı yuvarlanır.")]
        [SerializeField] private double targetAmount = 10d;

        [Tooltip("Zincir her tekrarlandığında hedefin çarpıldığı katsayı. 1 = hep aynı.")]
        [SerializeField] private double targetScalePerCycle = 2d;

        [Header("Ödül")]
        [Tooltip("Sabit para ödülü (ilk tur).")]
        [SerializeField] private double rewardAmount = 100d;

        [Tooltip("Zincir her tekrarlandığında sabit ödülün çarpıldığı katsayı.")]
        [SerializeField] private double rewardScalePerCycle = 2d;

        [Tooltip("Görev başladığındaki saniye başı gelirin bu kadar saniyesi ödüle eklenir. " +
                 "Ödülü ekonomiyle birlikte büyütür; prestijden sonra da dengesini korur.")]
        [SerializeField, Min(0f)] private float rewardIncomeSeconds = 0f;

        public string QuestId => questId;
        public string Title => string.IsNullOrEmpty(title) ? DefaultTitle : title;
        public abstract QuestType Type { get; }

        protected abstract string DefaultTitle { get; }
        protected abstract string DefaultDescriptionFormat { get; }
        protected string DescriptionFormat => string.IsNullOrEmpty(descriptionFormat) ? DefaultDescriptionFormat : descriptionFormat;

        /// <summary>Bu görevin ilerlemesini izleyen yeni bir hedef nesnesi üretir. Her görev örneği kendi nesnesini alır.</summary>
        public abstract QuestObjective CreateObjective();

        /// <summary>Verilen turdaki hedef: targetAmount * targetScalePerCycle^tur, en az 1.</summary>
        public double GetTarget(int cycle)
        {
            double raw = targetAmount * Math.Pow(targetScalePerCycle, Math.Max(0, cycle));
            return Math.Max(1d, Math.Ceiling(ClampFinite(raw)));
        }

        /// <summary>Verilen turdaki ödül: sabit kısım ölçeklenir, gelir kısmı o anki saniye başı gelirden hesaplanır.</summary>
        public double GetReward(int cycle, double incomePerSecond)
        {
            double flat = rewardAmount * Math.Pow(rewardScalePerCycle, Math.Max(0, cycle));
            double fromIncome = double.IsNaN(incomePerSecond) || incomePerSecond <= 0d ? 0d : rewardIncomeSeconds * incomePerSecond;
            return ClampFinite(flat + fromIncome);
        }

        /// <summary>İlerleme ve hedef miktarlarının metni. Varsayılan: büyük sayı biçimi (5, 1.25K, 3.40M).</summary>
        public virtual string FormatAmount(double value)
        {
            return CurrencyManager.FormatNumber(value);
        }

        public virtual string GetDescription(Quest quest)
        {
            return SafeFormat(DescriptionFormat, FormatAmount(quest.Target));
        }

        /// <summary>
        /// Inspector'dan düzenlenen biçim metinleri için güvenli string.Format:
        /// hatalı bir "{2}" oyunu FormatException ile düşürmesin.
        /// </summary>
        protected static string SafeFormat(string format, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format ?? string.Empty, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[Quest] Geçersiz açıklama biçimi: '{format}'");
                return string.Join(" ", args);
            }
        }

        private static double ClampFinite(double value)
        {
            if (double.IsNaN(value) || value < 0d)
            {
                return 0d;
            }

            return double.IsInfinity(value) ? double.MaxValue : value;
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(questId))
            {
                questId = name;
            }

            questId = questId.Trim();

            if (double.IsNaN(targetAmount) || targetAmount < 1d)
            {
                targetAmount = 1d;
            }

            if (double.IsNaN(targetScalePerCycle) || targetScalePerCycle < 1d)
            {
                targetScalePerCycle = 1d;
            }

            if (double.IsNaN(rewardAmount) || rewardAmount < 0d)
            {
                rewardAmount = 0d;
            }

            if (double.IsNaN(rewardScalePerCycle) || rewardScalePerCycle < 1d)
            {
                rewardScalePerCycle = 1d;
            }
        }
    }
}
