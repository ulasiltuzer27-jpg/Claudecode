using UnityEngine;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// "İstasyon Yükselt": görev başladıktan sonra istasyonları (veya
    /// yalnızca <see cref="TargetStation"/>'ı) N kez yükselt.
    ///
    /// Mutlak seviye ("Izgara 10. seviye olsun") yerine yükseltme SAYISI
    /// sayılıyor: mutlak hedef, zincir tekrarlandığında zaten sağlanmış
    /// olur ve oyuncu hiçbir şey yapmadan ödül toplardı.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest_UpgradeStation", menuName = "Idle Restaurant/Quests/Upgrade Station", order = 11)]
    public sealed class UpgradeStationQuestDefinition : QuestDefinition
    {
        [Header("Filtre")]
        [Tooltip("Boşsa herhangi bir istasyonun yükseltmesi sayılır; doluysa yalnızca bu istasyonunki. " +
                 "Açıklamada {1} = istasyon adı.")]
        [SerializeField] private StationData targetStation = null;

        public StationData TargetStation => targetStation;
        public override QuestType Type => QuestType.UpgradeStation;
        protected override string DefaultTitle => "İstasyon Yükselt";

        protected override string DefaultDescriptionFormat =>
            targetStation != null ? "{1} istasyonunu {0} kez yükselt" : "İstasyonları {0} kez yükselt";

        public override QuestObjective CreateObjective()
        {
            return new UpgradeStationObjective(targetStation);
        }

        public override string GetDescription(Quest quest)
        {
            string stationName = targetStation != null ? targetStation.StationName : string.Empty;
            return SafeFormat(DescriptionFormat, FormatAmount(quest.Target), stationName);
        }
    }

    /// <summary>Filtreye uyan her yükseltmede ilerlemeyi 1 artırır.</summary>
    public sealed class UpgradeStationObjective : QuestObjective
    {
        private readonly StationData _filter;

        /// <param name="filter">Null ise her istasyon sayılır.</param>
        public UpgradeStationObjective(StationData filter)
        {
            _filter = filter;
        }

        protected override void OnBind()
        {
            Signals.onStationUpgraded += HandleStationUpgraded;
        }

        protected override void OnUnbind()
        {
            Signals.onStationUpgraded -= HandleStationUpgraded;
        }

        private void HandleStationUpgraded(Station station)
        {
            if (_filter == null || (station != null && station.Data == _filter))
            {
                AddProgress(1d);
            }
        }
    }
}
