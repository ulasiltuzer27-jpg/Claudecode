using System;
using System.Collections.Generic;
using IdleRestaurant.Core;

namespace IdleRestaurant.Gameplay.Quests
{
    /// <summary>
    /// <see cref="IQuestSignals"/>'ın oyundaki uygulaması: para, istasyon ve
    /// prestij olaylarını tek bir noktadan görev hedeflerine aktarır.
    /// Kaynaklara bir kez abone olur; hedefler ne kadar sık bağlanıp çözülürse
    /// çözülsün yöneticilerin olay listeleri büyümez.
    /// </summary>
    public sealed class QuestSignalHub : IQuestSignals, IDisposable
    {
        private readonly CurrencyManager _currency;
        private readonly PrestigeManager _prestige;
        private readonly List<Station> _stations = new List<Station>();
        private bool _disposed;

        /// <param name="prestige">Prestij sistemi yoksa null; o zaman prestij olayı hiç gelmez.</param>
        public QuestSignalHub(CurrencyManager currency, IEnumerable<Station> stations, PrestigeManager prestige)
        {
            _currency = currency;
            _prestige = prestige;

            if (_currency != null)
            {
                _currency.onCurrencyEarned += RelayMoneyEarned;
            }

            if (stations != null)
            {
                foreach (Station station in stations)
                {
                    if (station != null)
                    {
                        _stations.Add(station);
                        station.onUpgraded += RelayStationUpgraded;
                    }
                }
            }

            if (_prestige != null)
            {
                _prestige.onPrestigePerformed += RelayPrestigePerformed;
            }
        }

        public event Action<double> onMoneyEarned;
        public event Action<Station> onStationUpgraded;
        public event Action<double> onPrestigePerformed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_currency != null)
            {
                _currency.onCurrencyEarned -= RelayMoneyEarned;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i] != null)
                {
                    _stations[i].onUpgraded -= RelayStationUpgraded;
                }
            }

            _stations.Clear();

            if (_prestige != null)
            {
                _prestige.onPrestigePerformed -= RelayPrestigePerformed;
            }

            onMoneyEarned = null;
            onStationUpgraded = null;
            onPrestigePerformed = null;
        }

        private void RelayMoneyEarned(double amount)
        {
            onMoneyEarned?.Invoke(amount);
        }

        private void RelayStationUpgraded(Station station)
        {
            onStationUpgraded?.Invoke(station);
        }

        private void RelayPrestigePerformed(double gemsEarned)
        {
            onPrestigePerformed?.Invoke(gemsEarned);
        }
    }
}
