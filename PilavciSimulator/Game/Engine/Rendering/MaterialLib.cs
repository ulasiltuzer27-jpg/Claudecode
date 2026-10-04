using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>Bir yuzey turu: doku + renk + isik davranisi.</summary>
public sealed class MaterialDef
{
    public required string Name { get; init; }
    public Texture2D Texture { get; set; }
    public Color Tint { get; set; } = Color.White;
    public float Specular { get; set; } = 0.1f;
    public Vector3 Emissive { get; set; }
    public bool Transparent { get; set; }
    public bool DoubleSided { get; set; }
    public bool Unlit { get; set; }
    public bool NoFog { get; set; }
    public bool CastShadow { get; set; } = true;
    public float Wind { get; set; }

    internal Material Native;
}

/// <summary>
/// Malzeme kaydi. Her malzeme tek bir raylib <see cref="Material"/>'a
/// karsilik gelir (shader = aydinlatma shader'i). Golge haritasi her
/// malzemenin BRDF yuvasina baglanir; raylib DrawMesh onu 10. doku
/// birimine kendiliginden bagliyor.
/// </summary>
public sealed class MaterialLib
{
    private readonly List<MaterialDef> _defs = new();
    private readonly Dictionary<string, int> _byName = new(StringComparer.Ordinal);
    private readonly Shader _lit;
    private Texture2D _shadowDepth;

    public MaterialLib(Shader lit) => _lit = lit;

    public int Count => _defs.Count;
    public MaterialDef this[int id] => _defs[id];

    public unsafe int Register(MaterialDef def)
    {
        if (_byName.TryGetValue(def.Name, out var existing))
        {
            return existing;
        }

        var m = Raylib.LoadMaterialDefault();
        m.Shader = _lit;
        if (def.Texture.Id != 0)
        {
            m.Maps[(int)MaterialMapIndex.Albedo].Texture = def.Texture;
        }

        m.Maps[(int)MaterialMapIndex.Albedo].Color = def.Tint;
        m.Maps[(int)MaterialMapIndex.Brdf].Texture = _shadowDepth;
        def.Native = m;
        _defs.Add(def);
        _byName[def.Name] = _defs.Count - 1;
        return _defs.Count - 1;
    }

    public int Id(string name) =>
        _byName.TryGetValue(name, out var id) ? id : throw new KeyNotFoundException($"malzeme yok: {name}");

    public bool TryGet(string name, out int id) => _byName.TryGetValue(name, out id);

    /// <summary>Golge haritasi yeniden olusturuldugunda (kalite degisti) tum malzemelere baglar.</summary>
    public unsafe void SetShadowTexture(Texture2D depth)
    {
        _shadowDepth = depth;
        foreach (var d in _defs)
        {
            d.Native.Maps[(int)MaterialMapIndex.Brdf].Texture = depth;
        }
    }

    /// <summary>Tum malzemeleri bir kez dolasmak icin.</summary>
    public IReadOnlyList<MaterialDef> All => _defs;
}
