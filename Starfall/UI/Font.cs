using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using StbTrueTypeSharp;
using Starfall.Render;

namespace Starfall.UI;

public enum FontKind { Body = 0, Strong = 1, Title = 2 }

public struct Glyph
{
    public float U0, V0, U1, V1;
    public float W, H, XOff, YOff; // taban buyuklukte piksel
    public float Advance;
}

/// <summary>
/// Isaretli mesafe alani (SDF) yazi atlasi: tek dokuda uc yazi tipi, her boyutta
/// keskin. Yazi tiplerinde olmayan semboller (yildiz, oklar, onay) kodla cizilir.
/// GL olmadan da olcum yapar (testler ve yerlesim icin).
/// </summary>
public sealed unsafe class Font
{
    public const int AtlasSize = 2048;
    public const float BasePx = 44f;
    private const int Pad = 6;
    private const byte OnEdge = 128;
    private const float DistScale = 128f / Pad;

    private readonly Dictionary<(int, int), Glyph> _glyphs = new();
    private readonly Dictionary<(int, int, int), float> _kern = new();
    private readonly byte[] _atlas = new byte[AtlasSize * AtlasSize];
    private int _penX = 1, _penY = 1, _rowH;
    private readonly List<FaceSet> _faces = new();
    public readonly float[] Ascent = new float[3], Descent = new float[3], LineGap = new float[3];
    private uint _tex;
    private bool _dirty = true;

    private sealed class Face
    {
        public StbTrueType.stbtt_fontinfo Info = new();
        public GCHandle Handle;
        public float Scale;
    }

    private sealed class FaceSet
    {
        public readonly List<Face> Faces = new();
    }

    public Font()
    {
        string[][] files =
        {
            new[] { "fonts/nunito700-latin.ttf", "fonts/nunito700-latin-ext.ttf" },
            new[] { "fonts/nunito800-latin.ttf", "fonts/nunito800-latin-ext.ttf" },
            new[] { "fonts/baloo800-latin.ttf", "fonts/baloo800-latin-ext.ttf" },
        };
        for (int k = 0; k < files.Length; k++)
        {
            var set = new FaceSet();
            foreach (var f in files[k])
            {
                var bytes = Gfx.ReadResourceBytes(f);
                var face = new Face { Handle = GCHandle.Alloc(bytes, GCHandleType.Pinned) };
                byte* p = (byte*)face.Handle.AddrOfPinnedObject();
                if (StbTrueType.stbtt_InitFont(face.Info, p, 0) == 0) throw new Exception($"yazi tipi acilamadi: {f}");
                face.Scale = StbTrueType.stbtt_ScaleForPixelHeight(face.Info, BasePx);
                set.Faces.Add(face);
            }
            _faces.Add(set);
            var f0 = set.Faces[0];
            int asc, desc, gap;
            StbTrueType.stbtt_GetFontVMetrics(f0.Info, &asc, &desc, &gap);
            Ascent[k] = asc * f0.Scale;
            Descent[k] = desc * f0.Scale;
            LineGap[k] = gap * f0.Scale;
        }
        // sik kullanilanlari onceden hazirla
        const string pre = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~çÇğĞıİöÖşŞüÜâÂêîû·—–…“”‘’";
        for (int k = 0; k < 3; k++) foreach (var c in pre) Get((FontKind)k, c);
    }

    public Glyph Get(FontKind kind, int cp)
    {
        var key = ((int)kind, cp);
        if (_glyphs.TryGetValue(key, out var g)) return g;
        g = Bake(kind, cp);
        _glyphs[key] = g;
        return g;
    }

    private Glyph Bake(FontKind kind, int cp)
    {
        if (cp == ' ' || cp == ' ')
        {
            var sf = _faces[(int)kind].Faces[0];
            int adv, lsb;
            StbTrueType.stbtt_GetCodepointHMetrics(sf.Info, ' ', &adv, &lsb);
            return new Glyph { Advance = adv * sf.Scale };
        }
        foreach (var face in _faces[(int)kind].Faces)
        {
            if (StbTrueType.stbtt_FindGlyphIndex(face.Info, cp) == 0) continue;
            int w, h, xo, yo;
            byte* sdf = StbTrueType.stbtt_GetCodepointSDF(face.Info, face.Scale, cp, Pad, OnEdge, DistScale, &w, &h, &xo, &yo);
            int adv, lsb;
            StbTrueType.stbtt_GetCodepointHMetrics(face.Info, cp, &adv, &lsb);
            var g = new Glyph { Advance = adv * face.Scale };
            if (sdf != null && w > 0 && h > 0)
            {
                var buf = new byte[w * h];
                for (int i = 0; i < buf.Length; i++) buf[i] = sdf[i];
                StbTrueType.stbtt_FreeSDF(sdf, null);
                Place(buf, w, h, ref g, xo, yo);
            }
            return g;
        }
        var custom = CustomGlyph(cp);
        if (custom != null)
        {
            var (buf, w, h, xo, yo, adv) = custom.Value;
            var g = new Glyph { Advance = adv };
            Place(buf, w, h, ref g, xo, yo);
            return g;
        }
        return cp == '?' ? new Glyph { Advance = BasePx * 0.5f } : Get(kind, '?');
    }

    private void Place(byte[] buf, int w, int h, ref Glyph g, int xo, int yo)
    {
        if (_penX + w + 1 >= AtlasSize)
        {
            _penX = 1;
            _penY += _rowH + 1;
            _rowH = 0;
        }
        if (_penY + h + 1 >= AtlasSize) return; // atlas doldu (pratikte olmaz)
        for (int y = 0; y < h; y++)
            System.Buffer.BlockCopy(buf, y * w, _atlas, (_penY + y) * AtlasSize + _penX, w);
        g.U0 = _penX / (float)AtlasSize;
        g.V0 = _penY / (float)AtlasSize;
        g.U1 = (_penX + w) / (float)AtlasSize;
        g.V1 = (_penY + h) / (float)AtlasSize;
        g.W = w; g.H = h; g.XOff = xo; g.YOff = yo;
        _penX += w + 1;
        _rowH = Math.Max(_rowH, h);
        _dirty = true;
    }

    // ---- kodla cizilen semboller (SDF) ----
    private static (byte[], int, int, int, int, float)? CustomGlyph(int cp)
    {
        List<Vector2>? poly = null;
        List<Vector2>? line = null;
        float size = BasePx * 0.62f;
        float thick = 0;
        switch (cp)
        {
            case '★':
            {
                poly = new List<Vector2>();
                for (int i = 0; i < 10; i++)
                {
                    float r = i % 2 == 0 ? 0.5f : 0.21f;
                    float a = -MathF.PI / 2 + i * MathF.PI / 5;
                    poly.Add(new Vector2(0.5f + MathF.Cos(a) * r, 0.53f + MathF.Sin(a) * r));
                }
                break;
            }
            case '▼': poly = new() { new(0.1f, 0.25f), new(0.9f, 0.25f), new(0.5f, 0.85f) }; break;
            case '◀': poly = new() { new(0.8f, 0.12f), new(0.8f, 0.88f), new(0.15f, 0.5f) }; break;
            case '▶': poly = new() { new(0.2f, 0.12f), new(0.85f, 0.5f), new(0.2f, 0.88f) }; break;
            case '→': poly = Arrow(0); break;
            case '←': poly = Arrow(MathF.PI); break;
            case '↑': poly = Arrow(-MathF.PI / 2); break;
            case '↓': poly = Arrow(MathF.PI / 2); break;
            case '↖': poly = Arrow(-MathF.PI * 0.75f); break;
            case '✓': line = new() { new(0.12f, 0.52f), new(0.4f, 0.8f), new(0.9f, 0.2f) }; thick = 0.11f; break;
            case '✥': poly = Cross(); break;
            case '☰': return Bars();
            case '⧉': line = new() { new(0.15f, 0.3f), new(0.65f, 0.3f), new(0.65f, 0.85f), new(0.15f, 0.85f), new(0.15f, 0.3f) }; thick = 0.07f; break;
            default: return null;
        }
        int px = (int)size + Pad * 2;
        var buf = new byte[px * px];
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                var p = new Vector2((x + 0.5f - Pad) / size, (y + 0.5f - Pad) / size);
                float d = poly != null ? PolyDist(poly, p) : LineDist(line!, p) - thick;
                float v = OnEdge - d * size * DistScale;
                buf[y * px + x] = (byte)Math.Clamp(v, 0, 255);
            }
        }
        // taban cizgisine gore: ust kenar ascent'in ~%75'inde
        int yo = -(int)(size * 0.92f) - Pad;
        return (buf, px, px, -Pad, yo, size * 1.08f);
    }

    private static List<Vector2> Arrow(float ang)
    {
        var pts = new List<Vector2> { new(-0.45f, -0.1f), new(0.05f, -0.1f), new(0.05f, -0.3f), new(0.45f, 0f), new(0.05f, 0.3f), new(0.05f, 0.1f), new(-0.45f, 0.1f) };
        float c = MathF.Cos(ang), s = MathF.Sin(ang);
        return pts.Select(p => new Vector2(0.5f + p.X * c - p.Y * s, 0.5f + p.X * s + p.Y * c)).ToList();
    }

    private static List<Vector2> Cross()
    {
        float a = 0.17f, b = 0.45f;
        return new() { new(0.5f - a, 0.5f - b), new(0.5f + a, 0.5f - b), new(0.5f + a, 0.5f - a), new(0.5f + b, 0.5f - a), new(0.5f + b, 0.5f + a), new(0.5f + a, 0.5f + a),
            new(0.5f + a, 0.5f + b), new(0.5f - a, 0.5f + b), new(0.5f - a, 0.5f + a), new(0.5f - b, 0.5f + a), new(0.5f - b, 0.5f - a), new(0.5f - a, 0.5f - a) };
    }

    private static (byte[], int, int, int, int, float) Bars()
    {
        float size = BasePx * 0.62f;
        int px = (int)size + Pad * 2;
        var buf = new byte[px * px];
        for (int y = 0; y < px; y++)
        {
            for (int x = 0; x < px; x++)
            {
                var p = new Vector2((x + 0.5f - Pad) / size, (y + 0.5f - Pad) / size);
                float d = float.MaxValue;
                foreach (float yy in new[] { 0.25f, 0.5f, 0.75f })
                    d = MathF.Min(d, SegDist(p, new(0.12f, yy), new(0.88f, yy)) - 0.07f);
                buf[y * px + x] = (byte)Math.Clamp(OnEdge - d * size * DistScale, 0, 255);
            }
        }
        return (buf, px, px, -Pad, -(int)(size * 0.92f) - Pad, size * 1.08f);
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float LineDist(List<Vector2> pts, Vector2 p)
    {
        float d = float.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++) d = MathF.Min(d, SegDist(p, pts[i], pts[i + 1]));
        return d;
    }

    private static float PolyDist(List<Vector2> poly, Vector2 p)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            d = MathF.Min(d, SegDist(p, a, b));
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside ? -d : d;
    }

    // ---- olcum ----
    public float Kern(FontKind k, int a, int b)
    {
        var key = ((int)k, a, b);
        if (_kern.TryGetValue(key, out float v)) return v;
        var face = _faces[(int)k].Faces[0];
        if (StbTrueType.stbtt_FindGlyphIndex(face.Info, a) == 0 || StbTrueType.stbtt_FindGlyphIndex(face.Info, b) == 0) v = 0;
        else v = StbTrueType.stbtt_GetCodepointKernAdvance(face.Info, a, b) * face.Scale;
        _kern[key] = v;
        return v;
    }

    public float Measure(string s, float px, FontKind k = FontKind.Body)
    {
        float sc = px / BasePx;
        float w = 0, maxW = 0;
        int prev = 0;
        foreach (var r in s.EnumerateRunes())
        {
            int cp = r.Value;
            if (cp == '\n') { maxW = MathF.Max(maxW, w); w = 0; prev = 0; continue; }
            var g = Get(k, cp);
            if (prev != 0) w += Kern(k, prev, cp) * sc;
            w += g.Advance * sc;
            prev = cp;
        }
        return MathF.Max(maxW, w);
    }

    /// <summary>Metni verilen genislige gore satirlara boler.</summary>
    public List<string> Wrap(string text, float px, float maxWidth, FontKind k = FontKind.Body)
    {
        var lines = new List<string>();
        foreach (var para in text.Split('\n'))
        {
            var words = para.Split(' ');
            string cur = "";
            foreach (var w in words)
            {
                string test = cur.Length == 0 ? w : cur + " " + w;
                if (Measure(test, px, k) > maxWidth && cur.Length > 0)
                {
                    lines.Add(cur);
                    cur = w;
                }
                else cur = test;
            }
            lines.Add(cur);
        }
        return lines;
    }

    public float LineHeight(float px, FontKind k = FontKind.Body) => (Ascent[(int)k] - Descent[(int)k] + LineGap[(int)k]) * px / BasePx;

    // ---- GPU ----
    public void Bind(GL gl, int unit)
    {
        if (_tex == 0)
        {
            _tex = gl.GenTexture();
            _dirty = true;
        }
        gl.ActiveTexture(TextureUnit.Texture0 + unit);
        gl.BindTexture(TextureTarget.Texture2D, _tex);
        if (_dirty)
        {
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* p = _atlas)
                gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, AtlasSize, AtlasSize, 0, PixelFormat.Red, PixelType.UnsignedByte, p);
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _dirty = false;
        }
    }
}
