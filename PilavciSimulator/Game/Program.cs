using System.Reflection;
using PilavciSimulator;
using PilavciSimulator.Core;

// Pilavci Simulatoru giris noktasi.
//
// Argumansiz calistirma normal oyundur. Otomatik dogrulama ve co-op testi
// icin secenekler: bkz. Core/LaunchOptions.cs
CrashHandler.Install();

LaunchOptions options;
try
{
    options = LaunchOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

if (options.DataDir is not null)
{
    Paths.OverrideUserRoot(options.DataDir);
}

Paths.EnsureUserFolders();
Log.Init(Paths.Logs);

var version = Assembly.GetExecutingAssembly().GetName().Version;
Log.Info($"Pilavci Simulatoru {version} basliyor. Kullanici verisi: {Paths.UserRoot}");

#if STEAM_BUILD
// Oyun Steam disindan (cift tiklayarak) acildiysa Steam uzerinden yeniden
// baslat. steam_appid.txt yanindaysa (gelistirme) bu kontrol atlanir.
if (PilavciSimulator.Platform.SteamService.RestartIfNecessary())
{
    Log.Info("Steam uzerinden yeniden baslatiliyor.");
    Log.Shutdown();
    return 0;
}
#endif

try
{
    using var game = new PilavciGame(options);
    return game.Run();
}
catch (Exception ex)
{
    var path = CrashHandler.Write(ex);
    Console.Error.WriteLine($"Oyun beklenmedik bir hatayla kapandi. Rapor: {path}");
    return 1;
}
finally
{
    Log.Shutdown();
}
