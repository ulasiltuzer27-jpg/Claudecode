using Starfall.Core;
using Starfall.World;

namespace Starfall;

public static class Program
{
    public static int Main(string[] argv)
    {
        var args = new Dictionary<string, string>();
        for (int i = 0; i < argv.Length; i++)
        {
            if (!argv[i].StartsWith("--")) continue;
            string key = argv[i][2..];
            string val = i + 1 < argv.Length && !argv[i + 1].StartsWith("--") ? argv[++i] : "1";
            args[key] = val;
        }
        if (args.ContainsKey("dump-heights"))
        {
            var t = new Terrain("isle1", Isle1Shape.Instance, 1337, 0, 0, 512, 256);
            foreach (var (x, z) in new[] { (6f, -112f), (104f, -58f), (8f, 108f) })
                Console.WriteLine($"{x},{z}: {t.Height(x, z):F4}");
            return 0;
        }
        if (args.ContainsKey("verify")) return Tests.Verify.Run(args);
        if (args.ContainsKey("playtest")) return Tests.Playtest.Run(args);
        if (args.ContainsKey("audio-test")) return Tests.AudioTest.Run(args);
        var game = new Game(args);
        game.Build();
        var host = new Host(game, new HostOptions { Capture = args.ContainsKey("capture") });
        host.Run();
        return 0;
    }
}
