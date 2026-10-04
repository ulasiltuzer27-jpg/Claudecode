using System.Numerics;
using Starfall.Core;

namespace Starfall.Render;

/// <summary>
/// Gun dongusu (0..24 saat): gokyuzu renkleri, gunes/ay yonu, isik siddetleri, sis.
/// JS surumundeki Sky.js anahtar kareleriyle ayni.
/// </summary>
public sealed class SkyState
{
    private sealed record Key(float H, Vector3 Top, Vector3 Hor, Vector3 Sun, float Si, Vector3 Sky, Vector3 Gnd, float Hi, Vector3 Fog);

    private static Key K(float h, string top, string hor, string sun, float si, string sky, string gnd, float hi, string fog) =>
        new(h, MathX.Hex(top), MathX.Hex(hor), MathX.Hex(sun), si, MathX.Hex(sky), MathX.Hex(gnd), hi, MathX.Hex(fog));

    private static readonly Key[] Keys =
    {
        K(0.0f, "#0a1230", "#1b2a57", "#9fb4ff", 0.5f, "#5060a8", "#262a40", 0.85f, "#1b2650"),
        K(4.5f, "#101a42", "#2c3466", "#9fb4ff", 0.45f, "#5060a8", "#262a40", 0.8f, "#252c5c"),
        K(5.6f, "#2d3d7a", "#d98c8f", "#ffb58a", 0.9f, "#8c8cc8", "#4a4050", 0.85f, "#b98a94"),
        K(6.8f, "#5f95dc", "#ffcfa0", "#ffd2a0", 1.8f, "#b4d0f4", "#6a6650", 0.9f, "#f2d2b6"),
        K(9.0f, "#3d93f2", "#c4e6ff", "#fff3df", 2.6f, "#b9dcff", "#6b6f4f", 0.9f, "#cfe7fb"),
        K(15.5f, "#3b8eef", "#cfe9ff", "#fff0d8", 2.5f, "#b9dcff", "#6b6f4f", 0.9f, "#d3e9fb"),
        K(18.0f, "#4d72c2", "#ffb07a", "#ffb070", 1.7f, "#c9b3c8", "#5c4a40", 0.75f, "#f4c09c"),
        K(19.3f, "#2e3a7a", "#e8807a", "#ff8a6a", 0.75f, "#8a78a8", "#3d3040", 0.6f, "#b9788a"),
        K(20.6f, "#141f4c", "#4a3f7a", "#9fb4ff", 0.48f, "#5060a8", "#262a40", 0.82f, "#2e2e60"),
        K(24.0f, "#0a1230", "#1b2a57", "#9fb4ff", 0.5f, "#5060a8", "#262a40", 0.85f, "#1b2650"),
    };

    public Vector3 Top, Horizon, SunColor, Fog, HemiSky, HemiGround;
    public float SunIntensity, HemiIntensity, Night;
    public Vector3 SunDir = Vector3.UnitY, MoonDir = -Vector3.UnitY;
    /// <summary>Golge veren isigin yonu (gunduz gunes, gece ay).</summary>
    public Vector3 LightDir = Vector3.UnitY;
    public float Aurora;   // 0..1, Kar Adasi'nda gece
    /// <summary>Final 2 sirasinda ek kuzey isigi parlakligi.</summary>
    public float AuroraBoost;
    public float Time;

    public void Update(float hour, float dt, float snowiness = 0)
    {
        Time += dt;
        int i = 0;
        while (i < Keys.Length - 2 && Keys[i + 1].H <= hour) i++;
        var a = Keys[i];
        var b = Keys[i + 1];
        float t = MathX.Clamp((hour - a.H) / (b.H - a.H), 0, 1);
        Top = Vector3.Lerp(a.Top, b.Top, t);
        Horizon = Vector3.Lerp(a.Hor, b.Hor, t);
        SunColor = Vector3.Lerp(a.Sun, b.Sun, t);
        Fog = Vector3.Lerp(a.Fog, b.Fog, t);
        SunIntensity = MathX.Lerp(a.Si, b.Si, t);
        HemiSky = Vector3.Lerp(a.Sky, b.Sky, t);
        HemiGround = Vector3.Lerp(a.Gnd, b.Gnd, t);
        HemiIntensity = MathX.Lerp(a.Hi, b.Hi, t);

        float ang = (hour - 6) / 12 * MathX.Pi;
        SunDir = Vector3.Normalize(new Vector3(MathF.Cos(ang), MathF.Sin(ang), 0.35f));
        var moon = -SunDir;
        moon.Y = MathF.Abs(moon.Y) * 0.8f + 0.25f;
        MoonDir = Vector3.Normalize(moon);
        Night = MathX.Clamp(1 - (SunDir.Y + 0.12f) / 0.3f, 0, 1);
        var ld = Night > 0.5f ? MoonDir : SunDir;
        ld.Y = MathF.Max(ld.Y, 0.18f);
        LightDir = Vector3.Normalize(ld);

        // Kar Adasi: daha soguk, daha mavi; gece kuzey isiklari
        if (snowiness > 0)
        {
            var cold = new Vector3(0.75f, 0.85f, 1.05f);
            HemiGround = Vector3.Lerp(HemiGround, HemiGround * cold + new Vector3(0.05f, 0.06f, 0.08f), snowiness);
            Fog = Vector3.Lerp(Fog, Fog * cold + new Vector3(0.04f), snowiness * 0.6f);
        }
        Aurora = MathF.Min(1.6f, snowiness * MathX.Smoothstep(0.4f, 0.9f, Night) + AuroraBoost);
    }
}
