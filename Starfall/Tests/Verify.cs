using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Starfall.Achievements;
using Starfall.Core;
using Starfall.Gameplay;
using Starfall.Render;
using Starfall.World;
using L = Starfall.World.Isle1Shape.L;

namespace Starfall.Tests;

/// <summary>
/// Statik dogrulama: pencere acmadan, saniyeler icinde. Oyunun kendi modullerini (arazi,
/// yerlesim, carpisma, yapilar) calistirir ve tasarim kararlarinin tuttugunu olcer:
///  - TR/EN ceviri anahtarlari esit, yer tutucular ayni, koddaki her anahtar var
///  - basarim semasi, istatistikleri koda bagli, esikler ulasilabilir
///  - arazi determinizmi, carpisma zemini arazi ile ayni
///  - ziplama/cirpma fizigi teras yuksekliklerine uygun (1 / 2 / 4 tuy)
///  - her esya carpisma icinde degil, zemine yakin, su ustunde, sinir icinde
/// </summary>
public sealed partial class Verify
{
    private int _fails, _checks;
    private readonly bool _verbose;
    private Game G = null!;

    private Verify(bool verbose) => _verbose = verbose;

    public static int Run(Dictionary<string, string> args)
    {
        Environment.SetEnvironmentVariable("STARFALL_QUIET", "1");
        var v = new Verify(args.ContainsKey("verbose") || Environment.GetEnvironmentVariable("VERBOSE") != null);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { v.Body(args); }
        catch (Exception ex)
        {
            v._fails++;
            Console.WriteLine("FAIL istisna: " + ex);
        }
        Console.WriteLine(v._fails > 0 ? $"\n{v._fails}/{v._checks} denetim BASARISIZ" : $"\nHepsi gecti ({v._checks} denetim, {sw.Elapsed.TotalSeconds:0.0} sn)");
        return v._fails > 0 ? 1 : 0;
    }

    private void Ok(string name, bool cond, string detail = "")
    {
        _checks++;
        if (!cond) _fails++;
        if (!cond || _verbose) Console.WriteLine($"{(cond ? "OK  " : "FAIL")} {name}{(detail.Length > 0 ? " — " + detail : "")}");
    }

    private static string? ProjectDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (File.Exists(Path.Combine(d.FullName, "Starfall.csproj"))) return d.FullName;
            d = d.Parent;
        }
        return null;
    }

    private void Body(Dictionary<string, string> args)
    {
        // ---------- ceviriler ----------
        var tr = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Gfx.ReadResource("i18n/tr.json"))!;
        var en = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Gfx.ReadResource("i18n/en.json"))!;
        var onlyTr = tr.Keys.Where(k => !en.ContainsKey(k)).ToList();
        var onlyEn = en.Keys.Where(k => !tr.ContainsKey(k)).ToList();
        Ok("TR ve EN ayni anahtarlar", onlyTr.Count == 0 && onlyEn.Count == 0, string.Join(", ", onlyTr.Concat(onlyEn)));
        var ph = new Regex(@"\{(\w+)\}");
        foreach (var k in tr.Keys.Where(en.ContainsKey))
        {
            if ((tr[k].ValueKind == JsonValueKind.Array) != (en[k].ValueKind == JsonValueKind.Array)) Ok($"dizi/metin uyumu: {k}", false);
            string P(JsonElement e) => string.Join(",", ph.Matches(e.GetRawText()).Select(m => m.Groups[1].Value).OrderBy(x => x));
            if (P(tr[k]) != P(en[k])) Ok($"yer tutucular ayni: {k}", false, $"{P(tr[k])} / {P(en[k])}");
        }

        // oyunu pencere olmadan kur (dunya + sistemler)
        var tmp = Path.Combine(Path.GetTempPath(), "starfall-verify-" + Environment.ProcessId);
        G = new Game(new Dictionary<string, string> { ["data"] = tmp, ["fresh"] = "1", ["quality"] = "low" }, headless: true);
        G.Build();
        var W = G.World;

        // koddaki anahtarlar (kaynak klasoru bulunursa)
        var used = new HashSet<string>();
        var proj = args.TryGetValue("src", out var srcDir) ? srcDir : ProjectDir();
        string code = "";
        if (proj != null)
        {
            var files = Directory.EnumerateFiles(proj, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"));
            code = string.Join("\n", files.Select(File.ReadAllText));
            foreach (Match m in Regex.Matches(code, @"\b(?:T|Lines|Say|Ask)\(\s*(?:npc,\s*)?""([a-zA-Z0-9_.]+)""")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(code, @"""((?:dlg|hint|journal|settings|shop|sign|photo|toast|quest|race|region|choice|fish|ui|menu|wardrobe|outfit|house|boat|climb|dig|treasure|island|prompt|coll|pause|slots|ach|campfire|finale|credits|lang|time|presence|npc)\.[a-zA-Z0-9_.]+)""")) used.Add(m.Groups[1].Value);
        }
        foreach (var a in AchievementData.All) { used.Add($"ach.{a.Id}.name"); used.Add($"ach.{a.Id}.desc"); }
        foreach (var r in W.Regions) { used.Add($"region.{r.Id}"); used.Add($"region.{r.Id}.sub"); }
        foreach (var n in G.Npcs.List) used.Add($"npc.{n.Id}");
        foreach (var f in W.AllFish) { used.Add($"fish.{f.Id}"); used.Add($"fish.hint.{f.Id}"); }
        foreach (var s in WD.ShopItems) { used.Add($"shop.{s.Id}"); used.Add($"dlg.hedgehog.bought.{s.Id}"); }
        foreach (var c in WD.Carrots.Concat(WD.Tools)) used.Add($"toast.{c.Id}");
        foreach (var q in SaveData.QuestIds) foreach (var s in new[] { "title", "desc", "done" }) used.Add($"quest.{q}.{s}");
        foreach (var f in PhotoMode.Filters) used.Add($"photo.filter.{f}");
        foreach (var o in Models.Outfits.All) used.Add($"outfit.{o.Id}");
        foreach (var sl in Enum.GetNames<Models.OutfitSlot>()) used.Add($"wardrobe.slot.{sl}");
        for (int i = 0; i < WD.Village.Signs.Length; i++) used.Add($"sign.{i}");
        foreach (var (_, _, key) in W.ExtraSigns) used.Add(key);
        foreach (var k in ExtraKeys()) used.Add(k);
        var missing = used.Where(k => !tr.ContainsKey(k) && !k.EndsWith('.') && !Regex.IsMatch(k, @"\.(vert|frag|glsl|png|json|ttf)$")).OrderBy(k => k).ToList();
        Ok($"koddaki {used.Count} ceviri anahtarinin hepsi var", missing.Count == 0, string.Join(", ", missing));

        // ---------- basarimlar ----------
        var stats = AchievementData.Stats.Select(s => s.Key).ToHashSet();
        var ids = AchievementData.All.Select(a => a.Id).ToList();
        Ok($"{ids.Count} basarim", ids.Count >= 25, $"{ids.Count}");
        Ok("basarim kimlikleri benzersiz ve Steam API bicimi", ids.Distinct().Count() == ids.Count && ids.All(i => Regex.IsMatch(i, "^ACH_[A-Z0-9_]+$")));
        foreach (var a in AchievementData.All)
        {
            Ok($"{a.Id}: istatistik tanimli", stats.Contains(a.Stat), a.Stat);
            Ok($"{a.Id}: esik pozitif tamsayi", a.Threshold > 0 && a.Threshold == Math.Floor(a.Threshold));
            Ok($"{a.Id}: ikon dosyasi var", Gfx.HasResource($"icons/ach/{a.Id}.png") && Gfx.HasResource($"icons/ach/{a.Id}_locked.png"));
        }
        if (code.Length > 0)
            foreach (var s in stats)
                Ok($"istatistik kodda guncelleniyor: {s}", Regex.IsMatch(code, $@"""{s}"""));
        var maxOf = MaxOfStats();
        foreach (var a in AchievementData.All)
            if (maxOf.TryGetValue(a.Stat, out var m)) Ok($"{a.Id}: esik icerikle ulasilabilir", a.Threshold <= m, $"{a.Threshold} <= {m}");
        Ok("30 yildiz parcasi", WD.Shards.Length + WD.QuestShards.Length == WD.ShardTotal);
        Ok("8 altin tuy (Ada 1)", WD.Feathers.Length + 4 == WD.FeatherTotal);
        var allIds = G.Collectibles.Items.Select(i => i.Id).ToList();
        Ok("esya kimlikleri benzersiz", allIds.Distinct().Count() == allIds.Count, string.Join(",", allIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));

        // ---------- arazi ----------
        var T1 = new Terrain("isle1", Isle1Shape.Instance, 1337, 0, 0, 512, 256);
        bool same = true;
        for (int i = 0; i < T1.Heights.Length; i += 97) if (T1.Heights[i] != W.Isle1.Heights[i]) { same = false; break; }
        Ok("dunya uretimi deterministik", same);
        float jsRef = T1.Height(6, -112);
        Ok("arazi JS surumuyle ayni (zirve)", MathF.Abs(jsRef - 39.0685f) < 0.01f, $"{jsRef:0.0000}");

        float worst = 0;
        foreach (var (x, z) in new[] { (0f, 0f), (37.3f, -12.9f), (-120.5f, 33.1f), (6f, -112f), (104f, -58f), (-150.2f, 80.7f), (88f, 120f) })
        {
            if (!W.Physics.GroundAt(x, z, 400, out var gy, out _)) continue;
            float gh = W.Height(x, z);
            if (MathF.Abs(gy - gh) < 3) worst = MathF.Max(worst, MathF.Abs(gy - gh));
        }
        Ok("carpisma zemini arazi ile ayni", worst < 0.12f, $"en buyuk fark {worst:0.000} m");

        // ---------- ziplama fizigi vs teraslar ----------
        float g = -Move.Gravity;
        float Apex(int n) => Move.JumpVel * Move.JumpVel / (2 * g) + n * (Move.FlapVel * Move.FlapVel / (2 * g));
        int Need(float h) { int n = 0; while (Apex(n) < h + 0.3f) n++; return n; }
        Ok("zirve terasi 1: 1 tuy", Need(L.PeakCliff1) == 1, $"apex0={Apex(0):0.00} cliff={L.PeakCliff1}");
        Ok("ruzgarli kayaliklar: 2 tuy", Need(L.WindyCliff) == 2, $"apex1={Apex(1):0.00} cliff={L.WindyCliff}");
        Ok("fener zirvesi: 4 tuy", Need(L.PeakCliff2) == 4, $"apex3={Apex(3):0.00} apex4={Apex(4):0.00} cliff={L.PeakCliff2}");
        Ok("tuy ilerleyisi zirveye yetiyor", new[] { "f1", "f3", "f4" }.All(id => WD.Feathers.Any(f => f.Id == id)) && 1 + 3 >= Need(L.PeakCliff2));
        float bounceApex = Move.BounceVel * Move.BounceVel / (2 * g);
        Ok("mantar ziplamasi yuksek esyalara yetiyor", bounceApex > 4.4f, $"{bounceApex:0.00} m");

        // ---------- toplanabilirlerin yerlesimi ----------
        var bad = new List<string>();
        int count = 0;
        foreach (var it in G.Collectibles.Items)
        {
            count++;
            var p = it.Pos;
            float maxGap = it.Kind switch
            {
                "shell" or "carrot" or "tool" or "map" => 0.6f,
                _ => SpotDy(it.Id) > 2 ? 5.0f : MaxGapFor(it),
            };
            var probs = new List<string>();
            if (W.Physics.PointInside(p + new Vector3(0, 0.3f, 0))) probs.Add("carpisma icinde");
            float gap = W.Physics.GroundAt(p.X, p.Z, p.Y + 0.1f, out var hy, out _) ? p.Y - hy : 99;
            if (gap > maxGap) probs.Add($"zemin {gap:0.0} m asagida");
            float wl = W.WaterLevel(p.X, p.Z);
            if (p.Y < wl + 0.25f) probs.Add("su altinda");
            if (it.Kind == "shell" && (gap > 50 || hy < wl - 0.2f) && !W.IsFrozenWater(p.X, p.Z)) probs.Add("kabuk suda");
            if (!W.InsideBoundary(p, -5)) probs.Add("sinir disinda");
            if (probs.Count > 0) bad.Add($"{it.Id}@({p.X:0},{p.Y:0.0},{p.Z:0}): {string.Join(", ", probs)}");
        }
        Ok($"{count} esyanin yerlesimi gecerli", bad.Count == 0, "\n    " + string.Join("\n    ", bad));

        // adalilar kuru zeminde
        var npcBad = new List<string>();
        foreach (var n in G.Npcs.List)
            if (!W.Physics.GroundAt(n.Pos.X, n.Pos.Z, n.Pos.Y + 50, out var ny, out _, n.Collider) || ny < W.WaterLevel(n.Pos.X, n.Pos.Z) + 0.2f) npcBad.Add(n.Id);
        Ok("adalilar kuru zeminde", npcBad.Count == 0, string.Join(",", npcBad));

        // gizli magaraya girilebiliyor: merkezden girise (+X) dogru engel yok
        {
            var d = WD.Landmarks.Dome;
            float floor = W.Height(d.X, d.Z);
            bool clear = true;
            foreach (var h in new[] { 0.35f, 0.8f })
                foreach (var dz in new[] { -0.35f, 0f, 0.35f })
                    if (W.Physics.Raycast(new Vector3(d.X, floor + h, d.Z + dz), new Vector3(MathF.Cos(d.Yaw), 0, -MathF.Sin(d.Yaw)), 9, out _)) clear = false;
            bool roof = W.Physics.GroundAt(d.X, d.Z, floor + 30, out var ry, out _);
            Ok("gizli magaranin girisi acik ve tavani var", clear && roof && ry > floor + 2, roof ? $"tavan {ry - floor:0.0} m" : "-");
        }

        // baslangic iskelede
        bool st = W.Physics.GroundAt(WD.Start.X, WD.Start.Z, 50, out var sy, out _);
        Ok("baslangic noktasi iskele ustunde", st && MathF.Abs(sy - WD.DockDef.Y) < 0.3f, st ? sy.ToString("0.00") : "yok");

        // kayit bicimi: yaz -> oku -> ayni
        var s1 = SaveManager.NewSave();
        s1.Collected.Add("s01"); s1.Flags["finale"] = true; s1.Fish["trout"] = 2; s1.House.Add(new PlacedFurniture { Id = "chair", X = 1, Z = 2, Rot = 1 });
        Storage.Write("verify_slot", s1);
        var s2 = Storage.Read<SaveData>("verify_slot");
        Storage.Remove("verify_slot");
        Ok("kayit yaz/oku tutarli", s2 != null && s2.Collected.SequenceEqual(s1.Collected) && s2.Flag("finale") && s2.Fish["trout"] == 2 && s2.House.Count == 1 && s2.House[0].Rot == 1);

        ExtraChecks();
        try { Directory.Delete(tmp, true); } catch { /* yok say */ }
    }

    private float SpotDy(string id)
    {
        foreach (var s in WD.Shards.Concat(WD.Feathers)) if (s.Id == id) return float.IsNaN(s.Dy) ? 0 : s.Dy;
        return ExtraSpotDy(id);
    }

    private Dictionary<string, double> MaxOfStats()
    {
        var W = G.World;
        var d = new Dictionary<string, double>
        {
            ["shards_max"] = WD.Shards.Length + WD.QuestShards.Length,
            ["feathers_max"] = W.FeatherTotal,
            ["shells_max"] = WD.ShellGroups.Length * 5,
            ["species_max"] = W.AllFish.Count,
            ["regions_max"] = W.MainRegions.Count,
            ["quests_max"] = SaveData.QuestIds.Length + ExtraQuestCount(),
            ["npcs_max"] = G.Npcs.List.Count,
            ["finale"] = 1, ["peak"] = 1, ["race"] = 1, ["cave"] = 1, ["night"] = 1, ["speedrun"] = 1, ["completion"] = 100,
            ["outfits_max"] = Models.Outfits.All.Length,
        };
        ExtraMaxOf(d);
        return d;
    }

    private float MaxGapFor(Item it) => 3.2f;

    private partial IEnumerable<string> ExtraKeys();
    private partial float ExtraSpotDy(string id);
    private partial int ExtraQuestCount();
    partial void ExtraMaxOf(Dictionary<string, double> d);
    partial void ExtraChecks();
}
