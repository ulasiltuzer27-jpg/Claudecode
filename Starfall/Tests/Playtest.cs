using System.Diagnostics;
using System.Numerics;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.World;

namespace Starfall.Tests;

/// <summary>
/// Uctan uca oyun testi: pencere olmadan, gercek oyun dongusuyle (sabit 1/60 sn adim).
/// Hareket gercek girdi eylemleriyle (Input.Hold/Tap); uzun yollar (100 kabuk gibi) oyuncuyu
/// esyanin ustune isinlayarak. Sonunda tum basarimlarin acildigi ve kaydin yeniden
/// yuklenince korundugu dogrulanir.
/// </summary>
public sealed partial class Playtest
{
    private readonly List<string> _results = new();
    private int _failed;
    private Game G = null!;
    private readonly string _data;
    private readonly Dictionary<string, string> _args;

    private Playtest(Dictionary<string, string> args)
    {
        _args = args;
        _data = args.TryGetValue("data", out var d) ? d : Path.Combine(Path.GetTempPath(), "starfall-playtest-" + Environment.ProcessId);
    }

    public static int Run(Dictionary<string, string> args)
    {
        Environment.SetEnvironmentVariable("STARFALL_QUIET", "1");
        var t = new Playtest(args);
        var sw = Stopwatch.StartNew();
        try { t.Body(); }
        catch (Exception ex)
        {
            t._failed++;
            t._results.Add("FAIL istisna: " + ex);
        }
        foreach (var r in t._results) Console.WriteLine(r);
        Console.WriteLine(t._failed > 0 ? $"\n{t._failed} denetim basarisiz" : $"\nHepsi gecti ({t._results.Count} denetim, {sw.Elapsed.TotalSeconds:0.0} sn)");
        try { if (!args.ContainsKey("data")) Directory.Delete(t._data, true); } catch { /* yok say */ }
        return t._failed > 0 ? 1 : 0;
    }

    private void Check(string name, bool cond, string detail = "")
    {
        _results.Add($"{(cond ? "OK  " : "FAIL")} {name}{(detail.Length > 0 ? $" ({detail})" : "")}");
        if (!cond) _failed++;
    }

    private Game Boot(bool fresh)
    {
        var a = new Dictionary<string, string> { ["data"] = _data, ["lang"] = "tr", ["quality"] = "low" };
        if (fresh) a["fresh"] = "1";
        var g = new Game(a, headless: true);
        g.Build();
        return g;
    }

    // ------------------------------------------------------------ yardimcilar
    private void Frame() => G.Frame(1 / 60f);

    private void Settle(int frames)
    {
        for (int i = 0; i < frames; i++) Frame();
    }

    private void Wait(float sec)
    {
        double t0 = G.Time;
        int guard = 0;
        while (G.Time < t0 + sec - 1e-4 && guard++ < 100000) Frame();
    }

    private bool WaitUntil(Func<bool> cond, float maxSec)
    {
        double t0 = G.Time;
        while (!cond())
        {
            if (G.Time - t0 > maxSec) return false;
            Frame();
        }
        return true;
    }

    private void Tap(string action, int hold = 3)
    {
        G.Input.Hold(action, true);
        G.Input.Tap(action);
        Settle(hold);
        G.Input.Hold(action, false);
    }

    private void Teleport(float x, float? y, float z, float yaw = 0)
    {
        float yy = y ?? ((G.World.Physics.GroundAt(x, z, 200, out var gy, out _) ? gy : G.World.Height(x, z)) + 0.05f);
        G.Player.Spawn(x, yy, z, yaw);
        G.CameraRig.Snap(G.Player.Pos, yaw + MathF.PI);
        Settle(3);
    }

    /// <summary>Diyalog acikken sonuna kadar ilerle; secenek varsa sec.</summary>
    private void RunDialogue(int choice = 0, int maxSteps = 60)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            var d = G.Dialogue;
            if (!d.Active) return;
            if (d.ShowingChoices && !d.Typing) d.Pick(Math.Min(choice, d.Choices!.Count - 1));
            else d.Advance();
            Settle(2);
        }
    }

    private void Talk(string id, int choice = 0)
    {
        var n = G.Npcs.ById[id];
        G.Player.Spawn(n.Pos.X + MathF.Sin(n.Yaw) * 1.8f, n.Pos.Y + 0.05f, n.Pos.Z + MathF.Cos(n.Yaw) * 1.8f, n.Yaw + MathF.PI);
        Settle(3);
        Tap("interact");
        Settle(2);
        RunDialogue(choice);
    }

    // ------------------------------------------------------------ senaryo
    private void Body()
    {
        G = Boot(true);
        G.NewGame(1);
        Settle(10);
        Check("yeni oyun: iskelede basladi", G.State == GameState.Playing && G.Player.Pos.Y > 0.9f, $"y={G.Player.Pos.Y:0.00}");

        // --- hareket
        var p0 = G.Player.Pos;
        G.Input.Hold("up", true);
        Wait(2);
        G.Input.Hold("up", false);
        Settle(10);
        var p1 = G.Player.Pos;
        float moved = MathX.Hypot(p1.X - p0.X, p1.Z - p0.Z);
        Check("W ile yurume", moved > 4, $"{moved:0.0} m");
        Check("iskelede kaldi (dusmedi)", p1.Y > 0.9f, $"y={p1.Y:0.00}");

        // --- ziplama: tepe yuksekligi
        Settle(5);
        float yBefore = G.Player.Pos.Y;
        G.Player.PeakY = float.MinValue;
        Tap("jump", 4);
        Wait(1.2f);
        float peak = G.Player.PeakY - yBefore;
        Check("ziplama yuksekligi ~1.8 m", peak > 1.3f && peak < 2.3f, $"{peak:0.00} m");
        Check("ziplama istatistigi", G.Stats.Get("jumps") >= 1);

        // --- baykus: giris + ilk tuy + zirveye ucus
        Talk("owl");
        Settle(12);
        RunDialogue();
        var owl = G.Npcs.ById["owl"];
        Check("baykus girisi", G.Flag("owlIntro"));
        Check("baykus tuyu verdi", G.Progress.FeatherCount() == 1, $"tuy={G.Progress.FeatherCount()}");
        Check("baykus zirveye uctu", owl.Pos.Z < -90, $"z={owl.Pos.Z:0.0}");

        // --- kanat cirpma
        Teleport(-58, null, 50);
        Settle(5);
        Tap("jump", 4);
        Wait(0.25f);
        Tap("jump", 4);
        Wait(0.1f);
        Check("havada kanat cirpma", G.Player.FlapsUsed == 1);
        Wait(1.5f);

        // --- suzulme + havada kalma: yuksekten, tus basili
        Teleport(40, 60, 150, MathF.PI);
        G.Input.Hold("jump", true);
        G.Input.Hold("up", true);
        Wait(0.6f);
        bool gliding = G.Player.Gliding;
        Wait(12);
        G.Input.Hold("up", false);
        G.Input.Hold("jump", false);
        Settle(20);
        Check("suzulme basladi", gliding);
        Check("suzulme mesafesi >= 60 m", G.Stats.Get("glide_max") >= 60, $"{G.Stats.Get("glide_max")} m");
        Check("havada kalma >= 10 s", G.Stats.Get("air_max") >= 10, $"{G.Stats.Get("air_max")} s");

        // --- yuzme
        Teleport(0, 0, 200, 0);
        G.Input.Hold("up", true);
        G.Input.Hold("sprint", true);
        Wait(3);
        G.Input.Hold("sprint", false);
        G.Input.Hold("up", false);
        Check("suya girince yuzuyor", G.Player.Swimming);
        Check("yuzme istatistigi artiyor", G.Stats.Get("swim_m") >= 5, $"{G.Stats.Get("swim_m")} m");

        // --- mantar ziplatmasi
        var m5 = WD.GiantMushrooms[0];
        Teleport(m5.X, G.World.Height(m5.X, m5.Z) + m5.H + 3, m5.Z);
        G.Player.PeakY = float.MinValue;
        float yb = G.Player.Pos.Y;
        Wait(1.6f);
        Check("dev mantar ziplatiyor", G.Player.PeakY > yb + 2, $"tepe +{G.Player.PeakY - yb:0.0} m");

        // --- toplanabilirler: Ada 1'dekilerin ustune isinlan
        foreach (var it in G.Collectibles.Items.Where(i => !i.Taken && !i.Hidden && G.World.IslandAt(i.Pos.X, i.Pos.Z) == "isle1").ToList())
        {
            G.Player.Spawn(it.Pos.X, it.Pos.Y - 0.6f, it.Pos.Z, 0);
            Settle(2);
        }
        Settle(4);
        var left = G.Collectibles.Items.Where(i => !i.Taken && !i.Hidden && G.World.IslandAt(i.Pos.X, i.Pos.Z) == "isle1").Select(i => i.Id).ToList();
        Check("26 dunya parcasi toplandi", G.Progress.ShardCount() == 26, $"parca={G.Progress.ShardCount()}");
        Check("100 kabuk toplandi", G.Save!.ShellsTotal == 100, $"kabuk={G.Save.ShellsTotal}");
        Check("dunyadaki 4 tuy (+baykus)", G.Progress.FeatherCount() == 5, $"tuy={G.Progress.FeatherCount()}");
        Check("3 havuc + 3 alet", G.Quests.Carrots() == 3 && G.Quests.Tools() == 3);
        Check("toplanmamis esya kalmadi", left.Count == 0, string.Join(",", left));

        // --- gorevler
        Talk("rabbit");
        Talk("rabbit");
        Talk("beaver");
        Talk("beaver");
        Check("tavsan gorevi", G.Save.Quest("rabbit") == "done");
        Check("kunduz gorevi + kopru", G.Save.Quest("beaver") == "done" && G.World.BridgeBuilt);
        Wait(1.5f);
        var (ba, bb) = G.World.BridgeEnds;
        float onBridge = G.World.Physics.GroundAt((ba.X + bb.X) / 2, (ba.Z + bb.Z) / 2, 30, out var by, out _) ? by : -99;
        Check("kopru ortasi yurunebilir", onBridge > 0.5f, $"y={onBridge:0.00}");

        // --- dukkan: 4 urun (95 kabuk)
        for (int i = 0; i < 4; i++)
        {
            Talk("hedgehog", 0);
            RunDialogue();
        }
        Check("dukkandan hepsi alindi", G.Save.Shells == 5 && G.Flag("rod") && G.Flag("hat"), $"kalan={G.Save.Shells}");
        Check("hasir sapka giyildi", G.Player.Model.Worn.TryGetValue(Models.OutfitSlot.Hat, out var hat) && hat == "hat_straw");

        // --- balik: gercek atis + vurma, sonra her tur icin yakalama
        Talk("bear");
        G.Player.Spawn(8, 1.3f, 172, MathF.PI / 2);
        Settle(6);
        Check("iskeleden balik tutulabiliyor", G.Fishing.CanFish());
        Tap("interact");
        Settle(3);
        Check("olta atildi", G.Fishing.State == "wait");
        bool bit = WaitUntil(() => G.Fishing.State == "bite", 12);
        Tap("interact");
        Settle(3);
        Check("vurdu -> makara", bit && G.Fishing.State == "reel", G.Fishing.State);
        // makara: tusu tutarak gercekten yakalamayi dene (zor baliklarda kacabilir)
        G.Input.Hold("interact", true);
        WaitUntil(() => G.Fishing.State != "reel", 25);
        G.Input.Hold("interact", false);
        if (G.Fishing.State != "idle") { Tap("back"); Settle(2); }
        foreach (var id in new[] { "anchovy", "seabass", "trout", "carp", "moonfish", "goldfish" })
        {
            G.Fishing.TestCatch(id);
            Settle(2);
        }
        Talk("bear");
        Talk("bear");
        Check("6 balik turu", G.Quests.Isle1Species() == 6);
        Check("ayi gorevi", G.Save.Quest("bear") == "done");

        // --- yaris: geri sayim, sonra bayraga kos
        Talk("frog", 0);
        bool running = WaitUntil(() => G.Race.State == "running", 10);
        var flag = G.World.RaceFlagPos;
        Teleport(flag.X - 1, null, flag.Z - 1);
        WaitUntil(() => G.Dialogue.Active, 10);
        RunDialogue();
        Settle(10);
        Check("yaris kazanildi", running && G.Save.Quest("frog") == "done" && G.Stats.Get("race") == 1);

        Check("30/30 parca", G.Progress.ShardCount() == 30, $"{G.Progress.ShardCount()}");
        Check("8/8 tuy", G.Progress.FeatherCount() == 8, $"{G.Progress.FeatherCount()}");

        // --- bolgeler
        foreach (var (x, z) in new[] { (8f, 112f), (-58f, 50f), (-120f, -6f), (30f, -20f), (136f, 50f), (-86f, -80f), (104f, -58f), (6f, -105f), (-182f, -108f) })
        {
            Teleport(x, null, z);
            Wait(0.7f);
        }
        var reg = G.Save.Regions.Where(r => WD.Regions.Any(d => d.Id == r)).OrderBy(r => r).ToList();
        Check("9 bolge kesfedildi", reg.Count == 9, string.Join(",", reg));

        // --- final
        Talk("owl", 0);
        Settle(30);
        Check("final basladi", G.Finale.Active);
        bool credits = WaitUntil(() => G.Credits.Active, 25);
        Check("jenerik basladi", credits);
        Wait(2);
        Tap("confirm");
        Settle(30);
        Check("final tamamlandi", G.Flag("finale") && G.State == GameState.Playing && G.Finale.Lit, $"durum={G.State}");
        Check("final odulu: tac", G.Save.Outfits.Contains("hat_crown"));

        // --- foto, gece, kalan sayaclar
        Tap("photo");
        Settle(3);
        Check("photo mode acildi", G.State == GameState.Photo);
        Tap("shot");
        Settle(3);
        Tap("back");
        Settle(3);
        Check("photo mode kapandi", G.State == GameState.Playing);
        G.SetHour(0.3f);
        Wait(1.2f);
        G.Stats.Add("jumps", Math.Max(0, 500 - G.Stats.Get("jumps")));
        G.Stats.Add("swim_m", Math.Max(0, 200 - G.Stats.Get("swim_m")));
        G.Progress.RefreshStats();
        Settle(10);

        Isle2Scenario();

        var missing = G.AchievementsMissing().ToList();
        Check($"{Achievements.AchievementData.All.Count}/{Achievements.AchievementData.All.Count} basarim", missing.Count == 0, "eksik: " + string.Join(",", missing));
        Check("tamamlanma %100", G.Progress.Completion() == 100, $"%{G.Progress.Completion()}");

        // --- kalicilik: menuye don (kaydeder), oyunu yeniden kur, yuvayi ac
        G.ToMainMenu();
        Settle(5);
        int ach = G.Achievements.Count;
        G = Boot(false);
        G.LoadSlot(1);
        Settle(6);
        Check("yuklemede ilerleme korundu", G.Progress.ShardCount() == 30 && G.Flag("finale") && G.World.BridgeBuilt && G.Finale.Lit,
            $"parca={G.Progress.ShardCount()} final={G.Flag("finale")} kopru={G.World.BridgeBuilt}");
        Check("yuklemede basarimlar korundu", G.Achievements.Count == ach, $"{G.Achievements.Count}/{ach}");
        Check("yuklemede kiyafetler korundu", G.Player.Model.Worn.TryGetValue(Models.OutfitSlot.Hat, out var h2) && h2 != null, h2 ?? "yok");
        Isle2PersistenceChecks();
    }

    partial void Isle2Scenario();
    partial void Isle2PersistenceChecks();
}
