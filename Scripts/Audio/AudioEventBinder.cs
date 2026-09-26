using System.Collections.Generic;
using IdleRestaurant.Core;
using IdleRestaurant.Gameplay;
using IdleRestaurant.Gameplay.Quests;
using IdleRestaurant.UI;
using UnityEngine;

namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Mevcut oyun olaylarını ses efektine ve titreşime çevirir. Hiçbir oyun
    /// sistemi ses çalmaz; ses yalnızca buradan tetiklenir. Bir olayın sesini
    /// değiştirmek veya yeni bir olaya ses eklemek yalnızca bu sınıfa dokunur.
    ///
    /// Eşleme:
    /// <list type="bullet">
    /// <item>UIManager/paneller buton tıklaması → ButtonClick (hafif titreşim)</item>
    /// <item>CurrencyManager para kazanıldı → CoinCollect, TİTREŞİMSİZ (pasif gelir
    ///       her döngüde gelir; kütüphanedeki aralık sesi seyreltir)</item>
    /// <item>Toplu para: çevrimdışı kazanç, görev ödülü → CoinCollect + titreşim</item>
    /// <item>Station yükseltildi → StationUpgrade (orta)</item>
    /// <item>QuestManager görev tamamlandı → QuestComplete (orta)</item>
    /// <item>PrestigeManager sıfırlama → PrestigeTrigger (güçlü)</item>
    /// </list>
    ///
    /// GameManager, oyun kurulduktan sonra <see cref="Bind"/> çağırır;
    /// yükleme sırasındaki seviye ve bakiye ayarları böylece ses çıkarmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioEventBinder : MonoBehaviour
    {
        private readonly List<Station> _stations = new List<Station>();
        private ISfxPlayer _player;
        private GameManager _game;
        private CurrencyManager _currency;
        private QuestManager _quests;
        private PrestigeManager _prestige;
        private UIManager _ui;

        public bool IsBound => _player != null;

        /// <summary>
        /// Olaylara abone olur. Tekrar çağrılırsa önceki abonelikler çözülür.
        /// Sahnede olmayan sistemler (görev, prestij, UI) atlanır.
        /// </summary>
        public void Bind(ISfxPlayer player, GameManager game)
        {
            Unbind();

            if (player == null || game == null)
            {
                return;
            }

            _player = player;
            _game = game;
            _currency = game.Currency;
            _quests = game.Quests;
            _prestige = game.Prestige;
            _ui = game.UI;

            _game.onOfflineEarningsGranted += HandleOfflineEarningsGranted;

            if (_currency != null)
            {
                _currency.onCurrencyEarned += HandleCurrencyEarned;
            }

            for (int i = 0; i < game.Stations.Count; i++)
            {
                Station station = game.Stations[i];
                if (station != null)
                {
                    _stations.Add(station);
                    station.onUpgraded += HandleStationUpgraded;
                }
            }

            if (_quests != null)
            {
                _quests.onQuestCompleted += HandleQuestCompleted;
                _quests.onQuestRewarded += HandleQuestRewarded;
            }

            if (_prestige != null)
            {
                _prestige.onPrestigePerformed += HandlePrestigePerformed;
            }

            if (_ui != null)
            {
                _ui.onButtonClicked += HandleButtonClicked;
            }
        }

        public void Unbind()
        {
            if (_game != null)
            {
                _game.onOfflineEarningsGranted -= HandleOfflineEarningsGranted;
            }

            if (_currency != null)
            {
                _currency.onCurrencyEarned -= HandleCurrencyEarned;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i] != null)
                {
                    _stations[i].onUpgraded -= HandleStationUpgraded;
                }
            }

            _stations.Clear();

            if (_quests != null)
            {
                _quests.onQuestCompleted -= HandleQuestCompleted;
                _quests.onQuestRewarded -= HandleQuestRewarded;
            }

            if (_prestige != null)
            {
                _prestige.onPrestigePerformed -= HandlePrestigePerformed;
            }

            if (_ui != null)
            {
                _ui.onButtonClicked -= HandleButtonClicked;
            }

            _player = null;
            _game = null;
            _currency = null;
            _quests = null;
            _prestige = null;
            _ui = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ── Olay → ses ─────────────────────────────────────────────────────────

        private void HandleButtonClicked()
        {
            _player.Play(SfxType.ButtonClick, true);
        }

        private void HandleCurrencyEarned(double amount)
        {
            _player.Play(SfxType.CoinCollect, false);
        }

        /// <summary>
        /// Çevrimdışı kazanç önce CurrencyManager olayını tetikler (ses orada
        /// çaldı ve aralığa takılır); burada yalnızca "para toplama"
        /// titreşimi eklenmiş olur.
        /// </summary>
        private void HandleOfflineEarningsGranted(double amount, double seconds)
        {
            _player.Play(SfxType.CoinCollect, true);
        }

        private void HandleQuestRewarded(Quest quest, double reward)
        {
            if (reward > 0d)
            {
                _player.Play(SfxType.CoinCollect, true);
            }
        }

        private void HandleStationUpgraded(Station station)
        {
            _player.Play(SfxType.StationUpgrade, true);
        }

        private void HandleQuestCompleted(Quest quest)
        {
            _player.Play(SfxType.QuestComplete, true);
        }

        private void HandlePrestigePerformed(double gemsEarned)
        {
            _player.Play(SfxType.PrestigeTrigger, true);
        }
    }
}
