using System.Numerics;
using Starfall.Achievements;
using Starfall.Core;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.UI;

internal static class Fmt
{
    public static string Time(double sec)
    {
        int m = (int)(sec / 60);
        int h = m / 60;
        return h > 0 ? T("time.hm", ("h", h), ("m", m % 60)) : T("time.m", ("m", m));
    }
}

/// <summary>Ana menu: logo + dikey liste (solda), surum ve Steam rozeti.</summary>
public sealed class MainMenu : UiScreen
{
    public MainMenu(UiManager ui) : base(ui) => Dim = false;

    protected override void Rebuild()
    {
        var g = G;
        int? last = g.Profile.LastSlot;
        var lastData = last is int ls ? g.Saves.LoadSlot(ls) : null;
        if (lastData != null) Add(T("menu.continue"), () => g.LoadSlot(last!.Value));
        Add(T("menu.play"), () => Ui.Push(new SlotsScreen(Ui)));
        Add(T("menu.achievements"), () => Ui.Push(new AchievementsScreen(Ui)));
        Add(T("menu.settings"), () => Ui.Push(new SettingsScreen(Ui)));
        Add(T("menu.credits"), () => g.Credits.Show(null));
        Add(T("menu.quit"), () => g.QuitRequested = true);
    }

    public override void OnBack() { }

    public override void Draw(UiCtx c)
    {
        var D = c.D;
        float a = Appear;
        // soldan saga koyu gecis
        var dk = new Vector4(14 / 255f, 18 / 255f, 40 / 255f, 0.55f * a);
        D.RectGradH(0, 0, c.W * 0.45f, c.H, dk, dk with { W = 0.15f * a });
        D.RectGradH(c.W * 0.45f, 0, c.W * 0.25f + 1, c.H, dk with { W = 0.15f * a }, dk with { W = 0 });
        float lx = c.W * 0.07f, ly = 720 * 0.1f;
        float tw = MathF.Sin(c.Time * 2.1f) * 0.08f;
        D.Text("★", lx + 26, ly - 4, 51 * (1 + tw), C.A(C.Gold2, a), FontKind.Strong, Align.Center);
        D.Text(T("game.title"), lx, ly + 52, 73.6f, C.A(C.White, a), FontKind.Title, Align.Left, 3);
        D.Text(T("game.subtitle").ToUpperInvariant(), lx + 4, ly + 52 + 84, c.Px(19.2f), C.A(C.White, 0.9f * a), FontKind.Strong, Align.Left, 2);
        float y = 720 * 0.42f;
        float fs = c.Px(24.8f);
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i];
            bool focus = i == Index;
            float w = MathF.Max(300, D.Measure(it.Label, fs, FontKind.Title) + 40);
            float h = fs * 1.6f;
            float x = lx + (focus ? 10 : 0);
            it.X = lx; it.Y = y; it.W = w; it.H = h; it.Drawn = true;
            if (focus) D.Rect(x, y, w, h, C.A(C.Cream, 0.95f * a), 14);
            D.Text(it.Label, x + 18, UiCtx.Mid(y, h, fs), fs, focus ? C.A(C.Ink, a) : C.A(C.White, a), FontKind.Title, Align.Left, focus ? 0 : 2);
            y += h + 10;
        }
        string ver = $"v{Game.Version}";
        string badge = Steam.Available ? "Steam ✓" : T("menu.offline");
        float bfs = c.Px(13.6f);
        D.Text(ver, lx, 720 * 0.95f - 20, bfs, C.A(C.White, 0.75f * a), FontKind.Strong);
        float vw = D.Measure(ver, bfs, FontKind.Strong) + 18;
        float bw = D.Measure(badge, bfs, FontKind.Strong) + 20;
        c.Pill(lx + vw, 720 * 0.95f - 24, bw, 26, C.A(C.White, 0.15f * a));
        D.Text(badge, lx + vw + 10, 720 * 0.95f - 20, bfs, C.A(C.White, 0.75f * a), FontKind.Strong);
    }
}

/// <summary>Panel tabanli ekranlar icin ortak cizim (baslik, govde, alt bilgi).</summary>
public abstract class PanelScreen : UiScreen
{
    protected float PanelW = 880, PanelH = 600;
    protected bool Small;
    protected float BodyTop, BodyH, BodyX, BodyW, PanelX, PanelY;

    protected PanelScreen(UiManager ui) : base(ui) { }

    protected abstract string Title { get; }
    protected virtual (string, string)[] FooterPairs => new[] { ("back", T("ui.back")) };

    protected virtual void HeadExtra(UiCtx c, float right, float y) { }

    protected abstract float BodyHeight(UiCtx c);
    protected abstract void DrawBody(UiCtx c, float x, float y, float w);

    public override void Draw(UiCtx c)
    {
        var D = c.D;
        float a = Appear;
        if (Dim) DimBg(c, a);
        float w = MathF.Min(Small ? 520 : PanelW, c.W * 0.94f);
        float contentH = BodyHeight(c);
        float headH = 74, footH = 58;
        float h = MathF.Min(720 * 0.88f, headH + contentH + 26 + footH);
        float x = c.W / 2 - w / 2, y = 720 / 2f - h / 2 + (1 - a) * 10;
        PanelX = x; PanelY = y;
        c.Panel(x, y, w, h);
        D.Text(Title, x + 30, y + 18, c.Px(32), C.Ink, FontKind.Title);
        HeadExtra(c, x + w - 30, y + 22);
        BodyX = x + 30; BodyW = w - 60;
        BodyTop = y + headH;
        BodyH = h - headH - footH;
        ScrollMax = MathF.Max(0, contentH + 10 - BodyH);
        Scroll = Math.Clamp(Scroll, 0, ScrollMax);
        foreach (var it in Items) it.Drawn = false;
        D.PushScissor(x, BodyTop, w, BodyH);
        DrawBody(c, BodyX, BodyTop - Scroll, BodyW);
        D.PopScissor();
        if (ScrollMax > 0)
        {
            float th = BodyH * BodyH / (BodyH + ScrollMax);
            float ty = BodyTop + (BodyH - th) * (Scroll / ScrollMax);
            D.Rect(x + w - 12, ty, 5, th, C.A(C.InkSoft, 0.35f), 3);
        }
        c.Footer(x + w - 30, y + h - footH + 12, FooterPairs);
        // gorunmeyen ogeler fareyle secilmesin
        foreach (var it in Items)
            if (it.Drawn && (it.Y + it.H < BodyTop || it.Y > BodyTop + BodyH)) it.Drawn = false;
    }

    /// <summary>Odak degisince ogeyi gorunur tut (cizimde cagrilir).</summary>
    protected void Track(UiItem it, float y, float h)
    {
        if (Items.IndexOf(it) == Index && _lastFocus != Index)
        {
            KeepVisible(BodyTop, BodyH, y + Scroll, h);
            _lastFocus = Index;
        }
    }

    private int _lastFocus = -1;

    protected void Btn(UiCtx c, UiItem it, float x, float y, float w, float h, string? meta = null, bool danger = false)
    {
        var D = c.D;
        bool focus = Items.IndexOf(it) == Index;
        float ox = focus ? 6 : 0;
        it.X = x; it.Y = y; it.W = w; it.H = h; it.Drawn = true;
        D.Rect(x + ox, y + 3, w, h, new Vector4(0, 0, 0, 0.06f), 16);
        D.Rect(x + ox, y, w, h, focus ? (danger ? C.Hex("#ffb0b0") : C.Gold2) : C.White, 16);
        float fs = c.Px(20.8f);
        D.Text(it.Label, x + ox + 20, UiCtx.Mid(y, h, fs), fs, it.Disabled ? C.A(C.Ink, 0.4f) : C.Ink, FontKind.Title);
        if (meta != null)
        {
            var lines = meta.Split('\n');
            float mfs = c.Px(14.4f);
            float my = y + h / 2 - lines.Length * mfs * 1.3f / 2;
            foreach (var l in lines)
            {
                D.Text(l, x + ox + w - 20, my, mfs, C.InkSoft, FontKind.Strong, Align.Right);
                my += mfs * 1.3f;
            }
        }
        Track(it, y, h);
    }
}

public sealed class SlotsScreen : PanelScreen
{
    public SlotsScreen(UiManager ui) : base(ui) => Small = true;

    protected override string Title => T("slots.title");
    protected override (string, string)[] FooterPairs => new[] { ("confirm", T("ui.select")), ("interact", T("slots.delete")), ("back", T("ui.back")) };

    protected override void Rebuild()
    {
        foreach (var (slot, data) in G.Saves.ListSlots())
        {
            int sl = slot;
            Add(T("slots.slot", ("n", slot)), () => { if (data != null) G.LoadSlot(sl); else G.NewGame(sl); }, tag: (slot, data));
        }
    }

    protected override void OnInput(Input input)
    {
        if (input.Pressed("interact") && !input.Pressed("confirm") && Index < Items.Count)
        {
            var (slot, data) = ((int, SaveData?))Items[Index].Tag!;
            if (data == null) return;
            Ui.Push(new ConfirmScreen(Ui, T("slots.confirmDelete", ("n", slot)), () =>
            {
                G.Saves.DeleteSlot(slot);
                if (G.Profile.LastSlot == slot) G.Profile.LastSlot = null;
                G.SaveProfile();
                Refresh();
                foreach (var s in Ui.Stack) if (s is MainMenu) s.Refresh();
            }));
        }
    }

    protected override float BodyHeight(UiCtx c) => Items.Count * 76;

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        foreach (var it in Items)
        {
            var (_, data) = ((int, SaveData?))it.Tag!;
            string meta;
            if (data != null)
            {
                int shards = data.Collected.Count(id => id.Length == 3 && id[0] == 's' && char.IsDigit(id[1])) + data.Rewards.Count(r => WD.QuestShards.Contains(r));
                meta = $"★ {shards}/{WD.ShardTotal} · {Fmt.Time(data.PlayTime)}\n{DateTimeOffset.FromUnixTimeMilliseconds(data.Updated).LocalDateTime:dd.MM.yyyy}";
            }
            else meta = T("slots.empty");
            Btn(c, it, x, y, w, 66, meta);
            y += 76;
        }
    }
}

public sealed class ConfirmScreen : PanelScreen
{
    private readonly string _text;
    private readonly Action _onYes;

    public ConfirmScreen(UiManager ui, string text, Action onYes) : base(ui)
    {
        _text = text;
        _onYes = onYes;
        Small = true;
    }

    protected override string Title => "";
    protected override (string, string)[] FooterPairs => new[] { ("confirm", T("ui.select")), ("back", T("ui.back")) };

    protected override void Rebuild()
    {
        Add(T("ui.no"), () => Ui.Pop());
        Add(T("ui.yes"), () => { Ui.Pop(); _onYes(); }, tag: "danger");
    }

    protected override float BodyHeight(UiCtx c) => 60 + 2 * 62 + 20;

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        float h = c.D.Paragraph(_text, x, y, w, c.Px(17.6f), C.Ink, FontKind.Strong, 1.5f);
        y += MathF.Max(50, h) + 12;
        foreach (var it in Items)
        {
            Btn(c, it, x, y, w, 52, null, it.Tag as string == "danger");
            y += 62;
        }
    }
}

public sealed class PauseMenu : PanelScreen
{
    public PauseMenu(UiManager ui) : base(ui) => Small = true;

    protected override string Title => T("pause.title");
    protected override (string, string)[] FooterPairs => new[] { ("confirm", T("ui.select")), ("back", T("pause.resume")) };

    protected override void Rebuild()
    {
        var g = G;
        Add(T("pause.resume"), () => g.Resume());
        Add(T("pause.journal"), () => Ui.Push(new JournalScreen(Ui)));
        if (g.Save != null && g.Save.Outfits.Count > 1) Add(T("pause.wardrobe"), () => Ui.Push(new WardrobeScreen(Ui)));
        Add(T("menu.achievements"), () => Ui.Push(new AchievementsScreen(Ui)));
        Add(T("menu.settings"), () => Ui.Push(new SettingsScreen(Ui)));
        Add(T("pause.photo"), () => { g.Resume(); g.Photo.Enter(); });
        Add(T("pause.toMenu"), () => g.ToMainMenu());
    }

    public override void OnBack()
    {
        G.Audio.Sfx("uiBack");
        G.Resume();
    }

    protected override void HeadExtra(UiCtx c, float right, float y)
    {
        string s = $"{G.Progress.ShardCount()}/{WD.ShardTotal}";
        float fs = c.Px(16);
        float w = c.D.Measure(s, fs, FontKind.Strong);
        c.D.Text(s, right, y + 8, fs, C.InkSoft, FontKind.Strong, Align.Right);
        var tex = UiTex.Get("hud/star.png");
        if (tex != null) c.D.Image(tex, right - w - 30, y + 4, 26, 26);
    }

    protected override float BodyHeight(UiCtx c) => Items.Count * 62;

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        foreach (var it in Items)
        {
            Btn(c, it, x, y, w, 52);
            y += 62;
        }
    }
}

public sealed class SettingsScreen : PanelScreen
{
    private readonly List<(UiItem? Item, string? Section, string Label, Func<string>? Value, Kind K, Func<float>? Frac, Func<bool>? On)> _rows = new();
    private enum Kind { Section, Slider, Toggle, Chooser, Text }

    public SettingsScreen(UiManager ui) : base(ui) { }

    protected override string Title => T("settings.title");
    protected override (string, string)[] FooterPairs => new[] { ("left", "◀ ▶"), ("back", T("ui.back")) };

    protected override void Rebuild()
    {
        _rows.Clear();
        var S = G.Settings;
        var V = S.V;
        void Section(string k) => _rows.Add((null, T(k), "", null, Kind.Section, null, null));
        void Slider(string label, Func<float> get, Action<float> set, string key, float min = 0, float max = 1, float step = 0.1f)
        {
            var it = Add(label, null,
                () => { set(MathF.Max(min, MathF.Round((get() - step) * 100) / 100)); S.Touch(key); },
                () => { set(MathF.Min(max, MathF.Round((get() + step) * 100) / 100)); S.Touch(key); });
            _rows.Add((it, null, label, () => $"{MathF.Round(get() * 100)}%", Kind.Slider, () => (get() - min) / (max - min), null));
        }
        void Toggle(string label, Func<bool> get, Action<bool> set, string key)
        {
            void Flip() { set(!get()); S.Touch(key); }
            var it = Add(label, Flip, Flip, Flip);
            _rows.Add((it, null, label, null, Kind.Toggle, null, get));
        }
        void Chooser<T2>(string label, T2[] options, string[] names, Func<T2> get, Action<T2> set, string key)
        {
            void Step(int d)
            {
                int cur = Math.Max(0, Array.IndexOf(options, get()));
                set(options[(cur + d + options.Length) % options.Length]);
                S.Touch(key);
                if (key == "lang") Ui.Refresh();
            }
            var it = Add(label, () => Step(1), () => Step(-1), () => Step(1));
            _rows.Add((it, null, label, () => names[Math.Max(0, Array.IndexOf(options, get()))], Kind.Chooser, null, null));
        }

        Section("settings.general");
        Chooser(T("settings.language"), Loc.Langs, Loc.Langs.Select(l => T($"lang.{l}")).ToArray(), () => V.Lang ?? "en", v => V.Lang = v, "lang");
        Chooser(T("settings.textScale"), new[] { 1f, 1.15f, 1.3f }, new[] { "100%", "115%", "130%" }, () => V.TextScale, v => V.TextScale = v, "textScale");
        Section("settings.audio");
        Slider(T("settings.master"), () => V.Master, v => V.Master = v, "master");
        Slider(T("settings.music"), () => V.Music, v => V.Music = v, "music");
        Slider(T("settings.sfx"), () => V.Sfx, v => V.Sfx = v, "sfx");
        Slider(T("settings.ambience"), () => V.Ambience, v => V.Ambience = v, "ambience");
        Section("settings.video");
        Chooser(T("settings.quality"), new[] { "low", "medium", "high" }, new[] { T("settings.q.low"), T("settings.q.medium"), T("settings.q.high") }, () => V.Quality, v => V.Quality = v, "quality");
        Toggle(T("settings.fullscreen"), () => V.Fullscreen, v => V.Fullscreen = v, "fullscreen");
        Toggle(T("settings.vsync"), () => V.VSync, v => V.VSync = v, "vsync");
        Toggle(T("settings.showFps"), () => V.ShowFps, v => V.ShowFps = v, "showFps");
        Section("settings.controls");
        Slider(T("settings.sensitivity"), () => V.Sensitivity, v => V.Sensitivity = v, "sensitivity", 0.3f, 2.0f, 0.1f);
        Toggle(T("settings.invertY"), () => V.InvertY, v => V.InvertY = v, "invertY");
        Toggle(T("settings.autoCamera"), () => V.AutoCamera, v => V.AutoCamera = v, "autoCamera");
        Toggle(T("settings.cameraShake"), () => V.CameraShake, v => V.CameraShake = v, "cameraShake");
        Section("settings.controlsHelp");
        _rows.Add((null, null, T("settings.controlsText"), null, Kind.Text, null, null));
    }

    protected override float BodyHeight(UiCtx c)
    {
        float h = 0;
        foreach (var r in _rows)
            h += r.K switch
            {
                Kind.Section => 40,
                Kind.Text => c.D.Font!.Wrap(r.Label, c.Px(15), BodyW > 0 ? BodyW - 32 : 760, FontKind.Strong).Count * c.Px(15) * 1.6f + 10,
                _ => 46,
            };
        return h;
    }

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        foreach (var r in _rows)
        {
            if (r.K == Kind.Section)
            {
                D.Text(r.Section!.ToUpperInvariant(), x + 16, y + 14, c.Px(17.6f), C.Teal, FontKind.Title);
                y += 40;
                continue;
            }
            if (r.K == Kind.Text)
            {
                y += D.Paragraph(r.Label, x + 16, y + 4, w - 32, c.Px(15), C.InkSoft, FontKind.Strong, 1.6f) + 10;
                continue;
            }
            var it = r.Item!;
            bool focus = Items.IndexOf(it) == Index;
            const float h = 44;
            it.X = x; it.Y = y; it.W = w; it.H = h; it.Drawn = true;
            if (focus) D.Rect(x, y, w, h, C.Gold2, 14);
            float fs = c.Px(16.8f);
            D.Text(r.Label, x + 16, UiCtx.Mid(y, h, fs), fs, C.Ink, FontKind.Strong);
            float right = x + w - 16;
            if (r.K == Kind.Slider)
            {
                string v = r.Value!();
                float vw = D.Measure(v, fs, FontKind.Strong);
                D.Text(v, right, UiCtx.Mid(y, h, fs), fs, C.Ink, FontKind.Strong, Align.Right);
                float sx = right - vw - 10 - 160;
                D.Rect(sx, y + h / 2 - 5, 160, 10, C.Track, 5);
                D.Rect(sx, y + h / 2 - 5, 160 * Math.Clamp(r.Frac!(), 0, 1), 10, C.Teal, 5);
            }
            else if (r.K == Kind.Toggle)
            {
                bool on = r.On!();
                D.Rect(right - 52, y + h / 2 - 14, 52, 28, on ? C.Teal : C.Hex("#d8ccba"), 14);
                D.Rect(right - 52 + (on ? 27 : 3), y + h / 2 - 11, 22, 22, C.White, 11);
            }
            else
            {
                string v = r.Value!();
                float vw = D.Measure(v, fs, FontKind.Strong);
                D.Text("▶", right, UiCtx.Mid(y, h, fs), fs, C.InkSoft, FontKind.Strong, Align.Right);
                D.Text(v, right - 26, UiCtx.Mid(y, h, fs), fs, C.Ink, FontKind.Strong, Align.Right);
                D.Text("◀", right - 26 - vw - 10, UiCtx.Mid(y, h, fs), fs, C.InkSoft, FontKind.Strong, Align.Right);
            }
            Track(it, y, h);
            y += 46;
        }
    }
}

public sealed class AchievementsScreen : PanelScreen
{
    public AchievementsScreen(UiManager ui) : base(ui) { }

    protected override string Title => T("menu.achievements");

    protected override void Rebuild()
    {
        foreach (var a in AchievementData.All) Add(a.Id, tag: a);
    }

    protected override void HeadExtra(UiCtx c, float right, float y)
    {
        var tr = G.Achievements;
        int n = tr.Count, total = AchievementData.All.Count;
        string s = $"{n} / {total}";
        float fs = c.Px(16);
        c.D.Rect(right - 180, y + 10, 180, 12, C.Track, 6);
        c.D.Rect(right - 180, y + 10, 180 * n / (float)total, 12, C.Gold, 6);
        c.D.Text(s, right - 194, y + 5, fs, C.InkSoft, FontKind.Strong, Align.Right);
    }

    protected override float BodyHeight(UiCtx c)
    {
        int cols = Math.Max(1, (int)((BodyW > 0 ? BodyW : 820) + 12) / 262);
        Columns = cols;
        return (int)MathF.Ceiling(Items.Count / (float)cols) * 96;
    }

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        var tr = G.Achievements;
        int cols = Columns;
        float cw = (w - (cols - 1) * 12) / cols;
        for (int i = 0; i < Items.Count; i++)
        {
            var it = Items[i];
            var a = (AchievementDef)it.Tag!;
            bool un = tr.IsUnlocked(a.Id);
            bool hidden = a.Hidden && !un;
            float cx = x + (i % cols) * (cw + 12), cy = y + (i / cols) * 96;
            const float h = 84;
            bool focus = i == Index;
            it.X = cx; it.Y = cy; it.W = cw; it.H = h; it.Drawn = true;
            D.Rect(cx, cy, cw, h, C.White, 16);
            if (focus)
            {
                D.Rect(cx, cy, cw, 3, C.Gold, 2);
                D.Rect(cx, cy + h - 3, cw, 3, C.Gold, 2);
                D.Rect(cx, cy, 3, h, C.Gold, 2);
                D.Rect(cx + cw - 3, cy, 3, h, C.Gold, 2);
            }
            var tex = UiTex.Get(hidden ? "icons/ach/hidden.png" : un ? $"icons/ach/{a.Id}.png" : $"icons/ach/{a.Id}_locked.png");
            if (tex != null) D.Image(tex, cx + 10, cy + 10, 64, 64);
            else D.Rect(cx + 10, cy + 10, 64, 64, C.Track, 14);
            float tx = cx + 86, tw = cw - 96;
            string name = hidden ? T("ach.hidden") : T($"ach.{a.Id}.name");
            string desc = hidden ? T("ach.hiddenDesc") : T($"ach.{a.Id}.desc");
            D.Text(Hud.Ellipsize(D, name, c.Px(17.3f), tw, FontKind.Title), tx, cy + 10, c.Px(17.3f), un ? C.Ink : C.Hex("#8a8078"), FontKind.Title);
            var lines = D.Font!.Wrap(desc, c.Px(13.6f), tw, FontKind.Strong);
            for (int k = 0; k < Math.Min(2, lines.Count); k++)
                D.Text(lines[k], tx, cy + 36 + k * 17, c.Px(13.6f), C.InkSoft, FontKind.Strong);
            if (!un && a.Threshold > 1 && !hidden)
            {
                D.Rect(tx, cy + h - 12, tw, 6, C.Hex("#eee3d2"), 3);
                D.Rect(tx, cy + h - 12, tw * tr.Progress(a), 6, C.Teal, 3);
            }
            Track(it, cy, h);
        }
    }
}
