using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Client;

public sealed partial class ModelLibrary
{
    /// <summary>
    /// Pilav arabasi: camli vitrin, isitmali kazan yuvasi, yan raf,
    /// tutamak, iki teker + destek ayagi, ustte tabela. Boyasi kozmetik.
    /// Yerel: +X itme yonu (tutamak -X ucunda), -Z musteri tarafi.
    /// </summary>
    public RenderModel Cart(int tier, string paint) => Get($"cart{tier}_{paint}", b =>
    {
        var hl = StationDefs.CartHalfLength(tier);
        var body = paint.Length == 6 ? Gfx.Hex(Convert.ToUInt32(paint, 16)) : Gfx.Hex(0xE6E2D6);
        var trim = Gfx.Hex(0xB03A2E);
        var top = StationDefs.CartTop;
        // Govde (alt dolap)
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.6f, 0), new Vector3(hl * 2, 0.62f, 0.8f), body, 1f);
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.31f, -0.401f), new Vector3(hl * 2, 0.06f, 0.01f), trim);
        // Musteri tarafinda "PILAV" seridi
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.62f, -0.402f), new Vector3(hl * 1.6f, 0.2f, 0.01f), trim);
        // Tezgah ustu (paslanmaz)
        Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(0, top - 0.02f, 0), new Vector3(hl * 2 + 0.06f, 0.04f, 0.86f), Color.White, 1f);
        // Kazan yuvasi cevresi (isitici halka)
        foreach (var sx in tier >= 1 ? new[] { -0.62f, 0.1f } : new[] { -0.3f })
        {
            Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(sx, top, 0), 0.3f, 0.02f, 20, 5, Gfx.Hex(0x4D5656));
        }

        // Vitrin camı: on, yanlar, ust (arka acik — usta arkadan servis eder)
        var gh = 0.55f;
        var g = new Color(220, 235, 245, 70);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(0, top + gh / 2, -0.4f), new Vector3(hl * 2, gh, 0.01f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(-hl, top + gh / 2, -0.12f), new Vector3(0.01f, gh, 0.56f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(hl, top + gh / 2, -0.12f), new Vector3(0.01f, gh, 0.56f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(0, top + gh, -0.12f), new Vector3(hl * 2, 0.01f, 0.56f), g);
        // Cam cercevesi
        var frame = Gfx.Hex(0xC9CED3);
        foreach (var x in new[] { -hl, hl })
        {
            Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(x, top + gh / 2, -0.4f), new Vector3(0.025f, gh, 0.025f), frame);
            Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(x, top + gh / 2, 0.16f), new Vector3(0.025f, gh, 0.025f), frame);
        }

        Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(0, top + gh, -0.4f), new Vector3(hl * 2, 0.025f, 0.025f), frame);
        Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(0, top + gh, 0.16f), new Vector3(hl * 2, 0.025f, 0.025f), frame);
        // Tabela direkleri
        foreach (var x in new[] { -hl * 0.55f, hl * 0.55f })
        {
            Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(x, top + gh + 0.12f, -0.12f), new Vector3(0.02f, 0.24f, 0.02f), frame);
        }

        // Yan raf (+X): tabak ve paket yigini burada
        Shapes.Box(b[M.Steel], Matrix4x4.CreateTranslation(hl + 0.15f, top - 0.03f, 0.02f), new Vector3(0.3f, 0.03f, 0.7f), Color.White);
        Shapes.Beam(b[M.Steel], new Vector3(hl, top - 0.2f, 0.3f), new Vector3(hl + 0.28f, top - 0.04f, 0.3f), 0.02f, frame);
        Shapes.Beam(b[M.Steel], new Vector3(hl, top - 0.2f, -0.26f), new Vector3(hl + 0.28f, top - 0.04f, -0.26f), 0.02f, frame);
        // Tutamak (-X)
        var hx = -hl - 0.2f;
        Shapes.Beam(b[M.Metal], new Vector3(-hl, 0.85f, 0.32f), new Vector3(hx, 0.95f, 0.32f), 0.035f, Black);
        Shapes.Beam(b[M.Metal], new Vector3(-hl, 0.85f, -0.32f), new Vector3(hx, 0.95f, -0.32f), 0.035f, Black);
        Shapes.Beam(b[M.Rubber], new Vector3(hx, 0.95f, -0.36f), new Vector3(hx, 0.95f, 0.36f), 0.045f, Black);
        // Tekerler (+X ucu) ve destek ayaklari (-X ucu)
        foreach (var z in new[] { -0.44f, 0.44f })
        {
            Shapes.Cylinder(b[M.Rubber], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(hl - 0.3f, 0.26f, z + (z > 0 ? -0.035f : -0.035f)), 0.26f, 0.07f, 16, Black);
            Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(hl - 0.3f, 0.26f, z - 0.04f), 0.08f, 0.09f, 10, Gfx.Hex(0x99A3A4));
            Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(-hl + 0.15f, 0.15f, z * 0.85f), new Vector3(0.05f, 0.3f, 0.05f), Black);
        }

        // Alt: tup gaz ve bulasik kovasi yuvasi
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(-0.35f, 0.02f, 0.05f), 0.15f, 0.45f, 12, Gfx.Hex(0x2E86C1));
        // Arkada sogutucu kutu (ayran)
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0.1f, 0.45f, 0.44f), new Vector3(0.56f, 0.34f, 0.12f), Gfx.Hex(0x1F618D));
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0.1f, 0.63f, 0.44f), new Vector3(0.58f, 0.03f, 0.14f), Gfx.Hex(0xEAF2F8));
        // Kasa (arka-sol, tezgah ustu)
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(-hl + 0.18f, top + 0.05f, 0.3f), new Vector3(0.24f, 0.1f, 0.17f), Gfx.Hex(0x5B2C6F));
        // Kirli tabak kutusu (on-sol, musteri tarafi)
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(-hl + 0.25f, 0.7f, -0.47f), new Vector3(0.38f, 0.2f, 0.14f), Gfx.Hex(0x7F8C8D));
        // Bulasik kovasi (yerde, ustanin yaninda)
        Shapes.Frustum(b[M.Plastic], Matrix4x4.CreateTranslation(-hl + 0.3f, 0, 0.62f), 0.15f, 0.18f, 0.3f, 12, Gfx.Hex(0xE74C3C), false, true);
        // Isitici dugmesi
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(-0.3f, 0.55f, 0.43f), 0.035f, 0.03f, 10, Black);
        // Karabiber, tursu kavanozu
        Shapes.Cylinder(b[M.Glass], Matrix4x4.CreateTranslation(hl - 0.08f, top, 0.34f), 0.03f, 0.1f, 8, new Color(60, 60, 60, 220));
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(hl - 0.08f, top + 0.1f, 0.34f), 0.032f, 0.02f, 8, frame);
    });

    public RenderModel PickleJar() => Get("pickles", b =>
    {
        Shapes.Cylinder(b[M.Glass], Matrix4x4.Identity, 0.06f, 0.18f, 12, new Color(200, 230, 180, 140));
        Shapes.Cylinder(b[M.White], Matrix4x4.CreateTranslation(0, 0.01f, 0), 0.052f, 0.14f, 10, Gfx.Hex(0x7DCEA0));
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.18f, 0), 0.062f, 0.025f, 12, Gfx.Hex(0xD4AC0D));
    });

    public RenderModel PlateStack() => Get("platestack", b =>
    {
        for (var i = 0; i < 8; i++)
        {
            Shapes.Lathe(b[M.Ceramic], Matrix4x4.CreateTranslation(0, i * 0.012f, 0), [new(0, 0), new(0.11f, 0), new(0.125f, 0.012f), new(0, 0.012f)], 18, Gfx.Hex(0xFBFCFC));
        }
    });

    public RenderModel PackageStack() => Get("pkgstack", b =>
    {
        for (var i = 0; i < 6; i++)
        {
            Shapes.Box(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.012f + i * 0.02f, 0), new Vector3(0.2f, 0.018f, 0.16f), Gfx.Hex(0xFDFEFE));
        }
    });

    public RenderModel AyranCup() => Get("ayran", b =>
    {
        Shapes.Frustum(b[M.Plastic], Matrix4x4.Identity, 0.028f, 0.034f, 0.09f, 10, Gfx.Hex(0xFDFEFE));
        Shapes.Cylinder(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.09f, 0), 0.036f, 0.004f, 10, Gfx.Hex(0x2E86C1));
    });

    public RenderModel Umbrella() => Get("umbrella", b =>
    {
        Shapes.Cylinder(b[M.Metal], Matrix4x4.Identity, 0.025f, 2.4f, 6, Gfx.Hex(0x7B7D7D));
        for (var i = 0; i < 8; i++)
        {
            var a0 = i * MathF.Tau / 8;
            var a1 = (i + 1) * MathF.Tau / 8;
            var c = i % 2 == 0 ? Gfx.Hex(0xC0392B) : Gfx.Hex(0xF4F6F7);
            var top = new Vector3(0, 2.55f, 0);
            var p0 = new Vector3(MathF.Cos(a0) * 1.4f, 2.1f, MathF.Sin(a0) * 1.4f);
            var p1 = new Vector3(MathF.Cos(a1) * 1.4f, 2.1f, MathF.Sin(a1) * 1.4f);
            Shapes.Triangle(b[M.Fabric], top, p1, p0, c);
        }
    });

    // ═══════════════════════════════════════════════════════════════
    // Araclar
    // ═══════════════════════════════════════════════════════════════
    public RenderModel Car(Color body) => Get($"car_{body.R}_{body.G}_{body.B}", b =>
    {
        Engine.Rendering.MeshData Mk(int m) => b[m];
        World.Props.CarMesh(Mk(M.Plastic), Matrix4x4.Identity, body);
        World.Props.CarGlass(Mk(M.WindowGlass), Matrix4x4.Identity);
        World.Props.CarWheels(Mk(M.Rubber), Matrix4x4.Identity);
    });

    public RenderModel Van(bool zabita) => Get(zabita ? "van_zabita" : "van_delivery", b =>
    {
        var body = zabita ? Gfx.Hex(0xF4F6F7) : Gfx.Hex(0xE8E2D0);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(-0.4f, 1.25f, 0), new Vector3(3.6f, 1.9f, 2f), body);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(1.85f, 0.95f, 0), new Vector3(1.2f, 1.3f, 1.95f), body);
        Shapes.Box(b[M.WindowGlass], Matrix4x4.CreateTranslation(2.1f, 1.25f, 0), new Vector3(0.75f, 0.55f, 1.97f), new Color(60, 75, 90, 40));
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(-0.4f, 1.1f, 0), new Vector3(3.62f, 0.35f, 2.02f), zabita ? Gfx.Hex(0x1F3A93) : Gfx.Hex(0xE8792E));
        World.Props.CarWheels(b[M.Rubber], Matrix4x4.CreateScale(1.15f, 1.1f, 1.15f));
        if (zabita)
        {
            Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(1.6f, 1.7f, 0), new Vector3(0.3f, 0.15f, 1.2f), Gfx.Hex(0x1F3A93));
        }
    });

    public RenderModel Beacon() => Get("beacon", b =>
    {
        Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(1.6f, 1.82f, -0.35f), new Vector3(0.25f, 0.1f, 0.4f), Gfx.Hex(0x2E86C1));
        Shapes.Box(b[M.Emissive], Matrix4x4.CreateTranslation(1.6f, 1.82f, 0.35f), new Vector3(0.25f, 0.1f, 0.4f), Gfx.Hex(0xE74C3C));
    });

    /// <summary>Vapur: beyaz govde, iki kat, baca. -Z ileri.</summary>
    public RenderModel Ferry() => Get("ferry", b =>
    {
        var hull = Gfx.Hex(0xF4F6F7);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 1.2f, 0), new Vector3(9, 2.4f, 36), Gfx.Hex(0x1C2833));
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 3.4f, 1), new Vector3(8.6f, 2.2f, 26), hull);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 5.5f, 1), new Vector3(7.6f, 2.0f, 18), hull);
        Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 2.6f, 1), new Vector3(8.7f, 0.25f, 26.2f), Gfx.Hex(0xE8792E));
        for (var z = -11f; z <= 12; z += 2.2f)
        {
            Shapes.Box(b[M.WindowGlass], Matrix4x4.CreateTranslation(0, 3.6f, z), new Vector3(8.65f, 0.8f, 1.3f), new Color(60, 80, 100, 128));
        }

        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 6.5f, 3), 0.8f, 2.6f, 12, Gfx.Hex(0xF4F6F7));
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(0, 8.6f, 3), 0.82f, 0.5f, 12, Gfx.Hex(0x1C2833));
    });

    // ═══════════════════════════════════════════════════════════════
    // Hayvanlar
    // ═══════════════════════════════════════════════════════════════
    public RenderModel Cat(Color fur) => Get($"cat_{fur.R}_{fur.G}_{fur.B}", b =>
    {
        Shapes.Sphere(b[M.Hair], Matrix4x4.CreateScale(0.7f, 0.6f, 1.4f) * Matrix4x4.CreateTranslation(0, 0.18f, 0), 0.13f, 6, 10, fur);
        Shapes.Sphere(b[M.Hair], Matrix4x4.CreateTranslation(0, 0.28f, -0.2f), 0.085f, 6, 10, fur);
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Frustum(b[M.Hair], Matrix4x4.CreateTranslation(s * 0.045f, 0.34f, -0.21f), 0.03f, 0.0f, 0.06f, 4, fur, false, false);
            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(s * 0.03f, 0.3f, -0.28f), new Vector3(0.018f, 0.02f, 0.01f), Gfx.Hex(0x58D68D));
            foreach (var z in new[] { -0.1f, 0.12f })
            {
                Shapes.Cylinder(b[M.Hair], Matrix4x4.CreateTranslation(s * 0.06f, 0, z), 0.025f, 0.14f, 6, fur);
            }
        }

        Shapes.Beam(b[M.Hair], new Vector3(0, 0.2f, 0.18f), new Vector3(0, 0.38f, 0.32f), 0.035f, fur);
    });

    public RenderModel Seagull() => Get("seagull", b =>
    {
        Shapes.Sphere(b[M.White], Matrix4x4.CreateScale(0.6f, 0.6f, 1.5f), 0.12f, 5, 8, Gfx.Hex(0xF8F9F9));
        Shapes.Sphere(b[M.White], Matrix4x4.CreateTranslation(0, 0.06f, -0.15f), 0.06f, 5, 8, Gfx.Hex(0xF8F9F9));
        Shapes.Frustum(b[M.White], Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.05f, -0.2f), 0.02f, 0.0f, 0.07f, 4, Gfx.Hex(0xF4D03F));
    });

    public RenderModel Wing() => Get("wing", b =>
    {
        Shapes.Triangle(b[M.White], new Vector3(0, 0, -0.08f), new Vector3(0.45f, 0, 0.05f), new Vector3(0, 0, 0.1f), Gfx.Hex(0xD5DBDB));
        Shapes.Triangle(b[M.White], new Vector3(0, 0, -0.08f), new Vector3(0, 0, 0.1f), new Vector3(0.45f, 0, 0.05f), Gfx.Hex(0xBFC9CA));
    });

    /// <summary>Isaret elmasi (egitim hedefi), pariltili.</summary>
    public RenderModel Marker() => Get("marker", b =>
    {
        Shapes.Frustum(b[M.Emissive], Matrix4x4.Identity, 0f, 0.12f, 0.16f, 4, Gfx.Hex(0xF4D03F), false, false);
        Shapes.Frustum(b[M.Emissive], Matrix4x4.CreateTranslation(0, 0.16f, 0), 0.12f, 0f, 0.1f, 4, Gfx.Hex(0xF7DC6F), false, false);
    });
}
