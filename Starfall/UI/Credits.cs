using System.Numerics;
using Starfall.Core;
using static Starfall.Core.Loc;

namespace Starfall.UI;

/// <summary>Jenerik: final sonrasi (veya menuden) kayan yazilar. Atlanabilir.</summary>
public sealed class Credits
{
    private readonly Game _g;
    public bool Active;
    private Action? _onDone;
    private float _t, _height;

    public Credits(Game g) => _g = g;

    public void Show(Action? onDone)
    {
        Active = true;
        _onDone = onDone;
        _t = 0;
        _g.Env.Post.Fade = 0;
    }

    public void Handle(Input input)
    {
        if (_t > 1.5f && (input.Pressed("confirm") || input.Pressed("back") || input.Pressed("jump"))) Finish();
    }

    public void Update(float dt)
    {
        if (!Active) return;
        _t += dt;
        // pencere yoksa (testler) yazinin uzunlugunu kabaca tahmin et
        float h = _height > 0 ? _height : 1400;
        if (720 - _t * 52 < -h) Finish();
    }

    private void Finish()
    {
        if (!Active) return;
        Active = false;
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke();
    }

    public void Draw(UiCtx c)
    {
        if (!Active) return;
        var D = c.D;
        // arka plan: ustte lacivert parilti
        D.Rect(0, 0, c.W, c.H, C.Hex("#0b1020"));
        for (int i = 0; i < 12; i++)
        {
            float r = 900 - i * 60;
            D.Rect(c.W / 2 - r, 720 * 0.3f - r * 0.6f, r * 2, r * 1.2f, C.Hex("#23306a", 0.09f), r);
        }
        float y = 720 - _t * 52;
        float y0 = y;
        D.Text(T("game.title"), c.W / 2, y, 57.6f, C.Gold2, FontKind.Title, Align.Center);
        y += 70;
        D.Text(T("credits.thanks"), c.W / 2, y, c.Px(20), C.White, FontKind.Strong, Align.Center);
        y += 40;
        foreach (var s in Lines("credits.sections"))
        {
            var parts = s.Split('|');
            y += 46;
            D.Text(parts[0].ToUpperInvariant(), c.W / 2, y, c.Px(16), C.Teal2, FontKind.Title, Align.Center);
            y += 30;
            foreach (var row in parts.Skip(1))
            {
                D.Text(row, c.W / 2, y, c.Px(20), C.White, FontKind.Strong, Align.Center);
                y += 32;
            }
        }
        y += 60;
        D.Text("★", c.W / 2, y, 28, C.Gold2, FontKind.Strong, Align.Center);
        y += 40;
        D.Text(T("credits.end"), c.W / 2, y, c.Px(20), C.White, FontKind.Strong, Align.Center);
        y += 40;
        _height = y - y0;
        D.Text(T("credits.skip"), c.W - 24, 720 - 40, c.Px(15), C.A(C.White, 0.7f), FontKind.Strong, Align.Right);
    }
}
