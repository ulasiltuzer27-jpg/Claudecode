namespace PilavciSimulator.Core;

public enum WindowMode
{
    Windowed,
    Borderless,
    Fullscreen,
}

/// <summary>
/// Oyuncunun kalici ayarlari: <c>config/settings.json</c>.
///
/// Tum alanlar varsayilan degerli; eski bir ayar dosyasinda yeni bir alan
/// yoksa varsayilan kullanilir, dosya bozuksa tamami varsayilana doner.
/// Degerler kullanilmadan once <see cref="Clamp"/> ile sinirlanir: elle
/// duzenlenmis bir dosyadaki "fov: 500" oyunu bozmamali.
/// </summary>
public sealed class GameSettings
{
    public const string FileName = "settings.json";

    // ── Goruntu ─────────────────────────────────────────────────────────
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
    public int Width { get; set; } = 1600;
    public int Height { get; set; } = 900;
    public bool VSync { get; set; } = true;
    /// <summary>0 = sinirsiz (VSync kapaliyken).</summary>
    public int FpsLimit { get; set; } = 144;
    /// <summary>3B sahnenin cozunurluk carpani. 0.5..1.0</summary>
    public float RenderScale { get; set; } = 1f;
    /// <summary>0 kapali, 1 dusuk (1024), 2 orta (2048), 3 yuksek (4096).</summary>
    public int ShadowQuality { get; set; } = 2;
    public bool Fxaa { get; set; } = true;
    public float Fov { get; set; } = 75f;
    public float Brightness { get; set; } = 1f;

    // ── Ses ─────────────────────────────────────────────────────────────
    public float MasterVolume { get; set; } = 0.8f;
    public float SfxVolume { get; set; } = 1f;
    public float MusicVolume { get; set; } = 0.5f;
    public float AmbientVolume { get; set; } = 0.7f;

    // ── Kontrol ─────────────────────────────────────────────────────────
    public float MouseSensitivity { get; set; } = 1f;
    public bool InvertY { get; set; }
    /// <summary>Eylem adi -> en fazla iki tus adi (raylib KeyboardKey / Mouse adi).</summary>
    public Dictionary<string, string[]> Bindings { get; set; } = new();

    // ── Oynanis / erisilebilirlik ───────────────────────────────────────
    public string Language { get; set; } = "tr";
    public bool HeadBob { get; set; } = true;
    public float UiScale { get; set; } = 1f;
    public bool ShowHints { get; set; } = true;
    public bool Subtitles { get; set; } = true;
    public string PlayerName { get; set; } = "";

    public void Clamp()
    {
        Width = Math.Clamp(Width, 800, 7680);
        Height = Math.Clamp(Height, 450, 4320);
        FpsLimit = FpsLimit == 0 ? 0 : Math.Clamp(FpsLimit, 30, 360);
        RenderScale = Math.Clamp(RenderScale, 0.5f, 1f);
        ShadowQuality = Math.Clamp(ShadowQuality, 0, 3);
        Fov = Math.Clamp(Fov, 60f, 100f);
        Brightness = Math.Clamp(Brightness, 0.6f, 1.6f);
        MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
        SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
        MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
        AmbientVolume = Math.Clamp(AmbientVolume, 0f, 1f);
        MouseSensitivity = Math.Clamp(MouseSensitivity, 0.1f, 4f);
        UiScale = Math.Clamp(UiScale, 0.75f, 1.75f);
        if (Language is not ("tr" or "en"))
        {
            Language = "tr";
        }

        if (PlayerName.Length > 24)
        {
            PlayerName = PlayerName[..24];
        }
    }

    public static string FilePath => Path.Combine(Paths.Config, FileName);

    public static GameSettings Load()
    {
        var s = JsonUtil.ReadOrDefault(FilePath, () => new GameSettings());
        s.Clamp();
        return s;
    }

    public void Save()
    {
        try
        {
            Clamp();
            JsonUtil.WriteAtomic(FilePath, this);
        }
        catch (Exception ex)
        {
            Log.Warn($"ayarlar kaydedilemedi: {ex.Message}");
        }
    }
}
