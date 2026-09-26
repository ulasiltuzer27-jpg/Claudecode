using UnityEngine;

namespace IdleRestaurant.Gameplay.Customers
{
    /// <summary>
    /// Müşterinin oturduğu/beklediği tek bir yer (masa, tezgah önü). Bir
    /// istasyona bağlıdır: siparişi o istasyon hazırlar. Aynı anda tek
    /// müşteri alır; bir istasyona daha çok müşteri için birden fazla
    /// CustomerSeat ekleyin.
    ///
    /// Yalnızca istasyonu satın alınmış (seviye &gt; 0) ve etkin masalar
    /// müşteri kabul eder; prestij sıfırlaması istasyonu kilitlerse masadaki
    /// müşteri kalkar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerSeat : MonoBehaviour
    {
        [Tooltip("Bu masanın siparişlerini hazırlayan istasyon. Boşsa üst nesnelerde aranır.")]
        [SerializeField] private Station station;

        [Tooltip("Müşterinin yürüyüp durduğu nokta. Boşsa bu nesnenin konumu.")]
        [SerializeField] private Transform standPoint = null;

        private CustomerController _occupant;

        public Station Station => station;
        public Transform StandPoint => standPoint != null ? standPoint : transform;
        public CustomerController Occupant => _occupant;
        public bool IsOccupied => _occupant != null;

        /// <summary>Masa hizmet verebiliyor mu: etkin ve istasyonu satın alınmış.</summary>
        public bool IsServiceAvailable => isActiveAndEnabled && station != null && station.IsInitialized && station.IsUnlocked;

        /// <summary>Hizmet veriyor ve boş.</summary>
        public bool IsAvailable => IsServiceAvailable && !IsOccupied;

        /// <summary>Masayı müşteriye ayırır. Masa doluysa veya hizmet vermiyorsa false.</summary>
        public bool TryReserve(CustomerController customer)
        {
            if (customer == null || !IsAvailable)
            {
                return false;
            }

            _occupant = customer;
            return true;
        }

        /// <summary>Masayı boşaltır. Yalnızca masayı tutan müşteri boşaltabilir.</summary>
        public void Release(CustomerController customer)
        {
            if (_occupant == customer)
            {
                _occupant = null;
            }
        }

        private void Awake()
        {
            if (station == null)
            {
                station = GetComponentInParent<Station>();
            }

            if (station == null)
            {
                Debug.LogWarning($"[CustomerSeat] '{name}' bir istasyona bağlı değil; müşteri almayacak.", this);
            }
        }

        private void OnDisable()
        {
            // Masa kapatılırsa üzerindeki müşteri Update'te IsServiceAvailable'ı
            // false görüp kalkar; ayırma burada da bırakılıyor ki yeniden
            // etkinleşen masa dolu kalmasın.
            _occupant = null;
        }
    }
}
