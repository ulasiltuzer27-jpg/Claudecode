using System.Globalization;
using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Input;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;

namespace PilavciSimulator.Screens;

/// <summary>
/// Ayarlar: grafik, ses, kontroller (tus atama), oynanis. Degisiklikler
/// aninda uygulanir (pencere modu/cozunurluk haric: onlar "Uygula" ile,
/// yanlislikla siyah ekranda kalinmasin). Kapaninca diske yazilir.
/// </summary>
public sealed class SettingsScreen : OverlayScreen
{
    private enum Tab
    {
        Graphics,
        Audio,
        Controls,
        Gameplay,
    }

    private static readonly (int W, int H)[] Resolutions =
    [
        (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
    ];

    private static readonly int[] FpsOptions = [30, 60, 120, 144, 165, 240, 0];

    private Tab _tab;
    private float _scroll;
    private float _contentHeight;
    private (GameAction Action, int Slot)? _rebinding;
    private int _pendingMode;
    private int _pendingRes;
    private string _name = "";
    private float? _uiScalePending;

    public override void Enter()
    {
        base.Enter();
        var s = Game.Settings;
        _pendingMode = (int)s.WindowMode;
        _pendingRes = Array.FindIndex(Resolutions, r => r.W == s.Width && r.H == s.Height);
        if (_pendingRes < 0)
        {
            _pendingRes = 2;
        }

        _name = s.PlayerName;
    }

    public override void Exit()
    {
        base.Exit();
        Game.Settings.PlayerName = _name.Trim();
        Game.SaveSettings();
    }

    public override void Update(float dt, bool focused)
    {
        Anim = MathF.Min(1, Anim + dt * 6);
        if (!focused)
        {
            return;
        }

        if (_rebinding is { } rb)
        {
            CaptureBinding(rb.Action, rb.Slot);
            return;
        }

        if (Game.Input.MenuBack && !Game.Ui.IsEditingText)
        {
            Close();
        }

        _scroll = Math.Clamp(_scroll - Game.Input.Wheel * Game.Ui.S(60), 0, MathF.Max(0, _contentHeight));
    }

    /// <summary>Tus atama: Esc iptal, Backspace yuvayi temizler, diger her tus/fare dugmesi atanir.</summary>
    private void CaptureBinding(GameAction action, int slot)
    {
        var input = Game.Input;
        if (input.KeyPressed(KeyboardKey.Escape))
        {
            _rebinding = null;
            return;
        }

        if (input.KeyPressed(KeyboardKey.Backspace))
        {
            input.ClearSlot(action, slot);
            _rebinding = null;
            return;
        }

        var key = input.Source.FirstKeyPressed;
        if (key is not KeyboardKey.Null and not KeyboardKey.F12 and not KeyboardKey.Enter and not KeyboardKey.KpEnter)
        {
            input.Rebind(action, slot, new Binding(key, null));
            _rebinding = null;
            Game.Audio.Play("ui_click");
            return;
        }

        foreach (var mb in new[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.Side, MouseButton.Extra })
        {
            if (input.MousePressed(mb))
            {
                input.Rebind(action, slot, new Binding(KeyboardKey.Null, mb));
                _rebinding = null;
                Game.Audio.Play("ui_click");
                return;
            }
        }
    }

    public override void DrawUi()
    {
        var ui = Game.Ui;
        var area = Window(1040, 860, Loc.T("menu.settings"));

        // Sekmeler
        var tabs = Enum.GetValues<Tab>();
        var tw = (area.Width - ui.S(12) * (tabs.Length - 1)) / tabs.Length;
        for (var i = 0; i < tabs.Length; i++)
        {
            var r = new Rectangle(area.X + i * (tw + ui.S(12)), area.Y, tw, ui.S(54));
            if (ui.Button(r, Loc.T("settings.tab." + tabs[i].ToString().ToLowerInvariant()), _tab == tabs[i] ? ButtonStyle.TabActive : ButtonStyle.Tab, true, 26))
            {
                if (_tab != tabs[i])
                {
                    _tab = tabs[i];
                    _scroll = 0;
                    _rebinding = null;
                }
            }
        }

        var body = new Rectangle(area.X, area.Y + ui.S(72), area.Width, area.Height - ui.S(72) - ui.S(72));
        Raylib.BeginScissorMode((int)body.X - 4, (int)body.Y, (int)body.Width + 8, (int)body.Height);
        var y = body.Y - _scroll;
        var startY = y;
        switch (_tab)
        {
            case Tab.Graphics:
                y = Graphics(body, y);
                break;
            case Tab.Audio:
                y = AudioTab(body, y);
                break;
            case Tab.Controls:
                y = Controls(body, y);
                break;
            case Tab.Gameplay:
                y = Gameplay(body, y);
                break;
        }

        Raylib.EndScissorMode();
        _contentHeight = MathF.Max(0, y - startY - body.Height);
        if (_contentHeight > 0)
        {
            // Kaydirma cubugu
            var track = new Rectangle(body.X + body.Width + ui.S(8), body.Y, ui.S(6), body.Height);
            ui.Panel(track, new Color(255, 255, 255, 30), 3);
            var frac = body.Height / (body.Height + _contentHeight);
            var knobH = track.Height * frac;
            var knobY = track.Y + (track.Height - knobH) * (_scroll / _contentHeight);
            ui.Panel(new Rectangle(track.X, knobY, track.Width, knobH), Theme.Primary, 3);
        }

        // Alt satir
        var bottom = new Rectangle(area.X, area.Y + area.Height - ui.S(58), ui.S(260), ui.S(58));
        if (ui.Button(bottom, Loc.T("common.done"), ButtonStyle.Primary))
        {
            Close();
        }

        if (_tab == Tab.Controls)
        {
            var reset = bottom with { X = area.X + area.Width - ui.S(320), Width = ui.S(320) };
            if (ui.Button(reset, Loc.T("settings.reset_controls")))
            {
                Game.Input.ResetToDefaults();
                Game.Settings.MouseSensitivity = 1f;
                Game.Settings.InvertY = false;
                Game.ApplySettings();
            }
        }

        if (_rebinding is { } rb)
        {
            Raylib.DrawRectangle(0, 0, ui.Width, ui.Height, new Color(0, 0, 0, 160));
            var r = new Rectangle(ui.Width / 2f - ui.S(380), ui.Height / 2f - ui.S(90), ui.S(760), ui.S(180));
            ui.Panel(r, Theme.HudBgStrong);
            ui.Text(Loc.T("settings.press_key", Loc.T(GameActions.LocKey(rb.Action))), new Vector2(r.X + r.Width / 2, r.Y + ui.S(40)), 32, Theme.Yellow, true, Align.Center);
            ui.Text(Loc.T("settings.press_key_hint"), new Vector2(r.X + r.Width / 2, r.Y + ui.S(110)), 22, Theme.Cream, false, Align.Center);
        }
    }

    private Rectangle Row(Rectangle body, ref float y, float h = 58)
    {
        var r = new Rectangle(body.X, y, body.Width, Game.Ui.S(h));
        y += Game.Ui.S(h + 8);
        return r;
    }

    private void Header(Rectangle body, ref float y, string text)
    {
        var ui = Game.Ui;
        y += ui.S(6);
        ui.Text(text, new Vector2(body.X, y), 24, Theme.Yellow, true);
        y += ui.S(40);
    }

    private static string Pct(float v) => ((int)MathF.Round(v * 100)).ToString(CultureInfo.InvariantCulture) + "%";

    private float Graphics(Rectangle body, float y)
    {
        var ui = Game.Ui;
        var s = Game.Settings;
        Header(body, ref y, Loc.T("settings.display"));
        var modes = new[] { Loc.T("settings.windowed"), Loc.T("settings.borderless"), Loc.T("settings.fullscreen") };
        ui.Selector(Row(body, ref y), Loc.T("settings.window_mode"), modes, ref _pendingMode);
        var resNames = Resolutions.Select(r => $"{r.W} x {r.H}").ToArray();
        ui.Selector(Row(body, ref y), Loc.T("settings.resolution"), resNames, ref _pendingRes);
        var changed = _pendingMode != (int)s.WindowMode || Resolutions[_pendingRes] != (s.Width, s.Height);
        if (ui.Button(Row(body, ref y, 52) with { Width = ui.S(300) }, Loc.T("settings.apply_display"), changed ? ButtonStyle.Primary : ButtonStyle.Normal, changed, 24))
        {
            s.WindowMode = (WindowMode)_pendingMode;
            (s.Width, s.Height) = Resolutions[_pendingRes];
            Game.ApplyWindowMode();
            Game.SaveSettings();
        }

        var vsync = s.VSync;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.vsync"), ref vsync))
        {
            s.VSync = vsync;
            Game.ApplySettings();
        }

        var fpsIndex = Math.Max(0, Array.IndexOf(FpsOptions, s.FpsLimit));
        var fpsNames = FpsOptions.Select(f => f == 0 ? Loc.T("settings.unlimited") : f.ToString(CultureInfo.InvariantCulture)).ToArray();
        if (ui.Selector(Row(body, ref y), Loc.T("settings.fps_limit"), fpsNames, ref fpsIndex))
        {
            s.FpsLimit = FpsOptions[fpsIndex];
            Game.ApplySettings();
        }

        Header(body, ref y, Loc.T("settings.quality"));
        var shadow = s.ShadowQuality;
        var shadowNames = new[] { Loc.T("settings.off"), Loc.T("settings.low"), Loc.T("settings.medium"), Loc.T("settings.high") };
        if (ui.Selector(Row(body, ref y), Loc.T("settings.shadows"), shadowNames, ref shadow))
        {
            s.ShadowQuality = shadow;
            Game.ApplySettings();
        }

        var scale = s.RenderScale;
        if (ui.Slider(Row(body, ref y), Loc.T("settings.render_scale"), ref scale, 0.5f, 1f, Pct(scale), 0.1f))
        {
            s.RenderScale = MathF.Round(scale * 20) / 20;
            Game.ApplySettings();
        }

        var fxaa = s.Fxaa;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.fxaa"), ref fxaa))
        {
            s.Fxaa = fxaa;
            Game.ApplySettings();
        }

        var fov = s.Fov;
        if (ui.Slider(Row(body, ref y), Loc.T("settings.fov"), ref fov, 60, 100, ((int)fov).ToString(CultureInfo.InvariantCulture) + "°", 0.05f))
        {
            s.Fov = MathF.Round(fov);
        }

        var bright = s.Brightness;
        if (ui.Slider(Row(body, ref y), Loc.T("settings.brightness"), ref bright, 0.6f, 1.6f, Pct(bright), 0.05f))
        {
            s.Brightness = bright;
            Game.ApplySettings();
        }

        return y;
    }

    private float AudioTab(Rectangle body, float y)
    {
        var ui = Game.Ui;
        var s = Game.Settings;
        Header(body, ref y, Loc.T("settings.volume"));
        void Vol(string key, Func<float> get, Action<float> set, ref float yy)
        {
            var v = get();
            if (ui.Slider(Row(body, ref yy), Loc.T(key), ref v, 0, 1, Pct(v), 0.05f))
            {
                set(v);
                Game.ApplySettings();
            }
        }

        Vol("settings.master", () => s.MasterVolume, v => s.MasterVolume = v, ref y);
        Vol("settings.sfx", () => s.SfxVolume, v => s.SfxVolume = v, ref y);
        Vol("settings.music", () => s.MusicVolume, v => s.MusicVolume = v, ref y);
        Vol("settings.ambient", () => s.AmbientVolume, v => s.AmbientVolume = v, ref y);
        y += ui.S(10);
        ui.Paragraph(Loc.T("settings.music_hint"), new Vector2(body.X, y), 20, body.Width, Theme.CreamDark);
        y += ui.S(60);
        return y;
    }

    private float Controls(Rectangle body, float y)
    {
        var ui = Game.Ui;
        var s = Game.Settings;
        Header(body, ref y, Loc.T("settings.mouse"));
        var sens = s.MouseSensitivity;
        if (ui.Slider(Row(body, ref y), Loc.T("settings.sensitivity"), ref sens, 0.1f, 4f, sens.ToString("0.00", CultureInfo.InvariantCulture), 0.025f))
        {
            s.MouseSensitivity = sens;
            Game.ApplySettings();
        }

        var inv = s.InvertY;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.invert_y"), ref inv))
        {
            s.InvertY = inv;
            Game.ApplySettings();
        }

        Header(body, ref y, Loc.T("settings.keys"));
        var slotW = ui.S(190);
        foreach (var a in GameActions.All)
        {
            var row = new Rectangle(body.X, y, body.Width, ui.S(52));
            ui.TextIn(row, Loc.T(GameActions.LocKey(a)), 24, Theme.Cream);
            var binds = Game.Input.BindingsOf(a);
            for (var slot = 0; slot < 2; slot++)
            {
                var r = new Rectangle(body.X + body.Width - (2 - slot) * (slotW + ui.S(10)), y + ui.S(4), slotW, ui.S(44));
                var label = slot < binds.Count ? binds[slot].Display : "—";
                var active = _rebinding is { } rb && rb.Action == a && rb.Slot == slot;
                if (ui.Button(r, active ? "..." : label, active ? ButtonStyle.Primary : ButtonStyle.Normal, true, 22) && _rebinding is null)
                {
                    _rebinding = (a, Math.Min(slot, binds.Count));
                }
            }

            y += ui.S(58);
        }

        y += ui.S(6);
        ui.Paragraph(Loc.T("settings.fixed_keys"), new Vector2(body.X, y), 20, body.Width, Theme.CreamDark);
        y += ui.S(70);
        return y;
    }

    private float Gameplay(Rectangle body, float y)
    {
        var ui = Game.Ui;
        var s = Game.Settings;
        Header(body, ref y, Loc.T("settings.general"));
        var langs = new[] { "Türkçe", "English" };
        var li = s.Language == "en" ? 1 : 0;
        if (ui.Selector(Row(body, ref y), Loc.T("settings.language"), langs, ref li))
        {
            s.Language = li == 1 ? "en" : "tr";
            Game.ApplySettings();
        }

        if (!Game.Platform.IsSteam)
        {
            var row = Row(body, ref y);
            ui.TextIn(row with { Width = row.Width * 0.42f }, Loc.T("settings.player_name"), 26, Theme.Cream);
            ui.TextField(row with { X = row.X + row.Width * 0.42f, Width = row.Width * 0.58f }, ref _name, 20, Game.Platform.PlayerName);
        }

        var bob = s.HeadBob;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.head_bob"), ref bob))
        {
            s.HeadBob = bob;
        }

        var hints = s.ShowHints;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.hints"), ref hints))
        {
            s.ShowHints = hints;
        }

        var subs = s.Subtitles;
        if (ui.Toggle(Row(body, ref y), Loc.T("settings.bubbles"), ref subs))
        {
            s.Subtitles = subs;
        }

        // Arayuz olcegi surukleme bitince uygulanir: surukleme sirasinda
        // degisirse kaydirici farenin altindan kayar.
        var uiScale = _uiScalePending ?? s.UiScale;
        if (ui.Slider(Row(body, ref y), Loc.T("settings.ui_scale"), ref uiScale, 0.75f, 1.5f, Pct(uiScale), 0.05f))
        {
            _uiScalePending = MathF.Round(uiScale * 20) / 20;
        }

        if (_uiScalePending is { } pending && !Game.Input.MouseDown())
        {
            s.UiScale = pending;
            _uiScalePending = null;
        }

        y += ui.S(10);
        ui.Paragraph(Loc.T("settings.data_path", Paths.UserRoot), new Vector2(body.X, y), 18, body.Width, Theme.CreamDark);
        y += ui.S(60);
        return y;
    }

    public override string Annotate() => $"ayarlar: sekme={_tab} atama={_rebinding?.Action.ToString() ?? "-"}";

    public override bool Command(string[] args)
    {
        if (args[0] == "settings-tab")
        {
            _tab = Enum.Parse<Tab>(args[1], ignoreCase: true);
            _scroll = 0;
            return true;
        }

        return false;
    }
}
