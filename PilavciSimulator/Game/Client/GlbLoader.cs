using Raylib_cs;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.Client;

/// <summary>
/// <c>Assets/Models/&lt;anahtar&gt;.glb</c> (ya da .gltf/.obj) varsa yukler ve
/// oyunun aydinlatma shader'iyla cizilecek bicime cevirir. Modelin kendi
/// albedo dokusu ve rengi korunur; golge ve isiklar ayni sekilde calisir.
///
/// Olcek kurali: 1 birim = 1 metre, tabani y=0, yuzu -Z.
/// </summary>
public sealed class GlbLoader
{
    private readonly MaterialLib _mats;
    private readonly string _dir = Path.Combine(Paths.Assets, "Models");

    public GlbLoader(MaterialLib mats) => _mats = mats;

    public unsafe RenderModel? TryLoad(string key)
    {
        if (!Directory.Exists(_dir))
        {
            return null;
        }

        string? path = null;
        foreach (var ext in new[] { ".glb", ".gltf", ".obj" })
        {
            var p = Path.Combine(_dir, key + ext);
            if (File.Exists(p))
            {
                path = p;
                break;
            }
        }

        if (path is null)
        {
            return null;
        }

        try
        {
            var model = Raylib.LoadModel(path);
            if (model.MeshCount == 0)
            {
                return null;
            }

            var rm = new RenderModel();
            for (var i = 0; i < model.MeshCount; i++)
            {
                var mesh = model.Meshes[i];
                var matIndex = model.MeshMaterial[i];
                var src = model.Materials[matIndex];
                var albedo = src.Maps[(int)MaterialMapIndex.Albedo];
                var def = new MaterialDef
                {
                    Name = $"glb:{key}:{i}",
                    Texture = albedo.Texture,
                    Tint = albedo.Color,
                    Specular = 0.2f,
                };
                var id = _mats.Register(def);
                var bounds = Bounds.Empty;
                for (var v = 0; v < mesh.VertexCount; v++)
                {
                    bounds.Encapsulate(new System.Numerics.Vector3(mesh.Vertices[v * 3], mesh.Vertices[v * 3 + 1], mesh.Vertices[v * 3 + 2]));
                }

                rm.Add(mesh, id, bounds);
            }

            Log.Info($"model override yuklendi: {Path.GetFileName(path)} ({model.MeshCount} mesh)");
            return rm;
        }
        catch (Exception ex)
        {
            Log.Warn($"model yuklenemedi ({path}): {ex.Message}; prosedurel model kullaniliyor");
            return null;
        }
    }
}
