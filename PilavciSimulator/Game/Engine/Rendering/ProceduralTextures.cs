using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

/// <summary>
/// Calisma aninda uretilen dokular. Oyunun hicbir doku dosyasi yok; hepsi
/// burada gurultu fonksiyonlariyla ciziliyor. Cogu gri tonlu "detay" dokusu
/// (rengi kose rengi veriyor); tugla, kiremit, ahsap gibi kendi rengi olanlar
/// renkli uretiliyor.
///
/// Tum dokular dikissiz dosenir, mipmap'li ve tekrarli (repeat) sarilir.
/// </summary>
public static class ProceduralTextures
{
    public delegate Vector4 PixelFunc(float u, float v);

    /// <summary>Piksel fonksiyonundan doku uretir (RGBA, 0..1).</summary>
    public static Texture2D Generate(int size, PixelFunc f, bool mipmaps = true, TextureFilter filter = TextureFilter.Anisotropic8X)
    {
        var pixels = new byte[size * size * 4];
        Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var c = f((x + 0.5f) / size, (y + 0.5f) / size);
                var i = (y * size + x) * 4;
                pixels[i + 0] = (byte)Math.Clamp(c.X * 255f, 0f, 255f);
                pixels[i + 1] = (byte)Math.Clamp(c.Y * 255f, 0f, 255f);
                pixels[i + 2] = (byte)Math.Clamp(c.Z * 255f, 0f, 255f);
                pixels[i + 3] = (byte)Math.Clamp(c.W * 255f, 0f, 255f);
            }
        });

        return FromPixels(pixels, size, size, mipmaps, filter);
    }

    /// <summary>RGBA8 piksel dizisinden doku.</summary>
    public static unsafe Texture2D FromPixels(byte[] pixels, int width, int height, bool mipmaps = true,
        TextureFilter filter = TextureFilter.Anisotropic8X, TextureWrap wrap = TextureWrap.Repeat)
    {
        // Image.Data raylib'in ayiricisiyla alinmali: UnloadImage onu free'liyor.
        var bytes = (byte*)Raylib.MemAlloc((uint)pixels.Length);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, (IntPtr)bytes, pixels.Length);
        var image = new Image
        {
            Data = bytes,
            Width = width,
            Height = height,
            Mipmaps = 1,
            Format = PixelFormat.UncompressedR8G8B8A8,
        };
        var tex = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        if (mipmaps)
        {
            Raylib.GenTextureMipmaps(ref tex);
        }

        if (mipmaps)
        {
            // raylib'de anizotropik filtre yalnizca anizotropi duzeyini ayarliyor;
            // kucultme filtresinin mipmap kullanmasi icin once trilinear secilmeli.
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            if (filter != TextureFilter.Trilinear)
            {
                Raylib.SetTextureFilter(tex, filter);
            }
        }
        else
        {
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
        }

        Raylib.SetTextureWrap(tex, wrap);
        return tex;
    }

    private static Vector4 Gray(float g) => new(g, g, g, 1f);
    private static Vector4 Rgb(float r, float g, float b) => new(r, g, b, 1f);
    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);

    public static Texture2D White() => Generate(4, (_, _) => Vector4.One, mipmaps: false);

    /// <summary>Hafif dalgali siva (renk kose renginden). 1 doku = 2 m.</summary>
    public static Texture2D Plaster() => Generate(256, (u, v) =>
    {
        var n = Noise.Fbm(u, v, 8, 5, 11);
        var speck = Noise.Value(u * 128, v * 128, 128, 3) > 0.93f ? -0.06f : 0f;
        return Gray(0.86f + (n - 0.5f) * 0.18f + speck);
    });

    /// <summary>Beton / sicak gri yuzey.</summary>
    public static Texture2D Concrete() => Generate(256, (u, v) =>
    {
        var n = Noise.Fbm(u, v, 6, 6, 21);
        var pores = Noise.Value(u * 96, v * 96, 96, 7);
        var g = 0.78f + (n - 0.5f) * 0.25f - (pores > 0.88f ? 0.12f : 0f);
        return Gray(g);
    });

    /// <summary>Asfalt: koyu, taneli. Renkli (gri).</summary>
    public static Texture2D Asphalt() => Generate(256, (u, v) =>
    {
        var n = Noise.Fbm(u, v, 16, 4, 31);
        var grain = Noise.Hash((int)(u * 256), (int)(v * 256), 5);
        var g = 0.22f + (n - 0.5f) * 0.12f + (grain - 0.5f) * 0.08f + (grain > 0.97f ? 0.12f : 0f);
        return Rgb(g, g, g * 1.04f);
    });

    /// <summary>Arnavut kaldirimi: yuvarlak taslar, koyu derzler. 1 doku = 2 m.</summary>
    public static Texture2D Cobblestone() => Generate(512, (u, v) =>
    {
        var (f1, f2, cell) = Noise.Cellular(u, v, 14, 41, 0.7f);
        var edge = Clamp01((f2 - f1) * 4.5f);
        var tone = 0.55f + Noise.Hash(cell, 1, 9) * 0.3f;
        var detail = Noise.Fbm(u, v, 32, 3, 13);
        var stone = tone * (0.85f + detail * 0.3f) * (0.6f + 0.4f * MathF.Pow(edge, 0.5f));
        var mortar = 0.28f;
        var g = edge < 0.18f ? mortar + edge : stone;
        return Rgb(g * 1.02f, g, g * 0.95f);
    });

    /// <summary>Kaldirim tasi (buyuk kare plakalar). 1 doku = 2 m.</summary>
    public static Texture2D Pavers() => Generate(256, (u, v) =>
    {
        var cx = u * 4;
        var cy = v * 4;
        var fx = cx - MathF.Floor(cx);
        var fy = cy - MathF.Floor(cy);
        var edge = MathF.Min(MathF.Min(fx, 1 - fx), MathF.Min(fy, 1 - fy));
        var id = (int)MathF.Floor(cx) + (int)MathF.Floor(cy) * 4;
        var tone = 0.72f + Noise.Hash(id, 3, 1) * 0.12f + (Noise.Fbm(u, v, 8, 4, 2) - 0.5f) * 0.12f;
        var g = edge < 0.025f ? 0.45f : tone;
        return Gray(g);
    });

    /// <summary>Tugla duvar. Renkli. 1 doku = 1 m.</summary>
    public static Texture2D Brick() => Generate(256, (u, v) =>
    {
        const int rows = 16;
        var row = (int)MathF.Floor(v * rows);
        var offset = row % 2 == 0 ? 0f : 0.5f;
        var bx = u * 4 + offset;
        var col = (int)MathF.Floor(bx);
        var fx = bx - col;
        var fy = v * rows - row;
        var mortar = fx < 0.04f || fy < 0.12f;
        var id = col * 31 + row * 7;
        var tone = 0.85f + Noise.Hash(id, 5, 2) * 0.3f;
        var n = Noise.Fbm(u, v, 16, 3, 17) * 0.2f;
        if (mortar)
        {
            return Rgb(0.72f, 0.69f, 0.64f);
        }

        return Rgb((0.62f + n) * tone, (0.30f + n * 0.5f) * tone, (0.22f + n * 0.4f) * tone);
    });

    /// <summary>Ahsap kalaslar (yatay). Renkli. 1 doku = 2 m.</summary>
    public static Texture2D Wood() => Generate(256, (u, v) =>
    {
        const int planks = 8;
        var p = (int)MathF.Floor(v * planks);
        var fy = v * planks - p;
        var grain = Noise.Fbm(u * 1f, v * 8f % 1f, 4, 4, 51 + p);
        var rings = MathF.Sin((u * 40 + grain * 6 + p * 3.1f) * 1.3f) * 0.5f + 0.5f;
        var tone = 0.8f + Noise.Hash(p, 9, 4) * 0.35f;
        var gap = fy < 0.05f ? 0.55f : 1f;
        var r = (0.55f + rings * 0.12f) * tone * gap;
        var g = (0.37f + rings * 0.08f) * tone * gap;
        var b = (0.22f + rings * 0.05f) * tone * gap;
        return Rgb(r, g, b);
    });

    /// <summary>Firca izli celik. Gri tonlu.</summary>
    public static Texture2D Metal() => Generate(256, (u, v) =>
    {
        var streak = Noise.Value(u * 4, v * 128, 128, 61) * 0.5f + Noise.Value(u * 8, v * 256, 256, 62) * 0.5f;
        var n = Noise.Fbm(u, v, 4, 3, 63);
        return Gray(0.78f + (streak - 0.5f) * 0.18f + (n - 0.5f) * 0.1f);
    });

    /// <summary>Beyaz mutfak fayansi + derz. 1 doku = 1 m (4x4 fayans).</summary>
    public static Texture2D Tiles() => Generate(256, (u, v) =>
    {
        var cx = u * 6;
        var cy = v * 6;
        var fx = cx - MathF.Floor(cx);
        var fy = cy - MathF.Floor(cy);
        var grout = fx < 0.04f || fy < 0.04f;
        var bevel = MathF.Min(MathF.Min(fx, 1 - fx), MathF.Min(fy, 1 - fy));
        var shine = 0.94f + MathF.Min(bevel * 2f, 0.06f);
        return grout ? Gray(0.62f) : Gray(shine);
    });

    /// <summary>Damalı yer karosu (dukkan zemini). Renkli.</summary>
    public static Texture2D Checker() => Generate(256, (u, v) =>
    {
        var cx = (int)MathF.Floor(u * 4);
        var cy = (int)MathF.Floor(v * 4);
        var dark = (cx + cy) % 2 == 0;
        var n = Noise.Fbm(u, v, 8, 3, 71) * 0.08f;
        return dark ? Rgb(0.30f + n, 0.24f + n, 0.20f + n) : Rgb(0.88f + n, 0.84f + n, 0.76f + n);
    });

    /// <summary>Kiremit cati. Renkli. 1 doku = 1 m.</summary>
    public static Texture2D RoofTiles() => Generate(256, (u, v) =>
    {
        const int rows = 6;
        var row = (int)MathF.Floor(v * rows);
        var fy = v * rows - row;
        var offset = row % 2 == 0 ? 0f : 0.5f;
        var bx = u * 5 + offset;
        var fx = bx - MathF.Floor(bx);
        var curve = MathF.Sin(fx * MathF.PI);
        var shade = 0.6f + 0.4f * curve * (0.6f + 0.4f * fy);
        var tone = 0.9f + Noise.Hash((int)MathF.Floor(bx) * 13 + row, 1, 8) * 0.2f;
        return Rgb(0.72f * shade * tone, 0.32f * shade * tone, 0.20f * shade * tone);
    });

    /// <summary>Cim. Renkli.</summary>
    public static Texture2D Grass() => Generate(256, (u, v) =>
    {
        var n = Noise.Fbm(u, v, 8, 5, 81);
        var blades = Noise.Value(u * 128, v * 64, 64, 82);
        var g = 0.35f + n * 0.25f + (blades - 0.5f) * 0.15f;
        return Rgb(g * 0.55f, g, g * 0.38f);
    });

    /// <summary>Toprak / kum. Renkli.</summary>
    public static Texture2D Dirt() => Generate(256, (u, v) =>
    {
        var n = Noise.Fbm(u, v, 8, 5, 91);
        var g = 0.5f + (n - 0.5f) * 0.3f;
        return Rgb(g * 0.82f, g * 0.68f, g * 0.5f);
    });

    /// <summary>Kumas dokusu (tente, onluk, minder). Gri tonlu.</summary>
    public static Texture2D Fabric() => Generate(128, (u, v) =>
    {
        var weave = (MathF.Sin(u * MathF.Tau * 48) * MathF.Sin(v * MathF.Tau * 48)) * 0.05f;
        var n = Noise.Fbm(u, v, 4, 3, 101) * 0.1f;
        return Gray(0.88f + weave + n);
    });

    /// <summary>Pilav: sik dizili pirinc taneleri. Renkli. Kazan ve tabak ustu.</summary>
    public static Texture2D Rice() => Generate(256, (u, v) =>
    {
        var best = 0f;
        // Uc katman rastgele yonlu elips "tane"
        for (var layer = 0; layer < 3; layer++)
        {
            var (f1, _, cell) = Noise.Cellular(u, v, 24 + layer * 6, 111 + layer, 0.9f);
            var ang = Noise.Hash(cell, layer, 5) * MathF.PI;
            var stretch = 0.45f + 0.25f * MathF.Abs(MathF.Cos(ang));
            var grain = Clamp01(1f - f1 / stretch);
            best = MathF.Max(best, grain);
        }

        var g = 0.78f + best * 0.22f;
        var shadow = best < 0.2f ? 0.82f : 1f;
        return Rgb(g * shadow, g * 0.97f * shadow, g * 0.88f * shadow);
    });

    /// <summary>Bulgur: daha iri, sarimsi-kahve taneler.</summary>
    public static Texture2D Bulgur() => Generate(256, (u, v) =>
    {
        var (f1, f2, cell) = Noise.Cellular(u, v, 40, 121, 0.9f);
        var edge = Clamp01((f2 - f1) * 3f);
        var tone = 0.8f + Noise.Hash(cell, 2, 3) * 0.25f;
        var g = (0.45f + edge * 0.4f) * tone;
        return Rgb(g * 0.95f, g * 0.72f, g * 0.42f);
    });

    /// <summary>Su yuzeyi icin hafif normal/parlaklik dokusu (gri).</summary>
    public static Texture2D Ripples() => Generate(256, (u, v) => Gray(0.5f + (Noise.Fbm(u, v, 8, 4, 131) - 0.5f) * 0.6f));

    /// <summary>Parcacik: yumusak daire (alfa).</summary>
    public static Texture2D SoftDot() => Generate(64, (u, v) =>
    {
        var dx = u - 0.5f;
        var dy = v - 0.5f;
        var d = MathF.Sqrt(dx * dx + dy * dy) * 2f;
        var a = Clamp01(1f - d);
        return new Vector4(1, 1, 1, a * a);
    }, mipmaps: false);

    /// <summary>Sahte golge lekesi (karakter alti) — yedek; gercek golge kapaliyken.</summary>
    public static Texture2D Blob() => Generate(64, (u, v) =>
    {
        var dx = u - 0.5f;
        var dy = v - 0.5f;
        var d = MathF.Sqrt(dx * dx + dy * dy) * 2f;
        return new Vector4(0, 0, 0, Clamp01(1f - d) * 0.6f);
    }, mipmaps: false);
}
