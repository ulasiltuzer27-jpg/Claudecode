using Starfall.Core;

namespace Starfall.Audio;

/// <summary>
/// Ortam sesleri: dalga ugultusu (kiyiya yakinlikla), gunduz kus civiltisi, gece cirkir bocegi,
/// yukseklerde ve suzulurken ruzgar; Kar Adasi'nda surekli hafif ruzgar ve buz citirtisi.
/// </summary>
public sealed class Ambience
{
    private readonly AudioEngine _e;
    private NoiseLoop? _waves, _wind;
    private float _t, _birdT = 2, _cricketT = 1, _iceT = 6;
    private readonly Random _r = new(91);

    public Ambience(AudioEngine e) => _e = e;

    public void Start()
    {
        _waves = new NoiseLoop(FilterType.Lowpass, 420, 0.6f, Mixer.Rate);
        _wind = new NoiseLoop(FilterType.Bandpass, 700, 0.9f, Mixer.Rate);
        _e.Mixer.AddLoop(_waves);
        _e.Mixer.AddLoop(_wind);
    }

    private static float CoastFactor(Game g)
    {
        var p = g.Player.Pos;
        int wet = 0;
        const int N = 12;
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * MathF.PI * 2;
            float x = p.X + MathF.Cos(a) * 18, z = p.Z + MathF.Sin(a) * 18;
            if (g.World.Height(x, z) < 0 && !g.World.IsFrozenWater(x, z)) wet++;
        }
        return wet / (float)N;
    }

    private float R() => (float)_r.NextDouble();

    public void Update(float dt, Game g)
    {
        if (_waves == null || _wind == null) return;
        _t += dt;
        float night = g.Env.Sky.Night;
        bool inMenu = g.State == GameState.Menu;
        float coast = inMenu ? 0.6f : CoastFactor(g);
        float swell = 0.6f + 0.4f * MathF.Sin(_t * 0.45f) * MathF.Sin(_t * 0.17f + 1);
        _waves.TargetGain = 0.05f + coast * 0.22f * swell;
        _waves.TargetFreq = 300 + swell * 300;

        var p = g.Player;
        bool snow = !inMenu && g.World.IsSnow(p.Pos.X, p.Pos.Z);
        float alt = MathF.Max(0, p.Pos.Y - 15) / 30;
        float speed = p.Vel.Length();
        float windAmt = MathF.Min(0.3f, alt * 0.08f + (p.Gliding ? 0.06f + speed * 0.012f : 0) + (p.Boating ? 0.03f + speed * 0.006f : 0) + (snow ? 0.045f : 0));
        if (inMenu) windAmt = 0;
        _wind.TargetGain = windAmt;
        _wind.TargetFreq = 500 + speed * 60 + (snow ? 300 * (0.5f + 0.5f * MathF.Sin(_t * 0.3f)) : 0);

        if (inMenu) return;
        // kuslar (Ada 1, gunduz)
        _birdT -= dt;
        if (_birdT <= 0 && night < 0.4f && !snow)
        {
            _birdT = 1.5f + R() * 4;
            float b = 2200 + R() * 1600;
            int n = 2 + _r.Next(4);
            for (int i = 0; i < n; i++)
                _e.Tone(b, Wave.Sine, i * 0.11f, 0.08f, 0.025f * (1 - coast * 0.5f), slide: b * (1.2f + R() * 0.5f), bus: Bus.Amb, reverb: 0.3f);
        }
        // cirkir bocekleri (Ada 1, gece)
        _cricketT -= dt;
        if (_cricketT <= 0 && night > 0.5f && !snow)
        {
            _cricketT = 0.6f + R() * 1.4f;
            float f = 4200 + R() * 600;
            for (int i = 0; i < 3; i++) _e.Tone(f, Wave.Square, i * 0.045f, 0.03f, 0.006f, bus: Bus.Amb, reverb: 0.1f);
        }
        // buz citirtisi / uzaktan can (Kar Adasi)
        _iceT -= dt;
        if (_iceT <= 0 && snow)
        {
            _iceT = 5 + R() * 9;
            if (R() < 0.5f)
            {
                _e.Noise(dur: 0.06f, vol: 0.02f, freq: 2400 + R() * 1200, q: 3, bus: Bus.Amb, reverb: 0.5f);
                _e.Noise(at: 0.08f, dur: 0.04f, vol: 0.012f, freq: 3000, q: 4, bus: Bus.Amb, reverb: 0.5f);
            }
            else
            {
                float f = 1400 + R() * 800;
                _e.Tone(f, Wave.Sine, 0, 1.6f, 0.012f, bus: Bus.Amb, reverb: 0.7f);
                _e.Tone(f * 2.4f, Wave.Sine, 0, 0.8f, 0.005f, bus: Bus.Amb, reverb: 0.7f);
            }
        }
    }
}
