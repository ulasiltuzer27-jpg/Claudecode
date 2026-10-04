namespace Starfall.Core;

/// <summary>Oyuncu ayarlari: profil dosyasindan bagimsiz, kendi dosyasinda (settings.json).</summary>
public sealed class SettingsData
{
    public string? Lang { get; set; }
    public float Master { get; set; } = 0.8f;
    public float Music { get; set; } = 0.6f;
    public float Sfx { get; set; } = 0.8f;
    public float Ambience { get; set; } = 0.7f;
    public string Quality { get; set; } = "medium";
    public float Sensitivity { get; set; } = 1.0f;
    public bool InvertY { get; set; }
    public bool Fullscreen { get; set; }
    public float TextScale { get; set; } = 1;
    public bool CameraShake { get; set; } = true;
    public bool AutoCamera { get; set; } = true;
    public bool ShowFps { get; set; }
    public bool VSync { get; set; } = true;
}

public sealed class Settings
{
    public SettingsData V = new();
    public event Action<string>? Changed;
    private float _saveT = -1;

    public void Load()
    {
        V = Storage.Read<SettingsData>("settings") ?? new SettingsData();
        V.Lang ??= Loc.Detect();
        V.TextScale = Math.Clamp(V.TextScale, 0.8f, 1.4f);
        V.Sensitivity = Math.Clamp(V.Sensitivity, 0.2f, 3f);
    }

    /// <summary>Bir ayar degisti: dinleyicilere haber ver, kisa gecikmeyle diske yaz.</summary>
    public void Touch(string key)
    {
        Changed?.Invoke(key);
        _saveT = 0.3f;
    }

    public void Update(float dt)
    {
        if (_saveT < 0) return;
        _saveT -= dt;
        if (_saveT < 0) Save();
    }

    public void Save() => Storage.Write("settings", V);
}
