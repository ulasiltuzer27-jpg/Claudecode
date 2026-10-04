using System.Numerics;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.Models;
using Starfall.UI;
using static Starfall.Core.Loc;

namespace Starfall.World;

/// <summary>Asama B'nin gunluk, koleksiyon, harita ve tamamlanma katkilari.</summary>
public sealed partial class GameWorld
{
    partial void ExtendCompletion(Game g, SaveData s, List<float> parts)
    {
        var p = g.Progress;
        int isle2Shells = s.Collected.Count(id => id.StartsWith("sh_") && int.TryParse(id[3..], out var n) && n >= 100);
        parts.Add(p.AuroraCount() / (float)AuroraTotal);
        parts.Add(s.Flag("finale2") ? 1 : 0);
        parts.Add(Quests.Isle2QuestIds.Count(q => s.Quest(q) == "done") / (float)Quests.Isle2QuestIds.Length);
        parts.Add(g.IceSpecies() / (float)WD2.Fish.Length);
        parts.Add(g.Digging.TreasuresFound() / (float)WD2.Treasures.Length);
        parts.Add(s.Regions.Count(r => WD2.Regions.Any(d => d.Id == r && !d.Secret)) / (float)WD2.Regions.Count(d => !d.Secret));
        parts.Add(s.Regions.Contains("icecave") ? 1 : 0);
        parts.Add(MathF.Min(1, isle2Shells / 40f));
    }

    partial void ExtraStats(Game g, SaveData s)
    {
        g.Stats.Max("auroras_max", g.Progress.AuroraCount());
        g.Stats.Max("treasures_max", g.Digging.TreasuresFound());
        g.Stats.Max("ice_species", g.IceSpecies());
        g.Stats.Max("outfits_max", s.Outfits.Count);
        g.Stats.Max("photo_subjects", g.PhotoQuest.Count);
        if (s.Flag("isle2Visited")) g.Stats.Max("isle2", 1);
    }

    partial void ExtraQuestsImpl(Game g, List<(string Title, string Desc, string State)> list)
    {
        var s = g.Save!;
        if (s.Flag("finale"))
        {
            int n = g.Progress.AuroraCount();
            list.Insert(1, (T("quest.main2.title"),
                s.Flag("finale2") ? T("quest.main2.done") : !s.Flag("owlSnowTold") ? T("quest.main2.start") : T("quest.main2.desc", ("n", n), ("goal", AuroraTotal)),
                s.Flag("finale2") ? "done" : "active"));
        }
        void Q(string id, params (string, object)[] p)
        {
            var st = s.Quest(id);
            if (st == "none") { list.Add((T($"quest.{id}.title"), T("quest.unknown"), "none")); return; }
            list.Add((T($"quest.{id}.title"), T(st == "done" ? $"quest.{id}.done" : $"quest.{id}.desc", p), st));
        }
        if (s.Flag("owlSnowTold") || s.Quest("sail") != "none") Q("sail");
        Q("mole", ("n", g.Digging.TreasuresFound()));
        Q("cat", ("n", g.PhotoQuest.Count));
        if (s.Flag("isle2Visited"))
        {
            Q("penguin");
            Q("seal", ("n", g.IceSpecies()));
            Q("goat");
            Q("bear2");
        }
    }

    partial void ExtraCollectionImpl(Game g, List<(string Num, string Label)> list)
    {
        var s = g.Save!;
        if (s.Flag("isle2Visited") || g.Progress.AuroraCount() > 0) list.Add(($"{g.Progress.AuroraCount()} / {AuroraTotal}", T("coll.auroras")));
        list.Add(($"{g.Digging.TreasuresFound()} / {WD2.Treasures.Length}", T("coll.treasures")));
        list.Add(($"{s.Outfits.Count} / {Outfits.All.Length}", T("coll.outfits")));
        list.Add(($"{s.Furniture.Distinct().Count()} / {Furniture.All.Length}", T("coll.furniture")));
        list.Add(($"{g.PhotoQuest.Count} / {WD2.PhotoSubjects.Length}", T("coll.photos")));
    }

    partial void DrawMapPinsImpl(Game g, UiCtx c, Terrain t, Func<float, float, Vector2> toMap)
    {
        var s = g.Save!;
        var D = c.D;
        // hazine haritasi kartlari: elde olan ama kazilmamis hazineler, haritanin altinda kucuk kart
        var maps = WD2.Treasures.Where(tr => s.Rewards.Contains(tr.MapId) && !s.Collected.Contains(tr.Id)).ToList();
        float cx = c.W / 2 + 250, cy = 120;
        foreach (var tr in maps.Take(2))
        {
            var tex = UiTex.Of(Digging.MapCard(this, tr));
            if (tex != null)
            {
                D.Rect(cx - 4, cy - 4, 128, 152, C.White, 12);
                D.Image(tex, cx, cy, 120, 120);
                D.Text(T("journal.treasure", ("n", tr.MapId[3..])), cx + 60, cy + 124, c.Px(13), C.Ink, FontKind.Strong, Align.Center);
                D.Text(T($"island.{tr.Island}"), cx + 60, cy + 140 - 2, c.Px(11), C.InkSoft, FontKind.Strong, Align.Center);
            }
            cy += 170;
        }
        // tekne simgesi
        if (s.Flag("sail"))
        {
            var bp = g.Boats.Node.Position;
            if (IslandAt(bp.X, bp.Z) == t.Id)
            {
                var m = toMap(bp.X, bp.Z);
                D.Rect(m.X - 7, m.Y - 7, 14, 14, C.White, 7);
                D.Rect(m.X - 4, m.Y - 4, 8, 8, C.Sky, 4);
            }
        }
    }
}
