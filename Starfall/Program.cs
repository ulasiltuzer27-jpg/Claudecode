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
            if (args.TryGetValue("at", out var at))
            {
                var t2 = new Terrain("isle2", Isle2Shape.Instance, 4242, L2.C.X, L2.C.Y, L2.Size, (int)(L2.Size / 2));
                var xz = at.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Console.WriteLine($"{xz[0]},{xz[1]}: isle1 {t.Height(xz[0], xz[1]):F2} isle2 {t2.Height(xz[0], xz[1]):F2}");
            }
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
