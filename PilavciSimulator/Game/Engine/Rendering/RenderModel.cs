using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>GPU'daki bir mesh + malzemesi.</summary>
public readonly record struct ModelPart(Mesh Mesh, int Material, Bounds Bounds);

/// <summary>
/// Birden cok malzemeli, tek parca cizilen model. Prosedurel modellerin
/// hepsi bu bicimde; <c>Assets/Models/*.glb</c> override'lari da bu bicime
/// cevrilir (bkz. ModelLibrary).
/// </summary>
public sealed class RenderModel
{
    public List<ModelPart> Parts { get; } = new();
    public Bounds Bounds = Bounds.Empty;

    public void Add(Mesh mesh, int material, Bounds bounds)
    {
        Parts.Add(new ModelPart(mesh, material, bounds));
        Bounds.Encapsulate(bounds);
    }
}

/// <summary>Malzeme basina bir MeshData toplayip tek seferde RenderModel uretir.</summary>
public sealed class ModelBuilder
{
    private readonly Dictionary<int, MeshData> _parts = new();

    public MeshData this[int material]
    {
        get
        {
            if (!_parts.TryGetValue(material, out var m))
            {
                m = new MeshData();
                _parts[material] = m;
            }

            return m;
        }
    }

    public RenderModel Build()
    {
        var model = new RenderModel();
        foreach (var (mat, data) in _parts)
        {
            if (data.IsEmpty)
            {
                continue;
            }

            var bounds = data.ComputeBounds();
            foreach (var mesh in data.UploadSplit())
            {
                model.Add(mesh, mat, bounds);
            }
        }

        return model;
    }
}

/// <summary>
/// Statik dunya: malzeme ve 24 m'lik hucre basina birlestirilmis mesh'ler.
/// Her parca kendi sinir kutusuyla gorus konisine gore elenir.
/// </summary>
public sealed class StaticScene
{
    public const float CellSize = 24f;

    public readonly List<ModelPart> Chunks = new();

    public sealed class Builder
    {
        private readonly Dictionary<(int Mat, int Cx, int Cz), MeshData> _cells = new();

        /// <summary>Verilen noktanin hucresindeki, verilen malzemeye ait mesh.</summary>
        public MeshData At(int material, Vector3 position)
        {
            var key = (material, (int)MathF.Floor(position.X / CellSize), (int)MathF.Floor(position.Z / CellSize));
            if (!_cells.TryGetValue(key, out var m))
            {
                m = new MeshData();
                _cells[key] = m;
            }

            return m;
        }

        public StaticScene Build()
        {
            var scene = new StaticScene();
            foreach (var ((mat, _, _), data) in _cells)
            {
                if (data.IsEmpty)
                {
                    continue;
                }

                var bounds = data.ComputeBounds();
                foreach (var mesh in data.UploadSplit())
                {
                    scene.Chunks.Add(new ModelPart(mesh, mat, bounds));
                }
            }

            return scene;
        }
    }
}
