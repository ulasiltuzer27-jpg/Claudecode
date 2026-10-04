using System.Numerics;
using Starfall.Core;
using Starfall.Models;
using Starfall.Physics;
using Starfall.Render;
using Starfall.World;

namespace Starfall.Gameplay;

public sealed class Npc
{
    public string Id = "";
    public NpcDef Def = null!;
    public AnimalModel Model = null!;
    public Vector3 Pos;
    public float Yaw, BaseYaw;
    public Billboard Marker = null!;
    public Shape? Collider;
    public bool Talking, Racing, Hidden;
    public float Voice => Def.Voice;
}

/// <summary>Adalilarin sahnedeki halleri: model, basinin ustundeki gorev isareti, oyuncuya bakma.</summary>
public sealed class Npcs
{
    private static readonly Dictionary<string, float> MarkerH = new()
    {
        ["owl"] = 1.75f, ["hedgehog"] = 1.2f, ["frog"] = 1.0f, ["bear"] = 2.1f, ["rabbit"] = 1.55f, ["beaver"] = 1.55f,
        ["penguin"] = 1.4f, ["seal"] = 1.45f, ["goat"] = 1.55f, ["mole"] = 1.25f, ["cat"] = 1.35f, ["polarbear"] = 2.2f,
    };

    private readonly Game _g;
    public readonly List<Npc> List = new();
    public readonly Dictionary<string, Npc> ById = new();
    private static readonly Dictionary<string, CpuImage> MarkerImg = new();
    private float _t;

    public Npcs(Game g) => _g = g;

    public static CpuImage MarkerImage(string sym)
    {
        if (MarkerImg.TryGetValue(sym, out var img)) return img;
        string col = sym switch { "!" => "#ffb627", "?" => "#4fb4ff", "★" => "#b98aff", _ => "#5dd6a8" };
        return MarkerImg[sym] = ImageGen.Marker(sym, col);
    }

    public void Build(IEnumerable<NpcDef> defs)
    {
        int seed = 0;
        foreach (var def in defs)
        {
            if (def.Id == "owlPeak" || def.Id.EndsWith("Alt")) continue;
            var model = new AnimalModel(def.Kind, seed++);
            var npc = new Npc { Id = def.Id, Def = def, Model = model, Yaw = def.Yaw, BaseYaw = def.Yaw };
            npc.Marker = new Billboard { Size = new Vector2(0.55f), Additive = false, Visible = false };
            _g.Env.Billboards.Add(npc.Marker);
            _g.Env.Scene.Add(model.Root);
            List.Add(npc);
            ById[def.Id] = npc;
            PlaceAt(npc, def.X, def.Z, def.Yaw);
            // basit carpisma: oyuncu adalilarin icinden gecmesin
            float r = def.Kind is "bear" or "polarbear" ? 0.6f : 0.4f;
            npc.Collider = _g.World.Physics.Add(ShapeDef.Cyl(r, 0.6f, new Vector3(0, 0.6f, 0)), npc.Pos, 0, "npc");
        }
    }

    public void PlaceAt(Npc npc, float x, float z, float yaw)
    {
        float y = _g.World.Physics.GroundAt(x, z, 150, out float gy, out _, npc.Collider) ? gy : _g.World.Height(x, z);
        npc.Pos = new Vector3(x, y, z);
        npc.Model.Root.Position = npc.Pos;
        npc.Yaw = yaw;
        npc.BaseYaw = yaw;
        npc.Model.Root.Rotation = new Vector3(0, yaw, 0);
        if (npc.Collider != null) _g.World.Physics.Move(npc.Collider, new Vector3(x, y + 0.6f, z));
    }

    public void MoveOwlToPeak()
    {
        var d = WD.Npc("owlPeak");
        PlaceAt(ById["owl"], d.X, d.Z, d.Yaw);
    }

    public void Update(float dt, Player player)
    {
        _t += dt;
        var quests = _g.Quests;
        foreach (var npc in List)
        {
            float d2 = Vector3.DistanceSquared(npc.Pos, player.Pos);
            if (d2 > 140 * 140)
            {
                npc.Model.Root.Visible = false;
                npc.Marker.Visible = false;
                continue;
            }
            npc.Model.Root.Visible = !npc.Hidden;
            bool near = d2 < 7 * 7;
            npc.Model.LookTarget = near ? player.Pos + new Vector3(0, 0.6f, 0) : null;
            // konusurken govdesiyle de don
            if (npc.Talking)
            {
                float want = MathF.Atan2(player.Pos.X - npc.Pos.X, player.Pos.Z - npc.Pos.Z);
                npc.Yaw += MathX.WrapAngle(want - npc.Yaw) * MathF.Min(1, dt * 4);
            }
            else if (!npc.Racing) npc.Yaw += MathX.WrapAngle(npc.BaseYaw - npc.Yaw) * MathF.Min(1, dt * 1.5f);
            npc.Model.Root.Rotation = new Vector3(0, npc.Yaw, 0);
            npc.Model.Talking = npc.Talking ? 1 : 0;
            npc.Model.Root.UpdateWorld(Matrix4x4.Identity);
            npc.Model.Update(dt);
            var mk = quests?.MarkerFor(npc.Id);
            float h = MarkerH.TryGetValue(npc.Def.Kind, out var mh) ? mh : 1.5f;
            if (mk != null && !npc.Hidden)
            {
                npc.Marker.Visible = true;
                npc.Marker.Image = MarkerImage(mk);
                npc.Marker.Position = npc.Model.Root.Position + new Vector3(0, h + MathF.Sin(_t * 3.3f) * 0.06f, 0);
            }
            else npc.Marker.Visible = false;
        }
    }

    public Npc? Nearest(Vector3 pos, float maxDist = 2.6f)
    {
        Npc? best = null;
        float bd = maxDist * maxDist;
        foreach (var npc in List)
        {
            if (npc.Hidden || npc.Racing) continue;
            float d = Vector3.DistanceSquared(npc.Pos, pos);
            if (d < bd) { bd = d; best = npc; }
        }
        return best;
    }
}
