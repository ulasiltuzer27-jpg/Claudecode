using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PilavciSimulator.Core;

/// <summary>
/// Ortak JSON ayarlari ve atomik dosya yazimi.
/// </summary>
public static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Turkce karakterler ş yerine oldugu gibi yazilsin; ayar
        // dosyasini elle acan oyuncu okuyabilsin.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        IncludeFields = false,
    };

    public static T Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, Options)
               ?? throw new InvalidDataException($"{path} bos ya da gecersiz");
    }

    public static T ReadOrDefault<T>(string path, Func<T> fallback)
    {
        try
        {
            return File.Exists(path) ? Read<T>(path) : fallback();
        }
        catch (Exception ex)
        {
            Log.Warn($"{Path.GetFileName(path)} okunamadi, varsayilanlar kullaniliyor: {ex.Message}");
            return fallback();
        }
    }

    /// <summary>
    /// Once <c>.tmp</c>'ye yazar, sonra yerine tasir; eski dosya <c>.bak</c>
    /// olarak kalir. Yazim ortasinda elektrik kesilirse eski kayit bozulmaz.
    /// </summary>
    public static void WriteAtomic<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(tmp, json);

        if (File.Exists(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
