using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Input;

namespace PilavciSimulator.Diagnostics;

/// <summary>
/// Senaryo dosyasindan beslenen sanal girdi. Gercek klavye/fare tamamen
/// yok sayilir; boylece otomatik dogrulama, Xvfb altinda bile tekrar
/// uretilebilir sonuc verir.
/// </summary>
public sealed class ScriptedInputSource : IInputSource
{
    private readonly HashSet<KeyboardKey> _down = new();
    private readonly HashSet<KeyboardKey> _pressedThisFrame = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressedThisFrame = new();
    private readonly List<int> _chars = new();
    private readonly List<int> _pendingChars = new();
    private readonly HashSet<KeyboardKey> _pendingPress = new();
    private readonly HashSet<MouseButton> _pendingMousePress = new();
    private Vector2 _pendingDelta;
    private KeyboardKey _firstKey;

    public Vector2 MousePosition { get; set; }
    public Vector2 MouseDelta { get; private set; }
    public float Wheel { get; private set; }
    public IReadOnlyList<int> Chars => _chars;
    public KeyboardKey FirstKeyPressed => _firstKey;
    public bool GamepadAvailable => false;

    public void Poll()
    {
        _pressedThisFrame.Clear();
        _mousePressedThisFrame.Clear();
        foreach (var k in _pendingPress)
        {
            _pressedThisFrame.Add(k);
        }

        foreach (var b in _pendingMousePress)
        {
            _mousePressedThisFrame.Add(b);
        }

        _firstKey = _pendingPress.Count > 0 ? _pendingPress.First() : KeyboardKey.Null;
        _pendingPress.Clear();
        _pendingMousePress.Clear();
        MouseDelta = _pendingDelta;
        _pendingDelta = Vector2.Zero;
        _chars.Clear();
        _chars.AddRange(_pendingChars);
        _pendingChars.Clear();
        Wheel = 0;
    }

    public void KeyDown(KeyboardKey k)
    {
        if (_down.Add(k))
        {
            _pendingPress.Add(k);
        }
    }

    public void KeyUp(KeyboardKey k) => _down.Remove(k);

    public void MouseButtonDown(MouseButton b)
    {
        if (_mouseDown.Add(b))
        {
            _pendingMousePress.Add(b);
        }
    }

    public void MouseButtonUp(MouseButton b) => _mouseDown.Remove(b);

    public void AddMouseDelta(Vector2 d) => _pendingDelta += d;

    public void Type(string text)
    {
        foreach (var r in text.EnumerateRunes())
        {
            _pendingChars.Add(r.Value);
        }
    }

    public bool IsKeyDown(KeyboardKey key) => _down.Contains(key);
    public bool WasKeyPressed(KeyboardKey key) => _pressedThisFrame.Contains(key);
    public bool IsMouseDown(MouseButton button) => _mouseDown.Contains(button);
    public bool WasMousePressed(MouseButton button) => _mousePressedThisFrame.Contains(button);
    public float Axis(GamepadAxis axis) => 0f;
    public bool IsPadDown(GamepadButton button) => false;
}
