using System.Numerics;
using Starfall.Core;

namespace Starfall.Render;

/// <summary>CPU tarafinda mesh verisi (pos3 + nrm3 + col3, indekssiz). GPU'ya ilk cizimde yuklenir.</summary>
public sealed class MeshData
{
    public const int Stride = 9;
    public readonly float[] Vertices;
    public readonly int VertexCount;
    public readonly Vector3 BoundsMin, BoundsMax;
    public readonly Vector3 Center;
    public readonly float Radius;
    internal GpuMesh? Gpu;

    public MeshData(float[] verts)
    {
        Vertices = verts;
        VertexCount = verts.Length / Stride;
        var mn = new Vector3(float.MaxValue);
        var mx = new Vector3(float.MinValue);
        for (int i = 0; i < VertexCount; i++)
        {
            var p = new Vector3(verts[i * Stride], verts[i * Stride + 1], verts[i * Stride + 2]);
            mn = Vector3.Min(mn, p);
            mx = Vector3.Max(mx, p);
        }
        if (VertexCount == 0) { mn = mx = Vector3.Zero; }
        BoundsMin = mn;
        BoundsMax = mx;
        Center = (mn + mx) * 0.5f;
        Radius = (mx - mn).Length() * 0.5f;
    }

    public static MeshData From(Geo g, bool flat = true)
    {
        if (flat) g.FlatNormals();
        var v = new float[g.Count * Stride];
        for (int i = 0; i < g.Count; i++)
        {
            var p = g.P[i]; var n = g.N[i]; var c = g.C[i];
            int o = i * Stride;
            v[o] = p.X; v[o + 1] = p.Y; v[o + 2] = p.Z;
            v[o + 3] = n.X; v[o + 4] = n.Y; v[o + 5] = n.Z;
            v[o + 6] = c.X; v[o + 7] = c.Y; v[o + 8] = c.Z;
        }
        return new MeshData(v);
    }
}

public enum Blend { Opaque, Alpha, Additive }

public sealed class Material
{
    public Vector3 Tint = Vector3.One;
    public Vector3 Emissive = Vector3.Zero;
    public float EmissiveIntensity = 1f;
    public float Opacity = 1f;
    public Blend Blend = Blend.Opaque;
    public bool DoubleSided;
    public bool CastShadow = true;
    public bool ReceiveShadow = true;
    public bool Unlit;
    public float Sway;           // ruzgar sallantisi (agac, cali)
    public bool DepthWrite = true;

    public static Material Std() => new();
    public static Material Char() => new();
    public static Material Glow(string emissive, float intensity) => new() { Emissive = MathX.Hex(emissive), EmissiveIntensity = intensity };
}

/// <summary>Sahne dugumu: donusum + cocuklar + istege bagli mesh. Euler sirasi three.js gibi XYZ.</summary>
public class Node
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale = Vector3.One;
    public Matrix4x4 World = Matrix4x4.Identity;
    public bool Visible = true;
    public MeshData? Mesh;
    public Material? Material;
    public readonly List<Node> Children = new();
    public Node? Parent;
    public string? Name;
    /// <summary>Elle verilen yerel matris (null degilse Position/Rotation/Scale yok sayilir).</summary>
    public Matrix4x4? LocalOverride;

    public Node() { }

    public Node(MeshData mesh, Material? mat = null)
    {
        Mesh = mesh;
        Material = mat ?? Material.Std();
    }

    public Node Add(Node child)
    {
        child.Parent?.Children.Remove(child);
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    public void Remove(Node child)
    {
        Children.Remove(child);
        child.Parent = null;
    }

    public Matrix4x4 Local =>
        LocalOverride ?? Matrix4x4.CreateScale(Scale) * MathX.EulerXYZ(Rotation.X, Rotation.Y, Rotation.Z) * Matrix4x4.CreateTranslation(Position);

    public void UpdateWorld(in Matrix4x4 parent)
    {
        World = Local * parent;
        foreach (var c in Children) c.UpdateWorld(World);
    }

    /// <summary>Yerel noktayi dunya uzayina (World guncel olmali).</summary>
    public Vector3 LocalToWorld(Vector3 p) => Vector3.Transform(p, World);

    public Vector3 WorldToLocal(Vector3 p)
    {
        Matrix4x4.Invert(World, out var inv);
        return Vector3.Transform(p, inv);
    }

    public Vector3 WorldPosition => World.Translation;
}

/// <summary>Ayni mesh'in yuzlerce kopyasi tek cizimde (agaclar, kayalar, cicekler, kabuklar).</summary>
public sealed class InstanceBatch
{
    public readonly MeshData Mesh;
    public readonly Material Material;
    public readonly List<Matrix4x4> Transforms = new();
    public readonly List<Vector4> Colors = new();
    public bool Dirty = true;
    public bool Visible = true;
    internal float[]? Packed;
    public Vector3 BoundsMin, BoundsMax;
    /// <summary>Kamera bu mesafeden uzaktaysa cizilmez (0 = sinirsiz).</summary>
    public float CullDistance;

    public InstanceBatch(MeshData mesh, Material mat)
    {
        Mesh = mesh;
        Material = mat;
    }

    public void Add(Matrix4x4 m, Vector4? color = null)
    {
        Transforms.Add(m);
        Colors.Add(color ?? Vector4.One);
        Dirty = true;
    }

    public void Set(int i, Matrix4x4 m)
    {
        Transforms[i] = m;
        Dirty = true;
    }

    internal float[] Pack()
    {
        if (!Dirty && Packed != null) return Packed;
        int n = Transforms.Count;
        if (Packed == null || Packed.Length < n * 20) Packed = new float[Math.Max(n, 1) * 20];
        var mn = new Vector3(float.MaxValue);
        var mx = new Vector3(float.MinValue);
        for (int i = 0; i < n; i++)
        {
            var m = Transforms[i];
            int o = i * 20;
            Packed[o] = m.M11; Packed[o + 1] = m.M12; Packed[o + 2] = m.M13; Packed[o + 3] = m.M14;
            Packed[o + 4] = m.M21; Packed[o + 5] = m.M22; Packed[o + 6] = m.M23; Packed[o + 7] = m.M24;
            Packed[o + 8] = m.M31; Packed[o + 9] = m.M32; Packed[o + 10] = m.M33; Packed[o + 11] = m.M34;
            Packed[o + 12] = m.M41; Packed[o + 13] = m.M42; Packed[o + 14] = m.M43; Packed[o + 15] = m.M44;
            var c = Colors[i];
            Packed[o + 16] = c.X; Packed[o + 17] = c.Y; Packed[o + 18] = c.Z; Packed[o + 19] = c.W;
            var t = m.Translation;
            mn = Vector3.Min(mn, t);
            mx = Vector3.Max(mx, t);
        }
        BoundsMin = mn - new Vector3(Mesh.Radius * 3);
        BoundsMax = mx + new Vector3(Mesh.Radius * 3);
        return Packed;
    }
}

/// <summary>Nokta isigi (kamp atesi, magara kristali, fener lambasi). Fizik tabanli dusus (three.js ile ayni).</summary>
public sealed class PointLight
{
    public Vector3 Position;
    public Vector3 Color = Vector3.One;
    public float Intensity;
    public float Distance;
    public float Decay = 2f;
}

/// <summary>Tum sahne: kok dugum + toplu cizimler + isiklar + ozel cizilebilirler.</summary>
public sealed class Scene
{
    public readonly Node Root = new() { Name = "root" };
    public readonly List<InstanceBatch> Batches = new();
    public readonly List<PointLight> Lights = new();

    public Node Add(Node n) => Root.Add(n);

    public InstanceBatch AddBatch(InstanceBatch b)
    {
        Batches.Add(b);
        return b;
    }
}
