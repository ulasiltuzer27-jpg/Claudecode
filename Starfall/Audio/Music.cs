using Starfall.Core;

namespace Starfall.Audio;

/// <summary>
/// Uretken muzik: akor ilerlemesi + bas + arpej + pentatonik melodi. Her olcu ileriye dogru
/// (lookahead) zamanlanir; gece daha yavas ve yumusak, final daha parlak. Asla ayni sekilde
/// tekrar etmez ama hep ayni "ses ailesinde" kalir. Kar Adasi icin can sesli bir ruh hali.
/// </summary>
public sealed class Music
{
    private sealed record Mood(float Bpm, (int Root, string Kind)[] Prog, int Octave, float Melody, float Arp, float Bright, bool Bells = false);

    private static readonly Dictionary<string, Mood> Moods = new()
    {
        ["menu"] = new(76, new[] { (0, "M"), (9, "m"), (5, "M"), (7, "M") }, 0, 0.45f, 0.6f, 0.8f),
        ["day"] = new(92, new[] { (0, "M"), (7, "M"), (9, "m"), (5, "M") }, 0, 0.6f, 0.85f, 1),
        ["night"] = new(66, new[] { (9, "m"), (5, "M"), (0, "M"), (7, "sus") }, -1, 0.35f, 0.45f, 0.55f),
        ["finale"] = new(104, new[] { (5, "M"), (7, "M"), (4, "m"), (9, "m"), (5, "M"), (7, "M"), (0, "M"), (0, "M") }, 0, 0.75f, 1, 1.2f),
        ["snow"] = new(80, new[] { (2, "m"), (10, "M"), (5, "M"), (0, "M") }, 0, 0.5f, 0.7f, 0.85f, true),
        ["aurora"] = new(60, new[] { (9, "m"), (4, "m"), (5, "M"), (0, "sus") }, -1, 0.4f, 0.5f, 0.6f, true),
        ["finale2"] = new(96, new[] { (9, "m"), (5, "M"), (0, "M"), (7, "M"), (2, "m"), (5, "M"), (7, "sus"), (0, "M") }, 0, 0.75f, 1, 1.1f, true),
        ["house"] = new(84, new[] { (0, "M"), (4, "m"), (5, "M"), (7, "M") }, 0, 0.5f, 0.7f, 0.75f),
    };

    private static readonly int[] Penta = { 0, 2, 4, 7, 9 };
    private readonly AudioEngine _e;
    private string _forced = "menu";
    public string Current = "menu";
    private double _nextBar;
    private int _bar;
    private List<(float Pos, float Len, int Deg)>? _motif;
    private readonly Random _r = new(77);

    public Music(AudioEngine e) => _e = e;

    private static float Midi(float n) => 440 * MathF.Pow(2, (n - 69) / 12f);

    private static int[] Chord(int root, string kind)
    {
        int third = kind == "m" ? 3 : kind == "sus" ? 5 : 4;
        return new[] { root, root + third, root + 7 };
    }

    public void Start() => _nextBar = _e.Mixer.Time + 0.3;

    public void SetMood(string m) => _forced = m;
    public string Forced => _forced;

    public void Update(float dt, Game g)
    {
        string mood = _forced;
        if (mood == "auto") mood = g.Ambience2Mood();
        if (g.State == GameState.Menu && _forced != "finale" && _forced != "finale2") mood = "menu";
        Current = mood;
        double now = _e.Mixer.Time;
        if (_nextBar < now) _nextBar = now + 0.05;
        while (_nextBar < now + 0.35)
        {
            ScheduleBar(_nextBar);
            _nextBar += 60.0 / Moods[Current].Bpm * 4;
            _bar++;
        }
    }

    private float Rnd() => (float)_r.NextDouble();

    private void ScheduleBar(double t0)
    {
        var M = Moods[Current];
        float beat = 60f / M.Bpm;
        var (root, kind) = M.Prog[_bar % M.Prog.Length];
        int b = 48 + 12 * M.Octave; // C3
        var notes = Chord(root, kind);

        // pad
        foreach (var n in notes)
        {
            _e.Tone(Midi(b + 12 + n), Wave.Sine, dur: beat * 4.2f, vol: 0.05f * M.Bright, attack: beat * 0.8f, bus: Bus.Music, reverb: 0.5f, atAbs: t0);
            _e.Tone(Midi(b + 12 + n), Wave.Triangle, dur: beat * 4.0f, vol: 0.018f * M.Bright, attack: beat, bus: Bus.Music, reverb: 0.5f, detune: 7, atAbs: t0);
        }
        // bas
        _e.Tone(Midi(b - 12 + root), Wave.Sine, dur: beat * 1.8f, vol: 0.12f, attack: 0.02f, bus: Bus.Music, reverb: 0.05f, atAbs: t0);
        _e.Tone(Midi(b - 12 + root + 7), Wave.Sine, dur: beat * 1.6f, vol: 0.09f, attack: 0.02f, bus: Bus.Music, reverb: 0.05f, atAbs: t0 + beat * 2);

        // arpej (sekizlik)
        int[] order = { 0, 1, 2, 1, 2, 0, 1, 2 };
        for (int i = 0; i < 8; i++)
        {
            if (Rnd() > M.Arp) continue;
            int n = notes[order[i]] + (i >= 4 ? 12 : 0);
            if (M.Bells)
            {
                float f = Midi(b + 24 + n);
                _e.Tone(f, Wave.Sine, dur: beat * 1.2f, vol: 0.022f * M.Bright, attack: 0.003f, bus: Bus.Music, reverb: 0.45f, atAbs: t0 + i * beat * 0.5f);
                _e.Tone(f * 2.76f, Wave.Sine, dur: beat * 0.4f, vol: 0.006f * M.Bright, attack: 0.002f, bus: Bus.Music, reverb: 0.45f, atAbs: t0 + i * beat * 0.5f);
            }
            else _e.Tone(Midi(b + 24 + n), Wave.Triangle, dur: beat * 0.6f, vol: 0.028f * M.Bright, attack: 0.004f, bus: Bus.Music, reverb: 0.3f, atAbs: t0 + i * beat * 0.5f);
        }

        // melodi: iki olculuk motif, kucuk degisikliklerle tekrar
        if (_bar % 4 == 0 || _motif == null) _motif = MakeMotif(M);
        bool variant = _bar % 2 == 1;
        foreach (var step in _motif)
        {
            if (Rnd() > M.Melody + 0.25f) continue;
            int deg = step.Deg + (variant && Rnd() < 0.3f ? (Rnd() < 0.5f ? -1 : 1) : 0);
            int oct = (int)MathF.Floor(deg / 5f);
            int pn = Penta[((deg % 5) + 5) % 5] + oct * 12;
            float freq = Midi(b + 36 + pn);
            double tt = t0 + step.Pos * beat;
            _e.Tone(freq, Wave.Sine, dur: step.Len * beat * 1.4f, vol: 0.06f * M.Bright, attack: 0.005f, bus: Bus.Music, reverb: 0.4f, atAbs: tt);
            _e.Tone(freq * 4, Wave.Sine, dur: 0.12f, vol: 0.012f * M.Bright, attack: 0.002f, bus: Bus.Music, reverb: 0.2f, atAbs: tt);
        }
    }

    private List<(float, float, int)> MakeMotif(Mood M)
    {
        var steps = new List<(float, float, int)>();
        float pos = 0;
        int deg = 5 + _r.Next(3);
        int[] moves = { -2, -1, -1, 0, 1, 1, 2 };
        while (pos < 4)
        {
            float len = Rnd() < 0.6f ? 0.5f : 1;
            if (Rnd() < M.Melody) steps.Add((pos, len, deg));
            deg = Math.Clamp(deg + moves[_r.Next(moves.Length)], 0, 10);
            pos += len;
        }
        return steps;
    }
}
