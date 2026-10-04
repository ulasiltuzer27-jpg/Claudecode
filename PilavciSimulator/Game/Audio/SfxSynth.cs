namespace PilavciSimulator.Audio;

/// <summary>
/// Prosedurel ses efektleri. Oyunun ses dosyasi yok: her efekt burada
/// osilator + gurultu + zarf + filtre ile sentezlenip bellekte WAV'a
/// cevriliyor. Gercek kayitlarla degistirmek icin <c>Assets/Audio/Sfx/AD.wav</c>
/// konursa o kullanilir (bkz. <see cref="BuildAll"/>).
///
/// Deterministik: ayni tohumla ayni baytlar.
/// </summary>
public static class SfxSynth
{
    public const int Rate = 22050;

    private sealed class Buf
    {
        public readonly float[] S;
        public Buf(float seconds) => S = new float[(int)(seconds * Rate)];
        public int Length => S.Length;
    }

    private static uint _seed = 12345;

    private static float Noise()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (_seed & 0xFFFFFF) / 8388607.5f - 1f;
    }

    private static float Env(float t, float attack, float decay) =>
        t < attack ? t / attack : MathF.Exp(-(t - attack) / decay);

    private static void LowPass(float[] s, float cutoff)
    {
        var rc = 1f / (cutoff * MathF.Tau);
        var a = (1f / Rate) / (rc + 1f / Rate);
        var y = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            y += a * (s[i] - y);
            s[i] = y;
        }
    }

    private static void HighPass(float[] s, float cutoff)
    {
        var rc = 1f / (cutoff * MathF.Tau);
        var a = rc / (rc + 1f / Rate);
        float prevX = 0, prevY = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var y = a * (prevY + s[i] - prevX);
            prevX = s[i];
            prevY = y;
            s[i] = y;
        }
    }

    private static void BandPass(float[] s, float center, float q)
    {
        // RBJ biquad bandpass
        var w0 = MathF.Tau * center / Rate;
        var alpha = MathF.Sin(w0) / (2 * q);
        var b0 = alpha;
        var b2 = -alpha;
        var a0 = 1 + alpha;
        var a1 = -2 * MathF.Cos(w0);
        var a2 = 1 - alpha;
        float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var x = s[i];
            var y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;
            s[i] = y;
        }
    }

    private static void Normalize(float[] s, float peak = 0.85f)
    {
        var max = 0f;
        foreach (var v in s)
        {
            max = MathF.Max(max, MathF.Abs(v));
        }

        if (max < 1e-6f)
        {
            return;
        }

        var k = peak / max;
        for (var i = 0; i < s.Length; i++)
        {
            s[i] *= k;
        }
    }

    /// <summary>Dongu sesleri icin: sonu basina yumusakca baglanir (tiklama olmasin).</summary>
    private static void LoopCrossfade(float[] s, float seconds = 0.25f)
    {
        var n = Math.Min((int)(seconds * Rate), s.Length / 4);
        for (var i = 0; i < n; i++)
        {
            var t = i / (float)n;
            var head = s[i];
            var tail = s[s.Length - n + i];
            s[i] = head * t + tail * (1 - t);
        }

        Array.Resize(ref s, s.Length - n);
    }

    private static float[] Tone(float seconds, Func<float, float> freq, Func<float, float> amp, float noise = 0f, int harmonics = 1)
    {
        var b = new Buf(seconds);
        var phase = 0f;
        for (var i = 0; i < b.Length; i++)
        {
            var t = i / (float)Rate;
            phase += freq(t) / Rate;
            var v = 0f;
            for (var h = 1; h <= harmonics; h++)
            {
                v += MathF.Sin(phase * MathF.Tau * h) / h;
            }

            v += Noise() * noise;
            b.S[i] = v * amp(t);
        }

        return b.S;
    }

    private static float[] NoiseBurst(float seconds, Func<float, float> amp)
    {
        var b = new Buf(seconds);
        for (var i = 0; i < b.Length; i++)
        {
            b.S[i] = Noise() * amp(i / (float)Rate);
        }

        return b.S;
    }

    private static float[] Mix(params float[][] parts)
    {
        var len = parts.Max(p => p.Length);
        var r = new float[len];
        foreach (var p in parts)
        {
            for (var i = 0; i < p.Length; i++)
            {
                r[i] += p[i];
            }
        }

        return r;
    }

    private static float[] Concat(params float[][] parts)
    {
        var r = new float[parts.Sum(p => p.Length)];
        var o = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, r, o, p.Length);
            o += p.Length;
        }

        return r;
    }

    private static float[] Silence(float seconds) => new float[(int)(seconds * Rate)];

    public static byte[] ToWav(float[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var dataLen = samples.Length * 2;
        w.Write("RIFF"u8);
        w.Write(36 + dataLen);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)1);
        w.Write(Rate);
        w.Write(Rate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(dataLen);
        foreach (var s in samples)
        {
            w.Write((short)Math.Clamp(s * 32767f, -32768f, 32767f));
        }

        return ms.ToArray();
    }

    /// <summary>Tek seferlik efektler.</summary>
    public static IEnumerable<(string Id, byte[] Wav)> BuildAll()
    {
        _seed = 12345;
        var list = new List<(string, float[])>
        {
            ("ui_click", Tone(0.06f, _ => 1400, t => Env(t, 0.002f, 0.015f) * 0.5f, harmonics: 2)),
            ("ui_hover", Tone(0.04f, _ => 2200, t => Env(t, 0.002f, 0.008f) * 0.2f)),
            ("ui_open", Tone(0.18f, t => 500 + t * 2400, t => Env(t, 0.01f, 0.06f) * 0.4f, harmonics: 3)),
            ("ui_close", Tone(0.16f, t => 900 - t * 2200, t => Env(t, 0.01f, 0.05f) * 0.4f, harmonics: 3)),
            ("ui_error", Tone(0.25f, _ => 180, t => Env(t, 0.005f, 0.08f) * 0.5f, harmonics: 5)),
            ("coin", Mix(Tone(0.35f, _ => 2637, t => Env(t, 0.002f, 0.12f) * 0.4f), Concat(Silence(0.07f), Tone(0.3f, _ => 3520, t => Env(t, 0.002f, 0.1f) * 0.35f)))),
            ("cash", Mix(Tone(0.7f, _ => 1760, t => Env(t, 0.002f, 0.25f) * 0.35f, harmonics: 2),
                Concat(Silence(0.04f), Tone(0.6f, _ => 2349, t => Env(t, 0.002f, 0.2f) * 0.3f)),
                NoiseBurst(0.12f, t => Env(t, 0.001f, 0.03f) * 0.4f))),
            ("pickup", Hp(NoiseBurst(0.14f, t => MathF.Sin(MathF.PI * t / 0.14f) * 0.5f), 900)),
            ("place", Lp(Mix(Tone(0.12f, t => 140 - t * 300, t => Env(t, 0.002f, 0.04f) * 0.8f), NoiseBurst(0.06f, t => Env(t, 0.001f, 0.015f) * 0.5f)), 2000)),
            ("drop", Lp(Mix(Tone(0.2f, t => 110 - t * 200, t => Env(t, 0.002f, 0.06f)), NoiseBurst(0.1f, t => Env(t, 0.001f, 0.03f) * 0.8f)), 1500)),
            ("knob", Hp(Mix(NoiseBurst(0.03f, t => Env(t, 0.0005f, 0.006f)), Tone(0.03f, _ => 3000, t => Env(t, 0.0005f, 0.004f) * 0.5f)), 1500)),
            ("lid", Mix(Tone(0.5f, _ => 1180, t => Env(t, 0.001f, 0.12f) * 0.35f, harmonics: 1), Tone(0.5f, _ => 1730, t => Env(t, 0.001f, 0.09f) * 0.25f), NoiseBurst(0.05f, t => Env(t, 0.001f, 0.01f) * 0.4f))),
            ("plate", Mix(Tone(0.4f, _ => 2100, t => Env(t, 0.001f, 0.07f) * 0.3f), Tone(0.4f, _ => 3150, t => Env(t, 0.001f, 0.05f) * 0.2f), Hp(NoiseBurst(0.03f, t => Env(t, 0.001f, 0.008f) * 0.5f), 2000))),
            ("scoop", Lp(Mix(NoiseBurst(0.25f, t => Env(t, 0.02f, 0.08f) * 0.6f), Tone(0.15f, t => 220 - t * 400, t => Env(t, 0.005f, 0.05f) * 0.5f)), 3000)),
            ("pour_rice", Bp(NoiseBurst(0.7f, t => MathF.Min(1, t * 10) * MathF.Min(1, (0.7f - t) * 6) * (0.6f + 0.4f * MathF.Sin(t * 90))), 4500, 0.7f)),
            ("pour_water", Bp(NoiseBurst(0.7f, t => MathF.Min(1, t * 8) * MathF.Min(1, (0.7f - t) * 5) * (0.7f + 0.3f * MathF.Sin(t * 37))), 900, 0.9f)),
            ("sizzle", Hp(NoiseBurst(0.6f, t => Env(t, 0.01f, 0.25f) * (0.6f + 0.4f * Noise())), 3000)),
            ("salt", Hp(NoiseBurst(0.18f, t => MathF.Max(0, MathF.Sin(t * 120)) * Env(t, 0.005f, 0.08f)), 3500)),
            ("pepper", Hp(NoiseBurst(0.22f, t => MathF.Max(0, MathF.Sin(t * 80)) * Env(t, 0.005f, 0.1f)), 2500)),
            ("stir", Bp(NoiseBurst(0.35f, t => MathF.Sin(MathF.PI * t / 0.35f) * 0.7f), 1800, 1.2f)),
            ("serve", Concat(Tone(0.12f, _ => 784, t => Env(t, 0.005f, 0.06f) * 0.4f, harmonics: 3), Tone(0.25f, _ => 1175, t => Env(t, 0.005f, 0.1f) * 0.4f, harmonics: 3))),
            ("bad", Tone(0.35f, t => 220 - t * 120, t => Env(t, 0.005f, 0.12f) * 0.45f, harmonics: 6)),
            ("tip", Concat(Tone(0.08f, _ => 1568, t => Env(t, 0.002f, 0.04f) * 0.3f, harmonics: 2), Tone(0.08f, _ => 2093, t => Env(t, 0.002f, 0.04f) * 0.3f, harmonics: 2), Tone(0.2f, _ => 2637, t => Env(t, 0.002f, 0.08f) * 0.3f, harmonics: 2))),
            ("levelup", Concat(Tone(0.12f, _ => 523, t => Env(t, 0.005f, 0.08f) * 0.4f, harmonics: 3), Tone(0.12f, _ => 659, t => Env(t, 0.005f, 0.08f) * 0.4f, harmonics: 3), Tone(0.12f, _ => 784, t => Env(t, 0.005f, 0.08f) * 0.4f, harmonics: 3), Tone(0.5f, _ => 1046, t => Env(t, 0.005f, 0.25f) * 0.45f, harmonics: 3))),
            ("achievement", Mix(Concat(Tone(0.1f, _ => 880, t => Env(t, 0.003f, 0.06f) * 0.35f, harmonics: 2), Tone(0.5f, _ => 1318, t => Env(t, 0.003f, 0.25f) * 0.35f, harmonics: 2)), Concat(Silence(0.1f), Tone(0.5f, _ => 1046, t => Env(t, 0.003f, 0.25f) * 0.25f, harmonics: 2)))),
            ("whistle", Concat(Tone(0.35f, t => 2900 + MathF.Sin(t * 180) * 160, t => MathF.Min(1, t * 40) * 0.5f, noise: 0.08f), Silence(0.08f), Tone(0.6f, t => 2900 + MathF.Sin(t * 180) * 160, t => MathF.Min(1, t * 40) * Env(t, 0.01f, 0.4f) * 0.5f, noise: 0.08f))),
            ("meow", Bp(Tone(0.6f, t => 520 + MathF.Sin(MathF.PI * t / 0.6f) * 380, t => MathF.Sin(MathF.PI * t / 0.6f) * 0.7f, harmonics: 6), 1100, 0.8f)),
            ("purr", Lp(Tone(0.9f, _ => 26, t => (0.5f + 0.5f * MathF.Sin(t * 14)) * 0.8f, noise: 0.6f, harmonics: 4), 400)),
            ("seagull", Concat(Bp(Tone(0.25f, t => 1800 - t * 1600, t => MathF.Sin(MathF.PI * t / 0.25f) * 0.7f, harmonics: 4, noise: 0.1f), 2000, 1.2f), Silence(0.05f), Bp(Tone(0.18f, t => 1700 - t * 1400, t => MathF.Sin(MathF.PI * t / 0.18f) * 0.6f, harmonics: 4, noise: 0.1f), 2000, 1.2f))),
            ("horn", Lp(Tone(1.6f, _ => 98, t => MathF.Min(1, t * 6) * MathF.Min(1, (1.6f - t) * 4) * 0.7f, harmonics: 8), 900)),
            ("van_horn", Concat(Tone(0.18f, _ => 410, t => 0.4f, harmonics: 5), Silence(0.06f), Tone(0.3f, _ => 410, t => MathF.Min(1, (0.3f - t) * 10) * 0.4f, harmonics: 5))),
            ("splash", Lp(NoiseBurst(0.5f, t => Env(t, 0.005f, 0.15f)), 2500)),
            ("wash", Bp(NoiseBurst(0.5f, t => MathF.Sin(MathF.PI * t / 0.5f) * (0.7f + 0.3f * MathF.Sin(t * 60))), 1200, 0.8f)),
            ("shred", Bp(NoiseBurst(0.18f, t => Env(t, 0.003f, 0.05f)), 2500, 1.5f)),
            ("ignite", Mix(Hp(NoiseBurst(0.4f, t => Env(t, 0.005f, 0.15f) * 0.7f), 600), Tone(0.08f, _ => 4000, t => Env(t, 0.001f, 0.01f) * 0.4f))),
            ("money_bad", Concat(Tone(0.15f, _ => 330, t => Env(t, 0.005f, 0.08f) * 0.4f, harmonics: 4), Tone(0.3f, _ => 247, t => Env(t, 0.005f, 0.15f) * 0.4f, harmonics: 4))),
            ("shutter", Lp(Mix(NoiseBurst(1.2f, t => (0.5f + 0.5f * MathF.Sin(t * 70)) * MathF.Min(1, t * 8) * MathF.Min(1, (1.2f - t) * 5) * 0.6f), Tone(1.2f, _ => 60, _ => 0.2f, harmonics: 3)), 1800)),
            ("eat", Lp(NoiseBurst(0.2f, t => MathF.Max(0, MathF.Sin(t * 50)) * 0.5f), 1200)),
            ("bell", Mix(Tone(1.2f, _ => 1320, t => Env(t, 0.002f, 0.4f) * 0.35f), Tone(1.2f, _ => 1980, t => Env(t, 0.002f, 0.3f) * 0.2f), Tone(1.2f, _ => 2640, t => Env(t, 0.002f, 0.2f) * 0.1f))),
        };

        for (var i = 1; i <= 4; i++)
        {
            var dur = 0.09f + i * 0.005f;
            list.Add(($"step{i}", Lp(Mix(NoiseBurst(dur, t => Env(t, 0.002f, 0.02f) * 0.8f), Tone(dur, t => 90 + i * 8 - t * 200, t => Env(t, 0.002f, 0.025f) * 0.6f)), 1400 + i * 150)));
        }

        foreach (var (id, s) in list)
        {
            var custom = Path.Combine(Core.Paths.Assets, "Audio", "Sfx", id + ".wav");
            if (File.Exists(custom))
            {
                yield return (id, File.ReadAllBytes(custom));
                continue;
            }

            Normalize(s, 0.8f);
            yield return (id, ToWav(s));
        }
    }

    /// <summary>Dongulu sesler (ortam, kaynama, alev).</summary>
    public static IEnumerable<(string Id, byte[] Wav)> BuildLoops()
    {
        _seed = 777;
        var loops = new List<(string, float[])>();

        // Sehir ugultusu: alcak frekansli trafik + uzak kornalar
        {
            var s = NoiseBurst(8f, t => 0.6f + 0.25f * MathF.Sin(t * 0.7f) + 0.15f * MathF.Sin(t * 2.3f));
            LowPass(s, 350);
            var horn = Concat(Silence(3.1f), Lp(Tone(0.25f, _ => 380, t => MathF.Min(1, (0.25f - t) * 8) * 0.06f, harmonics: 5), 1200));
            s = Mix(s, horn);
            loops.Add(("amb_city", s));
        }

        // Deniz: dalga kabarmasi
        {
            var s = NoiseBurst(9f, t => 0.25f + 0.75f * MathF.Pow(MathF.Max(0, MathF.Sin(t * MathF.Tau / 4.5f)), 2));
            LowPass(s, 900);
            loops.Add(("amb_sea", s));
        }

        // Yagmur
        {
            var s = NoiseBurst(6f, _ => 0.8f);
            HighPass(s, 900);
            LowPass(s, 7000);
            var drops = NoiseBurst(6f, t => Noise() > 0.97f ? 1.5f : 0f);
            LowPass(drops, 3000);
            loops.Add(("amb_rain", Mix(s, drops)));
        }

        // Gece: cirit bocekleri
        {
            var s = Tone(6f, _ => 4300, t => MathF.Max(0, MathF.Sin(t * MathF.Tau * 18)) * (MathF.Sin(t * MathF.Tau * 0.9f) > 0.2f ? 0.25f : 0f));
            loops.Add(("amb_night", s));
        }

        // Kalabalik ugultusu (stadyum, iskele)
        {
            var s = NoiseBurst(7f, t => 0.7f + 0.3f * MathF.Sin(t * 1.3f));
            BandPass(s, 600, 0.6f);
            loops.Add(("amb_crowd", s));
        }

        // Kaynama: rastgele "blop"lar
        {
            var s = new float[(int)(4f * Rate)];
            var r = 0;
            while (r < s.Length - Rate / 4)
            {
                var len = (int)(Rate * (0.03f + (Noise() * 0.5f + 0.5f) * 0.05f));
                var f0 = 180 + (Noise() * 0.5f + 0.5f) * 420;
                for (var i = 0; i < len && r + i < s.Length; i++)
                {
                    var t = i / (float)Rate;
                    s[r + i] += MathF.Sin(MathF.Tau * (f0 + t * 4000) * t) * MathF.Exp(-t * 60) * 0.6f;
                }

                r += (int)(Rate * (0.02f + (Noise() * 0.5f + 0.5f) * 0.09f));
            }

            loops.Add(("loop_boil", s));
        }

        // Cizirti (yag/tereyagi)
        {
            var s = NoiseBurst(4f, _ => 0.4f + (Noise() > 0.9f ? 0.8f : 0f));
            HighPass(s, 2500);
            loops.Add(("loop_sizzle", s));
        }

        // Gaz alevi: hafif "fsss"
        {
            var s = NoiseBurst(4f, _ => 0.5f);
            BandPass(s, 1500, 0.4f);
            loops.Add(("loop_flame", s));
        }

        // Musluk
        {
            var s = NoiseBurst(4f, t => 0.7f + 0.3f * MathF.Sin(t * 23));
            BandPass(s, 1100, 0.5f);
            loops.Add(("loop_water", s));
        }

        // Kepce/karistirma surtunmesi (basili tutarken)
        {
            var s = NoiseBurst(3f, t => 0.4f + 0.6f * MathF.Max(0, MathF.Sin(t * MathF.Tau * 1.6f)));
            BandPass(s, 1700, 1.1f);
            loops.Add(("loop_stir", s));
        }

        foreach (var (id, s0) in loops)
        {
            var s = s0;
            Normalize(s, 0.7f);
            var n = Math.Min((int)(0.3f * Rate), s.Length / 4);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)n;
                s[i] = s[i] * t + s[s.Length - n + i] * (1 - t);
            }

            Array.Resize(ref s, s.Length - n);
            yield return (id, ToWav(s));
        }
    }

    private static float[] Lp(float[] s, float c)
    {
        LowPass(s, c);
        return s;
    }

    private static float[] Hp(float[] s, float c)
    {
        HighPass(s, c);
        return s;
    }

    private static float[] Bp(float[] s, float c, float q)
    {
        BandPass(s, c, q);
        return s;
    }
}
