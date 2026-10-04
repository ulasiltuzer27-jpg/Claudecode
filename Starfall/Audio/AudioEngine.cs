using System.Numerics;
using Starfall.Core;

namespace Starfall.Audio;

/// <summary>Muzik ruh hali denetimi (uretken muzik Audio/Music.cs'de).</summary>
public sealed class MusicControl
{
    public string Mood = "menu";
    public void SetMood(string m) => Mood = m;
}

/// <summary>
/// Ses motoru: yazilim mikseri -> OpenAL akisi. (Gecici: sessiz iskelet; gercek sentez
/// Synth/Sfx/Music/Ambience dosyalarinda.)
/// </summary>
public sealed partial class AudioEngine : IDisposable
{
    private readonly Game _g;
    public readonly MusicControl Music = new();
    public readonly List<string> Log = new();

    public AudioEngine(Game g) => _g = g;

    public void Start() { }
    public void ApplyVolumes() { }
    public void Update(float dt) { }

    public void Sfx(string name, float param = 0)
    {
        if (_g.TestMode && Log.Count < 4000) Log.Add(name);
        SfxImpl(name, param);
    }

    partial void SfxImpl(string name, float param);

    public void VoiceBlip(float voice, char ch) { }

    public void Dispose() { }
}
