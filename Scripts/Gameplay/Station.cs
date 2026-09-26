using System;
using System.Collections;
using IdleRestaurant.Core;
using UnityEngine;

namespace IdleRestaurant.Gameplay
{
    /// <summary>
    /// Sahnedeki tek bir masa/tezgah: seviyesi, üretim döngüsü ve yükseltmesi.
    ///
    /// ── Seviye 0 ────────────────────────────────────────────────────────────
    /// Seviye 0 "henüz satın alınmamış" demek. Gelir baseIncome * 0 = 0
    /// olduğu için üretim döngüsü hiç çalışmaz; ilk <see cref="TryUpgrade"/>
    /// istasyonu baseCost * 1.15^0 = baseCost'a satın alır. Böylece satın
    /// alma ve yükseltme tek bir yol.
    ///
    /// ── Başlatma ────────────────────────────────────────────────────────────
    /// İstasyon kendi kendine çalışmaya başlamaz; <see cref="GameManager"/>
    /// kaydı yükledikten sonra <see cref="Initialize"/> çağırır. Awake/Start
    /// sırasına güvenmek, kayıt yüklenmeden seviye 1'de para üreten bir
    /// kare bırakabilirdi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Station : MonoBehaviour
    {
        [SerializeField] private StationData data;

        [Tooltip("Kayıt yoksa (ilk açılış) bu istasyonun seviyesi. 0 = kilitli. " +
                 "Oyuncunun ilk gelirini alabilmesi için en az bir istasyonu 1 yapın.")]
        [SerializeField, Min(0)] private int startingLevel;

        private CurrencyManager _currency;
        private Coroutine _productionRoutine;
        private float _cycleElapsed;
        private float _speedMultiplier = 1f;
        private double _incomeMultiplier = 1d;
        private bool _initialized;

        /// <summary>Seviye değiştiğinde (yükseltme, kayıt yükleme, prestij sıfırlaması) tetiklenir.</summary>
        public event Action<Station> onLevelChanged;

        /// <summary>
        /// YALNIZCA oyuncu <see cref="TryUpgrade"/> ile seviye satın aldığında
        /// tetiklenir. Kayıt yükleme ve prestij sıfırlaması tetiklemez; "N kez
        /// yükselt" görevleri buna bağlanır.
        /// </summary>
        public event Action<Station> onUpgraded;

        /// <summary>Bir veya daha fazla üretim döngüsü tamamlandığında, kazanılan toplam tutarla tetiklenir.</summary>
        public event Action<Station, double> onProductionCompleted;

        public StationData Data => data;
        public string StationId => data != null ? data.StationId : string.Empty;
        public string DisplayName => data != null ? data.StationName : name;
        public int Level { get; private set; }
        public int StartingLevel => startingLevel;
        public bool IsInitialized => _initialized;
        public bool IsUnlocked => Level > 0;
        public float SpeedMultiplier => _speedMultiplier;
        public double IncomeMultiplier => _incomeMultiplier;

        /// <summary>Hızlandırıcı dahil, bir üretim döngüsünün şu anki süresi (saniye).</summary>
        public float CurrentCycleTime => data != null ? data.CycleTime / _speedMultiplier : 0f;

        /// <summary>Mevcut döngüde geçen süre (saniye).</summary>
        public float CycleElapsed => _cycleElapsed;

        /// <summary>Mevcut döngünün 0-1 arası ilerlemesi; ilerleme çubukları için.</summary>
        public float CycleProgress
        {
            get
            {
                float cycle = CurrentCycleTime;
                return IsUnlocked && cycle > 0f ? Mathf.Clamp01(_cycleElapsed / cycle) : 0f;
            }
        }

        /// <summary>Bir sonraki seviyenin maliyeti: baseCost * costMultiplier^Level.</summary>
        public double UpgradeCost => GetUpgradeCost(Level);

        /// <summary>
        /// Maliyet double'ın sınırını aştığında (≈5000 seviye) Math.Pow
        /// Infinity döner; o noktada istasyon fiilen son seviyesindedir.
        /// </summary>
        public bool IsMaxLevel => Level == int.MaxValue || !IsFinite(UpgradeCost);

        /// <summary>
        /// Bir üretim döngüsünün şu anki kazancı: baseIncome * Level (* kalıcı
        /// gelir çarpanı; çarpan yoksa 1).
        /// </summary>
        public double CurrentIncome => GetIncome(Level);

        /// <summary>Yükseltmeden sonraki döngü kazancı; UI'da "sonraki seviye" önizlemesi için.</summary>
        public double NextLevelIncome => Level == int.MaxValue ? CurrentIncome : GetIncome(Level + 1);

        /// <summary>
        /// Hız çarpanları HARİÇ, temel döngü süresiyle saniye başı gelir (gelir
        /// çarpanı dahil). Hangi hız çarpanlarının kalıcı olduğunu istasyon
        /// bilmez; GameManager kalıcı olanları toplama kendisi uygular.
        /// </summary>
        public double IncomePerSecond => data != null ? CurrentIncome / data.CycleTime : 0d;

        public bool CanAffordUpgrade
        {
            get
            {
                if (!_initialized || IsMaxLevel)
                {
                    return false;
                }

                return _currency.CanAfford(UpgradeCost);
            }
        }

        public double GetUpgradeCost(int level)
        {
            if (data == null)
            {
                return double.PositiveInfinity;
            }

            return data.BaseCost * Math.Pow(data.CostMultiplier, Math.Max(0, level));
        }

        public double GetIncome(int level)
        {
            return data != null ? data.BaseIncome * Math.Max(0, level) * _incomeMultiplier : 0d;
        }

        /// <summary>
        /// İstasyonu para yöneticisine bağlar, seviyesini ayarlar ve (seviye
        /// 0'dan büyükse) üretimi başlatır. Birden fazla çağrılabilir; ikinci
        /// çağrı yalnızca seviyeyi günceller.
        /// </summary>
        public void Initialize(CurrencyManager currency, int level)
        {
            if (currency == null)
            {
                Debug.LogError($"[Station] '{name}': CurrencyManager verilmedi, istasyon çalışmayacak.", this);
                return;
            }

            if (data == null)
            {
                Debug.LogError($"[Station] '{name}': StationData atanmamış, istasyon çalışmayacak.", this);
                return;
            }

            _currency = currency;
            _initialized = true;
            ApplyLevel(level, notify: true);
        }

        /// <summary>Kayıt yükleme/hile menüsü gibi dış kaynaklar için seviyeyi doğrudan ayarlar.</summary>
        public void SetLevel(int level)
        {
            if (!_initialized)
            {
                Debug.LogWarning($"[Station] '{name}': Initialize çağrılmadan SetLevel yok sayıldı.", this);
                return;
            }

            ApplyLevel(level, notify: true);
        }

        /// <summary>
        /// Bir sonraki seviyeyi satın almayı dener. Para yetmiyorsa bakiye
        /// değişmez ve false döner.
        /// </summary>
        public bool TryUpgrade()
        {
            if (!_initialized || IsMaxLevel)
            {
                return false;
            }

            double cost = UpgradeCost;

            // Ücretsiz seviye (baseCost = 0) harcama gerektirmez; TrySpend sıfırı reddeder.
            if (cost > 0d && !_currency.TrySpend(cost))
            {
                return false;
            }

            ApplyLevel(Level + 1, notify: true);
            onUpgraded?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Döngü başı kazancı çarpar (1.25 = +%25). Prestij yükseltmeleri
        /// gibi kalıcı bonuslar için; seviyeyi değiştirmez.
        /// </summary>
        public void SetIncomeMultiplier(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0d)
            {
                multiplier = 1d;
            }

            _incomeMultiplier = multiplier;
        }

        /// <summary>
        /// Üretim hızını çarpar (2 = iki kat hızlı). Döngünün yüzde olarak
        /// ilerlemesi korunur: yarısı dolmuş bir döngü hızlandırıcıyla da
        /// yarısı dolmuş kalır, anında tamamlanmaz.
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || multiplier <= 0f)
            {
                multiplier = 1f;
            }

            if (Mathf.Approximately(multiplier, _speedMultiplier))
            {
                return;
            }

            _cycleElapsed *= _speedMultiplier / multiplier;
            _speedMultiplier = multiplier;
        }

        private void ApplyLevel(int level, bool notify)
        {
            int clamped = Mathf.Max(0, level);
            bool changed = clamped != Level;
            Level = clamped;

            if (!IsUnlocked)
            {
                _cycleElapsed = 0f;
            }

            RefreshProductionState();

            if (notify && changed)
            {
                onLevelChanged?.Invoke(this);
            }
        }

        private void RefreshProductionState()
        {
            bool shouldRun = _initialized && IsUnlocked && isActiveAndEnabled;

            if (shouldRun && _productionRoutine == null)
            {
                _productionRoutine = StartCoroutine(ProductionLoop());
            }
            else if (!shouldRun && _productionRoutine != null)
            {
                StopCoroutine(_productionRoutine);
                _productionRoutine = null;
            }
        }

        /// <summary>
        /// Her karede ilerleyen üretim döngüsü. WaitForSeconds yerine kare
        /// kare sayılıyor çünkü (1) ilerleme çubuğu ara değeri görmeli,
        /// (2) hızlandırıcı döngünün ortasında devreye girebilmeli.
        ///
        /// Tek karede birden fazla döngü tamamlanabilir (kısa cycleTime,
        /// takılan bir kare); hepsi tek seferde ödenir, hiçbiri kaybolmaz.
        /// </summary>
        private IEnumerator ProductionLoop()
        {
            while (true)
            {
                float cycle = CurrentCycleTime;
                _cycleElapsed += Time.deltaTime;

                if (cycle > 0f && _cycleElapsed >= cycle)
                {
                    int completedCycles = Mathf.FloorToInt(_cycleElapsed / cycle);
                    _cycleElapsed -= completedCycles * cycle;
                    Produce(completedCycles);
                }

                yield return null;
            }
        }

        private void Produce(int cycles)
        {
            double amount = CurrentIncome * cycles;
            if (amount <= 0d)
            {
                return;
            }

            _currency.AddCurrency(amount);
            onProductionCompleted?.Invoke(this, amount);
        }

        private void OnEnable()
        {
            RefreshProductionState();
        }

        private void OnDisable()
        {
            // Unity devre dışı kalan nesnenin coroutine'lerini zaten durdurur;
            // referans temizlenmezse yeniden etkinleşince döngü başlamaz.
            if (_productionRoutine != null)
            {
                StopCoroutine(_productionRoutine);
                _productionRoutine = null;
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
