using System;
using IdleRestaurant.Audio.Haptics;
using IdleRestaurant.Data;
using UnityEngine;
using UnityEngine.Audio;

namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Ses efektleri, arka plan müziği ve titreşim için tek giriş noktası.
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
    /// ── Müzik (BGM) ─────────────────────────────────────────────────────────
    /// İki AudioSource arasında çapraz geçiş: parça değişirken eskisi söner,
    /// yenisi yükselir. Sessize alınınca müzik söner ve DURAKLATILIR; açılınca
    /// kaldığı yerden yükselerek devam eder. Efekt sesi ve müzik ayrı ayarlanır
    /// (ayrı seviye, ayrı sessiz). Parçalar, fade süreleri ve döngü
    /// <see cref="SoundLibrary.Music"/>'te. <see cref="playMusicOnStart"/>
    /// açıksa müzik ilk karede başlar: o ana kadar kayıtlı ayarlar yüklenmiş
    /// olur, sessize alınmış müzik bir an bile duyulmaz.
    ///
    /// ── Prosedürel sesler ───────────────────────────────────────────────────
    /// Kütüphanede klibi olmayan efekt veya müzik için <see cref="ToneGenerator"/>
    /// ile basit sentez sesi üretilir (ilk kullanımda, bir kez). Gerçek klip
    /// atanınca o kullanılır. <see cref="proceduralFallback"/> kapatılırsa klipsiz
    /// türler sessiz kalır.
    ///
    /// ── Ayarlar ─────────────────────────────────────────────────────────────
    /// Efekt seviyesi, efekt sessiz, müzik seviyesi, müzik sessiz ve titreşim
    /// tercihi <see cref="ISaveable"/> ile kayda girer. Kayıt yoksa (veya ilgili
    /// bölüm eski kayıtta yoksa) Inspector'daki varsayılanlar kullanılır.
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

        [Header("Müzik (BGM)")]
        [Tooltip("Oyun başlayınca kütüphanedeki müziği çalar (ilk karede, kayıtlı ayarlar yüklendikten sonra).")]
        [SerializeField] private bool playMusicOnStart = true;

        [Tooltip("İsteğe bağlı: müziğin gideceği AudioMixer grubu.")]
        [SerializeField] private AudioMixerGroup musicOutputGroup = null;

        [Header("Prosedürel sesler")]
        [Tooltip("Kütüphanede klibi olmayan efekt ve müzik için basit sentez sesleri üretir. Gerçek klip atanınca o kullanılır.")]
        [SerializeField] private bool proceduralFallback = true;

        [Header("Varsayılan ayarlar (kayıt yokken)")]
        [SerializeField, Range(0f, 1f)] private float defaultVolume = 1f;
        [SerializeField] private bool defaultMuted = false;
        [SerializeField] private bool defaultHapticsEnabled = true;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.8f;
        [SerializeField] private bool defaultMusicMuted = false;

        [Header("Titreşim")]
        [Tooltip("İki titreşim arasındaki en kısa süre (sn). Bu süre içinde yalnızca daha güçlü bir titreşim geçer.")]
        [SerializeField, Min(0f)] private float minHapticInterval = 0.08f;

        [Tooltip("Editörde (cihaz yokken) titreşim isteklerini Console'a yazar.")]
        [SerializeField] private bool logHapticsInEditor = false;

        /// <summary>Kütüphane kaldırılırsa fade süreleri için kullanılan ayarlar.</summary>
        private static readonly SoundLibrary.MusicSettings FallbackMusicSettings = new SoundLibrary.MusicSettings();

        /// <summary>Bir müzik kaynağı ve onun fade durumu.</summary>
        private sealed class MusicChannel
        {
            public readonly AudioSource Source;
            public float Fade;
            public float Target;
            public float Rate;

            /// <summary>Sessize alındığı için kaldığı yerde duraklatıldı.</summary>
            public bool Paused;

            /// <summary>Parça atandı ama müzik sessizken istendiği için hiç başlamadı.</summary>
            public bool Pending;

            public MusicChannel(AudioSource source)
            {
                Source = source;
            }
        }

        private readonly MusicChannel[] _music = new MusicChannel[2];
        private int _activeMusic;
        private int _trackIndex;
        private bool _musicRequested;
        private bool _musicAutoStartPending;
        private float _musicPlayTime;
        private float _musicVolume;
        private bool _musicMuted;
        private AudioClip[] _proceduralSfx;
        private AudioClip _proceduralMusic;

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

        /// <summary>Efekt/müzik seviyesi, sessiz veya titreşim tercihi değiştiğinde tetiklenir (ayar ekranı ve kayıt için).</summary>
        public event Action onSettingsChanged;

        /// <summary>0-1 arası efekt ses seviyesi.</summary>
        public float Volume => _volume;
        public bool IsMuted => _muted;
        public bool HapticsEnabled => _hapticsEnabled;

        /// <summary>Cihaz titreşim verebiliyor mu. Ayar ekranı titreşim seçeneğini buna göre gizleyebilir.</summary>
        public bool HapticsSupported => _haptics != null && _haptics.IsSupported;

        public SoundLibrary Library => soundLibrary;

        /// <summary>0-1 arası oyuncu müzik seviyesi (kütüphanedeki karıştırma seviyesiyle çarpılır).</summary>
        public float MusicVolume => _musicVolume;
        public bool IsMusicMuted => _musicMuted;

        /// <summary>Müzik istendi mi (sessize alınmış olsa bile). <see cref="StopMusic"/> ile false olur.</summary>
        public bool IsMusicRequested => _musicRequested;

        /// <summary>Çalan (veya sessizde bekleyen) parça; müzik istenmediyse null.</summary>
        public AudioClip CurrentMusic => _musicRequested && _music[_activeMusic] != null ? _music[_activeMusic].Source.clip : null;

        /// <summary>Çalan parça gerçek klip değil, <see cref="ToneGenerator"/> döngüsü mü.</summary>
        public bool IsPlayingProceduralMusic => _proceduralMusic != null && CurrentMusic == _proceduralMusic;

        private SoundLibrary.MusicSettings MusicConfig => soundLibrary != null ? soundLibrary.Music : FallbackMusicSettings;

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
            ApplyMusicSettings(defaultMusicVolume, defaultMusicMuted);
            CreateVoices();
            CreateMusicChannels();
            _proceduralSfx = new AudioClip[Enum.GetValues(typeof(SfxType)).Length];
            _musicAutoStartPending = playMusicOnStart;

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
            DestroyProceduralClips();
        }

        private void Update()
        {
            if (_musicAutoStartPending)
            {
                _musicAutoStartPending = false;
                PlayMusic();
            }

            // unscaled: duraklatma menüsünde (timeScale 0) de müzik sönüp yükselebilmeli.
            UpdateMusic(Time.unscaledDeltaTime);
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

        private void CreateMusicChannels()
        {
            for (int i = 0; i < _music.Length; i++)
            {
                GameObject musicObject = new GameObject($"Music {i}");
                musicObject.transform.SetParent(transform, false);

                AudioSource source = musicObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;

                // En yüksek öncelik: çok ses çaldığında Unity müziği kesmesin.
                source.priority = 0;
                source.outputAudioMixerGroup = musicOutputGroup;
                _music[i] = new MusicChannel(source);
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
                clip = GetProceduralSfx(type);
                if (clip == null)
                {
                    return;
                }
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

        // ── Müzik ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Kütüphanedeki müziği başlatır (ilk dolu parçadan; parça yoksa
        /// prosedürel döngü). Müzik sessizse parça hazırlanır, açılınca başlar.
        /// </summary>
        /// <returns>Çalınacak bir parça bulunduysa true.</returns>
        public bool PlayMusic()
        {
            AudioClip track = ResolveTrack(_trackIndex, out int index);
            if (track == null)
            {
                return false;
            }

            _trackIndex = index;
            return PlayMusic(track);
        }

        /// <summary>
        /// Verilen parçaya çapraz geçişle geçer. Aynı parça zaten istenmişse
        /// yeniden başlatmaz; yalnızca sönüyorsa yeniden yükseltir.
        /// </summary>
        public bool PlayMusic(AudioClip clip)
        {
            if (_isDuplicate || _music[0] == null || clip == null)
            {
                return false;
            }

            _musicAutoStartPending = false;
            SoundLibrary.MusicSettings settings = MusicConfig;
            MusicChannel active = _music[_activeMusic];

            if (_musicRequested && active.Source.clip == clip)
            {
                active.Source.loop = settings.loop;
                ApplyMusicMute();
                return true;
            }

            // Çalan parça (varsa) sönerek biter; yenisi diğer kaynakta yükselir.
            if (_musicRequested)
            {
                FadeOutAndStop(active, settings.fadeOutSeconds);
            }

            _activeMusic = 1 - _activeMusic;
            MusicChannel next = _music[_activeMusic];
            next.Source.Stop();
            next.Source.clip = clip;
            next.Source.loop = settings.loop;
            next.Paused = false;
            next.Pending = true;
            SetFade(next, 0f, 0f);

            _musicRequested = true;
            _musicPlayTime = 0f;
            ApplyMusicMute();
            return true;
        }

        /// <summary>Müziği sönerek durdurur.</summary>
        public void StopMusic()
        {
            _musicAutoStartPending = false;
            if (!_musicRequested || _music[0] == null)
            {
                return;
            }

            _musicRequested = false;
            MusicChannel active = _music[_activeMusic];
            active.Pending = false;

            if (active.Paused)
            {
                active.Paused = false;
                active.Source.Stop();
                SetFade(active, 0f, 0f);
                return;
            }

            FadeOutAndStop(active, MusicConfig.fadeOutSeconds);
        }

        /// <summary>
        /// Müziğin sessiz/çalıyor durumunu uygular: sessizse söndürür (0'a
        /// varınca Update duraklatır), değilse başlatır veya kaldığı yerden
        /// sürdürüp yükseltir.
        /// </summary>
        private void ApplyMusicMute()
        {
            if (!_musicRequested || _music[0] == null)
            {
                return;
            }

            MusicChannel active = _music[_activeMusic];
            SoundLibrary.MusicSettings settings = MusicConfig;

            if (_musicMuted)
            {
                if (!active.Pending && !active.Paused)
                {
                    SetFade(active, 0f, settings.fadeOutSeconds);
                }

                return;
            }

            if (active.Pending)
            {
                active.Pending = false;
                active.Source.Play();
            }
            else if (active.Paused)
            {
                active.Paused = false;
                active.Source.UnPause();
            }

            SetFade(active, 1f, settings.fadeInSeconds);
        }

        private void FadeOutAndStop(MusicChannel channel, float seconds)
        {
            channel.Pending = false;
            channel.Paused = false;
            SetFade(channel, 0f, seconds);
        }

        /// <summary>Fade hedefini ayarlar; süre 0 ise hemen uygular.</summary>
        private void SetFade(MusicChannel channel, float target, float seconds)
        {
            channel.Target = target;
            if (seconds <= 0f)
            {
                channel.Fade = target;
                channel.Rate = 0f;
                channel.Source.volume = target * MusicConfig.volume * _musicVolume;
                return;
            }

            channel.Rate = 1f / seconds;
        }

        private void UpdateMusic(float deltaTime)
        {
            if (_music[0] == null)
            {
                return;
            }

            float mixVolume = MusicConfig.volume * _musicVolume;

            for (int i = 0; i < _music.Length; i++)
            {
                MusicChannel channel = _music[i];
                if (channel.Fade != channel.Target)
                {
                    channel.Fade = Mathf.MoveTowards(channel.Fade, channel.Target, channel.Rate * deltaTime);
                }

                channel.Source.volume = channel.Fade * mixVolume;

                if (channel.Fade > 0f || channel.Target > 0f || !channel.Source.isPlaying)
                {
                    continue;
                }

                // Sönme bitti: sessize alınan etkin parça kaldığı yerde bekler, diğerleri durur.
                if (i == _activeMusic && _musicRequested && _musicMuted)
                {
                    channel.Source.Pause();
                    channel.Paused = true;
                }
                else
                {
                    channel.Source.Stop();
                }
            }

            UpdatePlaylist(deltaTime);
        }

        /// <summary>Döngü kapalıysa biten parçanın yerine sıradakini başlatır.</summary>
        private void UpdatePlaylist(float deltaTime)
        {
            MusicChannel active = _music[_activeMusic];
            AudioClip clip = active.Source.clip;
            if (!_musicRequested || active.Pending || active.Paused || clip == null)
            {
                return;
            }

            if (active.Source.isPlaying)
            {
                _musicPlayTime += deltaTime;
                return;
            }

            // isPlaying yükleme sırasında da kısa süre false olabilir; parça gerçekten bitti mi?
            if (active.Source.loop || _musicPlayTime < clip.length - 0.1f)
            {
                return;
            }

            AudioClip next = ResolveTrack(_trackIndex + 1, out int index);
            _trackIndex = index;

            if (next == null || next == clip)
            {
                _musicPlayTime = 0f;
                active.Source.Play();
                return;
            }

            PlayMusic(next);
        }

        /// <summary>Kütüphanedeki parçayı bulur; parça yoksa (izin varsa) prosedürel döngü. Kütüphane yoksa null.</summary>
        private AudioClip ResolveTrack(int index, out int resolvedIndex)
        {
            resolvedIndex = 0;
            if (soundLibrary == null)
            {
                return null;
            }

            SoundLibrary.MusicSettings settings = soundLibrary.Music;
            int found = settings.FindTrack(index);
            if (found >= 0)
            {
                resolvedIndex = found;
                return settings.tracks[found];
            }

            if (!proceduralFallback)
            {
                return null;
            }

            if (_proceduralMusic == null)
            {
                _proceduralMusic = ToneGenerator.CreateMusicClip();
            }

            return _proceduralMusic;
        }

        private AudioClip GetProceduralSfx(SfxType type)
        {
            if (!proceduralFallback || _proceduralSfx == null)
            {
                return null;
            }

            int index = (int)type;
            if (_proceduralSfx[index] == null)
            {
                _proceduralSfx[index] = ToneGenerator.CreateSfxClip(type);
            }

            return _proceduralSfx[index];
        }

        /// <summary>Çalışma anında üretilen klipler sahnesiz nesnelerdir; kendiliğinden silinmez.</summary>
        private void DestroyProceduralClips()
        {
            if (_proceduralSfx != null)
            {
                for (int i = 0; i < _proceduralSfx.Length; i++)
                {
                    if (_proceduralSfx[i] != null)
                    {
                        Destroy(_proceduralSfx[i]);
                    }
                }

                _proceduralSfx = null;
            }

            if (_proceduralMusic != null)
            {
                Destroy(_proceduralMusic);
                _proceduralMusic = null;
            }
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

        /// <summary>Oyuncu müzik seviyesi (0-1); hemen duyulur.</summary>
        public void SetMusicVolume(float volume)
        {
            float clamped = float.IsNaN(volume) ? defaultMusicVolume : Mathf.Clamp01(volume);
            if (Mathf.Approximately(clamped, _musicVolume))
            {
                return;
            }

            _musicVolume = clamped;
            onSettingsChanged?.Invoke();
        }

        /// <summary>Müziği söndürüp duraklatır veya kaldığı yerden yükselterek sürdürür. Efekt sesini etkilemez.</summary>
        public void SetMusicMuted(bool muted)
        {
            if (muted == _musicMuted)
            {
                return;
            }

            _musicMuted = muted;
            ApplyMusicMute();
            onSettingsChanged?.Invoke();
        }

        public void ToggleMusicMute()
        {
            SetMusicMuted(!_musicMuted);
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

        private void ApplyMusicSettings(float volume, bool muted)
        {
            _musicVolume = float.IsNaN(volume) ? 1f : Mathf.Clamp01(volume);
            _musicMuted = muted;
        }

        // ── Kayıt (ISaveable) ──────────────────────────────────────────────────

        public void CaptureState(SaveData data)
        {
            data.audio = new AudioSaveData
            {
                hasSettings = true,
                volume = _volume,
                muted = _muted,
                hapticsEnabled = _hapticsEnabled,
                hasMusicSettings = true,
                musicVolume = _musicVolume,
                musicMuted = _musicMuted
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

            if (state != null && state.hasMusicSettings)
            {
                ApplyMusicSettings(state.musicVolume, state.musicMuted);
            }
            else
            {
                ApplyMusicSettings(defaultMusicVolume, defaultMusicMuted);
            }

            if (_muted)
            {
                StopAll();
            }

            ApplyMusicMute();
            onSettingsChanged?.Invoke();
        }
    }
}
