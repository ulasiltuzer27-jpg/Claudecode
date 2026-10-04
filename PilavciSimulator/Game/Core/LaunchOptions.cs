namespace PilavciSimulator.Core;

/// <summary>
/// Komut satiri secenekleri. Hepsi istege bagli; argumansiz calistirma
/// normal oyundur.
///
///   --data-dir DIR          kullanici verisini (kayit/ayar/log) DIR'e yaz
///   --capture-script FILE   senaryo dosyasini oynat (bkz. CaptureHarness)
///   --capture-out DIR       ekran goruntulerinin yazilacagi klasor
///   --new-game              menuyu atla, yeni oyun baslat
///   --host                  menuyu atla, co-op host olarak baslat
///   --join ADDR             menuyu atla, ADDR'deki host'a IP ile baglan
///   --port N                dogrudan IP oturumunun portu (varsayilan 27015)
///   --name NAME             oyuncu adi (Steam yokken)
///   --windowed WxH          ayarlari yok sayip bu boyutta pencere ac
///   --seed N                dunya/musteri rastgeleligi icin tohum
///   --no-audio              ses aygitini hic acma
///   --skip-tutorial         egitimi kapali baslat
/// </summary>
public sealed class LaunchOptions
{
    public string? DataDir { get; private set; }
    public string? CaptureScript { get; private set; }
    public string? CaptureOut { get; private set; }
    public bool NewGame { get; private set; }
    public bool Host { get; private set; }
    public string? JoinAddress { get; private set; }
    public int Port { get; private set; } = 27015;
    public string? PlayerName { get; private set; }
    public (int W, int H)? Windowed { get; private set; }
    public int? Seed { get; private set; }
    public bool NoAudio { get; private set; }
    public bool SkipTutorial { get; private set; }

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var o = new LaunchOptions();
        for (var i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"{args[i]} bir deger bekliyor");

            switch (args[i])
            {
                case "--data-dir": o.DataDir = Next(); break;
                case "--capture-script": o.CaptureScript = Next(); break;
                case "--capture-out": o.CaptureOut = Next(); break;
                case "--new-game": o.NewGame = true; break;
                case "--host": o.Host = true; break;
                case "--join": o.JoinAddress = Next(); break;
                case "--port": o.Port = int.Parse(Next()); break;
                case "--name": o.PlayerName = Next(); break;
                case "--seed": o.Seed = int.Parse(Next()); break;
                case "--no-audio": o.NoAudio = true; break;
                case "--skip-tutorial": o.SkipTutorial = true; break;
                case "--windowed":
                {
                    var parts = Next().Split('x');
                    o.Windowed = (int.Parse(parts[0]), int.Parse(parts[1]));
                    break;
                }
                default:
                    // Steam bazen kendi argumanlarini ekler (+connect_lobby vb.);
                    // tanimadigimiz argumanda patlamak yerine loglayip geciyoruz.
                    Log.Warn($"bilinmeyen arguman yok sayildi: {args[i]}");
                    break;
            }
        }

        return o;
    }
}
