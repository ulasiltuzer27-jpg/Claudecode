using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>Bir glTF malzemesi: taban rengi (dogrusal) ve varsa doku baytlari.</summary>
public sealed class GltfMaterial
{
    public string Name = "";
    public Vector4 BaseColor = Vector4.One;
    public byte[]? ImageBytes;
    public string ImageKey = "";
    public string MimeType = "image/png";
    public bool DoubleSided;
}

/// <summary>Malzemeye gore toplanmis ucgen mesh.</summary>
public sealed class GltfPrimitive
{
    public required MeshData Mesh;
    public int Material = -1;
    public bool HasVertexColor;
}

public sealed class GltfModel
{
    public List<GltfPrimitive> Primitives { get; } = new();
    public List<GltfMaterial> Materials { get; } = new();

    public Bounds ComputeBounds()
    {
        var b = Bounds.Empty;
        foreach (var p in Primitives)
        {
            foreach (var v in p.Mesh.Positions)
            {
                b.Encapsulate(v);
            }
        }

        return b;
    }
}

/// <summary>
/// Kucuk bir glTF 2.0 okuyucu (.glb ve dis .bin'li .gltf). GPU gerektirmez:
/// testler ve statik sahneye "pisirme" (bake) CPU'da yapilir. Dugum
/// donusumleri koselere uygulanir; yalnizca ucgen primitifler okunur.
///
/// Neden raylib'in LoadModel'i degil: o hem GL baglami ister hem de
/// mesh'i hemen GPU'ya yukler; biz once olcekleyip birlestirmek istiyoruz.
/// </summary>
public static class GltfReader
{
    private const uint GlbMagic = 0x46546C67; // "glTF"
    private const uint ChunkJson = 0x4E4F534A;
    private const uint ChunkBin = 0x004E4942;

    public static GltfModel Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            ? ReadGlb(bytes, Path.GetDirectoryName(path) ?? ".")
            : ReadGltf(bytes, Path.GetDirectoryName(path) ?? ".");
    }

    public static GltfModel ReadGlb(byte[] data, string baseDir)
    {
        if (data.Length < 20 || BitConverter.ToUInt32(data, 0) != GlbMagic)
        {
            throw new InvalidDataException("GLB degil");
        }

        var version = BitConverter.ToUInt32(data, 4);
        if (version != 2)
        {
            throw new InvalidDataException($"desteklenmeyen glTF surumu {version}");
        }

        var pos = 12;
        string? json = null;
        byte[]? bin = null;
        while (pos + 8 <= data.Length)
        {
            var len = (int)BitConverter.ToUInt32(data, pos);
            var type = BitConverter.ToUInt32(data, pos + 4);
            pos += 8;
            if (type == ChunkJson)
            {
                json = System.Text.Encoding.UTF8.GetString(data, pos, len);
            }
            else if (type == ChunkBin)
            {
                bin = new byte[len];
                Buffer.BlockCopy(data, pos, bin, 0, len);
            }

            pos += (len + 3) & ~3;
        }

        if (json is null)
        {
            throw new InvalidDataException("GLB'de JSON bolumu yok");
        }

        return Parse(json, bin, baseDir);
    }

    public static GltfModel ReadGltf(byte[] data, string baseDir) =>
        Parse(System.Text.Encoding.UTF8.GetString(data), null, baseDir);

    private static GltfModel Parse(string json, byte[]? glbBin, string baseDir)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var model = new GltfModel();

        // ── Tamponlar ───────────────────────────────────────────────
        List<byte[]> buffers = new();
        if (root.TryGetProperty("buffers", out var bufs))
        {
            foreach (var b in bufs.EnumerateArray())
            {
                if (b.TryGetProperty("uri", out var uri))
                {
                    buffers.Add(LoadUri(uri.GetString()!, baseDir));
                }
                else
                {
                    buffers.Add(glbBin ?? []);
                }
            }
        }

        List<JsonElement> views = root.TryGetProperty("bufferViews", out var bv) ? bv.EnumerateArray().ToList() : new();
        List<JsonElement> accessors = root.TryGetProperty("accessors", out var acc) ? acc.EnumerateArray().ToList() : new();

        byte[] ViewBytes(int viewIndex, out int offset, out int length, out int stride) =>
            ReadView(views, buffers, viewIndex, out offset, out length, out stride);

        static byte[] ReadView(List<JsonElement> views, List<byte[]> buffers, int viewIndex, out int offset, out int length, out int stride)
        {
            var v = views[viewIndex];
            var buf = buffers[v.GetProperty("buffer").GetInt32()];
            offset = v.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
            length = v.GetProperty("byteLength").GetInt32();
            stride = v.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 0;
            return buf;
        }

        // ── Malzemeler ve dokular ───────────────────────────────────
        var images = root.TryGetProperty("images", out var im) ? im.EnumerateArray().ToList() : new List<JsonElement>();
        var textures = root.TryGetProperty("textures", out var tx) ? tx.EnumerateArray().ToList() : new List<JsonElement>();
        if (root.TryGetProperty("materials", out var mats))
        {
            foreach (var m in mats.EnumerateArray())
            {
                var gm = new GltfMaterial { Name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "" };
                gm.DoubleSided = m.TryGetProperty("doubleSided", out var ds) && ds.GetBoolean();
                if (m.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    if (pbr.TryGetProperty("baseColorFactor", out var bc))
                    {
                        var f = bc.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                        gm.BaseColor = new Vector4(f[0], f[1], f[2], f.Length > 3 ? f[3] : 1);
                    }

                    if (pbr.TryGetProperty("baseColorTexture", out var bt) && bt.TryGetProperty("index", out var ti))
                    {
                        var tex = textures[ti.GetInt32()];
                        if (tex.TryGetProperty("source", out var src))
                        {
                            var img = images[src.GetInt32()];
                            if (img.TryGetProperty("uri", out var iu))
                            {
                                var u = iu.GetString()!;
                                gm.ImageBytes = LoadUri(u, baseDir);
                                gm.ImageKey = u.StartsWith("data:", StringComparison.Ordinal) ? $"embedded{src.GetInt32()}" : Path.GetFileName(u);
                                gm.MimeType = u.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || u.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";
                            }
                            else if (img.TryGetProperty("bufferView", out var ib))
                            {
                                var buf = ViewBytes(ib.GetInt32(), out var off, out var len, out _);
                                gm.ImageBytes = buf.AsSpan(off, len).ToArray();
                                gm.ImageKey = $"image{src.GetInt32()}";
                                gm.MimeType = img.TryGetProperty("mimeType", out var mt) ? mt.GetString() ?? "image/png" : "image/png";
                            }
                        }
                    }
                }

                model.Materials.Add(gm);
            }
        }

        // ── Erisimci okuma ──────────────────────────────────────────
        float[] ReadFloats(int accessorIndex, out int components)
        {
            var a = accessors[accessorIndex];
            var count = a.GetProperty("count").GetInt32();
            var type = a.GetProperty("type").GetString();
            components = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 1 };
            var ctype = a.GetProperty("componentType").GetInt32();
            var normalized = a.TryGetProperty("normalized", out var nz) && nz.GetBoolean();
            var result = new float[count * components];
            if (!a.TryGetProperty("bufferView", out var bvi))
            {
                return result; // seyrek/bos erisimci: sifir
            }

            var buf = ViewBytes(bvi.GetInt32(), out var viewOff, out _, out var stride);
            var accOff = a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0;
            var compSize = ctype switch { 5120 or 5121 => 1, 5122 or 5123 => 2, _ => 4 };
            var elemSize = compSize * components;
            if (stride == 0)
            {
                stride = elemSize;
            }

            for (var i = 0; i < count; i++)
            {
                var basePos = viewOff + accOff + i * stride;
                for (var c = 0; c < components; c++)
                {
                    var p = basePos + c * compSize;
                    result[i * components + c] = ctype switch
                    {
                        5126 => BitConverter.ToSingle(buf, p),
                        5121 => normalized ? buf[p] / 255f : buf[p],
                        5120 => normalized ? MathF.Max((sbyte)buf[p] / 127f, -1f) : (sbyte)buf[p],
                        5123 => normalized ? BitConverter.ToUInt16(buf, p) / 65535f : BitConverter.ToUInt16(buf, p),
                        5122 => normalized ? MathF.Max(BitConverter.ToInt16(buf, p) / 32767f, -1f) : BitConverter.ToInt16(buf, p),
                        5125 => BitConverter.ToUInt32(buf, p),
                        _ => 0,
                    };
                }
            }

            return result;
        }

        // ── Dugumler: dunya matrisleri ──────────────────────────────
        var nodes = root.TryGetProperty("nodes", out var nd) ? nd.EnumerateArray().ToList() : new List<JsonElement>();
        var meshes = root.TryGetProperty("meshes", out var ms) ? ms.EnumerateArray().ToList() : new List<JsonElement>();
        var roots = new List<int>();
        if (root.TryGetProperty("scenes", out var scenes) && scenes.GetArrayLength() > 0)
        {
            var sceneIndex = root.TryGetProperty("scene", out var si) ? si.GetInt32() : 0;
            if (scenes[sceneIndex].TryGetProperty("nodes", out var sn))
            {
                roots.AddRange(sn.EnumerateArray().Select(e => e.GetInt32()));
            }
        }
        else
        {
            // Sahnesiz dosya: cocugu olmayan tum dugumler kok sayilir
            var children = new HashSet<int>();
            foreach (var n in nodes)
            {
                if (n.TryGetProperty("children", out var ch))
                {
                    foreach (var c in ch.EnumerateArray())
                    {
                        children.Add(c.GetInt32());
                    }
                }
            }

            roots.AddRange(Enumerable.Range(0, nodes.Count).Where(i => !children.Contains(i)));
        }

        var byMaterial = new Dictionary<int, GltfPrimitive>();

        void Visit(int nodeIndex, Matrix4x4 parent, int depth)
        {
            if (depth > 64)
            {
                return;
            }

            var n = nodes[nodeIndex];
            var local = LocalMatrix(n);
            var world = local * parent;
            if (n.TryGetProperty("mesh", out var mi))
            {
                AddMesh(meshes[mi.GetInt32()], world);
            }

            if (n.TryGetProperty("children", out var ch))
            {
                foreach (var c in ch.EnumerateArray())
                {
                    Visit(c.GetInt32(), world, depth + 1);
                }
            }
        }

        void AddMesh(JsonElement mesh, Matrix4x4 world)
        {
            Matrix4x4.Invert(world, out var inv);
            var nxf = Matrix4x4.Transpose(inv);
            // Ayna (negatif olcek) sarim yonunu ters cevirir
            var flip = world.GetDeterminant() < 0;
            foreach (var prim in mesh.GetProperty("primitives").EnumerateArray())
            {
                var mode = prim.TryGetProperty("mode", out var md) ? md.GetInt32() : 4;
                if (mode != 4)
                {
                    continue; // yalnizca TRIANGLES
                }

                var attrs = prim.GetProperty("attributes");
                if (!attrs.TryGetProperty("POSITION", out var pa))
                {
                    continue;
                }

                var positions = ReadFloats(pa.GetInt32(), out _);
                var vcount = positions.Length / 3;
                var normals = attrs.TryGetProperty("NORMAL", out var na) ? ReadFloats(na.GetInt32(), out _) : null;
                var uvs = attrs.TryGetProperty("TEXCOORD_0", out var ua) ? ReadFloats(ua.GetInt32(), out _) : null;
                int colorComps = 0;
                var colors = attrs.TryGetProperty("COLOR_0", out var ca) ? ReadFloats(ca.GetInt32(), out colorComps) : null;
                var indices = prim.TryGetProperty("indices", out var ia)
                    ? ReadFloats(ia.GetInt32(), out _).Select(f => (int)f).ToArray()
                    : Enumerable.Range(0, vcount).ToArray();
                var matIndex = prim.TryGetProperty("material", out var mt) ? mt.GetInt32() : -1;
                if (!byMaterial.TryGetValue(matIndex, out var gp))
                {
                    gp = new GltfPrimitive { Mesh = new MeshData(), Material = matIndex };
                    byMaterial[matIndex] = gp;
                    model.Primitives.Add(gp);
                }

                gp.HasVertexColor |= colors is not null;
                var m = gp.Mesh;
                var start = m.VertexCount;
                for (var i = 0; i < vcount; i++)
                {
                    var p = Vector3.Transform(new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]), world);
                    var nrm = normals is not null
                        ? Shapes.SafeNormalize(Vector3.TransformNormal(new Vector3(normals[i * 3], normals[i * 3 + 1], normals[i * 3 + 2]), nxf))
                        : Vector3.Zero;
                    var uv = uvs is not null ? new Vector2(uvs[i * 2], uvs[i * 2 + 1]) : Vector2.Zero;
                    var col = Color.White;
                    if (colors is not null)
                    {
                        var r = colors[i * colorComps];
                        var g = colors[i * colorComps + 1];
                        var bl = colors[i * colorComps + 2];
                        col = new Color(LinearToSrgbByte(r), LinearToSrgbByte(g), LinearToSrgbByte(bl), (byte)255);
                    }

                    m.AddVertex(p, nrm, uv, col);
                }

                for (var t = 0; t + 2 < indices.Length; t += 3)
                {
                    if (flip)
                    {
                        m.AddTriangle(start + indices[t], start + indices[t + 2], start + indices[t + 1]);
                    }
                    else
                    {
                        m.AddTriangle(start + indices[t], start + indices[t + 1], start + indices[t + 2]);
                    }
                }

                if (normals is null)
                {
                    FlatNormals(m, start);
                }
            }
        }

        foreach (var r in roots)
        {
            Visit(r, Matrix4x4.Identity, 0);
        }

        return model;
    }

    private static Matrix4x4 LocalMatrix(JsonElement n)
    {
        if (n.TryGetProperty("matrix", out var mx))
        {
            // glTF sutun-oncelikli sutun-vektor matrisi == System.Numerics satir-vektor duzeni
            var f = mx.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            return new Matrix4x4(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11], f[12], f[13], f[14], f[15]);
        }

        var s = Vector3.One;
        var r = Quaternion.Identity;
        var t = Vector3.Zero;
        if (n.TryGetProperty("scale", out var sc))
        {
            var f = sc.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            s = new Vector3(f[0], f[1], f[2]);
        }

        if (n.TryGetProperty("rotation", out var ro))
        {
            var f = ro.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            r = new Quaternion(f[0], f[1], f[2], f[3]);
        }

        if (n.TryGetProperty("translation", out var tr))
        {
            var f = tr.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            t = new Vector3(f[0], f[1], f[2]);
        }

        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
    }

    private static byte[] LoadUri(string uri, string baseDir)
    {
        if (uri.StartsWith("data:", StringComparison.Ordinal))
        {
            var comma = uri.IndexOf(',');
            return Convert.FromBase64String(uri[(comma + 1)..]);
        }

        return File.ReadAllBytes(Path.Combine(baseDir, Uri.UnescapeDataString(uri)));
    }

    private static void FlatNormals(MeshData m, int start)
    {
        // Paylasilan koseler icin ucgen normallerinin ortalamasi
        var acc = new Vector3[m.VertexCount - start];
        for (var t = 0; t < m.Indices.Count; t += 3)
        {
            var a = m.Indices[t];
            var b = m.Indices[t + 1];
            var c = m.Indices[t + 2];
            if (a < start || b < start || c < start)
            {
                continue;
            }

            var n = Vector3.Cross(m.Positions[b] - m.Positions[a], m.Positions[c] - m.Positions[a]);
            acc[a - start] += n;
            acc[b - start] += n;
            acc[c - start] += n;
        }

        for (var i = 0; i < acc.Length; i++)
        {
            m.Normals[start + i] = Shapes.SafeNormalize(acc[i]);
        }
    }

    /// <summary>Dogrusal [0..1] -> sRGB bayt (glTF renk carpanlari dogrusal).</summary>
    public static byte LinearToSrgbByte(float v)
    {
        v = Math.Clamp(v, 0f, 1f);
        var s = v <= 0.0031308f ? v * 12.92f : 1.055f * MathF.Pow(v, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)MathF.Round(s * 255f), 0, 255);
    }
}
