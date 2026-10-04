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
    // Mutfak esyalari
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Kazan govdesi (kapaksiz). Yaricap kademeye gore.</summary>
    public RenderModel Kazan(int tier) => Get($"kazan{tier}", b =>
    {
        var r = ItemInfos.KazanRadius(tier);
        var h = r * 1.15f;
        var prof = new List<Vector2>
        {
            new(0, 0.01f), new(r * 0.85f, 0), new(r, r * 0.12f), new(r, h), new(r + 0.02f, h + 0.01f), new(r + 0.02f, h + 0.025f),
            new(r - 0.012f, h + 0.025f), new(r - 0.012f, r * 0.12f + 0.012f), new(r * 0.85f - 0.01f, 0.012f), new(0, 0.013f),
        };
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, prof, 28, Color.White, 2f);
        // Kulplar
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Torus(b[M.Steel], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(s * (r + 0.04f), h * 0.82f, 0), 0.045f, 0.012f, 12, 6, Color.White);
        }
    });

    public RenderModel KazanLid(int tier) => Get($"kazanlid{tier}", b =>
    {
        var r = ItemInfos.KazanRadius(tier) + 0.015f;
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, [new(r, 0), new(r * 0.9f, 0.03f), new(r * 0.4f, 0.06f), new(0, 0.065f)], 28, Color.White, 2f, smoothProfile: true);
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.06f, 0), 0.035f, 0.035f, 10, Black);
    });

    public RenderModel Tencere() => Get("tencere", b =>
    {
        const float r = 0.17f, h = 0.2f;
        var prof = new List<Vector2> { new(0, 0.005f), new(r * 0.9f, 0), new(r, 0.02f), new(r, h), new(r + 0.012f, h + 0.012f), new(r - 0.01f, h + 0.012f), new(r - 0.01f, 0.02f), new(0, 0.01f) };
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, prof, 24, Gfx.Hex(0xD6DADF), 2f);
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(s * (r + 0.04f), h * 0.85f, 0), new Vector3(0.07f, 0.025f, 0.05f), Black);
        }
    });

    public RenderModel TencereLid() => Get("tencerelid", b =>
    {
        const float r = 0.18f;
        Shapes.Lathe(b[M.Glass], Matrix4x4.Identity, [new(r, 0), new(r * 0.85f, 0.025f), new(0, 0.05f)], 24, new Color(255, 255, 255, 120), 2f, smoothProfile: true);
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.045f, 0), 0.025f, 0.03f, 8, Black);
    });

    public RenderModel Suzgec() => Get("suzgec", b =>
    {
        Shapes.Lathe(b[M.Steel], Matrix4x4.Identity, [new(0, 0), new(0.07f, 0), new(0.15f, 0.1f), new(0.16f, 0.11f), new(0.15f, 0.11f), new(0.07f, 0.012f), new(0, 0.012f)], 20, Gfx.Hex(0xE5E8EB), 2f);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0.23f, 0.1f, 0), new Vector3(0.14f, 0.02f, 0.035f), Gfx.Hex(0xC0392B));
    });

    public RenderModel Jug() => Get("jug", b =>
    {
        Shapes.Lathe(b[M.Glass], Matrix4x4.Identity, [new(0, 0), new(0.075f, 0), new(0.08f, 0.2f), new(0.085f, 0.205f), new(0.075f, 0.205f), new(0.07f, 0.005f), new(0, 0.005f)], 16, new Color(240, 248, 255, 110), 2f);
        Shapes.Torus(b[M.Glass], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.095f, 0.12f, 0), 0.04f, 0.009f, 10, 5, new Color(240, 248, 255, 140));
        // Olcu cizgileri
        for (var i = 1; i <= 4; i++)
        {
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, i * 0.04f, -0.079f), new Vector3(0.03f, 0.003f, 0.003f), Gfx.Hex(0x2E86C1));
        }
    });

    public RenderModel Spoon() => Get("kasik", b =>
    {
        Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.02f, 0.06f), new Vector3(0.025f, 0.02f, 0.32f), Gfx.Hex(0xC69C6D), 2f);
        Shapes.Sphere(b[M.Wood], Matrix4x4.CreateScale(1, 0.4f, 1.3f) * Matrix4x4.CreateTranslation(0, 0.02f, -0.15f), 0.045f, 5, 10, Gfx.Hex(0xC69C6D));
    });

    public RenderModel SaltBox() => Get("tuz", b =>
    {
        Shapes.Cylinder(b[M.Paper], Matrix4x4.Identity, 0.045f, 0.15f, 14, Gfx.Hex(0x2E86C1));
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.15f, 0), 0.047f, 0.02f, 14, Gfx.Hex(0xF4F6F7));
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, 0.08f, -0.046f), new Vector3(0.05f, 0.04f, 0.002f), Gfx.Hex(0xF4F6F7));
    });

    public RenderModel Butter() => Get("tereyagi", b =>
    {
        Shapes.BoxOnGround(b[M.Paper], Matrix4x4.Identity, new Vector3(0.12f, 0.05f, 0.075f), Gfx.Hex(0xF5E6A8));
        Shapes.BoxOnGround(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.002f, 0), new Vector3(0.122f, 0.03f, 0.077f), Gfx.Hex(0x3C7FB1));
    });

    public RenderModel ChickenPack() => Get("tavukpaketi", b =>
    {
        Shapes.BoxOnGround(b[M.Foam], Matrix4x4.Identity, new Vector3(0.24f, 0.03f, 0.17f), Gfx.Hex(0xF2F3F4));
        Shapes.Sphere(b[M.White], Matrix4x4.CreateScale(1.5f, 0.45f, 1f) * Matrix4x4.CreateTranslation(0, 0.04f, 0), 0.07f, 6, 10, Gfx.Hex(0xF1C6B5));
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(0, 0.045f, 0), new Vector3(0.24f, 0.03f, 0.17f), new Color(255, 255, 255, 50));
    });

    public RenderModel MeatPack() => Get("etpaketi", b =>
    {
        Shapes.BoxOnGround(b[M.Foam], Matrix4x4.Identity, new Vector3(0.22f, 0.03f, 0.16f), Gfx.Hex(0xF2F3F4));
        for (var i = 0; i < 6; i++)
        {
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.06f + (i % 3) * 0.06f, 0.045f, -0.03f + i / 3 * 0.06f), new Vector3(0.045f, 0.035f, 0.045f), Gfx.Hex(0xA93226));
        }
    });

    public RenderModel Tray() => Get("tepsi", b =>
    {
        Shapes.BoxOnGround(b[M.Steel], Matrix4x4.Identity, new Vector3(0.4f, 0.01f, 0.28f), Color.White);
        foreach (var (x, z, w, d) in new[] { (0f, -0.14f, 0.4f, 0.01f), (0f, 0.14f, 0.4f, 0.01f), (-0.2f, 0f, 0.01f, 0.28f), (0.2f, 0f, 0.01f, 0.28f) })
        {
            Shapes.BoxOnGround(b[M.Steel], Matrix4x4.CreateTranslation(x, 0, z), new Vector3(w, 0.05f, d), Color.White);
        }
    });

    public RenderModel Plate() => Get("tabak", b =>
    {
        Shapes.Lathe(b[M.Ceramic], Matrix4x4.Identity, [new(0, 0.005f), new(0.07f, 0), new(0.1f, 0.015f), new(0.125f, 0.03f), new(0.13f, 0.034f), new(0.12f, 0.034f), new(0.095f, 0.022f), new(0.065f, 0.012f), new(0, 0.013f)], 24, Gfx.Hex(0xFBFCFC), 2f, smoothProfile: true);
        Shapes.Torus(b[M.White], Matrix4x4.CreateTranslation(0, 0.032f, 0), 0.118f, 0.0035f, 24, 4, Gfx.Hex(0x2E86C1));
    });

    public RenderModel Package() => Get("paket", b =>
    {
        Shapes.Frustum(b[M.Foam], Matrix4x4.CreateScale(1.25f, 1, 1), 0.075f, 0.09f, 0.06f, 4, Gfx.Hex(0xFDFEFE), false, true);
        Shapes.Box(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.065f, 0), new Vector3(0.23f, 0.012f, 0.18f), Gfx.Hex(0xF4F6F7));
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
            Shapes.Cylinder(b[M.Metal], Matrix4x4.Identity, 0.16f, 0.5f, 14, Gfx.Hex(0x2E86C1));
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.5f, 0), 0.04f, 0.1f, 8, DarkSteel);
            return;
        }

        if (supplyId.StartsWith("pirinc", StringComparison.Ordinal) || supplyId.StartsWith("bulgur", StringComparison.Ordinal) ||
            supplyId.StartsWith("nohut", StringComparison.Ordinal) || supplyId.StartsWith("fasulye", StringComparison.Ordinal))
        {
            // Cuval
            Shapes.Sphere(b[M.Fabric], Matrix4x4.CreateScale(1.1f, 1.3f, 0.8f) * Matrix4x4.CreateTranslation(0, 0.2f, 0), 0.2f, 6, 10, Gfx.Hex(0xE8DCC0));
            Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.2f, -0.155f), new Vector3(0.18f, 0.12f, 0.01f), supplyId.StartsWith("pirinc", StringComparison.Ordinal) ? Gfx.Hex(0x1E8449) : Gfx.Hex(0xB9770E));
            return;
        }

        Shapes.BoxOnGround(b[M.Paper], Matrix4x4.Identity, new Vector3(0.5f, 0.36f, 0.4f), col);
        Shapes.BoxOnGround(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.355f, 0), new Vector3(0.08f, 0.006f, 0.402f), Gfx.Hex(0xD4AC0D));
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

    public RenderModel KazanOcagi() => Get("kazanocagi", b =>
    {
        var black = Gfx.Hex(0x2B2B2B);
        Shapes.Frustum(b[M.Metal], Matrix4x4.Identity, 0.3f, 0.26f, 0.38f, 16, black, true, false);
        Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.39f, 0), 0.22f, 0.025f, 18, 6, Gfx.Hex(0x3A3A3A));
        for (var i = 0; i < 3; i++)
        {
            var a = i * MathF.Tau / 3;
            Shapes.Box(b[M.Metal], Matrix4x4.CreateRotationY(a) * Matrix4x4.CreateTranslation(MathF.Cos(a) * 0.2f, 0.41f, MathF.Sin(a) * 0.2f), new Vector3(0.12f, 0.03f, 0.03f), Gfx.Hex(0x1E1E1E));
        }

        Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.36f, 0), 0.09f, 0.015f, 14, 5, Gfx.Hex(0x555555));
        // Dugme yuvasi
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.22f, -0.29f), new Vector3(0.14f, 0.12f, 0.04f), DarkSteel);
        // Tup ve hortum
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0.45f, 0, 0.1f), 0.15f, 0.55f, 14, Gfx.Hex(0x2E86C1));
        Shapes.Beam(b[M.Rubber], new Vector3(0.45f, 0.55f, 0.1f), new Vector3(0.25f, 0.2f, 0), 0.025f, Black);
    });

    public RenderModel Knob() => Get("knob", b =>
    {
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(-MathF.PI / 2), 0.035f, 0.03f, 12, Black);
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, 0.022f, -0.031f), new Vector3(0.008f, 0.03f, 0.004f), Color.White);
    });

    public RenderModel Stovetop(int tier) => Get($"stovetop{tier}", b =>
    {
        var w = tier >= 1 ? 1.16f : 0.84f;
        Shapes.BoxOnGround(b[M.Steel], Matrix4x4.Identity, new Vector3(w, 0.06f, 0.62f), Gfx.Hex(0xBFC5CA));
        var n = tier >= 1 ? 4 : 2;
        var step = tier >= 1 ? 0.28f : 0.48f;
        for (var i = 0; i < n; i++)
        {
            var x = (i - (n - 1) / 2f) * step;
            Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(x, 0.065f, -0.02f), 0.09f, 0.012f, 16, 5, Gfx.Hex(0x2B2B2B));
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(x, 0.06f, -0.02f), 0.05f, 0.012f, 12, Gfx.Hex(0x3A3A3A));
        }
    });

    public RenderModel Sink() => Get("sink", b =>
    {
        // Dolap (tezgahin parcasi) + tekne + musluk
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.Identity, new Vector3(0.9f, 0.86f, 0.62f), Gfx.Hex(0xB9A58B));
        Shapes.BoxOnGround(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.86f, 0), new Vector3(0.9f, 0.04f, 0.62f), Color.White, 1f, Shapes.Faces.Sides);
        Shapes.BoxOnGround(b[M.Steel], Matrix4x4.CreateTranslation(0, 0.7f, 0), new Vector3(0.6f, 0.02f, 0.46f), Gfx.Hex(0xAEB6BF));
        foreach (var (x, z, sw, sd) in new[] { (0f, -0.25f, 0.9f, 0.12f), (0f, 0.25f, 0.9f, 0.12f), (-0.375f, 0f, 0.15f, 0.62f), (0.375f, 0f, 0.15f, 0.62f) })
        {
            Shapes.BoxOnGround(b[M.Steel], Matrix4x4.CreateTranslation(x, 0.88f, z), new Vector3(sw, 0.02f, sd), Color.White);
        }

        var tap = new Vector3(0, 0.9f, 0.24f);
        Shapes.Cylinder(b[M.Steel], Matrix4x4.CreateTranslation(tap), 0.02f, 0.28f, 8, Color.White);
        Shapes.Beam(b[M.Steel], tap + new Vector3(0, 0.27f, 0), tap + new Vector3(0, 0.27f, -0.18f), 0.03f, Color.White);
        Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(tap + new Vector3(0, 0.3f, 0.02f)), new Vector3(0.12f, 0.02f, 0.03f), Color.White);
    });

    public RenderModel CuttingBoard() => Get("board", b =>
    {
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.Identity, new Vector3(0.6f, 0.03f, 0.4f), Gfx.Hex(0xD7B98E), 2f);
    });

    public RenderModel Fridge() => Get("fridge", b =>
    {
        Shapes.BoxOnGround(b[M.Plastic], Matrix4x4.Identity, new Vector3(0.76f, 1.9f, 0.7f), Gfx.Hex(0xF2F3F4));
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 1.25f, -0.352f), new Vector3(0.74f, 0.01f, 0.01f), Gfx.Hex(0xBDC3C7));
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0.28f, 1.5f, -0.37f), new Vector3(0.03f, 0.3f, 0.03f), DarkSteel);
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0.28f, 0.95f, -0.37f), new Vector3(0.03f, 0.3f, 0.03f), DarkSteel);
        // Kapaktaki etiketler (ne nerede)
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.15f, 1.45f, -0.352f), new Vector3(0.2f, 0.12f, 0.004f), Gfx.Hex(0xF4D03F));
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.15f, 1.05f, -0.352f), new Vector3(0.2f, 0.12f, 0.004f), Gfx.Hex(0xF1948A));
        Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(-0.15f, 0.65f, -0.352f), new Vector3(0.2f, 0.12f, 0.004f), Gfx.Hex(0xC0392B));
    });

    public RenderModel Pantry() => Get("pantry", b =>
    {
        var wood = Gfx.Hex(0x8B6B4A);
        // Iki katli raf: alt kat cuvallar, ust kat kutular
        foreach (var y in new[] { 0.05f, 0.95f, 1.7f })
        {
            Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(0, y, 0.1f), new Vector3(2.2f, 0.04f, 0.5f), wood);
        }

        foreach (var x in new[] { -1.08f, 1.08f })
        {
            Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.9f, 0.1f), new Vector3(0.05f, 1.8f, 0.5f), wood);
        }

        Shapes.Box(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.9f, 0.34f), new Vector3(2.2f, 1.8f, 0.02f), Gfx.Hex(0x6E5338));
    });

    public RenderModel Sack(Color label) => Get($"sack_{label.R}_{label.G}_{label.B}", b =>
    {
        Shapes.Lathe(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.8f), [new(0, 0), new(0.2f, 0.02f), new(0.24f, 0.25f), new(0.22f, 0.55f), new(0.17f, 0.62f), new(0.19f, 0.66f), new(0.0f, 0.62f)], 12, Gfx.Hex(0xE6D7B5), 2f, smoothProfile: true);
        Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.32f, -0.19f), new Vector3(0.22f, 0.16f, 0.01f), label);
    });

    public RenderModel CannedShelf() => Get("canned", b =>
    {
        for (var i = 0; i < 6; i++)
        {
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(-0.25f + (i % 3) * 0.25f, 0, (i / 3) * 0.12f), 0.05f, 0.13f, 10, Gfx.Hex(0xC0392B));
        }
    });

    public RenderModel Laptop() => Get("laptop", b =>
    {
        Shapes.BoxOnGround(b[M.Plastic], Matrix4x4.Identity, new Vector3(0.36f, 0.02f, 0.25f), Gfx.Hex(0x2C3E50));
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateRotationX(0.3f) * Matrix4x4.CreateTranslation(0, 0.13f, 0.14f), new Vector3(0.36f, 0.24f, 0.015f), Gfx.Hex(0x2C3E50));
    });

    public RenderModel LaptopScreen() => Get("laptopscreen", b =>
        Shapes.Box(b[M.Emissive], Matrix4x4.CreateRotationX(0.3f) * Matrix4x4.CreateTranslation(0, 0.13f, 0.131f), new Vector3(0.32f, 0.2f, 0.002f), Gfx.Hex(0x5DADE2)));

    public RenderModel Bed() => Get("bed", b =>
    {
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.Identity, new Vector3(2.0f, 0.3f, 0.9f), Gfx.Hex(0x7B5A3C));
        Shapes.BoxOnGround(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.3f, 0), new Vector3(1.95f, 0.15f, 0.85f), Gfx.Hex(0x7D3C98));
        Shapes.BoxOnGround(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.3f, 0.35f), new Vector3(1.95f, 0.55f, 0.18f), Gfx.Hex(0x6C3483));
        Shapes.BoxOnGround(b[M.Fabric], Matrix4x4.CreateTranslation(-0.7f, 0.44f, 0.05f), new Vector3(0.45f, 0.1f, 0.35f), Gfx.Hex(0xF5EEF8));
        Shapes.BoxOnGround(b[M.Fabric], Matrix4x4.CreateTranslation(0.25f, 0.45f, -0.05f), new Vector3(1.0f, 0.04f, 0.7f), Gfx.Hex(0xD98880));
    });

    public RenderModel Trash() => Get("trash", b =>
    {
        Shapes.Frustum(b[M.Plastic], Matrix4x4.Identity, 0.19f, 0.22f, 0.66f, 14, Gfx.Hex(0x7F8C8D), false, true);
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.66f, 0), 0.23f, 0.04f, 14, Gfx.Hex(0x566573));
    });

    public RenderModel Pallet() => Get("pallet", b =>
    {
        for (var i = 0; i < 5; i++)
        {
            Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(-0.48f + i * 0.24f, 0.1f, 0), new Vector3(0.14f, 0.025f, 1.0f), Gfx.Hex(0xC9A26B));
        }

        foreach (var z in new[] { -0.45f, 0f, 0.45f })
        {
            Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0, z), new Vector3(1.2f, 0.1f, 0.1f), Gfx.Hex(0xA9845A));
        }
    });

    public RenderModel Table() => Get("table", b =>
    {
        Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(0, 0.74f, 0), new Vector3(0.9f, 0.04f, 0.9f), Gfx.Hex(0x8B5A2B), 2f);
        Shapes.Cylinder(b[M.Metal], Matrix4x4.Identity, 0.05f, 0.74f, 8, Black);
        Shapes.Cylinder(b[M.Metal], Matrix4x4.Identity, 0.25f, 0.02f, 12, Black);
        Shapes.BoxOnGround(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.78f, 0), new Vector3(0.92f, 0.005f, 0.92f), Gfx.Hex(0xC0392B));
        foreach (var x in new[] { -0.75f, 0.75f })
        {
            Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x, 0.42f, 0), new Vector3(0.42f, 0.04f, 0.42f), Gfx.Hex(0x6E4B2A));
            Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x + MathF.Sign(x) * 0.19f, 0.42f, 0), new Vector3(0.04f, 0.45f, 0.42f), Gfx.Hex(0x6E4B2A));
            foreach (var (lx, lz) in new[] { (-0.17f, -0.17f), (0.17f, -0.17f), (-0.17f, 0.17f), (0.17f, 0.17f) })
            {
                Shapes.BoxOnGround(b[M.Wood], Matrix4x4.CreateTranslation(x + lx, 0, lz), new Vector3(0.04f, 0.42f, 0.04f), Gfx.Hex(0x5A3D22));
            }
        }
    });
}
