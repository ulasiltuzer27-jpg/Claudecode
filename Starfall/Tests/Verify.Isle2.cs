using System.Numerics;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.Models;
using Starfall.World;

namespace Starfall.Tests;

/// <summary>Asama B denetimleri (Kar Adasi, tekne, tirmanma, kizak, kazi, ev).</summary>
public sealed partial class Verify
{
    private partial IEnumerable<string> ExtraKeys()
    {
        foreach (var q in Quests.Isle2QuestIds) foreach (var s in new[] { "title", "desc", "done" }) yield return $"quest.{q}.{s}";
        foreach (var s in new[] { "title", "desc", "done", "start" }) yield return $"quest.main2.{s}";
        foreach (var p in WD2.PhotoSubjects) yield return $"photo.subject.{p.Id}";
        foreach (var f in Furniture.All) yield return $"furn.{f.Id}";
        foreach (var i in new[] { "isle1", "isle2" }) yield return $"island.{i}";
        foreach (var id in new[] { "tool_sail", "mitten" }) yield return $"toast.{id}";
        foreach (var r in new[] { "win", "winAgain", "lose" }) yield return $"dlg.penguin.{r}";
        foreach (var (_, _, _, key) in WD2.Signs) yield return key;
    }

    private partial float ExtraSpotDy(string id)
    {
        foreach (var s in WD2.Crystals) if (s.Id == id) return float.IsNaN(s.Dy) ? 0 : s.Dy;
        return 0;
    }

    private partial int ExtraQuestCount() => Quests.Isle2QuestIds.Length;

    partial void ExtraMaxOf(Dictionary<string, double> d)
    {
        d["auroras_max"] = WD2.Crystals.Length + WD2.QuestCrystals.Length;
        d["finale2"] = 1; d["sled"] = 1; d["hotspring"] = 1; d["icecave"] = 1; d["isle2"] = 1;
        d["ice_species"] = WD2.Fish.Length;
        d["treasures_max"] = WD2.Treasures.Length;
        d["photo_subjects"] = WD2.PhotoSubjects.Length;
        d["furniture_placed"] = Furniture.All.Length;
    }

    partial void ExtraChecks()
    {
        var W = G.World;
        float g = -Move.Gravity;
        float Apex(int n) => Move.JumpVel * Move.JumpVel / (2 * g) + n * (Move.FlapVel * Move.FlapVel / (2 * g));

        // --- aurora kristalleri
        Ok("15 aurora kristali", WD2.Crystals.Length + WD2.QuestCrystals.Length == GameWorld.AuroraTotal);

        // --- rasathane ucurumu: tuyle asilamaz, tirmanmak gerekir; her parca dayaniklilikla biter
        float top = W.Height(L2.Summit.X, L2.Summit.Y);
        Ok("rasathane ucurumu 8 tuyle asilamaz (tirmanis sart)", Apex(8) < L2.SummitCliff + 0.3f, $"apex8={Apex(8):0.0} ucurum={L2.SummitCliff}");
        float minStamina = Move.ClimbBase + 4; // finale gelen oyuncu en az 4 tuye sahip
        foreach (var (id, res, def) in W.ClimbWalls)
        {
            float t = def.H / Move.ClimbSpeed;
            Ok($"tirmanma duvari {id}: dayaniklilik yetiyor", t < minStamina - 0.5f, $"{def.H:0.0} m / {Move.ClimbSpeed} = {t:0.0} sn < {minStamina - 0.5f}");
            var topA = res.Group.World.Translation + new Vector3(0, def.H, 0);
            // duvarin tepesinin hemen arkasinda (yukarida) durulacak zemin var mi?
            var back = new Vector3(MathF.Sin(def.Yaw), 0, MathF.Cos(def.Yaw));
            var standAt = topA - back * 0.9f;
            bool stand = W.Physics.GroundAt(standAt.X, standAt.Z, topA.Y + 2, out var sy, out _) && MathF.Abs(sy - topA.Y) < 1.2f;
            Ok($"tirmanma duvari {id}: tepesinde durulur", stand, $"zemin {sy:0.0} / tepe {topA.Y:0.0}");
        }
        Ok("ucurumun dinlenme cikintisi var", W.Anchors.ContainsKey("summitLedge"));

        // --- tekne: iki iskele de derin suda, Ada 1 -> Ada 2 deniz yolu acik
        foreach (var isle2 in new[] { false, true })
        {
            var (mp, _) = G.Boats.Mooring(isle2);
            float depth = W.WaterLevel(mp.X, mp.Z) - W.Height(mp.X, mp.Z);
            Ok($"tekne iskelesi ({(isle2 ? "Kar Adasi" : "Yildiz Adasi")}) derin suda", depth > 1.0f, $"derinlik {depth:0.0} m");
        }
        {
            var a = new Vector2(210, -40);
            var b = new Vector2(G.Boats.Mooring(true).Pos.X, G.Boats.Mooring(true).Pos.Z);
            float shallow = 99;
            for (int i = 0; i <= 200; i++)
            {
                var p = Vector2.Lerp(a, b, i / 200f);
                if (Vector2.Distance(p, b) < 12) break;
                shallow = MathF.Min(shallow, W.WaterLevel(p.X, p.Y) - W.Height(p.X, p.Y));
            }
            Ok("adalar arasi deniz yolu yeterince derin", shallow > 1.0f, $"en sig {shallow:0.0} m");
            W.BoatMode = true;
            bool inside = true;
            for (int i = 0; i <= 50; i++)
            {
                var p = Vector2.Lerp(a, b, i / 50f);
                if (!W.InsideBoundary(new Vector3(p.X, 0, p.Y))) inside = false;
            }
            W.BoatMode = false;
            Ok("deniz yolu sinir icinde (teknedeyken)", inside);
        }

        // --- kizak yolu inerek gole varir
        {
            bool down = true;
            float prev = float.MaxValue;
            foreach (var p in WD2.SledPath)
            {
                float h = W.Height(p.X, p.Y);
                if (h > prev + 0.3f) down = false;
                prev = h;
            }
            float h0 = W.Height(L2.SledTop.X, L2.SledTop.Y), h1 = W.Height(L2.SledBottom.X, L2.SledBottom.Y);
            Ok("kizak yolu surekli iniyor", down && h0 - h1 > 10, $"{h0:0.0} -> {h1:0.0} m");
        }

        // --- buz delikleri ve sicak su havuzlari
        Ok("buz delikleri donmus golde", WD2.IceHoles.All(h => MathX.Hypot(h.X - L2.Lake.X, h.Y - L2.Lake.Y) < L2.LakeR - 2));
        foreach (var p in L2.Pools)
        {
            float d = L2.SpringLevel - W.Height(p.X, p.Y);
            Ok($"sicak su havuzu yuzulecek kadar derin", d > 0.5f, $"{d:0.00} m");
        }

        // --- kazi ve hazine noktalari kuru zeminde, sinir icinde
        var badDig = new List<string>();
        string Why(float x, float z, string? island = null)
        {
            float h = W.Height(x, z);
            if (h < W.WaterLevel(x, z) + 0.6f) return $"suda (h={h:0.0})";
            if (W.IsFrozenWater(x, z)) return "buzda";
            if (island != null && W.IslandAt(x, z) != island) return "yanlis ada";
            if (W.Physics.PointInside(new Vector3(x, h + 0.4f, z))) return "carpisma icinde";
            return "";
        }
        foreach (var d in WD2.DigSpots) if (Why(d.X, d.Z) is { Length: > 0 } w) badDig.Add($"{d.Id}: {w}");
        foreach (var t in WD2.Treasures) if (Why(t.X, t.Z, t.Island) is { Length: > 0 } w) badDig.Add($"{t.Id}: {w}");
        Ok("kazi ve hazine noktalari kuru, acik zeminde", badDig.Count == 0, string.Join(",", badDig));
        Ok("hazine zinciri kopuk degil (her harita bir oncekinin sandiginda)",
            WD2.Treasures.Skip(1).All(t => WD2.Treasures.Any(o => o.Contents.Contains(t.MapId))));

        // --- fotograf konulari olculebilir
        var missingAnchor = new List<string>();
        foreach (var s in WD2.PhotoSubjects) { try { s.Pos(W); } catch { missingAnchor.Add(s.Id); } }
        Ok("fotograf konularinin hepsinin yeri var", missingAnchor.Count == 0, string.Join(",", missingAnchor));

        // --- ev: mobilyalar izgaraya sigar, baslangic yerlesimi gecerli
        Ok("her mobilya izgaraya sigar", Furniture.All.All(f => f.W <= House.CX && f.D <= House.CZ));
        var s0 = SaveManager.NewSave();
        G.Save = s0;
        bool layout = true;
        foreach (var (id, x, z, r) in new[] { ("bed", 0, 0, 0), ("rug", 4, 3, 0), ("table", 4, 3, 0), ("chair", 6, 3, 3), ("plant", 9, 0, 0) })
        {
            if (!G.House.CanPlace(id, x, z, r)) layout = false;
            s0.House.Add(new PlacedFurniture { Id = id, X = x, Z = z, Rot = r });
        }
        Ok("evin baslangic yerlesimi gecerli", layout);
        Ok("cakisan mobilya reddedilir", !G.House.CanPlace("sofa", 0, 1, 0));
        Ok("duvar esyasi yalnizca arka duvara", G.House.CanPlace("painting", 3, 0, 0) && !G.House.CanPlace("painting", 3, 2, 0));
        G.Save = null;

        // --- kiyafet kaynaklari: magaza disi en az 15 kiyafet (ACH_FASHION satin almadan da acilir)
        var nonShop = new HashSet<string> { "scarf_red", "hat_straw", "hat_crown", "hat_sailor", "hat_explorer", "hat_flowers", "hat_party", "back_balloon", "back_shell", "face_star", "scarf_teal" };
        foreach (var t in WD2.Treasures) foreach (var c in t.Contents) if (c.StartsWith("outfit:")) nonShop.Add(c[7..]);
        Ok("en az 15 kiyafet magazasiz kazanilir", nonShop.Count >= 15, $"{nonShop.Count}");
        // mobilya: satin almadan en az 10
        int freeFurn = House.Starter.Length + WD2.Treasures.Sum(t => t.Contents.Count(c => c.StartsWith("furn:"))) + 4; // cerceve, somine, akvaryum, yildiz lamba
        Ok("en az 10 mobilya magazasiz kazanilir", freeFurn >= 10, $"{freeFurn}");
    }
}
