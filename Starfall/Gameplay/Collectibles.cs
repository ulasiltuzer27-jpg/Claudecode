using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Render;
using Starfall.World;

namespace Starfall.Gameplay;

public sealed class Item
{
    public string Kind = "", Id = "";
    public string? Sub;
    public Vector3 Pos;
    public Node? Node;
    public Billboard? Glow;
    public bool Taken;
    public float Phase, Yaw;
    public int Index = -1;
    public bool Hidden;          // or. hazine sandigi kazilmadan gorunmez
}

/// <summary>
/// Dunyadaki toplanabilirler: yildiz parcalari, altin tuyler, deniz kabuklari, havuclar,
/// kunduzun aletleri (+ Asama B: aurora kristalleri, hazine haritalari, yelken).
/// Konumlar WorldData'dan, "toplandi mi" bilgisi kayit dosyasindan gelir.
/// </summary>
public sealed class Collectibles
{
    private static readonly Dictionary<string, float> PickR = new()
    {
        ["shard"] = 1.3f, ["feather"] = 1.3f, ["shell"] = 1.0f, ["carrot"] = 1.1f, ["tool"] = 1.1f,
        ["aurora"] = 1.3f, ["map"] = 1.1f, ["photo"] = 0, ["chest"] = 1.2f,
    };

    private readonly Game _g;
    public readonly List<Item> Items = new();
    public readonly Dictionary<string, Item> ById = new();
    private InstanceBatch? _shellBatch;
    private float _t, _sparkT;
    private readonly Random _r = new(17);

    public Collectibles(Game g) => _g = g;

    public Vector3 Resolve(Spot def, float defaultDy = 1.0f)
    {
        var W = _g.World;
        if (def.On != null)
        {
            if (!W.Anchors.TryGetValue(def.On, out var a)) throw new InvalidOperationException($"Bilinmeyen capa: {def.On} ({def.Id})");
            if (!float.IsNaN(def.Dy) && def.X == 0 && def.Z == 0) a.Y += def.Dy - 0.9f;
            return a;
        }
        float ground = W.Physics.GroundAt(def.X, def.Z, 120, out float gy, out _) ? gy : W.Height(def.X, def.Z);
        float y = MathF.Max(ground, W.WaterLevel(def.X, def.Z)) + (float.IsNaN(def.Dy) ? defaultDy : def.Dy);
        return new Vector3(def.X, y, def.Z);
    }

    public void Build()
    {
        foreach (var s in WD.Shards) Add("shard", s.Id, Resolve(s));
        foreach (var f in WD.Feathers) Add("feather", f.Id, Resolve(f));
        foreach (var c in WD.Carrots) Add("carrot", c.Id, Resolve(c with { Dy = 0.05f }));
        foreach (var t in WD.Tools) Add("tool", t.Id, Resolve(t with { Dy = 0.05f }), t.Kind);

        // kabuklar: tek InstanceBatch (100 adet)
        _shellBatch = new InstanceBatch(CollectibleModels.Shell, new Material());
        _g.Env.Scene.AddBatch(_shellBatch);
        for (int gi = 0; gi < WD.ShellGroups.Length; gi++)
        {
            var (cx, cz, deg) = WD.ShellGroups[gi];
            float a = deg * MathX.Pi / 180f;
            for (int k = 0; k < 5; k++)
            {
                float off = (k - 2) * 1.7f;
                float x = cx + MathF.Cos(a) * off;
                float z = cz + MathF.Sin(a) * off;
                string id = $"sh_{gi * 5 + k:00}";
                var it = new Item { Kind = "shell", Id = id, Pos = Resolve(new Spot(id, x, z, 0.02f)), Yaw = a + k, Index = _shellBatch.Transforms.Count };
                _shellBatch.Add(Matrix4x4.Identity);
                Items.Add(it);
                ById[id] = it;
            }
        }
        RefreshShells();
    }

    public Item Add(string kind, string id, Vector3 pos, string? sub = null)
    {
        Node node;
        Billboard? glow = null;
        switch (kind)
        {
            case "shard":
                node = new Node(CollectibleModels.Star, CollectibleModels.StarMat);
                glow = new Billboard { Size = new Vector2(2.2f), Color = new Vector4(MathX.Hex("#ffd76a") * 0.55f, 1) };
                break;
            case "feather":
                node = new Node(CollectibleModels.Feather, CollectibleModels.FeatherMat) { Scale = new Vector3(0.9f) };
                glow = new Billboard { Size = new Vector2(2.0f), Color = new Vector4(MathX.Hex("#ffcf4a") * 0.45f, 1) };
                break;
            case "aurora":
                node = new Node(CollectibleModels.Aurora, CollectibleModels.AuroraMat);
                glow = new Billboard { Size = new Vector2(2.4f), Color = new Vector4(MathX.Hex("#5dffc8") * 0.5f, 1) };
                break;
            case "carrot":
                node = new Node(CollectibleModels.Carrot, new Material()) { Rotation = new Vector3(0, 0, 0.5f) };
                break;
            case "map":
                node = new Node(CollectibleModels.TreasureMap, new Material()) { Rotation = new Vector3(0, 0, 0.3f) };
                glow = new Billboard { Size = new Vector2(1.4f), Color = new Vector4(MathX.Hex("#ffe6a0") * 0.3f, 1) };
                break;
            default: // tool
                node = new Node(CollectibleModels.Tool(sub ?? "shovel"), new Material()) { Rotation = new Vector3(0, 0, MathX.Pi / 2.3f) };
                break;
        }
        node.Position = pos;
        _g.Env.Scene.Add(node);
        if (glow != null)
        {
            glow.Position = pos;
            _g.Env.Billboards.Add(glow);
        }
        var it = new Item { Kind = kind, Id = id, Pos = pos, Node = node, Glow = glow, Phase = (float)_r.NextDouble() * 6, Sub = sub };
        Items.Add(it);
        ById[id] = it;
        return it;
    }

    public void ApplySave(SaveData save)
    {
        var taken = save.Collected.ToHashSet();
        foreach (var it in Items)
        {
            it.Taken = taken.Contains(it.Id);
            SetVisible(it, !it.Taken && !it.Hidden);
        }
        RefreshShells();
    }

    private static void SetVisible(Item it, bool v)
    {
        if (it.Node != null) it.Node.Visible = v;
        if (it.Glow != null) it.Glow.Visible = v;
    }

    public void Reveal(string id)
    {
        if (!ById.TryGetValue(id, out var it)) return;
        it.Hidden = false;
        SetVisible(it, !it.Taken);
    }

    public void RefreshShells()
    {
        if (_shellBatch == null) return;
        foreach (var it in Items)
        {
            if (it.Kind != "shell") continue;
            _shellBatch.Set(it.Index, it.Taken ? Matrix4x4.CreateScale(0) * Matrix4x4.CreateTranslation(it.Pos) : MathX.TRS(it.Pos, it.Yaw, 1));
        }
    }

    public void Update(float dt, Player player)
    {
        _t += dt;
        float t = _t;
        var p = player.Pos;
        var center = new Vector3(p.X, p.Y + 0.5f, p.Z);
        bool shellsChanged = false;
        foreach (var it in Items)
        {
            if (it.Taken || it.Hidden) continue;
            if (it.Node != null)
            {
                float bob = MathF.Sin(t * 2 + it.Phase) * 0.15f;
                var np = it.Pos; np.Y += bob;
                it.Node.Position = np;
                var r = it.Node.Rotation;
                if (it.Kind == "shard" || it.Kind == "aurora") r.Y = t * 1.6f + it.Phase;
                else if (it.Kind == "feather") { r.Y = t * 1.2f + it.Phase; r.Z = MathF.Sin(t * 1.5f + it.Phase) * 0.3f; }
                else r.Y = t * 0.8f + it.Phase;
                it.Node.Rotation = r;
                if (it.Glow != null) it.Glow.Position = np;
            }
            float dx = it.Pos.X - center.X, dy = it.Pos.Y - center.Y, dz = it.Pos.Z - center.Z;
            float pr = PickR.TryGetValue(it.Kind, out var v) ? v : 1.1f;
            if (pr <= 0) continue;
            float rr = pr + (it.Kind == "shell" ? 0 : 0.2f);
            if (dx * dx + dz * dz < rr * rr && MathF.Abs(dy) < rr + 0.6f)
            {
                it.Taken = true;
                SetVisible(it, false);
                if (it.Kind == "shell") shellsChanged = true;
                _g.OnCollect(it);
            }
        }
        if (shellsChanged) RefreshShells();

        // yakin yildizlar hafif parildasin
        _sparkT -= dt;
        if (_sparkT <= 0)
        {
            _sparkT = 0.25f;
            foreach (var it in Items)
            {
                if (it.Taken || it.Hidden || (it.Kind != "shard" && it.Kind != "feather" && it.Kind != "aurora")) continue;
                if (Vector3.DistanceSquared(it.Pos, p) > 45 * 45) continue;
                var cols = it.Kind == "aurora" ? new[] { "#b8ffe9", "#8fe9ff" } : new[] { "#fff6b0", "#ffd76a" };
                _g.Env.Additive.Emit(Emit.At(it.Node!.Position, 1, cols).Sp(0.5f).V(0, 0.6f, 0, 0.2f).L(1.2f).S(0.18f).A(0.9f));
            }
        }
    }

    public Item? NearestUntaken(string kind, Vector3 from)
    {
        Item? best = null;
        float bd = float.MaxValue;
        foreach (var it in Items)
        {
            if (it.Taken || it.Hidden || it.Kind != kind) continue;
            float d = Vector3.DistanceSquared(it.Pos, from);
            if (d < bd) { bd = d; best = it; }
        }
        return best;
    }
}
