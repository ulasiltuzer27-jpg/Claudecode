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

    /// <summary>Baslikta kapatma (X) dugmesi var mi.</summary>
    protected virtual bool Closable => true;

    /// <summary>Pencere basligindaki ikon (alt siniflar degistirir).</summary>
    protected virtual Icon TitleIcon => Icon.None;

    /// <summary>
    /// Ortalanmis pencere: yumusak golge, koyu ahsap govde, turuncu baslik
    /// bandi ve altinda dukkan tentesi gibi dalgali sacak. Icerik alanini dondurur.
    /// </summary>
    protected Rectangle Window(float w, float h, string title, float dim = 0.55f, Icon icon = Icon.None)
    {
        var ui = Game.Ui;
        if (icon == Icon.None)
        {
            icon = TitleIcon;
        }

        var ease = 1f - MathF.Pow(1f - Anim, 3f);
        Raylib.DrawRectangle(0, 0, ui.Width, ui.Height, new Color(0, 0, 0, (int)(255 * dim * Anim)));
        var ww = ui.S(w);
        var hh = ui.S(h);
        var r = new Rectangle((ui.Width - ww) / 2, (ui.Height - hh) / 2 + (1 - ease) * ui.S(40), ww, hh);
        ui.SoftShadow(r, 18, 22, 140);
        ui.Panel(r, Gfx.Hex(0x2B211A), 18);
        ui.PanelOutline(new Rectangle(r.X + ui.S(4), r.Y + ui.S(4), r.Width - ui.S(8), r.Height - ui.S(8)), new Color(255, 220, 170, 22), 1.5f, 15);
        // Baslik bandi (degrade) ve tente sacagi
        var head = new Rectangle(r.X, r.Y, r.Width, ui.S(70));
        ui.Panel(head, Theme.PrimaryDark, 18);
        Raylib.DrawRectangleGradientV((int)r.X, (int)(r.Y + ui.S(12)), (int)r.Width, (int)ui.S(58), Theme.Primary, Theme.PrimaryDark);
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, r.Width, ui.S(36)), UiContext.Roundness(new Rectangle(0, 0, r.Width, ui.S(36)), ui.S(18)), 8, Theme.Primary);
        var scallops = Math.Max(6, (int)(r.Width / ui.S(34)));
        var sw = r.Width / scallops;
        for (var i = 0; i < scallops; i++)
        {
            var c = new Vector2(r.X + sw * (i + 0.5f), r.Y + ui.S(70));
            Raylib.DrawCircleSector(c, sw / 2, 0, 180, 12, i % 2 == 0 ? Theme.PrimaryDark : Theme.CreamDark);
        }

        var tx = r.X + ui.S(28);
        if (icon != Icon.None)
        {
            Icons.Draw(icon, new Vector2(tx + ui.S(18), r.Y + ui.S(35)), ui.S(36), Theme.Cream, Theme.PrimaryDark);
            tx += ui.S(48);
        }

        ui.TextOutlined(title, new Vector2(tx, r.Y + ui.S(14)), 36, Theme.White, new Color(90, 40, 10, 160), true);
        if (Closable && ui.Button(new Rectangle(r.X + r.Width - ui.S(62), r.Y + ui.S(13), ui.S(46), ui.S(44)), "", ButtonStyle.Ghost, true, 24, Icon.Cross))
        {
            Close();
        }

        return new Rectangle(r.X + ui.S(28), r.Y + ui.S(94), r.Width - ui.S(56), r.Height - ui.S(116));
    }
}

/// <summary>Evet/Hayir onay penceresi (geri alinamaz islemler icin).</summary>
public sealed class ConfirmScreen : OverlayScreen
{
    private readonly string _title;
    private readonly string _message;
    private readonly string _yes;
    private readonly string _no;
    private readonly Action _onYes;
    private readonly bool _danger;

    public ConfirmScreen(string title, string message, string yes, string no, Action onYes, bool danger = true)
    {
        _title = title;
        _message = message;
        _yes = yes;
        _no = no;
        _onYes = onYes;
        _danger = danger;
    }

    protected override Icon TitleIcon => _danger ? Icon.Warning : Icon.Info;

    public override void DrawUi()
    {
        var ui = Game.Ui;
        var area = Window(600, 340, _title, 0.6f);
        ui.Paragraph(_message, new Vector2(area.X, area.Y), 26, area.Width, Theme.Cream);
        var bw = (area.Width - ui.S(16)) / 2;
        var y = area.Y + area.Height - ui.S(64);
        if (ui.Button(new Rectangle(area.X, y, bw, ui.S(60)), _no, ButtonStyle.Normal))
        {
            Close();
        }

        if (ui.Button(new Rectangle(area.X + bw + ui.S(16), y, bw, ui.S(60)), _yes, _danger ? ButtonStyle.Danger : ButtonStyle.Primary))
        {
            Close();
            _onYes();
        }
    }
}
