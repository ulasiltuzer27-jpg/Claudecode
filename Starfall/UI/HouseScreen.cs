using System.Numerics;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.Models;
using Starfall.Render;
using static Starfall.Core.Loc;

namespace Starfall.UI;

/// <summary>
/// Ev duzenleme: ustten gorunum, imlec hucresi, secili mobilyanin hayaleti. Yerlestir (Space/Enter),
/// dondur (R), kaldir (X), mobilya sec (Q/E), cik (Esc).
/// </summary>
public sealed class HouseScreen : UiScreen
{
    public int Cx = 4, Cz = 3, Rot;
    public string? Selected;
    private Node? _ghost, _cursor;
    private readonly Material _ghostMat = new() { Blend = Blend.Alpha, Opacity = 0.6f, CastShadow = false };
    private readonly Material _cursorMat = new() { Blend = Blend.Alpha, Opacity = 0.45f, Unlit = true, CastShadow = false, DepthWrite = false };
    private MeshData? _cursorMesh;

    public HouseScreen(UiManager ui) : base(ui) => Dim = false;

    private House H => G.House;

    public List<string> Available() => Furniture.All.Select(f => f.Id).Where(id => H.OwnedCount(id) > H.PlacedCount(id)).ToList();

    protected override void Rebuild()
    {
        var av = Available();
        if (Selected == null || !av.Contains(Selected)) Selected = av.FirstOrDefault();
    }

    public override void Mount()
    {
        base.Mount();
        var o = House.O;
        G.CameraRig.Override = new CamOverride(o + new Vector3(0, 7.8f, 5.2f), o + new Vector3(0, 0, 0.4f), 6);
        G.Player.Model.Root.Visible = false;
        _cursorMesh = MeshData.From(Geo.BoxGeo(1, 0.04f, 1).Color("#ffffff"), true);
        _cursor = new Node(_cursorMesh, _cursorMat);
        G.Env.Scene.Add(_cursor);
    }

    public override void Unmount()
    {
        G.CameraRig.Override = null;
        G.CameraRig.Snap(G.Player.Pos, G.Player.Yaw + MathX.Pi);
        G.Player.Model.Root.Visible = true;
        if (_ghost != null) G.Env.Scene.Root.Remove(_ghost);
        if (_cursor != null) G.Env.Scene.Root.Remove(_cursor);
        G.Autosave();
    }

    public void Cycle(int d)
    {
        var av = Available();
        if (av.Count == 0) { Selected = null; return; }
        int i = Selected == null ? -1 : av.IndexOf(Selected);
        Selected = av[((i + d) % av.Count + av.Count) % av.Count];
        G.Audio.Sfx("uiMove");
    }

    public void PlaceOrPick()
    {
        if (Selected != null)
        {
            var f = Furniture.Get(Selected)!;
            if (H.Place(Selected, Cx, f.Wall ? 0 : Cz, Rot))
            {
                if (!Available().Contains(Selected)) Selected = Available().FirstOrDefault();
            }
            else G.Audio.Sfx("error");
            return;
        }
        var p = H.At(Cx, Cz);
        if (p != null)
        {
            Selected = p.Id;
            Rot = p.Rot;
            H.Remove(p);
        }
    }

    public override void Handle(Input input)
    {
        if (input.Pressed("left")) Cx = Math.Max(0, Cx - 1);
        if (input.Pressed("right")) Cx = Math.Min(House.CX - 1, Cx + 1);
        if (input.Pressed("up")) Cz = Math.Max(0, Cz - 1);
        if (input.Pressed("down")) Cz = Math.Min(House.CZ - 1, Cz + 1);
        if (input.Pressed("tabPrev")) Cycle(-1);
        else if (input.Pressed("tabNext")) Cycle(1);
        if (input.Pressed("rotate")) { Rot = (Rot + 1) % 4; G.Audio.Sfx("rotate"); }
        if (input.Pressed("remove"))
        {
            var p = H.At(Cx, Cz);
            if (p != null) H.Remove(p);
        }
        if (input.Pressed("jump") || input.Pressed("shot")) PlaceOrPick();
        if (input.Pressed("back")) OnBack();
    }

    public override void Draw(UiCtx c)
    {
        var D = c.D;
        // 3B hayalet + imlec
        var f = Selected != null ? Furniture.Get(Selected) : null;
        int cz = f?.Wall == true ? 0 : Cz;
        if (_cursor != null)
        {
            var (w, d) = f != null ? House.Footprint(f, f.Wall ? 0 : Rot) : (1, 1);
            var center = House.CellPos(Cx + (w - 1) / 2f, cz + (d - 1) / 2f);
            _cursor.LocalOverride = Matrix4x4.CreateScale(w * House.Cell * 0.96f, 1, d * House.Cell * 0.96f) * Matrix4x4.CreateTranslation(center + new Vector3(0, 0.04f, 0));
            bool ok = f == null ? H.At(Cx, Cz) != null : H.CanPlace(f.Id, Cx, cz, Rot);
            _cursorMat.Tint = ok ? MathX.Hex("#7fe08a") : MathX.Hex("#ff7a7a");
        }
        if (f != null)
        {
            if (_ghost == null || _ghost.Mesh != Furniture.Mesh(f.Id))
            {
                if (_ghost != null) G.Env.Scene.Root.Remove(_ghost);
                _ghost = new Node(Furniture.Mesh(f.Id), _ghostMat);
                G.Env.Scene.Add(_ghost);
            }
            _ghost.Visible = true;
            _ghost.LocalOverride = House.ItemMatrix(f, Cx, cz, Rot);
        }
        else if (_ghost != null) _ghost.Visible = false;

        // sol panel: mobilya listesi
        float a = Appear;
        float x = 24, y = 24, w2 = 280;
        var av = Available();
        float h = 74 + Math.Max(1, av.Count) * 30 + 16;
        c.Panel(x, y, w2, h, 22);
        D.Text(T("house.title"), x + 20, y + 14, c.Px(24), C.Ink, FontKind.Title);
        float yy = y + 62;
        if (av.Count == 0) D.Text(T("house.empty"), x + 20, yy, c.Px(14.5f), C.InkSoft, FontKind.Strong);
        foreach (var id in av)
        {
            bool sel = id == Selected;
            if (sel) D.Rect(x + 10, yy - 4, w2 - 20, 28, C.Gold2, 10);
            D.Text(T($"furn.{id}"), x + 20, yy, c.Px(15), C.Ink, FontKind.Strong);
            D.Text($"×{H.OwnedCount(id) - H.PlacedCount(id)}", x + w2 - 20, yy, c.Px(14), C.InkSoft, FontKind.Strong, Align.Right);
            yy += 30;
        }
        // alt bilgi
        var pairs = new[] { ("left", T("house.move")), ("tabNext", T("house.select")), ("jump", T("house.place")), ("rotate", T("house.rotate")), ("remove", T("house.remove")), ("back", T("ui.back")) };
        float total = 0;
        float fs = c.Px(14.5f);
        foreach (var (ac, l) in pairs) total += c.MeasureKey(G.Input.Glyph(ac)) + 6 + D.Measure(l, fs, FontKind.Strong) + 16;
        float bx = c.W / 2 - (total + 16) / 2, by = 720 - 64;
        D.Rect(bx, by, total + 16, 48, C.A(C.Cream, 0.95f * a), 16);
        float xx = bx + 16;
        foreach (var (ac, l) in pairs)
        {
            xx += c.KeyCap(xx, by + 8, G.Input.Glyph(ac)) + 6;
            D.Text(l, xx, UiCtx.Mid(by, 48, fs), fs, C.Ink, FontKind.Strong);
            xx += D.Measure(l, fs, FontKind.Strong) + 16;
        }
    }
}
