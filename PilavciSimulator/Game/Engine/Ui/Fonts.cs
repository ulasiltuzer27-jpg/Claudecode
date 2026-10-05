using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Core;

namespace PilavciSimulator.Engine.Ui;

/// <summary>
/// Nunito (SIL OFL, bkz. Assets/Fonts/OFL.txt) iki agirlikta, birden cok
/// boyutta yuklenir. raylib bitmap font kullandigi icin istenen boyuta en
/// yakin (esit ya da buyuk) atlas secilip kucultulerek cizilir; buyutmek
/// bulaniklastirdigi icin kacinilir.
///
/// Glif seti: ASCII + Latin-1 + Turkce (ĞğİıŞş) + ₺ ve birkac noktalama.
/// Atlasta olmayan bir karakter '?' olarak cizilir; testler Turkce
/// karakterlerin atlasta oldugunu dogruluyor.
/// </summary>
public sealed class Fonts : IDisposable
{
    private static readonly int[] Sizes = [18, 24, 32, 44, 60, 84, 120];

    private readonly Font[] _regular = new Font[Sizes.Length];
    private readonly Font[] _bold = new Font[Sizes.Length];

    public static int[] Codepoints()
    {
        var list = new List<int>();
        for (var c = 32; c <= 126; c++)
        {
            list.Add(c);
        }

        for (var c = 160; c <= 255; c++)
        {
            list.Add(c);
        }

        list.AddRange([0x011E, 0x011F, 0x0130, 0x0131, 0x015E, 0x015F, 0x20BA, 0x2022, 0x2026, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2248, 0x2212]);
        return list.ToArray();
    }

    public Fonts(string fontDir)
    {
        var cps = Codepoints();
        var regularPath = Path.Combine(fontDir, "Nunito-SemiBold.ttf");
        var boldPath = Path.Combine(fontDir, "Nunito-ExtraBold.ttf");
        for (var i = 0; i < Sizes.Length; i++)
        {
            _regular[i] = Load(regularPath, Sizes[i], cps);
            _bold[i] = Load(boldPath, Sizes[i], cps);
        }
    }

    private static Font Load(string path, int size, int[] cps)
    {
        if (!File.Exists(path))
        {
            Log.Warn($"font bulunamadi: {path}; raylib varsayilan fontu kullaniliyor");
            return Raylib.GetFontDefault();
        }

        var f = Raylib.LoadFontEx(path, size, cps, cps.Length);
        Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        return f;
    }

    public Font Pick(float size, bool bold, out float drawSize)
    {
        var set = bold ? _bold : _regular;
        for (var i = 0; i < Sizes.Length; i++)
        {
            if (Sizes[i] >= size * 0.92f)
            {
                drawSize = size;
                return set[i];
            }
        }

        drawSize = size;
        return set[^1];
    }

    public Vector2 Measure(string text, float size, bool bold = false)
    {
        var f = Pick(size, bold, out var ds);
        return Raylib.MeasureTextEx(f, text, ds, 0);
    }

    public void Draw(string text, Vector2 pos, float size, Color color, bool bold = false)
    {
        var f = Pick(size, bold, out var ds);
        Raylib.DrawTextEx(f, text, new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)), ds, 0, color);
    }

    /// <summary>Golgeli yazi: 3B sahnenin ustundeki HUD metinleri icin okunurluk.</summary>
    public void DrawShadowed(string text, Vector2 pos, float size, Color color, bool bold = false, float offset = 2f)
    {
        var a = (byte)(color.A * 0.6f);
        Draw(text, pos + new Vector2(offset), size, new Color((byte)0, (byte)0, (byte)0, a), bold);
        Draw(text, pos, size, color, bold);
    }

    /// <summary>8 yonlu konturlu yazi: HUD sayilari ve tabela basliklari her zeminde okunur.</summary>
    public void DrawOutlined(string text, Vector2 pos, float size, Color color, Color outline, bool bold = false, float thickness = 2f)
    {
        var o = MathF.Max(1f, thickness);
        var d = o * 0.7071f;
        Span<Vector2> offs = [new(o, 0), new(-o, 0), new(0, o), new(0, -o), new(d, d), new(-d, d), new(d, -d), new(-d, -d)];
        var oc = new Color(outline.R, outline.G, outline.B, (byte)(outline.A * color.A / 255));
        foreach (var off in offs)
        {
            Draw(text, pos + off, size, oc, bold);
        }

        Draw(text, pos, size, color, bold);
    }

    public void Dispose()
    {
        foreach (var f in _regular.Concat(_bold))
        {
            if (f.Texture.Id != Raylib.GetFontDefault().Texture.Id)
            {
                Raylib.UnloadFont(f);
            }
        }
    }
}
