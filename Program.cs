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

using var game = new Game1(harness);
game.Run();
