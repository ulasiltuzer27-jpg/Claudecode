using PilavciSimulator.Core;
using PilavciSimulator.Localization;
using Xunit;

namespace PilavciSimulator.Tests;

public class CoreTests
{
    [Fact]
    public void Localization_tables_have_identical_keys()
    {
        var dir = Path.Combine(TestPaths.GameDir, "Data", "Localization");
        var tr = Loc.LoadTable(Path.Combine(dir, "tr.json"));
        var en = Loc.LoadTable(Path.Combine(dir, "en.json"));
        var missingInEn = tr.Keys.Except(en.Keys).ToList();
        var missingInTr = en.Keys.Except(tr.Keys).ToList();
        Assert.True(missingInEn.Count == 0, "en.json eksik: " + string.Join(", ", missingInEn));
        Assert.True(missingInTr.Count == 0, "tr.json eksik: " + string.Join(", ", missingInTr));
    }

    [Fact]
    public void Localization_format_placeholders_match()
    {
        var dir = Path.Combine(TestPaths.GameDir, "Data", "Localization");
        var tr = Loc.LoadTable(Path.Combine(dir, "tr.json"));
        var en = Loc.LoadTable(Path.Combine(dir, "en.json"));
        foreach (var (key, trValue) in tr)
        {
            if (!en.TryGetValue(key, out var enValue))
            {
                continue;
            }

            for (var i = 0; i < 6; i++)
            {
                var token = "{" + i;
                Assert.True(trValue.Contains(token) == enValue.Contains(token), $"{key}: {{{i}}} yer tutucusu dillerde farkli");
            }
        }
    }

    [Fact]
    public void Missing_key_is_shown_in_brackets()
    {
        Loc.Load(Path.Combine(TestPaths.GameDir, "Data"), "tr");
        Assert.Equal("[yok.boyle.bir.anahtar]", Loc.T("yok.boyle.bir.anahtar"));
    }

    [Fact]
    public void Settings_are_clamped()
    {
        var s = new GameSettings { Fov = 500, MouseSensitivity = -3, RenderScale = 9, Language = "xx", ShadowQuality = 12 };
        s.Clamp();
        Assert.Equal(100f, s.Fov);
        Assert.Equal(0.1f, s.MouseSensitivity);
        Assert.Equal(1f, s.RenderScale);
        Assert.Equal("tr", s.Language);
        Assert.Equal(3, s.ShadowQuality);
    }

    [Fact]
    public void Atomic_write_keeps_backup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pilav-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "a.json");
        JsonUtil.WriteAtomic(path, new GameSettings { Fov = 70 });
        JsonUtil.WriteAtomic(path, new GameSettings { Fov = 80 });
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal(80f, JsonUtil.Read<GameSettings>(path).Fov);
        Assert.Equal(70f, JsonUtil.Read<GameSettings>(path + ".bak").Fov);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Rng_is_deterministic()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        var r = new Rng(7);
        for (var i = 0; i < 1000; i++)
        {
            var v = r.Range(3, 9);
            Assert.InRange(v, 3, 8);
            var f = r.NextFloat();
            Assert.InRange(f, 0f, 0.99999994f);
        }
    }
}
