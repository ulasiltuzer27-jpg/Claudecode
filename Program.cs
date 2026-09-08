using PixelSurvival;
using PixelSurvival.Diagnostics;

// MonoGame DesktopGL giris noktasi.
//
// Argumansiz calistirildiginda oyun normal acilir. Otomatik dogrulama icin
// bir yakalama script'i verilebilir (bkz. CaptureHarness):
//
//   dotnet run -- --capture-script Tools/captures/hud.txt --capture-out capture
//
var harness = CaptureHarness.FromArguments(args);

// --self-test: saf mantik denetimlerini kosturup cikar. Grafik penceresi
// yine acilir (envanter ItemDatabase'i Content Pipeline'dan geliyor), ama
// denetim biter bitmez kapanir ve cikis kodu sonucu tasir.
var selfTest = args.Contains("--self-test");

using var game = new Game1(harness, selfTest);
game.Run();

return Environment.ExitCode;
