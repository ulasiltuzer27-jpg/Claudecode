using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Client;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;
using PilavciSimulator.Localization;
using PilavciSimulator.Sim.Customers;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Interaction;

namespace PilavciSimulator.Screens;

/// <summary>
/// Kasa: musterinin uzattigi para ve odenecek tutar. Oyuncu banknot ve
/// bozuk paralara tiklayarak para ustunu toplar. Klavye: 1-7 banknotlar,
/// Backspace geri al, Enter ver.
/// </summary>
public sealed class CashScreen : OverlayScreen
{
    private readonly int _customerId;
    private readonly List<int> _given = new();
    private float _age;

    public CashScreen(int customerId) => _customerId = customerId;

    private CustomerEntity? Customer => Game.Session?.World.Get<CustomerEntity>(_customerId);

    public override void Update(float dt, bool focused)
    {
        base.Update(dt, focused);
        _age += dt;
        if (Customer is not { State: CustomerState.Paying })
        {
            // Co-op istemcide "kasayi ac" olayi musterinin durum guncellemesinden
            // once gelebilir: kisa bir sure bekle, sonra kapat.
            if (_age > 1.5f)
            {
                Close();
            }

            return;
        }

        if (!focused)
        {
            return;
        }

        var input = Game.Input;
        for (var i = 0; i < CustomerLogic.Bills.Length; i++)
        {
            if (input.KeyPressed(KeyboardKey.One + i))
            {
                Add(CustomerLogic.Bills[i]);
            }
        }

        if (input.KeyPressed(KeyboardKey.Backspace) && _given.Count > 0)
        {
            _given.RemoveAt(_given.Count - 1);
        }

        if (input.KeyPressed(KeyboardKey.Enter) || input.KeyPressed(KeyboardKey.KpEnter))
        {
            Give();
        }
    }

    private void Add(int bill)
    {
        _given.Add(bill);
        Game.Audio.Play(bill >= 5 ? "ui_click" : "coin", 0.6f);
    }

    private void Give()
    {
        if (Customer is not { } c)
        {
            return;
        }

        Game.Session!.SendAction(new ActionRequest { Action = ActionId.GiveChange, Kind = TargetKind.Customer, EntityId = c.Id, Param = _given.Sum(), Text = "" });
        Close();
    }

    public override void DrawUi()
    {
        if (Customer is not { } c)
        {
            return;
        }

        var ui = Game.Ui;
        var area = Window(820, 620, Loc.T("cash.title"));
        var change = c.PaidAmount - c.DueAmount;
        var given = _given.Sum();
        // Ust bilgi
        ui.Text(Loc.T("cash.due"), new Vector2(area.X, area.Y), 24, Theme.CreamDark);
        ui.Text(Fmt.Money(c.DueAmount), new Vector2(area.X, area.Y + ui.S(30)), 44, Theme.Cream, true);
        ui.Text(Loc.T("cash.paid"), new Vector2(area.X + ui.S(260), area.Y), 24, Theme.CreamDark);
        ui.Text(Fmt.Money(c.PaidAmount), new Vector2(area.X + ui.S(260), area.Y + ui.S(30)), 44, Theme.Yellow, true);
        ui.Text(Loc.T("cash.change"), new Vector2(area.X + ui.S(520), area.Y), 24, Theme.CreamDark);
        var color = given == change ? Theme.Green : given > change ? Theme.Red : Theme.Cream;
        ui.Text(Fmt.Money(given), new Vector2(area.X + ui.S(520), area.Y + ui.S(30)), 44, color, true);

        // Banknotlar ve bozuk paralar
        var y = area.Y + ui.S(110);
        var x = area.X;
        for (var i = 0; i < CustomerLogic.Bills.Length; i++)
        {
            var b = CustomerLogic.Bills[i];
            var coin = b < 5;
            var w = coin ? ui.S(90) : ui.S(150);
            var h = coin ? ui.S(90) : ui.S(80);
            var r = new Rectangle(x, y, w, h);
            if (ui.Button(r, $"{b} ₺", coin ? ButtonStyle.Normal : ButtonStyle.Primary, true, 30))
            {
                Add(b);
            }

            ui.Text($"{i + 1}", new Vector2(r.X + ui.S(8), r.Y + ui.S(4)), 16, Theme.InkSoft, true);
            x += w + ui.S(14);
            if (x + ui.S(160) > area.X + area.Width)
            {
                x = area.X;
                y += h + ui.S(14);
            }
        }

        y += ui.S(110);
        // Verilenler
        ui.Text(Loc.T("cash.given_list"), new Vector2(area.X, y), 22, Theme.CreamDark);
        var gx = area.X + ui.S(150);
        foreach (var g in _given.TakeLast(14))
        {
            var r = new Rectangle(gx, y - ui.S(4), ui.S(60), ui.S(34));
            ui.Panel(r, g >= 5 ? Theme.Green.WithAlpha(0.8f) : Theme.Yellow.WithAlpha(0.85f), 6);
            ui.TextIn(r, $"{g}", 20, Theme.Ink, true, Align.Center, 0);
            gx += ui.S(66);
        }

        var by = area.Y + area.Height - ui.S(70);
        if (ui.Button(new Rectangle(area.X, by, ui.S(200), ui.S(64)), Loc.T("cash.undo"), ButtonStyle.Normal, _given.Count > 0))
        {
            _given.RemoveAt(_given.Count - 1);
        }

        if (ui.Button(new Rectangle(area.X + ui.S(214), by, ui.S(200), ui.S(64)), Loc.T("cash.clear"), ButtonStyle.Normal, _given.Count > 0))
        {
            _given.Clear();
        }

        if (ui.Button(new Rectangle(area.X + area.Width - ui.S(320), by, ui.S(320), ui.S(64)), Loc.T("cash.give", Fmt.Money(given)), ButtonStyle.Primary))
        {
            Give();
        }

        if (Game.Settings.ShowHints)
        {
            ui.Text(Loc.T("cash.hint", Fmt.Money(change)), new Vector2(area.X, by - ui.S(40)), 20, Theme.CreamDark);
        }
    }

    public override string Annotate() =>
        Customer is { State: CustomerState.Paying } c ? $"kasa: hazir tutar={c.DueAmount} verilen={c.PaidAmount} ustu={c.PaidAmount - c.DueAmount} secilen={_given.Sum()}" : "kasa: bekliyor";

    /// <summary>Senaryo: "cash-auto" dogru para ustunu buyukten kucuge toplayip verir.</summary>
    public override bool Command(string[] args)
    {
        if (args[0] != "cash-auto" || Customer is not { State: CustomerState.Paying } c)
        {
            return false;
        }

        var left = c.PaidAmount - c.DueAmount;
        foreach (var bill in CustomerLogic.Bills)
        {
            while (left >= bill)
            {
                Add(bill);
                left -= bill;
            }
        }

        Give();
        return true;
    }
}
