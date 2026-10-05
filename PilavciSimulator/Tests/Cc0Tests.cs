using System.Numerics;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.World;
using Xunit;

namespace PilavciSimulator.Tests;

/// <summary>
/// CC0 hatti: glTF okuyucu gercek Kenney dosyalarini okuyabilmeli,
/// normalizasyon modeli tabana/merkeze oturtup olceklemeli ve dunya
/// (carpisma, yol grafigi) hazir modeller olsa da olmasa da ayni kalmali.
/// </summary>
public class Cc0Tests
{
    private static string Cc0Dir => Path.Combine(TestPaths.GameDir, "Assets", "Models", "cc0");

    [Fact]
    public void Reads_real_kenney_glb_with_texture()
    {
        var model = GltfReader.Load(Path.Combine(Cc0Dir, "basic", "tree.glb"));
        Assert.NotEmpty(model.Primitives);
        Assert.True(model.Primitives.Sum(p => p.Mesh.Indices.Count) / 3 > 100);
        Assert.Contains(model.Materials, m => m.ImageBytes is { Length: > 0 });
        var b = model.ComputeBounds();
        Assert.InRange(b.Max.Y - b.Min.Y, 1.8f, 2.0f);
    }

    [Fact]
    public void Normalize_sits_on_ground_centered_and_scaled()
    {
        var model = GltfReader.Load(Path.Combine(Cc0Dir, "racing", "vehicle-truck-green.glb"));
        var entry = new Cc0Entry { Key = "t", Group = "pickup", Yaw = 90, Fit = new Cc0Fit { Axis = "x", Size = 4.6f } };
        var asset = Cc0Catalog.Normalize(model, entry);
        var b = asset.Bounds;
        Assert.Equal(0f, b.Min.Y, 3);
        Assert.Equal(4.6f, b.Max.X - b.Min.X, 2);
        Assert.InRange(MathF.Abs(b.Min.X + b.Max.X), 0f, 0.01f);
        Assert.InRange(MathF.Abs(b.Min.Z + b.Max.Z), 0f, 0.01f);
        // Kamyonet yaw=90 ile +X'e bakar: genislik (Z) uzunluktan kisa
        Assert.True(b.Max.Z - b.Min.Z < b.Max.X - b.Min.X);
        // Opak modelde kose alfasi her zaman 255 (gece pencere parlamasi olmasin)
        Assert.All(asset.Parts.SelectMany(p => p.Mesh.Colors), c => Assert.Equal(255, c.A));
    }

    [Fact]
    public void Manifest_lists_every_imported_file()
    {
        var cat = Cc0Catalog.Load(Path.Combine(Cc0Dir, "manifest.json"));
        Assert.True(cat.Count >= 10);
        Assert.NotNull(cat.Pick("tree", 1234));
        Assert.NotNull(cat.Pick("pickup", 99));
    }

    [Fact]
    public void World_is_identical_with_or_without_prefab_models()
    {
        // Gorselsiz kurulum (testler) ile prefab'li kurulum ayni carpisma ve yol grafigini uretmeli.
        var plain = DistrictBuilder.Build(true, null);
        var withPrefabs = DistrictBuilder.Build(true, null, prefabs: new AlwaysPrefab());
        Assert.Equal(plain.Collision.Static.Count, withPrefabs.Collision.Static.Count);
        Assert.Equal(plain.Nav.Nodes.Count, withPrefabs.Nav.Nodes.Count);
        for (var i = 0; i < plain.Collision.Static.Count; i++)
        {
            Assert.Equal(plain.Collision.Static[i].Center, withPrefabs.Collision.Static[i].Center);
        }

        Assert.NotEmpty(plain.Prefabs);
    }

    private sealed class AlwaysPrefab : IPrefabSource
    {
        public bool Has(string group) => true;

        public void Bake(string group, uint hash, in Matrix4x4 xf, StaticScene.Builder g)
        {
        }
    }
}
