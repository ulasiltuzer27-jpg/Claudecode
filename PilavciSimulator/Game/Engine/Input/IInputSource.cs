using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Input;

/// <summary>
/// Ham girdi kaynagi. Gercek oyunda raylib; otomatik dogrulamada senaryo
/// dosyasi (bkz. Diagnostics/CaptureHarness). Oyun kodu yalnizca
/// <see cref="InputSystem"/>'i gorur, kaynagin hangisi oldugunu bilmez.
/// </summary>
public interface IInputSource
{
    void Poll();
    bool IsKeyDown(KeyboardKey key);
    bool WasKeyPressed(KeyboardKey key);
    bool IsMouseDown(MouseButton button);
    bool WasMousePressed(MouseButton button);
    Vector2 MousePosition { get; }
    Vector2 MouseDelta { get; }
    float Wheel { get; }
    /// <summary>Bu karede yazilan karakterler (sohbet, isim alani).</summary>
    IReadOnlyList<int> Chars { get; }
    /// <summary>Bu karede basilan ilk tus (tus atama ekrani).</summary>
    KeyboardKey FirstKeyPressed { get; }
    bool GamepadAvailable { get; }
    float Axis(GamepadAxis axis);
    bool IsPadDown(GamepadButton button);
}

public sealed class RaylibInputSource : IInputSource
{
    private readonly List<int> _chars = new();

    public Vector2 MousePosition { get; private set; }
    public Vector2 MouseDelta { get; private set; }
    public float Wheel { get; private set; }
    public IReadOnlyList<int> Chars => _chars;
    public KeyboardKey FirstKeyPressed { get; private set; }
    public bool GamepadAvailable { get; private set; }

    public void Poll()
    {
        MousePosition = Raylib.GetMousePosition();
        MouseDelta = Raylib.GetMouseDelta();
        Wheel = Raylib.GetMouseWheelMove();
        _chars.Clear();
        int c;
        while ((c = Raylib.GetCharPressed()) != 0)
        {
            _chars.Add(c);
        }

        FirstKeyPressed = (KeyboardKey)Raylib.GetKeyPressed();
        // Kuyrukta kalanlari bosalt (bir sonraki karede tekrar gelmesin)
        while (Raylib.GetKeyPressed() != 0)
        {
        }

        GamepadAvailable = Raylib.IsGamepadAvailable(0);
    }

    public bool IsKeyDown(KeyboardKey key) => Raylib.IsKeyDown(key);
    public bool WasKeyPressed(KeyboardKey key) => Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);
    public bool IsMouseDown(MouseButton button) => Raylib.IsMouseButtonDown(button);
    public bool WasMousePressed(MouseButton button) => Raylib.IsMouseButtonPressed(button);

    public float Axis(GamepadAxis axis)
    {
        if (!GamepadAvailable)
        {
            return 0f;
        }

        var v = Raylib.GetGamepadAxisMovement(0, axis);
        return MathF.Abs(v) < 0.18f ? 0f : v;
    }

    public bool IsPadDown(GamepadButton button) => GamepadAvailable && Raylib.IsGamepadButtonDown(0, button);
}
