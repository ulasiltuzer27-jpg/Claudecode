using System.Numerics;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Bir karenin isik ve atmosfer durumu. Gun/gece dongusu bunu her kare
/// doldurur (bkz. World/DayNight). Renkler 0..1; gunes rengi dogrusal ve
/// yogunlugu icerir, gokyuzu renkleri ekran (sRGB) uzayinda.
/// </summary>
public struct SceneEnvironment
{
    public Vector3 SunDirection;     // gunese DOGRU, birim
    public Vector3 SunColor;         // dogrusal, yogunluk dahil
    public Vector3 SkyAmbient;       // dogrusal
    public Vector3 GroundAmbient;    // dogrusal
    public Vector3 ZenithColor;      // ekran
    public Vector3 HorizonColor;     // ekran; sis rengi de bu
    public Vector3 GroundColor;      // ekran
    public Vector3 SunDiscColor;     // ekran
    public Vector3 MoonDirection;
    public Vector3 CloudColor;
    public float CloudAmount;
    public float StarAmount;
    public float FogDensity;
    public float Exposure;
    public float NightGlow;          // pencere/tabela isiklari 0..1
    public float Time;               // saniye (animasyonlar icin)

    public static SceneEnvironment Default => new()
    {
        SunDirection = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.3f)),
        SunColor = new Vector3(2.6f, 2.45f, 2.2f),
        SkyAmbient = new Vector3(0.32f, 0.38f, 0.48f),
        GroundAmbient = new Vector3(0.16f, 0.14f, 0.12f),
        ZenithColor = new Vector3(0.28f, 0.52f, 0.86f),
        HorizonColor = new Vector3(0.72f, 0.82f, 0.92f),
        GroundColor = new Vector3(0.35f, 0.36f, 0.38f),
        SunDiscColor = new Vector3(1f, 0.95f, 0.8f),
        MoonDirection = Vector3.Normalize(new Vector3(-0.4f, 0.6f, -0.3f)),
        CloudColor = new Vector3(0.96f, 0.96f, 0.98f),
        CloudAmount = 0.4f,
        StarAmount = 0f,
        FogDensity = 0.0045f,
        Exposure = 1f,
        NightGlow = 0f,
    };
}

/// <summary>Nokta isik (sokak lambasi, ocak alevi, tabela).</summary>
public readonly record struct PointLight(Vector3 Position, float Range, Vector3 Color);
