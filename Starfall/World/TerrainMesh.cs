using System.Numerics;
using Starfall.Core;
using Starfall.Render;

namespace Starfall.World;

public readonly record struct Exclusion(float X, float Z, float R);

/// <summary>Bir adanin boyanmasi: vertex rengi + cimen maskesi.</summary>
public interface IIslandPaint
{
    Vector3 Color(Terrain t, float x, float z, float h, Vector3 n, float pathDist, float water, out Vector4 mask);
}

public static class Tint
{
    public static float Gauss(float x, float z, Vector2 c, float r)
    {
        float d2 = ((x - c.X) * (x - c.X) + (z - c.Y) * (z - c.Y)) / (r * r);
        return MathF.Exp(-d2);
    }
}

/// <summary>Ada 1 boyasi: JS surumundeki TerrainMesh.js ile ayni kurallar.</summary>
public sealed class Isle1Paint : IIslandPaint
{
    public static readonly Isle1Paint Instance = new();
    private static readonly Vector3 Sand = MathX.Hex("#ead7a0"), SandWet = MathX.Hex("#cdb47c"), Grass = MathX.Hex("#79c25a"),
        GrassDark = MathX.Hex("#5aa646"), Meadow = MathX.Hex("#9fd36a"), Forest = MathX.Hex("#4f9845"), Moss = MathX.Hex("#5fae80"),
        Rock = MathX.Hex("#9c938b"), RockDark = MathX.Hex("#7f7670"), PathC = MathX.Hex("#cfae7a"), Deep = MathX.Hex("#8f7d5a"),
        Windy = MathX.Hex("#a9c96a"), PeakC = MathX.Hex("#8fcf6a");

    public static (float meadow, float forest, float hollow, float windy, float peak) Tints(float x, float z)
    {
        return (Tint.Gauss(x, z, Isle1Shape.L.Meadow, 48), Tint.Gauss(x, z, Isle1Shape.L.Forest, 52), Tint.Gauss(x, z, Isle1Shape.L.Hollow, 36),
            Tint.Gauss(x, z, Isle1Shape.L.Windy, 40), Tint.Gauss(x, z, Isle1Shape.L.Peak, 40));
    }

    public Vector3 Color(Terrain t, float x, float z, float h, Vector3 n, float pd, float wl, out Vector4 mask)
    {
        bool lake = MathF.Sqrt((x - Isle1Shape.L.Lake.X) * (x - Isle1Shape.L.Lake.X) + (z - Isle1Shape.L.Lake.Y) * (z - Isle1Shape.L.Lake.Y)) < Isle1Shape.L.LakeR + 5;
        var (meadow, forest, hollow, windy, peak) = Tints(x, z);
        float vary = (float)(t.N3.Get(x * 0.08, z * 0.08) * 0.5 + t.N3.Get(x * 0.3, z * 0.3) * 0.25);
        var c = Vector3.Lerp(Grass, GrassDark, MathX.Smoothstep(-0.3f, 0.6f, vary));
        c = Vector3.Lerp(c, Meadow, meadow * 0.8f);
        c = Vector3.Lerp(c, Forest, forest * 0.85f);
        c = Vector3.Lerp(c, Moss, hollow * 0.75f);
        c = Vector3.Lerp(c, Windy, windy * 0.6f);
        c = Vector3.Lerp(c, PeakC, peak * 0.4f);
        float beachTop = lake ? wl + 0.55f : 1.8f;
        float beach = 1 - MathX.Smoothstep(beachTop, beachTop + 0.9f, h);
        c = Vector3.Lerp(c, Sand, beach);
        if (h < wl) c = Vector3.Lerp(SandWet, Deep, MathX.Smoothstep(0, 6, wl - h));
        float rock = MathX.Smoothstep(0.82f, 0.7f, n.Y);
        c = Vector3.Lerp(c, Rock, rock);
        c = Vector3.Lerp(c, RockDark, rock * MathX.Smoothstep(0, 1, vary + 0.3f) * 0.5f);
        float path = (1 - MathX.Smoothstep(1.1f, 2.1f, pd)) * (h > wl + 0.3f ? 1 : 0) * (1 - rock);
        c = Vector3.Lerp(c, PathC, path * 0.92f);
        c *= 1 + vary * 0.08f;

        float dens = (1 - beach) * (1 - rock) * (1 - MathX.Smoothstep(0.9f, 2.6f, 3 - pd)) * (h > wl + 0.25f ? 1 : 0);
        dens *= 0.55f + 0.45f * MathX.Smoothstep(-0.4f, 0.4f, vary);
        dens = MathF.Min(1, dens * (1 + meadow * 0.4f));
        mask = new Vector4(dens, MathF.Min(1, meadow * 0.9f + windy * 0.8f), MathF.Min(1, forest + hollow * 0.7f), 1);
        return c;
    }
}

public static class TerrainMesh
{
    /// <summary>Duz golgeli arazi meshi (2 m hucre, vertex renkleri).</summary>
    public static (MeshData mesh, Vector3[] colors) Build(Terrain t, IIslandPaint paint, Func<float, float, float> waterLevel)
    {
        int V = t.Verts;
        var colors = new Vector3[V * V];
        Parallel.For(0, V, iz =>
        {
            for (int ix = 0; ix < V; ix++)
            {
                float x = t.VertexX(ix), z = t.VertexZ(iz);
                int i = iz * V + ix;
                colors[i] = paint.Color(t, x, z, t.Heights[i], t.Normal(x, z), t.PathDist[i], waterLevel(x, z), out _);
            }
        });
        int C = t.Cells;
        // tamamen derin suyun altindaki hucreleri atla (gorunmezler)
        var verts = new List<float>(C * C * 6 * MeshData.Stride);
        void Put(int ix, int iz, Vector3 n)
        {
            int i = iz * V + ix;
            verts.Add(t.VertexX(ix)); verts.Add(t.Heights[i]); verts.Add(t.VertexZ(iz));
            verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
            var c = colors[i];
            verts.Add(c.X); verts.Add(c.Y); verts.Add(c.Z);
        }
        Vector3 P(int ix, int iz) => new(t.VertexX(ix), t.Heights[iz * V + ix], t.VertexZ(iz));
        for (int iz = 0; iz < C; iz++)
        {
            for (int ix = 0; ix < C; ix++)
            {
                float hmax = MathF.Max(MathF.Max(t.Heights[iz * V + ix], t.Heights[iz * V + ix + 1]), MathF.Max(t.Heights[(iz + 1) * V + ix], t.Heights[(iz + 1) * V + ix + 1]));
                if (hmax < -8.5f) continue;
                // ucgenler: (a, d, b) ve (b, d, e); a=(ix,iz) b=(ix+1,iz) d=(ix,iz+1) e=(ix+1,iz+1)
                var a = P(ix, iz); var b = P(ix + 1, iz); var d = P(ix, iz + 1); var e = P(ix + 1, iz + 1);
                var n1 = Vector3.Normalize(Vector3.Cross(d - a, b - a));
                var n2 = Vector3.Normalize(Vector3.Cross(d - b, e - b));
                Put(ix, iz, n1); Put(ix, iz + 1, n1); Put(ix + 1, iz, n1);
                Put(ix + 1, iz, n2); Put(ix, iz + 1, n2); Put(ix + 1, iz + 1, n2);
            }
        }
        return (new MeshData(verts.ToArray()), colors);
    }
}
