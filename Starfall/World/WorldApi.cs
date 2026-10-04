using System.Numerics;
using Starfall.Core;

namespace Starfall.World;

/// <summary>
/// Oynanisin dunyaya sordugu sorular (adadan bagimsiz arayuz). Kar Adasi (Asama B)
/// buradaki listeleri genisletir.
/// </summary>
public sealed partial class GameWorld
{
    public const float BoundaryRadius = 232; // otesinde akinti oyuncuyu geri iter

    public readonly List<UpdraftDef> Updrafts = new(WD.Updrafts);
    public readonly List<FishDef> AllFish = new(WD.Fish);
    public readonly List<RegionDef> Regions = new(WD.Regions);
    public readonly HashSet<string> MainRegions = new(WD.MainRegions());
    public readonly List<string> ExtraFeatherRewards = new();
    public readonly List<NpcDef> NpcDefs = new(WD.Npcs);
    /// <summary>Serbest dolasim alanlari: (merkez, yaricap). Disinda akinti geri iter.</summary>
    public readonly List<(Vector2 C, float R)> Boundaries = new() { (Vector2.Zero, BoundaryRadius) };
    /// <summary>Adalar arasi deniz koridorlari (yalnizca teknedeyken): A-B dogru parcasi + yari genislik.</summary>
    public readonly List<(Vector2 A, Vector2 B, float W)> Corridors = new();
    public bool BoatMode;
    public const int AuroraTotal = 15;
    /// <summary>Okunabilir tabelalar (Ada 1'in koy tabelalari Game'de; buradakiler ek).</summary>
    public readonly List<(float X, float Z, string Key)> ExtraSigns = new();

    /// <summary>Tum dunyayi kur (arazi, harita, su, yapilar, bitki, bulutlar).</summary>
    public void BuildAll(bool test)
    {
        BuildTerrains();
        Physics = new Physics.CollisionWorld(Terrains);
        CollectExclusions();
        BuildTerrainMeshes();
        BuildMaps();
        BuildWaters();
        BuildIsle1Structures();
        BuildIsle2();
        BuildGlow();
        CloudSets.Add(new Render.Clouds(Scene, Vector2.Zero));
    }

    partial void BuildIsle2Impl();
    private void BuildIsle2() => BuildIsle2Impl();

    /// <summary>0 = Ada 1 iklimi, 1 = tam kar (gokyuzu/sis tonu ve kar parcaciklari icin).</summary>
    public float Snowiness(Vector3 p) => 0;

    private float BoundaryDepth(Vector2 p, out Vector2 inward)
    {
        // pozitif = sinirin ne kadar disinda; inward: iceri dogru birim vektor
        float best = float.MaxValue;
        inward = Vector2.Zero;
        foreach (var (c, r) in Boundaries)
        {
            var d = p - c;
            float l = d.Length();
            float depth = l - r;
            if (depth < best) { best = depth; inward = l > 1e-4f ? -d / l : Vector2.Zero; }
        }
        if (BoatMode)
        {
            foreach (var (a, b, w) in Corridors)
            {
                var ab = b - a;
                float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
                var q = a + ab * t;
                var d = p - q;
                float l = d.Length();
                float depth = l - w;
                if (depth < best) { best = depth; inward = l > 1e-4f ? -d / l : Vector2.Zero; }
            }
        }
        return best;
    }

    public bool InsideBoundary(Vector3 pos, float margin = 0) => BoundaryDepth(new Vector2(pos.X, pos.Z), out _) < margin;

    /// <summary>Sinir disindaysa iceri dogru akinti (m/s^2 cinsinden kaba itme).</summary>
    public Vector2 BoundaryPush(Vector3 pos)
    {
        float depth = BoundaryDepth(new Vector2(pos.X, pos.Z), out var inward);
        if (depth <= 0) return Vector2.Zero;
        return inward * (depth * 1.5f + 2);
    }

    /// <summary>Donmus gol ustu (yuruyerek gecilir, yuzulmez).</summary>
    public bool IsFrozenWater(float x, float z)
    {
        foreach (var f in FrozenLakes)
            if (MathX.Hypot(x - f.C.X, z - f.C.Y) < f.R && !IceHoleNear(x, z, out _, 0.6f)) return true;
        return false;
    }

    public readonly List<(Vector2 C, float R, float Level)> FrozenLakes = new();
    public readonly List<Vector2> IceHoles = new();

    public bool IceHoleNear(float x, float z, out Vector2 hole, float r = 1.6f)
    {
        foreach (var h in IceHoles)
            if (MathX.Hypot(x - h.X, z - h.Y) < r) { hole = h; return true; }
        hole = default;
        return false;
    }

    public bool IsSnow(float x, float z) => TerrainAt(x, z) is { } t && t.Id == "isle2";

    /// <summary>Konumun ait oldugu ada (arazi disinda: en yakin ada merkezi).</summary>
    public string IslandAt(float x, float z)
    {
        if (TerrainAt(x, z) is { } t) return t.Id;
        string best = "isle1";
        float bd = float.MaxValue;
        foreach (var tr in Terrains)
        {
            float d = MathX.Hypot(x - tr.CenterX, z - tr.CenterZ);
            if (d < bd) { bd = d; best = tr.Id; }
        }
        return best;
    }

    public List<float> CompletionParts(Core.Game g, SaveData s)
    {
        var p = g.Progress;
        var parts = new List<float>
        {
            p.ShardCount() / (float)WD.ShardTotal,
            MathF.Min(1, p.FeatherCount() / (float)WD.FeatherTotal),
            MathF.Min(1, s.ShellsTotal / 100f),
            g.Quests.Isle1Species() / (float)WD.Fish.Length,
            MathF.Min(1, g.Quests.QuestsDone() / 4f),
            s.Regions.Count(r => WD.MainRegions().Contains(r)) / (float)WD.MainRegions().Count(),
            s.Flag("finale") ? 1 : 0,
            s.Regions.Contains("cave") ? 1 : 0,
        };
        ExtendCompletion(g, s, parts);
        return parts;
    }

    partial void ExtendCompletion(Core.Game g, SaveData s, List<float> parts);

    public void RefreshExtraStats(Core.Game g, SaveData s) => ExtraStats(g, s);

    public int FeatherTotal => WD.FeatherTotal + ExtraFeatherRewards.Count + ExtraFeatherSpots;
    public int ExtraFeatherSpots;

    public void ExtraQuests(Core.Game g, List<(string Title, string Desc, string State)> list) => ExtraQuestsImpl(g, list);
    partial void ExtraQuestsImpl(Core.Game g, List<(string Title, string Desc, string State)> list);

    public void ExtraCollection(Core.Game g, List<(string Num, string Label)> list) => ExtraCollectionImpl(g, list);
    partial void ExtraCollectionImpl(Core.Game g, List<(string Num, string Label)> list);

    public void DrawMapPins(Core.Game g, UI.UiCtx c, Terrain t, Func<float, float, Vector2> toMap) => DrawMapPinsImpl(g, c, t, toMap);
    partial void DrawMapPinsImpl(Core.Game g, UI.UiCtx c, Terrain t, Func<float, float, Vector2> toMap);

    partial void ExtraStats(Core.Game g, SaveData s);
}
