using UnityEngine;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// "Prestij Yap": görev başladıktan sonra restoranı N kez sıfırla.
    /// Sahnede PrestigeManager yoksa bu görev tamamlanamaz; zincire
    /// eklemeden önce prestij sisteminin kurulu olduğundan emin olun.
    /// Önerilen ayar: targetAmount = 1, targetScalePerCycle = 1.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_Prestige", menuName = "Idle Restaurant/Quests/Prestige", order = 12)]
    public sealed class PrestigeQuestDefinition : QuestDefinition
    {
        public override QuestType Type => QuestType.Prestige;
        protected override string DefaultTitle => "Prestij Yap";
        protected override string DefaultDescriptionFormat => "Restoranı {0} kez sıfırla";

        public override QuestObjective CreateObjective()
        {
            return new PrestigeObjective();
        }
    }

    /// <summary>Her prestijde ilerlemeyi 1 artırır.</summary>
    public sealed class PrestigeObjective : QuestObjective
    {
        protected override void OnBind()
        {
            Signals.onPrestigePerformed += HandlePrestigePerformed;
        }

        protected override void OnUnbind()
        {
            Signals.onPrestigePerformed -= HandlePrestigePerformed;
        }

        private void HandlePrestigePerformed(double gemsEarned)
        {
            AddProgress(1d);
        }
    }
}
