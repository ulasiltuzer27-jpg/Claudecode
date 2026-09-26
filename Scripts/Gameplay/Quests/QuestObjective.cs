using System;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// Tek bir görev örneğinin ilerlemesini izleyen nesne. Etkin görev
    /// başladığında <see cref="Bind"/> ile oyun sinyallerine bağlanır, görev
    /// bitince <see cref="Unbind"/> ile çözülür.
    ///
    /// Alt sınıflar yalnızca hangi sinyali dinleyeceklerini ve sinyali
    /// ilerlemeye nasıl çevireceklerini bilir; tamamlanmayı, ödülü ve
    /// zincirde ilerlemeyi <see cref="QuestManager"/> yönetir.
    /// </summary>
    public abstract class QuestObjective
    {
        private Action _onProgressChanged;

        protected Quest Quest { get; private set; }
        protected IQuestSignals Signals { get; private set; }
        public bool IsBound => Quest != null;

        public void Bind(Quest quest, IQuestSignals signals, Action onProgressChanged)
        {
            if (quest == null)
            {
                throw new ArgumentNullException(nameof(quest));
            }

            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            Unbind();
            Quest = quest;
            Signals = signals;
            _onProgressChanged = onProgressChanged;
            OnBind();
        }

        public void Unbind()
        {
            if (!IsBound)
            {
                return;
            }

            OnUnbind();
            Quest = null;
            Signals = null;
            _onProgressChanged = null;
        }

        /// <summary><see cref="Signals"/> olaylarına abone olun.</summary>
        protected abstract void OnBind();

        /// <summary><see cref="OnBind"/>'daki aboneliklerin hepsini çözün.</summary>
        protected abstract void OnUnbind();

        protected void AddProgress(double amount)
        {
            // !(amount > 0) NaN'ı da eler.
            if (!IsBound || !(amount > 0d))
            {
                return;
            }

            SetProgress(Quest.Progress + amount);
        }

        protected void SetProgress(double value)
        {
            if (!IsBound || !Quest.IsActive)
            {
                return;
            }

            double before = Quest.Progress;
            Quest.SetProgress(value);

            if (Quest.Progress != before)
            {
                _onProgressChanged?.Invoke();
            }
        }
    }
}
