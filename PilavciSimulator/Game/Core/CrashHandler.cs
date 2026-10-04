using System.Text;

namespace PilavciSimulator.Core;

/// <summary>
/// Yakalanmamis istisnalari <c>logs/crash-YYYYMMDD-HHMMSS.txt</c> dosyasina
/// yazar. Steam incelemelerinde "oyun acilmiyor" sikayetine verilecek ilk
/// cevap bu dosyayi istemektir; bu yuzden ortam bilgisi de eklenir.
/// </summary>
public static class CrashHandler
{
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Write(ex);
            }
        };
    }

    public static string? Write(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            var path = Path.Combine(Paths.Logs, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine("Pilavci Simulatoru - cokme raporu");
            sb.AppendLine($"Zaman      : {DateTime.Now:O}");
            sb.AppendLine($"Surum      : {typeof(CrashHandler).Assembly.GetName().Version}");
            sb.AppendLine($"OS         : {Environment.OSVersion}");
            sb.AppendLine($".NET       : {Environment.Version}");
            sb.AppendLine($"64 bit     : {Environment.Is64BitProcess}");
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
            File.WriteAllText(path, sb.ToString());
            Log.Error("COKME", ex);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
