using System.Diagnostics;
using Starfall.Audio;
using Starfall.Core;

namespace Starfall.Tests;

/// <summary>
/// Ses motorunu cevrimdisi isler: muzik + tum efektler -> WAV. Tepe/RMS, NaN ve islemci
/// maliyeti (gercek zamanin yuzdesi) olculur. dotnet run -- --audio-test cikti.wav
/// </summary>
public static class AudioTest
{
    public static int Run(Dictionary<string, string> args)
    {
        Environment.SetEnvironmentVariable("STARFALL_QUIET", "1");
        var g = new Game(new Dictionary<string, string> { ["data"] = Path.Combine(Path.GetTempPath(), "starfall-audio-" + Environment.ProcessId), ["fresh"] = "1" }, headless: true);
        g.Build();
        g.Audio.StartOffline();
        var mix = g.Audio.Mixer;
        string[] sfx = { "jump", "flap", "land", "step:grass", "step:wood", "step:snow", "step:ice", "shell", "shard", "aurora", "feather", "item", "splash",
            "bounce", "uiMove", "uiConfirm", "uiBack", "error", "buy", "achievement", "fanfare", "lose", "beep", "beepHigh", "cast", "bite",
            "reel", "shutter", "ignite", "firework", "poof", "discover", "climbGrab", "climbStep", "staminaLow", "boatIn", "paddle", "sail",
            "creak", "dig", "chest", "sled", "telescope", "place", "rotate", "pickup", "door", "wardrobe", "crack" };
        float seconds = args.TryGetValue("seconds", out var s) ? float.Parse(s, System.Globalization.CultureInfo.InvariantCulture) : 24;
        int frames = (int)(seconds * Mixer.Rate);
        var all = new float[frames * 2];
        var block = new float[1024 * 2];
        var sw = new Stopwatch();
        int si = 0;
        float nextSfx = 0.5f;
        int maxVoices = 0;
        string[] moods = { "menu", "day", "night", "snow", "aurora", "finale" };
        for (int f = 0; f < frames; f += 1024)
        {
            float t = f / (float)Mixer.Rate;
            g.Audio.Music.SetMood(moods[(int)(t / (seconds / moods.Length)) % moods.Length]);
            g.State = GameState.Playing;
            if (t >= nextSfx && si < sfx.Length)
            {
                g.Audio.Sfx(sfx[si++], 20);
                nextSfx += (seconds - 2) / sfx.Length;
            }
            if (si % 7 == 3) g.Audio.VoiceBlip(1.2f, 'a');
            g.Audio.Update(1024f / Mixer.Rate);
            sw.Start();
            mix.Render(block, 1024);
            sw.Stop();
            maxVoices = Math.Max(maxVoices, mix.ActiveCount);
            int n = Math.Min(1024, frames - f);
            Array.Copy(block, 0, all, f * 2, n * 2);
        }
        float peak = 0;
        double sum = 0;
        int nan = 0;
        foreach (var v in all)
        {
            if (!float.IsFinite(v)) { nan++; continue; }
            peak = MathF.Max(peak, MathF.Abs(v));
            sum += v * v;
        }
        double rms = Math.Sqrt(sum / all.Length);
        double cpu = sw.Elapsed.TotalSeconds / seconds * 100;
        Console.WriteLine($"{seconds} sn islendi: tepe {peak:0.000}, RMS {rms:0.0000}, NaN {nan}, en cok ses {maxVoices}, islemci %{cpu:0.0} (gercek zamana gore)");
        if (args.TryGetValue("audio-test", out var path) && path != "1") WriteWav(path, all);
        bool ok = nan == 0 && peak > 0.05f && peak <= 1.0f && rms > 0.005 && cpu < 40;
        Console.WriteLine(ok ? "ses testi gecti" : "ses testi BASARISIZ");
        return ok ? 0 : 1;
    }

    private static void WriteWav(string path, float[] st)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int bytes = st.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(Mixer.Rate); w.Write(Mixer.Rate * 4); w.Write((short)4); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (var v in st) w.Write((short)(Math.Clamp(float.IsFinite(v) ? v : 0, -1, 1) * 32000));
    }
}
