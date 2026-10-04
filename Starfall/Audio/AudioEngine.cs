using System.Numerics;
using Starfall.Core;

namespace Starfall.Audio;

/// <summary>
/// Ses motoru: hicbir ses dosyasi yok, hepsi yazilim mikserinde sentezleniyor. Lisans derdi
/// yok, oyun boyutu kucuk, ve her ses oyunun paletine gore ayarlanabiliyor. Otobusler:
/// master -> {music, sfx, ambience}, ortak yanki.
/// </summary>
public sealed partial class AudioEngine : IDisposable
{
    private readonly Game _g;
    public readonly Mixer Mixer = new();
    private OpenAlOutput? _out;
    public readonly Music Music;
    public readonly Ambience Ambience;
    public readonly List<string> Log = new();
    private double _voiceT;
    private readonly Random _r = new(41);
    /// <summary>Pencere yoksa (testler) mikser calismaz; komutlar yine de kaydedilir.</summary>
    public bool Ready;

    public AudioEngine(Game g)
    {
        _g = g;
        Music = new Music(this);
        Ambience = new Ambience(this);
    }

    public string Status => _out?.Status ?? "kapali";

    public void Start()
    {
        if (_g.Arg("nosound")) return;
        _out = new OpenAlOutput(Mixer);
        _out.Start();
        Ready = _out.Ok;
        Game.Log("ses: " + _out.Status);
        if (Ready) StartGraph();
    }

    /// <summary>Cevrimdisi isleme (testler): mikseri dogrudan surer.</summary>
    public void StartOffline()
    {
        Ready = true;
        StartGraph();
    }

    private void StartGraph()
    {
        ApplyVolumes();
        Ambience.Start();
        Music.Start();
    }

    public void ApplyVolumes()
    {
        var s = _g.Settings.V;
        Mixer.Master = s.Master;
        Mixer.MusicGain = s.Music * 0.55f;
        Mixer.SfxGain = s.Sfx;
        Mixer.AmbGain = s.Ambience * 0.8f;
    }

    public double Now => Mixer.Time + 0.02;

    // ------------------------------------------------------------ yapi taslari
    public void Tone(float freq, Wave type = Wave.Sine, float at = 0, float dur = 0.2f, float vol = 0.3f, float attack = 0.005f,
        float slide = 0, Bus bus = Bus.Sfx, float reverb = 0.15f, float detune = 0, double? atAbs = null)
    {
        if (!Ready) return;
        Mixer.Play(new VoiceDesc
        {
            Wave = type, Freq = freq, Slide = slide, Start = atAbs ?? Now + at, Dur = dur, Vol = vol, Attack = attack,
            Bus = bus, Reverb = reverb, Detune = detune,
        });
    }

    public void Noise(float at = 0, float dur = 0.2f, float vol = 0.3f, FilterType filter = FilterType.Bandpass, float freq = 1000,
        float q = 1, float slide = 0, Bus bus = Bus.Sfx, float attack = 0.005f, float reverb = 0.05f)
    {
        if (!Ready) return;
        Mixer.Play(new VoiceDesc
        {
            Noise = true, Filter = filter, Freq = freq, Q = q, Slide = slide, Start = Now + at, Dur = dur, Vol = vol, Attack = attack,
            Bus = bus, Reverb = reverb,
        });
    }

    public void Bell(float freq, float at = 0, float vol = 0.2f, float dur = 1.2f)
    {
        Tone(freq, Wave.Sine, at, dur, vol, reverb: 0.4f);
        Tone(freq * 2.01f, Wave.Sine, at, dur * 0.6f, vol * 0.35f, reverb: 0.4f);
        Tone(freq * 3.02f, Wave.Sine, at, dur * 0.3f, vol * 0.15f, reverb: 0.4f);
    }

    private float R(float a, float b) => a + (float)_r.NextDouble() * (b - a);

    // ------------------------------------------------------------ oyun sesleri
    public void Sfx(string name, float param = 0)
    {
        if (_g.TestMode && Log.Count < 4000) Log.Add(name);
        if (!Ready) return;
        string surface = "";
        if (name.StartsWith("step:")) { surface = name[5..]; name = "step"; }
        switch (name)
        {
            case "jump":
                Tone(R(330, 370), Wave.Triangle, slide: 620, dur: 0.16f, vol: 0.12f);
                break;
            case "flap":
                Noise(dur: 0.25f, vol: 0.18f, freq: 900, slide: 2400, q: 0.8f);
                Bell(880, 0, 0.08f, 0.5f);
                Bell(1320, 0.06f, 0.06f, 0.5f);
                break;
            case "land":
                Noise(dur: 0.12f, vol: MathF.Min(0.3f, 0.08f + param * 0.012f), filter: FilterType.Lowpass, freq: 500);
                break;
            case "step":
            {
                float f = surface switch { "wood" => 1800, "sand" => 700, "mushroom" => 400, "snow" => 2600, "ice" => 3400, _ => 1200 };
                bool wood = surface == "wood";
                if (surface == "snow")
                {
                    Noise(dur: 0.09f, vol: 0.05f, freq: R(f * 0.85f, f * 1.15f), q: 0.7f, reverb: 0);
                    Noise(at: 0.02f, dur: 0.05f, vol: 0.03f, filter: FilterType.Lowpass, freq: 700, reverb: 0);
                }
                else if (surface == "ice") Noise(dur: 0.04f, vol: 0.05f, freq: R(f * 0.9f, f * 1.1f), q: 6, reverb: 0.05f);
                else Noise(dur: wood ? 0.05f : 0.07f, vol: wood ? 0.07f : 0.045f, freq: R(f * 0.85f, f * 1.15f), q: wood ? 4 : 1.2f, reverb: 0);
                break;
            }
            case "shell":
                Bell(R(1170, 1190), 0, 0.09f, 0.5f);
                Bell(1567, 0.07f, 0.08f, 0.6f);
                break;
            case "shard":
            {
                float[] notes = { 784, 988, 1175, 1568 };
                for (int i = 0; i < notes.Length; i++) Bell(notes[i], i * 0.07f, 0.14f, 1.4f);
                Noise(dur: 0.6f, vol: 0.05f, freq: 6000, q: 0.5f, reverb: 0.4f);
                break;
            }
            case "aurora":
            {
                float[] notes = { 659, 880, 1109, 1319, 1760 };
                for (int i = 0; i < notes.Length; i++) Bell(notes[i], i * 0.06f, 0.11f, 1.8f);
                Tone(220, Wave.Sine, 0, 2.2f, 0.06f, 0.4f, 330, reverb: 0.6f);
                Noise(dur: 1.2f, vol: 0.04f, freq: 7000, q: 0.4f, attack: 0.2f, reverb: 0.6f);
                break;
            }
            case "feather":
            {
                float[] n = { 523, 659, 784, 1047, 1319 };
                for (int i = 0; i < n.Length; i++) Tone(n[i], Wave.Triangle, i * 0.05f, 0.9f, 0.08f, reverb: 0.5f);
                break;
            }
            case "item":
                Bell(660, 0, 0.12f, 0.6f);
                Bell(990, 0.09f, 0.12f, 0.8f);
                break;
            case "splash":
                Noise(dur: 0.5f, vol: 0.12f + param * 0.15f, freq: 1400, slide: 400, q: 0.7f);
                Noise(at: 0.05f, dur: 0.3f, vol: 0.06f, freq: 3000, q: 2);
                break;
            case "bounce":
                Tone(160, Wave.Sine, slide: 520, dur: 0.35f, vol: 0.25f);
                Tone(240, Wave.Triangle, 0.02f, 0.3f, 0.08f, slide: 720);
                break;
            case "uiMove":
                Tone(880, Wave.Sine, dur: 0.06f, vol: 0.05f, reverb: 0);
                break;
            case "uiConfirm":
                Tone(660, Wave.Triangle, dur: 0.1f, vol: 0.08f, reverb: 0.1f);
                Tone(990, Wave.Triangle, 0.07f, 0.14f, 0.08f, reverb: 0.1f);
                break;
            case "uiBack":
                Tone(660, Wave.Triangle, slide: 440, dur: 0.14f, vol: 0.07f, reverb: 0);
                break;
            case "error":
                Tone(220, Wave.Square, dur: 0.12f, vol: 0.04f, reverb: 0);
                Tone(196, Wave.Square, 0.12f, 0.16f, 0.04f, reverb: 0);
                break;
            case "buy":
            {
                float[] n = { 1047, 1319, 1568 };
                for (int i = 0; i < n.Length; i++) Bell(n[i], i * 0.06f, 0.1f, 0.6f);
                break;
            }
            case "achievement":
            {
                float[] n = { 523, 659, 784, 1047 };
                for (int i = 0; i < n.Length; i++) Tone(n[i], Wave.Triangle, i * 0.09f, 0.5f, 0.1f, reverb: 0.35f);
                Bell(2093, 0.38f, 0.08f, 1.2f);
                break;
            }
            case "fanfare":
                foreach (var (n, at) in new[] { (523f, 0f), (659f, 0.1f), (784f, 0.2f), (1047f, 0.32f), (784f, 0.46f), (1047f, 0.56f) })
                    Tone(n, Wave.Square, at, 0.18f, 0.05f, reverb: 0.3f);
                break;
            case "lose":
            {
                float[] n = { 523, 494, 466 };
                for (int i = 0; i < n.Length; i++) Tone(n[i], Wave.Triangle, i * 0.16f, 0.2f, 0.07f);
                break;
            }
            case "beep": Tone(660, Wave.Square, dur: 0.15f, vol: 0.05f, reverb: 0); break;
            case "beepHigh": Tone(1320, Wave.Square, dur: 0.3f, vol: 0.05f, reverb: 0); break;
            case "cast":
                Noise(dur: 0.3f, vol: 0.08f, freq: 2000, slide: 600, q: 1);
                Tone(300, Wave.Sine, 0.35f, 0.1f, 0.06f, slide: 200);
                break;
            case "bite":
                Tone(500, Wave.Sine, slide: 180, dur: 0.15f, vol: 0.18f);
                Noise(at: 0.02f, dur: 0.2f, vol: 0.08f, freq: 1200, q: 2);
                break;
            case "reel": Tone(R(1800, 2200), Wave.Square, dur: 0.02f, vol: 0.015f, reverb: 0); break;
            case "shutter":
                Noise(dur: 0.05f, vol: 0.15f, freq: 3000, q: 1, reverb: 0);
                Noise(at: 0.08f, dur: 0.06f, vol: 0.12f, freq: 2200, q: 1, reverb: 0);
                break;
            case "ignite":
            {
                float[] n = { 262, 330, 392, 523, 659, 784 };
                for (int i = 0; i < n.Length; i++) Tone(n[i], Wave.Sine, i * 0.12f, 3.5f, 0.07f, 0.3f, reverb: 0.6f);
                Noise(dur: 2.5f, vol: 0.06f, freq: 500, slide: 4000, q: 0.5f, attack: 0.5f, reverb: 0.5f);
                break;
            }
            case "firework":
            {
                float d = param > 0 ? param : 40;
                float v = MathF.Min(0.25f, 6 / d);
                Noise(dur: 0.6f, vol: v, filter: FilterType.Lowpass, freq: 900, slide: 200);
                for (int i = 0; i < 6; i++) Noise(at: 0.15f + R(0, 0.6f), dur: 0.05f, vol: v * 0.4f, freq: 4000, q: 2);
                break;
            }
            case "poof":
                Noise(dur: 0.35f, vol: 0.12f, freq: 1500, slide: 300, q: 0.6f);
                Bell(1568, 0.05f, 0.06f, 0.6f);
                break;
            case "discover":
            {
                float[] n = { 392, 523, 659, 784 };
                for (int i = 0; i < n.Length; i++) Tone(n[i], Wave.Triangle, i * 0.12f, 0.9f, 0.06f, reverb: 0.5f);
                break;
            }
            // --- Asama B sesleri
            case "climbGrab":
                Noise(dur: 0.08f, vol: 0.09f, freq: 900, q: 1.5f, reverb: 0);
                Tone(260, Wave.Triangle, dur: 0.08f, vol: 0.05f, reverb: 0);
                break;
            case "climbStep":
                Noise(dur: 0.05f, vol: 0.05f, freq: R(700, 1000), q: 2, reverb: 0);
                break;
            case "staminaLow":
                Tone(880, Wave.Sine, dur: 0.08f, vol: 0.04f, reverb: 0);
                Tone(660, Wave.Sine, 0.1f, 0.08f, 0.04f, reverb: 0);
                break;
            case "boatIn":
                Noise(dur: 0.4f, vol: 0.1f, freq: 800, slide: 300, q: 0.8f);
                Tone(140, Wave.Triangle, dur: 0.25f, vol: 0.08f, slide: 110);
                break;
            case "paddle":
                Noise(dur: 0.35f, vol: 0.06f + param * 0.04f, freq: 1100, slide: 500, q: 0.9f);
                break;
            case "sail":
                Noise(dur: 0.6f, vol: 0.08f, freq: 600, slide: 1600, q: 0.6f, attack: 0.1f);
                break;
            case "creak":
                Tone(R(180, 220), Wave.Saw, dur: 0.25f, vol: 0.02f, slide: R(150, 260), reverb: 0.1f);
                break;
            case "dig":
                Noise(dur: 0.18f, vol: 0.12f, filter: FilterType.Lowpass, freq: 650, reverb: 0);
                Noise(at: 0.05f, dur: 0.1f, vol: 0.05f, freq: 2400, q: 1.5f, reverb: 0);
                break;
            case "chest":
                Tone(200, Wave.Saw, dur: 0.3f, vol: 0.03f, slide: 340, reverb: 0.1f);
                foreach (var (n, at) in new[] { (523f, 0.3f), (659f, 0.4f), (784f, 0.5f), (1047f, 0.62f), (1319f, 0.74f) })
                    Bell(n, at, 0.1f, 1.2f);
                break;
            case "sled":
                Noise(dur: 0.3f, vol: 0.05f + param * 0.05f, freq: 2200, q: 0.8f, reverb: 0.05f);
                break;
            case "telescope":
                Tone(330, Wave.Triangle, dur: 0.6f, vol: 0.05f, slide: 660, reverb: 0.3f);
                Noise(dur: 0.5f, vol: 0.04f, freq: 1800, q: 3, reverb: 0.2f);
                break;
            case "place":
                Noise(dur: 0.08f, vol: 0.1f, filter: FilterType.Lowpass, freq: 900, reverb: 0.05f);
                Tone(440, Wave.Triangle, 0.02f, 0.08f, 0.05f, reverb: 0.05f);
                break;
            case "rotate": Tone(700, Wave.Triangle, dur: 0.07f, vol: 0.05f, slide: 900, reverb: 0); break;
            case "pickup": Tone(900, Wave.Triangle, dur: 0.1f, vol: 0.05f, slide: 600, reverb: 0); break;
            case "door":
                Tone(160, Wave.Saw, dur: 0.35f, vol: 0.02f, slide: 240, reverb: 0.2f);
                Noise(at: 0.3f, dur: 0.1f, vol: 0.08f, filter: FilterType.Lowpass, freq: 500, reverb: 0.1f);
                break;
            case "wardrobe":
                Noise(dur: 0.2f, vol: 0.06f, freq: 3000, slide: 1500, q: 0.7f);
                Bell(1175, 0.1f, 0.06f, 0.5f);
                break;
            case "crack":
                Noise(dur: 0.12f, vol: 0.12f, freq: 2600, q: 1.2f, reverb: 0.2f);
                Tone(90, Wave.Sine, dur: 0.3f, vol: 0.1f, slide: 50, reverb: 0.2f);
                break;
            default:
                break;
        }
    }

    /// <summary>Hayvan Gecidi tarzi konusma "mirildanmasi": her harfte kisa bir ses.</summary>
    public void VoiceBlip(float pitch, char ch)
    {
        if (!Ready) return;
        double now = Mixer.Time;
        if (now - _voiceT < 0.055) return;
        _voiceT = now;
        int code = char.ToLowerInvariant(ch);
        float b = 260 * pitch * (1 + ((code * 7) % 12) / 24f);
        Tone(b, Wave.Triangle, dur: 0.07f, vol: 0.06f, reverb: 0.05f);
        Tone(b * 1.5f, Wave.Sine, dur: 0.05f, vol: 0.025f, reverb: 0);
    }

    public void Update(float dt)
    {
        if (!Ready) return;
        Music.Update(dt, _g);
        Ambience.Update(dt, _g);
    }

    public void Dispose() => _out?.Dispose();
}
