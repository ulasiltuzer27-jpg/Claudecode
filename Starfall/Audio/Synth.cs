namespace Starfall.Audio;

public enum Wave { Sine, Triangle, Square, Saw }
public enum FilterType { Lowpass, Bandpass, Highpass }
public enum Bus { Music, Sfx, Amb }

/// <summary>Bir sesin tarifi (JS'teki tone()/noise() secenekleri). Zamanlar saniye.</summary>
public struct VoiceDesc
{
    public bool Noise;
    public Wave Wave;
    public FilterType Filter;
    public float Freq, Slide, Q, Detune;
    public double Start;    // mikser saatinde mutlak baslangic
    public float Dur, Vol, Attack, Reverb;
    public Bus Bus;
}

/// <summary>RBJ biquad (WebAudio BiquadFilterNode ile ayni tanimlar: alcak/yuksek geciren Q'su dB).</summary>
public struct Biquad
{
    private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

    public void Set(FilterType type, float freq, float q, float rate)
    {
        freq = Math.Clamp(freq, 10, rate * 0.49f);
        float w0 = 2 * MathF.PI * freq / rate;
        float cos = MathF.Cos(w0), sin = MathF.Sin(w0);
        float b0, b1, b2, a0, a1, a2;
        if (type == FilterType.Bandpass)
        {
            float alpha = sin / (2 * MathF.Max(0.0001f, q));
            b0 = alpha; b1 = 0; b2 = -alpha;
            a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
        }
        else
        {
            // WebAudio: Q dB cinsinden rezonans
            float qLin = MathF.Pow(10, q / 20);
            float alpha = sin / (2 * qLin);
            if (type == FilterType.Lowpass) { b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2; }
            else { b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2; }
            a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
        }
        _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
    }

    public float Process(float x)
    {
        // transpoze direkt form II
        float y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }
}

/// <summary>Tek seslik sentez (osilator veya filtreli gurultu) + ustel zarf.</summary>
public sealed class Voice
{
    public VoiceDesc D;
    private double _phase;
    private float _inc, _incMul, _freq, _freqMul;
    private float _gain, _gainMulA, _gainMulD;
    private int _attackSamples, _totalSamples, _pos, _stopSamples;
    private Biquad _bq;
    private uint _rng;
    private int _filterCounter;
    private readonly float _rate;
    public bool Done;

    public Voice(VoiceDesc d, float rate, uint seed)
    {
        D = d;
        _rate = rate;
        _rng = seed | 1;
        float detuneMul = MathF.Pow(2, d.Detune / 1200f);
        _freq = d.Freq * detuneMul;
        _attackSamples = Math.Max(1, (int)(d.Attack * rate));
        _totalSamples = Math.Max(_attackSamples + 1, (int)(d.Dur * rate));
        _stopSamples = _totalSamples + (int)(0.05f * rate);
        // ustel rampalar: 0.0001 -> vol (atak), vol -> 0.0001 (sonuna kadar)
        float vol = MathF.Max(0.0001f, d.Vol);
        _gain = 0.0001f;
        _gainMulA = MathF.Pow(vol / 0.0001f, 1f / _attackSamples);
        _gainMulD = MathF.Pow(0.0001f / vol, 1f / (_totalSamples - _attackSamples));
        _freqMul = d.Slide > 0 ? MathF.Pow(MathF.Max(20, d.Slide) / MathF.Max(1, d.Freq), 1f / _totalSamples) : 1;
        _inc = _freq / rate;
        _incMul = _freqMul;
        if (d.Noise) _bq.Set(d.Filter, d.Freq, d.Q <= 0 ? 1 : d.Q, rate);
    }

    private static float PolyBlep(double t, float dt)
    {
        if (t < dt) { float x = (float)(t / dt); return x + x - x * x - 1; }
        if (t > 1 - dt) { float x = (float)((t - 1) / dt); return x * x + x + x + 1; }
        return 0;
    }

    /// <summary>Ses ornegini uret (zarf ve kaydirma dahil).</summary>
    public float Next()
    {
        if (_pos >= _stopSamples) { Done = true; return 0; }
        float env = _pos < _totalSamples ? _gain : 0;
        if (_pos < _attackSamples) _gain *= _gainMulA;
        else if (_pos < _totalSamples) _gain *= _gainMulD;
        _pos++;
        float s;
        if (D.Noise)
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            float n = (_rng & 0xFFFFFF) / 8388608f - 1f;
            if (_freqMul != 1)
            {
                _freq *= _freqMul;
                if (++_filterCounter >= 32)
                {
                    _filterCounter = 0;
                    _bq.Set(D.Filter, _freq, D.Q <= 0 ? 1 : D.Q, _rate);
                }
            }
            s = _bq.Process(n);
        }
        else
        {
            double p = _phase;
            switch (D.Wave)
            {
                case Wave.Sine: s = MathF.Sin((float)(p * 2 * Math.PI)); break;
                case Wave.Triangle: s = p < 0.5 ? (float)(4 * p - 1) : (float)(3 - 4 * p); break;
                case Wave.Square:
                    s = p < 0.5 ? 1 : -1;
                    s += PolyBlep(p, _inc);
                    s -= PolyBlep((p + 0.5) % 1.0, _inc);
                    break;
                default:
                    s = (float)(2 * p - 1) - PolyBlep(p, _inc);
                    break;
            }
            _phase += _inc;
            if (_phase >= 1) _phase -= Math.Floor(_phase);
            _inc *= _incMul;
        }
        return s * env;
    }
}

/// <summary>Surekli filtreli gurultu (dalga, ruzgar): hedef kazanc/frekansa yumusak gecis.</summary>
public sealed class NoiseLoop
{
    public volatile float TargetGain, TargetFreq;
    private float _gain, _freq;
    private readonly FilterType _type;
    private readonly float _q, _rate;
    private Biquad _bq;
    private uint _rng = 0x9E3779B9;
    private int _counter;
    public Bus Bus = Bus.Amb;

    public NoiseLoop(FilterType type, float freq, float q, float rate)
    {
        _type = type;
        _freq = TargetFreq = freq;
        _q = q;
        _rate = rate;
        _bq.Set(type, freq, q, rate);
    }

    public float Next()
    {
        // ~0.3 sn zaman sabitli yumusatma
        _gain += (TargetGain - _gain) * 0.00008f;
        if (++_counter >= 64)
        {
            _counter = 0;
            _freq += (TargetFreq - _freq) * 0.012f;
            _bq.Set(_type, _freq, _q, _rate);
        }
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        float n = (_rng & 0xFFFFFF) / 8388608f - 1f;
        return _bq.Process(n) * _gain;
    }
}

/// <summary>Freeverb (Jezar): 8 tarak + 4 tum-geciren, stereo genislik icin kaydirilmis.</summary>
public sealed class Freeverb
{
    private sealed class Comb
    {
        private readonly float[] _buf;
        private int _i;
        private float _store;
        public float Feedback, Damp;
        public Comb(int n) => _buf = new float[n];
        public float Process(float x)
        {
            float y = _buf[_i];
            _store = y * (1 - Damp) + _store * Damp;
            _buf[_i] = x + _store * Feedback;
            if (++_i >= _buf.Length) _i = 0;
            return y;
        }
    }

    private sealed class AllPass
    {
        private readonly float[] _buf;
        private int _i;
        public AllPass(int n) => _buf = new float[n];
        public float Process(float x)
        {
            float b = _buf[_i];
            float y = -x + b;
            _buf[_i] = x + b * 0.5f;
            if (++_i >= _buf.Length) _i = 0;
            return y;
        }
    }

    private static readonly int[] CombT = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] AllT = { 556, 441, 341, 225 };
    private const int Spread = 23;
    private readonly Comb[] _cl, _cr;
    private readonly AllPass[] _al, _ar;

    public Freeverb(float rate, float room = 0.86f, float damp = 0.3f)
    {
        float k = rate / 44100f;
        _cl = CombT.Select(t => new Comb((int)(t * k)) { Feedback = room, Damp = damp }).ToArray();
        _cr = CombT.Select(t => new Comb((int)((t + Spread) * k)) { Feedback = room, Damp = damp }).ToArray();
        _al = AllT.Select(t => new AllPass((int)(t * k))).ToArray();
        _ar = AllT.Select(t => new AllPass((int)((t + Spread) * k))).ToArray();
    }

    public void Process(float x, out float l, out float r)
    {
        x *= 0.015f;
        l = 0; r = 0;
        for (int i = 0; i < _cl.Length; i++) { l += _cl[i].Process(x); r += _cr[i].Process(x); }
        for (int i = 0; i < _al.Length; i++) { l = _al[i].Process(l); r = _ar[i].Process(r); }
    }
}

/// <summary>Basit ileri beslemeli kompresor (esik -14 dB, oran 3, yumusak diz) + tanh sinirlayici.</summary>
public sealed class Compressor
{
    private float _env;
    private readonly float _att, _rel;
    public float ThresholdDb = -14, Ratio = 3, KneeDb = 12;

    public Compressor(float rate)
    {
        _att = 1 - MathF.Exp(-1 / (0.003f * rate));
        _rel = 1 - MathF.Exp(-1 / (0.25f * rate));
    }

    public float Gain(float peak)
    {
        _env += (peak - _env) * (peak > _env ? _att : _rel);
        float db = 20 * MathF.Log10(MathF.Max(1e-6f, _env));
        float over = db - ThresholdDb;
        float gainDb;
        if (over <= -KneeDb / 2) gainDb = 0;
        else if (over >= KneeDb / 2) gainDb = -over * (1 - 1 / Ratio);
        else
        {
            float x = over + KneeDb / 2;
            gainDb = -(1 - 1 / Ratio) * x * x / (2 * KneeDb);
        }
        return MathF.Pow(10, gainDb / 20);
    }
}
