using System.Globalization;
using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Core;

namespace PilavciSimulator.Diagnostics;

/// <summary>
/// Otomatik dogrulama: bir senaryo dosyasini kare kare oynatir, ekran
/// goruntusu alir ve durum notlarini stdout'a yazar. Kalip PixelSurvival'daki
/// <c>Diagnostics/CaptureHarness.cs</c>'den alindi; 3B oyun icin fare
/// hareketi, tiklama ve gelistirici komutlari eklendi.
///
/// Senaryo yuklendiginde oyun sabit adimla (1/60 sn) ilerler; boylece
/// yavas bir yazilim GPU'sunda bile ayni senaryo ayni durumu uretir.
///
/// Komutlar (satir basina bir tane, # yorum):
///   wait N         N kare bekle ("wait 2s" saniye)
///   tap KEY        tusa bir kare bas birak (raylib KeyboardKey adi)
///   down KEY / up KEY
///   click [left|right]   bir kare tikla
///   press left|right / release left|right
///   look DX DY     fare hareketi (piksel)
///   mouse X Y      fare konumu (1080p tasarim koordinati)
///   type METIN     yazi yaz
///   shot AD        ekran goruntusu -> OUT/AD.png
///   annotate       ust ekranin durum notunu yaz
///   cmd ...        gelistirici komutu (bkz. GameplayScreen.Command)
///   expect METIN   son notta METIN gecmiyorsa hata say
///   quit           cik
/// </summary>
public sealed class CaptureHarness
{
    private readonly List<string> _lines;
    private readonly string _outDir;
    private int _cursor;
    private int _wait;
    private string? _pendingShot;
    private string _lastAnnotation = "";

    public ScriptedInputSource Input { get; } = new();
    public int Failures { get; private set; }
    public bool Finished { get; private set; }
    public int Shots { get; private set; }
    public const float FixedDt = 1f / 60f;

    private CaptureHarness(List<string> lines, string outDir)
    {
        _lines = lines;
        _outDir = outDir;
        Directory.CreateDirectory(outDir);
    }

    public static CaptureHarness? FromOptions(LaunchOptions o)
    {
        if (o.CaptureScript is null)
        {
            return null;
        }

        var lines = File.ReadAllLines(o.CaptureScript)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();
        return new CaptureHarness(lines, o.CaptureOut ?? "capture");
    }

    /// <summary>Kare basinda: siradaki komutlari bekleme gelene kadar isle.</summary>
    public void BeforeFrame(PilavciGame game)
    {
        if (Finished)
        {
            return;
        }

        if (_wait > 0)
        {
            _wait--;
            return;
        }

        while (_cursor < _lines.Count && _wait == 0 && _pendingShot is null)
        {
            var line = _lines[_cursor++];
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? line[(line.IndexOf(' ') + 1)..].Trim() : "";
            switch (cmd)
            {
                case "wait":
                    _wait = arg.EndsWith('s')
                        ? (int)(float.Parse(arg[..^1], CultureInfo.InvariantCulture) * 60f)
                        : int.Parse(arg, CultureInfo.InvariantCulture);
                    break;
                case "tap":
                    Input.KeyDown(Key(arg));
                    _releaseNext.Add(Key(arg));
                    _wait = 1;
                    break;
                case "down":
                    Input.KeyDown(Key(arg));
                    break;
                case "up":
                    Input.KeyUp(Key(arg));
                    break;
                case "click":
                {
                    var b = arg == "right" ? MouseButton.Right : MouseButton.Left;
                    Input.MouseButtonDown(b);
                    _releaseMouseNext.Add(b);
                    _wait = 1;
                    break;
                }
                case "press":
                    Input.MouseButtonDown(arg == "right" ? MouseButton.Right : MouseButton.Left);
                    break;
                case "release":
                    Input.MouseButtonUp(arg == "right" ? MouseButton.Right : MouseButton.Left);
                    break;
                case "look":
                    Input.AddMouseDelta(new Vector2(F(parts[1]), F(parts[2])));
                    break;
                case "mouse":
                {
                    var s = Raylib.GetScreenHeight() / 1080f;
                    Input.MousePosition = new Vector2(F(parts[1]) * s, F(parts[2]) * s);
                    break;
                }
                case "type":
                    Input.Type(arg);
                    break;
                case "shot":
                    _pendingShot = arg;
                    break;
                case "annotate":
                    _lastAnnotation = game.Annotate();
                    Console.WriteLine($"[capture] {_lastAnnotation}");
                    break;
                case "expect":
                    _lastAnnotation = game.Annotate();
                    if (!_lastAnnotation.Contains(arg, StringComparison.Ordinal))
                    {
                        Failures++;
                        Console.WriteLine($"[capture] BEKLENTI KARSILANMADI: '{arg}' yok -> {_lastAnnotation}");
                    }
                    else
                    {
                        Console.WriteLine($"[capture] tamam: {arg}");
                    }

                    break;
                case "cmd":
                    if (!game.Command(parts.Skip(1).ToArray()))
                    {
                        Failures++;
                        Console.WriteLine($"[capture] komut taninmadi: {arg}");
                    }

                    break;
                case "quit":
                    Finished = true;
                    _cursor = _lines.Count;
                    return;
                default:
                    Failures++;
                    Console.WriteLine($"[capture] bilinmeyen satir: {line}");
                    break;
            }
        }

        if (_cursor >= _lines.Count && _wait == 0 && _pendingShot is null)
        {
            Finished = true;
        }
    }

    private readonly List<KeyboardKey> _releaseNext = new();
    private readonly List<MouseButton> _releaseMouseNext = new();

    /// <summary>Cizim bittikten sonra, EndDrawing'den once: ekran goruntusu.</summary>
    public void AfterDraw()
    {
        foreach (var k in _releaseNext)
        {
            Input.KeyUp(k);
        }

        _releaseNext.Clear();
        foreach (var b in _releaseMouseNext)
        {
            Input.MouseButtonUp(b);
        }

        _releaseMouseNext.Clear();

        if (_pendingShot is null)
        {
            return;
        }

        Rlgl.DrawRenderBatchActive();
        var img = Raylib.LoadImageFromScreen();
        var path = Path.Combine(_outDir, _pendingShot + ".png");
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
        Console.WriteLine($"[capture] shot {path}");
        Shots++;
        _pendingShot = null;
    }

    private static KeyboardKey Key(string name) =>
        Enum.TryParse<KeyboardKey>(name, true, out var k) ? k : throw new ArgumentException($"tus yok: {name}");

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
