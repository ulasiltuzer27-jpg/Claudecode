using System;
using System.Globalization;
using UnityEngine;

namespace IdleRestaurant.Core
{
    /// <summary>
    /// Oyuncunun parasını tutan tek kaynak.
    ///
    /// ── Neden double ────────────────────────────────────────────────────────
    /// Idle oyunlarda bakiye birkaç saat içinde long'un sınırını (9.2e18)
    /// aşar. double ~1.8e308'e kadar gider; kaybedilen hassasiyet (15-16
    /// anlamlı basamak) ekranda zaten 3 basamak gösterildiği için görünmez.
    ///
    /// ── Taşma ───────────────────────────────────────────────────────────────
    /// double taşınca istisna atmaz, sessizce Infinity olur ve Infinity bir
    /// kez bakiyeye girdiğinde her karşılaştırmayı bozar (Infinity - x =
    /// Infinity, kayıtta geçersiz JSON). Bu yüzden her yazım
    /// <see cref="MaxBalance"/>'a kırpılır; NaN/Infinity/negatif girdiler
    /// hiç kabul edilmez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyManager : MonoBehaviour
    {
        /// <summary>Bakiyenin çıkabileceği en yüksek değer.</summary>
        public const double MaxBalance = double.MaxValue;

        [Tooltip("Kayıt yoksa (ilk açılış) oyuncunun başladığı para.")]
        [SerializeField] private double startingBalance = 10d;

        private double _balance;

        /// <summary>
        /// Bakiye her değiştiğinde yeni bakiyeyle tetiklenir. UI metinleri ve
        /// yükseltme butonlarının etkinliği buna bağlanır.
        /// </summary>
        public event Action<double> onCurrencyChanged;

        public double Balance => _balance;
        public double StartingBalance => startingBalance;

        /// <summary>
        /// Bakiyeyi doğrudan ayarlar. Yalnızca başlatma/kayıt yükleme içindir;
        /// oyun içi kazanç ve harcama <see cref="AddCurrency"/> ve
        /// <see cref="TrySpend"/> üzerinden geçmeli.
        /// </summary>
        public void SetBalance(double value)
        {
            _balance = Sanitize(value);
            onCurrencyChanged?.Invoke(_balance);
        }

        /// <summary>Bakiyeye para ekler. Geçersiz veya sıfır/negatif tutarlar yok sayılır.</summary>
        /// <returns>Tutar kabul edildiyse true.</returns>
        public bool AddCurrency(double amount)
        {
            if (!IsValidAmount(amount))
            {
                return false;
            }

            // double.MaxValue + büyük sayı = Infinity; Math.Min onu geri kırpar.
            _balance = Math.Min(_balance + amount, MaxBalance);
            onCurrencyChanged?.Invoke(_balance);
            return true;
        }

        /// <summary>Bakiye yetiyorsa tutarı düşer.</summary>
        /// <returns>Harcama yapıldıysa true; bakiye değişmediyse false.</returns>
        public bool TrySpend(double amount)
        {
            if (!IsValidAmount(amount) || !CanAfford(amount))
            {
                return false;
            }

            // Kayan nokta çıkarması çok küçük negatif bir artık bırakabilir.
            _balance = Math.Max(0d, _balance - amount);
            onCurrencyChanged?.Invoke(_balance);
            return true;
        }

        public bool CanAfford(double amount)
        {
            return !double.IsNaN(amount) && !double.IsInfinity(amount) && amount >= 0d && _balance >= amount;
        }

        private static bool IsValidAmount(double amount)
        {
            return amount > 0d && !double.IsNaN(amount) && !double.IsInfinity(amount);
        }

        private static double Sanitize(double value)
        {
            if (double.IsNaN(value) || value <= 0d)
            {
                return 0d;
            }

            return value >= MaxBalance ? MaxBalance : value;
        }

        private void OnValidate()
        {
            startingBalance = Sanitize(startingBalance);
        }

        // ── Büyük sayı biçimlendirme ───────────────────────────────────────────

        /// <summary>
        /// 1000'in her kuvveti için bir sonek. K/M/B/T'den sonra idle
        /// türünün alışıldık "aa, ab, ... zz" dizisi geliyor. double'ın üst
        /// sınırı 10^308 → 102. basamak; dizi bunu rahatça karşılıyor.
        /// </summary>
        private static readonly string[] Suffixes = BuildSuffixes();

        private static string[] BuildSuffixes()
        {
            string[] named = { "", "K", "M", "B", "T" };
            const int letterTiers = 26 * 26;
            string[] result = new string[named.Length + letterTiers];
            named.CopyTo(result, 0);

            for (int i = 0; i < letterTiers; i++)
            {
                char first = (char)('a' + i / 26);
                char second = (char)('a' + i % 26);
                result[named.Length + i] = new string(new[] { first, second });
            }

            return result;
        }

        /// <summary>
        /// Sayıyı 3 anlamlı basamak ve sonekle yazar: 1250 → "1.25K",
        /// 3400000 → "3.40M", 12800000000 → "12.8B", 999 → "999".
        ///
        /// Değer yuvarlanmaz, AŞAĞI kesilir: 999.999 yuvarlansa "1000K" gibi
        /// geçersiz bir çıktı verir, ve yuvarlanan bir bakiye oyuncuya
        /// alamayacağı bir yükseltmeyi alabilirmiş gibi gösterir.
        ///
        /// Ondalık ayırıcı her zaman noktadır (InvariantCulture). Türkçe
        /// cihazlarda aksi halde "1,25K" yazılırdı.
        /// </summary>
        public static string FormatNumber(double value)
        {
            if (double.IsNaN(value))
            {
                return "0";
            }

            if (double.IsInfinity(value))
            {
                return value > 0d ? "∞" : "-∞";
            }

            if (value < 0d)
            {
                return "-" + FormatNumber(-value);
            }

            if (value < 1000d)
            {
                return FormatSmall(value);
            }

            int tier = (int)Math.Floor(Math.Log10(value) / 3d);
            double scaled = value / Math.Pow(1000d, tier);

            // Log10 kenar değerlerde (ör. tam 1e15) bir alt basamağı verebilir.
            if (scaled >= 1000d)
            {
                tier++;
                scaled /= 1000d;
            }
            else if (scaled < 1d)
            {
                tier--;
                scaled *= 1000d;
            }

            if (tier >= Suffixes.Length)
            {
                return value.ToString("0.00e0", CultureInfo.InvariantCulture);
            }

            int decimals = scaled >= 100d ? 0 : scaled >= 10d ? 1 : 2;
            double truncated = Truncate(scaled, decimals);
            return truncated.ToString(DecimalFormats[decimals], CultureInfo.InvariantCulture) + Suffixes[tier];
        }

        private static readonly string[] DecimalFormats = { "0", "0.0", "0.00" };
        private static readonly string[] TrimmedFormats = { "0", "0.#", "0.##" };

        /// <summary>
        /// 1000'in altı: aynı 3 anlamlı basamak kuralı, ama sondaki sıfırlar
        /// atılır. "5.00" yerine "5", "12.50" yerine "12.5".
        /// </summary>
        private static string FormatSmall(double value)
        {
            int decimals = value >= 100d ? 0 : value >= 10d ? 1 : 2;
            return Truncate(value, decimals).ToString(TrimmedFormats[decimals], CultureInfo.InvariantCulture);
        }

        private static double Truncate(double value, int decimals)
        {
            double factor = decimals == 0 ? 1d : decimals == 1 ? 10d : 100d;
            // Küçük epsilon: 3.4 * 100 = 339.99999999999994 gibi ikili temsil
            // artıklarının bir basamak kaybettirmesini önler.
            return Math.Floor(value * factor + 1e-9) / factor;
        }
    }
}
