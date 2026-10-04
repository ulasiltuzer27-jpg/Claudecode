using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starfall.Core;

/// <summary>
/// Kayit klasoru ve atomik JSON yazimi. Windows: %APPDATA%/StarfallIsle,
/// Linux: $XDG_DATA_HOME/StarfallIsle (~/.local/share), macOS: ~/Library/Application Support.
/// Yazim once .tmp'ye yapilir, sonra yerine tasinir; eski surum .bak olarak kalir.
/// </summary>
public static class Storage
{
    public static string Dir { get; private set; } = DefaultDir();

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private static string DefaultDir()
    {
        string root;
        if (OperatingSystem.IsWindows()) root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        else if (OperatingSystem.IsMacOS()) root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        else root = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x ? x : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(root, "StarfallIsle");
    }

    /// <summary>Testler gecici klasor kullanir (--data).</summary>
    public static void SetDir(string dir) => Dir = dir;

    public static string PathOf(string name) => Path.Combine(Dir, name + ".json");

    public static T? Read<T>(string name) where T : class
    {
        foreach (var p in new[] { PathOf(name), PathOf(name) + ".bak" })
        {
            try
            {
                if (!File.Exists(p)) continue;
                var v = JsonSerializer.Deserialize<T>(File.ReadAllText(p), Json);
                if (v != null) return v;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[kayit] okunamadi {p}: {ex.Message}");
            }
        }
        return null;
    }

    public static bool Write<T>(string name, T data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var path = PathOf(name);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(tmp, path, true);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kayit] yazilamadi {name}: {ex.Message}");
            return false;
        }
    }

    public static void Remove(string name)
    {
        foreach (var p in new[] { PathOf(name), PathOf(name) + ".bak" })
            try { if (File.Exists(p)) File.Delete(p); } catch { /* yok say */ }
    }

    public static string PhotosDir()
    {
        var pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var d = string.IsNullOrEmpty(pics) ? Path.Combine(Dir, "photos") : Path.Combine(pics, "Starfall Isle");
        Directory.CreateDirectory(d);
        return d;
    }
}
