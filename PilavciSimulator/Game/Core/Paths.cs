namespace PilavciSimulator.Core;

/// <summary>
/// Diskteki tum konumlar.
///
/// Iki ayri kok var:
///  - <see cref="GameRoot"/>: exe'nin klasoru. Salt okunur kabul edilir
///    (Steam kurulumu Program Files altinda olabilir). Data/ ve Assets/ burada.
///  - <see cref="UserRoot"/>: oyuncuya ait yazilabilir veri.
///    Windows'ta <c>%LOCALAPPDATA%/PilavciSimulator</c>. Kayitlar, ayarlar,
///    loglar ve ekran goruntuleri burada; Steam Auto-Cloud bu klasoru
///    hedefleyecek sekilde ayarlanabilir (README).
/// </summary>
public static class Paths
{
    public const string AppFolderName = "PilavciSimulator";

    public static string GameRoot { get; private set; } = AppContext.BaseDirectory;
    public static string UserRoot { get; private set; } = DefaultUserRoot();

    public static string Data => Path.Combine(GameRoot, "Data");
    public static string Assets => Path.Combine(GameRoot, "Assets");
    public static string Saves => Path.Combine(UserRoot, "saves");
    public static string Config => Path.Combine(UserRoot, "config");
    public static string Logs => Path.Combine(UserRoot, "logs");
    public static string Screenshots => Path.Combine(UserRoot, "screenshots");

    /// <summary>
    /// Testler ve otomatik dogrulama, oyuncunun gercek kayitlarina
    /// dokunmamak icin kullanici kokunu degistirir (<c>--data-dir</c>).
    /// </summary>
    public static void OverrideUserRoot(string path) => UserRoot = Path.GetFullPath(path);

    /// <summary>Testlerin kaynak agacindaki Data/ klasorunu kullanabilmesi icin.</summary>
    public static void OverrideGameRoot(string path) => GameRoot = Path.GetFullPath(path);

    public static void EnsureUserFolders()
    {
        Directory.CreateDirectory(Saves);
        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Screenshots);
    }

    private static string DefaultUserRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local))
        {
            local = Path.Combine(AppContext.BaseDirectory, "userdata");
        }

        return Path.Combine(local, AppFolderName);
    }
}
