using System.Collections.Concurrent;
using Silk.NET.OpenAL;

namespace Starfall.Audio;

/// <summary>
/// Yazilim mikseri: sesler (osilator/gurultu) + surekli dongular -> otobusler (muzik, efekt,
/// ortam) -> master + ortak yanki (Freeverb) -> kompresor. Ses ipliginde calisir; oyun
/// ipliginden komutlar kilitsiz kuyrukla gelir.
/// </summary>
public sealed class Mixer
{
    public const int Rate = 44100;
    private readonly ConcurrentQueue<VoiceDesc> _queue = new();
    private readonly List<Voice> _pending = new(), _active = new();
    /// <summary>Surekli dongular (kopyala-degistir: ses ipligi kilitsiz okur).</summary>
    public volatile NoiseLoop[] Loops = Array.Empty<NoiseLoop>();

    public void AddLoop(NoiseLoop l) => Loops = Loops.Append(l).ToArray();
    private readonly Freeverb _reverb = new(Rate);
    private readonly Compressor _comp = new(Rate);
    private long _samples;
    private uint _seed = 12345;
    public volatile float Master = 0.8f, MusicGain = 0.33f, SfxGain = 0.8f, AmbGain = 0.56f;
    private float _m, _mu, _sf, _am;
    public volatile int ActiveCount;
    public float ReverbLevel = 0.35f;

    /// <summary>Mikser saati (sn): su ana kadar uretilen ornek sayisi.</summary>
    public double Time => Interlocked.Read(ref _samples) / (double)Rate;

    public void Play(in VoiceDesc d)
    {
        if (_queue.Count < 2048) _queue.Enqueue(d);
    }

    /// <summary>Stereo serpistirilmis (LRLR) float blok uret.</summary>
    public void Render(float[] buf, int frames)
    {
        while (_queue.TryDequeue(out var d))
        {
            _seed = _seed * 1664525 + 1013904223;
            var v = new Voice(d, Rate, _seed);
            _pending.Add(v);
        }
        double t0 = Time;
        for (int f = 0; f < frames; f++)
        {
            double now = t0 + f / (double)Rate;
            // zamani gelen sesleri baslat
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].D.Start <= now)
                {
                    _active.Add(_pending[i]);
                    _pending.RemoveAt(i);
                }
            }
            _m += (Master - _m) * 0.002f;
            _mu += (MusicGain - _mu) * 0.002f;
            _sf += (SfxGain - _sf) * 0.002f;
            _am += (AmbGain - _am) * 0.002f;
            float music = 0, sfx = 0, amb = 0, send = 0;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var v = _active[i];
                float s = v.Next();
                if (v.Done) { _active.RemoveAt(i); continue; }
                switch (v.D.Bus)
                {
                    case Bus.Music: music += s; send += s * v.D.Reverb * _mu; break;
                    case Bus.Amb: amb += s; send += s * v.D.Reverb * _am; break;
                    default: sfx += s; send += s * v.D.Reverb * _sf; break;
                }
            }
            foreach (var lp in Loops)
            {
                float s = lp.Next();
                if (lp.Bus == Bus.Amb) amb += s; else sfx += s;
            }
            _reverb.Process(send, out float rl, out float rr);
            float dry = music * _mu + sfx * _sf + amb * _am;
            float l = (dry + rl * ReverbLevel * 2.2f) * _m;
            float r = (dry + rr * ReverbLevel * 2.2f) * _m;
            float g = _comp.Gain(MathF.Max(MathF.Abs(l), MathF.Abs(r)));
            buf[f * 2] = MathF.Tanh(l * g * 1.1f);
            buf[f * 2 + 1] = MathF.Tanh(r * g * 1.1f);
        }
        Interlocked.Add(ref _samples, frames);
        ActiveCount = _active.Count + _pending.Count;
    }
}

/// <summary>OpenAL akis cikisi: ses ipliginde mikseri doldurup tamponlari kuyruga ekler.</summary>
public sealed unsafe class OpenAlOutput : IDisposable
{
    private const int Frames = 1024, BufferCount = 4;
    private readonly Mixer _mixer;
    private ALContext? _alc;
    private AL? _al;
    private Device* _device;
    private Context* _context;
    private uint _source;
    private uint[] _buffers = Array.Empty<uint>();
    private Thread? _thread;
    private volatile bool _run;
    public bool Ok { get; private set; }
    public string Status = "kapali";

    public OpenAlOutput(Mixer mixer) => _mixer = mixer;

    public void Start()
    {
        try
        {
            _alc = ALContext.GetApi(true);
            _al = AL.GetApi(true);
            _device = _alc.OpenDevice("");
            if (_device == null) { Status = "ses aygiti yok"; return; }
            _context = _alc.CreateContext(_device, null);
            _alc.MakeContextCurrent(_context);
            _source = _al.GenSource();
            _buffers = new uint[BufferCount];
            fixed (uint* b = _buffers) _al.GenBuffers(BufferCount, b);
            var f = new float[Frames * 2];
            var pcm = new short[Frames * 2];
            foreach (var b in _buffers)
            {
                Fill(f, pcm);
                fixed (short* p = pcm) _al.BufferData(b, BufferFormat.Stereo16, p, pcm.Length * 2, Mixer.Rate);
            }
            fixed (uint* b = _buffers) _al.SourceQueueBuffers(_source, BufferCount, b);
            _al.SourcePlay(_source);
            Ok = true;
            Status = "ok";
            _run = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "starfall-audio", Priority = ThreadPriority.AboveNormal };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Ok = false;
            Status = "OpenAL acilamadi: " + ex.GetType().Name;
        }
    }

    private void Fill(float[] f, short[] pcm)
    {
        _mixer.Render(f, Frames);
        for (int i = 0; i < f.Length; i++) pcm[i] = (short)(Math.Clamp(f[i], -1, 1) * 32000);
    }

    private void Loop()
    {
        var f = new float[Frames * 2];
        var pcm = new short[Frames * 2];
        var al = _al!;
        _alc!.MakeContextCurrent(_context);
        while (_run)
        {
            al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out int processed);
            if (processed <= 0)
            {
                Thread.Sleep(4);
                continue;
            }
            while (processed-- > 0)
            {
                uint b = 0;
                al.SourceUnqueueBuffers(_source, 1, &b);
                Fill(f, pcm);
                fixed (short* p = pcm) al.BufferData(b, BufferFormat.Stereo16, p, pcm.Length * 2, Mixer.Rate);
                al.SourceQueueBuffers(_source, 1, &b);
            }
            al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
            if (state != (int)SourceState.Playing) al.SourcePlay(_source);
        }
    }

    public void Dispose()
    {
        _run = false;
        _thread?.Join(500);
        if (!Ok) return;
        try
        {
            _al!.SourceStop(_source);
            _al.DeleteSource(_source);
            fixed (uint* b = _buffers) _al.DeleteBuffers(BufferCount, b);
            _alc!.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _alc.CloseDevice(_device);
        }
        catch { /* yok say */ }
        Ok = false;
    }
}
