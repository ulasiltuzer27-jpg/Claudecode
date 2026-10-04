using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Engine.Ui;

public enum Align
{
    Left,
    Center,
    Right,
}

public enum ButtonStyle
{
    Normal,
    Primary,
    Danger,
    Ghost,
    Tab,
    TabActive,
}

/// <summary>Arayuz renkleri: sicak, esnaf tabelasi tonlari.</summary>
public static class Theme
{
    public static readonly Color Primary = Gfx.Hex(0xE8792E);
    public static readonly Color PrimaryDark = Gfx.Hex(0xB9541A);
    public static readonly Color Cream = Gfx.Hex(0xFFF4E2);
    public static readonly Color CreamDark = Gfx.Hex(0xF1DFC4);
    public static readonly Color Ink = Gfx.Hex(0x3B2A1E);
    public static readonly Color InkSoft = Gfx.Hex(0x7A6452);
    public static readonly Color Green = Gfx.Hex(0x4E9F4A);
    public static readonly Color Red = Gfx.Hex(0xD24C3E);
    public static readonly Color Yellow = Gfx.Hex(0xF2C14E);
    public static readonly Color Blue = Gfx.Hex(0x3C7FB1);
    public static readonly Color HudBg = new(24, 18, 14, 170);
    public static readonly Color HudBgStrong = new(24, 18, 14, 215);
    public static readonly Color White = Color.White;
    public static readonly Color Shade = new(0, 0, 0, 140);
}

/// <summary>
/// Anlik kip (immediate-mode) arayuz. Her kare bastan cizilir; durum
/// cagiranin elinde (<c>ref</c> parametreler).
///
/// Klavye/gamepad gezinme: odaklanabilir her bilesen cagri sirasina gore
/// numara alir. Yukari/Asagi odagi tasir, Enter/A etkinlestirir,
/// Sol/Sag kaydirici ve seciciyi degistirir. Fare uzerine gelince odak
/// oraya gecer. Boylece tum menuler fare olmadan da kullanilabiliyor.
///
/// Olcek: tasarim 1080p icin yapildi; <see cref="Scale"/> ekran
/// yuksekligine ve oyuncunun arayuz olcegi ayarina gore carpar.
/// </summary>
public sealed class UiContext
{
    public Fonts Fonts { get; }
    public InputSystem Input { get; }

    public float Scale { get; private set; } = 1f;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool Interactive { get; private set; }

    private int _index;
    private int _count;
    private int _focus;
    private int _active = -1;
    private Vector2 _lastMouse;
    private bool _mouseMoved;
    private int _textFocus = -1;
    private bool _navUp, _navDown, _navLeft, _navRight, _navAccept;
    private float _navRepeat;

    /// <summary>Bu kare fare bir bilesenin ustundeydi (alttaki oyun tiklamayi almasin).</summary>
    public bool MouseOverUi { get; private set; }

    public UiContext(Fonts fonts, InputSystem input)
    {
        Fonts = fonts;
        Input = input;
    }

    public float S(float v) => v * Scale;

    public void Begin(int width, int height, float uiScale, bool interactive, float dt)
    {
        Width = width;
        Height = height;
        Scale = height / 1080f * uiScale;
        Interactive = interactive;
        _count = _index;
        _index = 0;
        MouseOverUi = false;
        if (_count > 0)
        {
            _focus = Math.Clamp(_focus, 0, _count - 1);
        }

        var mouse = Input.MousePosition;
        _mouseMoved = Vector2.DistanceSquared(mouse, _lastMouse) > 4f;
        _lastMouse = mouse;

        var pad = Input.Source.GamepadAvailable;
        bool Key(KeyboardKey k) => Input.KeyPressed(k);
        _navUp = Key(KeyboardKey.Up);
        _navDown = Key(KeyboardKey.Down);
        _navLeft = Key(KeyboardKey.Left);
        _navRight = Key(KeyboardKey.Right);
        _navAccept = Key(KeyboardKey.Enter) || Key(KeyboardKey.KpEnter);
        if (pad)
        {
            _navRepeat -= dt;
            var ly = Input.Source.Axis(GamepadAxis.LeftY);
            var lx = Input.Source.Axis(GamepadAxis.LeftX);
            var up = Input.Source.IsPadDown(GamepadButton.LeftFaceUp) || ly < -0.6f;
            var down = Input.Source.IsPadDown(GamepadButton.LeftFaceDown) || ly > 0.6f;
            var left = Input.Source.IsPadDown(GamepadButton.LeftFaceLeft) || lx < -0.6f;
            var right = Input.Source.IsPadDown(GamepadButton.LeftFaceRight) || lx > 0.6f;
            if ((up || down || left || right) && _navRepeat <= 0)
            {
                _navUp |= up;
                _navDown |= down;
                _navLeft |= left;
                _navRight |= right;
                _navRepeat = 0.18f;
            }
            else if (!(up || down || left || right))
            {
                _navRepeat = 0;
            }

            if (Input.Source.IsPadDown(GamepadButton.RightFaceDown) && !_padAcceptPrev)
            {
                _navAccept = true;
            }

            _padAcceptPrev = Input.Source.IsPadDown(GamepadButton.RightFaceDown);
        }

        if (_textFocus < 0 && _count > 0)
        {
            if (_navDown)
            {
                _focus = (_focus + 1) % _count;
            }

            if (_navUp)
            {
                _focus = (_focus - 1 + _count) % _count;
            }
        }

        if (!Input.MouseDown())
        {
            _active = -1;
        }

        Input.TextCapture = _textFocus >= 0;
    }

    private bool _padAcceptPrev;

    public void End()
    {
        if (_textFocus >= _index)
        {
            _textFocus = -1;
        }
    }

    /// <summary>Menu acildiginda odagi ilk bilesene al.</summary>
    public void ResetFocus(int index = 0)
    {
        _focus = index;
        _textFocus = -1;
    }

    private int NextId(Rectangle r, out bool hovered, out bool focused)
    {
        var id = _index++;
        hovered = Interactive && Raylib.CheckCollisionPointRec(Input.MousePosition, r);
        if (hovered)
        {
            MouseOverUi = true;
            if (_mouseMoved)
            {
                _focus = id;
            }
        }

        focused = Interactive && _focus == id;
        return id;
    }

    // ── Cizim ilkelleri ──────────────────────────────────────────────

    public void Panel(Rectangle r, Color color, float radius = 14f)
    {
        var rr = Roundness(r, S(radius));
        Raylib.DrawRectangleRounded(r, rr, 8, color);
    }

    public void PanelOutline(Rectangle r, Color color, float thickness = 2f, float radius = 14f)
    {
        Raylib.DrawRectangleRoundedLinesEx(r, Roundness(r, S(radius)), 8, S(thickness), color);
    }

    public static float Roundness(Rectangle r, float radiusPx)
    {
        var m = MathF.Min(r.Width, r.Height);
        return m <= 0 ? 0 : Math.Clamp(radiusPx * 2f / m, 0f, 1f);
    }

    public Vector2 Measure(string text, float size, bool bold = false) => Fonts.Measure(text, S(size), bold);

    /// <summary>Metin; boyut 1080p tasarim pikseli cinsinden.</summary>
    public void Text(string text, Vector2 pos, float size, Color color, bool bold = false, Align align = Align.Left, bool shadow = false)
    {
        var px = S(size);
        if (align != Align.Left)
        {
            var w = Fonts.Measure(text, px, bold).X;
            pos.X -= align == Align.Center ? w / 2 : w;
        }

        if (shadow)
        {
            Fonts.DrawShadowed(text, pos, px, color, bold, MathF.Max(1f, S(2)));
        }
        else
        {
            Fonts.Draw(text, pos, px, color, bold);
        }
    }

    /// <summary>Dikdortgen icinde dikey ortali metin.</summary>
    public void TextIn(Rectangle r, string text, float size, Color color, bool bold = false, Align align = Align.Left, float padding = 12f)
    {
        var px = S(size);
        var m = Fonts.Measure(text, px, bold);
        var x = align switch
        {
            Align.Center => r.X + (r.Width - m.X) / 2,
            Align.Right => r.X + r.Width - m.X - S(padding),
            _ => r.X + S(padding),
        };
        Fonts.Draw(text, new Vector2(x, r.Y + (r.Height - m.Y) / 2), px, color, bold);
    }

    /// <summary>Genislige sigan satirlara boler.</summary>
    public List<string> Wrap(string text, float size, float maxWidth, bool bold = false)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var words = paragraph.Split(' ');
            var line = "";
            foreach (var w in words)
            {
                var test = line.Length == 0 ? w : line + " " + w;
                if (Measure(test, size, bold).X > maxWidth && line.Length > 0)
                {
                    lines.Add(line);
                    line = w;
                }
                else
                {
                    line = test;
                }
            }

            lines.Add(line);
        }

        return lines;
    }

    /// <summary>Sarili paragraf cizer; kullanilan yuksekligi dondurur.</summary>
    public float Paragraph(string text, Vector2 pos, float size, float maxWidth, Color color, bool bold = false, float lineGap = 1.25f)
    {
        var y = pos.Y;
        foreach (var line in Wrap(text, size, maxWidth, bold))
        {
            Text(line, new Vector2(pos.X, y), size, color, bold);
            y += S(size * lineGap);
        }

        return y - pos.Y;
    }

    public void Bar(Rectangle r, float t, Color fill, Color? back = null)
    {
        Raylib.DrawRectangleRounded(r, Roundness(r, r.Height / 2), 6, back ?? new Color(0, 0, 0, 110));
        if (t > 0.001f)
        {
            var f = r with { Width = r.Width * Math.Clamp(t, 0f, 1f) };
            Raylib.DrawRectangleRounded(f, Roundness(f, r.Height / 2), 6, fill);
        }
    }

    // ── Bilesenler ───────────────────────────────────────────────────

    public bool Button(Rectangle r, string label, ButtonStyle style = ButtonStyle.Normal, bool enabled = true, float textSize = 28f)
    {
        var id = NextId(r, out var hovered, out var focused);
        var clicked = false;
        if (enabled && Interactive)
        {
            if (hovered && Input.MousePressed())
            {
                _active = id;
                clicked = true;
            }

            if (focused && _navAccept)
            {
                clicked = true;
            }
        }

        var hot = enabled && (hovered || focused);
        Color bg, fg;
        switch (style)
        {
            case ButtonStyle.Primary:
                bg = hot ? Theme.PrimaryDark : Theme.Primary;
                fg = Theme.White;
                break;
            case ButtonStyle.Danger:
                bg = hot ? Gfx.Hex(0xA8382C) : Theme.Red;
                fg = Theme.White;
                break;
            case ButtonStyle.Ghost:
                bg = hot ? new Color(255, 255, 255, 40) : new Color(255, 255, 255, 0);
                fg = Theme.White;
                break;
            case ButtonStyle.Tab:
                bg = hot ? Theme.CreamDark : new Color(0, 0, 0, 0);
                fg = Theme.Ink;
                break;
            case ButtonStyle.TabActive:
                bg = Theme.Primary;
                fg = Theme.White;
                break;
            default:
                bg = hot ? Theme.CreamDark : Theme.Cream;
                fg = Theme.Ink;
                break;
        }

        if (!enabled)
        {
            bg = bg.WithAlpha(0.45f);
            fg = fg.WithAlpha(0.5f);
        }

        var drawRect = r;
        if (hot && style != ButtonStyle.Ghost && style != ButtonStyle.Tab)
        {
            Panel(new Rectangle(r.X + S(3), r.Y + S(4), r.Width, r.Height), new Color(0, 0, 0, 60), 12);
        }

        Panel(drawRect, bg, 12);
        if (focused && Input.Source.GamepadAvailable)
        {
            PanelOutline(drawRect, Theme.Yellow, 3, 12);
        }

        TextIn(drawRect, label, textSize, fg, bold: true, align: Align.Center);
        return clicked;
    }

    /// <summary>Etiketli kaydirici. Deger degistiyse true.</summary>
    public bool Slider(Rectangle r, string label, ref float value, float min, float max, string valueText, float step = 0.05f)
    {
        var id = NextId(r, out var hovered, out var focused);
        var changed = false;
        var labelW = r.Width * 0.42f;
        var track = new Rectangle(r.X + labelW, r.Y + r.Height / 2 - S(6), r.Width - labelW - S(110), S(12));

        if (Interactive)
        {
            if (hovered && Input.MousePressed())
            {
                _active = id;
            }

            if (_active == id && Input.MouseDown())
            {
                var t = Math.Clamp((Input.MousePosition.X - track.X) / track.Width, 0f, 1f);
                var nv = min + t * (max - min);
                if (MathF.Abs(nv - value) > 1e-4f)
                {
                    value = nv;
                    changed = true;
                }
            }

            if (focused && (_navLeft || _navRight))
            {
                var range = max - min;
                value = Math.Clamp(value + (_navRight ? step : -step) * range, min, max);
                changed = true;
            }
        }

        if (focused || hovered)
        {
            Panel(r, new Color(255, 255, 255, 30), 10);
        }

        TextIn(new Rectangle(r.X, r.Y, labelW, r.Height), label, 26, Theme.Cream);
        var frac = (value - min) / (max - min);
        Bar(track, frac, Theme.Primary);
        var knob = new Vector2(track.X + track.Width * frac, track.Y + track.Height / 2);
        Raylib.DrawCircleV(knob, S(13), Theme.Cream);
        Raylib.DrawCircleV(knob, S(8), Theme.Primary);
        TextIn(new Rectangle(r.X + r.Width - S(100), r.Y, S(100), r.Height), valueText, 24, Theme.Cream, align: Align.Right, padding: 6);
        return changed;
    }

    public bool Toggle(Rectangle r, string label, ref bool value)
    {
        var id = NextId(r, out var hovered, out var focused);
        _ = id;
        var changed = false;
        if (Interactive && ((hovered && Input.MousePressed()) || (focused && (_navAccept || _navLeft || _navRight))))
        {
            value = !value;
            changed = true;
        }

        if (focused || hovered)
        {
            Panel(r, new Color(255, 255, 255, 30), 10);
        }

        TextIn(r, label, 26, Theme.Cream);
        var sw = new Rectangle(r.X + r.Width - S(84), r.Y + r.Height / 2 - S(15), S(64), S(30));
        Raylib.DrawRectangleRounded(sw, 1f, 8, value ? Theme.Green : new Color(255, 255, 255, 60));
        var kx = value ? sw.X + sw.Width - S(15) : sw.X + S(15);
        Raylib.DrawCircleV(new Vector2(kx, sw.Y + sw.Height / 2), S(12), Theme.Cream);
        return changed;
    }

    public bool Selector(Rectangle r, string label, IReadOnlyList<string> options, ref int index)
    {
        var id = NextId(r, out var hovered, out var focused);
        _ = id;
        var changed = false;
        var arrowW = S(44);
        var right = r.X + r.Width;
        var leftArrow = new Rectangle(right - S(300), r.Y + S(6), arrowW, r.Height - S(12));
        var rightArrow = new Rectangle(right - arrowW - S(8), r.Y + S(6), arrowW, r.Height - S(12));
        if (Interactive && options.Count > 0)
        {
            var dir = 0;
            if (Input.MousePressed())
            {
                if (Raylib.CheckCollisionPointRec(Input.MousePosition, leftArrow)) dir = -1;
                else if (Raylib.CheckCollisionPointRec(Input.MousePosition, rightArrow) || (hovered && Input.MousePosition.X > leftArrow.X)) dir = 1;
            }

            if (focused)
            {
                if (_navLeft) dir = -1;
                if (_navRight || _navAccept) dir = 1;
            }

            if (dir != 0)
            {
                index = (index + dir + options.Count) % options.Count;
                changed = true;
            }
        }

        if (focused || hovered)
        {
            Panel(r, new Color(255, 255, 255, 30), 10);
        }

        TextIn(r, label, 26, Theme.Cream);
        Panel(leftArrow, new Color(255, 255, 255, 40), 8);
        Panel(rightArrow, new Color(255, 255, 255, 40), 8);
        TextIn(leftArrow, "<", 26, Theme.Cream, true, Align.Center);
        TextIn(rightArrow, ">", 26, Theme.Cream, true, Align.Center);
        var valueRect = new Rectangle(leftArrow.X + arrowW, r.Y, rightArrow.X - leftArrow.X - arrowW, r.Height);
        if (options.Count > 0)
        {
            TextIn(valueRect, options[Math.Clamp(index, 0, options.Count - 1)], 24, Theme.Cream, true, Align.Center);
        }

        return changed;
    }

    /// <summary>Tek satir metin alani. Enter'a basildiginda true.</summary>
    public bool TextField(Rectangle r, ref string text, int maxLength, string placeholder = "")
    {
        var id = NextId(r, out var hovered, out var focused);
        var submitted = false;
        if (Interactive && hovered && Input.MousePressed())
        {
            _textFocus = id;
        }
        else if (Interactive && Input.MousePressed() && _textFocus == id && !hovered)
        {
            _textFocus = -1;
        }

        if (Interactive && focused && _navAccept && _textFocus != id)
        {
            _textFocus = id;
            _navAccept = false;
        }

        var editing = _textFocus == id;
        if (editing)
        {
            foreach (var c in Input.Source.Chars)
            {
                if (c >= 32 && text.Length < maxLength)
                {
                    text += char.ConvertFromUtf32(c);
                }
            }

            if (Input.KeyPressed(KeyboardKey.Backspace) && text.Length > 0)
            {
                text = text[..^1];
            }

            if (Input.KeyPressed(KeyboardKey.Enter) || Input.KeyPressed(KeyboardKey.KpEnter))
            {
                submitted = true;
                _textFocus = -1;
            }

            if (Input.KeyPressed(KeyboardKey.Escape))
            {
                _textFocus = -1;
            }
        }

        Panel(r, editing ? Theme.Cream : new Color(255, 255, 255, focused || hovered ? 70 : 45), 10);
        var shown = text.Length == 0 && !editing ? placeholder : text;
        var color = editing ? Theme.Ink : text.Length == 0 ? new Color(255, 255, 255, 130) : Theme.Cream;
        var caret = editing && (Raylib.GetTime() % 1.0) < 0.5 ? "|" : "";
        TextIn(r, shown + caret, 26, color);
        return submitted;
    }

    public bool IsEditingText => _textFocus >= 0;

    /// <summary>Kutulu tus gosterimi: "[E] Al" gibi ipuclari.</summary>
    public float KeyCap(Vector2 pos, string key, float size = 22f)
    {
        var m = Measure(key, size, true);
        var w = MathF.Max(m.X + S(16), S(size * 1.5f));
        var r = new Rectangle(pos.X, pos.Y, w, S(size * 1.45f));
        Panel(r, Theme.Cream, 6);
        Raylib.DrawRectangleRoundedLinesEx(r, Roundness(r, S(6)), 6, S(1.5f), Theme.InkSoft);
        TextIn(r, key, size, Theme.Ink, true, Align.Center, 0);
        return w;
    }
}
