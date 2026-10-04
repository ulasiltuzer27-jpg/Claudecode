using System.Numerics;
using Silk.NET.Input;

namespace Starfall.Core;

/// <summary>
/// Girdi: klavye + fare + gamepad, "eylem" katmaniyla. Oyun kodu tus adi bilmez; 'jump',
/// 'interact' gibi eylemler sorar. Son kullanilan cihaz (klavye/gamepad) izlenir ki ekrandaki
/// ipuclari dogru tusu gostersin. Testler eylemleri dogrudan enjekte eder (Hold/Tap).
/// </summary>
public sealed class Input
{
    private static readonly Dictionary<string, Key[]> Keys = new()
    {
        ["jump"] = new[] { Key.Space },
        ["interact"] = new[] { Key.E, Key.F },
        ["sprint"] = new[] { Key.ShiftLeft, Key.ShiftRight },
        ["pause"] = new[] { Key.Escape },
        ["journal"] = new[] { Key.Tab, Key.J },
        ["photo"] = new[] { Key.P },
        ["up"] = new[] { Key.W, Key.Up },
        ["down"] = new[] { Key.S, Key.Down },
        ["left"] = new[] { Key.A, Key.Left },
        ["right"] = new[] { Key.D, Key.Right },
        ["confirm"] = new[] { Key.Enter, Key.KeypadEnter, Key.Space, Key.E },
        ["back"] = new[] { Key.Escape, Key.Backspace },
        ["camLeft"] = new[] { Key.Q },
        ["camRight"] = new[] { Key.R },
        ["shot"] = new[] { Key.Enter, Key.F12 },
        ["filter"] = new[] { Key.F },
        ["hide"] = new[] { Key.H },
        ["rise"] = new[] { Key.E },
        ["sink"] = new[] { Key.Q },
        ["tabPrev"] = new[] { Key.Q, Key.LeftBracket },
        ["tabNext"] = new[] { Key.E, Key.RightBracket },
        ["rotate"] = new[] { Key.R },
        ["remove"] = new[] { Key.X, Key.Delete },
        ["wardrobe"] = new[] { Key.G },
    };

    // Standart gamepad eslemesi (Xbox duzeni; Silk/GLFW sirasi)
    private static readonly Dictionary<string, ButtonName[]> Pad = new()
    {
        ["jump"] = new[] { ButtonName.A },
        ["interact"] = new[] { ButtonName.X },
        ["sprint"] = new[] { ButtonName.B, ButtonName.RightBumper },
        ["pause"] = new[] { ButtonName.Start },
        ["journal"] = new[] { ButtonName.Back },
        ["photo"] = new[] { ButtonName.Y },
        ["up"] = new[] { ButtonName.DPadUp },
        ["down"] = new[] { ButtonName.DPadDown },
        ["left"] = new[] { ButtonName.DPadLeft },
        ["right"] = new[] { ButtonName.DPadRight },
        ["confirm"] = new[] { ButtonName.A },
        ["back"] = new[] { ButtonName.B },
        ["shot"] = new[] { ButtonName.A },
        ["filter"] = new[] { ButtonName.RightBumper },
        ["hide"] = new[] { ButtonName.X },
        ["rise"] = Array.Empty<ButtonName>(),
        ["sink"] = Array.Empty<ButtonName>(),
        ["tabPrev"] = new[] { ButtonName.LeftBumper },
        ["tabNext"] = new[] { ButtonName.RightBumper },
        ["camLeft"] = Array.Empty<ButtonName>(),
        ["camRight"] = Array.Empty<ButtonName>(),
        ["rotate"] = new[] { ButtonName.Y },
        ["remove"] = new[] { ButtonName.X },
        ["wardrobe"] = Array.Empty<ButtonName>(),
    };

    private const float Dead = 0.18f;

    private readonly HashSet<Key> _down = new(), _pressed = new(), _released = new();
    private readonly HashSet<ButtonName> _padDown = new(), _padPrev = new();
    private readonly HashSet<string> _simHeld = new(), _simTap = new(), _simTapNext = new();
    private Vector2 _simMove;
    public Vector2 MouseDelta, MousePos;
    public float Wheel;
    public bool MouseLeft, MouseRight, MouseLeftPressed;
    private float _triggerL, _triggerR;
    private Vector2 _stickL, _stickR;
    public string Device = "kbm";
    public bool Enabled = true;
    public bool Locked;
    private IInputContext? _ctx;
    private IMouse? _mouse;
    private Vector2 _lastMouse;
    private bool _firstMouse = true;
    /// <summary>Yazi girisi (kayit adi vb. icin) — su an kullanilmiyor ama klavye karakterleri toplanir.</summary>
    public readonly List<char> Typed = new();

    public void Attach(IInputContext ctx)
    {
        _ctx = ctx;
        foreach (var kb in ctx.Keyboards)
        {
            kb.KeyDown += (_, k, _) =>
            {
                if (_down.Add(k)) _pressed.Add(k);
                Device = "kbm";
            };
            kb.KeyUp += (_, k, _) =>
            {
                _down.Remove(k);
                _released.Add(k);
            };
            kb.KeyChar += (_, c) => Typed.Add(c);
        }
        foreach (var m in ctx.Mice)
        {
            _mouse = m;
            m.MouseMove += (_, p) =>
            {
                if (_firstMouse) { _lastMouse = p; _firstMouse = false; }
                var d = p - _lastMouse;
                _lastMouse = p;
                MousePos = p;
                if (Locked || MouseRight) MouseDelta += d;
            };
            m.MouseDown += (_, b) =>
            {
                if (b == MouseButton.Left) { MouseLeft = true; MouseLeftPressed = true; }
                if (b == MouseButton.Right) MouseRight = true;
                Device = "kbm";
            };
            m.MouseUp += (_, b) =>
            {
                if (b == MouseButton.Left) MouseLeft = false;
                if (b == MouseButton.Right) MouseRight = false;
            };
            m.Scroll += (_, s) => Wheel += -MathF.Sign(s.Y);
        }
    }

    public void SetLocked(bool on)
    {
        if (Locked == on) return;
        Locked = on;
        if (_mouse != null)
        {
            try { _mouse.Cursor.CursorMode = on ? CursorMode.Raw : CursorMode.Normal; }
            catch
            {
                try { _mouse.Cursor.CursorMode = on ? CursorMode.Disabled : CursorMode.Normal; } catch { /* desteklenmiyor */ }
            }
        }
        _firstMouse = true;
    }

    private static float Deadzone(float v)
    {
        float a = MathF.Abs(v);
        if (a < Dead) return 0;
        return MathF.Sign(v) * ((a - Dead) / (1 - Dead));
    }

    /// <summary>Kare basinda: gamepad durumunu oku.</summary>
    public void Update()
    {
        _padPrev.Clear();
        foreach (var b in _padDown) _padPrev.Add(b);
        _padDown.Clear();
        _stickL = _stickR = Vector2.Zero;
        _triggerL = _triggerR = 0;
        // bir sonraki karede gecerli olacak test dokunuslari
        foreach (var a in _simTapNext) _simTap.Add(a);
        _simTapNext.Clear();
        if (_ctx == null) return;
        foreach (var gp in _ctx.Gamepads)
        {
            if (!gp.IsConnected) continue;
            foreach (var b in gp.Buttons) if (b.Pressed) _padDown.Add(b.Name);
            if (gp.Thumbsticks.Count > 0) _stickL = new Vector2(Deadzone(gp.Thumbsticks[0].X), Deadzone(gp.Thumbsticks[0].Y));
            if (gp.Thumbsticks.Count > 1) _stickR = new Vector2(Deadzone(gp.Thumbsticks[1].X), Deadzone(gp.Thumbsticks[1].Y));
            if (gp.Triggers.Count > 0) _triggerL = gp.Triggers[0].Position;
            if (gp.Triggers.Count > 1) _triggerR = gp.Triggers[1].Position;
            if (_triggerL > 0.5f) _padDown.Add(ButtonName.LeftStick);
            if (_padDown.Count > 0 || _stickL != Vector2.Zero || _stickR != Vector2.Zero) Device = "pad";
            break;
        }
    }

    /// <summary>Ayni karedeki sonraki alt adimlar "yeni basildi" olaylarini tekrar gormesin.</summary>
    public void ConsumeEdges()
    {
        _pressed.Clear();
        _released.Clear();
        _simTap.Clear();
        _padPrev.Clear();
        foreach (var b in _padDown) _padPrev.Add(b);
        MouseDelta = Vector2.Zero;
        Wheel = 0;
        MouseLeftPressed = false;
    }

    public void EndFrame()
    {
        _pressed.Clear();
        _released.Clear();
        _simTap.Clear();
        MouseDelta = Vector2.Zero;
        Wheel = 0;
        MouseLeftPressed = false;
        Typed.Clear();
    }

    public bool Held(string action)
    {
        if (!Enabled) return false;
        if (_simHeld.Contains(action)) return true;
        if (Keys.TryGetValue(action, out var ks)) foreach (var k in ks) if (_down.Contains(k)) return true;
        if (Pad.TryGetValue(action, out var bs)) foreach (var b in bs) if (_padDown.Contains(b)) return true;
        if (action == "sprint" && _triggerR > 0.5f) return true;
        if (action == "rise" && _triggerR > 0.5f) return true;
        if (action == "sink" && _triggerL > 0.5f) return true;
        return false;
    }

    public bool Pressed(string action)
    {
        if (!Enabled) return false;
        if (_simTap.Contains(action)) return true;
        if (Keys.TryGetValue(action, out var ks)) foreach (var k in ks) if (_pressed.Contains(k)) return true;
        if (Pad.TryGetValue(action, out var bs)) foreach (var b in bs) if (_padDown.Contains(b) && !_padPrev.Contains(b)) return true;
        return false;
    }

    public bool Released(string action)
    {
        if (Keys.TryGetValue(action, out var ks)) foreach (var k in ks) if (_released.Contains(k)) return true;
        if (Pad.TryGetValue(action, out var bs)) foreach (var b in bs) if (!_padDown.Contains(b) && _padPrev.Contains(b)) return true;
        return false;
    }

    /// <summary>Hareket vektoru: x sag, y ileri.</summary>
    public Vector2 Move()
    {
        if (!Enabled) return Vector2.Zero;
        float x = 0, y = 0;
        if (_down.Contains(Key.W) || _simHeld.Contains("up")) y += 1;
        if (_down.Contains(Key.S) || _simHeld.Contains("down")) y -= 1;
        if (_down.Contains(Key.A) || _simHeld.Contains("left")) x -= 1;
        if (_down.Contains(Key.D) || _simHeld.Contains("right")) x += 1;
        if (_down.Contains(Key.Up)) y += 1;
        if (_down.Contains(Key.Down)) y -= 1;
        if (_down.Contains(Key.Left)) x -= 1;
        if (_down.Contains(Key.Right)) x += 1;
        x += _stickL.X + _simMove.X;
        y += -_stickL.Y + _simMove.Y; // GLFW: cubuk yukari = negatif Y
        float len = MathF.Sqrt(x * x + y * y);
        if (len > 1) { x /= len; y /= len; }
        return new Vector2(x, y);
    }

    public Vector2 Look()
    {
        float x = MouseDelta.X * 0.0025f;
        float y = MouseDelta.Y * 0.0025f;
        x += _stickR.X * 0.045f;
        y += _stickR.Y * 0.035f;
        if (Held("camLeft")) x -= 0.03f;
        if (Held("camRight")) x += 0.03f;
        return new Vector2(x, y);
    }

    public void Rumble(float strength = 0.4f, int ms = 120)
    {
        // Silk.NET gamepad titresimi platforma gore degisir; desteklenmeyen yerde sessizce gecer.
        if (_ctx == null) return;
        try
        {
            foreach (var gp in _ctx.Gamepads)
                if (gp.IsConnected && gp.VibrationMotors.Count > 0)
                {
                    foreach (var m in gp.VibrationMotors) m.Speed = strength;
                    _rumbleT = ms / 1000f;
                    break;
                }
        }
        catch { /* yok say */ }
    }

    private float _rumbleT;

    public void Tick(float dt)
    {
        if (_rumbleT <= 0 || _ctx == null) return;
        _rumbleT -= dt;
        if (_rumbleT > 0) return;
        try
        {
            foreach (var gp in _ctx.Gamepads)
                foreach (var m in gp.VibrationMotors) m.Speed = 0;
        }
        catch { /* yok say */ }
    }

    public string Glyph(string action)
    {
        if (Device == "pad")
        {
            return action switch
            {
                "jump" => "A", "interact" => "X", "sprint" => "B", "pause" => "☰", "journal" => "⧉", "photo" => "Y",
                "confirm" => "A", "back" => "B", "shot" => "A", "filter" => "RB", "hide" => "X", "tabPrev" => "LB",
                "tabNext" => "RB", "left" => "✥", "rotate" => "Y", "remove" => "X", _ => "?",
            };
        }
        return action switch
        {
            "jump" => "Space", "interact" => "E", "sprint" => "Shift", "pause" => "Esc", "journal" => "Tab", "photo" => "P",
            "confirm" => "Enter", "back" => "Esc", "shot" => "Enter", "filter" => "F", "hide" => "H", "tabPrev" => "Q",
            "tabNext" => "E", "left" => "← →", "rotate" => "R", "remove" => "X", "wardrobe" => "G", _ => "?",
        };
    }

    // ---------------- test enjeksiyonu ----------------
    public void Hold(string action, bool on)
    {
        if (on) _simHeld.Add(action); else _simHeld.Remove(action);
    }

    /// <summary>Tek karelik basma (bir sonraki Update'te gecerli olur).</summary>
    public void Tap(string action) => _simTapNext.Add(action);

    public void SimMove(Vector2 v) => _simMove = v;

    public void ClearSim()
    {
        _simHeld.Clear();
        _simTap.Clear();
        _simTapNext.Clear();
        _simMove = Vector2.Zero;
    }
}
