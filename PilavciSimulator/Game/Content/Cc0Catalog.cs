using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.World;

namespace PilavciSimulator.Content;

/// <summary>Manifestteki bir CC0 model kaydi.</summary>
public sealed class Cc0Entry
{
    public string Key { get; set; } = "";
    public string File { get; set; } = "";
    /// <summary>Kullanim grubu: "tree", "car", "fountain"... Varyantlar ayni grupta toplanir.</summary>
    public string Group { get; set; } = "";
    public Cc0Fit Fit { get; set; } = new();
    /// <summary>Derece: modelin "on"u oyunun kuralina (-Z; arac icin +X) donsun.</summary>
    public float Yaw { get; set; }
    public float[] Offset { get; set; } = [0, 0, 0];
    public string Tint { get; set; } = "FFFFFF";
    public float Saturation { get; set; } = 1f;
    /// <summary>Yaprak ucgenleri ruzgarda salinir ve mevsimle renk degistirir.</summary>
    public bool Foliage { get; set; }
    public bool Shadow { get; set; } = true;
    public float Weight { get; set; } = 1f;
}

/// <summary>
/// Olcek kurali: tam olarak biri kullanilir. Axis+Size: o eksendeki boy
/// (x/y/z) metre olur. Scale: dogrudan carpan. MaxBox: kutuya sigar.
/// </summary>
public sealed class Cc0Fit
{
    public string Axis { get; set; } = "";
    public float Size { get; set; }
    public float Scale { get; set; }
    public float[]? MaxBox { get; set; }
}

public sealed class Cc0Manifest
{
    public List<Cc0Entry> Assets { get; set; } = new();
}

/// <summary>Normalize edilmis, malzemelere ayrilmis bir model.</summary>
public sealed class Cc0Asset
{
    public required Cc0Entry Entry;
    public List<(string MaterialKey, byte[]? Image, MeshData Mesh)> Parts { get; } = new();
    public Bounds Bounds;
}

/// <summary>
/// Hazir CC0 modellerin katalogu (<c>Assets/Models/cc0/manifest.json</c>).
///
/// Her model yuklenirken normalize edilir: tabani y=0, yatayda ortali,
/// manifestteki yaw ve olcek uygulanir, renkler koselere "pisirilir".
/// Statik dunyaya giren modeller (agac, cesme) hucre mesh'lerine eklenir,
/// kare basina ek cizim cagrisi olmaz. Katalog bossa (dosyalar silinmis,
/// testler) her yerde prosedurel yedek kullanilir.
/// </summary>
public sealed class Cc0Catalog : IPrefabSource
{
    private readonly Dictionary<string, Cc0Asset> _assets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Cc0Asset>> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _materialIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RenderModel> _models = new(StringComparer.Ordinal);
    private bool _gpuReady;

    public int Count => _assets.Count;
    public IEnumerable<Cc0Asset> Assets => _assets.Values;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Manifesti ve modelleri okur (CPU). Bozuk tek model tum katalogu durdurmaz.</summary>
    public static Cc0Catalog Load(string manifestPath)
    {
        var cat = new Cc0Catalog();
        if (!System.IO.File.Exists(manifestPath))
        {
            return cat;
        }

        Cc0Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Cc0Manifest>(System.IO.File.ReadAllText(manifestPath), Json) ?? new Cc0Manifest();
        }
        catch (Exception ex)
        {
            Log.Warn($"CC0 manifest okunamadi: {ex.Message}");
            return cat;
        }

        var dir = Path.GetDirectoryName(manifestPath) ?? ".";
        foreach (var e in manifest.Assets)
        {
            try
            {
                var path = Path.Combine(dir, e.File);
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                var asset = Normalize(GltfReader.Load(path), e);
                cat._assets[e.Key] = asset;
                if (!cat._groups.TryGetValue(e.Group, out var list))
                {
                    list = new List<Cc0Asset>();
                    cat._groups[e.Group] = list;
                }

                list.Add(asset);
            }
            catch (Exception ex)
            {
                Log.Warn($"CC0 model yuklenemedi ({e.Key}): {ex.Message}");
            }
        }

        if (cat.Count > 0)
        {
            Log.Info($"CC0 katalogu: {cat.Count} model, {cat._groups.Count} grup");
        }

        return cat;
    }

    /// <summary>
    /// Ham modeli oyunun kuralina oturtur. Saf matematik: testler GPU'suz
    /// cagirir. Sira: yaw -> sinir -> tabana/merkeze tasima -> olcek -> ofset.
    /// </summary>
    public static Cc0Asset Normalize(GltfModel model, Cc0Entry e)
    {
        var yaw = Matrix4x4.CreateRotationY(e.Yaw * MathF.PI / 180f);
        var raw = Bounds.Empty;
        foreach (var p in model.Primitives)
        {
            foreach (var v in p.Mesh.Positions)
            {
                raw.Encapsulate(Vector3.Transform(v, yaw));
            }
        }

        var xf = yaw * FitTransform(raw, e);
        var tint = Gfx.Hex(Convert.ToUInt32(e.Tint, 16));
        var asset = new Cc0Asset { Entry = e, Bounds = Bounds.Empty };
        foreach (var prim in model.Primitives)
        {
            var mat = prim.Material >= 0 && prim.Material < model.Materials.Count ? model.Materials[prim.Material] : null;
            var baseCol = mat?.BaseColor ?? Vector4.One;
            var factor = new Color(GltfReader.LinearToSrgbByte(baseCol.X), GltfReader.LinearToSrgbByte(baseCol.Y), GltfReader.LinearToSrgbByte(baseCol.Z), (byte)255);
            var mesh = new MeshData();
            mesh.Append(prim.Mesh, xf);
            for (var i = 0; i < mesh.VertexCount; i++)
            {
                var c = Multiply(Multiply(mesh.Colors[i], factor), tint);
                c = Saturate(c, e.Saturation);
                // Opak malzemede kose alfasi "pencere" isaretidir: her zaman 255.
                mesh.Colors[i] = c with { A = 255 };
                asset.Bounds.Encapsulate(mesh.Positions[i]);
            }

            var image = mat?.ImageBytes;
            var key = image is not null ? $"{Path.GetDirectoryName(e.File)}/{mat!.ImageKey}" : "";
            asset.Parts.Add((key, image, mesh));
        }

        return asset;
    }

    /// <summary>Yaw uygulanmis sinirlardan: tabana oturt, ortala, olcekle, ofset ekle.</summary>
    public static Matrix4x4 FitTransform(Bounds raw, Cc0Entry e)
    {
        var size = raw.Max - raw.Min;
        var center = (raw.Min + raw.Max) * 0.5f;
        var s = 1f;
        var fit = e.Fit;
        if (fit.Scale > 0)
        {
            s = fit.Scale;
        }
        else if (fit.Size > 0 && fit.Axis.Length > 0)
        {
            var extent = fit.Axis switch { "x" => size.X, "z" => size.Z, _ => size.Y };
            s = extent > 1e-5f ? fit.Size / extent : 1f;
        }
        else if (fit.MaxBox is { Length: 3 } box)
        {
            s = float.MaxValue;
            if (size.X > 1e-5f) s = MathF.Min(s, box[0] / size.X);
            if (size.Y > 1e-5f) s = MathF.Min(s, box[1] / size.Y);
            if (size.Z > 1e-5f) s = MathF.Min(s, box[2] / size.Z);
            if (s == float.MaxValue) s = 1f;
        }

        var offset = e.Offset.Length == 3 ? new Vector3(e.Offset[0], e.Offset[1], e.Offset[2]) : Vector3.Zero;
        return Matrix4x4.CreateTranslation(-center.X, -raw.Min.Y, -center.Z) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateTranslation(offset);
    }

    private static Color Multiply(Color a, Color b) =>
        new((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255), (byte)(a.A * b.A / 255));

    private static Color Saturate(Color c, float sat)
    {
        if (MathF.Abs(sat - 1f) < 1e-3f)
        {
            return c;
        }

        var l = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
        byte Ch(float v) => (byte)Math.Clamp((int)(l + (v - l) * sat), 0, 255);
        return new Color(Ch(c.R), Ch(c.G), Ch(c.B), c.A);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GPU tarafi (yalnizca istemci)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Dokulari yukler ve malzemeleri kaydeder. Statik sahne kurulmadan once cagrilmali.</summary>
    public void RegisterMaterials(MaterialLib mats, Texture2D white)
    {
        foreach (var a in _assets.Values)
        {
            foreach (var (key, image, _) in a.Parts)
            {
                var name = MaterialName(key, a.Entry.Foliage);
                if (_materialIds.ContainsKey(name))
                {
                    continue;
                }

                var tex = white;
                if (image is not null)
                {
                    tex = LoadTexture(image, key);
                }

                _materialIds[name] = mats.Register(new MaterialDef
                {
                    Name = name,
                    Texture = tex,
                    Specular = 0.12f,
                    Wind = a.Entry.Foliage ? 0.03f : 0f,
                    CastShadow = a.Entry.Shadow,
                    DoubleSided = a.Entry.Foliage,
                });
            }
        }

        _gpuReady = true;
    }

    private static string MaterialName(string textureKey, bool foliage) =>
        (foliage ? "cc0f:" : "cc0:") + (textureKey.Length > 0 ? textureKey : "flat");

    private static unsafe Texture2D LoadTexture(byte[] bytes, string key)
    {
        var ext = key.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        var img = Raylib.LoadImageFromMemory(ext, bytes);
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.GenTextureMipmaps(&tex);
        Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
        Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
        return tex;
    }

    public bool Has(string group) => _gpuReady && _groups.ContainsKey(group);

    public int VariantCount(string group) => _groups.TryGetValue(group, out var l) ? l.Count : 0;

    /// <summary>Konum karmasindan (RNG'ye dokunmadan) agirlikli varyant secimi.</summary>
    public Cc0Asset? Pick(string group, uint hash)
    {
        if (!_groups.TryGetValue(group, out var list) || list.Count == 0)
        {
            return null;
        }

        var total = list.Sum(a => MathF.Max(0.01f, a.Entry.Weight));
        var t = (hash % 10000) / 10000f * total;
        foreach (var a in list)
        {
            t -= MathF.Max(0.01f, a.Entry.Weight);
            if (t <= 0)
            {
                return a;
            }
        }

        return list[^1];
    }

    /// <summary>Statik sahneye pisir: her parca dunyadaki hucresinin mesh'ine eklenir.</summary>
    public void Bake(string group, uint hash, in Matrix4x4 xf, StaticScene.Builder g)
    {
        if (!_gpuReady || Pick(group, hash) is not { } asset)
        {
            return;
        }

        var at = Vector3.Transform(Vector3.Zero, xf);
        foreach (var (key, _, mesh) in asset.Parts)
        {
            if (_materialIds.TryGetValue(MaterialName(key, asset.Entry.Foliage), out var id))
            {
                g.At(id, at).Append(mesh, xf);
            }
        }
    }

    /// <summary>Dinamik cizim icin (araclar): grup + tohumdan bir model; yoksa null.</summary>
    public RenderModel? Model(string group, uint seed)
    {
        if (!_gpuReady || Pick(group, seed) is not { } asset)
        {
            return null;
        }

        if (_models.TryGetValue(asset.Entry.Key, out var cached))
        {
            return cached;
        }

        var rm = new RenderModel();
        foreach (var (key, _, mesh) in asset.Parts)
        {
            if (_materialIds.TryGetValue(MaterialName(key, asset.Entry.Foliage), out var id) && !mesh.IsEmpty)
            {
                foreach (var part in mesh.UploadSplit())
                {
                    rm.Add(part, id, mesh.ComputeBounds());
                }
            }
        }

        _models[asset.Entry.Key] = rm;
        return rm;
    }
}
