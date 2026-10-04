using System.Numerics;
using Starfall.Core;

namespace Starfall.UI;

/// <summary>
/// Arayuz koku: HUD, diyalog, menu ekranlari yigini, jenerik, photo mode. Menu ekranlari
/// klavye, fare ve gamepad ile ayni sekilde gezilir.
/// </summary>
public sealed class UiManager
{
    public readonly Game G;
    public readonly List<UiScreen> Stack = new();
    public Font? Font;
    private UiDrawList? _list;
    private readonly UiCtx _ctx = new();
    private Vector2 _lastMouse;
    private bool _click, _moved;
    private float _wheel;
    public float Time;

    public UiManager(Game g) => G = g;

    public void OnHostReady()
    {
        Font = new Font();
        _list = new UiDrawList { Font = Font };
        G.Env.Ui = _list;
    }

    public void Push(UiScreen s)
    {
        Stack.Add(s);
        s.Mount();
    }

    public UiScreen? Pop()
    {
        if (Stack.Count == 0) return null;
        var s = Stack[^1];
        Stack.RemoveAt(Stack.Count - 1);
        s.Unmount();
        // oyun icinden acilan tek ekran kapandiysa oyuna don
        if (Stack.Count == 0 && G.State == GameState.Paused) G.Resume();
        return s;
    }

    public void Replace(UiScreen s)
    {
        if (Stack.Count > 0)
        {
            var old = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            old.Unmount();
        }
        Push(s);
    }

    public void Clear() => Stack.Clear();

    public UiScreen? Top => Stack.Count > 0 ? Stack[^1] : null;

    public void Refresh()
    {
        foreach (var s in Stack) s.Refresh();
    }

    public void HandleInput(Input input)
    {
        if (G.Credits.Active) { G.Credits.Handle(input); return; }
        Top?.Handle(input);
    }

    public void Update(float dt)
    {
        Time += dt;
        foreach (var s in Stack) s.Update(dt);
        var input = G.Input;
        var m = MouseVirtual();
        _moved |= Vector2.DistanceSquared(m, _lastMouse) > 0.25f;
        _lastMouse = m;
        _click |= input.MouseLeftPressed;
        _wheel += input.Wheel;
        // fare etkilesimi (son cizimdeki oge yerleriyle)
        _ctx.Mouse = m;
        _ctx.Click = _click;
        _ctx.MouseMoved = _moved;
        _ctx.G = G;
        bool menu = G.State is GameState.Menu or GameState.Paused;
        if (menu && !G.Credits.Active && Top is { } top && !G.Input.Locked)
        {
            top.Mouse(_ctx);
            if (_wheel != 0)
            {
                top.Scroll = Math.Clamp(top.Scroll + _wheel * 60, 0, MathF.Max(0, top.ScrollMax));
            }
        }
        if (G.State == GameState.Dialogue && _click && !G.Input.Locked) DialogueClick();
        _click = false;
        _moved = false;
        _wheel = 0;
    }

    private void DialogueClick()
    {
        var d = G.Dialogue;
        if (!d.Active) return;
        if (d.ShowingChoices && d.Choices != null)
        {
            for (int i = 0; i < d.Choices.Count; i++)
                if (i < Hud.ChoiceRects.Count && _ctx.Hover(Hud.ChoiceRects[i].X, Hud.ChoiceRects[i].Y, Hud.ChoiceRects[i].Z, Hud.ChoiceRects[i].W)) { d.Pick(i); return; }
            return;
        }
        d.Advance();
    }

    private Vector2 MouseVirtual()
    {
        var host = G.Host;
        if (host == null) return Vector2.Zero;
        var ls = host.LogicalSize;
        if (ls.Y <= 0) return Vector2.Zero;
        return G.Input.MousePos * (720f / ls.Y);
    }

    /// <summary>Her karede (pencere varsa) cizim listesini bastan kur.</summary>
    public void Draw(int fbW, int fbH)
    {
        if (_list == null) return;
        float scale = fbH / 720f;
        _list.Begin(scale);
        var c = _ctx;
        c.D = _list;
        c.W = fbW / scale;
        c.H = 720;
        c.Ts = G.Settings.V.TextScale;
        c.Time = Time;
        c.G = G;
        try
        {
            G.Hud.Draw(c);
            for (int i = 0; i < Stack.Count; i++) Stack[i].Draw(c);
            G.Credits.Draw(c);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[ui] " + ex);
        }
    }
}
