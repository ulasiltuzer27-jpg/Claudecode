using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Engine.Ui;

/// <summary>
/// "Sicak sokak" temasinin bilesenleri: yumusak golgeler, kart / fis /
/// tabela / kara tahta yuzeyleri, sekmeler, adimlayici, kaydirma alani,
/// ipucu baloncugu, 2B izgara gezinmesi ve gecis animasyonlari.
/// </summary>
public sealed partial class UiContext
{
    /// <summary>Bu karenin suresi (saniye).</summary>
    public float Dt { get; private set; }

    /// <summary>Arayuz saati (animasyonlar icin).</summary>
    public float Time { get; private set; }

    /// <summary>Son girdi klavye/gamepad gezinmesiydi: odak cercevesi gorunur.</summary>
    public bool KeyboardNav { get; private set; }

    /// <summary>Ses calma kancasi (oyun baglar): kimlik, ses duzeyi.</summary>
    public Action<string, float>? Sound { get; set; }

    private readonly Dictionary<int, float> _anim = new();
    private readonly Dictionary<int, float> _ease = new();
    private int _hoverId = -1;
    private int _prevHoverId = -1;
    private float _hoverTime;
    private int _lastId = -1;
    private string? _tooltip;
    private readonly List<(int Start, int End, int Cols)> _grids = new();
    private readonly List<(int Start, int End, int Cols)> _gridsPrev = new();
    private int _gridStart = -1;
    private int _gridCols;

    private string? _scrollKey;
    private Rectangle _scrollView;
    private readonly Dictionary<string, float> _scrollOffsets = new();
    private readonly Dictionary<string, float> _scrollContent = new();

    private void BeginFrameState()
    {
        _hoverTime = _hoverId >= 0 && _hoverId == _prevHoverId ? _hoverTime + Dt : 0f;
        _prevHoverId = _hoverId;
        _hoverId = -1;
        _tooltip = null;
        _gridsPrev.Clear();
        _gridsPrev.AddRange(_grids);
        _grids.Clear();
    }

    private void EndFrameState()
    {
        if (_tooltip is { } tip)
        {
            DrawTooltip(tip);
        }
    }

    // ── Animasyon ────────────────────────────────────────────────────

    /// <summary>Bilesen basina 0..1 yumusak gecis (uzerine gelme, secim).</summary>
    private float Anim(int id, bool on, float speed = 14f)
    {
        _anim.TryGetValue(id, out var v);
        v += ((on ? 1f : 0f) - v) * MathF.Min(1f, Dt * speed);
        _anim[id] = v;
        return v;
    }

    /// <summary>Anahtarla saklanan, hedefe yumusakca yaklasan deger (sayac, kayan cizgi).</summary>
    public float Ease(string key, float target, float speed = 8f, bool snap = false)
    {
        var h = key.GetHashCode();
        if (snap || !_ease.TryGetValue(h, out var v))
        {
            _ease[h] = target;
            return target;
        }

        v += (target - v) * MathF.Min(1f, Dt * speed);
        if (MathF.Abs(target - v) < 0.01f)
        {
            v = target;
        }

        _ease[h] = v;
        return v;
    }

    // ── Ipucu baloncugu ──────────────────────────────────────────────

    /// <summary>Son bilesenin ustunde bir sure durulursa aciklama gosterir (devre disi dugmeler: neden).</summary>
    public void Tooltip(string text)
    {
        if (_lastId >= 0 && _lastId == _prevHoverId && _hoverTime > 0.35f && text.Length > 0)
        {
            _tooltip = text;
        }
    }

    private void DrawTooltip(string text)
    {
        var maxW = S(420);
        var lines = Wrap(text, 22, maxW);
        var w = lines.Max(l => Measure(l, 22).X) + S(28);
        var h = lines.Count * S(22 * 1.3f) + S(18);
        var m = Input.MousePosition;
        var x = Math.Clamp(m.X + S(18), S(8), Width - w - S(8));
        var y = m.Y + S(26) + h > Height ? m.Y - h - S(12) : m.Y + S(26);
        var r = new Rectangle(x, y, w, h);
        SoftShadow(r, 10, 10, 90);
        Panel(r, Gfx.Hex(0x2B211A), 10);
        PanelOutline(r, Theme.Primary.WithAlpha(0.7f), 1.5f, 10);
        var ty = y + S(9);
        foreach (var l in lines)
        {
            Text(l, new Vector2(x + S(14), ty), 22, Theme.Cream);
            ty += S(22 * 1.3f);
        }
    }

    // ── Izgara gezinmesi ─────────────────────────────────────────────

    /// <summary>Bu cagri ile <see cref="EndGrid"/> arasindaki bilesenler satir satir izgaradir: sol/sag/yukari/asagi 2B gezinir.</summary>
    public void BeginGrid(int columns)
    {
        _gridStart = _index;
        _gridCols = Math.Max(1, columns);
    }

    public void EndGrid()
    {
        if (_gridStart >= 0 && _index > _gridStart)
        {
            _grids.Add((_gridStart, _index, _gridCols));
        }

        _gridStart = -1;
    }

    /// <summary>Odak bir izgaradaysa ok tuslarini 2B uygular; tuketildiyse true.</summary>
    private bool GridNav()
    {
        foreach (var (start, end, cols) in _gridsPrev)
        {
            if (_focus < start || _focus >= end)
            {
                continue;
            }

            var local = _focus - start;
            var handled = false;
            if (_navLeft && local % cols > 0)
            {
                _focus--;
                handled = true;
            }
            else if (_navRight && local % cols < cols - 1 && _focus + 1 < end)
            {
                _focus++;
                handled = true;
            }
            else if (_navUp)
            {
                _focus = local - cols >= 0 ? _focus - cols : Math.Max(0, start - 1);
                handled = true;
            }
            else if (_navDown)
            {
                _focus = _focus + cols < end ? _focus + cols : local / cols < (end - start - 1) / cols ? end - 1 : Math.Min(_count - 1, end);
                handled = true;
            }

            if (handled)
            {
                _navLeft = _navRight = _navUp = _navDown = false;
            }

            return handled;
        }

        return false;
    }

    // ── Kaydirma alani ───────────────────────────────────────────────

    /// <summary>
    /// Kaydirilabilir alan baslatir; icerigin ust kenarinin y'sini dondurur.
    /// Icerik cizildikten sonra <see cref="EndScroll"/> en alt y ile cagrilir.
    /// Fare tekerlegi, surukleme ve klavye odagi takibi desteklenir.
    /// </summary>
    public float BeginScroll(string key, Rectangle view)
    {
        _scrollKey = key;
        _scrollView = view;
        var off = _scrollOffsets.GetValueOrDefault(key);
        var content = _scrollContent.GetValueOrDefault(key, view.Height);
        var max = MathF.Max(0, content - view.Height);
        if (Interactive && Raylib.CheckCollisionPointRec(Input.MousePosition, view) && Input.Wheel != 0)
        {
            off -= Input.Wheel * S(70);
        }

        off = Math.Clamp(off, 0, max);
        _scrollOffsets[key] = off;
        Raylib.BeginScissorMode((int)view.X, (int)view.Y, (int)MathF.Ceiling(view.Width), (int)MathF.Ceiling(view.Height));
        return view.Y - off;
    }

    public void EndScroll(float contentBottom)
    {
        Raylib.EndScissorMode();
        if (_scrollKey is not { } key)
        {
            return;
        }

        var view = _scrollView;
        var off = _scrollOffsets.GetValueOrDefault(key);
        var content = contentBottom - (view.Y - off);
        _scrollContent[key] = content;
        _scrollKey = null;
        if (content <= view.Height + 1)
        {
            return;
        }

        // Kaydirma cubugu (surukleme destekli)
        var track = new Rectangle(view.X + view.Width - S(8), view.Y + S(4), S(6), view.Height - S(8));
        var frac = view.Height / content;
        var thumbH = MathF.Max(S(36), track.Height * frac);
        var maxOff = content - view.Height;
        var thumbY = track.Y + (track.Height - thumbH) * (off / maxOff);
        Raylib.DrawRectangleRounded(track, 1f, 6, new Color(255, 255, 255, 25));
        var thumb = new Rectangle(track.X - S(2), thumbY, track.Width + S(4), thumbH);
        var hot = Interactive && Raylib.CheckCollisionPointRec(Input.MousePosition, new Rectangle(track.X - S(10), track.Y, track.Width + S(20), track.Height));
        if (hot && Input.MouseDown())
        {
            var t = Math.Clamp((Input.MousePosition.Y - track.Y - thumbH / 2) / (track.Height - thumbH), 0f, 1f);
            _scrollOffsets[key] = t * maxOff;
            MouseOverUi = true;
        }

        Raylib.DrawRectangleRounded(thumb, 1f, 6, hot ? Theme.Primary : new Color(255, 244, 226, 120));
    }

    /// <summary>Klavyeyle odaklanan bilesen kaydirma alaninin disindaysa alani kaydirir.</summary>
    private void FollowFocus(Rectangle r)
    {
        if (_scrollKey is not { } key)
        {
            return;
        }

        var off = _scrollOffsets.GetValueOrDefault(key);
        if (r.Y < _scrollView.Y)
        {
            off -= _scrollView.Y - r.Y + S(10);
        }
        else if (r.Y + r.Height > _scrollView.Y + _scrollView.Height)
        {
            off += r.Y + r.Height - (_scrollView.Y + _scrollView.Height) + S(10);
        }

        _scrollOffsets[key] = MathF.Max(0, off);
    }

    // ── Yuzeyler ─────────────────────────────────────────────────────

    /// <summary>Katmanli yari saydam dikdortgenlerle yumusak golge.</summary>
    public void SoftShadow(Rectangle r, float radius = 14f, float spread = 12f, byte alpha = 80)
    {
        const int layers = 5;
        for (var i = layers; i >= 1; i--)
        {
            var g = S(spread) * i / layers;
            var rr = new Rectangle(r.X - g * 0.6f, r.Y - g * 0.3f + S(spread) * 0.35f, r.Width + g * 1.2f, r.Height + g * 1.2f);
            Raylib.DrawRectangleRounded(rr, Roundness(rr, S(radius) + g), 8, new Color(0, 0, 0, alpha / (layers + 1)));
        }
    }

    /// <summary>Krem kart: yumusak golge, istege bagli sol vurgu seridi.</summary>
    public void Card(Rectangle r, Color? accent = null, bool dark = false)
    {
        SoftShadow(r, 14, 10, 70);
        Panel(r, dark ? Gfx.Hex(0x3A2D23) : Theme.Cream, 14);
        if (accent is { } a)
        {
            var strip = r with { Width = S(8) };
            Raylib.DrawRectangleRounded(strip, Roundness(strip, S(4)), 6, a);
        }
    }

    /// <summary>Fis kagidi: kirik beyaz, alt kenari tirtikli, soluk cizgiler.</summary>
    public void Paper(Rectangle r, int lines = 0)
    {
        SoftShadow(r, 4, 8, 70);
        var body = r with { Height = r.Height - S(8) };
        Raylib.DrawRectangleRec(body, Gfx.Hex(0xFBF8F1));
        var teeth = Math.Max(4, (int)(r.Width / S(14)));
        var tw = r.Width / teeth;
        for (var i = 0; i < teeth; i++)
        {
            var x = r.X + i * tw;
            var y = body.Y + body.Height;
            Raylib.DrawTriangle(new Vector2(x, y), new Vector2(x + tw / 2, y + S(8)), new Vector2(x + tw, y), Gfx.Hex(0xFBF8F1));
        }

        for (var i = 1; i <= lines; i++)
        {
            var y = r.Y + body.Height * i / (lines + 1);
            for (var x = r.X + S(12); x < r.X + r.Width - S(12); x += S(10))
            {
                Raylib.DrawRectangleRec(new Rectangle(x, y, S(5), S(1.5f)), new Color(160, 140, 120, 70));
            }
        }
    }

    /// <summary>Esnaf tabelasi: ahsap pano, vida baslari, cerceveli baslik.</summary>
    public void SignBoard(Rectangle r, string title, Icon icon = Icon.None, float textSize = 34f)
    {
        SoftShadow(r, 10, 10, 90);
        Panel(r, Gfx.Hex(0x8B5A2B), 10);
        // Ahsap damarlari
        for (var i = 1; i < 4; i++)
        {
            var y = r.Y + r.Height * i / 4f + MathF.Sin(i * 1.7f) * S(2);
            Raylib.DrawRectangleRec(new Rectangle(r.X + S(8), y, r.Width - S(16), S(1.5f)), new Color(60, 35, 15, 60));
        }

        var inner = new Rectangle(r.X + S(6), r.Y + S(6), r.Width - S(12), r.Height - S(12));
        PanelOutline(inner, new Color(255, 220, 160, 70), 1.5f, 8);
        foreach (var (x, y) in new[] { (r.X + S(14), r.Y + S(14)), (r.X + r.Width - S(14), r.Y + S(14)), (r.X + S(14), r.Y + r.Height - S(14)), (r.X + r.Width - S(14), r.Y + r.Height - S(14)) })
        {
            Raylib.DrawCircleV(new Vector2(x, y), S(4), Gfx.Hex(0xC9B79C));
            Raylib.DrawLineEx(new Vector2(x - S(2.5f), y), new Vector2(x + S(2.5f), y), S(1.2f), Gfx.Hex(0x6E5A44));
        }

        var tw = Measure(title, textSize, true).X;
        var iconW = icon != Icon.None ? S(textSize * 1.1f) + S(12) : 0;
        var x0 = r.X + (r.Width - tw - iconW) / 2;
        var cy = r.Y + r.Height / 2;
        if (icon != Icon.None)
        {
            Icons.Draw(icon, new Vector2(x0 + S(textSize * 0.55f), cy), S(textSize * 1.1f), Theme.Cream, Theme.Primary);
        }

        TextOutlined(title, new Vector2(x0 + iconW, cy - Measure(title, textSize, true).Y / 2), textSize, Theme.Cream, new Color(50, 28, 10, 200), true);
    }

    /// <summary>Kara tahta: ahsap cerceve, koyu yesil yuzey, tebesir lekeleri.</summary>
    public void Chalkboard(Rectangle r)
    {
        SoftShadow(r, 8, 10, 90);
        Panel(r, Gfx.Hex(0x7A4E2A), 8);
        var board = new Rectangle(r.X + S(12), r.Y + S(12), r.Width - S(24), r.Height - S(24));
        Panel(board, Gfx.Hex(0x2F4A3A), 4);
        var seed = (int)(r.X * 7 + r.Y * 3);
        for (var i = 0; i < 6; i++)
        {
            var h = (uint)(seed * 2654435761u + i * 40503u);
            var x = board.X + (h % 1000) / 1000f * board.Width;
            var y = board.Y + ((h >> 10) % 1000) / 1000f * board.Height;
            Raylib.DrawEllipse((int)x, (int)y, S(40 + (h % 30)), S(10 + (h % 8)), new Color(255, 255, 255, 8));
        }
    }

    /// <summary>Dikey renk gecisli serit (baslik bandi).</summary>
    public void Gradient(Rectangle r, Color top, Color bottom) =>
        Raylib.DrawRectangleGradientV((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height, top, bottom);

    // ── Metin ────────────────────────────────────────────────────────

    /// <summary>Konturlu metin (HUD'da her zeminde okunur).</summary>
    public void TextOutlined(string text, Vector2 pos, float size, Color color, Color outline, bool bold = false, Align align = Align.Left, float thickness = 2f)
    {
        var px = S(size);
        if (align != Align.Left)
        {
            var w = Fonts.Measure(text, px, bold).X;
            pos.X -= align == Align.Center ? w / 2 : w;
        }

        Fonts.DrawOutlined(text, pos, px, color, outline, bold, MathF.Max(1f, S(thickness)));
    }

    /// <summary>Ikon + metin satiri; kullanilan genisligi dondurur.</summary>
    public float IconText(Icon icon, string text, Vector2 pos, float size, Color color, Color? iconColor = null, bool bold = false, bool outline = false)
    {
        var isz = S(size * 1.05f);
        var th = Measure(text, size, bold).Y;
        Icons.Draw(icon, new Vector2(pos.X + isz / 2, pos.Y + th / 2), isz, iconColor ?? color);
        var tp = new Vector2(pos.X + isz + S(8), pos.Y);
        if (outline)
        {
            TextOutlined(text, tp, size, color, new Color(0, 0, 0, 170), bold);
        }
        else
        {
            Text(text, tp, size, color, bold);
        }

        return isz + S(8) + Measure(text, size, bold).X;
    }

    /// <summary>Kucuk hap etiketi (rozet).</summary>
    public float Badge(Vector2 pos, string text, Color bg, Color fg, float size = 20f)
    {
        var m = Measure(text, size, true);
        var r = new Rectangle(pos.X, pos.Y, m.X + S(18), m.Y + S(6));
        Raylib.DrawRectangleRounded(r, 1f, 8, bg);
        TextIn(r, text, size, fg, true, Align.Center, 0);
        return r.Width;
    }

    /// <summary>Halka gostergesi (gun ilerlemesi, XP).</summary>
    public void Ring(Vector2 center, float radius, float thickness, float t, Color fill, Color back)
    {
        var r = S(radius);
        var th = S(thickness);
        Raylib.DrawRing(center, r - th, r, 0, 360, 48, back);
        if (t > 0.001f)
        {
            Raylib.DrawRing(center, r - th, r, -90, -90 + 360 * Math.Clamp(t, 0f, 1f), 48, fill);
        }
    }

    // ── Kaplar ve bilesenler ─────────────────────────────────────────

    /// <summary>Sekme cubugu; secim degistiyse true. Etkin sekmenin alt cizgisi kayar.</summary>
    public bool Tabs(Rectangle r, IReadOnlyList<string> labels, ref int index, IReadOnlyList<Icon>? icons = null, string key = "tabs", float textSize = 24f)
    {
        var changed = false;
        Panel(r, new Color(0, 0, 0, 70), 12);
        var n = labels.Count;
        if (n == 0)
        {
            return false;
        }

        var w = (r.Width - S(8)) / n;
        // Etkin sekme: kayan turuncu zemin ve alt cizgi (yazilarin arkasinda)
        var sx = Ease(key + ":x", r.X + S(4) + index * w);
        var active = new Rectangle(sx, r.Y + S(4), w - S(4), r.Height - S(8));
        Panel(active, Theme.Primary, 10);
        Raylib.DrawRectangleRounded(new Rectangle(sx + S(10), r.Y + r.Height - S(9), w - S(24), S(3)), 1f, 6, Theme.Yellow);
        BeginGrid(n);
        for (var i = 0; i < n; i++)
        {
            var tr = new Rectangle(r.X + S(4) + i * w, r.Y + S(4), w - S(4), r.Height - S(8));
            var id = NextId(tr, out var hovered, out var focused);
            var hot = hovered || focused;
            var a = Anim(id, hot || i == index);
            if (Interactive && ((hovered && Input.MousePressed()) || (focused && _navAccept)) && i != index)
            {
                index = i;
                changed = true;
                Sound?.Invoke("ui_click", 0.35f);
            }

            if (i != index && a > 0.01f)
            {
                Panel(tr, new Color(255, 255, 255, (int)(28 * a)), 10);
            }

            var fg = i == index ? Theme.White : Theme.CreamDark;
            var ic = icons is not null && i < icons.Count ? icons[i] : Icon.None;
            if (ic != Icon.None)
            {
                var isz = S(textSize);
                var tw = Measure(labels[i], textSize, true).X;
                var x0 = tr.X + (tr.Width - isz - S(8) - tw) / 2;
                Icons.Draw(ic, new Vector2(x0 + isz / 2, tr.Y + tr.Height / 2), isz, fg);
                Text(labels[i], new Vector2(x0 + isz + S(8), tr.Y + (tr.Height - Measure(labels[i], textSize, true).Y) / 2), textSize, fg, true);
            }
            else
            {
                TextIn(tr, labels[i], textSize, fg, true, Align.Center);
            }

            if (focused && KeyboardNav)
            {
                PanelOutline(tr, Theme.Yellow, 2, 10);
            }
        }

        EndGrid();
        return changed;
    }

    /// <summary>Etiket + [-] deger [+]. Sol/sag ok ve fare ile; deger degistiyse true.</summary>
    public bool Stepper(Rectangle r, string label, ref int value, int min, int max, int step = 1, string? valueText = null)
    {
        var id = NextId(r, out var hovered, out var focused);
        _ = id;
        var changed = false;
        var bw = S(44);
        var minus = new Rectangle(r.X + r.Width - S(190), r.Y + (r.Height - bw) / 2, bw, bw);
        var plus = new Rectangle(r.X + r.Width - bw - S(6), minus.Y, bw, bw);
        var dir = 0;
        if (Interactive)
        {
            if (Input.MousePressed())
            {
                if (Raylib.CheckCollisionPointRec(Input.MousePosition, minus)) dir = -1;
                else if (Raylib.CheckCollisionPointRec(Input.MousePosition, plus)) dir = 1;
            }

            if (focused)
            {
                if (_navLeft) dir = -1;
                if (_navRight) dir = 1;
            }
        }

        if (dir != 0)
        {
            var nv = Math.Clamp(value + dir * step, min, max);
            if (nv != value)
            {
                value = nv;
                changed = true;
                Sound?.Invoke("ui_click", 0.3f);
            }
        }

        if (focused || hovered)
        {
            Panel(r, new Color(255, 255, 255, 30), 10);
            if (focused && KeyboardNav)
            {
                PanelOutline(r, Theme.Yellow.WithAlpha(0.8f), 2, 10);
            }
        }

        TextIn(r with { Width = r.Width - S(200) }, label, 26, Theme.Cream);
        foreach (var (br, ic, ok) in new[] { (minus, Icon.Minus, value > min), (plus, Icon.Plus, value < max) })
        {
            var bh = Interactive && Raylib.CheckCollisionPointRec(Input.MousePosition, br);
            Panel(br, ok ? (bh ? Theme.Primary : new Color(255, 255, 255, 45)) : new Color(255, 255, 255, 15), 10);
            Icons.Draw(ic, new Vector2(br.X + br.Width / 2, br.Y + br.Height / 2), bw * 0.45f, ok ? Theme.Cream : new Color(255, 255, 255, 70));
        }

        TextIn(new Rectangle(minus.X + bw, r.Y, plus.X - minus.X - bw, r.Height), valueText ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture), 26, Theme.Cream, true, Align.Center, 0);
        return changed;
    }
}

/// <summary>Basit yerlesim yardimcisi: bir alani sirayla dikey/yatay dilimler.</summary>
public struct UiStack
{
    public Rectangle Area;
    public float Cursor;
    public float Gap;
    public bool Horizontal;

    public UiStack(Rectangle area, float gap, bool horizontal = false)
    {
        Area = area;
        Gap = gap;
        Horizontal = horizontal;
        Cursor = 0;
    }

    /// <summary>Sonraki dilim (piksel cinsinden boyut).</summary>
    public Rectangle Next(float size)
    {
        var r = Horizontal
            ? new Rectangle(Area.X + Cursor, Area.Y, size, Area.Height)
            : new Rectangle(Area.X, Area.Y + Cursor, Area.Width, size);
        Cursor += size + Gap;
        return r;
    }

    /// <summary>Kalan alani esit n parcaya boler.</summary>
    public Rectangle[] Split(int n)
    {
        var total = (Horizontal ? Area.Width : Area.Height) - Cursor - Gap * (n - 1);
        var each = total / n;
        var list = new Rectangle[n];
        for (var i = 0; i < n; i++)
        {
            list[i] = Next(each);
        }

        return list;
    }

    public readonly float Remaining => (Horizontal ? Area.Width : Area.Height) - Cursor;
}
