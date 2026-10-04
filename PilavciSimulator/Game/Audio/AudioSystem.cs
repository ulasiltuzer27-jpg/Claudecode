using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Core;

namespace PilavciSimulator.Audio;

public enum SoundBus
{
    Sfx,
    Ambient,
    Music,
    Ui,
}

/// <summary>
/// Ses: prosedurel efektler (bkz. SfxSynth), 3B konuma gore ses duzeyi ve
/// sol/sag dengesi, dongulu ortam sesleri, istege bagli muzik
/// (<c>Assets/Audio/Music/*.ogg</c>).
///
/// Ses aygiti acilamazsa (sunucu, ses karti yok) tum cagrilar sessizce
/// hicbir sey yapmaz; oyun calismaya devam eder.
/// </summary>
public sealed class AudioSystem : IDisposable
{
    private sealed class Voice
    {
        public Sound[] Aliases = [];
        public int Next;
    }

    private sealed class Loop
    {
        public required Music Music;
        public float Target;
        public float Current;
        public SoundBus Bus;
    }

    private readonly Dictionary<string, Voice> _sounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Loop> _loops = new(StringComparer.Ordinal);
    private readonly List<Music> _music = new();
    private int _musicIndex = -1;
    private float _musicGap;
    private float _master = 1f, _sfx = 1f, _ambient = 1f, _musicVol = 0.5f;

    public bool Enabled { get; }
    public Vector3 ListenerPosition { get; set; }
    public Vector3 ListenerForward { get; set; } = -Vector3.UnitZ;
    public bool MusicEnabled { get; set; } = true;

    public AudioSystem(bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            Raylib.InitAudioDevice();
            Enabled = Raylib.IsAudioDeviceReady();
        }
        catch (Exception ex)
        {
            Log.Warn($"ses aygiti acilamadi: {ex.Message}");
            Enabled = false;
        }

        if (!Enabled)
        {
            Log.Warn("ses kapali (aygit yok)");
            return;
        }

        foreach (var (id, wav) in SfxSynth.BuildAll())
        {
            Register(id, wav, polyphony: id.StartsWith("step", StringComparison.Ordinal) ? 4 : 3);
        }

        foreach (var (id, wav) in SfxSynth.BuildLoops())
        {
            var music = Raylib.LoadMusicStreamFromMemory(".wav", wav);
            music.Looping = true;
            _loops[id] = new Loop { Music = music, Bus = id.StartsWith("amb", StringComparison.Ordinal) ? SoundBus.Ambient : SoundBus.Sfx };
        }

        LoadMusic();
    }

    private void Register(string id, byte[] wav, int polyphony)
    {
        var wave = Raylib.LoadWaveFromMemory(".wav", wav);
        var baseSound = Raylib.LoadSoundFromWave(wave);
        Raylib.UnloadWave(wave);
        var v = new Voice { Aliases = new Sound[polyphony] };
        v.Aliases[0] = baseSound;
        for (var i = 1; i < polyphony; i++)
        {
            v.Aliases[i] = Raylib.LoadSoundAlias(baseSound);
        }

        _sounds[id] = v;
    }

    private void LoadMusic()
    {
        var dir = Path.Combine(Paths.Assets, "Audio", "Music");
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var f in Directory.GetFiles(dir).Where(f => f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).Order())
        {
            var m = Raylib.LoadMusicStream(f);
            m.Looping = false;
            _music.Add(m);
        }

        Log.Info($"{_music.Count} muzik parcasi yuklendi");
    }

    public void ApplyVolumes(GameSettings s)
    {
        _master = s.MasterVolume;
        _sfx = s.SfxVolume;
        _ambient = s.AmbientVolume;
        _musicVol = s.MusicVolume;
        if (Enabled)
        {
            Raylib.SetMasterVolume(_master);
        }
    }

    private float BusVolume(SoundBus bus) => bus switch
    {
        SoundBus.Ambient => _ambient,
        SoundBus.Music => _musicVol,
        _ => _sfx,
    };

    /// <summary>2B (arayuz) sesi.</summary>
    public void Play(string id, float volume = 1f, float pitch = 1f, SoundBus bus = SoundBus.Ui)
    {
        if (!Enabled || !_sounds.TryGetValue(id, out var v))
        {
            return;
        }

        var s = v.Aliases[v.Next];
        v.Next = (v.Next + 1) % v.Aliases.Length;
        Raylib.SetSoundVolume(s, Math.Clamp(volume * BusVolume(bus), 0f, 1f));
        Raylib.SetSoundPitch(s, pitch);
        Raylib.SetSoundPan(s, 0.5f);
        Raylib.PlaySound(s);
    }

    /// <summary>3B ses: dinleyiciye uzakliga gore kisilir, yone gore sola/saga kayar.</summary>
    public void PlayAt(string id, Vector3 position, float volume = 1f, float pitch = 1f, float range = 18f)
    {
        if (!Enabled || !_sounds.TryGetValue(id, out var v))
        {
            return;
        }

        var (gain, pan) = Spatial(position, range);
        if (gain <= 0.01f)
        {
            return;
        }

        var s = v.Aliases[v.Next];
        v.Next = (v.Next + 1) % v.Aliases.Length;
        Raylib.SetSoundVolume(s, Math.Clamp(volume * gain * _sfx, 0f, 1f));
        Raylib.SetSoundPitch(s, pitch);
        Raylib.SetSoundPan(s, pan);
        Raylib.PlaySound(s);
    }

    private (float Gain, float Pan) Spatial(Vector3 position, float range)
    {
        var to = position - ListenerPosition;
        var d = to.Length();
        var gain = Math.Clamp(1f - d / range, 0f, 1f);
        gain *= gain;
        var fwd = new Vector3(ListenerForward.X, 0, ListenerForward.Z);
        var right = Vector3.Normalize(Vector3.Cross(fwd.LengthSquared() > 0 ? Vector3.Normalize(fwd) : -Vector3.UnitZ, Vector3.UnitY));
        var side = d > 0.01f ? Vector3.Dot(Vector3.Normalize(to), right) : 0f;
        // raylib: pan 0.5 orta, 1.0 sol, 0.0 sag
        var pan = 0.5f - side * 0.35f;
        return (gain, pan);
    }

    /// <summary>
    /// Dongulu ses hedef duzeyi (0 = sessiz). Her kare cagrilir; duzey
    /// yumusakca hedefe kayar, sifira inince akis durur.
    /// </summary>
    public void SetLoop(string id, float level)
    {
        if (_loops.TryGetValue(id, out var l))
        {
            l.Target = Math.Clamp(level, 0f, 1f);
        }
    }

    public void SetLoopAt(string id, Vector3 position, float level, float range = 14f)
    {
        var (gain, _) = Spatial(position, range);
        if (_loops.TryGetValue(id, out var l))
        {
            l.Target = MathF.Max(l.Target, Math.Clamp(level * gain, 0f, 1f));
        }
    }

    /// <summary>Kare basinda dongu hedeflerini sifirlar; SetLoopAt'lar yeniden doldurur.</summary>
    public void ResetLoopTargets()
    {
        foreach (var l in _loops.Values)
        {
            l.Target = 0;
        }
    }

    public void Update(float dt)
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var l in _loops.Values)
        {
            l.Current += (l.Target - l.Current) * MathF.Min(1f, dt * 3f);
            var playing = Raylib.IsMusicStreamPlaying(l.Music);
            if (l.Current > 0.005f)
            {
                if (!playing)
                {
                    Raylib.PlayMusicStream(l.Music);
                }

                Raylib.SetMusicVolume(l.Music, l.Current * BusVolume(l.Bus));
                Raylib.UpdateMusicStream(l.Music);
            }
            else if (playing)
            {
                Raylib.StopMusicStream(l.Music);
            }
        }

        UpdateMusic(dt);
    }

    private void UpdateMusic(float dt)
    {
        if (_music.Count == 0 || !MusicEnabled)
        {
            return;
        }

        if (_musicIndex >= 0)
        {
            var m = _music[_musicIndex];
            Raylib.SetMusicVolume(m, _musicVol * 0.6f);
            Raylib.UpdateMusicStream(m);
            if (Raylib.IsMusicStreamPlaying(m))
            {
                return;
            }
        }

        _musicGap -= dt;
        if (_musicGap > 0)
        {
            return;
        }

        _musicIndex = (_musicIndex + 1) % _music.Count;
        Raylib.PlayMusicStream(_music[_musicIndex]);
        _musicGap = 20f;
    }

    public void Dispose()
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var v in _sounds.Values)
        {
            for (var i = v.Aliases.Length - 1; i >= 1; i--)
            {
                Raylib.UnloadSoundAlias(v.Aliases[i]);
            }

            Raylib.UnloadSound(v.Aliases[0]);
        }

        foreach (var l in _loops.Values)
        {
            Raylib.UnloadMusicStream(l.Music);
        }

        foreach (var m in _music)
        {
            Raylib.UnloadMusicStream(m);
        }

        Raylib.CloseAudioDevice();
    }
}
