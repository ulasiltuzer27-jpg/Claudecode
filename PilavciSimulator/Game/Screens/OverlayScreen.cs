using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;

namespace PilavciSimulator.Screens;

/// <summary>Oyunun ustune acilan panel: arka plan karartilir, imlec gorunur.</summary>
public abstract class OverlayScreen : Screen
{
    public override bool IsOverlay => true;
    public override bool ShowCursor => true;

    /// <summary>Co-op'ta oyun durmaz; tek oyunculuda menuler oyunu durdurmaz da (laptop, kasa) — yalnizca duraklatma durdurur.</summary>
    public override bool PausesBelow => false;

    protected float Anim;

    public override void Enter()
    {
        Game.Ui.ResetFocus();
        Game.Audio.Play("ui_open", 0.6f);
    }

    public override void Exit() => Game.Audio.Play("ui_close", 0.5f);

    public override void Update(float dt, bool focused)
    {
        Anim = MathF.Min(1, Anim + dt * 6);
        if (focused && Game.Input.MenuBack)
        {
            Close();
        }
    }

    protected virtual void Close() => Game.Screens.Remove(this);

    protected Rectangle Window(float w, float h, string title, float dim = 0.55f)
    {
        var ui = Game.Ui;
        Raylib.DrawRectangle(0, 0, ui.Width, ui.Height, new Color(0, 0, 0, (int)(255 * dim * Anim)));
        var ww = ui.S(w);
        var hh = ui.S(h);
        var r = new Rectangle((ui.Width - ww) / 2, (ui.Height - hh) / 2 + (1 - Anim) * ui.S(30), ww, hh);
        ui.Panel(new Rectangle(r.X + ui.S(6), r.Y + ui.S(8), r.Width, r.Height), new Color(0, 0, 0, 90), 18);
        ui.Panel(r, Gfx.Hex(0x2B211A), 18);
        ui.Panel(new Rectangle(r.X, r.Y, r.Width, ui.S(70)), Theme.Primary, 18);
        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y + ui.S(50), r.Width, ui.S(20)), Theme.Primary);
        ui.Text(title, new Vector2(r.X + ui.S(28), r.Y + ui.S(14)), 36, Theme.White, true);
        if (ui.Button(new Rectangle(r.X + r.Width - ui.S(62), r.Y + ui.S(13), ui.S(46), ui.S(44)), "X", ButtonStyle.Ghost, true, 26))
        {
            Close();
        }

        return new Rectangle(r.X + ui.S(28), r.Y + ui.S(90), r.Width - ui.S(56), r.Height - ui.S(112));
    }
}
