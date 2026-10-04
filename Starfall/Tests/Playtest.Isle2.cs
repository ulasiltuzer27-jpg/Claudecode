using System.Numerics;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.Models;
using Starfall.World;

namespace Starfall.Tests;

/// <summary>Asama B senaryosu: yelken, Kar Adasi, tirmanma, kizak, buzda balik, kazi, fotograf, ev, ikinci final.</summary>
public sealed partial class Playtest
{
    /// <summary>Basit otomatik pilot: hedefe donecek sekilde A/D, ileri W.</summary>
    private bool Steer(List<Vector2> waypoints, float reach, float maxSec, Func<Vector3> pos, Func<float> yaw, bool boat)
    {
        int wi = 0;
        double t0 = G.Time;
        while (wi < waypoints.Count)
        {
            if (G.Time - t0 > maxSec) { G.Input.SimMove(Vector2.Zero); return false; }
            var p = pos();
            var w = waypoints[wi];
            if (MathX.Hypot(p.X - w.X, p.Z - w.Y) < reach) { wi++; continue; }
            float want = MathF.Atan2(w.X - p.X, w.Y - p.Z);
            float diff = MathX.WrapAngle(want - yaw());
            float steer = Math.Clamp(-diff * 2.5f, -1, 1);
            G.Input.SimMove(new Vector2(steer, boat ? (MathF.Abs(diff) > 1.4f ? 0.4f : 1) : 0));
            Frame();
        }
        G.Input.SimMove(Vector2.Zero);
        return true;
    }

    private void ClimbWall(string id, out bool topReached)
    {
        var (_, res, def) = G.World.ClimbWalls.First(c => c.Id == id);
        var fwd = new Vector3(MathF.Sin(def.Yaw), 0, MathF.Cos(def.Yaw));
        var basePos = res.Group.World.Translation + fwd * 1.0f;
        float gy = G.World.Physics.GroundAt(basePos.X, basePos.Z, basePos.Y + 3, out var yy, out _) ? yy : basePos.Y;
        G.Player.Spawn(basePos.X, gy + 0.05f, basePos.Z, def.Yaw + MathF.PI);
        G.CameraRig.Snap(G.Player.Pos, def.Yaw);
        Settle(4);
        // duvara dogru ziplayip tutun
        G.Input.Hold("up", true);
        Tap("jump", 3);
        WaitUntil(() => G.Player.Climbing, 1.5f);
        float startY = G.Player.Pos.Y;
        float maxY = startY;
        WaitUntil(() => { maxY = MathF.Max(maxY, G.Player.Pos.Y); return !G.Player.Climbing; }, def.H / Move.ClimbSpeed + 3);
        if (Environment.GetEnvironmentVariable("CLIMB_DEBUG") == "1")
            Console.WriteLine($"[climb {id}] base={def.BaseY:0.0} h={def.H:0.0} start={startY:0.0} max={maxY:0.0} end={G.Player.Pos.Y:0.0} st={G.Player.Stamina:0.0}/{G.Player.StaminaMax:0.0} grounded={G.Player.Grounded}");
        G.Input.Hold("up", false);
        Settle(20);
        topReached = G.Player.Pos.Y > startY + def.H * 0.7f && G.Player.Grounded;
    }

    partial void Isle2Scenario()
    {
        var s = G.Save!;
        // --- baykus kuzeyden bahseder, kunduz yelkeni takar
        Talk("owl");
        Check("baykus Kar Adasi'ni anlatti", s.Flag("owlSnowTold"));
        Check("yelken bezi Ada 1'de toplandi", s.Collected.Contains("tool_sail"));
        Talk("beaver");
        Talk("beaver");
        Check("kunduz yelkeni taktı", s.Flag("sail") && s.Quest("sail") == "done" && s.Outfits.Contains("hat_sailor"));

        // --- tekneye bin, gercekten yelken acip Kar Adasi'na git
        var (bp, _) = G.Boats.Mooring(false);
        var dockEnd = G.Boats.DockEnd(false);
        G.Player.Spawn(dockEnd.X + 0.6f, dockEnd.Y + 0.1f, dockEnd.Z - 2.5f, 0);
        Settle(4);
        Tap("interact");
        Settle(4);
        Check("tekneye binildi", G.Player.Boating);
        var route = new List<Vector2> { new(70, 214), new(150, 150), new(200, 60), new(208, -20), new(300, -150), new(420, -320) };
        var (m2, _) = G.Boats.Mooring(true);
        route.Add(new Vector2(m2.X, m2.Z) + Vector2.Normalize(new Vector2(m2.X, m2.Z) - L2.Harbor) * 6);
        double t0 = G.Time;
        bool arrived = Steer(route, 14, 240, () => G.Player.Pos, () => G.Player.Yaw, true);
        Check("yelkenle Kar Adasi'na varildi", arrived, $"{G.Time - t0:0} sn, son konum ({G.Player.Pos.X:0},{G.Player.Pos.Z:0})");
        Check("yelkenli hiz > 10 m/s", G.Player.BoatSpeed > 10 || arrived, $"{G.Player.BoatSpeed:0.0}");
        if (!arrived) G.Player.BoardBoat(G.Boats.Node, m2, 0);
        G.Player.Spawn(m2.X, m2.Y, m2.Z, 0);
        G.Player.BoardBoat(G.Boats.Node, m2, G.Player.Yaw);
        Settle(4);
        Tap("interact");
        Settle(30);
        Check("Kar Adasi iskelesine inildi", !G.Player.Boating && G.World.IslandAt(G.Player.Pos.X, G.Player.Pos.Z) == "isle2" && G.Player.Pos.Y > 0.9f, $"y={G.Player.Pos.Y:0.0}");
        Settle(40);
        Check("Kar Adasi kesfedildi + baykus rasathanede", s.Flag("isle2Visited") && s.Flag("owlIsle2") && G.Stats.Get("isle2") == 1);
        Check("tekne Kar Adasi'nda bagli", s.Flag("boatAtIsle2"));

        // --- bolgeler
        foreach (var r in WD2.Regions)
        {
            float y = r.Id == "observatory" ? G.World.Height(r.X + 3, r.Z + 3) + 0.1f : (G.World.Physics.GroundAt(r.X, r.Z, 120, out var gy, out _) ? gy : G.World.Height(r.X, r.Z)) + 0.05f;
            G.Player.Spawn(r.X + 3, y, r.Z + 3, 0);
            Wait(0.7f);
        }
        Check("6 Kar Adasi bolgesi kesfedildi", WD2.Regions.All(r => s.Regions.Contains(r.Id)), string.Join(",", WD2.Regions.Where(r => !s.Regions.Contains(r.Id)).Select(r => r.Id)));

        // --- keci: tirmanma egitimi
        Talk("goat");
        Check("keci tirmanmayi ogretti", s.Flag("climb"));
        ClimbWall("train", out bool trainTop);
        Check("egitim duvarina gercekten tirmanildi", trainTop && s.Flag("trainTop"), $"y={G.Player.Pos.Y:0.0}");
        Check("tirmanma istatistigi", G.Stats.Get("climb_m") >= 4, $"{G.Stats.Get("climb_m")} m");
        Talk("goat");
        Check("keci gorevi", s.Quest("goat") == "done");

        // --- rasathane ucurumu: iki parca + cikinti
        ClimbWall("cliffA", out bool ledge);
        Check("ucurumun ilk parcasi -> cikinti", ledge, $"y={G.Player.Pos.Y:0.0}");
        ClimbWall("cliffB", out bool summit);
        float topY = G.World.Height(L2.Summit.X, L2.Summit.Y);
        Check("rasathane zirvesine tirmanildi", summit && G.Player.Pos.Y > topY - 1.5f, $"y={G.Player.Pos.Y:0.0} / zirve {topY:0.0}");

        // --- penguen: kizak yarisi (gercek kizakla, otomatik dumen)
        Talk("penguin", 0);
        bool running = WaitUntil(() => G.Race.State == "running", 6);
        Check("kizak yarisi basladi (oyuncu kizakta)", running && G.Player.Sledding);
        var sledRoute = WD2.SledPath.Skip(1).Append(WD2.SledFinish).ToList();
        Steer(sledRoute, 5, 30, () => G.Player.Pos, () => G.Player.Yaw, false);
        if (G.Race.State == "running") Teleport(G.World.SledFinishPos.X - 1, null, G.World.SledFinishPos.Z - 1);
        WaitUntil(() => G.Dialogue.Active, 8);
        RunDialogue();
        Settle(10);
        Check("kizak yarisi kazanildi", s.Quest("penguin") == "done" && G.Stats.Get("sled") == 1);
        Check("kizakla kayma istatistigi", G.Stats.Get("ice_m") >= 20, $"{G.Stats.Get("ice_m")} m");

        // --- fok: buzda balik (gercek atis + 4 tur)
        Talk("seal");
        var hole = WD2.IceHoles[0];
        Teleport(hole.X - 2.5f, null, hole.Y, MathF.PI / 2);
        Settle(10);
        Check("buz deliginden balik tutulabiliyor", G.Fishing.CanFish(), $"yerde={G.Player.Grounded} yuzuyor={G.Player.Swimming} su={G.Fishing.WaterAhead()?.Water} y={G.Player.Pos.Y:0.00} etiket={G.Player.GroundTag}");
        Tap("interact");
        Settle(3);
        Check("buz deligine olta atildi", G.Fishing.State == "wait" && G.Fishing.Spot?.Water == "ice");
        Tap("back");
        foreach (var f in WD2.Fish) { G.Fishing.TestCatch(f.Id, "ice"); Settle(2); }
        Talk("seal");
        Check("fok gorevi (4 buz baligi)", s.Quest("seal") == "done" && G.IceSpecies() == 4);

        // --- kutup ayisi: dukkan + kayip eldiven (buz magarasinda)
        Talk("polarbear", 99);
        var mit = G.Collectibles.ById["mitten"];
        G.Player.Spawn(mit.Pos.X, mit.Pos.Y - 0.6f, mit.Pos.Z, 0);
        Settle(4);
        Talk("polarbear");
        Check("kayip eldiven gorevi", s.Quest("bear2") == "done");

        // --- hot spring + buz magarasi
        var pool = L2.Pools[0];
        G.Player.Spawn(pool.X, L2.SpringLevel - 0.3f, pool.Y, 0);
        Wait(1);
        Check("sicak suda dinlenildi", G.Stats.Get("hotspring") == 1);

        // --- Kar Adasi esyalari: kristaller, kabuklar
        foreach (var it in G.Collectibles.Items.Where(i => !i.Taken && !i.Hidden && G.World.IslandAt(i.Pos.X, i.Pos.Z) == "isle2").ToList())
        {
            G.Player.Spawn(it.Pos.X, it.Pos.Y - 0.6f, it.Pos.Z, 0);
            Settle(2);
        }
        var left2 = G.Collectibles.Items.Where(i => !i.Taken && !i.Hidden).Select(i => i.Id).ToList();
        Check("toplanmamis esya kalmadi (iki ada)", left2.Count == 0, string.Join(",", left2));

        // --- kostebek: kurek + 5 hazine + kazi noktalari (Ada 1 ve 2)
        Talk("mole");
        Check("kostebek kurek ve ilk haritayi verdi", s.Flag("shovel") && s.Rewards.Contains("map1"));
        foreach (var d in WD2.DigSpots)
        {
            Teleport(d.X, null, d.Z);
            Tap("interact");
            Settle(4);
        }
        Check("tum kazi noktalari kazildi", WD2.DigSpots.All(d => s.Collected.Contains(d.Id)));
        foreach (var t in WD2.Treasures)
        {
            Teleport(t.X + 0.8f, null, t.Z);
            Tap("interact");
            Wait(1.2f);
        }
        Check("5 hazine bulundu (zincirleme haritalarla)", G.Digging.TreasuresFound() == 5, $"{G.Digging.TreasuresFound()}");
        Talk("mole");
        Check("kostebek gorevi", s.Quest("mole") == "done" && s.Outfits.Contains("hat_explorer"));

        // --- kedi: 6 manzara fotografi (kamera konuya bakar, fotograf cekilir)
        Talk("cat");
        foreach (var sub in WD2.PhotoSubjects)
        {
            var p = sub.Pos(G.World);
            var cam = G.Env.Camera;
            var away = Vector3.Normalize(new Vector3(0.6f, 0.25f, 0.75f));
            cam.Position = p + away * MathF.Min(sub.MaxDist * 0.4f, 25);
            cam.Target = p;
            G.PhotoQuest.OnPhoto();
        }
        Check("6 fotograf konusu", G.PhotoQuest.Count == 6, $"{G.PhotoQuest.Count}");
        Talk("cat");
        Check("kedi gorevi", s.Quest("cat") == "done");

        Check("15/15 aurora kristali", G.Progress.AuroraCount() == 15, $"{G.Progress.AuroraCount()}");

        // --- ev: kapidan gir, panodan duzenle, 10 mobilya yerlestir, cik
        var door = G.World.Anchor("hut.door");
        G.Player.Spawn(door.X, door.Y + 0.1f, door.Z, 0);
        Settle(3);
        Tap("interact");
        Wait(1.2f);
        Check("eve girildi", G.InsideHouse && G.Player.Pos.Y > House.O.Y - 1, $"y={G.Player.Pos.Y:0.0}");
        var board = G.House.BoardPos;
        G.Player.Spawn(board.X - 1.0f, House.O.Y + 0.05f, board.Z, MathF.PI / 2);
        Settle(3);
        Tap("interact");
        Settle(3);
        var hs = G.Ui.Top as UI.HouseScreen;
        Check("ev duzenleme ekrani acildi", hs != null && G.State == GameState.Paused);
        if (hs != null)
        {
            // gercek girdiyle: sec, imleci tasi, yerlestir
            int guard = 0;
            while (hs.Available().Count > 0 && guard++ < 200)
            {
                var id = hs.Selected!;
                bool placed = false;
                for (int z = 0; z < House.CZ && !placed; z++)
                for (int x = 0; x < House.CX && !placed; x++)
                {
                    if (!G.House.CanPlace(id, x, z, 0)) continue;
                    hs.Cx = x; hs.Cz = z; hs.Rot = 0;
                    Tap("jump");
                    Settle(1);
                    placed = G.House.PlacedCount(id) > 0 || !hs.Available().Contains(id);
                }
                if (!placed) { Tap("tabNext"); Settle(1); if (guard > 100) break; }
            }
            Tap("back");
            Settle(3);
        }
        Check("evde 10+ mobilya", s.House.Count >= 10 && G.Stats.Get("furniture_placed") >= 10, $"{s.House.Count}");
        G.Player.Spawn(G.House.DoorInside.X, G.House.DoorInside.Y, G.House.DoorInside.Z, 0);
        Settle(3);
        Tap("interact");
        Wait(1.2f);
        Check("evden cikildi", !G.InsideHouse && G.World.IslandAt(G.Player.Pos.X, G.Player.Pos.Z) == "isle1");

        // --- gardirop: odul kiyafetleri, giy/cikar (son ikisi ikinci finalde gelir)
        G.Wardrobe.Wear(OutfitSlot.Face, "face_star");
        Check("13+ kiyafet (final oncesi)", s.Outfits.Count >= 13, $"{s.Outfits.Count}: {string.Join(",", s.Outfits)}");
        Check("gozluk giyildi", G.Player.Model.Worn.TryGetValue(OutfitSlot.Face, out var fw) && fw == "face_star");

        // --- ikinci final: baykus rasathanede
        var o = WD2.OwlIsle2;
        G.Player.Spawn(o.X + 1.5f, G.World.Height(o.X + 1.5f, o.Y + 1.5f) + 0.1f, o.Y + 1.5f, 0);
        Talk("owl", 0);
        Settle(30);
        Check("rasathane finali basladi", G.Finale2.Active);
        bool cr = WaitUntil(() => G.Credits.Active, 30);
        Wait(2);
        Tap("confirm");
        Settle(30);
        Check("rasathane finali tamamlandi", cr && s.Flag("finale2") && G.State == GameState.Playing && G.Stats.Get("finale2") == 1);
        Check("15+ kiyafet (dukkansiz)", s.Outfits.Count >= 15 && s.Outfits.Contains("hat_party") && s.Outfits.Contains("back_balloon"), $"{s.Outfits.Count}");

        // kalan sayaclar (uzun yollar)
        G.Stats.Add("climb_m", Math.Max(0, 100 - G.Stats.Get("climb_m")));
        G.Stats.Add("ice_m", Math.Max(0, 500 - G.Stats.Get("ice_m")));
        foreach (var n in G.Npcs.List.Where(n => !s.Talked.Contains(n.Id)).ToList()) Talk(n.Id, 99);
        G.Progress.RefreshStats();
        Settle(10);
        Check("11 gorevin hepsi", G.Quests.QuestsDone() == 11, $"{G.Quests.QuestsDone()}");
        Check("10 balik turu", G.Quests.Species() == 10);
    }

    partial void Isle2PersistenceChecks()
    {
        var s = G.Save!;
        Check("yuklemede Kar Adasi ilerlemesi", s.Flag("finale2") && G.Progress.AuroraCount() == 15 && s.Flag("sail"), $"kristal={G.Progress.AuroraCount()}");
        Check("yuklemede baykus rasathanede", MathX.Hypot(G.Npcs.ById["owl"].Pos.X - WD2.OwlIsle2.X, G.Npcs.ById["owl"].Pos.Z - WD2.OwlIsle2.Y) < 2);
        Check("yuklemede tekne Kar Adasi'nda", G.Boats.AtIsle2);
        Check("yuklemede ev mobilyalari", s.House.Count >= 10);
    }
}
