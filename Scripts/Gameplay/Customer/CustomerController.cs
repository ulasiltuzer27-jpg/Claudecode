using System;
using UnityEngine;

namespace IdleRestaurant.Gameplay.Customers
{
    public enum CustomerState
    {
        /// <summary>Havuzda, ziyaret yok.</summary>
        Inactive,

        /// <summary>Kapıdan masaya yürüyor.</summary>
        WalkingToSeat,

        /// <summary>Masada, siparişin hazırlanmasını bekliyor.</summary>
        WaitingForOrder,

        /// <summary>Siparişi geldi, masada yiyor; bitince parayı bırakıp kalkar.</summary>
        Eating,

        /// <summary>Çıkışa yürüyor (ödeyip veya ödemeden).</summary>
        Leaving
    }

    /// <summary>Spawner'ın her ziyarete verdiği ayarlar.</summary>
    [Serializable]
    public struct CustomerVisitSettings
    {
        /// <summary>Masaya oturduktan sonra siparişin en erken hazır olacağı süre (sn).</summary>
        public float minWaitSeconds;

        /// <summary>Sipariş için en uzun bekleme (sn); dolarsa ödemeden çıkar. 0 = sınırsız.</summary>
        public float patienceSeconds;

        /// <summary>Sipariş geldikten sonra masada yeme süresi (sn). 0 = servis edilince hemen öder ve kalkar.</summary>
        public float eatSeconds;

        public CustomerVisitSettings(float minWaitSeconds, float patienceSeconds, float eatSeconds = 0f)
        {
            this.minWaitSeconds = minWaitSeconds;
            this.patienceSeconds = patienceSeconds;
            this.eatSeconds = eatSeconds;
        }
    }

    /// <summary>
    /// Tek bir müşterinin ziyareti: kapıdan masaya yürü → siparişi bekle →
    /// ye → parayı bırak → çık.
    ///
    /// ── Sipariş ne zaman hazır ──────────────────────────────────────────────
    /// Masanın istasyonu bir üretim döngüsünü tamamladığında. Böylece müşteri
    /// istasyonun ilerleme çubuğu dolduğu anda kalkar; hızlandırıcı ve hız
    /// yükseltmeleri müşteri akışını da görünür biçimde hızlandırır.
    ///
    /// ── Hareket ─────────────────────────────────────────────────────────────
    /// Vector3.MoveTowards ile düz çizgide; 2D ve 3D sahnede aynı çalışır
    /// (2D'de kapı ve masaları aynı z'de tutun). Görünüm (zıplama, eğilme,
    /// Animator) <see cref="CustomerVisual"/>'dadır; <see cref="State"/>,
    /// <see cref="MoveDirection"/> ve olaylardan beslenir, kökün konumuna dokunmaz.
    ///
    /// ── Sorumluluk sınırı ───────────────────────────────────────────────────
    /// Müşteri parayı kendisi eklemez: yemeğini bitirince
    /// <see cref="onCheckout"/> ile duyurur, bıraktığı parayı ekonomi
    /// ayarlarını tutan <see cref="CustomerSpawner"/> hesaplar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CustomerController : MonoBehaviour
    {
        [Tooltip("Yürüme hızı (birim/sn).")]
        [SerializeField, Min(0.01f)] private float moveSpeed = 2f;

        [Tooltip("Hedefe bu mesafeden yakınsa varılmış sayılır.")]
        [SerializeField, Min(0.001f)] private float arrivalDistance = 0.05f;

        private CustomerSeat _seat;
        private Station _station;
        private Transform _exitPoint;
        private CustomerVisitSettings _settings;
        private float _waitTimer;
        private float _eatTimer;
        private bool _orderReady;
        private bool _listeningToStation;
        private bool _served;
        private Vector3 _moveDirection;

        /// <summary>Sipariş masaya geldiğinde (müşteri, siparişi hazırlayan istasyon) ile tetiklenir; müşteri yemeye başlar.</summary>
        public event Action<CustomerController, Station> onOrderServed;

        /// <summary>
        /// Müşteri yemeğini bitirip parayı bıraktığında, kalkmadan hemen önce
        /// tetiklenir. Spawner ödemeyi burada ekler. Ödemeden ayrılan
        /// (sabrı biten, masası kapanan) müşteride tetiklenmez.
        /// </summary>
        public event Action<CustomerController, Station> onCheckout;

        /// <summary>Müşteri çıkışa vardığında tetiklenir; spawner onu havuza döndürür.</summary>
        public event Action<CustomerController> onExited;

        public CustomerState State { get; private set; }
        public CustomerSeat Seat => _seat;
        public bool WasServed => _served;
        public bool IsMoving => State == CustomerState.WalkingToSeat || State == CustomerState.Leaving;

        /// <summary>Masada: siparişi bekliyor veya yiyor.</summary>
        public bool IsAtSeat => State == CustomerState.WaitingForOrder || State == CustomerState.Eating;

        /// <summary>Son karedeki hareket yönü (birim vektör); dururken sıfır.</summary>
        public Vector3 MoveDirection => _moveDirection;

        /// <summary>Beklemede geçen süre (sn).</summary>
        public float WaitTime => _waitTimer;

        /// <summary>Yemekte geçen süre (sn).</summary>
        public float EatTime => _eatTimer;

        /// <summary>
        /// Yeni bir ziyaret başlatır. Masa çağırandan önce
        /// <see cref="CustomerSeat.TryReserve"/> ile bu müşteriye ayrılmış olmalı.
        /// </summary>
        public void Begin(CustomerSeat seat, Transform exitPoint, CustomerVisitSettings settings)
        {
            if (seat == null || exitPoint == null)
            {
                Debug.LogError($"[CustomerController] '{name}': masa veya çıkış noktası verilmedi.", this);
                return;
            }

            ResetVisit();
            _seat = seat;
            _station = seat.Station;
            _exitPoint = exitPoint;
            _settings = settings;
            State = CustomerState.WalkingToSeat;
        }

        /// <summary>Müşteriyi ödemeden çıkışa yollar (masa kapandı, spawner durdu...).</summary>
        public void Leave()
        {
            if (State == CustomerState.Inactive || State == CustomerState.Leaving)
            {
                return;
            }

            StopListeningToStation();
            ReleaseSeat();
            State = CustomerState.Leaving;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            switch (State)
            {
                case CustomerState.WalkingToSeat:
                    UpdateWalkingToSeat(deltaTime);
                    break;
                case CustomerState.WaitingForOrder:
                    UpdateWaiting(deltaTime);
                    break;
                case CustomerState.Eating:
                    UpdateEating(deltaTime);
                    break;
                case CustomerState.Leaving:
                    UpdateLeaving(deltaTime);
                    break;
            }
        }

        private void UpdateWalkingToSeat(float deltaTime)
        {
            if (!_seat.IsServiceAvailable)
            {
                Leave();
                return;
            }

            if (MoveTowards(_seat.StandPoint.position, deltaTime))
            {
                StartWaiting();
            }
        }

        private void StartWaiting()
        {
            State = CustomerState.WaitingForOrder;
            _moveDirection = Vector3.zero;
            _waitTimer = 0f;
            _orderReady = false;

            _station.onProductionCompleted += HandleProductionCompleted;
            _listeningToStation = true;
        }

        private void UpdateWaiting(float deltaTime)
        {
            if (!_seat.IsServiceAvailable)
            {
                Leave();
                return;
            }

            _waitTimer += deltaTime;

            if (_orderReady && _waitTimer >= _settings.minWaitSeconds)
            {
                Serve();
                return;
            }

            if (_settings.patienceSeconds > 0f && _waitTimer >= _settings.patienceSeconds)
            {
                Leave();
            }
        }

        /// <summary>
        /// Yalnızca işaretler; servis bir sonraki Update'te yapılır. İstasyonun
        /// olayının içinden para eklemek ve masayı bırakmak, çağrı yığınında
        /// iç içe durum değişikliği demekti.
        /// </summary>
        private void HandleProductionCompleted(Station station, double amount)
        {
            _orderReady = true;
        }

        private void Serve()
        {
            _served = true;
            StopListeningToStation();
            State = CustomerState.Eating;
            _eatTimer = 0f;
            onOrderServed?.Invoke(this, _station);

            // Dinleyici müşteriyi göndermiş veya kapatmış olabilir.
            if (State == CustomerState.Eating && _settings.eatSeconds <= 0f)
            {
                Checkout();
            }
        }

        private void UpdateEating(float deltaTime)
        {
            // Yemek sırasında istasyon kilitlenirse (prestij) müşteri ödemeden kalkar:
            // sıfırlanmış oyuna önceki turun parası eklenmesin.
            if (!_seat.IsServiceAvailable)
            {
                Leave();
                return;
            }

            _eatTimer += deltaTime;
            if (_eatTimer >= _settings.eatSeconds)
            {
                Checkout();
            }
        }

        private void Checkout()
        {
            Station station = _station;
            ReleaseSeat();
            State = CustomerState.Leaving;
            onCheckout?.Invoke(this, station);
        }

        private void UpdateLeaving(float deltaTime)
        {
            if (_exitPoint == null || MoveTowards(_exitPoint.position, deltaTime))
            {
                Exit();
            }
        }

        private void Exit()
        {
            State = CustomerState.Inactive;
            _moveDirection = Vector3.zero;
            onExited?.Invoke(this);
        }

        /// <summary>Hedefe doğru bir kare yürür; varıldıysa true.</summary>
        private bool MoveTowards(Vector3 target, float deltaTime)
        {
            Vector3 position = transform.position;
            Vector3 toTarget = target - position;
            float arrivalSqr = arrivalDistance * arrivalDistance;

            if (toTarget.sqrMagnitude <= arrivalSqr)
            {
                _moveDirection = Vector3.zero;
                return true;
            }

            _moveDirection = toTarget.normalized;
            Vector3 next = Vector3.MoveTowards(position, target, moveSpeed * deltaTime);
            transform.position = next;
            return (target - next).sqrMagnitude <= arrivalSqr;
        }

        private void StopListeningToStation()
        {
            if (_listeningToStation && _station != null)
            {
                _station.onProductionCompleted -= HandleProductionCompleted;
            }

            _listeningToStation = false;
        }

        private void ReleaseSeat()
        {
            if (_seat != null)
            {
                _seat.Release(this);
            }

            _seat = null;
        }

        private void ResetVisit()
        {
            StopListeningToStation();
            ReleaseSeat();
            _station = null;
            _exitPoint = null;
            _waitTimer = 0f;
            _eatTimer = 0f;
            _orderReady = false;
            _served = false;
            _moveDirection = Vector3.zero;
            State = CustomerState.Inactive;
        }

        private void OnDisable()
        {
            // Havuza dönerken veya ziyaret ortasında kapatılırsa: masayı ve
            // istasyon aboneliğini bırak, yarım ziyaret sonraki Begin'e sızmasın.
            ResetVisit();
        }
    }
}
