using System.Numerics;
using Starfall.Render;

namespace Starfall.UI;

public enum Align { Left, Center, Right }

/// <summary>
/// Anlik (immediate) arayuz cizim listesi. Ekran piksel koordinatlari; renkler sRGB.
/// Her kare bastan kurulur; GPU'ya tek tamponla gider.
/// </summary>
public sealed class UiDrawList
{
    public const int Stride = 12; // pos2 uv2 rgba4 params4
    public float[] Data = new float[Stride * 6 * 2048];
    public int Count; // kose sayisi
    public readonly List<Cmd> Commands = new();
    public Font? Font;
    private Texture? _curTex;
    private (int X, int Y, int W, int H)? _curScissor;
    /// <summary>Sanal birimden piksele carpan (ekran yuksekligi 720 birim).</summary>
    public float Scale = 1;

    public struct Cmd
    {
        public int Start, Count;
        public Texture? Texture;
        public (int X, int Y, int W, int H)? Scissor;
    }

    public void Begin(float scale)
    {
        Count = 0;
        Commands.Clear();
        _curTex = null;
        _curScissor = null;
        Scale = scale;
    }

    private void Batch(Texture? tex)
    {
        if (Commands.Count == 0 || tex != _curTex || Commands[^1].Scissor != _curScissor)
        {
            _curTex = tex;
            Commands.Add(new Cmd { Start = Count, Count = 0, Texture = tex, Scissor = _curScissor });
        }
    }

    public void PushScissor(float x, float y, float w, float h)
    {
        _curScissor = ((int)(x * Scale), (int)(y * Scale), (int)(w * Scale), (int)(h * Scale));
        Commands.Add(new Cmd { Start = Count, Count = 0, Texture = _curTex, Scissor = _curScissor });
    }

    public void PopScissor()
    {
        _curScissor = null;
        Commands.Add(new Cmd { Start = Count, Count = 0, Texture = _curTex, Scissor = null });
    }

    private void Vtx(float x, float y, float u, float v, Vector4 c, Vector4 prm)
    {
        if ((Count + 1) * Stride > Data.Length) Array.Resize(ref Data, Data.Length * 2);
        int o = Count * Stride;
        Data[o] = x * Scale; Data[o + 1] = y * Scale; Data[o + 2] = u; Data[o + 3] = v;
        Data[o + 4] = c.X; Data[o + 5] = c.Y; Data[o + 6] = c.Z; Data[o + 7] = c.W;
        Data[o + 8] = prm.X; Data[o + 9] = prm.Y; Data[o + 10] = prm.Z; Data[o + 11] = prm.W;
        Count++;
    }

    private void Quad(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, Vector4 c, Vector4 prm)
    {
        Vtx(x0, y0, u0, v0, c, prm); Vtx(x1, y0, u1, v0, c, prm); Vtx(x1, y1, u1, v1, c, prm);
        Vtx(x0, y0, u0, v0, c, prm); Vtx(x1, y1, u1, v1, c, prm); Vtx(x0, y1, u0, v1, c, prm);
        var cmd = Commands[^1];
        cmd.Count += 6;
        Commands[^1] = cmd;
    }

    /// <summary>Yuvarlatilmis dikdortgen (SDF, kenarlari yumusak).</summary>
    public void Rect(float x, float y, float w, float h, Vector4 color, float radius = 0)
    {
        if (color.W <= 0.001f || w <= 0 || h <= 0) return;
        Batch(_curTex);
        float hw = w / 2 * Scale, hh = h / 2 * Scale;
        float r = MathF.Min(radius * Scale, MathF.Min(hw, hh));
        Quad(x, y, x + w, y + h, -hw, -hh, hw, hh, color, new Vector4(0, hw, hh, r));
    }

    public void Shadow(float x, float y, float w, float h, float radius, float alpha = 0.28f)
    {
        Batch(_curTex);
        float g = 18;
        float hw = w / 2 * Scale, hh = h / 2 * Scale;
        float pg = g * Scale;
        Quad(x - g, y - g + 6, x + w + g, y + h + g + 6, -hw - pg, -hh - pg, hw + pg, hh + pg,
            new Vector4(0.16f, 0.1f, 0.05f, alpha), new Vector4(3, hw, hh, MathF.Min(radius * Scale, MathF.Min(hw, hh))));
    }

    public void Image(Texture tex, float x, float y, float w, float h, Vector4? tint = null)
    {
        Batch(tex);
        Quad(x, y, x + w, y + h, 0, 0, 1, 1, tint ?? Vector4.One, new Vector4(1, 0, 0, 0));
    }

    /// <summary>Metin ciz; (x, y) ust-sol (veya hizaya gore). Genisligi dondurur.</summary>
    public float Text(string s, float x, float y, float px, Vector4 color, FontKind k = FontKind.Body, Align align = Align.Left, float shadow = 0)
    {
        if (Font == null || string.IsNullOrEmpty(s)) return 0;
        float w = Font.Measure(s, px, k);
        if (align == Align.Center) x -= w / 2;
        else if (align == Align.Right) x -= w;
        if (shadow > 0) DrawText(s, x, y + shadow * 1.5f, px, new Vector4(0, 0, 0, color.W * 0.45f), k);
        DrawText(s, x, y, px, color, k);
        return w;
    }

    private void DrawText(string s, float x, float y, float px, Vector4 color, FontKind k)
    {
        var f = Font!;
        float sc = px / Font.BasePx;
        float baseline = y + f.Ascent[(int)k] * sc;
        float pen = x;
        int prev = 0;
        Batch(_curTex);
        foreach (var r in s.EnumerateRunes())
        {
            int cp = r.Value;
            var g = f.Get(k, cp);
            if (prev != 0) pen += f.Kern(k, prev, cp) * sc;
            if (g.W > 0)
            {
                float x0 = pen + g.XOff * sc, y0 = baseline + g.YOff * sc;
                Quad(x0, y0, x0 + g.W * sc, y0 + g.H * sc, g.U0, g.V0, g.U1, g.V1, color, new Vector4(2, 0, 0, 0));
            }
            pen += g.Advance * sc;
            prev = cp;
        }
    }

    /// <summary>Satir kaydirmali paragraf; toplam yuksekligi dondurur.</summary>
    public float Paragraph(string s, float x, float y, float maxW, float px, Vector4 color, FontKind k = FontKind.Body, float lineMul = 1.35f, Align align = Align.Left)
    {
        if (Font == null) return 0;
        float lh = px * lineMul;
        var lines = Font.Wrap(s, px, maxW, k);
        for (int i = 0; i < lines.Count; i++)
        {
            float lx = align == Align.Center ? x + maxW / 2 : align == Align.Right ? x + maxW : x;
            Text(lines[i], lx, y + i * lh, px, color, k, align);
        }
        return lines.Count * lh;
    }

    public float Measure(string s, float px, FontKind k = FontKind.Body) => Font?.Measure(s, px, k) ?? 0;
}
