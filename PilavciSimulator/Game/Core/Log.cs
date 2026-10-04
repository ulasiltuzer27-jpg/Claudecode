using System.Diagnostics;
using System.Text;

namespace PilavciSimulator.Core;

/// <summary>
/// Oyunun tek log noktasi. Hem konsola hem <c>logs/log.txt</c> dosyasina yazar.
///
/// Steam'de yayinlanan bir oyunda oyuncudan alinabilecek tek tani bilgisi
/// genellikle bu dosyadir; bu yuzden her satir zaman damgali ve seviyeli.
/// Dosya her acilista yeniden yazilir, bir onceki oturum <c>log-prev.txt</c>
/// olarak saklanir.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static string? FilePath { get; private set; }

    public static void Init(string logDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            var path = Path.Combine(logDirectory, "log.txt");
            var prev = Path.Combine(logDirectory, "log-prev.txt");
            if (File.Exists(path))
            {
                File.Copy(path, prev, overwrite: true);
            }

            _file = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
            FilePath = path;
        }
        catch (Exception ex)
        {
            // Log dosyasi acilamiyorsa oyun yine de calismali.
            Console.Error.WriteLine($"[log] dosya acilamadi: {ex.Message}");
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"[{Clock.Elapsed.TotalSeconds,8:F2}] {level,-5} {message}";
        lock (Gate)
        {
            Console.WriteLine(line);
            _file?.WriteLine(line);
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            _file?.Dispose();
            _file = null;
        }
    }
}
