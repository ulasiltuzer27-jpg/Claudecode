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
        var steel = Gfx.Hex(0xD5D8DC);
        var frame = Gfx.Hex(0xC9CED3);
        var dark = Gfx.Hex(0x2B2B2B);
        // Govde (alt dolap): pahli kutu, alt ve ust bordur
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.6f, 0), new Vector3(hl * 2, 0.62f, 0.8f), 0.045f, 2, body, 1f);
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.315f, 0), new Vector3(hl * 2 + 0.012f, 0.06f, 0.812f), 0.025f, 2, trim);
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.87f, 0), new Vector3(hl * 2 + 0.008f, 0.035f, 0.808f), 0.015f, 1, trim);
        // Satici tarafi dolap kapaklari ve D kulplar
        foreach (var dx in tier >= 1 ? new[] { -0.75f, -0.15f, 0.45f } : new[] { -0.42f, 0.22f })
        {
            Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(dx, 0.6f, 0.402f), new Vector3(0.5f, 0.4f, 0.012f), 0.02f, 1, Lerp(body, Color.White, 0.25f));
            Shapes.Arc(b[M.Steel], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(dx + 0.18f, 0.6f, 0.415f), 0.035f, 0.007f, MathF.PI, MathF.PI, 8, 5, steel);
        }

        // Tezgah ustu (paslanmaz) ve kazan yuvasi halkasi
        Shapes.RoundedBox(b[M.Steel], Matrix4x4.CreateTranslation(0, top - 0.02f, 0), new Vector3(hl * 2 + 0.06f, 0.04f, 0.86f), 0.015f, 2, Color.White, 1f);
        foreach (var sx in tier >= 1 ? new[] { -0.62f, 0.1f } : new[] { -0.3f })
        {
            Shapes.Torus(b[M.Metal], Matrix4x4.CreateTranslation(sx, top, 0), 0.3f, 0.02f, 24, 6, Gfx.Hex(0x4D5656));
            Shapes.Disc(b[M.Metal], Matrix4x4.CreateTranslation(sx, top + 0.001f, 0), 0.28f, 24, Gfx.Hex(0x34393B));
        }

        // Vitrin cami: on, yanlar, ust (arka acik: usta arkadan servis eder)
        var gh = 0.55f;
        var g = new Color(220, 235, 245, 70);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(0, top + gh / 2, -0.4f), new Vector3(hl * 2, gh, 0.01f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(-hl, top + gh / 2, -0.12f), new Vector3(0.01f, gh, 0.56f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(hl, top + gh / 2, -0.12f), new Vector3(0.01f, gh, 0.56f), g);
        Shapes.Box(b[M.Glass], Matrix4x4.CreateTranslation(0, top + gh, -0.12f), new Vector3(hl * 2, 0.01f, 0.56f), g);
        // Cam cercevesi: yuvarlak profiller
        foreach (var x in new[] { -hl, hl })
        {
            Shapes.CapsuleBetween(b[M.Steel], new Vector3(x, top, -0.4f), new Vector3(x, top + gh, -0.4f), 0.013f, 4, 8, frame);
            Shapes.CapsuleBetween(b[M.Steel], new Vector3(x, top, 0.16f), new Vector3(x, top + gh, 0.16f), 0.013f, 4, 8, frame);
            Shapes.CapsuleBetween(b[M.Steel], new Vector3(x, top + gh, -0.4f), new Vector3(x, top + gh, 0.16f), 0.013f, 4, 8, frame);
        }

        Shapes.CapsuleBetween(b[M.Steel], new Vector3(-hl, top + gh, -0.4f), new Vector3(hl, top + gh, -0.4f), 0.013f, 4, 8, frame);
        Shapes.CapsuleBetween(b[M.Steel], new Vector3(-hl, top + gh, 0.16f), new Vector3(hl, top + gh, 0.16f), 0.013f, 4, 8, frame);
        // Tabela direkleri
        foreach (var x in new[] { -hl * 0.55f, hl * 0.55f })
        {
            Shapes.CapsuleBetween(b[M.Steel], new Vector3(x, top + gh, -0.12f), new Vector3(x, top + gh + 0.24f, -0.12f), 0.011f, 4, 8, frame);
        }

        // Yan raf (+X): tabak ve paket yigini burada
        Shapes.RoundedBox(b[M.Steel], Matrix4x4.CreateTranslation(hl + 0.15f, top - 0.03f, 0.02f), new Vector3(0.3f, 0.03f, 0.7f), 0.01f, 1, Color.White);
        foreach (var z in new[] { 0.3f, -0.26f })
        {
            Shapes.Tube(b[M.Steel], Shapes.Curve(new Vector3(hl, top - 0.22f, z), new Vector3(hl + 0.24f, top - 0.2f, z), new Vector3(hl + 0.28f, top - 0.045f, z), 6), 0.011f, 6, frame);
        }

        // Tutamak (-X): bukuk boru + lastik kabza
        var hx = -hl - 0.22f;
        foreach (var z in new[] { 0.32f, -0.32f })
        {
            Shapes.Tube(b[M.Metal], Shapes.Curve(new Vector3(-hl + 0.02f, 0.8f, z), new Vector3(hx + 0.06f, 0.82f, z), new Vector3(hx, 0.95f, z), 8), 0.018f, 8, dark);
        }

        Shapes.CapsuleBetween(b[M.Metal], new Vector3(hx, 0.95f, -0.34f), new Vector3(hx, 0.95f, 0.34f), 0.019f, 4, 10, dark);
        Shapes.CapsuleBetween(b[M.Rubber], new Vector3(hx, 0.95f, -0.22f), new Vector3(hx, 0.95f, 0.22f), 0.027f, 4, 12, Gfx.Hex(0x1C1C1C));
        // Tekerler: lastik + jant + tel jant (+X ucu); destek ayaklari (-X ucu)
        var wx = hl - 0.3f;
        foreach (var z in new[] { -0.45f, 0.45f })
        {
            var wheel = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(wx, 0.26f, z);
            Shapes.Torus(b[M.Rubber], wheel, 0.215f, 0.045f, 28, 8, Gfx.Hex(0x1C1C1C));
            Shapes.Torus(b[M.Metal], wheel, 0.172f, 0.013f, 24, 5, Gfx.Hex(0xB3B6B7));
            Shapes.CapsuleBetween(b[M.Metal], new Vector3(wx, 0.26f, z - 0.045f), new Vector3(wx, 0.26f, z + 0.045f), 0.045f, 4, 12, Gfx.Hex(0x99A3A4));
            for (var i = 0; i < 12; i++)
            {
                var a = i * MathF.Tau / 12;
                var dir = new Vector3(MathF.Cos(a), MathF.Sin(a), 0);
                Shapes.CapsuleBetween(b[M.Metal], new Vector3(wx, 0.26f, z) + dir * 0.04f, new Vector3(wx, 0.26f, z) + dir * 0.17f, 0.0045f, 2, 4, Gfx.Hex(0xC9CED3));
            }

            Shapes.Tube(b[M.Metal], [new Vector3(-hl + 0.15f, 0.31f, z * 0.85f), new Vector3(-hl + 0.15f, 0.02f, z * 0.85f)], 0.02f, 8, dark);
            Shapes.Sphere(b[M.Rubber], Matrix4x4.CreateScale(1f, 0.5f, 1f) * Matrix4x4.CreateTranslation(-hl + 0.15f, 0.015f, z * 0.85f), 0.035f, 4, 8, Gfx.Hex(0x1C1C1C));
        }

        Shapes.CapsuleBetween(b[M.Metal], new Vector3(wx, 0.26f, -0.42f), new Vector3(wx, 0.26f, 0.42f), 0.016f, 2, 8, dark);
        // Alt: tup gaz ve hortum
        Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(-0.35f, 0.02f, 0.05f), [new(0, 0), new(0.15f, 0), new(0.15f, 0.36f), new(0.11f, 0.44f), new(0.04f, 0.46f), new(0, 0.46f)], 16, Gfx.Hex(0x2E86C1), smoothProfile: true);
        Shapes.Cylinder(b[M.Metal], Matrix4x4.CreateTranslation(-0.35f, 0.47f, 0.05f), 0.02f, 0.05f, 8, Gfx.Hex(0xB7950B));
        // Arkada sogutucu kutu (ayran)
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0.1f, 0.45f, 0.44f), new Vector3(0.56f, 0.34f, 0.12f), 0.03f, 2, Gfx.Hex(0x1F618D));
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(0.1f, 0.635f, 0.44f), new Vector3(0.58f, 0.03f, 0.14f), 0.012f, 1, Gfx.Hex(0xEAF2F8));
        // Kasa (arka-sol, tezgah ustu): para gozu ve kilit
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(-hl + 0.18f, top + 0.05f, 0.3f), new Vector3(0.24f, 0.1f, 0.17f), 0.015f, 1, Gfx.Hex(0x5B2C6F));
        Shapes.Box(b[M.Metal], Matrix4x4.CreateTranslation(-hl + 0.18f, top + 0.101f, 0.3f), new Vector3(0.12f, 0.004f, 0.012f), dark);
        // Kirli tabak kovasi (on-sol, musteri tarafi)
        Shapes.RoundedBox(b[M.Plastic], Matrix4x4.CreateTranslation(-hl + 0.25f, 0.7f, -0.47f), new Vector3(0.38f, 0.2f, 0.14f), 0.025f, 2, Gfx.Hex(0x7F8C8D));
        // Bulasik kovasi (yerde, ustanin yaninda) + sap
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(-hl + 0.3f, 0, 0.62f), [new(0, 0), new(0.15f, 0), new(0.18f, 0.3f), new(0.19f, 0.31f), new(0.17f, 0.31f), new(0.14f, 0.02f), new(0, 0.02f)], 16, Gfx.Hex(0xE74C3C));
        Shapes.Arc(b[M.Metal], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(-hl + 0.3f, 0.31f, 0.62f), 0.17f, 0.005f, 0, MathF.PI, 12, 4, Gfx.Hex(0x7B7D7D));
        // Isitici dugmesi
        Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(-0.3f, 0.55f, 0.43f), 0.035f, 0.03f, 12, dark);
        // Karabiberlik ve pecetelik
        Shapes.Lathe(b[M.Glass], Matrix4x4.CreateTranslation(hl - 0.08f, top, 0.34f), [new(0, 0), new(0.03f, 0), new(0.032f, 0.06f), new(0.026f, 0.1f), new(0, 0.1f)], 10, new Color(60, 60, 60, 220));
        Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(hl - 0.08f, top + 0.1f, 0.34f), [new(0.026f, 0), new(0.028f, 0.012f), new(0.018f, 0.025f), new(0, 0.028f)], 10, frame, smoothProfile: true);
        Shapes.RoundedBox(b[M.Metal], Matrix4x4.CreateTranslation(hl - 0.28f, top + 0.045f, 0.36f), new Vector3(0.12f, 0.09f, 0.06f), 0.01f, 1, steel);
        Shapes.Box(b[M.Paper], Matrix4x4.CreateTranslation(hl - 0.28f, top + 0.06f, 0.36f), new Vector3(0.1f, 0.1f, 0.045f), Gfx.Hex(0xFDFEFE));
        Shapes.ShadeByHeight(b[M.Metal], 0, 0f, 0.9f, 0.25f);
    });

    private static Color Lerp(Color a, Color c, float t) => new(
        (byte)(a.R + (c.R - a.R) * t), (byte)(a.G + (c.G - a.G) * t), (byte)(a.B + (c.B - a.B) * t), (byte)255);

    public RenderModel PickleJar() => Get("pickles", b =>
    {
        Shapes.Lathe(b[M.Glass], Matrix4x4.Identity, [new(0, 0), new(0.058f, 0), new(0.062f, 0.02f), new(0.062f, 0.15f), new(0.05f, 0.17f), new(0.048f, 0.18f), new(0, 0.18f)], 16, new Color(200, 230, 180, 140), smoothProfile: true);
        // Icerde salatalik ve biber parcalari
        for (var i = 0; i < 7; i++)
        {
            var a = i * 2.4f;
            var c = i % 3 == 2 ? Gfx.Hex(0xE74C3C) : Gfx.Hex(0x7DCEA0);
            var x = MathF.Cos(a) * 0.03f;
            var z = MathF.Sin(a) * 0.03f;
            Shapes.CapsuleBetween(b[M.White], new Vector3(x, 0.025f, z), new Vector3(x * 0.6f, 0.14f, z * 0.6f), 0.013f, 3, 6, c);
        }

        Shapes.Lathe(b[M.Metal], Matrix4x4.CreateTranslation(0, 0.172f, 0), [new(0.05f, 0), new(0.054f, 0.004f), new(0.054f, 0.022f), new(0.045f, 0.028f), new(0, 0.028f)], 16, Gfx.Hex(0xD4AC0D), smoothProfile: true);
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
            Shapes.RoundedBox(b[M.Foam], Matrix4x4.CreateTranslation(0, 0.012f + i * 0.02f, 0), new Vector3(0.2f, 0.018f, 0.16f), 0.006f, 1, Gfx.Hex(0xFDFEFE));
        }
    });

    public RenderModel AyranCup() => Get("ayran", b =>
    {
        Shapes.Lathe(b[M.Plastic], Matrix4x4.Identity, [new(0, 0), new(0.027f, 0), new(0.034f, 0.088f), new(0.037f, 0.092f), new(0, 0.092f)], 14, Gfx.Hex(0xFDFEFE));
        Shapes.Lathe(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.03f, 0), [new(0.0295f, 0), new(0.0325f, 0.04f)], 14, Gfx.Hex(0x2E86C1));
        Shapes.Disc(b[M.Paper], Matrix4x4.CreateTranslation(0, 0.093f, 0), 0.037f, 14, Gfx.Hex(0xD5D8DC));
    });

    public RenderModel Umbrella() => Get("umbrella", b =>
    {
        Shapes.CapsuleBetween(b[M.Metal], Vector3.Zero, new Vector3(0, 2.62f, 0), 0.022f, 3, 8, Gfx.Hex(0x7B7D7D));
        for (var i = 0; i < 8; i++)
        {
            var c = i % 2 == 0 ? Gfx.Hex(0xC0392B) : Gfx.Hex(0xF4F6F7);
            var a0 = i * MathF.Tau / 8;
            // Hafif bombeli kanopi dilimi + kenarda sarkan saçak
            Shapes.Lathe(b[M.Fabric], Matrix4x4.Identity, [new(1.42f, 2.08f), new(1.0f, 2.32f), new(0.5f, 2.5f), new(0.0f, 2.58f)], 4, c, smoothProfile: true, startAngle: a0, sweep: MathF.Tau / 8);
            Shapes.Lathe(b[M.Fabric], Matrix4x4.Identity, [new(1.42f, 1.98f), new(1.42f, 2.08f)], 4, c, startAngle: a0, sweep: MathF.Tau / 8);
            var rib = new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0));
            Shapes.Tube(b[M.Metal], [new Vector3(0, 2.56f, 0) + rib * 0.02f, rib * 0.7f + new Vector3(0, 2.42f, 0), rib * 1.4f + new Vector3(0, 2.08f, 0)], 0.008f, 4, Gfx.Hex(0x7B7D7D));
        }

        Shapes.Sphere(b[M.Metal], Matrix4x4.CreateTranslation(0, 2.64f, 0), 0.04f, 4, 8, Gfx.Hex(0x7B7D7D));
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
        var m = b[M.Hair];
        var dark = Lerp(fur, Gfx.Hex(0x1C1C1C), 0.35f);
        var light = Lerp(fur, Color.White, 0.45f);
        // Govde: kesitlerle yumusak, gogus onde (-Z)
        Shapes.Loft(m, Matrix4x4.Identity,
        [
            new Shapes.LoftSection(-0.2f, 0.25f, 0.035f, 0.035f, 0.03f),
            new Shapes.LoftSection(-0.15f, 0.21f, 0.06f, 0.065f, 0.055f),
            new Shapes.LoftSection(-0.05f, 0.19f, 0.07f, 0.07f, 0.065f),
            new Shapes.LoftSection(0.08f, 0.185f, 0.072f, 0.068f, 0.065f),
            new Shapes.LoftSection(0.17f, 0.19f, 0.062f, 0.06f, 0.055f),
            new Shapes.LoftSection(0.2f, 0.195f, 0.03f, 0.03f, 0.025f),
        ], 3, fur);
        // Bas, agiz, burun
        Shapes.Sphere(m, Matrix4x4.CreateScale(1.05f, 0.92f, 0.95f) * Matrix4x4.CreateTranslation(0, 0.285f, -0.235f), 0.072f, 8, 12, fur);
        Shapes.Sphere(m, Matrix4x4.CreateScale(1.25f, 0.8f, 0.9f) * Matrix4x4.CreateTranslation(0, 0.262f, -0.288f), 0.03f, 5, 8, light);
        Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.278f, -0.315f), 0.009f, 3, 6, Gfx.Hex(0xE59866));
        foreach (var s2 in new[] { -1f, 1f })
        {
            // Kulaklar (ucgen ekstruzyon), gozler, biyiklar
            Shapes.Extrude(m, Matrix4x4.CreateRotationZ(s2 * -0.25f) * Matrix4x4.CreateRotationY(s2 * 0.2f) * Matrix4x4.CreateTranslation(s2 * 0.042f, 0.335f, -0.228f),
                [new(-0.026f, 0), new(0.026f, 0), new(0.004f, 0.055f)], 0.012f, fur);
            Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 1.1f, 0.6f) * Matrix4x4.CreateTranslation(s2 * 0.03f, 0.3f, -0.294f), 0.014f, 4, 8, Gfx.Hex(0x9ACD32));
            Shapes.Sphere(m, Matrix4x4.CreateScale(0.35f, 1.1f, 0.5f) * Matrix4x4.CreateTranslation(s2 * 0.03f, 0.3f, -0.302f), 0.012f, 3, 6, Gfx.Hex(0x111111));
            for (var k = -1; k <= 1; k++)
            {
                Shapes.Tube(m, [new Vector3(s2 * 0.02f, 0.264f, -0.31f), new Vector3(s2 * 0.075f, 0.268f + k * 0.012f, -0.3f), new Vector3(s2 * 0.11f, 0.262f + k * 0.022f, -0.285f)], 0.0012f, 3, Gfx.Hex(0xEDEDED), false, false);
            }

            // Bacaklar ve patiler
            foreach (var z in new[] { -0.12f, 0.13f })
            {
                Shapes.CapsuleBetween(m, new Vector3(s2 * 0.045f, 0.17f, z), new Vector3(s2 * 0.048f, 0.02f, z - 0.01f), 0.022f, 4, 8, fur);
                Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 0.6f, 1.25f) * Matrix4x4.CreateTranslation(s2 * 0.048f, 0.013f, z - 0.018f), 0.024f, 4, 8, light);
            }
        }

        // Kivrik kuyruk (sivrilen)
        var tail = Shapes.Curve(new Vector3(0, 0.2f, 0.19f), new Vector3(0, 0.22f, 0.32f), new Vector3(0.04f, 0.42f, 0.32f), new Vector3(-0.02f, 0.45f, 0.24f), 10);
        Shapes.Tube(m, tail, 0.02f, 6, dark, radii: [0.022f, 0.021f, 0.02f, 0.019f, 0.018f, 0.017f, 0.016f, 0.015f, 0.014f, 0.013f, 0.012f]);
    });

    public RenderModel Seagull() => Get("seagull", b =>
    {
        var m = b[M.White];
        var white = Gfx.Hex(0xF8F9F9);
        Shapes.Loft(m, Matrix4x4.Identity,
        [
            new Shapes.LoftSection(-0.16f, 0.04f, 0.03f, 0.03f, 0.028f),
            new Shapes.LoftSection(-0.08f, 0.0f, 0.065f, 0.065f, 0.06f),
            new Shapes.LoftSection(0.06f, 0.0f, 0.06f, 0.06f, 0.055f),
            new Shapes.LoftSection(0.18f, 0.02f, 0.02f, 0.012f, 0.01f),
        ], 3, white);
        Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.07f, -0.16f), 0.055f, 6, 10, white);
        // Gaga: sari, ucunda kirmizi benek
        Shapes.CapsuleBetween(m, new Vector3(0, 0.06f, -0.2f), new Vector3(0, 0.05f, -0.27f), 0.012f, 3, 6, Gfx.Hex(0xF4D03F));
        Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.045f, -0.255f), 0.007f, 3, 5, Gfx.Hex(0xC0392B));
        foreach (var s2 in new[] { -1f, 1f })
        {
            Shapes.Sphere(m, Matrix4x4.CreateTranslation(s2 * 0.035f, 0.09f, -0.19f), 0.008f, 3, 6, Gfx.Hex(0x111111));
            Shapes.CapsuleBetween(m, new Vector3(s2 * 0.025f, -0.04f, 0.02f), new Vector3(s2 * 0.025f, -0.1f, 0.03f), 0.006f, 2, 5, Gfx.Hex(0xF0B27A));
        }

        // Kuyruk tuyleri (gri, uclari koyu)
        Shapes.Extrude(m, Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.02f, 0.16f), [new(-0.05f, 0), new(0.05f, 0), new(0.03f, -0.08f), new(-0.03f, -0.08f)], 0.006f, Gfx.Hex(0xBFC9CA));
    });

    /// <summary>Kanat: kok (0,0,0), +X yonunde uzanir; gri ust, siyah uc.</summary>
    public RenderModel Wing() => Get("wing", b =>
    {
        var toXz = Matrix4x4.CreateRotationX(-MathF.PI / 2);
        Shapes.Extrude(b[M.White], toXz, [new(0, -0.1f), new(0.2f, -0.09f), new(0.36f, -0.06f), new(0.36f, 0.04f), new(0.16f, 0.07f), new(0, 0.08f)], 0.008f, Gfx.Hex(0xD5DBDB));
        Shapes.Extrude(b[M.White], toXz, [new(0.36f, -0.06f), new(0.47f, -0.035f), new(0.5f, 0.0f), new(0.36f, 0.04f)], 0.008f, Gfx.Hex(0x2C2C2C));
    });

    /// <summary>Isaret elmasi (egitim hedefi), pariltili.</summary>
    public RenderModel Marker() => Get("marker", b =>
    {
        Shapes.Frustum(b[M.Emissive], Matrix4x4.Identity, 0f, 0.12f, 0.16f, 4, Gfx.Hex(0xF4D03F), false, false);
        Shapes.Frustum(b[M.Emissive], Matrix4x4.CreateTranslation(0, 0.16f, 0), 0.12f, 0f, 0.1f, 4, Gfx.Hex(0xF7DC6F), false, false);
    });
}
