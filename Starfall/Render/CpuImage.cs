using System.Numerics;
using System.Runtime.InteropServices;
using Starfall.Core;
using StbTrueTypeSharp;

namespace Starfall.Render;

/// <summary>CPU'da uretilen RGBA goruntu; GPU dokusu ilk cizimde (pencere varsa) olusur.</summary>
public sealed class CpuImage
{
    public readonly int Width, Height;
    public readonly byte[] Rgba;
    internal Texture? Gpu;

    public CpuImage(int w, int h)
    {
        Width = w;
        Height = h;
        Rgba = new byte[w * h * 4];
    }

    /// <summary>Kaplamali (premultiplied olmayan) "uzerine ciz".</summary>
    public void Blend(int x, int y, Vector3 c, float a)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || a <= 0) return;
        int i = (y * Width + x) * 4;
        float da = Rgba[i + 3] / 255f;
        float oa = a + da * (1 - a);
        if (oa <= 0) return;
        for (int k = 0; k < 3; k++)
        {
            float dc = Rgba[i + k] / 255f;
            float sc = k == 0 ? c.X : k == 1 ? c.Y : c.Z;
            Rgba[i + k] = (byte)Math.Clamp((sc * a + dc * da * (1 - a)) / oa * 255f, 0, 255);
        }
        Rgba[i + 3] = (byte)Math.Clamp(oa * 255f, 0, 255);
    }
}

/// <summary>Prosedurel kucuk goruntuler (gorev isaretleri, harita pinleri).</summary>
public static unsafe class ImageGen
{
    private static StbTrueType.stbtt_fontinfo? _font;
    private static GCHandle _fontHandle;

    private static StbTrueType.stbtt_fontinfo Font()
    {
        if (_font != null) return _font;
        var bytes = Gfx.ReadResourceBytes("fonts/baloo800-latin.ttf");
        _fontHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        _font = new StbTrueType.stbtt_fontinfo();
        StbTrueType.stbtt_InitFont(_font, (byte*)_fontHandle.AddrOfPinnedObject(), 0);
        return _font;
    }

    private static Vector3 Srgb(string hex)
    {
        var v = MathX.HexSrgb(hex);
        return new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>Daire + beyaz cerceve + ortada sembol ('!', '?', '★', ...). 128x128.</summary>
    public static CpuImage Marker(string symbol, string color)
    {
        const int S = 128;
        var img = new CpuImage(S, S);
        var fill = Srgb(color);
        var white = Vector3.One;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = MathF.Sqrt((x + 0.5f - 64) * (x + 0.5f - 64) + (y + 0.5f - 64) * (y + 0.5f - 64));
            float outer = Math.Clamp(56.5f - d, 0, 1);       // cerceve dis kenari (52 + 4)
            float inner = Math.Clamp(48.5f - d, 0, 1);       // dolgu (52 - 4)
            if (outer > 0) img.Blend(x, y, white, outer);
            if (inner > 0) img.Blend(x, y, fill, inner);
        }
        if (symbol == "★") DrawStar(img, 64, 66, 36, 15, white);
        else DrawGlyph(img, symbol[0], 64, 70, 80, white);
        return img;
    }

    private static void DrawStar(CpuImage img, float cx, float cy, float ro, float ri, Vector3 c)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 10; i++)
        {
            float r = i % 2 == 0 ? ro : ri;
            float a = i / 10f * MathX.TwoPi - MathX.Pi / 2;
            pts.Add(new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r));
        }
        FillPolygon(img, pts, c);
    }

    public static void FillPolygon(CpuImage img, List<Vector2> pts, Vector3 c, int ss = 4)
    {
        float minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        for (int y = (int)MathF.Floor(minY); y <= (int)MathF.Ceiling(maxY); y++)
        for (int x = (int)MathF.Floor(minX); x <= (int)MathF.Ceiling(maxX); x++)
        {
            int hits = 0;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
                if (Inside(pts, x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss)) hits++;
            if (hits > 0) img.Blend(x, y, c, hits / (float)(ss * ss));
        }
    }

    private static bool Inside(List<Vector2> p, float x, float y)
    {
        bool c = false;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) c = !c;
        return c;
    }

    /// <summary>Tek karakteri (cx, baseline yakin cy) merkezli ciz.</summary>
    public static void DrawGlyph(CpuImage img, char ch, float cx, float cy, float px, Vector3 c)
    {
        var f = Font();
        float scale = StbTrueType.stbtt_ScaleForPixelHeight(f, px);
        int w, h, xo, yo;
        byte* bmp = StbTrueType.stbtt_GetCodepointBitmap(f, scale, scale, ch, &w, &h, &xo, &yo);
        if (bmp == null) return;
        int ox = (int)MathF.Round(cx - w / 2f), oy = (int)MathF.Round(cy - h / 2f - 4);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float a = bmp[y * w + x] / 255f;
            if (a > 0) img.Blend(ox + x, oy + y, c, a);
        }
        StbTrueType.stbtt_FreeBitmap(bmp, null);
    }
}
