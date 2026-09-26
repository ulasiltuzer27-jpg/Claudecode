using System;
using UnityEngine;

namespace IdleRestaurant.Audio
{
    /// <summary>
    /// Ses dosyası olmadan test edebilmek için basit prosedürel sentez: her
    /// <see cref="SfxType"/> için kısa bir efekt (yumuşak tık, tatlı çan,
    /// yükselen arpej, fanfar, parıltılı süzülme) ve dikişsiz dönen sakin
    /// bir arka plan müziği. <see cref="AudioManager"/>, kütüphanede gerçek
    /// klip yoksa bunları kullanır; gerçek klip atanınca o tercih edilir.
    ///
    /// ── Yapı ────────────────────────────────────────────────────────────────
    /// Render* metotları saf: yalnızca örnek dizisi (mono, -1..1) üretir,
    /// Unity'ye dokunmaz ve her çağrıda aynı sonucu verir. AudioClip'e
    /// çevirmek <see cref="CreateClip"/>'in işi. Üretilen klipler çalışma
    /// anında oluşturulduğu için sahibi (AudioManager) işi bitince yok etmeli.
    ///
    /// ── Hız ─────────────────────────────────────────────────────────────────
    /// Sinüsler her örnekte Math.Sin yerine dönen bir fazörle (karmaşık sayı
    /// çarpımı) hesaplanır; 10 sn'lik müzik döngüsü birkaç on milisaniyede çıkar.
    /// Müzikte notaların kuyrukları döngünün başına sarılır: son ölçüde çalan
    /// notanın sesi baştan devam eder, döngü noktasında tık duyulmaz.
    /// </summary>
    public static class ToneGenerator
    {
        /// <summary>Üretilen kliplerin örnekleme hızı (Hz). Sentez sesleri için yeterli, belleği yarıya indirir.</summary>
        public const int SampleRate = 22050;

        /// <summary>Müzik döngüsünün temposu (vuruş/dk).</summary>
        public const float MusicBpm = 100f;

        /// <summary>Müzik döngüsünün uzunluğu (4/4 ölçü).</summary>
        public const int MusicBars = 4;

        private const double TwoPi = Math.PI * 2d;

        /// <summary>Notalar bu kadar zaman sabitinden sonra kesilir: e^-5.76 ≈ -50 dB, duyulmaz.</summary>
        private const double DecayCutoff = 5.76d;

        /// <summary>Efekt sonundaki kısa sönüm; son örnek tam sıfıra insin, tık olmasın.</summary>
        private const double EdgeFadeSeconds = 0.004d;

        // Tınılar: (frekans oranı, genlik) çiftleri.
        private static readonly double[] Bell = { 1d, 1d, 2d, 0.4d, 3d, 0.15d, 4.2d, 0.08d };
        private static readonly double[] Pluck = { 1d, 1d, 2d, 0.25d, 3d, 0.06d };
        private static readonly double[] Warm = { 1d, 1d, 2d, 0.3d, 3d, 0.1d };
        private static readonly double[] Tick = { 1d, 1d, 1.5d, 0.5d };
        private static readonly double[] Bass = { 1d, 1d, 2d, 0.3d };

        // Müzik: Cmaj7 → Am7 → Fmaj7 → G7, her akor bir ölçü (MIDI nota numaraları).
        private static readonly int[][] MusicChords =
        {
            new[] { 60, 64, 67, 71 },
            new[] { 57, 60, 64, 67 },
            new[] { 53, 57, 60, 64 },
            new[] { 55, 59, 62, 65 }
        };

        private static readonly int[] MusicBassRoots = { 48, 45, 41, 43 };

        /// <summary>Ölçü başına sekiz sekizlik arpej; 4 = kökün bir oktav üstü.</summary>
        private static readonly int[] ArpeggioSteps = { 0, 1, 2, 3, 4, 2, 1, 2 };

        /// <summary>2. ve 4. ölçüde çan gibi basit bir ezgi (vuruş başına bir nota).</summary>
        private static readonly int[][] MusicMelody =
        {
            null,
            new[] { 76, 79, 81, 79 },
            null,
            new[] { 74, 79, 77, 74 }
        };

        // ── Unity ──────────────────────────────────────────────────────────────

        /// <summary>Mono örneklerden bellekte duran (stream olmayan) bir klip oluşturur.</summary>
        public static AudioClip CreateClip(string clipName, float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                throw new ArgumentException("Boş örnek dizisinden klip oluşturulamaz.", nameof(samples));
            }

            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateSfxClip(SfxType type)
        {
            return CreateClip("Procedural " + type, RenderSfx(type));
        }

        public static AudioClip CreateMusicClip()
        {
            return CreateClip("Procedural Music", RenderMusicLoop());
        }

        // ── Efektler ───────────────────────────────────────────────────────────

        /// <summary>Efektin örnekleri; tepe genliği 0.8-0.9'a normalize, baş ve son sıfırda.</summary>
        public static float[] RenderSfx(SfxType type)
        {
            switch (type)
            {
                case SfxType.ButtonClick:
                    return RenderClick();
                case SfxType.CoinCollect:
                    return RenderCoin();
                case SfxType.StationUpgrade:
                    return RenderUpgrade();
                case SfxType.QuestComplete:
                    return RenderQuestComplete();
                case SfxType.PrestigeTrigger:
                    return RenderPrestige();
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Bu efekt için sentez tanımlı değil.");
            }
        }

        /// <summary>Hafif tık: çok kısa, yüksek perdeli bir vuruş + küçük bir hışırtı.</summary>
        private static float[] RenderClick()
        {
            float[] buffer = Allocate(0.06d);
            AddNote(buffer, 0d, 2100d, 0.6d, 0.0005d, 0.008d, Tick, false);
            AddNoise(buffer, 0d, 0.25d, 0.002d, 11, 0.5d, false);
            return Finish(buffer, 0.8f, false);
        }

        /// <summary>Tatlı çan: iki nota (Si5 → Mi6), çan tınısı.</summary>
        private static float[] RenderCoin()
        {
            float[] buffer = Allocate(0.5d);
            AddNote(buffer, 0d, 987.77d, 0.5d, 0.002d, 0.05d, Bell, false);
            AddNote(buffer, 0.07d, 1318.51d, 0.8d, 0.002d, 0.16d, Bell, false);
            return Finish(buffer, 0.85f, false);
        }

        /// <summary>Seviye atlama: hızlı yükselen Do majör arpej.</summary>
        private static float[] RenderUpgrade()
        {
            float[] buffer = Allocate(0.6d);
            double[] notes = { 523.25d, 659.25d, 783.99d, 1046.5d };
            for (int i = 0; i < notes.Length; i++)
            {
                AddNote(buffer, i * 0.07d, notes[i], 0.6d, 0.004d, i == notes.Length - 1 ? 0.22d : 0.09d, Warm, false);
            }

            return Finish(buffer, 0.85f, false);
        }

        /// <summary>Görev tamamlandı: üç notalık giriş + titreşimli (vibrato) majör akor.</summary>
        private static float[] RenderQuestComplete()
        {
            float[] buffer = Allocate(1.1d);
            AddNote(buffer, 0d, 783.99d, 0.5d, 0.004d, 0.1d, Bell, false);
            AddNote(buffer, 0.1d, 1046.5d, 0.5d, 0.004d, 0.1d, Bell, false);
            AddNote(buffer, 0.2d, 1318.51d, 0.5d, 0.004d, 0.12d, Bell, false);

            double[] chord = { 1046.5d, 1318.51d, 1567.98d };
            for (int i = 0; i < chord.Length; i++)
            {
                AddNote(buffer, 0.32d, chord[i], 0.35d, 0.01d, 0.3d, Warm, false, 5.5d, 0.004d);
            }

            return Finish(buffer, 0.9f, false);
        }

        /// <summary>Prestij: yukarı süzülen ton, pentatonik parıltılar ve kapanış akoru.</summary>
        private static float[] RenderPrestige()
        {
            float[] buffer = Allocate(1.6d);
            AddGlide(buffer, 0d, 330d, 1320d, 0.9d, 0.4d, 0.05d, 0.45d, Warm);

            double[] sparkles = { 1046.5d, 1174.66d, 1318.51d, 1567.98d, 1760d, 1567.98d, 2093d, 1760d };
            for (int i = 0; i < sparkles.Length; i++)
            {
                AddNote(buffer, 0.15d + i * 0.1d, sparkles[i], 0.25d, 0.002d, 0.1d, Bell, false);
            }

            double[] chord = { 523.25d, 659.25d, 783.99d, 1046.5d };
            for (int i = 0; i < chord.Length; i++)
            {
                AddNote(buffer, 0.95d, chord[i], 0.3d, 0.01d, 0.2d, Warm, false);
            }

            return Finish(buffer, 0.9f, false);
        }

        // ── Müzik ──────────────────────────────────────────────────────────────

        /// <summary>Müzik döngüsünün uzunluğu (örnek).</summary>
        public static int MusicLoopSamples => (int)Math.Round(60d / MusicBpm * 4d * MusicBars * SampleRate);

        /// <summary>
        /// Sakin, kafe havasında dört ölçülük döngü: arpejli yumuşak piyano,
        /// ölçü başı bas, arka vuruşlarda hafif çalkalayıcı (shaker), iki
        /// ölçüde çan ezgisi. Uçtan uca dikişsiz döner; tepe genliği 0.7.
        /// </summary>
        public static float[] RenderMusicLoop()
        {
            double beat = 60d / MusicBpm;
            float[] buffer = new float[MusicLoopSamples];

            for (int bar = 0; bar < MusicBars; bar++)
            {
                int chordIndex = bar % MusicChords.Length;
                int[] chord = MusicChords[chordIndex];
                double barStart = bar * 4d * beat;

                for (int step = 0; step < ArpeggioSteps.Length; step++)
                {
                    int toneIndex = ArpeggioSteps[step];
                    int note = toneIndex < chord.Length ? chord[toneIndex] : chord[0] + 12;
                    double time = barStart + step * 0.5d * beat;
                    AddNote(buffer, time, MidiToHz(note), 0.16d, 0.005d, 0.3d, Pluck, true);

                    if (step % 2 == 1)
                    {
                        AddNoise(buffer, time, 0.035d, 0.025d, bar * 16 + step, 0.6d, true);
                    }
                }

                int root = MusicBassRoots[chordIndex];
                AddNote(buffer, barStart, MidiToHz(root), 0.3d, 0.01d, 0.45d, Bass, true);
                AddNote(buffer, barStart + 2d * beat, MidiToHz(root), 0.22d, 0.01d, 0.4d, Bass, true);

                int[] melody = MusicMelody[chordIndex];
                if (melody != null)
                {
                    for (int i = 0; i < melody.Length; i++)
                    {
                        AddNote(buffer, barStart + i * beat, MidiToHz(melody[i]), 0.12d, 0.004d, 0.4d, Bell, true);
                    }
                }
            }

            return Finish(buffer, 0.7f, true);
        }

        public static double MidiToHz(int note)
        {
            return 440d * Math.Pow(2d, (note - 69) / 12d);
        }

        // ── Sentez yapı taşları ────────────────────────────────────────────────

        private static float[] Allocate(double seconds)
        {
            return new float[(int)Math.Ceiling(seconds * SampleRate)];
        }

        /// <summary>
        /// Harmonikli bir nota ekler: doğrusal atak, üstel sönüm.
        /// <paramref name="wrap"/> true ise taşan kuyruk dizinin başına sarılır (döngü).
        /// Vibrato verilirse faz her örnekte biriktirilir (yavaş yol), yoksa fazörle.
        /// </summary>
        private static void AddNote(float[] buffer, double startTime, double frequency, double amplitude, double attack,
            double decay, double[] partials, bool wrap, double vibratoHz = 0d, double vibratoDepth = 0d)
        {
            int start = (int)Math.Round(startTime * SampleRate);
            int attackSamples = Math.Max(1, (int)(attack * SampleRate));
            int length = attackSamples + (int)Math.Ceiling(decay * DecayCutoff * SampleRate);
            double decayFactor = Math.Exp(-1d / (decay * SampleRate));

            int count = partials.Length / 2;
            double[] step = new double[count];
            double[] gain = new double[count];
            double gainSum = 0d;
            for (int k = 0; k < count; k++)
            {
                double partialHz = frequency * partials[k * 2];
                // Nyquist'e yaklaşan harmonik aliasing yapar; atlanır.
                gain[k] = partialHz < SampleRate * 0.45d ? partials[k * 2 + 1] : 0d;
                step[k] = TwoPi * partialHz / SampleRate;
                gainSum += partials[k * 2 + 1];
            }

            double[] re = new double[count];
            double[] im = new double[count];
            double[] cos = new double[count];
            double[] sin = new double[count];
            double[] phase = vibratoHz > 0d ? new double[count] : null;
            for (int k = 0; k < count; k++)
            {
                re[k] = 1d;
                cos[k] = Math.Cos(step[k]);
                sin[k] = Math.Sin(step[k]);
            }

            double scale = amplitude / gainSum;
            double envelope = 1d;
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (wrap)
                {
                    index %= buffer.Length;
                }
                else if (index >= buffer.Length)
                {
                    break;
                }

                double sample = 0d;
                if (phase != null)
                {
                    double bend = 1d + vibratoDepth * Math.Sin(TwoPi * vibratoHz * i / SampleRate);
                    for (int k = 0; k < count; k++)
                    {
                        phase[k] += step[k] * bend;
                        sample += gain[k] * Math.Sin(phase[k]);
                    }
                }
                else
                {
                    for (int k = 0; k < count; k++)
                    {
                        double nextRe = re[k] * cos[k] - im[k] * sin[k];
                        im[k] = re[k] * sin[k] + im[k] * cos[k];
                        re[k] = nextRe;
                        sample += gain[k] * im[k];
                    }
                }

                double attackGain = i < attackSamples ? (double)i / attackSamples : 1d;
                buffer[index] += (float)(scale * attackGain * envelope * sample);

                if (i >= attackSamples)
                {
                    envelope *= decayFactor;
                }
            }
        }

        /// <summary>Frekansı üstel olarak kayan (süzülen) ton; kaydırma bitince son frekansta kalır.</summary>
        private static void AddGlide(float[] buffer, double startTime, double fromHz, double toHz, double glideSeconds,
            double amplitude, double attack, double decay, double[] partials)
        {
            int start = (int)Math.Round(startTime * SampleRate);
            int attackSamples = Math.Max(1, (int)(attack * SampleRate));
            int length = attackSamples + (int)Math.Ceiling(decay * DecayCutoff * SampleRate);
            int glideSamples = Math.Max(1, (int)(glideSeconds * SampleRate));
            double decayFactor = Math.Exp(-1d / (decay * SampleRate));
            double ratio = toHz / fromHz;

            int count = partials.Length / 2;
            double gainSum = 0d;
            for (int k = 0; k < count; k++)
            {
                gainSum += partials[k * 2 + 1];
            }

            double[] phase = new double[count];
            double envelope = 1d;
            for (int i = 0; i < length && start + i < buffer.Length; i++)
            {
                double t = Math.Min(1d, (double)i / glideSamples);
                double hz = fromHz * Math.Pow(ratio, t);
                double sample = 0d;
                for (int k = 0; k < count; k++)
                {
                    double partialHz = hz * partials[k * 2];
                    phase[k] += TwoPi * partialHz / SampleRate;
                    if (partialHz < SampleRate * 0.45d)
                    {
                        sample += partials[k * 2 + 1] * Math.Sin(phase[k]);
                    }
                }

                double attackGain = i < attackSamples ? (double)i / attackSamples : 1d;
                buffer[start + i] += (float)(amplitude / gainSum * attackGain * envelope * sample);
                if (i >= attackSamples)
                {
                    envelope *= decayFactor;
                }
            }
        }

        /// <summary>
        /// Üstel sönümlü gürültü patlaması. <paramref name="smoothing"/> (0-1)
        /// basit bir alçak geçiren filtre: büyüdükçe hışırtı yumuşar. Tohum
        /// sabit; her render aynı sesi verir.
        /// </summary>
        private static void AddNoise(float[] buffer, double startTime, double amplitude, double decay, int seed,
            double smoothing, bool wrap)
        {
            int start = (int)Math.Round(startTime * SampleRate);
            int length = (int)Math.Ceiling(decay * DecayCutoff * SampleRate);
            double decayFactor = Math.Exp(-1d / (decay * SampleRate));
            System.Random random = new System.Random(seed);
            double filtered = 0d;
            double envelope = 1d;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (wrap)
                {
                    index %= buffer.Length;
                }
                else if (index >= buffer.Length)
                {
                    break;
                }

                double white = random.NextDouble() * 2d - 1d;
                filtered += (1d - smoothing) * (white - filtered);
                buffer[index] += (float)(amplitude * envelope * filtered);
                envelope *= decayFactor;
            }
        }

        /// <summary>
        /// Tepe genliğini <paramref name="peak"/>'e getirir. Döngü değilse
        /// sona kısa bir sönüm uygular: son örnek sıfır olur.
        /// </summary>
        private static float[] Finish(float[] buffer, float peak, bool loop)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                max = Math.Max(max, Math.Abs(buffer[i]));
            }

            if (max > 0f)
            {
                float gain = peak / max;
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] *= gain;
                }
            }

            if (!loop)
            {
                int fade = Math.Min(buffer.Length, (int)(EdgeFadeSeconds * SampleRate));
                for (int i = 0; i < fade; i++)
                {
                    buffer[buffer.Length - 1 - i] *= (float)i / fade;
                }
            }

            return buffer;
        }
    }
}
