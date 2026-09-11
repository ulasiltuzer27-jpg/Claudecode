using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PixelSurvival.Systems.Input;

namespace PixelSurvival.Diagnostics;

/// <summary>
/// Oyunu insansız sürüp belirlenen karelerde ekran görüntüsü alan sürücü.
///
/// Neden gerekli? Projenin kuralı "her adımda üretilen kod gerçekten
/// çalıştırılmalı ve beklenen sonucun gerçekleştiği doğrulanmalı". Bir
/// başsız (headless) makinede bunu elle yapmak mümkün değil; bu sınıf
/// script dosyasındaki tuş dizisini oynatıp PNG kare döküyor. Çıkan
/// kareler, maddelerin görsel kanıtı olarak kullanılıyor.
///
/// Oyun mantığına dokunmaz: yalnızca sanal klavye besler ve back buffer'ı
/// okur. Script verilmezse tamamen devre dışıdır ve oyun normal açılır.
///
/// Script dili (satır bazlı, '#' yorum):
/// <code>
///   wait 60          60 kare bekle (60 kare = 1 saniye, sabit adımda)
///   down W           W tuşunu basılı tut
///   up W             W tuşunu bırak
///   tap Tab          Tab'a bas ve 2 kare sonra bırak (kenar tespitli tuşlar için)
///   shot hud         o karenin görüntüsünü &lt;çıktı&gt;/hud.png olarak kaydet
///   screen Settings  dogrudan o ekrana gec (menu gezinmesini atlar)
///   quit             oyunu kapat
/// </code>
/// </summary>
public sealed class CaptureHarness
{
    /// <summary>Tuşun "tap" sonrası kaç kare basılı kalacağı.</summary>
    private const int TapHoldFrames = 2;

    private readonly List<Command> _commands;
    private readonly string _outputDirectory;
    private readonly int _frameLimit;

    /// <summary>Şu an basılı sayılan sanal tuşlar.</summary>
    private readonly HashSet<Keys> _held = [];

    /// <summary>"tap" ile basılan tuşların bırakılacağı kare numaraları.</summary>
    private readonly Dictionary<Keys, int> _releaseAt = [];

    private int _commandIndex;
    private int _waitFrames;
    private int _frame;

    /// <summary>Bu karede kaydedilecek görüntünün adı; yoksa null.</summary>
    private string? _pendingShot;

    /// <summary>
    /// Script'in istediği ekran adı; oyun kabuğu okuyup uygular ve
    /// temizler.
    ///
    /// Neden var: ekranlar artık ana menüden açılıyor ve her doğrulama
    /// script'inin menüde kaç kez aşağı ineceğini saymasi gerekiyordu.
    /// O sayım menüye bir satır eklendiği anda sessizce bozuluyor ve
    /// script yanlış ekranın görüntüsünü kaydediyordu. Menü gezinmesi
    /// kendi script'inde (menu.txt) test ediliyor; diğer script'ler
    /// kendi ozelliklerini test etmeli.
    /// </summary>
    public string? RequestedScreen { get; private set; }

    /// <summary>İstenen ekranı okur ve isteği temizler.</summary>
    public string? TakeRequestedScreen()
    {
        var value = RequestedScreen;
        RequestedScreen = null;
        return value;
    }

    /// <summary>Script "quit" dedi ya da kare sınırı doldu.</summary>
    public bool ShouldExit { get; private set; }

    /// <summary>Kaydedilen görüntü sayısı — çalışma sonunda rapor edilir.</summary>
    public int ShotCount { get; private set; }

    private CaptureHarness(List<Command> commands, string outputDirectory, int frameLimit)
    {
        _commands = commands;
        _outputDirectory = outputDirectory;
        _frameLimit = frameLimit;
    }

    /// <summary>
    /// Komut satırı argümanlarından harness kurar. Script verilmemişse
    /// <c>null</c> döner ve oyun normal (insan sürücülü) modda açılır.
    ///
    /// <c>--capture-script &lt;dosya&gt; --capture-out &lt;klasör&gt; [--capture-frame-limit N]</c>
    /// </summary>
    public static CaptureHarness? FromArguments(string[] args)
    {
        var script = ValueOf(args, "--capture-script");
        if (script is null) return null;

        var output = ValueOf(args, "--capture-out") ?? "capture";
        var limitText = ValueOf(args, "--capture-frame-limit");

        // Sonsuz döngüye karşı emniyet: script bitmese bile süreç sonlanır.
        var limit = int.TryParse(limitText, out var parsed) ? parsed : 60 * 60;

        Directory.CreateDirectory(output);

        var harness = new CaptureHarness(ParseScript(script), output, limit);

        // Oyunun klavye okuduğu tek noktayı sanal duruma bağla.
        InputSource.Override = harness.BuildKeyboardState;
        return harness;
    }

    private static string? ValueOf(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static List<Command> ParseScript(string path)
    {
        var commands = new List<Command>();

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();

            // Boş satır ve yorum atlanır; script'ler okunabilir kalsın.
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();
            var argument = parts.Length > 1 ? parts[1] : "";

            commands.Add(new Command(verb, argument));
        }

        return commands;
    }

    /// <summary>Sanal klavye durumu — <see cref="InputSource"/> her tick bunu çağırır.</summary>
    private KeyboardState BuildKeyboardState() => new([.. _held]);

    /// <summary>
    /// Bir kare ilerletir: süresi dolan "tap" tuşlarını bırakır, bekleme
    /// varsa sayar, yoksa sıradaki komutları <c>wait</c>/<c>shot</c> görene
    /// kadar işler.
    ///
    /// Oyunun <c>Update</c>'inin BAŞINDA çağrılmalı ki tuşlar aynı karede okunsun.
    /// </summary>
    public void Update()
    {
        _frame++;

        if (_frame > _frameLimit)
        {
            Console.WriteLine($"[capture] kare siniri ({_frameLimit}) doldu, cikiliyor.");
            ShouldExit = true;
            return;
        }

        // Süresi dolan tap tuşlarını bırak.
        foreach (var (key, frame) in _releaseAt.ToArray())
        {
            if (_frame < frame) continue;

            _held.Remove(key);
            _releaseAt.Remove(key);
        }

        if (_waitFrames > 0)
        {
            _waitFrames--;
            return;
        }

        while (_commandIndex < _commands.Count)
        {
            var command = _commands[_commandIndex++];

            switch (command.Verb)
            {
                case "wait":
                    _waitFrames = int.TryParse(command.Argument, out var frames) ? frames : 1;
                    return;

                case "down":
                    if (TryParseKey(command.Argument, out var downKey)) _held.Add(downKey);
                    break;

                case "up":
                    if (TryParseKey(command.Argument, out var upKey))
                    {
                        _held.Remove(upKey);
                        _releaseAt.Remove(upKey);
                    }
                    break;

                case "tap":
                    if (TryParseKey(command.Argument, out var tapKey))
                    {
                        _held.Add(tapKey);
                        _releaseAt[tapKey] = _frame + TapHoldFrames;
                    }

                    // Tap kendi bekleme suresini DAYATIR ve komut islemeyi
                    // durdurur.
                    //
                    // Aksi halde arka arkaya yazilan "tap Down / tap Down"
                    // ayni karede islenir, ikisi de ayni tusu basili
                    // yapar ve oyun kenar tespiti kullandigi icin TEK
                    // basis olarak gorunur. Script yazan kisi ise iki
                    // basis bekler. Bu, script'i sessizce yanlis
                    // calistiran bir tuzakti.
                    _waitFrames = TapHoldFrames + 1;
                    return;

                case "shot":
                    // Görüntü ancak Draw bittikten sonra alınabilir; işaretlenip
                    // bu kare için komut işleme durduruluyor.
                    _pendingShot = command.Argument;
                    return;

                case "screen":
                    RequestedScreen = command.Argument;
                    _waitFrames = 1;
                    return;

                case "quit":
                    ShouldExit = true;
                    return;

                default:
                    Console.WriteLine($"[capture] bilinmeyen komut: {command.Verb}");
                    break;
            }
        }

        // Script bitti.
        ShouldExit = true;
    }

    private static bool TryParseKey(string text, out Keys key) =>
        Enum.TryParse(text, ignoreCase: true, out key);

    /// <summary>
    /// Bekleyen görüntü varsa back buffer'ı PNG'ye yazar.
    /// Oyunun <c>Draw</c>'unun EN SONUNDA çağrılmalı.
    /// </summary>
    public void CaptureIfRequested(GraphicsDevice device)
    {
        if (_pendingShot is null) return;

        var name = _pendingShot;
        _pendingShot = null;

        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;

        var pixels = new Microsoft.Xna.Framework.Color[width * height];
        device.GetBackBufferData(pixels);

        // GetBackBufferData ham (premultiplied) veriyi verir; PNG'ye yazarken
        // Texture2D üzerinden geçmek doğru kanal sırasını garanti eder.
        using var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        var path = Path.Combine(_outputDirectory, $"{name}.png");
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);

        ShotCount++;
        Console.WriteLine($"[capture] kare {_frame}: {path}" +
                          (Annotation.Length > 0 ? $"  {Annotation}" : ""));
    }

    /// <summary>
    /// Ekran görüntüsüyle birlikte stdout'a yazılacak tek satırlık durum.
    ///
    /// Neden var: bazı kurallar KAREYE BAKARAK doğrulanamıyor. Çarpışma
    /// köşe düzeltmesi bunun en net örneği — ekran görüntüsü karakterin
    /// durduğunu gösterir ama "kaç pixel ilerledi" sorusunu cevaplamaz.
    /// Oysa düzeltmenin tek ölçülebilir sonucu tam olarak budur.
    ///
    /// Oyun mantığına dokunmuyor; yalnızca doğrulama koşumlarında
    /// <see cref="Game1"/> tarafından dolduruluyor.
    /// </summary>
    public string Annotation { get; set; } = "";

    private readonly record struct Command(string Verb, string Argument);
}
