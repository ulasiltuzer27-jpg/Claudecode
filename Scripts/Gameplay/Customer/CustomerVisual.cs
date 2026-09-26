using UnityEngine;

namespace IdleRestaurant.Gameplay.Customers
{
    /// <summary>
    /// Müşterinin görünümü: yürürken "Wobble &amp; Hop" (küçük sıçramalar,
    /// sağa-sola yalpalama, esneyip basılma), masada bekleme ve yeme
    /// pozları, parayı bırakınca sevinç zıplaması. İsteğe bağlı bir
    /// Animator'a da aynı durumları bool parametre olarak iletir. Havuzdan
    /// her çıkışta <see cref="appearance"/> yeni bir kıyafet rengi, şapka
    /// ve boy seçer; kapıdan giren müşteriler birbirinden farklı görünür.
    ///
    /// ── Neyi hareket ettirir ────────────────────────────────────────────────
    /// Yalnızca <see cref="model"/>'i (gövde, şapka, göz... hepsinin ebeveyni).
    /// Kökü <see cref="CustomerController"/> yürütür; görsel köke dokunsaydı
    /// iki bileşen aynı konum için yarışırdı. Model'in pivotu ayak hizasında
    /// (y = 0) olmalı: eğilme ve basılma ayakların etrafında olur. Model
    /// dinlenme pozunda kökün ileri yönüne (+Z) bakmalı.
    ///
    /// ── Animator (isteğe bağlı) ─────────────────────────────────────────────
    /// Bağlıysa <see cref="IsWalkingParameter"/>, <see cref="IsEatingParameter"/>,
    /// <see cref="IsWaitingParameter"/> bool'ları ve varsa <see cref="CheerParameter"/>
    /// tetikleyicisi sürülür. Controller'da olmayan parametre atlanır (konsolu
    /// uyarıyla doldurmaz). Rigli yürüyüş zaten zıplama içeriyorsa
    /// <see cref="proceduralMotion"/>'ı kapatın; Animator'ın Apply Root Motion'ı
    /// kapalı olmalı.
    ///
    /// Durumlar controller'dan her karede okunur, sevinç ise
    /// <see cref="CustomerController.onCheckout"/> olayıyla tetiklenir. Havuza
    /// dönüşte (OnDisable) model dinlenme pozuna geri konur.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CustomerController))]
    public sealed class CustomerVisual : MonoBehaviour
    {
        public const string IsWalkingParameter = "IsWalking";
        public const string IsEatingParameter = "IsEating";
        public const string IsWaitingParameter = "IsWaiting";
        public const string CheerParameter = "Cheer";

        private static readonly int IsWalkingHash = Animator.StringToHash(IsWalkingParameter);
        private static readonly int IsEatingHash = Animator.StringToHash(IsEatingParameter);
        private static readonly int IsWaitingHash = Animator.StringToHash(IsWaitingParameter);
        private static readonly int CheerHash = Animator.StringToHash(CheerParameter);

        private const float TwoPi = Mathf.PI * 2f;

        [Header("Referanslar")]
        [Tooltip("Sallanan görsel kök (gövde, şapka...). CustomerController'ın olduğu kök nesne OLMAMALI. Pivot ayak hizasında.")]
        [SerializeField] private Transform model = null;

        [Tooltip("İsteğe bağlı. Boşsa bu nesnenin altında aranır. IsWalking / IsEating / IsWaiting bool'ları ve varsa Cheer tetikleyicisi sürülür.")]
        [SerializeField] private Animator animator = null;

        [Tooltip("Prosedürel hareket (zıplama, eğilme, basılma). Rigli animasyon bunları zaten yapıyorsa kapatın.")]
        [SerializeField] private bool proceduralMotion = true;

        [Header("Yürüme: Wobble & Hop")]
        [Tooltip("Saniyedeki sıçrama sayısı; her sıçramada gövde bir yana yatar.")]
        [SerializeField, Min(0f)] private float hopsPerSecond = 4f;

        [Tooltip("Sıçrama yüksekliği (birim).")]
        [SerializeField, Min(0f)] private float hopHeight = 0.08f;

        [Tooltip("Sağa-sola yalpalama açısı (derece).")]
        [SerializeField, Range(0f, 45f)] private float wobbleAngle = 8f;

        [Tooltip("Yürürken öne eğilme (derece).")]
        [SerializeField, Range(0f, 30f)] private float forwardLean = 5f;

        [Tooltip("Esneme/basılma miktarı: tepede uzar, yere değerken basılır. Hacim korunur.")]
        [SerializeField, Range(0f, 0.5f)] private float squashAndStretch = 0.08f;

        [Tooltip("Dönme hızı (derece/sn).")]
        [SerializeField, Min(0f)] private float turnSpeed = 540f;

        [Tooltip("Pozlar arası geçiş hızı (1/sn). Büyük değer = daha sert geçiş.")]
        [SerializeField, Min(0.01f)] private float blendSpeed = 6f;

        [Header("Masada")]
        [Tooltip("Masaya geçince gövdenin basılıp 'oturması'.")]
        [SerializeField, Range(0f, 0.3f)] private float seatedSquash = 0.06f;

        [Tooltip("Beklerken nefes alma sıklığı (1/sn).")]
        [SerializeField, Min(0f)] private float breathsPerSecond = 0.8f;

        [Tooltip("Nefes alırken gövdenin esnemesi.")]
        [SerializeField, Range(0f, 0.2f)] private float breathAmount = 0.03f;

        [Tooltip("Siparişi beklerken yavaş sağa-sola sallanma (derece).")]
        [SerializeField, Range(0f, 20f)] private float waitingSwayAngle = 4f;

        [Tooltip("Yerken saniyedeki 'lokma' baş sallama sayısı.")]
        [SerializeField, Min(0f)] private float eatNodsPerSecond = 2.5f;

        [Tooltip("Lokma alırken öne eğilme (derece).")]
        [SerializeField, Range(0f, 45f)] private float eatNodAngle = 12f;

        [Header("Görünüm çeşitliliği")]
        [Tooltip("Her gelişte rastgele kıyafet rengi, şapka (veya şapkasız) ve boy.")]
        [SerializeField] private CustomerAppearance appearance = new CustomerAppearance();

        [Header("Ödeme tepkisi")]
        [Tooltip("Sevinç zıplamasının süresi (sn).")]
        [SerializeField, Min(0.05f)] private float cheerDuration = 0.5f;

        [Tooltip("Sevinç zıplamasının yüksekliği (birim).")]
        [SerializeField, Min(0f)] private float cheerHeight = 0.3f;

        [Tooltip("Zıplarken sağa-sola kıpırdanma (derece).")]
        [SerializeField, Range(0f, 45f)] private float cheerWiggleAngle = 12f;

        private CustomerController _controller;
        private bool _motionEnabled;
        private bool _hasRestPose;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _restScale;
        private float _sizeScale = 1f;

        private float _walkWeight;
        private float _seatWeight;
        private float _eatWeight;
        private float _walkPhase;
        private float _idlePhase;
        private float _eatPhase;
        private float _yaw;
        private bool _snapYaw;
        private float _cheerTime = -1f;

        private RuntimeAnimatorController _boundController;
        private bool _hasWalkingParameter;
        private bool _hasEatingParameter;
        private bool _hasWaitingParameter;
        private bool _hasCheerParameter;
        private bool _animatorSynced;
        private bool _sentWalking;
        private bool _sentEating;
        private bool _sentWaiting;

        public Transform Model => model;
        public Animator Animator => animator;
        public bool ProceduralMotionActive => _motionEnabled;
        public CustomerAppearance Appearance => appearance;

        /// <summary>Sevinç zıplaması sürüyor mu.</summary>
        public bool IsCheering => _cheerTime >= 0f;

        private void Awake()
        {
            _controller = GetComponent<CustomerController>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (model == null)
            {
                Debug.LogWarning($"[CustomerVisual] '{name}': model atanmadı; prosedürel hareket kapalı.", this);
            }
            else if (model == transform)
            {
                // Kökü CustomerController yürütüyor; burada oynamak konumu bozar.
                Debug.LogWarning($"[CustomerVisual] '{name}': model kök nesnenin kendisi olamaz, bir alt nesne atayın; prosedürel hareket kapalı.", this);
                model = null;
            }

            if (model != null)
            {
                _restPosition = model.localPosition;
                _restRotation = model.localRotation;
                _restScale = model.localScale;
                _hasRestPose = true;
            }

            _motionEnabled = proceduralMotion && _hasRestPose;
            appearance.Initialize();
        }

        private void OnEnable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.onCheckout += HandleCheckout;

            _walkWeight = 0f;
            _seatWeight = 0f;
            _eatWeight = 0f;
            _cheerTime = -1f;
            _yaw = 0f;
            _snapYaw = true;

            // Aynı anda yürüyen müşteriler aynı ritimde zıplamasın.
            _walkPhase = Random.Range(0f, TwoPi);
            _idlePhase = Random.Range(0f, TwoPi);
            _eatPhase = Random.Range(0f, TwoPi);

            // Animator etkinleşince parametreleri varsayılana döner; yeniden gönder.
            _animatorSynced = false;

            _sizeScale = appearance.Randomize();
            if (_hasRestPose)
            {
                SetLocalPose(_restPosition, _restRotation, _restScale * _sizeScale);
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.onCheckout -= HandleCheckout;
            }

            ApplyRestPose();
        }

        private void HandleCheckout(CustomerController customer, Station station)
        {
            _cheerTime = 0f;

            if (EnsureAnimatorBound() && _hasCheerParameter)
            {
                animator.SetTrigger(CheerHash);
            }
        }

        private void LateUpdate()
        {
            if (_controller == null)
            {
                return;
            }

            CustomerState state = _controller.State;
            UpdateAnimator(state);

            if (!_motionEnabled)
            {
                _cheerTime = -1f;
                return;
            }

            float deltaTime = Time.deltaTime;
            bool walking = _controller.IsMoving;
            bool eating = state == CustomerState.Eating;
            bool cheering = IsCheering;

            // Sevinç sırasında yürüyüş sıçraması bekler; iki zıplama üst üste binmesin.
            _walkWeight = Mathf.MoveTowards(_walkWeight, walking && !cheering ? 1f : 0f, blendSpeed * deltaTime);
            _seatWeight = Mathf.MoveTowards(_seatWeight, _controller.IsAtSeat ? 1f : 0f, blendSpeed * deltaTime);
            _eatWeight = Mathf.MoveTowards(_eatWeight, eating ? 1f : 0f, blendSpeed * deltaTime);

            // Her |sin| tümseği bir sıçrama: faz saniyede hopsPerSecond × π ilerler.
            _walkPhase = Mathf.Repeat(_walkPhase + deltaTime * hopsPerSecond * Mathf.PI, TwoPi);
            _idlePhase = Mathf.Repeat(_idlePhase + deltaTime * breathsPerSecond * TwoPi, TwoPi * 2f);
            _eatPhase = Mathf.Repeat(_eatPhase + deltaTime * eatNodsPerSecond * TwoPi, TwoPi);

            UpdateFacing(walking, deltaTime);
            ApplyPose(deltaTime);
        }

        /// <summary>Yürürken gittiği yöne, masadayken istasyona döner; ikisi de yoksa yönünü korur.</summary>
        private void UpdateFacing(bool walking, float deltaTime)
        {
            Vector3 worldDirection = Vector3.zero;
            if (walking)
            {
                worldDirection = _controller.MoveDirection;
            }
            else if (_controller.IsAtSeat && _controller.Seat != null)
            {
                CustomerSeat seat = _controller.Seat;
                worldDirection = seat.transform.position - seat.StandPoint.position;
            }

            Transform parent = model.parent;
            Vector3 direction = parent != null ? parent.InverseTransformDirection(worldDirection) : worldDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _yaw = _snapYaw ? targetYaw : Mathf.MoveTowardsAngle(_yaw, targetYaw, turnSpeed * deltaTime);
            _snapYaw = false;
        }

        private void ApplyPose(float deltaTime)
        {
            float wave = Mathf.Sin(_walkPhase);   // -1 sol ... +1 sağ
            float hop = Mathf.Abs(wave);          // 0 yerde ... 1 tepede
            float breath = Mathf.Sin(_idlePhase);
            float nod = Mathf.Max(0f, Mathf.Sin(_eatPhase));
            float waitWeight = _seatWeight * (1f - _eatWeight);

            float height = hop * hopHeight * _walkWeight;
            float roll = wave * wobbleAngle * _walkWeight
                         + Mathf.Sin(_idlePhase * 0.5f) * waitingSwayAngle * waitWeight;
            float pitch = forwardLean * _walkWeight + nod * eatNodAngle * _eatWeight;

            // Artı = uzama, eksi = basılma.
            float stretch = (hop * 2f - 1f) * squashAndStretch * _walkWeight
                            + breath * breathAmount * (1f - _walkWeight)
                            - seatedSquash * _seatWeight
                            - nod * breathAmount * _eatWeight;

            if (IsCheering)
            {
                _cheerTime += deltaTime;
                float t = Mathf.Clamp01(_cheerTime / cheerDuration);
                float arc = 4f * t * (1f - t);   // 0 → 1 (tepe) → 0

                height += arc * cheerHeight;
                stretch += arc * squashAndStretch * 1.5f;
                roll += Mathf.Sin(t * TwoPi * 2f) * cheerWiggleAngle * (1f - t);

                if (_cheerTime >= cheerDuration)
                {
                    _cheerTime = -1f;
                }
            }

            SetLocalPose(_restPosition + Vector3.up * height,
                Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(pitch, 0f, roll) * _restRotation,
                Vector3.Scale(_restScale * _sizeScale, new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f - stretch * 0.5f)));
        }

        private void ApplyRestPose()
        {
            if (!_hasRestPose || model == null)
            {
                return;
            }

            SetLocalPose(_restPosition, _restRotation, _restScale);
        }

        private void SetLocalPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
#if UNITY_2022_1_OR_NEWER
            // Tek çağrı: dönüşüm değişikliği hiyerarşiye bir kez yayılır.
            model.SetLocalPositionAndRotation(position, rotation);
#else
            model.localPosition = position;
            model.localRotation = rotation;
#endif
            model.localScale = scale;
        }

        // ── Animator ───────────────────────────────────────────────────────────

        private void UpdateAnimator(CustomerState state)
        {
            if (!EnsureAnimatorBound())
            {
                return;
            }

            bool walking = state == CustomerState.WalkingToSeat || state == CustomerState.Leaving;
            bool eating = state == CustomerState.Eating;
            bool waiting = state == CustomerState.WaitingForOrder;

            if (_animatorSynced && walking == _sentWalking && eating == _sentEating && waiting == _sentWaiting)
            {
                return;
            }

            SetBool(_hasWalkingParameter, IsWalkingHash, walking);
            SetBool(_hasEatingParameter, IsEatingHash, eating);
            SetBool(_hasWaitingParameter, IsWaitingHash, waiting);
            _sentWalking = walking;
            _sentEating = eating;
            _sentWaiting = waiting;
            _animatorSynced = true;
        }

        private void SetBool(bool exists, int hash, bool value)
        {
            if (exists)
            {
                animator.SetBool(hash, value);
            }
        }

        /// <summary>
        /// Animator kullanılabilir mi; controller değiştiyse hangi parametrelerin
        /// var olduğunu yeniden okur. Parametre listesi yalnızca controller
        /// değişince okunur (her okuma dizi ayırır).
        /// </summary>
        private bool EnsureAnimatorBound()
        {
            if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized)
            {
                return false;
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                _boundController = null;
                return false;
            }

            if (controller == _boundController)
            {
                return true;
            }

            _boundController = controller;
            _hasWalkingParameter = false;
            _hasEatingParameter = false;
            _hasWaitingParameter = false;
            _hasCheerParameter = false;

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    _hasWalkingParameter |= parameter.nameHash == IsWalkingHash;
                    _hasEatingParameter |= parameter.nameHash == IsEatingHash;
                    _hasWaitingParameter |= parameter.nameHash == IsWaitingHash;
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    _hasCheerParameter |= parameter.nameHash == CheerHash;
                }
            }

            _animatorSynced = false;
            return true;
        }

        private void OnValidate()
        {
            if (model == transform)
            {
                Debug.LogWarning($"[CustomerVisual] '{name}': model kök nesnenin kendisi olamaz; gövdeyi taşıyan bir alt nesne atayın.", this);
            }
        }
    }
}
