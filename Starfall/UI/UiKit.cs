using System.Numerics;
using Starfall.Core;
using Starfall.Render;

namespace Starfall.UI;

/// <summary>Arayuz renkleri (ui.css degiskenlerinin karsiligi, sRGB).</summary>
public static class C
{
    public static readonly Vector4 Cream = MathX.HexSrgb("#fff8ec");
    public static readonly Vector4 Cream2 = MathX.HexSrgb("#fbeed8");
    public static readonly Vector4 Ink = MathX.HexSrgb("#3a2e2a");
    public static readonly Vector4 InkSoft = MathX.HexSrgb("#7a6a60");
    public static readonly Vector4 Gold = MathX.HexSrgb("#ffb627");
    public static readonly Vector4 Gold2 = MathX.HexSrgb("#ffd36b");
    public static readonly Vector4 Teal = MathX.HexSrgb("#2fb5a8");
    public static readonly Vector4 Teal2 = MathX.HexSrgb("#6fdad0");
    public static readonly Vector4 Rose = MathX.HexSrgb("#ef6b8a");
    public static readonly Vector4 Sky = MathX.HexSrgb("#4fb4ff");
    public static readonly Vector4 White = Vector4.One;
    public static readonly Vector4 Track = MathX.HexSrgb("#e7dccb");
    public static readonly Vector4 Dark = new(30 / 255f, 24 / 255f, 40 / 255f, 0.75f);

    public static Vector4 A(Vector4 c, float a) => c with { W = c.W * a };
    public static Vector4 Hex(string h, float a = 1) => MathX.HexSrgb(h, a);
}

/// <summary>Gomulu PNG'lerden UI dokulari (yalnizca pencere varken yuklenir).</summary>
public static class UiTex
{
    private static readonly Dictionary<string, Texture?> Cache = new();

    public static Texture? Get(string path)
    {
        if (Gfx.GL == null) return null;
        if (Cache.TryGetValue(path, out var t)) return t;
        try { t = Gfx.HasResource(path) ? Texture.FromPngBytes(Gfx.ReadResourceBytes(path)) : null; }
        catch { t = null; }
        return Cache[path] = t;
    }

    public static Texture? Of(CpuImage img)
    {
        if (Gfx.GL == null) return null;
        return img.Gpu ??= Texture.Rgba8(img.Width, img.Height, img.Rgba, true, true);
    }
}

/// <summary>Bir karelik cizim baglami: sanal birimler (ekran yuksekligi 720).</summary>
public sealed class UiCtx
{
    public UiDrawList D = null!;
    public float W, H = 720, Ts = 1, Time;
    public Vector2 Mouse;
    public bool Click, MouseMoved;
    public float Wheel;
    public Game G = null!;

    public float Px(float css) => css * Ts;

    /// <summary>Tus kapagi: [glyph]. Genisligi dondurur.</summary>
    public float KeyCap(float x, float y, string glyph, float alpha = 1)
    {
        bool pad = G.Input.Device == "pad";
        float fs = Px(13.5f);
        float tw = D.Measure(glyph, fs, FontKind.Strong);
        float h = 32;
        float w = pad ? MathF.Max(32, tw + 12) : MathF.Max(32, tw + 16);
        D.Rect(x, y + 3, w, h, C.A(C.Hex("#1b1412"), alpha), pad ? h / 2 : 10);
        D.Rect(x, y, w, h, C.A(C.Ink, alpha), pad ? h / 2 : 10);
        D.Text(glyph, x + w / 2, y + h / 2 - fs * 0.49f, fs, C.A(C.White, alpha), FontKind.Strong, Align.Center);
        return w;
    }

    public bool Hover(float x, float y, float w, float h) => Mouse.X >= x && Mouse.X <= x + w && Mouse.Y >= y && Mouse.Y <= y + h;

    /// <summary>Krem panel + beyaz cerceve + golge.</summary>
    public void Panel(float x, float y, float w, float h, float r = 28)
    {
        D.Shadow(x, y, w, h, r, 0.3f);
        D.Rect(x - 4, y - 4, w + 8, h + 8, C.White, r + 4);
        D.Rect(x, y, w, h, C.Cream, r);
    }

    public void Pill(float x, float y, float w, float h, Vector4 c) => D.Rect(x, y, w, h, c, h / 2);

    /// <summary>Alt bilgi satiri: [tus] etiket ciftleri, saga yasli. Sol kenar x'i dondurur.</summary>
    public float Footer(float right, float y, params (string action, string label)[] pairs)
    {
        float fs = Px(14.5f);
        float total = 0;
        var widths = new List<(float kw, float lw)>();
        foreach (var (a, l) in pairs)
        {
            float kw = MeasureKey(G.Input.Glyph(a));
            float lw = D.Measure(l, fs, FontKind.Strong);
            widths.Add((kw, lw));
            total += kw + 6 + lw + 18;
        }
        float x = right - total + 18;
        float start = x;
        for (int i = 0; i < pairs.Length; i++)
        {
            float kw = KeyCap(x, y, G.Input.Glyph(pairs[i].action));
            x += kw + 6;
            D.Text(pairs[i].label, x, y + 16 - fs * 0.49f, fs, C.InkSoft, FontKind.Strong);
            x += widths[i].lw + 18;
        }
        return start;
    }

    public float MeasureKey(string glyph)
    {
        bool pad = G.Input.Device == "pad";
        float tw = D.Measure(glyph, Px(13.5f), FontKind.Strong);
        return pad ? MathF.Max(32, tw + 12) : MathF.Max(32, tw + 16);
    }

    /// <summary>Dikey ortalanmis tek satir metin icin y (ust kenar).</summary>
    public static float Mid(float top, float h, float fs) => top + h / 2 - fs * 0.49f;
}

/// <summary>Odak gezinmeli ekranin bir ogesi.</summary>
public sealed class UiItem
{
    public string Label = "";
    public Action? Select, Left, Right;
    public bool Disabled;
    public object? Tag;
    public float X, Y, W, H;    // son cizimdeki yeri (fare icin)
    public bool Drawn;
}

/// <summary>
/// Odak gezinmeli ekran tabani. Ogeler Rebuild()'de kurulur (cizimden bagimsiz: testler
/// pencere olmadan da menuyu gezebilir); Draw() yerlerini atar.
/// </summary>
public abstract class UiScreen
{
    protected readonly UiManager Ui;
    protected Game G => Ui.G;
    public readonly List<UiItem> Items = new();
    public int Index;
    public int Columns = 1;
    public bool Dim = true;
    public float Scroll, ScrollMax;
    private float _appear;

    protected UiScreen(UiManager ui) => Ui = ui;

    public virtual void Mount() => Refresh();

    public virtual void Unmount() { }

    public void Refresh()
    {
        int keep = Index;
        Items.Clear();
        Rebuild();
        Index = Items.Count == 0 ? 0 : Math.Clamp(keep, 0, Items.Count - 1);
    }

    protected abstract void Rebuild();

    public UiItem Add(string label, Action? select = null, Action? left = null, Action? right = null, bool disabled = false, object? tag = null)
    {
        var it = new UiItem { Label = label, Select = select, Left = left, Right = right, Disabled = disabled, Tag = tag };
        Items.Add(it);
        return it;
    }

    public void Focus(int i, bool silent = false)
    {
        if (Items.Count == 0) return;
        i = Math.Clamp(i, 0, Items.Count - 1);
        if (i != Index && !silent) G.Audio.Sfx("uiMove");
        Index = i;
    }

    public void Activate(UiItem it, float? clickX = null)
    {
        if (it.Disabled) { G.Audio.Sfx("error"); return; }
        if (it.Select != null)
        {
            G.Audio.Sfx("uiConfirm");
            it.Select();
        }
        else if (it.Right != null && clickX is float cx)
        {
            if (cx < it.X + it.W / 2 && it.Left != null) it.Left(); else it.Right();
            G.Audio.Sfx("uiMove");
        }
    }

    public virtual void Handle(Input input)
    {
        var it = Index < Items.Count ? Items[Index] : null;
        if (input.Pressed("up")) Focus(Index - Columns);
        else if (input.Pressed("down")) Focus(Index + Columns);
        else if (input.Pressed("left"))
        {
            if (it?.Left != null) { it.Left(); G.Audio.Sfx("uiMove"); }
            else if (Columns > 1) Focus(Index - 1);
        }
        else if (input.Pressed("right"))
        {
            if (it?.Right != null) { it.Right(); G.Audio.Sfx("uiMove"); }
            else if (Columns > 1) Focus(Index + 1);
        }
        else if (input.Pressed("confirm") && it != null) Activate(it);
        else if (input.Pressed("back")) OnBack();
        OnInput(input);
    }

    protected virtual void OnInput(Input input) { }

    public virtual void OnBack()
    {
        G.Audio.Sfx("uiBack");
        Ui.Pop();
    }

    /// <summary>Fare: uzerine gelinen ogeye odaklan, tiklananı calistir.</summary>
    public void Mouse(UiCtx c)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i];
            if (!it.Drawn || !c.Hover(it.X, it.Y, it.W, it.H)) continue;
            if (c.MouseMoved) Focus(i, true);
            if (c.Click) { Focus(i, true); Activate(it, c.Mouse.X); }
            break;
        }
    }

    public void Update(float dt) => _appear = MathF.Min(1, _appear + dt * 5);

    public float Appear => 1 - MathF.Pow(1 - _appear, 3);

    public abstract void Draw(UiCtx c);

    /// <summary>Odakli ogeyi kaydirilabilir alanin icinde tut.</summary>
    protected void KeepVisible(float viewTop, float viewH, float itemTop, float itemH)
    {
        if (itemTop - Scroll < viewTop) Scroll = itemTop - viewTop;
        if (itemTop + itemH - Scroll > viewTop + viewH) Scroll = itemTop + itemH - viewTop - viewH;
        Scroll = Math.Clamp(Scroll, 0, MathF.Max(0, ScrollMax));
    }

    public static void DimBg(UiCtx c, float a = 1)
    {
        c.D.Rect(0, 0, c.W, c.H, new Vector4(14 / 255f, 12 / 255f, 28 / 255f, 0.55f * a));
    }
}
