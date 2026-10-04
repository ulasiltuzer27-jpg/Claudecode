using System.Numerics;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim;

namespace PilavciSimulator.Client;

/// <summary>
/// Saat ve havadan isik/gokyuzu. Gunes 05:30'da dogar, 20:30'da batar
/// (Istanbul yazi kabaca). Renkler birkac anahtar saat arasinda karisiyor.
/// </summary>
public static class DayNight
{
    private readonly record struct Key(float Hour, Vector3 Sun, Vector3 SkyAmb, Vector3 GroundAmb, Vector3 Zenith, Vector3 Horizon, float Exposure, float Glow);

    private static readonly Key[] Keys =
    [
        new(0, new(0.05f, 0.07f, 0.14f), new(0.04f, 0.05f, 0.1f), new(0.02f, 0.02f, 0.04f), new(0.01f, 0.02f, 0.06f), new(0.05f, 0.07f, 0.14f), 1.25f, 1),
        new(4.8f, new(0.05f, 0.07f, 0.14f), new(0.05f, 0.06f, 0.12f), new(0.02f, 0.02f, 0.04f), new(0.03f, 0.05f, 0.12f), new(0.12f, 0.12f, 0.2f), 1.2f, 1),
        new(6.0f, new(1.6f, 0.75f, 0.35f), new(0.25f, 0.24f, 0.3f), new(0.1f, 0.08f, 0.07f), new(0.32f, 0.45f, 0.7f), new(0.96f, 0.68f, 0.45f), 1.05f, 0.35f),
        new(8.0f, new(2.5f, 2.15f, 1.75f), new(0.32f, 0.36f, 0.45f), new(0.15f, 0.13f, 0.11f), new(0.3f, 0.52f, 0.85f), new(0.78f, 0.84f, 0.9f), 1f, 0),
        new(13.0f, new(2.8f, 2.65f, 2.4f), new(0.34f, 0.39f, 0.48f), new(0.17f, 0.15f, 0.13f), new(0.26f, 0.5f, 0.88f), new(0.72f, 0.82f, 0.93f), 1f, 0),
        new(17.5f, new(2.6f, 2.1f, 1.5f), new(0.32f, 0.34f, 0.42f), new(0.16f, 0.13f, 0.1f), new(0.3f, 0.48f, 0.82f), new(0.86f, 0.8f, 0.72f), 1f, 0),
        new(19.6f, new(1.9f, 0.85f, 0.4f), new(0.26f, 0.22f, 0.28f), new(0.12f, 0.08f, 0.07f), new(0.28f, 0.33f, 0.6f), new(0.98f, 0.55f, 0.36f), 1.05f, 0.4f),
        new(20.6f, new(0.35f, 0.25f, 0.35f), new(0.12f, 0.11f, 0.2f), new(0.05f, 0.04f, 0.06f), new(0.09f, 0.1f, 0.26f), new(0.45f, 0.3f, 0.4f), 1.15f, 0.9f),
        new(21.5f, new(0.06f, 0.08f, 0.16f), new(0.05f, 0.06f, 0.12f), new(0.02f, 0.02f, 0.04f), new(0.02f, 0.03f, 0.09f), new(0.07f, 0.09f, 0.17f), 1.25f, 1),
        new(24, new(0.05f, 0.07f, 0.14f), new(0.04f, 0.05f, 0.1f), new(0.02f, 0.02f, 0.04f), new(0.01f, 0.02f, 0.06f), new(0.05f, 0.07f, 0.14f), 1.25f, 1),
    ];

    /// <summary>Gunes yonu: dogu (+X) ufkundan yukselip bati (-X) ufkuna; hafif guneyden (+Z) gecer.</summary>
    public static Vector3 SunDirection(float hour)
    {
        var t = (hour - 5.5f) / 15f;
        var angle = t * MathF.PI;
        var up = MathF.Sin(angle);
        var x = MathF.Cos(angle);
        return Vector3.Normalize(new Vector3(x, MathF.Max(up, -0.3f) * 0.95f, 0.45f));
    }

    public static SceneEnvironment Compute(GameWorld w, float realTime)
    {
        var hour = w.Clock.Minute / 60f;
        return Compute(hour, w.Weather.Cloud, w.Weather.Rain, realTime);
    }

    public static SceneEnvironment Compute(float hour, float cloud, float rain, float realTime)
    {
        hour = ((hour % 24) + 24) % 24;
        var i = 0;
        while (i < Keys.Length - 2 && Keys[i + 1].Hour <= hour)
        {
            i++;
        }

        var a = Keys[i];
        var b = Keys[i + 1];
        var t = Math.Clamp((hour - a.Hour) / MathF.Max(0.001f, b.Hour - a.Hour), 0f, 1f);
        t = t * t * (3 - 2 * t);

        var env = SceneEnvironment.Default;
        env.Time = realTime;
        var sunDir = SunDirection(hour);
        var night = sunDir.Y < 0.02f;
        env.SunDirection = night ? Vector3.Normalize(new Vector3(-0.3f, 0.55f, -0.4f)) : sunDir;
        env.MoonDirection = Vector3.Normalize(new Vector3(-0.3f, 0.55f, -0.4f));
        env.SunColor = Vector3.Lerp(a.Sun, b.Sun, t);
        env.SkyAmbient = Vector3.Lerp(a.SkyAmb, b.SkyAmb, t);
        env.GroundAmbient = Vector3.Lerp(a.GroundAmb, b.GroundAmb, t);
        env.ZenithColor = Vector3.Lerp(a.Zenith, b.Zenith, t);
        env.HorizonColor = Vector3.Lerp(a.Horizon, b.Horizon, t);
        env.Exposure = a.Exposure + (b.Exposure - a.Exposure) * t;
        env.NightGlow = a.Glow + (b.Glow - a.Glow) * t;
        env.StarAmount = Math.Clamp(env.NightGlow * 1.2f - 0.2f, 0f, 1f) * (1 - cloud * 0.8f);
        env.SunDiscColor = night ? Vector3.Zero : Vector3.Lerp(new Vector3(1f, 0.55f, 0.3f), new Vector3(1f, 0.95f, 0.85f), Math.Clamp(sunDir.Y * 3f, 0f, 1f));
        env.GroundColor = env.HorizonColor * 0.45f;

        // Bulut ve yagmur: gunesi bastir, gokyuzunu grilestir
        env.CloudAmount = Math.Clamp(cloud, 0f, 1f);
        var overcast = Math.Clamp(cloud * 0.7f + rain * 0.6f, 0f, 1f);
        env.SunColor *= 1f - overcast * 0.65f;
        env.SkyAmbient *= 1f + overcast * 0.25f;
        var gray = new Vector3(Vector3.Dot(env.HorizonColor, new Vector3(0.33f)));
        env.HorizonColor = Vector3.Lerp(env.HorizonColor, gray * 0.95f, overcast * 0.7f);
        env.ZenithColor = Vector3.Lerp(env.ZenithColor, gray * 0.8f, overcast * 0.7f);
        env.CloudColor = Vector3.Lerp(new Vector3(0.97f), new Vector3(0.62f, 0.64f, 0.68f), overcast) * MathF.Max(0.15f, 1f - env.NightGlow * 0.85f) +
                         Vector3.Lerp(Vector3.Zero, new Vector3(0.35f, 0.18f, 0.1f), env.NightGlow * (1 - env.NightGlow) * 3f);
        env.FogDensity = 0.0042f + rain * 0.006f + overcast * 0.0015f;
        return env;
    }

    /// <summary>Lambalar ne kadar yaniyor: 0 gunduz, 1 gece.</summary>
    public static float LampLevel(float hour)
    {
        hour = ((hour % 24) + 24) % 24;
        if (hour >= 20 || hour < 5.5f)
        {
            return 1f;
        }

        if (hour >= 19)
        {
            return hour - 19;
        }

        if (hour < 6.5f)
        {
            return 6.5f - hour;
        }

        return 0;
    }
}
