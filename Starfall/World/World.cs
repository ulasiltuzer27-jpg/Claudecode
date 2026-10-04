using System.Numerics;
using Starfall.Core;
using Starfall.Render;

namespace Starfall.World;

/// <summary>
/// Dunya: adalarin yukseklik haritalari, kuresel zemin haritasi (su/cimen
/// shader'lari icin), su yuzeyleri. Yapilar, bitki ortusu ve carpisma
/// WorldBuild.cs'de kurulur.
/// </summary>
public sealed partial class GameWorld
{
    public const float SeaLevel = 0f;
    // kuresel harita: iki adayi da kapsar
    public const float MapOriginX = -260, MapOriginZ = -800, MapSize = 1160;
    public const int MapRes = 580;

    public readonly List<Terrain> Terrains = new();
    public Terrain Isle1 = null!, Isle2 = null!;
    public readonly Scene Scene;
    public readonly RenderEnv Env;
    public GroundMaps Maps = null!;
    public readonly List<Exclusion> Exclusions = new();
    public float Time;

    public GameWorld(RenderEnv env)
    {
        Env = env;
        Scene = env.Scene;
    }

    public float Height(float x, float z)
    {
        foreach (var t in Terrains) if (t.Contains(x, z)) return t.Height(x, z);
        return -9f;
    }

    public float HeightTri(float x, float z)
    {
        foreach (var t in Terrains) if (t.Contains(x, z)) return t.HeightTri(x, z);
        return -9f;
    }

    public Vector3 Normal(float x, float z)
    {
        foreach (var t in Terrains) if (t.Contains(x, z)) return t.Normal(x, z);
        return Vector3.UnitY;
    }

    public Terrain? TerrainAt(float x, float z)
    {
        foreach (var t in Terrains) if (t.Contains(x, z)) return t;
        return null;
    }

    public bool IsLake(float x, float z)
    {
        var L = Isle1Shape.L.Lake;
        return MathX.Hypot(x - L.X, z - L.Y) < Isle1Shape.L.LakeR + 3;
    }

    public float WaterLevel(float x, float z)
    {
        if (IsLake(x, z)) return Isle1Shape.L.LakeLevel;
        if (Terrains.Count > 1)
        {
            if (MathX.Hypot(x - L2.Lake.X, z - L2.Lake.Y) < L2.LakeR + 3) return L2.LakeLevel;
            foreach (var p in L2.Pools)
                if (MathX.Hypot(x - p.X, z - p.Y) < L2.PoolR + 0.8f) return L2.SpringLevel;
        }
        return SeaLevel;
    }

    public bool InHotSpring(float x, float z)
    {
        foreach (var p in L2.Pools) if (MathX.Hypot(x - p.X, z - p.Y) < L2.PoolR) return true;
        return false;
    }

    public float PathDistance(float x, float z) => TerrainAt(x, z)?.PathDistance(x, z) ?? 999f;

    public bool IsExcluded(float x, float z, float pad = 0)
    {
        foreach (var e in Exclusions)
            if ((x - e.X) * (x - e.X) + (z - e.Z) * (z - e.Z) < (e.R + pad) * (e.R + pad)) return true;
        return false;
    }

    public void BuildTerrains()
    {
        Isle1 = new Terrain("isle1", Isle1Shape.Instance, 1337, 0, 0, 512, 256);
        Terrains.Add(Isle1);
        Isle2 = new Terrain("isle2", Isle2Shape.Instance, 4242, L2.C.X, L2.C.Y, L2.Size, (int)(L2.Size / 2));
        Terrains.Add(Isle2);
    }

    public void BuildTerrainMeshes()
    {
        foreach (var t in Terrains)
        {
            var paint = PaintFor(t);
            var (mesh, colors) = TerrainMesh.Build(t, paint, WaterLevel);
            TerrainColors[t.Id] = colors;
            var node = new Node(mesh, new Material { DoubleSided = false }) { Name = $"terrain:{t.Id}" };
            Scene.Add(node);
        }
    }

    public readonly Dictionary<string, Vector3[]> TerrainColors = new();

    public IIslandPaint PaintFor(Terrain t) => t.Id switch
    {
        "isle2" => Isle2Paint.Instance,
        _ => Isle1Paint.Instance,
    };

    /// <summary>Kuresel yukseklik + cimen maskesi (su derinligi, kiyi kopugu, cimen yogunlugu).</summary>
    public void BuildMaps()
    {
        int R = MapRes;
        var m = new GroundMaps { OriginX = MapOriginX, OriginZ = MapOriginZ, Size = MapSize, Res = R, Heights = new float[R * R], Mask = new byte[R * R * 4] };
        float cell = MapSize / (R - 1);
        Parallel.For(0, R, iz =>
        {
            for (int ix = 0; ix < R; ix++)
            {
                float x = MapOriginX + ix * cell, z = MapOriginZ + iz * cell;
                int i = iz * R + ix;
                var t = TerrainAt(x, z);
                float h = t?.Height(x, z) ?? -9f;
                m.Heights[i] = h;
                if (t == null) continue;
                PaintFor(t).Color(t, x, z, h, t.Normal(x, z), t.PathDistance(x, z), WaterLevel(x, z), out var mask);
                float dens = mask.X;
                foreach (var e in Exclusions)
                {
                    float d = MathX.Hypot(x - e.X, z - e.Z);
                    if (d < e.R + 2) dens *= MathX.Smoothstep(e.R, e.R + 2, d);
                }
                m.Mask[i * 4] = (byte)(MathX.Clamp(dens, 0, 1) * 255);
                m.Mask[i * 4 + 1] = (byte)(MathX.Clamp(mask.Y, 0, 1) * 255);
                m.Mask[i * 4 + 2] = (byte)(MathX.Clamp(mask.Z, 0, 1) * 255);
                m.Mask[i * 4 + 3] = (byte)(MathX.Clamp(mask.W, 0, 1) * 255);
            }
        });
        Maps = m;
        Env.Maps = m;
    }

    public void BuildWaters()
    {
        // deniz: iki adayi da kapsayan buyuk duzlem
        Env.Waters.Add(new WaterSurface
        {
            Mesh = MeshData.From(Plane(2600, 2600, 260), false),
            Position = new Vector3(300, 0, -280),
            Level = SeaLevel,
            Amp = 0.14f,
        });
        var lake = Isle1Shape.L.Lake;
        Env.Waters.Add(new WaterSurface
        {
            Mesh = MeshData.From(Geo.CircleGeo(Isle1Shape.L.LakeR + 4, 64).RotateX(-MathX.Pi / 2), false),
            Position = new Vector3(lake.X, 0, lake.Y),
            Level = Isle1Shape.L.LakeLevel,
            Amp = 0.04f,
            Shallow = MathX.Hex("#5fe0d0"),
            Deep = MathX.Hex("#2a7fb8"),
        });
    }

    public static Geo Plane(float w, float h, int seg)
    {
        var g = new Geo();
        float s = w / seg;
        for (int iz = 0; iz < seg; iz++)
        {
            for (int ix = 0; ix < seg; ix++)
            {
                float x0 = -w / 2 + ix * s, z0 = -h / 2 + iz * s;
                var a = new Vector3(x0, 0, z0); var b = new Vector3(x0 + s, 0, z0);
                var c = new Vector3(x0, 0, z0 + s); var d = new Vector3(x0 + s, 0, z0 + s);
                g.P.AddRange(new[] { a, c, b, b, c, d });
                for (int k = 0; k < 6; k++) { g.N.Add(Vector3.UnitY); g.C.Add(Vector3.One); }
            }
        }
        return g;
    }
}
