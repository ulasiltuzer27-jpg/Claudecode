using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// CPU tarafinda mesh insasi. Prosedurel modellerin ve statik dunyanin
/// hepsi buradan gecer; <see cref="Upload"/> ile GPU'ya yuklenir.
///
/// raylib indeksleri 16 bit (<c>ushort</c>): tek bir mesh en fazla 65535
/// kose tasiyabilir. Buyuk statik geometri <see cref="UploadSplit"/> ile
/// parcalanir.
/// </summary>
public sealed class MeshData
{
    public const int MaxVertices = 65535;

    public readonly List<Vector3> Positions = new();
    public readonly List<Vector3> Normals = new();
    public readonly List<Vector2> Uvs = new();
    public readonly List<Color> Colors = new();
    public readonly List<int> Indices = new();

    public int VertexCount => Positions.Count;
    public bool IsEmpty => Indices.Count == 0;

    public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Color color)
    {
        Positions.Add(position);
        Normals.Add(normal);
        Uvs.Add(uv);
        Colors.Add(color);
        return Positions.Count - 1;
    }

    public void AddTriangle(int a, int b, int c)
    {
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    public void AddQuad(int a, int b, int c, int d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    /// <summary>Baska bir mesh'i donusumle ekler. Normaller ters-devrik ile cevrilir.</summary>
    public void Append(MeshData other, in Matrix4x4 transform, Color? tint = null)
    {
        var baseIndex = Positions.Count;
        Matrix4x4.Invert(transform, out var inv);
        var normalMatrix = Matrix4x4.Transpose(inv);
        for (var i = 0; i < other.Positions.Count; i++)
        {
            Positions.Add(Vector3.Transform(other.Positions[i], transform));
            Normals.Add(Vector3.Normalize(Vector3.TransformNormal(other.Normals[i], normalMatrix)));
            Uvs.Add(other.Uvs[i]);
            var c = other.Colors[i];
            if (tint is { } t)
            {
                c = new Color((byte)(c.R * t.R / 255), (byte)(c.G * t.G / 255), (byte)(c.B * t.B / 255), c.A);
            }

            Colors.Add(c);
        }

        foreach (var idx in other.Indices)
        {
            Indices.Add(baseIndex + idx);
        }
    }

    public Bounds ComputeBounds()
    {
        var b = Bounds.Empty;
        foreach (var p in Positions)
        {
            b.Encapsulate(p);
        }

        return b;
    }

    public void Clear()
    {
        Positions.Clear();
        Normals.Clear();
        Uvs.Clear();
        Colors.Clear();
        Indices.Clear();
    }

    /// <summary>GPU'ya yukler. Kose sayisi 65535'i asarsa istisna.</summary>
    public unsafe Mesh Upload(bool dynamic = false)
    {
        if (Positions.Count > MaxVertices)
        {
            throw new InvalidOperationException($"mesh {Positions.Count} kose tasiyor; UploadSplit kullanin");
        }

        var mesh = new Mesh
        {
            VertexCount = Positions.Count,
            TriangleCount = Indices.Count / 3,
        };

        // raylib UnloadMesh bu dizileri kendi free'si ile birakir; bu yuzden
        // raylib'in ayiricisi (MemAlloc) kullaniliyor, Marshal degil.
        mesh.Vertices = (float*)Raylib.MemAlloc((uint)(Positions.Count * 3 * sizeof(float)));
        mesh.Normals = (float*)Raylib.MemAlloc((uint)(Positions.Count * 3 * sizeof(float)));
        mesh.TexCoords = (float*)Raylib.MemAlloc((uint)(Positions.Count * 2 * sizeof(float)));
        mesh.Colors = (byte*)Raylib.MemAlloc((uint)(Positions.Count * 4));
        mesh.Indices = (ushort*)Raylib.MemAlloc((uint)(Indices.Count * sizeof(ushort)));

        for (var i = 0; i < Positions.Count; i++)
        {
            var p = Positions[i];
            var n = Normals[i];
            var uv = Uvs[i];
            var c = Colors[i];
            mesh.Vertices[i * 3 + 0] = p.X;
            mesh.Vertices[i * 3 + 1] = p.Y;
            mesh.Vertices[i * 3 + 2] = p.Z;
            mesh.Normals[i * 3 + 0] = n.X;
            mesh.Normals[i * 3 + 1] = n.Y;
            mesh.Normals[i * 3 + 2] = n.Z;
            mesh.TexCoords[i * 2 + 0] = uv.X;
            mesh.TexCoords[i * 2 + 1] = uv.Y;
            mesh.Colors[i * 4 + 0] = c.R;
            mesh.Colors[i * 4 + 1] = c.G;
            mesh.Colors[i * 4 + 2] = c.B;
            mesh.Colors[i * 4 + 3] = c.A;
        }

        for (var i = 0; i < Indices.Count; i++)
        {
            mesh.Indices[i] = (ushort)Indices[i];
        }

        Raylib.UploadMesh(ref mesh, dynamic);
        return mesh;
    }

    /// <summary>65535 kose sinirini asan geometriyi ucgen sirasina gore parcalara bolup yukler.</summary>
    public List<Mesh> UploadSplit()
    {
        var result = new List<Mesh>();
        if (Positions.Count <= MaxVertices)
        {
            if (!IsEmpty)
            {
                result.Add(Upload());
            }

            return result;
        }

        var part = new MeshData();
        var remap = new Dictionary<int, int>();
        Span<int> tri = stackalloc int[3];
        for (var t = 0; t < Indices.Count; t += 3)
        {
            if (part.VertexCount + 3 > MaxVertices)
            {
                result.Add(part.Upload());
                part = new MeshData();
                remap.Clear();
            }

            for (var k = 0; k < 3; k++)
            {
                var src = Indices[t + k];
                if (!remap.TryGetValue(src, out var dst))
                {
                    dst = part.AddVertex(Positions[src], Normals[src], Uvs[src], Colors[src]);
                    remap[src] = dst;
                }

                tri[k] = dst;
            }

            part.AddTriangle(tri[0], tri[1], tri[2]);
        }

        if (!part.IsEmpty)
        {
            result.Add(part.Upload());
        }

        return result;
    }
}
