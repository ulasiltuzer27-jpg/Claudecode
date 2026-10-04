using System.Numerics;
using Starfall.Achievements;
using Starfall.Core;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.UI;

/// <summary>
/// Oyun ici gostergeler: sayaclar, etkilesim ipucu, bolge afisi, bildirimler, yaris, balik
/// karti; ayrica diyalog kutusu, balik tutma cubugu ve photo mode katmani.
/// </summary>
public sealed class Hud
{
    private sealed class ToastItem
    {
        public string Text = "";
        public AchievementDef? Ach;
        public float T, Life;
    }

    private readonly Game _g;
    public bool Visible;
    private float _vis;
    private string? _promptAction, _promptText;
    private float _promptT;
    private string? _hint;
    private float _hintT, _hintLife, _hintAge;
    private string? _bannerName, _bannerSub;
    private float _bannerT = -1;
    private readonly List<ToastItem> _toasts = new();
    private string? _raceBanner;
    private float _raceBannerT;
    private float? _raceTimer;
    private string? _titleMain, _titleSub;
    private float _titleT;
    private (string Name, int Size, bool New)? _fish;
    private float _fishT;
    private int _lastShards = -1, _lastShells = -1, _lastAuroras = -1;
    private float _bumpShards, _bumpShells, _bumpAuroras;
    public static readonly List<Vector4> ChoiceRects = new();
    public string? LastToast;

    public Hud(Game g) => _g = g;

    // ------------------------------------------------------------ durum API'si
    public void Prompt(string action, string? text)
    {
        if (text != _promptText) _promptT = 0;
        _promptAction = action;
        _promptText = text;
    }

    public void Hint(string? text, float sec = 5)
    {
        _hint = text;
        _hintLife = sec;
        _hintAge = 0;
    }

    public string? CurrentHint => _hint;

    public void RegionBanner(string name, string? sub)
    {
        _bannerName = name;
        _bannerSub = sub;
        _bannerT = 0;
    }

    public void Toast(string text)
    {
        LastToast = text;
        PushToast(new ToastItem { Text = text, Life = 3.2f });
    }

    public void Achievement(AchievementDef a) => PushToast(new ToastItem { Ach = a, Life = 5.2f });

    private void PushToast(ToastItem t)
    {
        _toasts.Add(t);
        while (_toasts.Count > 4) _toasts.RemoveAt(0);
    }

    public void RaceBanner(string? text)
    {
        _raceBanner = text;
        _raceBannerT = 0;
    }

    public void RaceTimer(float? sec) => _raceTimer = sec;

    public void TitleCard(string? main, string? sub)
    {
        _titleMain = main;
        _titleSub = sub;
        _titleT = 0;
    }

    public void FishCard(string name, int size, bool isNew)
    {
        _fish = (name, size, isNew);
        _fishT = 0;
    }

    public void Update(float dt)
    {
        _vis = Math.Clamp(_vis + (Visible ? dt : -dt) * 2.5f, 0, 1);
        _promptT += dt;
        if (_hint != null)
        {
            _hintAge += dt;
            if (_hintAge > _hintLife) _hint = null;
        }
        if (_bannerT >= 0) { _bannerT += dt; if (_bannerT > 4.6f) _bannerT = -1; }
        foreach (var t in _toasts) t.T += dt;
        _toasts.RemoveAll(t => t.T > t.Life + 0.5f);
        _raceBannerT += dt;
        _titleT += dt;
        if (_fish != null) { _fishT += dt; if (_fishT > 3.2f) _fish = null; }
        _bumpShards = MathF.Max(0, _bumpShards - dt * 3);
        _bumpShells = MathF.Max(0, _bumpShells - dt * 3);
        _bumpAuroras = MathF.Max(0, _bumpAuroras - dt * 3);
        _hintT += dt;
    }

    private static float Pop(float t, float dur = 0.35f)
    {
        float k = Math.Clamp(t / dur, 0, 1);
        return k < 0.7f ? MathX.Lerp(0.6f, 1.06f, k / 0.7f) : MathX.Lerp(1.06f, 1, (k - 0.7f) / 0.3f);
    }

    // ------------------------------------------------------------ cizim
    public void Draw(UiCtx c)
    {
        var g = _g;
        var D = c.D;
        bool photo = g.State == GameState.Photo;
        if (photo) { DrawPhoto(c); return; }
        if (g.State == GameState.Menu && !g.Credits.Active) { DrawFps(c); return; }
        float a = _vis;
        if (a > 0.01f && g.Save != null)
        {
            DrawCounters(c, a);
            DrawBanner(c, a);
            if (!string.IsNullOrEmpty(_promptText) && g.State == GameState.Playing) DrawPrompt(c, a);
            if (_hint != null && !g.Dialogue.Active) DrawHint(c, a);
            if (_raceTimer is float rt)
            {
                string s = rt.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
                float fs = c.Px(22.4f);
                float w = D.Measure(s, fs, FontKind.Strong) + 36;
                c.Pill(c.W / 2 - w / 2, 18, w, 40, C.Dark);
                D.Text(s, c.W / 2, UiCtx.Mid(18, 40, fs), fs, C.White, FontKind.Strong, Align.Center);
            }
            if (_fish is { } f) DrawFishCard(c, f);
            DrawExtraHud(c, a);
        }
        if (g.Fishing.State != "idle") DrawFishing(c);
        DrawToasts(c);
        if (_raceBanner != null)
        {
            float fs = 96 * Pop(_raceBannerT);
            D.Text(_raceBanner, c.W / 2, 720 * 0.3f - fs * 0.6f + 6, fs, C.A(C.Hex("#000000"), 0.18f), FontKind.Title, Align.Center);
            D.Text(_raceBanner, c.W / 2, 720 * 0.3f - fs * 0.6f, fs, C.White, FontKind.Title, Align.Center);
        }
        if (_titleMain != null)
        {
            float k = Math.Clamp(_titleT / 1.2f, 0, 1);
            float y = 720 * 0.24f + (1 - k) * 10;
            D.Text(_titleMain, c.W / 2, y, 64, C.A(C.White, k), FontKind.Title, Align.Center, 2);
            if (_titleSub != null) D.Text(_titleSub, c.W / 2, y + 78, c.Px(20.8f), C.A(C.White, k), FontKind.Strong, Align.Center, 2);
        }
        if (g.Dialogue.Active) DrawDialogue(c);
        DrawFps(c);
    }

    private void DrawFps(UiCtx c)
    {
        if (!_g.Settings.V.ShowFps) return;
        string s = $"{_g.Fps:0} FPS";
        c.D.Text(s, c.W - 10, 720 - 22, 12, C.White, FontKind.Strong, Align.Right, 1);
    }

    private void DrawCounters(UiCtx c, float a)
    {
        var g = _g;
        var D = c.D;
        var p = g.Progress;
        int shards = p.ShardCount(), shells = g.Save!.Shells, feathers = p.FeatherCount(), used = g.Player.FlapsUsed;
        if (_lastShards >= 0 && shards != _lastShards) _bumpShards = 1;
        if (_lastShells >= 0 && shells != _lastShells) _bumpShells = 1;
        _lastShards = shards;
        _lastShells = shells;
        float x = 18, y = 18;
        float fs = c.Px(16.8f);

        void Pill(string icon, string val, string? sub, float bump)
        {
            float vw = D.Measure(val, fs, FontKind.Strong);
            float sw = sub != null ? D.Measure(sub, c.Px(13.6f), FontKind.Strong) + 6 : 0;
            float w = 8 + 28 + 8 + vw + sw + 14;
            float h = 40;
            float s = 1 + MathF.Sin(bump * MathX.Pi) * 0.08f;
            float px = x, py = y;
            D.Shadow(px, py, w * s, h * s, h / 2, 0.2f * a);
            c.Pill(px, py, w * s, h * s, C.A(C.Cream, 0.92f * a));
            var tex = UiTex.Get(icon);
            if (tex != null) D.Image(tex, px + 8, py + 6, 28, 28, new Vector4(1, 1, 1, a));
            D.Text(val, px + 44, UiCtx.Mid(py, h, fs), fs, C.A(C.Ink, a), FontKind.Strong);
            if (sub != null) D.Text(sub, px + 44 + vw + 6, UiCtx.Mid(py, h, c.Px(13.6f)) + 1, c.Px(13.6f), C.A(C.InkSoft, a), FontKind.Strong);
            y += h + 8;
        }

        Pill("hud/star.png", shards.ToString(), $"/ {WD.ShardTotal}", _bumpShards);
        if (feathers > 0)
        {
            float w = 20 + feathers * 22 - 4;
            float h = 28;
            c.Pill(x, y, w, h, C.A(C.Cream, 0.92f * a));
            for (int i = 0; i < feathers; i++)
            {
                bool on = i < feathers - used;
                float s = on ? 18 : 14;
                float cx = x + 10 + i * 22 + 9, cy = y + h / 2;
                D.Rect(cx - s / 2, cy - s / 2, s, s, on ? C.A(C.Gold, a) : C.A(C.Hex("#cfc4b2"), a), s / 2);
                if (on) D.Rect(cx - s / 2 + 3, cy - s / 2 + 2, s * 0.45f, s * 0.4f, C.A(C.Hex("#fff3b0"), 0.8f * a), s * 0.2f);
            }
            y += h + 8;
        }
        Pill("hud/shell.png", shells.ToString(), null, _bumpShells);
        int auroras = p.AuroraCount();
        if (auroras > 0 || g.World.IslandAt(g.Player.Pos.X, g.Player.Pos.Z) == "isle2")
        {
            if (_lastAuroras >= 0 && auroras != _lastAuroras) _bumpAuroras = 1;
            _lastAuroras = auroras;
            Pill("hud/crystal.png", auroras.ToString(), $"/ {GameWorld.AuroraTotal}", _bumpAuroras);
        }
    }

    /// <summary>Asama B gostergeleri (tirmanma dayanikliligi, tekne).</summary>
    private void DrawExtraHud(UiCtx c, float a)
    {
        var pl = _g.Player;
        if (pl.Climbing || pl.StaminaShow > 0.01f)
        {
            float k = pl.Climbing ? 1 : pl.StaminaShow;
            float frac = Math.Clamp(pl.Stamina / MathF.Max(0.01f, pl.StaminaMax), 0, 1);
            float cx = c.W / 2 + 70, cy = 720 / 2f - 40;
            float r = 22;
            c.D.Rect(cx - r - 4, cy - r - 4, r * 2 + 8, r * 2 + 8, C.A(C.Dark, 0.8f * k * a), r + 4);
            int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float ang = i / (float)segs * MathX.TwoPi - MathX.Pi / 2;
                bool on = i / (float)segs < frac;
                var col = on ? (frac < 0.25f ? C.Rose : C.Gold) : C.A(C.White, 0.25f);
                c.D.Rect(cx + MathF.Cos(ang) * (r - 4) - 3, cy + MathF.Sin(ang) * (r - 4) - 3, 6, 6, C.A(col, k * a), 3);
            }
            var tex = UiTex.Get("hud/stamina.png");
            if (tex != null) c.D.Image(tex, cx - 12, cy - 12, 24, 24, new Vector4(1, 1, 1, k * a));
        }
    }

    private void DrawBanner(UiCtx c, float a)
    {
        if (_bannerT < 0 || _bannerName == null) return;
        float k = _bannerT < 0.8f ? _bannerT / 0.8f : _bannerT > 3.8f ? MathF.Max(0, 1 - (_bannerT - 3.8f) / 0.8f) : 1;
        k *= a;
        float y = 720 * 0.12f;
        c.D.Text(_bannerName, c.W / 2, y, c.Px(48), C.A(C.White, k), FontKind.Title, Align.Center, 3);
        if (!string.IsNullOrEmpty(_bannerSub)) c.D.Text(_bannerSub!, c.W / 2, y + c.Px(54), c.Px(16.8f), C.A(C.White, 0.9f * k), FontKind.Strong, Align.Center, 2);
        float ly = y + c.Px(54) + c.Px(28);
        for (int i = 0; i < 14; i++)
        {
            float f = 1 - MathF.Abs(i - 6.5f) / 7f;
            c.D.Rect(c.W / 2 - 70 + i * 10, ly, 10, 3, C.A(C.White, f * k));
        }
    }

    private void DrawPrompt(UiCtx c, float a)
    {
        var D = c.D;
        string glyph = _g.Input.Glyph(_promptAction ?? "interact");
        float fs = c.Px(16);
        float kw = c.MeasureKey(glyph);
        float tw = D.Measure(_promptText!, fs, FontKind.Strong);
        float w = 8 + kw + 10 + tw + 18, h = 48;
        float k = Math.Clamp(_promptT / 0.25f, 0, 1) * a;
        float x = c.W / 2 - w / 2, y = 720 * 0.87f - h + (1 - k) * 10;
        D.Shadow(x, y, w, h, h / 2, 0.25f * k);
        c.Pill(x, y, w, h, C.A(C.Cream, 0.95f * k));
        c.KeyCap(x + 8, y + 8, glyph, k);
        D.Text(_promptText!, x + 8 + kw + 10, UiCtx.Mid(y, h, fs), fs, C.A(C.Ink, k), FontKind.Strong);
    }

    private void DrawHint(UiCtx c, float a)
    {
        var D = c.D;
        float fs = c.Px(16);
        float maxW = MathF.Min(560, c.W * 0.9f) - 36;
        var lines = D.Font!.Wrap(_hint!, fs, maxW, FontKind.Strong);
        float lh = fs * 1.4f;
        float tw = lines.Max(l => D.Measure(l, fs, FontKind.Strong));
        float w = tw + 36, h = lines.Count * lh + 20;
        float k = Math.Clamp(_hintAge / 0.3f, 0, 1) * Math.Clamp((_hintLife - _hintAge) / 0.3f, 0, 1) * a;
        float x = c.W / 2 - w / 2, y = 720 * 0.78f - h + (1 - k) * 10;
        D.Rect(x, y, w, h, C.A(C.Hex("#1e1828"), 0.72f * k), 16);
        for (int i = 0; i < lines.Count; i++) D.Text(lines[i], c.W / 2, y + 10 + i * lh + (lh - fs) * 0.3f, fs, C.A(C.White, k), FontKind.Strong, Align.Center);
    }

    private void DrawToasts(UiCtx c)
    {
        var D = c.D;
        float y = 18;
        foreach (var t in _toasts)
        {
            float slide = t.T < 0.45f ? Back(t.T / 0.45f) : 1;
            float outK = t.T > t.Life ? Math.Clamp((t.T - t.Life) / 0.5f, 0, 1) : 0;
            float alpha = 1 - outK;
            if (t.Ach == null)
            {
                float fs = c.Px(16);
                float w = D.Measure(t.Text, fs, FontKind.Strong) + 32, h = 44;
                float x = c.W - 18 - w + (1 - slide) * (w + 40) + outK * 40;
                D.Shadow(x, y, w, h, 18, 0.25f * alpha);
                D.Rect(x, y, w, h, C.A(C.Cream, 0.97f * alpha), 18);
                D.Text(t.Text, x + 16, UiCtx.Mid(y, h, fs), fs, C.A(C.Ink, alpha), FontKind.Strong);
                y += h + 10;
            }
            else
            {
                var a = t.Ach;
                float w = 340, h = 80;
                float x = c.W - 18 - w + (1 - slide) * (w + 40) + outK * 40;
                D.Shadow(x, y, w, h, 18, 0.28f * alpha);
                D.Rect(x, y, w, h, C.A(C.Cream, 0.97f * alpha), 18);
                var tex = UiTex.Get($"icons/ach/{a.Id}.png");
                if (tex != null) D.Image(tex, x + 12, y + 12, 56, 56, new Vector4(1, 1, 1, alpha));
                float tx = x + 80;
                D.Text(T("ach.unlocked").ToUpperInvariant(), tx, y + 10, c.Px(11.5f), C.A(C.Teal, alpha), FontKind.Strong);
                D.Text(T($"ach.{a.Id}.name"), tx, y + 24, c.Px(18.4f), C.A(C.Ink, alpha), FontKind.Title);
                D.Text(Ellipsize(D, T($"ach.{a.Id}.desc"), c.Px(13.6f), w - 92), tx, y + 52, c.Px(13.6f), C.A(C.InkSoft, alpha), FontKind.Strong);
                y += h + 10;
            }
        }
    }

    public static string Ellipsize(UiDrawList d, string s, float fs, float maxW, FontKind k = FontKind.Strong)
    {
        if (d.Measure(s, fs, k) <= maxW) return s;
        while (s.Length > 1 && d.Measure(s + "…", fs, k) > maxW) s = s[..^1];
        return s + "…";
    }

    private static float Back(float t)
    {
        const float c1 = 1.3f, c3 = c1 + 1;
        float x = t - 1;
        return 1 + c3 * x * x * x + c1 * x * x;
    }

    private void DrawFishCard(UiCtx c, (string Name, int Size, bool New) f)
    {
        var D = c.D;
        float s = Pop(_fishT, 0.4f);
        float w = MathF.Max(220, D.Measure(f.Name, c.Px(28.8f), FontKind.Title) + 48) * s, h = (f.New ? 104 : 86) * s;
        float x = c.W / 2 - w / 2, y = 720 * 0.18f;
        D.Shadow(x, y, w, h, 20, 0.3f);
        D.Rect(x, y, w, h, C.A(C.Cream, 0.97f), 20);
        float ty = y + 12;
        if (f.New)
        {
            D.Text(T("fish.newSpecies").ToUpperInvariant(), c.W / 2, ty, c.Px(12.8f), C.Rose, FontKind.Strong, Align.Center);
            ty += 18;
        }
        D.Text(f.Name, c.W / 2, ty, c.Px(28.8f) * s, C.Ink, FontKind.Title, Align.Center);
        D.Text($"{f.Size} cm", c.W / 2, ty + 40 * s, c.Px(16) * s, C.InkSoft, FontKind.Strong, Align.Center);
    }

    private void DrawFishing(UiCtx c)
    {
        var D = c.D;
        var F = _g.Fishing;
        float cx = c.W * 0.78f - 80;
        float cy = 720 / 2f;
        bool reeling = F.State == "reel";
        float hintY = cy + 170;
        if (reeling)
        {
            float bw = 46, bh = 260;
            float bx = cx + 80 - bw / 2, by = cy - bh / 2 - 20;
            D.Rect(bx - 4, by - 4, bw + 8, bh + 8, C.A(C.White, 0.85f), 28);
            D.Rect(bx, by, bw, bh, new Vector4(20 / 255f, 40 / 255f, 70 / 255f, 0.75f), 24);
            float zh = F.ZoneH * bh;
            float zy = by + bh - F.Zone * bh - zh;
            D.Rect(bx + 4, zy, bw - 8, zh, F.Inside ? new Vector4(140 / 255f, 240 / 255f, 140 / 255f, 0.85f) : new Vector4(111 / 255f, 218 / 255f, 208 / 255f, 0.55f), 18);
            float fy = by + bh - F.FishY * bh * 0.92f - 22;
            // kucuk balik: govde + kuyruk
            D.Rect(bx + 10, fy + 5, 20, 12, C.Hex("#ff9a3c"), 6);
            D.Rect(bx + 28, fy + 6, 8, 10, C.Hex("#ff7a2a"), 3);
            D.Rect(bx + 13, fy + 8, 3, 3, C.Ink, 1.5f);
            float pw = 160;
            float px = cx + 80 - pw / 2, py = by + bh + 14;
            D.Rect(px, py, pw, 12, new Vector4(0, 0, 0, 0.35f), 8);
            D.Rect(px, py, pw * Math.Clamp(F.Progress, 0, 1), 12, C.Teal, 8);
            hintY = py + 24;
        }
        if (!string.IsNullOrEmpty(F.Hint))
        {
            float fs = c.Px(16);
            float w = D.Measure(F.Hint, fs, FontKind.Strong) + 28, h = 38;
            float x = cx + 80 - w / 2;
            D.Rect(x, hintY, w, h, C.Dark, 12);
            D.Text(F.Hint, cx + 80, UiCtx.Mid(hintY, h, fs), fs, C.White, FontKind.Strong, Align.Center);
        }
    }

    private void DrawDialogue(UiCtx c)
    {
        var D = c.D;
        var d = _g.Dialogue;
        float w = MathF.Min(820, c.W * 0.94f);
        float fs = c.Px(19.5f);
        float lh = fs * 1.5f;
        var lines = D.Font!.Wrap(d.Visible, fs, w - 60, FontKind.Strong);
        var fullLines = D.Font.Wrap(d.Full, fs, w - 60, FontKind.Strong);
        float textH = MathF.Max(2, fullLines.Count) * lh;
        float h = MathF.Max(120, 26 + textH + 26);
        float x = c.W / 2 - w / 2, y = 720 * 0.955f - h;
        c.Panel(x, y, w, h, 22);
        for (int i = 0; i < lines.Count; i++) D.Text(lines[i], x + 30, y + 26 + i * lh, fs, C.Ink, FontKind.Strong);
        if (!string.IsNullOrEmpty(d.Name))
        {
            float nfs = c.Px(18.4f);
            float nw = D.Measure(d.Name, nfs, FontKind.Title) + 36;
            D.Shadow(x + 26, y - 18, nw, 34, 17, 0.18f);
            c.Pill(x + 26, y - 18, nw, 34, C.Teal);
            D.Text(d.Name, x + 26 + nw / 2, UiCtx.Mid(y - 18, 34, nfs), nfs, C.White, FontKind.Title, Align.Center);
        }
        if (d.CanAdvance && MathF.Sin(c.Time * MathX.TwoPi) > -0.3f)
            D.Text("▼", x + w - 30, y + h - 30, c.Px(17.6f), C.Gold, FontKind.Strong, Align.Center);
        ChoiceRects.Clear();
        if (d.ShowingChoices && d.Choices != null)
        {
            float cfs = c.Px(16.8f);
            float by = y - 14;
            for (int i = d.Choices.Count - 1; i >= 0; i--)
            {
                var ch = d.Choices[i];
                float cw = D.Measure(ch.Label, cfs, FontKind.Strong) + 36 + 6, chh = 42;
                by -= chh;
                bool focus = i == d.ChoiceIdx;
                float cxp = x + w - 18 - cw - (focus ? 6 : 0);
                D.Shadow(cxp, by, cw, chh, 16, 0.22f);
                D.Rect(cxp - 3, by - 3, cw + 6, chh + 6, C.White, 18);
                D.Rect(cxp, by, cw, chh, focus ? C.Gold2 : C.Cream, 16);
                D.Text(ch.Label, cxp + 18, UiCtx.Mid(by, chh, cfs), cfs, C.Ink, FontKind.Strong);
                by -= 8;
            }
            // tiklama icin yerleri (ust->alt sirasiyla yeniden hesapla)
            float yy = y - 14;
            var rects = new Vector4[d.Choices.Count];
            for (int i = d.Choices.Count - 1; i >= 0; i--)
            {
                float cw = D.Measure(d.Choices[i].Label, cfs, FontKind.Strong) + 42, chh = 42;
                yy -= chh;
                rects[i] = new Vector4(x + w - 18 - cw, yy, cw, chh);
                yy -= 8;
            }
            ChoiceRects.AddRange(rects);
        }
    }

    private void DrawPhoto(UiCtx c)
    {
        var P = _g.Photo;
        var D = c.D;
        if (!P.HideUi)
        {
            string top = $"{T("photo.title")} · {P.FilterName}";
            float fs = c.Px(16);
            float w = D.Measure(top, fs, FontKind.Strong) + 36;
            c.Pill(c.W / 2 - w / 2, 16, w, 36, new Vector4(20 / 255f, 16 / 255f, 30 / 255f, 0.6f));
            D.Text(top, c.W / 2, UiCtx.Mid(16, 36, fs), fs, C.White, FontKind.Strong, Align.Center);
            var pairs = new[] { ("shot", T("photo.shoot")), ("filter", T("photo.filter")), ("hide", T("photo.hide")), ("back", T("photo.exit")) };
            float total = 0;
            foreach (var (a, l) in pairs) total += c.MeasureKey(_g.Input.Glyph(a)) + 6 + D.Measure(l, fs, FontKind.Strong) + 16;
            float bx = c.W / 2 - (total + 16) / 2, by = 720 - 16 - 48;
            D.Rect(bx, by, total + 16, 48, new Vector4(20 / 255f, 16 / 255f, 30 / 255f, 0.55f), 16);
            float xx = bx + 16;
            foreach (var (a, l) in pairs)
            {
                xx += c.KeyCap(xx, by + 8, _g.Input.Glyph(a)) + 6;
                D.Text(l, xx, UiCtx.Mid(by, 48, fs), fs, C.White, FontKind.Strong);
                xx += D.Measure(l, fs, FontKind.Strong) + 16;
            }
        }
        if (P.Flash > 0) D.Rect(0, 0, c.W, c.H, C.A(C.White, 0.85f * P.Flash));
    }
}
