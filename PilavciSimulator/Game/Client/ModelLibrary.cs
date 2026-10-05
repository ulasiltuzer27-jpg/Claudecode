using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Client;

/// <summary>
/// Tum prosedurel modellerin onbellegi. Bir model ilk istendiginde kodla
/// uretilir; <c>Assets/Models/&lt;anahtar&gt;.glb</c> varsa onun yerine o
/// yuklenir (sanatci gercek modeli birakinca kod degismeden oyuna girer).
///
/// Butun modellerin tabani y=0'da, "yuzu" -Z'ye bakar (Entity.Forward).
/// </summary>
public sealed partial class ModelLibrary
{
    private readonly Dictionary<string, RenderModel> _cache = new(StringComparer.Ordinal);
    private readonly GlbLoader _glb;

    public ModelLibrary(MaterialLib mats) => _glb = new GlbLoader(mats);

    public RenderModel Get(string key, Action<ModelBuilder> build)
    {
        if (_cache.TryGetValue(key, out var m))
        {
            return m;
        }

        m = _glb.TryLoad(key) ?? Build(build);
        _cache[key] = m;
        return m;
    }

    private static RenderModel Build(Action<ModelBuilder> build)
    {
        var b = new ModelBuilder();
        build(b);
        return b.Build();
    }

    /// <summary>Surumler/surecler arasi sabit karma (string.GetHashCode her calistirmada degisir).</summary>
    public static uint StableHash(string s)
    {
        var h = 2166136261u;
        foreach (var ch in s)
        {
            h = (h ^ ch) * 16777619u;
        }

        return h;
    }

    public static readonly Color Steel = Gfx.Hex(0xC9CED3);
    public static readonly Color DarkSteel = Gfx.Hex(0x6E7378);
    public static readonly Color Copper = Gfx.Hex(0xB87333);
    public static readonly Color Black = Gfx.Hex(0x222222);

    // ═══════════════════════════════════════════════════════════════
    // Yardimcilar
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Yanda dikey D kulp: merkez, yaricap, kalinlik; side +1 = +X'e cikik.</summary>
    private static void SideHandle(MeshData m, Vector3 center, float radius, float thickness, float side, Color c, int segs = 10)
    {
        var start = side > 0 ? -MathF.PI / 2 : MathF.PI / 2;
        Shapes.Arc(m, Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(center), radius, thickness, start, MathF.PI, segs, 5, c);
    }

    private static void InnerBox(MeshData m, Vector3 center, Vector3 size, Color c, float uv = 1f) =>
        Shapes.InnerBox(m, Matrix4x4.CreateTranslation(center), size, c, uv);

    // ═══════════════════════════════════════════════════════════════
    // Mutfak esyalari
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Kazan govdesi (kapaksiz). Yaricap kademeye gore.</summary>
    public RenderModel Kazan(int tier) => Get($"kazan{tier}", b =>
    {
        var r = ItemInfos.KazanRadius(tier);
        var h = r * 1.15f;
        var prof = new List<Vector2>
        {
            new(0, 0.01f), new(r * 0.85f, 0), new(r * 0.97f, r * 0.04f), new(r, r * 0.12f), new(r, h), new(r - 0.012f, h), new(r - 0.012f, r * 0.12f + 0.012f),
            new(r * 0.85f - 0.01f, 0.012f), new(0, 0.013f),
        };
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, prof, 32, Color.White, 2f, smoothProfile: true);
        // Kivrik agiz ve govde bandi
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, h + 0.004f, 0), r - 0.002f, 0.011f, 32, 6, Color.White);
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, h * 0.5f, 0), r + 0.001f, 0.004f, 32, 4, Gfx.Hex(0xC9CED3));
        // D kulplar ve percinler
        foreach (var s in new[] { -1f, 1f })
        {
            SideHandle(b[M.Steel], new Vector3(s * (r + 0.006f), h * 0.8f, 0), 0.04f, 0.011f, s, Color.White);
            foreach (var dz in new[] { -0.034f, 0.034f })
            {
                Shapes.Sphere(b[M.Steel], Matrix4x4.CreateTranslation(s * r, h * 0.8f + 0.0f, dz), 0.009f, 3, 6, Gfx.Hex(0xB3B6B7));
            }
        }
    });

    public RenderModel KazanLid(int tier) => Get($"kazanlid{tier}", b =>
    {
        var r = ItemInfos.KazanRadius(tier) + 0.015f;
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, [new(r, 0), new(r * 0.9f, 0.03f), new(r * 0.4f, 0.06f), new(0, 0.065f)], 32, Color.White, 2f, smoothProfile: true);
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.002f, 0), r - 0.004f, 0.007f, 32, 4, Gfx.Hex(0xC9CED3));
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.058f, 0), [new(0.03f, 0), new(0.022f, 0.02f), new(0.035f, 0.032f), new(0.03f, 0.045f), new(0, 0.048f)], 12, Black, smoothProfile: true);
    });

    public RenderModel Tencere() => Get("tencere", b =>
    {
        const float r = 0.17f, h = 0.2f;
        var prof = new List<Vector2> { new(0, 0.005f), new(r * 0.9f, 0), new(r * 0.98f, 0.008f), new(r, 0.02f), new(r, h), new(r - 0.01f, h), new(r - 0.01f, 0.02f), new(0, 0.01f) };
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, prof, 28, Gfx.Hex(0xD6DADF), 2f, smoothProfile: true);
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, h + 0.003f, 0), r - 0.004f, 0.008f, 28, 5, Gfx.Hex(0xD6DADF));
        foreach (var s in new[] { -1f, 1f })
        {
            SideHandle(b[M.Plastic], new Vector3(s * (r + 0.004f), h * 0.82f, 0), 0.032f, 0.01f, s, Black);
        }
    });

    public RenderModel TencereLid() => Get("tencerelid", b =>
    {
        const float r = 0.18f;
        Shapes.Lathe(b[M.Glass], Matrix4x4.Identity, [new(r, 0), new(r * 0.85f, 0.025f), new(0, 0.05f)], 24, new Color(255, 255, 255, 120), 2f, smoothProfile: true);
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.002f, 0), r - 0.004f, 0.006f, 24, 4, Gfx.Hex(0xC9CED3));
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.044f, 0), [new(0.022f, 0), new(0.016f, 0.015f), new(0.026f, 0.025f), new(0.022f, 0.035f), new(0, 0.037f)], 10, Black, smoothProfile: true);
    });

    public RenderModel Suzgec() => Get("suzgec", b =>
    {
        var steel = Gfx.Hex(0xE5E8EB);
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, [new(0, 0), new(0.07f, 0), new(0.15f, 0.1f), new(0.16f, 0.11f), new(0.15f, 0.11f), new(0.07f, 0.012f), new(0, 0.012f)], 24, steel, 2f, smoothProfile: true);
        // Delikler: kase yuzeyinde koyu noktalar (halka halka)
        for (var ring = 0; ring < 4; ring++)
        {
            var t = 0.18f + ring * 0.2f;
            var rr = 0.07f + (0.15f - 0.07f) * t;
            var y = 0.1f * t;
            var n = 10 + ring * 4;
            for (var i = 0; i < n; i++)
            {
                var a = i * MathF.Tau / n + ring * 0.3f;
                Shapes.Sphere(b[M.Steel], Matrix4x4.CreateScale(1f, 1f, 1f) * Matrix4x4.CreateTranslation(MathF.Cos(a) * (rr + 0.001f), y, MathF.Sin(a) * (rr + 0.001f)), 0.0055f, 2, 4, Gfx.Hex(0x4D5656));
            }
        }

        for (var i = 0; i < 7; i++)
        {
            var a = i * MathF.Tau / 7;
            Shapes.Sphere(b[M.Steel], Matrix4x4.CreateTranslation(MathF.Cos(a) * 0.035f, 0.0005f, MathF.Sin(a) * 0.035f), 0.006f, 2, 4, Gfx.Hex(0x4D5656));
        }

        // Ayak halkasi ve kirmizi sapli tutamak
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.004f, 0), 0.062f, 0.006f, 18, 4, steel);
        Shapes.Tube(b[M.Steel], [new Vector3(0.155f, 0.105f, 0), new Vector3(0.19f, 0.108f, 0), new Vector3(0.22f, 0.11f, 0)], 0.008f, 6, steel, false, false);
        Shapes.CapsuleBetween(b[M.Plastic], new Vector3(0.2f, 0.11f, 0), new Vector3(0.3f, 0.115f, 0), 0.014f, 4, 8, Gfx.Hex(0xC0392B));
    });

    public RenderModel Jug() => Get("jug", b =>
    {
        var glass = new Color(240, 248, 255, 110);
        Shapes.Lathe(b[M.Glass], Matrix4x4.Identity, [new(0, 0), new(0.075f, 0), new(0.08f, 0.2f), new(0.085f, 0.205f), new(0.075f, 0.205f), new(0.07f, 0.005f), new(0, 0.005f)], 20, glass, 2f);
        // D kulp (yan) ve gaga
        Shapes.Arc(b[M.Glass], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateScale(1f, 1.4f, 1f) * Matrix4x4.CreateTranslation(0.082f, 0.12f, 0), 0.045f, 0.01f, -MathF.PI / 2, MathF.PI, 12, 5, new Color(240, 248, 255, 150));
        Shapes.Extrude(b[M.Glass], Matrix4x4.CreateRotationY(MathF.PI) * Matrix4x4.CreateTranslation(-0.086f, 0.195f, 0), [new(0, 0), new(0.025f, 0.012f), new(0, 0.014f)], 0.03f, glass);
        // Olcu cizgileri
        for (var i = 1; i <= 4; i++)
        {
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, i * 0.04f, -0.079f), new Vector3(i % 2 == 0 ? 0.035f : 0.02f, 0.003f, 0.003f), Gfx.Hex(0x2E86C1));
        }
    });

    /// <summary>Kepce: tahta sapli metal kase.</summary>
    public RenderModel Spoon() => Get("kasik", b =>
    {
        Shapes.Lathe(b[M.Steel], Matrix4x4.CreateRotationZ(MathF.PI) * Matrix4x4.CreateTranslation(0, 0.045f, -0.15f),
            [new(0, 0), new(0.035f, 0.004f), new(0.05f, 0.02f), new(0.055f, 0.04f), new(0.05f, 0.042f), new(0.046f, 0.024f), new(0.032f, 0.009f), new(0, 0.005f)], 16, Gfx.Hex(0xD6DADF), 2f, smoothProfile: true);
        var handle = Shapes.Curve(new Vector3(0, 0.045f, -0.1f), new Vector3(0, 0.07f, -0.02f), new Vector3(0, 0.05f, 0.14f), 8);
        Shapes.Tube(b[M.Steel], handle, 0.007f, 6, Gfx.Hex(0xC9CED3));
        Shapes.CapsuleBetween(b[M.Wood], new Vector3(0, 0.05f, 0.08f), new Vector3(0, 0.048f, 0.2f), 0.014f, 4, 8, Gfx.Hex(0xC69C6D));
    });

    public RenderModel SaltBox() => Get("tuz", b =>
    {
        Shapes.Lathe(b[M.Paper], Matrix4x4.Identity, [new(0, 0), new(0.044f, 0), new(0.046f, 0.01f), new(0.046f, 0.14f), new(0.044f, 0.15f), new(0, 0.15f)], 18, Gfx.Hex(0x2E86C1), smoothProfile: true);
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.145f, 0), [new(0.047f, 0), new(0.048f, 0.02f), new(0.03f, 0.03f), new(0, 0.032f)], 18, Gfx.Hex(0xF4F6F7), smoothProfile: true);
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, 0.08f, -0.0465f), new Vector3(0.05f, 0.045f, 0.002f), Gfx.Hex(0xF4F6F7));
        for (var i = 0; i < 5; i++)
        {
            var a = i * MathF.Tau / 5;
            Shapes.Sphere(b[M.Plastic], Matrix4x4.CreateTranslation(MathF.Cos(a) * 0.012f, 0.177f, MathF.Sin(a) * 0.012f), 0.0025f, 2, 4, Gfx.Hex(0x566573));
        }
    });

    public RenderModel Butter() => Get("tereyagi", b =>
    {
        Shapes.RoundedBox(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.025f, 0), new Vector3(0.12f, 0.05f, 0.075f), 0.008f, 2, Gfx.Hex(0xF5E6A8));
        Shapes.RoundedBox(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.016f, 0), new Vector3(0.123f, 0.032f, 0.078f), 0.008f, 2, Gfx.Hex(0x3C7FB1));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.0505f, 0), new Vector3(0.06f, 0.002f, 0.04f), Gfx.Hex(0xF4F6F7));
    });

    public RenderModel ChickenPack() => Get("tavukpaketi", b =>
    {
        Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.015f, 0), new Vector3(0.24f, 0.03f, 0.17f), 0.01f, 1, Gfx.Hex(0xF2F3F4));
        Shapes.Sphere(b[M.White], Matrix4x4.CreateScale(1.4f, 0.45f, 0.9f) * Matrix4x4.CreateTranslation(-0.04f, 0.038f, 0), 0.06f, 6, 10, Gfx.Hex(0xF1C6B5));
        Shapes.Sphere(b[M.White], Matrix4x4.CreateScale(1.3f, 0.42f, 0.85f) * Matrix4x4.CreateTranslation(0.05f, 0.036f, 0.015f), 0.055f, 6, 10, Gfx.Hex(0xEDB9A6));
        Shapes.RoundedBox(b[M.Glass], Matrix4x4.CreateTranslation(0, 0.046f, 0), new Vector3(0.242f, 0.03f, 0.172f), 0.012f, 1, new Color(255, 255, 255, 50));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0.06f, 0.062f, -0.04f), new Vector3(0.07f, 0.002f, 0.045f), Gfx.Hex(0xF4D03F));
    });

    public RenderModel MeatPack() => Get("etpaketi", b =>
    {
        Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.015f, 0), new Vector3(0.22f, 0.03f, 0.16f), 0.01f, 1, Gfx.Hex(0xF2F3F4));
        for (var i = 0; i < 6; i++)
        {
            Shapes.RoundedBox(b[M.White], Matrix4x4.CreateRotationY(i * 0.4f) * Matrix4x4.CreateTranslation(-0.06f + (i % 3) * 0.06f, 0.045f, -0.03f + i / 3 * 0.06f), new Vector3(0.045f, 0.035f, 0.045f), 0.01f, 1, i % 2 == 0 ? Gfx.Hex(0xA93226) : Gfx.Hex(0x922B21));
        }

        Shapes.RoundedBox(b[M.Glass], Matrix4x4.CreateTranslation(0, 0.05f, 0), new Vector3(0.222f, 0.04f, 0.162f), 0.012f, 1, new Color(255, 255, 255, 45));
    });

    public RenderModel Tray() => Get("tepsi", b =>
    {
        Shapes.RoundedBox(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.005f, 0), new Vector3(0.4f, 0.01f, 0.28f), 0.004f, 1, Color.White);
        Shapes.Loft(b[M.Steel], Matrix4x4.CreateRotationX(-MathF.PI / 2),
        [
            new Shapes.LoftSection(0.0f, 0, 0.2f, 0.14f, 0.03f),
            new Shapes.LoftSection(0.05f, 0, 0.205f, 0.145f, 0.032f),
        ], 3, Color.White, false, false);
        Shapes.Torus(b[M.Steel], Matrix4x4.CreateScale(1.42f, 1f, 1f) * Matrix4x4.CreateTranslation(0, 0.05f, 0), 0.145f, 0.005f, 24, 4, Color.White);
    });

    public RenderModel Plate() => Get("tabak", b =>
    {
        Shapes.Lathe(b[M.Ceramic], Matrix4x4.Identity, [new(0, 0.005f), new(0.07f, 0), new(0.1f, 0.015f), new(0.125f, 0.03f), new(0.13f, 0.034f), new(0.12f, 0.034f), new(0.095f, 0.022f), new(0.065f, 0.012f), new(0, 0.013f)], 28, Gfx.Hex(0xFBFCFC), 2f, smoothProfile: true);
        Shapes.Torus(b[M.White], Matrix4x4.CreateTranslation(0, 0.032f, 0), 0.118f, 0.0035f, 28, 4, Gfx.Hex(0x2E86C1));
        Shapes.Torus(b[M.White], Matrix4x4.CreateTranslation(0, 0.0255f, 0), 0.105f, 0.0018f, 28, 3, Gfx.Hex(0x2E86C1));
    });

    public RenderModel Package() => Get("paket", b =>
    {
        Shapes.Loft(b[M.Foam], Matrix4x4.CreateRotationX(-MathF.PI / 2),
        [
            new Shapes.LoftSection(0.0f, 0, 0.095f, 0.075f, 0.02f),
            new Shapes.LoftSection(0.06f, 0, 0.112f, 0.088f, 0.024f),
        ], 3, Gfx.Hex(0xFDFEFE), true, false);
        Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.066f, 0), new Vector3(0.232f, 0.012f, 0.182f), 0.005f, 1, Gfx.Hex(0xF4F6F7));
        Shapes.Box(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.0725f, 0), new Vector3(0.2f, 0.002f, 0.004f), Gfx.Hex(0xD5D8DC));
    });

    public RenderModel Box(string supplyId) => Get("koli_" + supplyId, b =>
    {
        var col = supplyId switch
        {
            var s when s.StartsWith("pirinc", StringComparison.Ordinal) => Gfx.Hex(0xEAD9B8),
            var s when s.StartsWith("ayran", StringComparison.Ordinal) => Gfx.Hex(0xD6EAF8),
            "tup" => Gfx.Hex(0x5D6D7E),
            _ => Gfx.Hex(0xC8A27A),
        };
        if (supplyId == "tup")
        {
            GasBottle(b, Vector3.Zero, 0.85f);
            return;
        }

        if (supplyId.StartsWith("pirinc", StringComparison.Ordinal) || supplyId.StartsWith("bulgur", StringComparison.Ordinal) ||
            supplyId.StartsWith("nohut", StringComparison.Ordinal) || supplyId.StartsWith("fasulye", StringComparison.Ordinal))
        {
            // Cuval: bagli agiz, etiket
            var label = supplyId.StartsWith("pirinc", StringComparison.Ordinal) ? Gfx.Hex(0x1E8449) : Gfx.Hex(0xB9770E);
            Shapes.Lathe(b[M.Fabric], Matrix4x4.CreateScale(1.1f, 1f, 0.8f), [new(0, 0), new(0.16f, 0.01f), new(0.2f, 0.12f), new(0.19f, 0.3f), new(0.12f, 0.38f), new(0.05f, 0.41f), new(0.07f, 0.45f), new(0.0f, 0.44f)], 14, Gfx.Hex(0xE8DCC0), 2f, smoothProfile: true);
            Shapes.Torus(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.41f, 0), 0.05f, 0.008f, 10, 4, Gfx.Hex(0x7E5109));
            Shapes.Box(b[M.Fabric], Matrix4x4.CreateRotationX(0.1f) * Matrix4x4.CreateTranslation(0, 0.2f, -0.158f), new Vector3(0.18f, 0.12f, 0.006f), label);
            return;
        }

        Shapes.RoundedBoxOnGround(b[M.Paper], Matrix4x4.Identity, new Vector3(0.5f, 0.36f, 0.4f), 0.012f, 1, col);
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.361f, 0), new Vector3(0.002f, 0.002f, 0.402f), Gfx.Hex(0x8C6D46));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.3605f, 0), new Vector3(0.08f, 0.002f, 0.41f), Gfx.Hex(0xD4AC0D));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.31f, 0.2055f), new Vector3(0.08f, 0.1f, 0.002f), Gfx.Hex(0xD4AC0D));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0.13f, 0.2f, -0.2015f), new Vector3(0.14f, 0.1f, 0.002f), Gfx.Hex(0xF4F6F7));
    });

    // ═══════════════════════════════════════════════════════════════
    // Icerikler (doluluk)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Birim disk: icerik yuzeyi (olceklenerek kullanilir).</summary>
    public RenderModel Surface(int material, string key, Color color) => Get("surf_" + key, b => Shapes.Disc(b[material], Matrix4x4.Identity, 1f, 28, color, 6f));

    /// <summary>Kubbe: tabaktaki pilav yigini.</summary>
    public RenderModel Mound(int material, string key, Color color) => Get("mound_" + key, b =>
        Shapes.Lathe(b[material], Matrix4x4.Identity, [new(1f, 0), new(0.92f, 0.35f), new(0.7f, 0.75f), new(0.38f, 0.95f), new(0, 1f)], 18, color, 4f, smoothProfile: true));

    /// <summary>Kucuk taneler (nohut/fasulye/tavuk parcasi) kumesi.</summary>
    public RenderModel Bits(string key, Color color, float size, int count, bool round) => Get("bits_" + key, b =>
    {
        var rng = new Rng(StableHash(key));
        for (var i = 0; i < count; i++)
        {
            var a = rng.NextFloat() * MathF.Tau;
            var r = MathF.Sqrt(rng.NextFloat()) * 0.8f;
            var p = new Vector3(MathF.Cos(a) * r, rng.Range(0f, 0.25f), MathF.Sin(a) * r);
            var c = Gfx.Lerp(color, Color.White, rng.NextFloat() * 0.15f);
            if (round)
            {
                Shapes.Sphere(b[M.White], Matrix4x4.CreateTranslation(p), size, 3, 6, c);
            }
            else
            {
                Shapes.Box(b[M.White], Matrix4x4.CreateRotationY(rng.NextFloat() * 3) * Matrix4x4.CreateTranslation(p), new Vector3(size * 2.2f, size * 0.8f, size), c);
            }
        }
    });

    // ═══════════════════════════════════════════════════════════════
    // Istasyonlar
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Mavi tup gaz: taban <paramref name="basePos"/>, yukseklik ~0.55*olcek; vana ustte.</summary>
    private static void GasBottle(ModelBuilder b, Vector3 basePos, float scale = 1f)
    {
        var xf = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(basePos);
        var blue = Gfx.Hex(0x2E86C1);
        // Taban halkasi, govde, omuz, boyun halkasi (tutamak)
        Shapes.Lathe(b[M.Metal], xf, [new(0.13f, 0), new(0.145f, 0.005f), new(0.15f, 0.04f), new(0.145f, 0.045f)], 18, Gfx.Hex(0x1F5F8B));
        Shapes.Lathe(b[M.Metal], xf, [new(0, 0.04f), new(0.15f, 0.045f), new(0.16f, 0.07f), new(0.16f, 0.38f), new(0.14f, 0.45f), new(0.09f, 0.49f), new(0.035f, 0.5f), new(0, 0.5f)], 20, blue, smoothProfile: true);
        Shapes.Lathe(b[M.Metal], xf, [new(0.09f, 0.48f), new(0.1f, 0.5f), new(0.1f, 0.58f), new(0.085f, 0.6f), new(0.08f, 0.6f), new(0.085f, 0.5f)], 16, blue);
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.56f, -0.1f) * xf, new Vector3(0.07f, 0.03f, 0.012f), Gfx.Hex(0x1A1A1A));
        // Vana ve regulator
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.5f, 0) * xf, 0.022f, 0.06f, 10, Gfx.Hex(0xB7950B));
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.56f, 0) * xf, 0.035f, 0.035f, 12, DarkSteel);
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.575f, 0) * xf, 0.012f, 0.06f, 8, Gfx.Hex(0xC0392B));
        // Uyari etiketi
        Shapes.Lathe(b[M.Paper], xf, [new(0.161f, 0.2f), new(0.161f, 0.3f)], 20, Gfx.Hex(0xF4F6F7), 1f, false, -MathF.PI * 0.85f, MathF.PI * 0.7f);
    }

    public RenderModel KazanOcagi() => Get("kazanocagi", b =>
    {
        var iron = Gfx.Hex(0x2B2B2B);
        var dark = Gfx.Hex(0x161616);
        // Dokum govde: ayak halkasi, hafif konik dis duvar, ust dudak, ic duvar
        Shapes.Lathe(b[M.Metal], Matrix4x4.Identity,
            [new(0.3f, 0), new(0.305f, 0.03f), new(0.28f, 0.045f), new(0.265f, 0.33f), new(0.29f, 0.355f), new(0.29f, 0.38f), new(0.245f, 0.385f), new(0.235f, 0.3f)], 28, iron);
        Shapes.Disc(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.3f, 0), 0.236f, 24, dark);
        // Havalandirma delikleri
        for (var i = 0; i < 6; i++)
        {
            var a = i * MathF.Tau / 6 + 0.5f;
            Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateRotationY(MathF.PI / 2 - a) * Matrix4x4.CreateTranslation(MathF.Cos(a) * 0.272f, 0.16f, MathF.Sin(a) * 0.272f), new Vector3(0.05f, 0.1f, 0.012f), 0.005f, 1, dark);
        }

        // Brulor: alev halkasi ve tac
        Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.3f, 0), [new(0.04f, 0), new(0.1f, 0.03f), new(0.1f, 0.06f), new(0.07f, 0.065f), new(0.0f, 0.07f)], 20, Gfx.Hex(0x4A4A4A));
        Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.36f, 0), 0.09f, 0.012f, 20, 5, Gfx.Hex(0x5E5E5E));
        // Kazan ayaklari: ust dudaktan ice uzanan 3 kol (kazan 0.42'de oturur)
        for (var i = 0; i < 3; i++)
        {
            var a = i * MathF.Tau / 3 + MathF.PI / 6;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            Shapes.Tube(b[M.Metal], [dir * 0.29f + new Vector3(0, 0.37f, 0), dir * 0.25f + new Vector3(0, 0.405f, 0), dir * 0.15f + new Vector3(0, 0.405f, 0)], 0.014f, 6, dark);
        }

        // On panel: dugme yuvasi
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.22f, -0.285f), new Vector3(0.16f, 0.13f, 0.05f), 0.012f, 2, DarkSteel);
        Shapes.Torus(b[M.Metal], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.22f, -0.311f), 0.042f, 0.004f, 16, 4, Steel);
        // Tup ve sarkik hortum (tupun vanasindan ocagin yan girisine)
        GasBottle(b, new Vector3(0.48f, 0, 0.1f), 0.95f);
        Shapes.Tube(b[M.Rubber], Shapes.Curve(new Vector3(0.48f, 0.56f, 0.065f), new Vector3(0.45f, 0.7f, -0.15f), new Vector3(0.42f, -0.05f, -0.12f), new Vector3(0.275f, 0.12f, -0.06f), 16), 0.012f, 6, Gfx.Hex(0x1C1C1C));
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.3f, 0.12f, -0.06f), 0.02f, 0.03f, 8, Gfx.Hex(0xB7950B));
        Shapes.ShadeByHeight(b[M.Metal], 0, 0f, 0.4f, 0.2f);
    });

    public RenderModel Knob() => Get("knob", b =>
    {
        // Duvara oturan pul, govde ve tirtikli kavrama (-Z'ye cikar, Z ekseninde doner)
        var toFront = Matrix4x4.CreateRotationX(-MathF.PI / 2);
        Shapes.Cylinder(b[M.Metal], toFront * Matrix4x4.CreateTranslation(0, 0, 0.008f), 0.042f, 0.008f, 18, Gfx.Hex(0x9AA0A6));
        Shapes.Lathe(b[M.Plastic], toFront, [new(0.034f, 0), new(0.036f, 0.004f), new(0.033f, 0.024f), new(0.028f, 0.03f), new(0, 0.031f)], 18, Black, smoothProfile: true);
        for (var i = 0; i < 12; i++)
        {
            var a = i * MathF.Tau / 12;
            Shapes.Box(b[M.Plastic], Matrix4x4.CreateRotationZ(a) * Matrix4x4.CreateTranslation(MathF.Cos(a + MathF.PI / 2) * 0.035f, MathF.Sin(a + MathF.PI / 2) * 0.035f, -0.013f), new Vector3(0.004f, 0.006f, 0.02f), Gfx.Hex(0x333333));
        }

        // Kavrama kulakcigi ve beyaz isaret
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0, -0.04f), new Vector3(0.014f, 0.058f, 0.02f), 0.006f, 1, Gfx.Hex(0x2C2C2C));
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, 0.02f, -0.0505f), new Vector3(0.005f, 0.018f, 0.002f), Color.White);
    });

    public RenderModel Stovetop(int tier) => Get($"stovetop{tier}", b =>
    {
        var w = tier >= 1 ? 1.16f : 0.84f;
        // Paslanmaz kasa (pahli), siyah cam ust, on kontrol seridi
        Shapes.RoundedBox(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.03f, 0), new Vector3(w, 0.06f, 0.62f), 0.014f, 2, Gfx.Hex(0xBFC5CA));
        Shapes.RoundedBox(b[M.Glass], Matrix4x4.CreateTranslation(0, 0.0605f, 0.03f), new Vector3(w - 0.06f, 0.004f, 0.52f), 0.01f, 1, new Color(25, 28, 32, 255));
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.03f, -0.3105f), new Vector3(w - 0.06f, 0.035f, 0.002f), Gfx.Hex(0x8E959C));
        var n = tier >= 1 ? 4 : 2;
        var step = tier >= 1 ? 0.28f : 0.48f;
        for (var i = 0; i < n; i++)
        {
            var x = (i - (n - 1) / 2f) * step;
            var c = new Vector3(x, 0.062f, -0.02f);
            // Brulor kasesi, tac ve kapak
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(c), 0.075f, 0.002f, 20, Gfx.Hex(0x9AA0A6));
            Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(c), [new(0.055f, 0), new(0.056f, 0.006f), new(0.05f, 0.007f), new(0.045f, 0.004f), new(0, 0.004f)], 18, Gfx.Hex(0x3A3A3A));
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(c + new Vector3(0, 0.004f, 0)), 0.038f, 0.004f, 16, Gfx.Hex(0x1E1E1E));
            // Dokum izgara: halka + dort kol (ust yuzey 0.07'de; tencere oraya oturur)
            Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(c + new Vector3(0, 0.003f, 0)), 0.115f, 0.005f, 20, 4, Gfx.Hex(0x1A1A1A));
            for (var k = 0; k < 4; k++)
            {
                var a = k * MathF.PI / 2 + MathF.PI / 4;
                var d = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                Shapes.Tube(b[M.Metal], [c + d * 0.12f + new Vector3(0, 0.002f, 0), c + d * 0.1f + new Vector3(0, 0.0035f, 0), c + d * 0.055f + new Vector3(0, 0.0035f, 0)], 0.0045f, 4, Gfx.Hex(0x1A1A1A), true, true);
            }

            // Kontrol seridinde isaret noktasi
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(x + 0.05f, 0.045f, -0.312f), new Vector3(0.012f, 0.004f, 0.002f), Color.White);
        }
    });

    public RenderModel Sink() => Get("sink", b =>
    {
        // Dolap tezgahla ayni derinlikte (z -0.37..0.35); ust yuzu yok ki tekne gorunsun.
        var cab = Gfx.Hex(0xB9A58B);
        var door = Gfx.Hex(0xCDB998);
        var steel = Gfx.Hex(0xE7EAED);
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.08f, -0.01f), new Vector3(0.9f, 0.77f, 0.72f), cab, 1f, Shapes.Faces.All & ~Shapes.Faces.PosY);
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0, -0.005f), new Vector3(0.88f, 0.08f, 0.69f), Gfx.Hex(0x3B3127));
        foreach (var x in new[] { -0.215f, 0.215f })
        {
            Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.47f, -0.375f), new Vector3(0.42f, 0.7f, 0.02f), 0.008f, 1, door, 1f);
            Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.47f, -0.386f), new Vector3(0.32f, 0.58f, 0.004f), 0.004f, 1, Gfx.Lerp(door, cab, 0.6f), 1f);
            var hx = x - MathF.Sign(x) * 0.16f;
            Shapes.Tube(b[M.Metal], [new Vector3(hx, 0.62f, -0.385f), new Vector3(hx, 0.62f, -0.41f), new Vector3(hx, 0.74f, -0.41f), new Vector3(hx, 0.74f, -0.385f)], 0.007f, 6, Steel);
        }

        // Paslanmaz tezgah: teknenin cevresinde dort serit (ic kenarlari tekne agzi olur)
        foreach (var (x, z, sw, sd) in new[] { (0f, -0.295f, 0.9f, 0.15f), (0f, 0.285f, 0.9f, 0.13f), (-0.375f, 0f, 0.15f, 0.44f), (0.375f, 0f, 0.15f, 0.44f) })
        {
            Shapes.BoxOnGround(b[M.Steel], Matrix4x4.CreateTranslation(x, 0.85f, z), new Vector3(sw, 0.05f, sd), steel);
        }

        // Tekne (ice bakan), suzgec deligi
        InnerBox(b[M.Steel], new Vector3(0, 0.815f, 0), new Vector3(0.6f, 0.07f, 0.44f), Gfx.Hex(0xB6BEC6));
        Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.782f, 0.05f), 0.032f, 0.004f, 14, 4, Gfx.Hex(0x8E959C));
        Shapes.Disc(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.781f, 0.05f), 0.03f, 14, Gfx.Hex(0x2B2B2B));
        // Kugu boyun musluk: govde, kavis, perlator (su 1.16'dan akar), kol
        var tap = new Vector3(0, 0.9f, 0.28f);
        Shapes.Cylinder(b[M.Steel], Matrix4x4.CreateTranslation(tap), 0.035f, 0.02f, 16, Color.White);
        Shapes.Cylinder(b[M.Steel], Matrix4x4.CreateTranslation(tap + new Vector3(0, 0.02f, 0)), 0.024f, 0.12f, 14, Color.White);
        var neck = new List<Vector3> { tap + new Vector3(0, 0.14f, 0) };
        neck.AddRange(Shapes.Curve(tap + new Vector3(0, 0.14f, 0), new Vector3(0, 1.33f, 0.28f), new Vector3(0, 1.33f, 0.06f), new Vector3(0, 1.185f, 0.06f), 18));
        Shapes.Tube(b[M.Steel], neck, 0.014f, 10, Color.White);
        Shapes.Cylinder(b[M.Steel], Matrix4x4.CreateTranslation(0, 1.162f, 0.06f), 0.017f, 0.025f, 12, Gfx.Hex(0xAEB6BF));
        Shapes.CapsuleBetween(b[M.Steel], tap + new Vector3(0.03f, 0.1f, 0), tap + new Vector3(0.1f, 0.12f, -0.02f), 0.009f, 2, 8, Color.White);
        // Tezgah ustu: deterjan sisesi ve sunger
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateScale(1f, 1f, 0.7f) * Matrix4x4.CreateTranslation(-0.38f, 0.9f, 0.27f), [new(0, 0), new(0.035f, 0), new(0.038f, 0.12f), new(0.02f, 0.16f), new(0.012f, 0.17f), new(0.012f, 0.19f), new(0, 0.19f)], 14, Gfx.Hex(0x58D68D), smoothProfile: true);
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(-0.38f, 0.97f, 0.244f), new Vector3(0.05f, 0.06f, 0.002f), Gfx.Hex(0xF9E79F));
        Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateRotationY(0.3f) * Matrix4x4.CreateTranslation(0.38f, 0.915f, -0.05f), new Vector3(0.1f, 0.022f, 0.065f), 0.008f, 1, Gfx.Hex(0xF4D03F));
        Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateRotationY(0.3f) * Matrix4x4.CreateTranslation(0.38f, 0.93f, -0.05f), new Vector3(0.1f, 0.01f, 0.065f), 0.004f, 1, Gfx.Hex(0x27AE60));
    });

    public RenderModel CuttingBoard() => Get("board", b =>
    {
        var wood = Gfx.Hex(0xD7B98E);
        var groove = Gfx.Hex(0xBE9D6E);
        Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.015f, 0), new Vector3(0.6f, 0.03f, 0.4f), 0.012f, 2, wood, 2f);
        // Meyve suyu olugu ve asma deligi
        foreach (var (x, z, sx, sz) in new[] { (0f, -0.165f, 0.52f, 0.008f), (0f, 0.165f, 0.52f, 0.008f), (-0.26f, 0f, 0.008f, 0.33f), (0.26f, 0f, 0.008f, 0.33f) })
        {
            Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.0302f, z), new Vector3(sx, 0.0006f, sz), groove);
        }

        Shapes.Disc(b[M.Wood], Matrix4x4.CreateTranslation(-0.282f, 0.0303f, 0), 0.011f, 12, Gfx.Hex(0x3A2E22));
        // Bicak (arka kenarda yatik)
        var blade = new List<Vector2> { new(0, 0), new(0.13f, -0.002f), new(0.18f, 0.01f), new(0.205f, 0.03f), new(0, 0.038f) };
        Shapes.Extrude(b[M.Steel], Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0.02f, 0.0315f, 0.185f), blade, 0.003f, Gfx.Hex(0xDDE2E6));
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(-0.035f, 0.04f, 0.166f), new Vector3(0.11f, 0.02f, 0.026f), 0.009f, 2, Gfx.Hex(0x1C1C1C));
        foreach (var rx in new[] { -0.065f, -0.035f, -0.005f })
        {
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(rx, 0.049f, 0.166f), 0.0035f, 0.002f, 6, Steel);
        }
    });

    public RenderModel Fridge() => Get("fridge", b =>
    {
        var white = Gfx.Hex(0xF2F3F4);
        var shade = Gfx.Hex(0xE3E6E8);
        // Govde (kapaklar onunde), ayaklar
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.97f, 0.03f), new Vector3(0.76f, 1.84f, 0.66f), 0.03f, 2, shade);
        foreach (var (fx, fz) in new[] { (-0.32f, -0.26f), (0.32f, -0.26f), (-0.32f, 0.3f), (0.32f, 0.3f) })
        {
            Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(fx, 0, fz), 0.025f, 0.05f, 8, Gfx.Hex(0x2C2C2C));
        }

        // Kapaklar: ust dondurucu, alt sogutucu; aradaki conta boslugu
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 1.585f, -0.33f), new Vector3(0.75f, 0.59f, 0.05f), 0.02f, 2, white);
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.665f, -0.33f), new Vector3(0.75f, 1.21f, 0.05f), 0.02f, 2, white);
        Shapes.Box(b[M.Rubber], Matrix4x4.CreateTranslation(0, 1.28f, -0.32f), new Vector3(0.73f, 0.02f, 0.03f), Gfx.Hex(0x5D6D7E));
        // Boru kulplar (sag kenar)
        foreach (var (y0, y1) in new[] { (1.32f, 1.52f), (0.98f, 1.22f) })
        {
            Shapes.Tube(b[M.Metal], [new Vector3(0.31f, y0, -0.35f), new Vector3(0.31f, y0, -0.385f), new Vector3(0.31f, y1, -0.385f), new Vector3(0.31f, y1, -0.35f)], 0.011f, 8, DarkSteel);
        }

        // Rozet, havalandirma izgarasi
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 1.82f, -0.357f), new Vector3(0.12f, 0.025f, 0.004f), 0.006f, 1, Steel);
        for (var i = 0; i < 5; i++)
        {
            Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.08f + i * 0.012f, -0.306f), new Vector3(0.6f, 0.005f, 0.004f), Gfx.Hex(0x7F8C8D));
        }

        // Kapaktaki etiketler (ne nerede): renkli kart + yazi cizgisi
        foreach (var (y, c) in new[] { (1.45f, Gfx.Hex(0xF4D03F)), (1.05f, Gfx.Hex(0xF1948A)), (0.65f, Gfx.Hex(0xC0392B)) })
        {
            Shapes.RoundedBox(b[M.White], Matrix4x4.CreateTranslation(-0.15f, y, -0.3565f), new Vector3(0.2f, 0.12f, 0.004f), 0.01f, 1, c);
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.15f, y + 0.015f, -0.359f), new Vector3(0.14f, 0.014f, 0.001f), Color.White);
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.17f, y - 0.02f, -0.359f), new Vector3(0.1f, 0.01f, 0.001f), new Color(255, 255, 255, 200));
        }

        // Miknatislar ve tutturulmus fis
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0.12f, 0.42f, -0.355f), 0.02f, 0.01f, 10, Gfx.Hex(0x2E86C1));
        Shapes.Box(b[M.Paper], Matrix4x4.CreateRotationZ(0.08f) * Matrix4x4.CreateTranslation(0.12f, 0.33f, -0.3565f), new Vector3(0.09f, 0.16f, 0.002f), Gfx.Hex(0xFDFEFE));
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0.16f, 1.68f, -0.355f), 0.018f, 0.01f, 10, Gfx.Hex(0xE74C3C));
    });

    public RenderModel Pantry() => Get("pantry", b =>
    {
        var wood = Gfx.Hex(0x8B6B4A);
        var dark = Gfx.Hex(0x6E5338);
        // Dikmeler (dort kose) ve yan capraz baglantilar
        foreach (var x in new[] { -1.08f, 1.08f })
        {
            foreach (var z in new[] { -0.12f, 0.31f })
            {
                Shapes.RoundedBoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x, 0, z), new Vector3(0.05f, 1.8f, 0.05f), 0.008f, 1, dark);
            }

            Shapes.Beam(b[M.Wood], new Vector3(x, 0.1f, -0.12f), new Vector3(x, 0.9f, 0.31f), 0.025f, dark);
            Shapes.Beam(b[M.Wood], new Vector3(x, 1.0f, 0.31f), new Vector3(x, 1.65f, -0.12f), 0.025f, dark);
        }

        // Raflar: tahta kalaslar (aralarinda ince bosluk) + on dudak
        foreach (var y in new[] { 0.05f, 0.95f, 1.7f })
        {
            for (var k = 0; k < 3; k++)
            {
                var z = -0.11f + k * 0.165f;
                Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(0, y, z + 0.0775f), new Vector3(2.12f, 0.04f, 0.155f), k % 2 == 0 ? wood : Gfx.Lerp(wood, dark, 0.25f), 2f);
            }

            Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(0, y + 0.03f, -0.155f), new Vector3(2.12f, 0.03f, 0.02f), dark);
        }

        // Arka pano: dikey tahtalar
        for (var i = 0; i < 11; i++)
        {
            var x = -1.0f + i * 0.2f;
            Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.9f, 0.34f), new Vector3(0.195f, 1.8f, 0.02f), i % 2 == 0 ? dark : Gfx.Lerp(dark, wood, 0.25f), 1f);
        }

        // Ust raf susleri: kavanozlar (kurutulmus biber, sogan) ve sepet
        var jars = new[] { (-0.85f, Gfx.Hex(0xC0392B)), (-0.68f, Gfx.Hex(0xF5CBA7)), (-0.52f, Gfx.Hex(0x7D6608)) };
        foreach (var (x, fill) in jars)
        {
            Shapes.Cylinder(b[M.Fabric], Matrix4x4.CreateTranslation(x, 1.72f, 0.1f), 0.055f, 0.12f, 12, fill);
            Shapes.Lathe(b[M.Glass], Matrix4x4.CreateTranslation(x, 1.72f, 0.1f), [new(0, 0), new(0.06f, 0), new(0.062f, 0.16f), new(0.045f, 0.18f), new(0, 0.18f)], 14, new Color(220, 235, 240, 90), smoothProfile: true);
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(x, 1.9f, 0.1f), 0.047f, 0.025f, 12, Gfx.Hex(0xD4AC0D));
        }

        Shapes.Lathe(b[M.Fabric], Matrix4x4.CreateTranslation(0.75f, 1.72f, 0.1f), [new(0, 0), new(0.16f, 0), new(0.2f, 0.16f), new(0.21f, 0.17f), new(0.19f, 0.17f), new(0.15f, 0.015f), new(0, 0.015f)], 16, Gfx.Hex(0xB9894F), 3f);
        var rng = new Rng(77u);
        for (var i = 0; i < 6; i++)
        {
            var a = rng.NextFloat() * MathF.Tau;
            var r = rng.Range(0.02f, 0.11f);
            Shapes.Sphere(b[M.Fabric], Matrix4x4.CreateScale(1f, 0.85f, 1f) * Matrix4x4.CreateTranslation(0.75f + MathF.Cos(a) * r, 1.8f + rng.Range(0f, 0.03f), 0.1f + MathF.Sin(a) * r), 0.045f, 4, 8, Gfx.Hex(0xAF601A));
        }
    });

    public RenderModel Sack(Color label) => Get($"sack_{label.R}_{label.G}_{label.B}", b =>
    {
        var jute = Gfx.Hex(0xE6D7B5);
        // Sismis govde, bogaz, kivrik agiz; agzi ip ile bagli
        Shapes.Lathe(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.8f),
            [new(0, 0), new(0.19f, 0.005f), new(0.235f, 0.08f), new(0.245f, 0.28f), new(0.225f, 0.48f), new(0.15f, 0.58f), new(0.085f, 0.62f), new(0.1f, 0.66f), new(0.13f, 0.7f), new(0.0f, 0.68f)], 14, jute, 2f, smoothProfile: true);
        Shapes.Torus(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.8f) * Matrix4x4.CreateTranslation(0, 0.615f, 0), 0.085f, 0.009f, 12, 4, Gfx.Hex(0x7E5109));
        Shapes.Tube(b[M.Fabric], [new Vector3(0.07f, 0.615f, -0.04f), new Vector3(0.11f, 0.58f, -0.08f), new Vector3(0.12f, 0.52f, -0.09f)], 0.006f, 4, Gfx.Hex(0x7E5109));
        // Dikis cizgisi ve basili etiket (ortada beyaz bant)
        Shapes.Lathe(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.8f), [new(0.247f, 0.36f), new(0.245f, 0.38f)], 14, Gfx.Lerp(jute, label, 0.35f), 1f, false, 0f, MathF.Tau);
        Shapes.Box(b[M.Fabric], Matrix4x4.CreateRotationX(0.04f) * Matrix4x4.CreateTranslation(0, 0.25f, -0.195f), new Vector3(0.22f, 0.15f, 0.008f), label);
        Shapes.Box(b[M.Fabric], Matrix4x4.CreateRotationX(0.04f) * Matrix4x4.CreateTranslation(0, 0.255f, -0.2f), new Vector3(0.16f, 0.035f, 0.002f), Gfx.Hex(0xFDFEFE));
    });

    public RenderModel CannedShelf() => Get("canned", b =>
    {
        for (var i = 0; i < 6; i++)
        {
            var at = Matrix4x4.CreateTranslation(-0.25f + (i % 3) * 0.25f, 0, (i / 3) * 0.12f);
            // Teneke: kenar bombeleri, kirmizi etiket, ust kapak halkasi
            Shapes.Lathe(b[M.Metal], at, [new(0, 0), new(0.046f, 0), new(0.05f, 0.006f), new(0.05f, 0.124f), new(0.046f, 0.13f), new(0, 0.13f)], 14, Steel);
            Shapes.Lathe(b[M.Paper], at, [new(0.0505f, 0.018f), new(0.0505f, 0.112f)], 14, Gfx.Hex(0xC0392B));
            Shapes.Lathe(b[M.Paper], at, [new(0.051f, 0.05f), new(0.051f, 0.08f)], 14, Gfx.Hex(0xF4D03F), 1f, false, -MathF.PI * 0.8f, MathF.PI * 0.6f);
            Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.128f, 0) * at, 0.03f, 0.003f, 12, 3, DarkSteel);
        }
    });

    public RenderModel Laptop() => Get("laptop", b =>
    {
        var shell = Gfx.Hex(0x2C3E50);
        // Govde, klavye tuslari, dokunmatik alan
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.01f, 0), new Vector3(0.36f, 0.02f, 0.25f), 0.008f, 2, shell);
        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 12; c++)
            {
                Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(-0.143f + c * 0.026f, 0.0205f, -0.005f + r * 0.022f), new Vector3(0.021f, 0.002f, 0.018f), Gfx.Hex(0x1B2631));
            }
        }

        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.0205f, 0.104f), new Vector3(0.14f, 0.002f, 0.016f), Gfx.Hex(0x1B2631));
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.0202f, -0.075f), new Vector3(0.11f, 0.002f, 0.065f), 0.005f, 1, Gfx.Hex(0x34495E));
        // Kapak (mentese arkada), cerceve ve arkadaki logo
        var lid = Matrix4x4.CreateRotationX(0.3f) * Matrix4x4.CreateTranslation(0, 0.13f, 0.14f);
        Shapes.RoundedBox(b[M.Plastic], lid, new Vector3(0.36f, 0.24f, 0.015f), 0.007f, 2, shell);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0, -0.0078f) * lid, new Vector3(0.335f, 0.215f, 0.001f), Gfx.Hex(0x111111));
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.01f, 0.0075f) * lid, 0.025f, 0.001f, 14, Gfx.Hex(0xE8792E));
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.15f, 0.02f, 0.125f), 0.008f, 0.3f, 8, Gfx.Hex(0x1B2631));
    });

    public RenderModel LaptopScreen() => Get("laptopscreen", b =>
    {
        var lid = Matrix4x4.CreateRotationX(0.3f) * Matrix4x4.CreateTranslation(0, 0.13f, 0.131f);
        // Masaustu: mavi zemin, gorev cubugu, birkac uygulama simgesi
        Shapes.Box(b[M.Emissive], lid, new Vector3(0.32f, 0.2f, 0.002f), Gfx.Hex(0x5DADE2));
        Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(0, -0.091f, -0.0012f) * lid, new Vector3(0.32f, 0.018f, 0.001f), Gfx.Hex(0x1B4F72));
        var icons = new[] { Gfx.Hex(0xE8792E), Gfx.Hex(0x58D68D), Gfx.Hex(0xF4D03F), Gfx.Hex(0xEC7063) };
        for (var i = 0; i < icons.Length; i++)
        {
            Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(-0.13f, 0.07f - i * 0.04f, -0.0012f) * lid, new Vector3(0.025f, 0.025f, 0.001f), icons[i]);
        }

        Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(0.03f, 0.01f, -0.0012f) * lid, new Vector3(0.18f, 0.12f, 0.001f), Gfx.Hex(0xFDFEFE));
        Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(0.03f, 0.063f, -0.0018f) * lid, new Vector3(0.18f, 0.014f, 0.001f), Gfx.Hex(0xE8792E));
    });

    public RenderModel Bed() => Get("bed", b =>
    {
        var frame = Gfx.Hex(0x7B5A3C);
        var frameDark = Gfx.Hex(0x5E4129);
        // Karyola: yan kasalar, ayaklar, yuvarlak basucu (bas -X'te)
        Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.2f, 0), new Vector3(2.0f, 0.2f, 0.9f), 0.025f, 2, frame, 1f);
        foreach (var (lx, lz) in new[] { (-0.96f, -0.41f), (0.96f, -0.41f), (-0.96f, 0.41f), (0.96f, 0.41f) })
        {
            Shapes.RoundedBoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(lx, 0, lz), new Vector3(0.07f, 0.12f, 0.07f), 0.012f, 1, frameDark);
        }

        var head = new List<Vector2> { new(-0.45f, 0), new(0.45f, 0), new(0.45f, 0.62f), new(0.3f, 0.74f), new(0f, 0.78f), new(-0.3f, 0.74f), new(-0.45f, 0.62f) };
        Shapes.Extrude(b[M.Wood], Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(-1.01f, 0.1f, 0), head, 0.05f, frame);
        Shapes.Extrude(b[M.Wood], Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(1.0f, 0.1f, 0), [new(-0.45f, 0), new(0.45f, 0), new(0.45f, 0.38f), new(-0.45f, 0.38f)], 0.04f, frame);
        // Yatak, carsaf, yorgan (kenardan sarkar), yastik
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.37f, 0), new Vector3(1.94f, 0.16f, 0.84f), 0.05f, 2, Gfx.Hex(0xF5EEF8));
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateTranslation(0.25f, 0.44f, 0), new Vector3(1.42f, 0.05f, 0.9f), 0.02f, 2, Gfx.Hex(0x7D3C98));
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateTranslation(0.25f, 0.34f, -0.445f), new Vector3(1.42f, 0.2f, 0.02f), 0.008f, 1, Gfx.Hex(0x6C3483));
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateTranslation(-0.42f, 0.455f, 0), new Vector3(0.25f, 0.04f, 0.9f), 0.02f, 2, Gfx.Hex(0xEBDEF0));
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateRotationZ(-0.12f) * Matrix4x4.CreateTranslation(-0.78f, 0.5f, 0), new Vector3(0.3f, 0.11f, 0.6f), 0.05f, 2, Color.White);
        // Desen: yorganda serit
        for (var i = 0; i < 3; i++)
        {
            Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(-0.15f + i * 0.4f, 0.4655f, 0), new Vector3(0.06f, 0.002f, 0.9f), Gfx.Hex(0xD98880));
        }
    });

    public RenderModel Trash() => Get("trash", b =>
    {
        var body = Gfx.Hex(0x7F8C8D);
        // Pedalli cop kovasi: konik govde (ustte genis), kenar halkalari, kubbeli kapak
        Shapes.Lathe(b[M.Plastic], Matrix4x4.Identity, [new(0, 0), new(0.185f, 0), new(0.19f, 0.02f), new(0.215f, 0.62f), new(0.222f, 0.63f), new(0.222f, 0.64f)], 20, body);
        Shapes.Torus(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.03f, 0), 0.19f, 0.012f, 20, 4, Gfx.Hex(0x566573));
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.64f, 0), [new(0.232f, 0), new(0.232f, 0.025f), new(0.2f, 0.05f), new(0.1f, 0.068f), new(0, 0.07f)], 20, Gfx.Hex(0x566573), smoothProfile: true);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.69f, -0.12f), new Vector3(0.1f, 0.012f, 0.03f), Gfx.Hex(0x34495E));
        // Pedal
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.035f, -0.23f), new Vector3(0.12f, 0.02f, 0.08f), 0.008f, 1, DarkSteel);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateRotationX(-0.04f) * Matrix4x4.CreateTranslation(0, 0.35f, -0.206f), new Vector3(0.1f, 0.1f, 0.003f), Gfx.Hex(0x58D68D));
    });

    public RenderModel Pallet() => Get("pallet", b =>
    {
        var plank = Gfx.Hex(0xC9A26B);
        var block = Gfx.Hex(0xA9845A);
        // Ust tahtalar, alt kirisler ve takozlar; tahtalarda civi
        for (var i = 0; i < 5; i++)
        {
            var x = -0.48f + i * 0.24f;
            Shapes.RoundedBoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.12f, 0), new Vector3(0.14f, 0.022f, 1.0f), 0.004f, 1, i % 2 == 0 ? plank : Gfx.Lerp(plank, block, 0.3f));
            foreach (var z in new[] { -0.45f, 0f, 0.45f })
            {
                Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(x, 0.1415f, z), 0.006f, 0.001f, 6, DarkSteel);
            }
        }

        foreach (var z in new[] { -0.45f, 0f, 0.45f })
        {
            Shapes.RoundedBoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.1f, z), new Vector3(1.2f, 0.02f, 0.1f), 0.004f, 1, block);
            foreach (var x in new[] { -0.52f, 0f, 0.52f })
            {
                Shapes.RoundedBoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.0f, z), new Vector3(0.14f, 0.1f, 0.1f), 0.006f, 1, Gfx.Lerp(block, Black, 0.15f));
            }
        }
    });

    public RenderModel Table() => Get("table", b =>
    {
        var top = Gfx.Hex(0x8B5A2B);
        var stool = Gfx.Hex(0x6E4B2A);
        // Masa: pahli tabla, ortuyle; dokum ayak ve capraz taban
        Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.76f, 0), new Vector3(0.9f, 0.04f, 0.9f), 0.012f, 2, top, 2f);
        Shapes.RoundedBox(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.7815f, 0), new Vector3(0.6f, 0.003f, 0.6f), 0.0015f, 1, Gfx.Hex(0xC0392B));
        for (var i = 0; i < 4; i++)
        {
            Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(-0.225f + i * 0.15f, 0.7835f, 0), new Vector3(0.05f, 0.001f, 0.6f), Gfx.Hex(0xFDFEFE));
        }

        Shapes.Cylinder(b[M.Metal], Matrix4x4.Identity, 0.035f, 0.74f, 10, Black);
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.68f, 0), 0.08f, 0.06f, 12, Black);
        foreach (var a in new[] { MathF.PI / 4, -MathF.PI / 4 })
        {
            Shapes.RoundedBoxOnGround(b[M.Metal], Matrix4x4.CreateRotationY(a), new Vector3(0.62f, 0.03f, 0.05f), 0.01f, 1, Black);
        }

        // Iki tabure (masanin iki yaninda), oturagi yuvarlatilmis, sirt cubuklu
        foreach (var x in new[] { -0.75f, 0.75f })
        {
            var s = MathF.Sign(x);
            Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.43f, 0), new Vector3(0.42f, 0.04f, 0.42f), 0.012f, 2, stool);
            foreach (var (lx, lz) in new[] { (-0.17f, -0.17f), (0.17f, -0.17f), (-0.17f, 0.17f), (0.17f, 0.17f) })
            {
                Shapes.Tube(b[M.Wood], [new Vector3(x + lx * 1.08f, 0, lz * 1.08f), new Vector3(x + lx, 0.41f, lz)], 0.018f, 6, Gfx.Hex(0x5A3D22));
            }

            Shapes.Beam(b[M.Wood], new Vector3(x - 0.17f, 0.15f, -0.17f), new Vector3(x - 0.17f, 0.15f, 0.17f), 0.02f, Gfx.Hex(0x5A3D22));
            Shapes.Beam(b[M.Wood], new Vector3(x + 0.17f, 0.15f, -0.17f), new Vector3(x + 0.17f, 0.15f, 0.17f), 0.02f, Gfx.Hex(0x5A3D22));
            foreach (var z in new[] { -0.17f, 0.17f })
            {
                Shapes.Tube(b[M.Wood], [new Vector3(x + s * 0.19f, 0.43f, z), new Vector3(x + s * 0.205f, 0.85f, z)], 0.016f, 6, stool);
            }

            Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(x + s * 0.205f, 0.8f, 0), new Vector3(0.42f, 0.09f, 0.025f), 0.01f, 1, stool);
            Shapes.RoundedBox(b[M.Wood], Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(x + s * 0.2f, 0.62f, 0), new Vector3(0.38f, 0.05f, 0.02f), 0.008f, 1, stool);
        }

        // Masa ustu: pecetelik ve tuzluk
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0.25f, 0.82f, 0.25f), new Vector3(0.1f, 0.075f, 0.05f), 0.008f, 1, Steel);
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(0.25f, 0.84f, 0.25f), new Vector3(0.085f, 0.09f, 0.035f), Gfx.Hex(0xFDFEFE));
        Shapes.Lathe(b[M.Glass], Matrix4x4.CreateTranslation(0.12f, 0.78f, 0.28f), [new(0, 0), new(0.022f, 0), new(0.024f, 0.06f), new(0, 0.06f)], 10, new Color(235, 235, 235, 200));
        Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(0.12f, 0.84f, 0.28f), [new(0.02f, 0), new(0.022f, 0.01f), new(0.014f, 0.022f), new(0, 0.025f)], 10, Steel, smoothProfile: true);
    });
}
