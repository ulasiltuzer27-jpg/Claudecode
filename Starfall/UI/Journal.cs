using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Render;
using Starfall.World;
using static Starfall.Core.Loc;

namespace Starfall.UI;

/// <summary>Adanin kuşbakışı haritasi (arazi renkleri + golgeleme), CPU'da bir kez uretilir.</summary>
public static class MapImage
{
    private static readonly Dictionary<string, CpuImage> Cache = new();

    public static CpuImage For(GameWorld w, Terrain t, int size = 512)
    {
        if (Cache.TryGetValue(t.Id, out var img)) return img;
        img = new CpuImage(size, size);
        var colors = w.TerrainColors[t.Id];
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float x = t.MinX + (px + 0.5f) / size * t.Size;
            float z = t.MinZ + (py + 0.5f) / size * t.Size;
            float h = t.Height(x, z);
            float wl = w.WaterLevel(x, z);
            float r, g, b;
            if (w.IsFrozenWater(x, z)) { r = 205; g = 235; b = 248; }
            else if (h < wl)
            {
                float d = MathF.Min(1, (wl - h) / 7);
                r = MathX.Lerp(120, 40, d); g = MathX.Lerp(214, 110, d); b = MathX.Lerp(214, 170, d);
            }
            else
            {
                int ix = Math.Min(t.Verts - 1, (int)MathF.Round((x - t.MinX) / t.Cell));
                int iz = Math.Min(t.Verts - 1, (int)MathF.Round((z - t.MinZ) / t.Cell));
                var c = colors[iz * t.Verts + ix];
                r = MathX.LinearToSrgb(c.X) * 255; g = MathX.LinearToSrgb(c.Y) * 255; b = MathX.LinearToSrgb(c.Z) * 255;
                var n = t.Normal(x, z);
                float shade = 0.75f + 0.35f * MathF.Max(0, -n.X * 0.6f + n.Y * 0.6f - n.Z * 0.5f);
                r *= shade; g *= shade; b *= shade;
            }
            int o = (py * size + px) * 4;
            img.Rgba[o] = (byte)Math.Clamp(r, 0, 255);
            img.Rgba[o + 1] = (byte)Math.Clamp(g, 0, 255);
            img.Rgba[o + 2] = (byte)Math.Clamp(b, 0, 255);
            img.Rgba[o + 3] = 255;
        }
        return Cache[t.Id] = img;
    }
}

/// <summary>Gunluk: harita / gorevler / koleksiyon sekmeleri.</summary>
public sealed class JournalScreen : PanelScreen
{
    private int _tab;
    private string _island = "isle1";
    private readonly string[] _tabs = { "journal.map", "journal.quests", "journal.collection" };

    public JournalScreen(UiManager ui) : base(ui)
    {
        _island = ui.G.World.IslandAt(ui.G.Player.Pos.X, ui.G.Player.Pos.Z);
    }

    protected override string Title => T("pause.journal");
    protected override (string, string)[] FooterPairs =>
        _tab == 0 && G.World.Terrains.Count > 1 && G.Save?.Flag("isle2Visited") == true
            ? new[] { ("tabPrev", "/"), ("tabNext", T("journal.switch")), ("confirm", T("journal.otherIsland")), ("back", T("ui.back")) }
            : new[] { ("tabPrev", "/"), ("tabNext", T("journal.switch")), ("back", T("ui.back")) };

    protected override void Rebuild() { }

    protected override void OnInput(Input input)
    {
        if (input.Pressed("tabPrev") || input.Pressed("left")) { _tab = (_tab + 2) % 3; Scroll = 0; G.Audio.Sfx("uiMove"); }
        if (input.Pressed("tabNext") || input.Pressed("right")) { _tab = (_tab + 1) % 3; Scroll = 0; G.Audio.Sfx("uiMove"); }
        if (_tab == 0 && input.Pressed("confirm") && G.World.Terrains.Count > 1 && G.Save?.Flag("isle2Visited") == true)
        {
            _island = _island == "isle1" ? "isle2" : "isle1";
            G.Audio.Sfx("uiMove");
        }
        if (input.Pressed("journal")) OnBack();
        if (input.Pressed("up")) Scroll = MathF.Max(0, Scroll - 50);
        if (input.Pressed("down")) Scroll = MathF.Min(ScrollMax, Scroll + 50);
    }

    protected override void HeadExtra(UiCtx c, float right, float y)
    {
        var D = c.D;
        float fs = c.Px(15.5f);
        float x = right;
        for (int i = _tabs.Length - 1; i >= 0; i--)
        {
            string s = T(_tabs[i]);
            float w = D.Measure(s, fs, FontKind.Strong) + 32;
            x -= w;
            bool on = i == _tab;
            c.Pill(x, y, w, 34, on ? C.Teal : C.White);
            D.Text(s, x + w / 2, UiCtx.Mid(y, 34, fs), fs, on ? C.White : C.InkSoft, FontKind.Strong, Align.Center);
            if (c.Click && c.Hover(x, y, w, 34)) { _tab = i; Scroll = 0; }
            x -= 8;
        }
    }

    private List<(string Title, string Desc, string State)> QuestList()
    {
        var g = G;
        var s = g.Save!;
        var Q = g.Quests;
        var list = new List<(string, string, string)>();
        int shards = g.Progress.ShardCount();
        list.Add((T("quest.main.title"),
            s.Flag("finale") ? T("quest.main.done") : !s.Flag("owlIntro") ? T("quest.main.start") : T("quest.main.desc", ("n", shards), ("goal", WD.ShardsForFinale)),
            s.Flag("finale") ? "done" : "active"));
        void Q1(string id, params (string, object)[] p)
        {
            var st = s.Quest(id);
            if (st == "none") { list.Add((T($"quest.{id}.title"), T("quest.unknown"), "none")); return; }
            list.Add((T($"quest.{id}.title"), T(st == "done" ? $"quest.{id}.done" : $"quest.{id}.desc", p), st));
        }
        Q1("rabbit", ("n", Q.Carrots()));
        Q1("beaver", ("n", Q.Tools()));
        Q1("frog");
        Q1("bear", ("n", Q.FishTotal()), ("species", Q.Isle1Species()), ("total", WD.Fish.Length));
        g.World.ExtraQuests(g, list);
        return list;
    }

    private List<(string Num, string Label)> Collection()
    {
        var g = G;
        var s = g.Save!;
        var p = g.Progress;
        var list = new List<(string, string)>
        {
            ($"{p.ShardCount()} / {WD.ShardTotal}", T("coll.shards")),
            ($"{p.FeatherCount()} / {g.World.FeatherTotal}", T("coll.feathers")),
            ($"{s.ShellsTotal} / 100", T("coll.shells")),
            ($"{g.Quests.Species()} / {g.World.AllFish.Count}", T("coll.fish")),
            ($"{s.Regions.Count(r => g.World.MainRegions.Contains(r))} / {g.World.MainRegions.Count}", T("coll.regions")),
        };
        g.World.ExtraCollection(g, list);
        list.Add(($"{p.Completion()}%", T("coll.completion")));
        return list;
    }

    protected override float BodyHeight(UiCtx c)
    {
        if (G.Save == null) return 100;
        if (_tab == 0) return MathF.Min(470, BodyW > 0 ? BodyW : 520);
        if (_tab == 1) return QuestList().Count * 72;
        int cols = Math.Max(1, (int)((BodyW > 0 ? BodyW : 820) + 10) / 200);
        int rows = (int)MathF.Ceiling(Collection().Count / (float)cols);
        int frows = (int)MathF.Ceiling(G.World.AllFish.Count / (float)cols);
        return rows * 84 + 48 + frows * 70;
    }

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        if (G.Save == null) return;
        if (_tab == 0) DrawMap(c, x, y, w);
        else if (_tab == 1) DrawQuests(c, x, y, w);
        else DrawCollection(c, x, y, w);
    }

    private void DrawMap(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        var g = G;
        var t = g.World.Terrains.FirstOrDefault(tt => tt.Id == _island) ?? g.World.Isle1;
        float size = MathF.Min(470, w);
        float mx = x + w / 2 - size / 2, my = y;
        D.Rect(mx - 4, my - 4, size + 8, size + 8, C.White, 24);
        var tex = UiTex.Of(MapImage.For(g.World, t));
        if (tex != null) D.Image(tex, mx, my, size, size);
        Vector2 ToMap(float wx, float wz) => new(mx + (wx - t.MinX) / t.Size * size, my + (wz - t.MinZ) / t.Size * size);
        var s = g.Save!;
        foreach (var r in g.World.Regions)
        {
            if (g.World.IslandAt(r.X, r.Z) != t.Id) continue;
            bool known = s.Regions.Contains(r.Id);
            if (r.Secret && !known) continue;
            var p = ToMap(r.X, r.Z);
            string label = known ? T($"region.{r.Id}") : "???";
            D.Text(label, p.X, p.Y - 8, c.Px(13.6f), C.A(C.White, known ? 1 : 0.55f), FontKind.Title, Align.Center, 1.5f);
        }
        g.World.DrawMapPins(g, c, t, ToMap);
        if (g.World.IslandAt(g.Player.Pos.X, g.Player.Pos.Z) == t.Id)
        {
            var pp = ToMap(g.Player.Pos.X, g.Player.Pos.Z);
            float pulse = 4 + MathF.Sin(c.Time * 4) * 1.5f;
            D.Rect(pp.X - 8 - pulse, pp.Y - 8 - pulse, 16 + pulse * 2, 16 + pulse * 2, C.A(C.Rose, 0.35f), 8 + pulse);
            D.Rect(pp.X - 8, pp.Y - 8, 16, 16, C.White, 8);
            D.Rect(pp.X - 5, pp.Y - 5, 10, 10, C.Rose, 5);
        }
        if (g.World.Terrains.Count > 1 && s.Flag("isle2Visited"))
            D.Text(T($"island.{t.Id}"), mx + 14, my + 10, c.Px(16), C.White, FontKind.Title, Align.Left, 1.5f);
    }

    private void DrawQuests(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        foreach (var (title, desc, state) in QuestList())
        {
            const float h = 64;
            D.Rect(x, y, w, h, C.White, 14);
            var col = state == "done" ? C.Teal : state == "active" ? C.Gold : C.Hex("#d8ccba");
            D.Rect(x + 14, y + 12, 26, 26, col, 13);
            string sym = state == "done" ? "✓" : state == "active" ? "!" : "?";
            D.Text(sym, x + 27, y + 14, 17, C.White, FontKind.Strong, Align.Center);
            D.Text(title, x + 52, y + 9, c.Px(16), C.Ink, FontKind.Strong);
            D.Text(Hud.Ellipsize(D, desc, c.Px(14.7f), w - 66), x + 52, y + 34, c.Px(14.7f), C.InkSoft, FontKind.Strong);
            y += h + 8;
        }
    }

    private void DrawCollection(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        var g = G;
        int cols = Math.Max(1, (int)(w + 10) / 200);
        float cw = (w - (cols - 1) * 10) / cols;
        var list = Collection();
        for (int i = 0; i < list.Count; i++)
        {
            float cx = x + (i % cols) * (cw + 10), cy = y + (i / cols) * 84;
            D.Rect(cx, cy, cw, 74, C.White, 14);
            D.Text(list[i].Num, cx + 14, cy + 8, c.Px(25.6f), C.Ink, FontKind.Title);
            D.Text(list[i].Label, cx + 14, cy + 46, c.Px(14.4f), C.InkSoft, FontKind.Strong);
        }
        y += (int)MathF.Ceiling(list.Count / (float)cols) * 84 + 10;
        D.Text(T("coll.fishList").ToUpperInvariant(), x + 4, y + 8, c.Px(17.6f), C.Teal, FontKind.Title);
        y += 38;
        var s = g.Save!;
        for (int i = 0; i < g.World.AllFish.Count; i++)
        {
            var f = g.World.AllFish[i];
            int n = s.Fish.TryGetValue(f.Id, out var v) ? v : 0;
            float cx = x + (i % cols) * (cw + 10), cy = y + (i / cols) * 70;
            D.Rect(cx, cy, cw, 60, C.White, 14);
            D.Text(n > 0 ? T($"fish.{f.Id}") : "???", cx + 14, cy + 8, c.Px(14.4f), n > 0 ? C.Ink : C.InkSoft, FontKind.Strong);
            string sub = n > 0 ? $"× {n}" + (s.FishBest.TryGetValue(f.Id, out var b) ? $" · {b:0} cm" : "") : T($"fish.hint.{f.Id}");
            D.Text(Hud.Ellipsize(D, sub, c.Px(13), cw - 24), cx + 14, cy + 32, c.Px(13), C.InkSoft, FontKind.Strong);
        }
    }
}

/// <summary>Gardirop: sahip olunan kiyafetleri yuva yuva dene/giy.</summary>
public sealed class WardrobeScreen : PanelScreen
{
    private static readonly OutfitSlot[] Slots = { OutfitSlot.Hat, OutfitSlot.Face, OutfitSlot.Back, OutfitSlot.Scarf };

    public WardrobeScreen(UiManager ui) : base(ui) { }

    protected override string Title => T("wardrobe.title");
    protected override (string, string)[] FooterPairs => new[] { ("left", "◀ ▶"), ("confirm", T("wardrobe.wear")), ("back", T("ui.back")) };

    protected override void Rebuild()
    {
        foreach (var slot in Slots)
        {
            var sl = slot;
            Add(T($"wardrobe.slot.{slot}"), () => Cycle(sl, 1), () => Cycle(sl, -1), () => Cycle(sl, 1), tag: slot);
        }
    }

    private List<string?> Options(OutfitSlot slot)
    {
        var owned = G.Save!.Outfits.Where(id => Outfits.Get(id)?.Slot == slot).Cast<string?>().ToList();
        if (slot != OutfitSlot.Scarf) owned.Insert(0, null);
        return owned;
    }

    private void Cycle(OutfitSlot slot, int d)
    {
        var opts = Options(slot);
        if (opts.Count == 0) return;
        int cur = opts.IndexOf(G.Wardrobe.WornIn(slot));
        var next = opts[((cur < 0 ? 0 : cur) + d + opts.Count) % opts.Count];
        G.Wardrobe.Wear(slot, next);
        G.Autosave();
    }

    protected override float BodyHeight(UiCtx c) => Slots.Length * 70 + 40;

    protected override void DrawBody(UiCtx c, float x, float y, float w)
    {
        var D = c.D;
        foreach (var it in Items)
        {
            var slot = (OutfitSlot)it.Tag!;
            bool focus = Items.IndexOf(it) == Index;
            const float h = 60;
            it.X = x; it.Y = y; it.W = w; it.H = h; it.Drawn = true;
            D.Rect(x, y, w, h, focus ? C.Gold2 : C.White, 16);
            float fs = c.Px(18);
            D.Text(it.Label, x + 18, UiCtx.Mid(y, h, fs), fs, C.Ink, FontKind.Title);
            var worn = G.Wardrobe.WornIn(slot);
            string name = worn == null ? T("wardrobe.none") : T($"outfit.{worn}");
            int n = Options(slot).Count(o => o != null);
            float right = x + w - 18;
            D.Text("▶", right, UiCtx.Mid(y, h, fs), fs, C.InkSoft, FontKind.Strong, Align.Right);
            float nw = D.Measure(name, fs, FontKind.Strong);
            D.Text(name, right - 28, UiCtx.Mid(y, h, fs), fs, C.Ink, FontKind.Strong, Align.Right);
            D.Text("◀", right - 28 - nw - 12, UiCtx.Mid(y, h, fs), fs, C.InkSoft, FontKind.Strong, Align.Right);
            if (slot == OutfitSlot.Scarf && worn != null && Outfits.Get(worn)?.Color is string col)
                D.Rect(right - 28 - nw - 60, y + h / 2 - 10, 20, 20, C.Hex(col), 10);
            D.Text($"{n}", x + 18 + D.Measure(it.Label, fs, FontKind.Title) + 10, UiCtx.Mid(y, h, c.Px(13)), c.Px(13), C.InkSoft, FontKind.Strong);
            Track(it, y, h);
            y += 70;
        }
        D.Text(T("wardrobe.hint"), x + 4, y + 6, c.Px(14), C.InkSoft, FontKind.Strong);
    }
}
