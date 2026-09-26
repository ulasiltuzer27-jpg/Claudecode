using System;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>Görevin türü. UI ikon/renk seçimi gibi sınıflandırmalar için.</summary>
    public enum QuestType
    {
        /// <summary>Para Biriktir: görev başladıktan sonra toplam X para kazan.</summary>
        EarnMoney,

        /// <summary>İstasyon Yükselt: istasyonları (veya belirli birini) N kez yükselt.</summary>
        UpgradeStation,

        /// <summary>Prestij Yap: restoranı N kez sıfırla.</summary>
        Prestige
    }

    public enum QuestState
    {
        /// <summary>İlerleme sayılıyor.</summary>
        Active,

        /// <summary>Hedefe ulaşıldı, ödül bekleniyor.</summary>
        Completed,

        /// <summary>Ödül verildi; zincir bir sonraki göreve geçti.</summary>
        Claimed
    }

    /// <summary>
    /// Bir görevin çalışma zamanı örneği: tanım + bu turdaki hedef/ödül +
    /// ilerleme. <see cref="QuestManager.onQuestUpdated"/> ile UI'a giden
    /// nesne budur. Durumu yalnızca görev sistemi değiştirebilir.
    /// </summary>
    public sealed class Quest
    {
        internal Quest(QuestDefinition definition, int cycle, double target, double reward, double progress, QuestState state)
        {
            Definition = definition;
            Cycle = cycle;
            Target = target;
            Reward = reward;
            State = state;
            SetProgress(progress);
        }

        public QuestDefinition Definition { get; }
        public string QuestId => Definition.QuestId;
        public QuestType Type => Definition.Type;

        /// <summary>Zincirin kaçıncı turu (0'dan başlar); hedef ve ödül buna göre ölçeklendi.</summary>
        public int Cycle { get; }

        public double Target { get; }

        /// <summary>Tamamlanınca verilecek para; görev başlarken sabitlendi.</summary>
        public double Reward { get; }

        public double Progress { get; private set; }
        public QuestState State { get; private set; }

        public bool IsActive => State == QuestState.Active;
        public bool IsTargetReached => Progress >= Target;

        /// <summary>0-1 arası ilerleme; ilerleme çubukları için.</summary>
        public float NormalizedProgress => Target > 0d ? (float)Math.Min(1d, Progress / Target) : 1f;

        public string Title => Definition.Title;
        public string Description => Definition.GetDescription(this);

        /// <summary>"1.25K / 10.0K" veya "3 / 5" biçiminde ilerleme metni.</summary>
        public string ProgressText => Definition.FormatAmount(Progress) + " / " + Definition.FormatAmount(Target);

        /// <summary>İlerleme hedefin üstüne çıkmaz; negatif ve NaN yok sayılır.</summary>
        internal void SetProgress(double value)
        {
            if (double.IsNaN(value))
            {
                return;
            }

            Progress = Math.Max(0d, Math.Min(value, Target));
        }

        internal void SetState(QuestState state)
        {
            State = state;
        }
    }
}
