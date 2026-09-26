using UnityEngine;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// "Para Biriktir": görev başladıktan sonra toplam X para kazan.
    ///
    /// Bakiyeye değil KAZANCA bakar. "Bakiyen X olsun" bir idle oyunda
    /// oyuncuyu yükseltme yapmamaya zorlardı; kazanç ise harcamadan
    /// etkilenmez.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_EarnMoney", menuName = "Idle Restaurant/Quests/Earn Money", order = 10)]
    public sealed class EarnMoneyQuestDefinition : QuestDefinition
    {
        public override QuestType Type => QuestType.EarnMoney;
        protected override string DefaultTitle => "Para Biriktir";
        protected override string DefaultDescriptionFormat => "{0} para kazan";

        public override QuestObjective CreateObjective()
        {
            return new EarnMoneyObjective();
        }
    }

    /// <summary>Kazanılan her parayı ilerlemeye ekler.</summary>
    public sealed class EarnMoneyObjective : QuestObjective
    {
        protected override void OnBind()
        {
            Signals.onMoneyEarned += HandleMoneyEarned;
        }

        protected override void OnUnbind()
        {
            Signals.onMoneyEarned -= HandleMoneyEarned;
        }

        private void HandleMoneyEarned(double amount)
        {
            AddProgress(amount);
        }
    }
}
