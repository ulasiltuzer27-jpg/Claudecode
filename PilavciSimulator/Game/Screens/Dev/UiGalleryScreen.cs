using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Engine.Ui;

namespace PilavciSimulator.Screens.Dev;

/// <summary>Gelistirici vitrini: tum ikonlar ve arayuz bilesenleri (capture ile gorsel kontrol).</summary>
public sealed class UiGalleryScreen : OverlayScreen
{
    private int _tab;
    private int _stepper = 3;
    private bool _toggle = true;
    private float _slider = 0.6f;

    protected override Icon TitleIcon => Icon.Paint;

    public override void DrawUi()
    {
        var ui = Game.Ui;
        var area = Window(1700, 960, "Arayüz vitrini", 0.35f);
        // Sol: ikon izgarasi
        var icons = Icons.All.ToList();
        const int cols = 8;
        var cell = ui.S(92);
        for (var i = 0; i < icons.Count; i++)
        {
            var x = area.X + (i % cols) * cell;
            var iy = area.Y + (i / cols) * cell;
            var r = new Rectangle(x, iy, cell - ui.S(8), cell - ui.S(8));
            ui.Panel(r, new Color(255, 255, 255, 18), 10);
            Icons.Draw(icons[i], new Vector2(x + (cell - ui.S(8)) / 2, iy + ui.S(34)), ui.S(44), i % 3 == 0 ? Theme.Primary : i % 3 == 1 ? Theme.Cream : Theme.Yellow, i % 2 == 0 ? Theme.PrimaryDark : null);
            ui.Text(icons[i].ToString(), new Vector2(x + (cell - ui.S(8)) / 2, iy + ui.S(64)), 16, Theme.CreamDark, false, Align.Center);
        }

        // Sag: bilesenler
        var rx = area.X + cols * cell + ui.S(24);
        var rw = area.X + area.Width - rx;
        var col = new UiStack(new Rectangle(rx, area.Y, rw, area.Height), ui.S(14));
        ui.Tabs(col.Next(ui.S(58)), ["Genel", "Görev", "Harita"], ref _tab, [Icon.Home, Icon.Task, Icon.Map], "gallery:tabs");
        var row = new UiStack(col.Next(ui.S(60)), ui.S(12), horizontal: true);
        var btns = row.Split(3);
        ui.Button(btns[0], "Normal", ButtonStyle.Normal, true, 26, Icon.Gear);
        ui.Button(btns[1], "Kaydet", ButtonStyle.Primary, true, 26, Icon.Save);
        ui.Button(btns[2], "Çıkış", ButtonStyle.Danger, false, 26, Icon.Exit, "Devre dışı: neden burada yazar");
        ui.Stepper(col.Next(ui.S(56)), "Porsiyon", ref _stepper, 0, 10);
        ui.Toggle(col.Next(ui.S(56)), "Bildirimler", ref _toggle);
        ui.Slider(col.Next(ui.S(56)), "Müzik", ref _slider, 0, 1, $"{_slider * 100:0}%");
        var cards = new UiStack(col.Next(ui.S(150)), ui.S(16), horizontal: true).Split(2);
        ui.Card(cards[0], Theme.Green);
        ui.IconText(Icon.Money, "1.250 ₺", new Vector2(cards[0].X + ui.S(24), cards[0].Y + ui.S(18)), 34, Theme.Ink, Theme.Green, true);
        ui.Badge(new Vector2(cards[0].X + ui.S(24), cards[0].Y + ui.S(80)), "+%12 bugün", Theme.Green, Theme.White);
        ui.Paper(cards[1], 3);
        ui.Text("FİŞ  #0042", new Vector2(cards[1].X + ui.S(16), cards[1].Y + ui.S(12)), 24, Theme.Ink, true);
        ui.Text("Tavuklu pilav x2", new Vector2(cards[1].X + ui.S(16), cards[1].Y + ui.S(52)), 22, Theme.InkSoft);
        var signs = new UiStack(col.Next(ui.S(120)), ui.S(16), horizontal: true).Split(2);
        ui.SignBoard(signs[0], "PİLAVCI", Icon.Kazan);
        ui.Chalkboard(signs[1]);
        ui.Text("Nohutlu  40 ₺", new Vector2(signs[1].X + ui.S(30), signs[1].Y + ui.S(28)), 30, new Color(240, 240, 230, 230), true);
        ui.Ring(new Vector2(rx + ui.S(50), col.Area.Y + col.Cursor + ui.S(50)), 44, 10, 0.68f, Theme.Yellow, new Color(255, 255, 255, 40));
        ui.TextOutlined("Gün 3", new Vector2(rx + ui.S(50), col.Area.Y + col.Cursor + ui.S(34)), 26, Theme.White, Color.Black, true, Align.Center);
        col.Next(ui.S(104));
        // Kaydirma alani
        var view = col.Next(col.Remaining - ui.S(4));
        ui.Panel(view, new Color(0, 0, 0, 60), 12);
        var y = ui.BeginScroll("gallery:scroll", view);
        for (var i = 0; i < 12; i++)
        {
            ui.Button(new Rectangle(view.X + ui.S(10), y + ui.S(10), view.Width - ui.S(30), ui.S(50)), $"Satır {i + 1}", ButtonStyle.Normal, true, 22, i % 2 == 0 ? Icon.Box : Icon.Cup);
            y += ui.S(58);
        }

        ui.EndScroll(y + ui.S(10));
    }
}
