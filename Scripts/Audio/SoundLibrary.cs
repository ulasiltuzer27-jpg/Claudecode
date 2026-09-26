using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Ses efekti türlerini klip, ses seviyesi, tekrar aralığı ve titreşimle
    /// eşleyen tasarım verisi. Create → Idle Restaurant → Audio → Sound
    /// Library ile oluşturulur ve <see cref="AudioManager"/>'a atanır.
    ///
    /// Yeni varlık beş türün hepsiyle, önerilen titreşim ve aralıklarla
    /// gelir; yalnızca klipleri sürüklemek yeter. Klibi olmayan türe
    /// AudioManager (izin verildiyse) prosedürel bir ses üretir; o da kapalıysa
    /// tür sessiz kalır ama titreşimi yine çalışır.
    ///
    /// Arka plan müziği <see cref="Music"/>'te: parçalar, karıştırma seviyesi,
    /// fade süreleri ve döngü. Parça yoksa AudioManager prosedürel bir döngü çalar.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Idle Restaurant/Audio/Sound Library", order = 20)]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class SoundEntry
        {
            public SfxType type;

            [Tooltip("Birden fazla klip verilirse her çalışta rastgele biri seçilir.")]
            public AudioClip[] clips = new AudioClip[0];

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("Her çalışta perde bu kadar rastgele oynar; aynı sesin tekrarı mekanik duyulmasın.")]
            [Range(0f, 0.5f)] public float pitchVariance = 0.05f;

            [Tooltip("Aynı sesin yeniden çalabilmesi için geçmesi gereken en kısa süre (sn). " +
                     "Pasif gelir gibi sık olaylarda ses yığılmasını önler.")]
            [Min(0f)] public float minInterval = 0.05f;

            [Tooltip("Bu sesle birlikte tetiklenen titreşim.")]
            public HapticType haptic = HapticType.None;

            public SoundEntry()
            {
            }

            public SoundEntry(SfxType type, float volume, float pitchVariance, float minInterval, HapticType haptic)
            {
                this.type = type;
                this.volume = volume;
                this.pitchVariance = pitchVariance;
                this.minInterval = minInterval;
                this.haptic = haptic;
            }

            /// <summary>Kliplerden birini rastgele seçer; hiç klip yoksa null.</summary>
            public AudioClip PickClip()
            {
                if (clips == null || clips.Length == 0)
                {
                    return null;
                }

                return clips.Length == 1 ? clips[0] : clips[UnityEngine.Random.Range(0, clips.Length)];
            }
        }

        /// <summary>Arka plan müziği (BGM) ayarları.</summary>
        [Serializable]
        public sealed class MusicSettings
        {
            [Tooltip("Çalınacak parçalar. Boşsa AudioManager (izin verildiyse) prosedürel bir döngü üretir.")]
            public AudioClip[] tracks = new AudioClip[0];

            [Tooltip("Müziğin karıştırmadaki seviyesi; oyuncunun müzik ayarıyla çarpılır. Efektleri bastırmasın diye düşük tutun.")]
            [Range(0f, 1f)] public float volume = 0.45f;

            [Tooltip("Başlarken ve sessizden çıkarken yükselme süresi (sn).")]
            [Min(0f)] public float fadeInSeconds = 1.5f;

            [Tooltip("Dururken, parça değişirken ve sessize alınırken alçalma süresi (sn).")]
            [Min(0f)] public float fadeOutSeconds = 0.8f;

            [Tooltip("Açık: parça başa sararak sürer. Kapalı: bitince sıradaki parçaya geçilir (liste başa sarar).")]
            public bool loop = true;

            /// <summary>
            /// <paramref name="index"/>'ten başlayarak ilk dolu parçayı bulur.
            /// </summary>
            /// <returns>Parçanın sırası; hiç parça yoksa -1.</returns>
            public int FindTrack(int index)
            {
                if (tracks == null || tracks.Length == 0)
                {
                    return -1;
                }

                for (int i = 0; i < tracks.Length; i++)
                {
                    int candidate = ((index + i) % tracks.Length + tracks.Length) % tracks.Length;
                    if (tracks[candidate] != null)
                    {
                        return candidate;
                    }
                }

                return -1;
            }
        }

        [SerializeField] private List<SoundEntry> entries = CreateDefaultEntries();

        [SerializeField] private MusicSettings music = new MusicSettings();

        private SoundEntry[] _lookup;

        public IReadOnlyList<SoundEntry> Entries => entries;

        public MusicSettings Music => music;

        /// <summary>Türün kaydını döndürür; kütüphanede yoksa null.</summary>
        public SoundEntry GetEntry(SfxType type)
        {
            if (_lookup == null)
            {
                BuildLookup();
            }

            int index = (int)type;
            return index >= 0 && index < _lookup.Length ? _lookup[index] : null;
        }

        /// <summary>
        /// Kütüphane atanmamışken veya türün kaydı yokken kullanılan titreşim:
        /// tıklama ve para hafif, seviye ve görev orta, prestij güçlü.
        /// </summary>
        public static HapticType GetDefaultHaptic(SfxType type)
        {
            switch (type)
            {
                case SfxType.ButtonClick:
                case SfxType.CoinCollect:
                    return HapticType.Light;
                case SfxType.StationUpgrade:
                case SfxType.QuestComplete:
                    return HapticType.Medium;
                case SfxType.PrestigeTrigger:
                    return HapticType.Heavy;
                default:
                    return HapticType.None;
            }
        }

        private static List<SoundEntry> CreateDefaultEntries()
        {
            return new List<SoundEntry>
            {
                new SoundEntry(SfxType.ButtonClick, 0.8f, 0.05f, 0.05f, HapticType.Light),
                new SoundEntry(SfxType.CoinCollect, 0.5f, 0.1f, 0.12f, HapticType.Light),
                new SoundEntry(SfxType.StationUpgrade, 0.9f, 0.03f, 0.05f, HapticType.Medium),
                new SoundEntry(SfxType.QuestComplete, 1f, 0f, 0.3f, HapticType.Medium),
                new SoundEntry(SfxType.PrestigeTrigger, 1f, 0f, 1f, HapticType.Heavy)
            };
        }

        private void BuildLookup()
        {
            _lookup = new SoundEntry[Enum.GetValues(typeof(SfxType)).Length];

            for (int i = 0; i < entries.Count; i++)
            {
                SoundEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                int index = (int)entry.type;
                if (index < 0 || index >= _lookup.Length)
                {
                    continue;
                }

                if (_lookup[index] != null)
                {
                    Debug.LogWarning($"[SoundLibrary] '{entry.type}' iki kez tanımlı; ilki kullanılıyor.", this);
                    continue;
                }

                _lookup[index] = entry;
            }
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void OnValidate()
        {
            // Inspector'daki değişiklik bir sonraki çalışta geçerli olsun.
            _lookup = null;
        }
    }
}
