using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Input;

/// <summary>
/// Eylem tabanli girdi. Her kare kaynaktan okunur, eylem basina "basili",
/// "bu kare basildi", "bu kare birakildi" hesaplanir. Klavye/fare
/// atamalari ve gamepad ayni eylemlere duser.
///
/// Gamepad: sol cubuk hareket, sag cubuk bakis, A ziplama, X etkilesim,
/// B ikincil, Y birak, RT kullan, LT alternatif, LB kos, RB egil,
/// Select telefon, Start menu.
/// </summary>
public sealed class InputSystem
{
    private readonly Dictionary<GameAction, Binding[]> _bindings = new();
    private readonly bool[] _down = new bool[GameActions.All.Length];
    private readonly bool[] _prev = new bool[GameActions.All.Length];
    private readonly bool[] _pressedEdge = new bool[GameActions.All.Length];

    public IInputSource Source { get; set; }
    public float MouseSensitivity { get; set; } = 1f;
    public bool InvertY { get; set; }

    /// <summary>Arayuz klavyeyi yakaladiginda (sohbet yazarken) eylemler devre disi.</summary>
    public bool TextCapture { get; set; }

    public InputSystem(IInputSource source)
    {
        Source = source;
        ResetToDefaults();
    }

    public void ResetToDefaults()
    {
        foreach (var a in GameActions.All)
        {
            _bindings[a] = GameActions.Defaults(a).Select(n => Binding.TryParse(n, out var b) ? b : default).ToArray();
        }
    }

    public void Load(Dictionary<string, string[]> saved)
    {
        ResetToDefaults();
        foreach (var (name, keys) in saved)
        {
            if (!Enum.TryParse<GameAction>(name, out var action))
            {
                continue;
            }

            var list = new List<Binding>();
            foreach (var k in keys.Take(2))
            {
                if (Binding.TryParse(k, out var b))
                {
                    list.Add(b);
                }
            }

            if (list.Count > 0)
            {
                _bindings[action] = list.ToArray();
            }
        }
    }

    public Dictionary<string, string[]> Save() =>
        _bindings.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Select(b => b.Name).ToArray());

    public IReadOnlyList<Binding> BindingsOf(GameAction a) => _bindings[a];

    /// <summary>
    /// Bir eylemin bir yuvasina tus atar. Ayni tus baska bir eylemde
    /// kullaniliyorsa oradan alinir: bir tus iki isi birden yapmasin.
    /// </summary>
    public void Rebind(GameAction action, int slot, Binding binding)
    {
        foreach (var other in GameActions.All)
        {
            if (other == action)
            {
                continue;
            }

            var arr = _bindings[other];
            if (arr.Contains(binding))
            {
                _bindings[other] = arr.Where(b => b != binding).ToArray();
            }
        }

        var cur = _bindings[action].ToList();
        cur.Remove(binding);
        if (slot >= cur.Count)
        {
            cur.Add(binding);
        }
        else
        {
            cur[slot] = binding;
        }

        _bindings[action] = cur.Take(2).ToArray();
    }

    public void ClearSlot(GameAction action, int slot)
    {
        var cur = _bindings[action].ToList();
        if (slot < cur.Count && cur.Count > 1)
        {
            cur.RemoveAt(slot);
        }

        _bindings[action] = cur.ToArray();
    }

    public void Update()
    {
        Source.Poll();
        for (var i = 0; i < GameActions.All.Length; i++)
        {
            var a = GameActions.All[i];
            _prev[i] = _down[i];
            var down = false;
            var edge = false;
            if (!TextCapture)
            {
                foreach (var b in _bindings[a])
                {
                    if (b.Mouse is { } mb)
                    {
                        down |= Source.IsMouseDown(mb);
                        edge |= Source.WasMousePressed(mb);
                    }
                    else if (b.Key != KeyboardKey.Null)
                    {
                        down |= Source.IsKeyDown(b.Key);
                        edge |= Source.WasKeyPressed(b.Key) && !Source.IsKeyDown(b.Key);
                    }
                }

                down |= PadDown(a);
            }

            _down[i] = down;
            // Ayni karede basilip birakilan tuslari kacirmamak icin kaynak kenari da sayilir.
            _pressedEdge[i] = edge;
        }
    }

    private bool PadDown(GameAction a)
    {
        if (!Source.GamepadAvailable)
        {
            return false;
        }

        return a switch
        {
            GameAction.Jump => Source.IsPadDown(GamepadButton.RightFaceDown),
            GameAction.Interact => Source.IsPadDown(GamepadButton.RightFaceLeft),
            GameAction.Secondary => Source.IsPadDown(GamepadButton.RightFaceRight),
            GameAction.Drop => Source.IsPadDown(GamepadButton.RightFaceUp),
            GameAction.Use => Source.Axis(GamepadAxis.RightTrigger) > 0.3f,
            GameAction.AltUse => Source.Axis(GamepadAxis.LeftTrigger) > 0.3f,
            GameAction.Sprint => Source.IsPadDown(GamepadButton.LeftTrigger1),
            GameAction.Crouch => Source.IsPadDown(GamepadButton.RightTrigger1),
            GameAction.Phone => Source.IsPadDown(GamepadButton.MiddleLeft),
            _ => false,
        };
    }

    public bool Down(GameAction a) => _down[(int)a];
    public bool Pressed(GameAction a) => (_down[(int)a] && !_prev[(int)a]) || _pressedEdge[(int)a];
    public bool Released(GameAction a) => !_down[(int)a] && _prev[(int)a];

    /// <summary>Hareket vektoru: X sag, Y ileri. Uzunlugu en fazla 1.</summary>
    public Vector2 Move
    {
        get
        {
            var v = Vector2.Zero;
            if (Down(GameAction.MoveForward)) v.Y += 1;
            if (Down(GameAction.MoveBack)) v.Y -= 1;
            if (Down(GameAction.MoveRight)) v.X += 1;
            if (Down(GameAction.MoveLeft)) v.X -= 1;
            if (!TextCapture && Source.GamepadAvailable)
            {
                v.X += Source.Axis(GamepadAxis.LeftX);
                v.Y -= Source.Axis(GamepadAxis.LeftY);
            }

            return v.LengthSquared() > 1 ? Vector2.Normalize(v) : v;
        }
    }

    /// <summary>Bakis degisimi (radyan): X yaw, Y pitch.</summary>
    public Vector2 Look
    {
        get
        {
            var d = Source.MouseDelta * (0.0022f * MouseSensitivity);
            if (Source.GamepadAvailable)
            {
                d.X += Source.Axis(GamepadAxis.RightX) * 0.045f * MouseSensitivity;
                d.Y += Source.Axis(GamepadAxis.RightY) * 0.035f * MouseSensitivity;
            }

            return new Vector2(d.X, InvertY ? d.Y : -d.Y);
        }
    }

    // ── Arayuz icin ham erisim (atanamaz tuslar) ───────────────────────
    public bool KeyPressed(KeyboardKey k) => Source.WasKeyPressed(k);
    public bool KeyDown(KeyboardKey k) => Source.IsKeyDown(k);
    public Vector2 MousePosition => Source.MousePosition;
    public bool MousePressed(MouseButton b = MouseButton.Left) => Source.WasMousePressed(b);
    public bool MouseDown(MouseButton b = MouseButton.Left) => Source.IsMouseDown(b);
    public float Wheel => Source.Wheel;

    public bool MenuBack => Source.WasKeyPressed(KeyboardKey.Escape) || (Source.GamepadAvailable && Source.IsPadDown(GamepadButton.MiddleRight) && !_padStartPrev);

    private bool _padStartPrev;

    /// <summary>Kare sonunda cagrilir: gamepad Start kenari icin.</summary>
    public void EndFrame() => _padStartPrev = Source.GamepadAvailable && Source.IsPadDown(GamepadButton.MiddleRight);
}
