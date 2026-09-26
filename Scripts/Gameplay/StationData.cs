using UnityEngine;

namespace IdleRestaurant.Gameplay
{
    /// <summary>
    /// Bir masa/tezgah türünün değişmeyen ayarları. Sahnedeki
    /// <see cref="Station"/> bileşenleri bu varlığa bakar; aynı veriyi
    /// paylaşan iki istasyon aynı ekonomiyle çalışır.
    ///
    /// Oluşturmak için: Project penceresi → Create → Idle Restaurant →
    /// Station Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStationData", menuName = "Idle Restaurant/Station Data", order = 0)]
    public sealed class StationData : ScriptableObject
    {
        public const double DefaultCostMultiplier = 1.15d;

        /// <summary>Uzun bekleme sürelerinin sıfıra bölmeye düşmemesi için alt sınır.</summary>
        private const float MinCycleTime = 0.05f;

        [Tooltip("Kayıt dosyasındaki benzersiz anahtar. Yayından sonra DEĞİŞTİRMEYİN: " +
                 "değişirse oyuncuların bu istasyondaki seviyeleri kaybolur.")]
        [SerializeField] private string stationId = "station";

        [Tooltip("Arayüzde gösterilen ad.")]
        [SerializeField] private string stationName = "Station";

        [Tooltip("Seviye 0 → 1 (satın alma) maliyeti. Sonraki her seviye costMultiplier ile çarpılır.")]
        [SerializeField] private double baseCost = 10d;

        [Tooltip("Seviye 1'de bir üretim döngüsünün kazancı. Gelir seviyeyle doğrusal artar.")]
        [SerializeField] private double baseIncome = 1d;

        [Tooltip("Bir üretim döngüsünün saniye cinsinden süresi.")]
        [SerializeField] private float cycleTime = 1f;

        [Tooltip("Her seviyede maliyetin çarpıldığı katsayı.")]
        [SerializeField] private double costMultiplier = DefaultCostMultiplier;

        public string StationId => stationId;
        public string StationName => stationName;
        public double BaseCost => baseCost;
        public double BaseIncome => baseIncome;
        public float CycleTime => Mathf.Max(MinCycleTime, cycleTime);
        public double CostMultiplier => costMultiplier;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stationId))
            {
                stationId = name;
            }

            stationId = stationId.Trim();

            // double alanlarda [Min] özniteliği Inspector'da çalışmadığı için
            // sınırlar burada uygulanıyor.
            if (double.IsNaN(baseCost) || baseCost < 0d)
            {
                baseCost = 0d;
            }

            if (double.IsNaN(baseIncome) || baseIncome < 0d)
            {
                baseIncome = 0d;
            }

            if (cycleTime < MinCycleTime)
            {
                cycleTime = MinCycleTime;
            }

            // 1'in altındaki çarpan yükseltmeyi her seviyede ucuzlatırdı.
            if (double.IsNaN(costMultiplier) || costMultiplier < 1d)
            {
                costMultiplier = 1d;
            }
        }
    }
}
