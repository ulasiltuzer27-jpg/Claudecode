using System;
using IdleRestaurant.Audio.Haptics;
using IdleRestaurant.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Ses efektleri ve titreşim için tek giriş noktası.
    ///
    /// ── Ses havuzu ──────────────────────────────────────────────────────────
    /// Başlangıçta <see cref="voiceCount"/> kadar AudioSource kurulur ve
    /// sırayla kullanılır. Tek bir AudioSource + PlayOneShot, çakışan seslerde
    /// perdeyi tek tek değiştiremezdi; her çalış için yeni nesne de
    /// oluşturulmuyor (mobilde çöp toplayıcı takılması). Havuz doluysa en
    /// eski ses kesilir.
    ///
    /// ── Titreşim ────────────────────────────────────────────────────────────
    /// Platform ayrıntısı <see cref="IHapticService"/>'te. Aynı karede gelen
    /// "tıklama (hafif)" + "seviye atladı (orta)" gibi çiftlerde yalnızca
    /// güçlü olan hissedilir: <see cref="minHapticInterval"/> içinde gelen
    /// titreşim ancak öncekinden güçlüyse çalar.
    ///
    /// ── Ayarlar ─────────────────────────────────────────────────────────────
    /// Ses seviyesi, sessiz ve titreşim tercihi <see cref="ISaveable"/> ile
    /// kayda girer. Kayıt yoksa (veya v3 öncesi kayıtsa) Inspector'daki
    /// varsayılanlar kullanılır.
    ///
    /// Sahne ömrü GameManager ile aynı: DontDestroyOnLoad yok, singleton
    /// yalnızca her yerden erişim için.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour, ISaveable, ISfxPlayer
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sesler")]
        [Tooltip("Create → Idle Restaurant → Audio → Sound Library. Boşsa ses çalmaz, titreşim yine çalışır.")]
        [SerializeField] private SoundLibrary soundLibrary = null;

        [Tooltip("Aynı anda çalabilecek en fazla efekt. Dolunca en eski ses kesilir.")]
        [SerializeField, Range(1, 32)] private int voiceCount = 8;

        [Tooltip("İsteğe bağlı: efektlerin gideceği AudioMixer grubu.")]
        [SerializeField] private AudioMixerGroup outputGroup = null;

        [Header("Varsayılan ayarlar (kayıt yokken)")]
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
        [SerializeField] private bool defaultMuted = false;
        [SerializeField] private bool defaultHapticsEnabled = true;

        [Header("Titreşim")]
        [Tooltip("İki titreşim arasındaki en kısa süre (sn). Bu süre içinde yalnızca daha güçlü bir titreşim geçer.")]
        [SerializeField, Min(0f)] private float minHapticInterval = 0.08f;

        [Tooltip("Editörde (cihaz yokken) titreşim isteklerini Console'a yazar.")]
        [SerializeField] private bool logHapticsInEditor = false;

        private AudioSource[] _voices;
        private int _nextVoice;
        private float[] _lastPlayTimes;
        private IHapticService _haptics;
        private float _lastHapticTime = float.NegativeInfinity;
        private HapticType _lastHapticType;
        private float _volume;
        private bool _muted;
        private bool _hapticsEnabled;
        private bool _isDuplicate;

        /// <summary>Ses seviyesi, sessiz veya titreşim tercihi değiştiğinde tetiklenir (ayar ekranı ve kayıt için).</summary>
        public event Action onSettingsChanged;

        /// <summary>0-1 arası efekt ses seviyesi.</summary>
        public float Volume => _volume;
        public bool IsMuted => _muted;
        public bool HapticsEnabled => _hapticsEnabled;

        /// <summary>Cihaz titreşim verebiliyor mu. Ayar ekranı titreşim seçeneğini buna göre gizleyebilir.</summary>
        public bool HapticsSupported => _haptics != null && _haptics.IsSupported;

        public SoundLibrary Library => soundLibrary;

        // ── Unity yaşam döngüsü ────────────────────────────────────────────────

        /// <summary>Domain reload kapalıyken önceki oturumun örneği Instance'ta kalmasın.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[AudioManager] Sahnede ikinci bir AudioManager var ('{name}'); bu kopya kaldırılıyor.", this);
                _isDuplicate = true;
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            ApplySettings(defaultVolume, defaultMuted, defaultHapticsEnabled);
            CreateVoices();

            _lastPlayTimes = new float[Enum.GetValues(typeof(SfxType)).Length];
            for (int i = 0; i < _lastPlayTimes.Length; i++)
            {
                _lastPlayTimes[i] = float.NegativeInfinity;
            }

            _haptics = HapticServiceFactory.Create(logHapticsInEditor);
        }

        private void OnDestroy()
        {
            if (_isDuplicate)
            {
                return;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            IDisposable disposable = _haptics as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            _haptics = null;
        }

        private void CreateVoices()
        {
            _voices = new AudioSource[voiceCount];

            for (int i = 0; i < _voices.Length; i++)
            {
                GameObject voiceObject = new GameObject($"SFX Voice {i}");
                voiceObject.transform.SetParent(transform, false);

                AudioSource voice = voiceObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                voice.outputAudioMixerGroup = outputGroup;
                _voices[i] = voice;
            }
        }

        // ── Çalma ──────────────────────────────────────────────────────────────

        /// <summary>Efekti ve kütüphanede tanımlı titreşimini çalar.</summary>
        public void Play(SfxType type)
        {
            Play(type, true);
        }

        /// <summary>
        /// Efekti çalar; <paramref name="withHaptic"/> true ise tanımlı
        /// titreşimi de tetikler. Titreşim sesin tekrar aralığından
        /// bağımsızdır: ses aynı karede zaten çaldıysa bile titreşim gelebilir.
        /// </summary>
        public void Play(SfxType type, bool withHaptic)
        {
            if (_isDuplicate || _voices == null)
            {
                return;
            }

            SoundLibrary.SoundEntry entry = soundLibrary != null ? soundLibrary.GetEntry(type) : null;

            if (withHaptic)
            {
                PlayHaptic(entry != null ? entry.haptic : SoundLibrary.GetDefaultHaptic(type));
            }

            if (entry == null || _muted || _volume <= 0f)
            {
                return;
            }

            int index = (int)type;
            if (index < 0 || index >= _lastPlayTimes.Length)
            {
                return;
            }

            // unscaled: duraklatma menüsünde (timeScale 0) de buton sesi çalmalı.
            float now = Time.unscaledTime;
            if (now - _lastPlayTimes[index] < entry.minInterval)
            {
                return;
            }

            AudioClip clip = entry.PickClip();
            if (clip == null)
            {
                return;
            }

            _lastPlayTimes[index] = now;

            AudioSource voice = NextVoice();
            voice.clip = clip;
            voice.volume = entry.volume * _volume;
            voice.pitch = 1f + UnityEngine.Random.Range(-entry.pitchVariance, entry.pitchVariance);
            voice.Play();
        }

        /// <summary>Sessiz bir havuz sesi arar; hepsi çalıyorsa sıradaki (en eski) sesi keser.</summary>
        private AudioSource NextVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int candidate = (_nextVoice + i) % _voices.Length;
                if (!_voices[candidate].isPlaying)
                {
                    _nextVoice = (candidate + 1) % _voices.Length;
                    return _voices[candidate];
                }
            }

            AudioSource oldest = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return oldest;
        }

        /// <summary>
        /// Titreşim tetikler. Kapalıysa, cihaz desteklemiyorsa veya kısa süre
        /// önce aynı ya da daha güçlü bir titreşim çaldıysa hiçbir şey yapmaz.
        /// </summary>
        public void PlayHaptic(HapticType type)
        {
            if (type == HapticType.None || !_hapticsEnabled || _haptics == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastHapticTime < minHapticInterval && type <= _lastHapticType)
            {
                return;
            }

            _lastHapticTime = now;
            _lastHapticType = type;
            _haptics.Play(type);
        }

        /// <summary>Çalan tüm efektleri keser.</summary>
        public void StopAll()
        {
            if (_voices == null)
            {
                return;
            }

            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i] != null && _voices[i].isPlaying)
                {
                    _voices[i].Stop();
                }
            }
        }

        // ── Ayarlar ────────────────────────────────────────────────────────────

        public void SetVolume(float volume)
        {
            float clamped = float.IsNaN(volume) ? defaultVolume : Mathf.Clamp01(volume);
            if (Mathf.Approximately(clamped, _volume))
            {
                return;
            }

            _volume = clamped;
            onSettingsChanged?.Invoke();
        }

        public void SetMuted(bool muted)
        {
            if (muted == _muted)
            {
                return;
            }

            _muted = muted;
            if (muted)
            {
                StopAll();
            }

            onSettingsChanged?.Invoke();
        }

        public void ToggleMute()
        {
            SetMuted(!_muted);
        }

        public void SetHapticsEnabled(bool enabledHaptics)
        {
            if (enabledHaptics == _hapticsEnabled)
            {
                return;
            }

            _hapticsEnabled = enabledHaptics;
            onSettingsChanged?.Invoke();
        }

        private void ApplySettings(float volume, bool muted, bool hapticsEnabled)
        {
            _volume = float.IsNaN(volume) ? 1f : Mathf.Clamp01(volume);
            _muted = muted;
            _hapticsEnabled = hapticsEnabled;
        }

        // ── Kayıt (ISaveable) ──────────────────────────────────────────────────

        public void CaptureState(SaveData data)
        {
            data.audio = new AudioSaveData
            {
                hasSettings = true,
                volume = _volume,
                muted = _muted,
                hapticsEnabled = _hapticsEnabled
            };
        }

        public void RestoreState(SaveData data)
        {
            AudioSaveData state = data != null ? data.audio : null;

            if (state != null && state.hasSettings)
            {
                ApplySettings(state.volume, state.muted, state.hapticsEnabled);
            }
            else
            {
                ApplySettings(defaultVolume, defaultMuted, defaultHapticsEnabled);
            }

            if (_muted)
            {
                StopAll();
            }

            onSettingsChanged?.Invoke();
        }
    }
}
