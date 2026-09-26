using System;
using System.Collections.Generic;
using IdleRestaurant.Core;
using IdleRestaurant.Data;
using UnityEngine;

namespace IdleRestaurant.Gameplay
{
    /// <summary>
    /// Prestij döngüsü: ömür boyu kazançtan Gem hesaplar, restoranı sıfırlar
    /// ve Gem ile kalıcı çarpanlar satar.
    ///
    /// ── Gem formülü ─────────────────────────────────────────────────────────
    /// toplamGem(K) = floor(gemScale * (K / earningsForFirstGem) ^ gemExponent)
    /// bekleyenGem  = toplamGem(ömür boyu kazanç) - şimdiye kadar alınan Gem
    ///
    /// Varsayılanlarla (1M, 0.5): 1M → 1, 4M → 2, 100M → 10, 1T → 1000 Gem.
    /// Formül ömür boyu kazancın TAMAMINA bakar ve alınanı düşer; böylece her
    /// para bir kez sayılır ve prestij zamanlaması oyuncuyu cezalandırmaz
    /// (bugün sıfırlamak ile yarın sıfırlamak toplamda aynı Gem'i verir).
    ///
    /// ── Sorumluluk sınırı ───────────────────────────────────────────────────
    /// PrestigeManager çarpanları HESAPLAR ama istasyonlara uygulamaz;
    /// <see cref="onMultipliersChanged"/> ile duyurur. Uygulama, reklam
    /// hızlandırıcısıyla birleştirmeyi zaten yapan GameManager'da. Kaydetme
    /// de burada değil: sistem <see cref="ISaveable"/> olarak SaveManager'a
    /// kaydolur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrestigeManager : MonoBehaviour, ISaveable
    {
        [Header("Gem formülü")]
        [Tooltip("İlk Gem için gereken ömür boyu kazanç.")]
        [SerializeField] private double earningsForFirstGem = 1_000_000d;

        [Tooltip("Formülün üssü. 0.5 = karekök: 4 kat kazanç 2 kat Gem verir.")]
        [SerializeField, Range(0.1f, 1f)] private float gemExponent = 0.5f;

        [Tooltip("Formülün çarpanı.")]
        [SerializeField] private double gemScale = 1d;

        [Tooltip("Sıfırlamanın açılması için bekleyen en az Gem.")]
        [SerializeField, Min(1)] private int minGemsToPrestige = 1;

        [Header("Kalıcı yükseltmeler")]
        [SerializeField] private List<PermanentUpgradeDefinition> permanentUpgrades = new List<PermanentUpgradeDefinition>
        {
            new PermanentUpgradeDefinition("global_speed", "Genel Hız", PermanentUpgradeType.ProductionSpeed, 0.10f, 1d, 2d, 0),
            new PermanentUpgradeDefinition("global_income", "Genel Gelir", PermanentUpgradeType.Income, 0.25f, 2d, 2d, 0)
        };

        private readonly Dictionary<string, int> _upgradeLevels = new Dictionary<string, int>();
        private CurrencyManager _currency;
        private IReadOnlyList<Station> _stations;
        private double _gems;
        private double _totalGemsEarned;
        private int _prestigeCount;
        private float _speedMultiplier = 1f;
        private double _incomeMultiplier = 1d;

        /// <summary>Gem bakiyesi değiştiğinde yeni bakiyeyle tetiklenir.</summary>
        public event Action<double> onGemsChanged;

        /// <summary>Restoran sıfırlandığında, bu prestijde kazanılan Gem'le tetiklenir.</summary>
        public event Action<double> onPrestigePerformed;

        /// <summary>Kalıcı hız veya gelir çarpanı değiştiğinde tetiklenir.</summary>
        public event Action onMultipliersChanged;

        /// <summary>Kalıcı yükseltme satın alındığında (tanım, yeni seviye) ile tetiklenir.</summary>
        public event Action<PermanentUpgradeDefinition, int> onUpgradePurchased;

        public bool IsInitialized => _currency != null;
        public double Gems => _gems;
        public double TotalGemsEarned => _totalGemsEarned;
        public int PrestigeCount => _prestigeCount;
        public int MinGemsToPrestige => minGemsToPrestige;

        /// <summary>Kalıcı yükseltmelerden gelen üretim hızı çarpanı (1.3 = +%30).</summary>
        public float SpeedMultiplier => _speedMultiplier;

        /// <summary>Kalıcı yükseltmelerden gelen gelir çarpanı (1.5 = +%50).</summary>
        public double IncomeMultiplier => _incomeMultiplier;

        public IReadOnlyList<PermanentUpgradeDefinition> PermanentUpgrades => permanentUpgrades;

        /// <summary>Şimdi sıfırlanırsa kazanılacak Gem.</summary>
        public double PendingGems => IsInitialized ? CalculatePendingGems(_currency.LifetimeEarnings) : 0d;

        public bool CanPrestige => PendingGems >= minGemsToPrestige;

        /// <summary>Bekleyen Gem'e bir tane daha eklenmesi için kazanılması gereken para.</summary>
        public double EarningsUntilNextGem
        {
            get
            {
                if (!IsInitialized)
                {
                    return 0d;
                }

                double lifetime = _currency.LifetimeEarnings;
                double required = GetEarningsRequiredForTotalGems(CalculateTotalGemsForEarnings(lifetime) + 1d);
                return Math.Max(0d, required - lifetime);
            }
        }

        /// <summary>
        /// Bağımlılıkları verir. Sıfırlama bu para yöneticisini ve istasyonları
        /// kullanır; <see cref="RestoreState"/>'ten önce çağrılmalı.
        /// </summary>
        public void Initialize(CurrencyManager currency, IReadOnlyList<Station> stations)
        {
            if (currency == null)
            {
                Debug.LogError("[PrestigeManager] CurrencyManager verilmedi; prestij çalışmayacak.", this);
                return;
            }

            _currency = currency;
            _stations = stations ?? Array.Empty<Station>();
            WarnAboutDuplicateUpgradeIds();
            RecalculateMultipliers();
        }

        // ── Formül ─────────────────────────────────────────────────────────────

        /// <summary>Verilen ömür boyu kazancın toplamda hak ettirdiği Gem (daha önce alınanlar dahil).</summary>
        public double CalculateTotalGemsForEarnings(double lifetimeEarnings)
        {
            if (double.IsNaN(lifetimeEarnings) || lifetimeEarnings < earningsForFirstGem)
            {
                return 0d;
            }

            // Küçük epsilon: pow(4, 0.5) = 1.9999999999999998 gibi artıklar bir Gem kaybettirmesin.
            double gems = Math.Floor(gemScale * Math.Pow(lifetimeEarnings / earningsForFirstGem, gemExponent) + 1e-9);
            return double.IsInfinity(gems) ? double.MaxValue : gems;
        }

        /// <summary>Verilen ömür boyu kazançla şimdi sıfırlanırsa alınacak Gem.</summary>
        public double CalculatePendingGems(double lifetimeEarnings)
        {
            return Math.Max(0d, CalculateTotalGemsForEarnings(lifetimeEarnings) - _totalGemsEarned);
        }

        /// <summary>Formülün tersi: toplamda <paramref name="totalGems"/> Gem için gereken ömür boyu kazanç.</summary>
        public double GetEarningsRequiredForTotalGems(double totalGems)
        {
            if (double.IsNaN(totalGems) || totalGems <= 0d)
            {
                return 0d;
            }

            double required = earningsForFirstGem * Math.Pow(totalGems / gemScale, 1d / gemExponent);
            return double.IsInfinity(required) ? double.MaxValue : required;
        }

        // ── Sıfırlama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Restoranı sıfırlar: istasyonlar başlangıç seviyelerine, para
        /// başlangıç bakiyesine döner; bekleyen Gem oyuncuya eklenir. Ömür
        /// boyu kazanç, Gem'ler ve kalıcı yükseltmeler korunur.
        /// </summary>
        /// <returns>Bekleyen Gem <see cref="MinGemsToPrestige"/>'ın altındaysa false; hiçbir şey değişmez.</returns>
        public bool ResetRestaurant()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[PrestigeManager] Initialize çağrılmadan ResetRestaurant yok sayıldı.", this);
                return false;
            }

            double earned = PendingGems;
            if (earned < minGemsToPrestige)
            {
                return false;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                Station station = _stations[i];
                if (station != null && station.IsInitialized)
                {
                    station.SetLevel(station.StartingLevel);
                }
            }

            _currency.SetBalance(_currency.StartingBalance);

            _gems = ClampAmount(_gems + earned);
            _totalGemsEarned = ClampAmount(_totalGemsEarned + earned);
            _prestigeCount++;

            onGemsChanged?.Invoke(_gems);
            onPrestigePerformed?.Invoke(earned);
            return true;
        }

        /// <summary>Prestij dışı kaynaklardan (görev ödülü, IAP) Gem ekler.</summary>
        public void AddGems(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
            {
                return;
            }

            _gems = ClampAmount(_gems + amount);
            onGemsChanged?.Invoke(_gems);
        }

        // ── Kalıcı yükseltmeler ────────────────────────────────────────────────

        public PermanentUpgradeDefinition FindUpgrade(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId))
            {
                return null;
            }

            for (int i = 0; i < permanentUpgrades.Count; i++)
            {
                PermanentUpgradeDefinition definition = permanentUpgrades[i];
                if (definition != null && definition.upgradeId == upgradeId)
                {
                    return definition;
                }
            }

            return null;
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            int level;
            return !string.IsNullOrEmpty(upgradeId) && _upgradeLevels.TryGetValue(upgradeId, out level) ? level : 0;
        }

        /// <summary>Bir sonraki seviyenin Gem maliyeti; tanım yoksa veya son seviyedeyse PositiveInfinity.</summary>
        public double GetUpgradeCost(string upgradeId)
        {
            PermanentUpgradeDefinition definition = FindUpgrade(upgradeId);
            if (definition == null)
            {
                return double.PositiveInfinity;
            }

            int level = GetUpgradeLevel(upgradeId);
            return definition.IsMaxed(level) ? double.PositiveInfinity : definition.GetCost(level);
        }

        public bool IsUpgradeMaxed(string upgradeId)
        {
            PermanentUpgradeDefinition definition = FindUpgrade(upgradeId);
            return definition == null || definition.IsMaxed(GetUpgradeLevel(upgradeId));
        }

        public bool CanPurchaseUpgrade(string upgradeId)
        {
            double cost = GetUpgradeCost(upgradeId);
            return !double.IsInfinity(cost) && _gems >= cost;
        }

        /// <summary>Gem harcayarak kalıcı yükseltmenin bir sonraki seviyesini alır.</summary>
        /// <returns>Gem yetmiyorsa, tanım yoksa veya son seviyedeyse false; hiçbir şey değişmez.</returns>
        public bool TryPurchaseUpgrade(string upgradeId)
        {
            if (!CanPurchaseUpgrade(upgradeId))
            {
                return false;
            }

            PermanentUpgradeDefinition definition = FindUpgrade(upgradeId);
            int newLevel = GetUpgradeLevel(upgradeId) + 1;

            _gems = Math.Max(0d, _gems - definition.GetCost(newLevel - 1));
            _upgradeLevels[definition.upgradeId] = newLevel;

            RecalculateMultipliers();
            onGemsChanged?.Invoke(_gems);
            onUpgradePurchased?.Invoke(definition, newLevel);
            return true;
        }

        private void RecalculateMultipliers()
        {
            double speedBonus = 0d;
            double incomeBonus = 0d;

            for (int i = 0; i < permanentUpgrades.Count; i++)
            {
                PermanentUpgradeDefinition definition = permanentUpgrades[i];
                if (definition == null)
                {
                    continue;
                }

                int level = GetUpgradeLevel(definition.upgradeId);

                // Tasarımcı maxLevel'ı düşürdüyse eski kayıttaki fazla seviye bonus vermesin.
                if (definition.maxLevel > 0)
                {
                    level = Math.Min(level, definition.maxLevel);
                }

                double bonus = definition.GetTotalBonus(level);
                switch (definition.type)
                {
                    case PermanentUpgradeType.ProductionSpeed:
                        speedBonus += bonus;
                        break;
                    case PermanentUpgradeType.Income:
                        incomeBonus += bonus;
                        break;
                }
            }

            _speedMultiplier = (float)Math.Min(1d + speedBonus, float.MaxValue);
            _incomeMultiplier = 1d + incomeBonus;
            onMultipliersChanged?.Invoke();
        }

        // ── Kayıt (ISaveable) ──────────────────────────────────────────────────

        public void CaptureState(SaveData data)
        {
            PrestigeSaveData state = new PrestigeSaveData
            {
                gems = _gems,
                totalGemsEarned = _totalGemsEarned,
                prestigeCount = _prestigeCount
            };

            // Tanımı artık olmayan yükseltmeler de yazılır: geçici olarak
            // kaldırılan bir yükseltmenin satın alınmış seviyesi kaybolmasın.
            foreach (KeyValuePair<string, int> pair in _upgradeLevels)
            {
                if (pair.Value > 0)
                {
                    state.upgrades.Add(new PermanentUpgradeSaveData(pair.Key, pair.Value));
                }
            }

            data.prestige = state;
        }

        public void RestoreState(SaveData data)
        {
            _upgradeLevels.Clear();

            PrestigeSaveData state = data != null ? data.prestige : null;
            if (state != null)
            {
                _gems = state.gems;
                _totalGemsEarned = state.totalGemsEarned;
                _prestigeCount = state.prestigeCount;

                for (int i = 0; i < state.upgrades.Count; i++)
                {
                    PermanentUpgradeSaveData entry = state.upgrades[i];
                    _upgradeLevels[entry.upgradeId] = Math.Max(0, entry.level);
                }
            }
            else
            {
                _gems = 0d;
                _totalGemsEarned = 0d;
                _prestigeCount = 0;
            }

            RecalculateMultipliers();
            onGemsChanged?.Invoke(_gems);
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        private void WarnAboutDuplicateUpgradeIds()
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < permanentUpgrades.Count; i++)
            {
                PermanentUpgradeDefinition definition = permanentUpgrades[i];
                if (definition != null && !seen.Add(definition.upgradeId))
                {
                    Debug.LogError($"[PrestigeManager] '{definition.upgradeId}' kimliği birden fazla yükseltmede " +
                                   "kullanılıyor; seviyeleri ortak sayılır ve bonus iki kez uygulanır.", this);
                }
            }
        }

        private static double ClampAmount(double value)
        {
            return double.IsNaN(value) || value < 0d ? 0d : Math.Min(value, double.MaxValue);
        }

        private void OnValidate()
        {
            if (double.IsNaN(earningsForFirstGem) || earningsForFirstGem < 1d)
            {
                earningsForFirstGem = 1d;
            }

            if (double.IsNaN(gemScale) || gemScale <= 0d)
            {
                gemScale = 1d;
            }

            for (int i = 0; i < permanentUpgrades.Count; i++)
            {
                if (permanentUpgrades[i] != null)
                {
                    permanentUpgrades[i].Validate();
                }
            }
        }

        [ContextMenu("Debug/Add 10 Gems")]
        private void DebugAddTenGems()
        {
            AddGems(10d);
        }
    }
}
