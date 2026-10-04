using PilavciSimulator.Core;

namespace PilavciSimulator.Tests;

/// <summary>Testler kaynak agacindaki Data/ klasorunu okur (derleme ciktisina kopyalanan da ayni).</summary>
public static class TestPaths
{
    public static string GameDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Game", "PilavciSimulator.csproj")))
            {
                dir = dir.Parent;
            }

            return dir is null ? AppContext.BaseDirectory : Path.Combine(dir.FullName, "Game");
        }
    }

    public static void UseSourceData() => Paths.OverrideGameRoot(GameDir);
}
