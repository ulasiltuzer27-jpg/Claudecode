using System;
using System.Collections.Generic;
using IdleRestaurant.Core;
using UnityEngine;

namespace IdleRestaurant.Gameplay.Customers
{
    /// <summary>
    /// Müşteri akışını yönetir: aralıklarla kapıda müşteri doğurur, boş ve
    /// hizmet veren bir masaya yönlendirir, servis edilen müşterinin
    /// bıraktığı parayı ekler ve çıkan müşteriyi havuza geri alır.
    ///
    /// ── Boş masa yoksa ──────────────────────────────────────────────────────
    /// Müşteri doğmaz. Kapıda bekleyen kuyruk yok; oyuncu yeni istasyon
    /// açtıkça ve masa ekledikçe restoran kalabalıklaşır.
    ///
    /// ── Bırakılan para ──────────────────────────────────────────────────────
    /// istasyonun döngü başı geliri × <see cref="paymentMultiplier"/>. Kalıcı
    /// gelir çarpanı istasyon gelirine zaten dahil. Bu, oyun açıkken gelen
    /// ek gelirdir; çevrimdışı hesaba girmez. 0 yapılırsa müşteriler yalnızca
    /// görseldir.
    ///
    /// ── Havuz ───────────────────────────────────────────────────────────────
    /// Müşteriler yok edilmez; çıkınca kapatılıp yeniden kullanılır. Mobilde
    /// sürekli Instantiate/Destroy çöp toplayıcı takılmalarına yol açar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerSpawner : MonoBehaviour
    {
        [Header("Müşteri")]
        [Tooltip("CustomerController bileşeni olan prefab.")]
        [SerializeField] private CustomerController customerPrefab = null;

        [Tooltip("Müşteri nesnelerinin ebeveyni. Boşsa bu nesne.")]
        [SerializeField] private Transform customerParent = null;

        [Header("Noktalar")]
        [Tooltip("Müşterilerin restorana girdiği kapı.")]
        [SerializeField] private Transform entryPoint = null;

        [Tooltip("Müşterilerin çıktığı nokta. Boşsa kapı kullanılır.")]
        [SerializeField] private Transform exitPoint = null;

        [Tooltip("Boş bırakılırsa sahnedeki tüm CustomerSeat bileşenleri (pasifler dahil) bulunur.")]
        [SerializeField] private List<CustomerSeat> seats = new List<CustomerSeat>();

        [Header("Akış")]
        [SerializeField, Min(0.1f)] private float minSpawnInterval = 3f;
        [SerializeField, Min(0.1f)] private float maxSpawnInterval = 6f;
        [SerializeField, Min(1)] private int maxActiveCustomers = 8;

        [Tooltip("Başlangıçta havuzda hazır bekleyen müşteri sayısı; ilk doğuşlardaki Instantiate takılmasını önler.")]
        [SerializeField, Min(0)] private int prewarmCount = 4;

        [Header("Sipariş ve ödeme")]
        [Tooltip("Masaya oturan müşterinin siparişi en erken bu kadar saniyede hazır olur.")]
        [SerializeField, Min(0f)] private float minWaitSeconds = 1f;

        [Tooltip("Sipariş için en uzun bekleme (sn); dolarsa müşteri ödemeden çıkar. 0 = sınırsız.")]
        [SerializeField, Min(0f)] private float patienceSeconds = 0f;

        [Tooltip("Bırakılan para = istasyonun döngü başı geliri × bu değer. 0 = müşteriler yalnızca görsel.")]
        [SerializeField, Min(0f)] private float paymentMultiplier = 1f;

        private readonly Stack<CustomerController> _pool = new Stack<CustomerController>();
        private readonly List<CustomerController> _active = new List<CustomerController>();
        private readonly List<CustomerSeat> _seatBuffer = new List<CustomerSeat>();
        private CurrencyManager _currency;
        private float _spawnTimer;

        /// <summary>Bir müşteri kapıdan girdiğinde tetiklenir.</summary>
        public event Action<CustomerController> onCustomerSpawned;

        /// <summary>Servis edilen müşteri para bıraktığında (müşteri, tutar) ile tetiklenir; uçan para yazısı vb. için.</summary>
        public event Action<CustomerController, double> onCustomerPaid;

        public bool IsRunning => _currency != null && isActiveAndEnabled;
        public int ActiveCustomerCount => _active.Count;
        public IReadOnlyList<CustomerSeat> Seats => seats;
        public IReadOnlyList<CustomerController> ActiveCustomers => _active;

        /// <summary>
        /// Akışı başlatır. GameManager, istasyonlar seviyeleriyle kurulduktan
        /// sonra çağırır; masaların müsaitliği istasyon seviyesine bağlı.
        /// </summary>
        public void Initialize(CurrencyManager currency)
        {
            if (currency == null)
            {
                Debug.LogError("[CustomerSpawner] CurrencyManager verilmedi; müşteri akışı başlamadı.", this);
                return;
            }

            if (customerPrefab == null || entryPoint == null)
            {
                Debug.LogError("[CustomerSpawner] Müşteri prefab'ı ve giriş noktası atanmalı; müşteri akışı başlamadı.", this);
                return;
            }

            _currency = currency;
            ResolveSeats();
            Prewarm();

            // İlk müşteri tam bir aralık beklemesin; restoran açılışta boş görünmesin.
            _spawnTimer = NextInterval() * 0.5f;
        }

        private void Update()
        {
            if (_currency == null)
            {
                return;
            }

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            _spawnTimer = NextInterval();
            TrySpawn();
        }

        /// <summary>
        /// Boş bir masa varsa hemen bir müşteri doğurur.
        /// </summary>
        /// <returns>Doğan müşteri; sınır dolu, masa yok veya akış başlamamışsa null.</returns>
        public CustomerController TrySpawn()
        {
            if (_currency == null || _active.Count >= maxActiveCustomers)
            {
                return null;
            }

            CustomerSeat seat = PickAvailableSeat();
            if (seat == null)
            {
                return null;
            }

            CustomerController customer = Rent();
            customer.transform.position = entryPoint.position;
            customer.gameObject.SetActive(true);

            // Etkinleştirme OnDisable/OnEnable zincirinden sonra ayrılıyor;
            // müşteri kapanıp açılırken masayı bırakmasın.
            if (!seat.TryReserve(customer))
            {
                Recycle(customer);
                return null;
            }

            customer.onOrderServed += HandleOrderServed;
            customer.onExited += HandleExited;
            customer.Begin(seat, exitPoint != null ? exitPoint : entryPoint,
                new CustomerVisitSettings(minWaitSeconds, patienceSeconds));

            _active.Add(customer);
            onCustomerSpawned?.Invoke(customer);
            return customer;
        }

        /// <summary>Tüm müşterileri anında havuza döndürür (sahne geçişi, oyun sıfırlama).</summary>
        public void DespawnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Recycle(_active[i]);
            }
        }

        // ── Müşteri olayları ───────────────────────────────────────────────────

        private void HandleOrderServed(CustomerController customer, Station station)
        {
            if (station == null || paymentMultiplier <= 0f)
            {
                return;
            }

            double payment = station.CurrentIncome * paymentMultiplier;
            if (payment > 0d && _currency.AddCurrency(payment))
            {
                onCustomerPaid?.Invoke(customer, payment);
            }
        }

        private void HandleExited(CustomerController customer)
        {
            Recycle(customer);
        }

        // ── Havuz ──────────────────────────────────────────────────────────────

        private CustomerController Rent()
        {
            while (_pool.Count > 0)
            {
                CustomerController pooled = _pool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return CreateCustomer();
        }

        private CustomerController CreateCustomer()
        {
            Transform parent = customerParent != null ? customerParent : transform;
            CustomerController customer = Instantiate(customerPrefab, entryPoint.position, entryPoint.rotation, parent);
            customer.gameObject.SetActive(false);
            return customer;
        }

        private void Recycle(CustomerController customer)
        {
            if (customer == null)
            {
                _active.Remove(customer);
                return;
            }

            customer.onOrderServed -= HandleOrderServed;
            customer.onExited -= HandleExited;
            _active.Remove(customer);

            // Kapatmak müşterinin OnDisable'ında masayı ve istasyon aboneliğini bırakır.
            customer.gameObject.SetActive(false);
            _pool.Push(customer);
        }

        private void Prewarm()
        {
            for (int i = _pool.Count + _active.Count; i < prewarmCount; i++)
            {
                _pool.Push(CreateCustomer());
            }
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────

        private CustomerSeat PickAvailableSeat()
        {
            _seatBuffer.Clear();

            for (int i = 0; i < seats.Count; i++)
            {
                CustomerSeat seat = seats[i];
                if (seat != null && seat.IsAvailable)
                {
                    _seatBuffer.Add(seat);
                }
            }

            return _seatBuffer.Count == 0 ? null : _seatBuffer[UnityEngine.Random.Range(0, _seatBuffer.Count)];
        }

        private float NextInterval()
        {
            return UnityEngine.Random.Range(minSpawnInterval, Mathf.Max(minSpawnInterval, maxSpawnInterval));
        }

        private void ResolveSeats()
        {
            seats.RemoveAll(seat => seat == null);
            if (seats.Count > 0)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            seats.AddRange(FindObjectsByType<CustomerSeat>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID));
#else
            seats.AddRange(FindObjectsOfType<CustomerSeat>(true));
#endif

            if (seats.Count == 0)
            {
                Debug.LogWarning("[CustomerSpawner] Sahnede CustomerSeat yok; müşteri doğmayacak.", this);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null)
                {
                    _active[i].onOrderServed -= HandleOrderServed;
                    _active[i].onExited -= HandleExited;
                }
            }
        }

        private void OnValidate()
        {
            if (maxSpawnInterval < minSpawnInterval)
            {
                maxSpawnInterval = minSpawnInterval;
            }
        }
    }
}
