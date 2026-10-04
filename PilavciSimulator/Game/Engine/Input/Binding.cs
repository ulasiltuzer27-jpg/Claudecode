using Raylib_cs;

namespace PilavciSimulator.Engine.Input;

/// <summary>Tek bir tus ya da fare dugmesi. Ayar dosyasinda ad olarak saklanir.</summary>
public readonly record struct Binding(KeyboardKey Key, MouseButton? Mouse)
{
    public static bool TryParse(string name, out Binding binding)
    {
        if (name.StartsWith("Mouse.", StringComparison.Ordinal))
        {
            if (Enum.TryParse<MouseButton>(name[6..], ignoreCase: true, out var mb))
            {
                binding = new Binding(KeyboardKey.Null, mb);
                return true;
            }
        }
        else if (Enum.TryParse<KeyboardKey>(name, ignoreCase: true, out var key) && key != KeyboardKey.Null)
        {
            binding = new Binding(key, null);
            return true;
        }

        binding = default;
        return false;
    }

    public string Name => Mouse is { } m ? "Mouse." + m : Key.ToString();

    /// <summary>Ekranda gosterilecek kisa ad.</summary>
    public string Display => Mouse switch
    {
        MouseButton.Left => "LMB",
        MouseButton.Right => "RMB",
        MouseButton.Middle => "MMB",
        { } m => m.ToString(),
        null => Key switch
        {
            KeyboardKey.LeftShift => "Shift",
            KeyboardKey.RightShift => "R-Shift",
            KeyboardKey.LeftControl => "Ctrl",
            KeyboardKey.RightControl => "R-Ctrl",
            KeyboardKey.LeftAlt => "Alt",
            KeyboardKey.Space => "Space",
            KeyboardKey.Enter => "Enter",
            KeyboardKey.Tab => "Tab",
            KeyboardKey.Backspace => "Bksp",
            KeyboardKey.Up => "Up",
            KeyboardKey.Down => "Down",
            KeyboardKey.Left => "Left",
            KeyboardKey.Right => "Right",
            >= KeyboardKey.Zero and <= KeyboardKey.Nine => ((int)Key - (int)KeyboardKey.Zero).ToString(),
            _ => Key.ToString(),
        },
    };
}
