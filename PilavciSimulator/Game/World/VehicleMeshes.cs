using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;

namespace PilavciSimulator.World;

/// <summary>Sehir arabasi govde tipi.</summary>
public enum CarBody : byte
{
    Sedan,
    Hatchback,
    Taxi,
    Minibus,
}

/// <summary>
/// Prosedurel araclar (+X ileri, taban y=0): loft govde, egimli cam kabin,
/// tavan ve direkler, camurluk boslugu, lastik + jant, far, stop, plaka.
/// Hem statik sahneye (park etmis arabalar) hem dinamik modellere cizilir:
/// <paramref name="mesh"/> malzeme kimliginden hedef agi verir.
/// </summary>
public static class VehicleMeshes
{
    public static readonly Color GlassColor = new(60, 75, 90, 40);
    public static readonly Color TaxiYellow = Gfx.Hex(0xF4C20D);
    private static readonly Color Dark = Gfx.Hex(0x1C1C1C);
    private static readonly Color Trim = Gfx.Hex(0x2B2B2B);
    private static readonly Color HeadLight = Gfx.Hex(0xFFF6D5);
    private static readonly Color TailLight = Gfx.Hex(0xC0392B);

    /// <summary>Tohumdan govde tipi; park yerlerine minibus sigmaz.</summary>
    public static CarBody BodyFor(uint seed, bool parked)
    {
        var k = Rng.Hash(seed, 77) % 10;
        return k switch
        {
            0 or 1 => CarBody.Taxi,
            2 when !parked => CarBody.Minibus,
            3 or 4 or 5 => CarBody.Hatchback,
            _ => CarBody.Sedan,
        };
    }

    public static void Car(Func<int, MeshData> mesh, Matrix4x4 xf, CarBody body, Color paint)
    {
        if (body == CarBody.Minibus)
        {
            Van(mesh, xf, Gfx.Hex(0xF2F0E6), Gfx.Hex(0x17A589), windows: true, beaconBar: false);
            return;
        }

        var hatch = body == CarBody.Hatchback;
        if (body == CarBody.Taxi)
        {
            paint = TaxiYellow;
        }

        var hl = hatch ? 1.95f : 2.1f;
        var toX = Matrix4x4.CreateRotationY(MathF.PI / 2) * xf;
        var pm = mesh(M.Plastic);
        var start = pm.VertexCount;
        // Alt govde: arka tampon, bagaj, kapilar, kaput, on tampon
        Shapes.Loft(pm, toX,
        [
            new(-hl, 0.6f, 0.8f, 0.22f, 0.11f),
            new(-hl + 0.12f, hatch ? 0.66f : 0.63f, 0.87f, hatch ? 0.34f : 0.3f, 0.16f),
            new(-hl + 0.7f, 0.64f, 0.88f, 0.33f, 0.17f),
            new(hl - 0.75f, 0.62f, 0.88f, 0.32f, 0.17f),
            new(hl - 0.15f, 0.56f, 0.86f, 0.25f, 0.14f),
            new(hl, 0.5f, 0.8f, 0.17f, 0.1f),
        ], 3, paint);
        Shapes.ShadeByHeight(pm, start, 0.3f, 1.0f, 0.18f);

        // Cam kabin: on cam ve arka cam egimli (hatchback'te arka dik)
        float rearBase = hatch ? -hl + 0.13f : -1.5f, roofRear = hatch ? -hl + 0.25f : -0.8f, roofFront = hatch ? 0.12f : 0.2f, front = hatch ? 0.85f : 0.95f;
        var cabin = hatch
            ? new List<Shapes.LoftSection>
            {
                new(rearBase, 1.13f, 0.77f, 0.19f, 0.08f),
                new(roofRear, 1.18f, 0.745f, 0.25f, 0.12f),
                new(roofFront, 1.18f, 0.74f, 0.25f, 0.12f),
                new(front, 0.96f, 0.8f, 0.02f, 0.02f),
            }
            : new List<Shapes.LoftSection>
            {
                new(rearBase, 0.96f, 0.8f, 0.02f, 0.02f),
                new(roofRear, 1.18f, 0.74f, 0.25f, 0.12f),
                new(roofFront, 1.18f, 0.74f, 0.25f, 0.12f),
                new(front, 0.96f, 0.8f, 0.02f, 0.02f),
            };
        Shapes.Loft(mesh(M.WindowGlass), toX, cabin, 3, GlassColor);
        // Tavan ve direkler (govde renginde)
        Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation((roofRear + roofFront) / 2, 1.43f, 0) * xf, new Vector3(roofFront - roofRear + 0.08f, 0.05f, 1.5f), 0.025f, 2, paint);
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Tube(pm, [Vector3.Transform(new Vector3(front, 0.97f, s * 0.79f), xf), Vector3.Transform(new Vector3(roofFront, 1.42f, s * 0.745f), xf)], 0.04f, 6, paint);
            Shapes.Tube(pm, [Vector3.Transform(new Vector3(-0.32f, 0.97f, s * 0.8f), xf), Vector3.Transform(new Vector3(-0.32f, 1.42f, s * 0.745f), xf)], 0.045f, 6, paint);
            if (hatch)
            {
                Shapes.Tube(pm, [Vector3.Transform(new Vector3(rearBase + 0.04f, 0.97f, s * 0.78f), xf), Vector3.Transform(new Vector3(roofRear, 1.42f, s * 0.745f), xf)], 0.06f, 6, paint);
            }
            else
            {
                Shapes.Tube(pm, [Vector3.Transform(new Vector3(roofRear, 1.42f, s * 0.745f), xf), Vector3.Transform(new Vector3(rearBase, 0.97f, s * 0.79f), xf)], 0.05f, 6, paint);
            }

            // Ayna, kapi cizgileri, kulplar
            Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation(front - 0.08f, 1.0f, s * 0.94f) * xf, new Vector3(0.1f, 0.08f, 0.13f), 0.025f, 1, paint);
            var seams = hatch ? new[] { front - 0.02f, -0.32f } : new[] { front - 0.02f, -0.32f, -1.45f };
            foreach (var x in seams)
            {
                Shapes.Box(mesh(M.Rubber), Matrix4x4.CreateTranslation(x, 0.67f, s * 0.882f) * xf, new Vector3(0.012f, 0.44f, 0.004f), Trim);
            }

            foreach (var x in new[] { 0.5f, -0.75f })
            {
                Shapes.Box(mesh(M.Metal), Matrix4x4.CreateTranslation(x, 0.86f, s * 0.876f) * xf, new Vector3(0.13f, 0.025f, 0.014f), Gfx.Hex(0x9AA0A6));
            }
        }

        var wheelX = hatch ? 1.22f : 1.32f;
        Wheels(mesh, xf, wheelX, 0.78f, 0.33f, 0.88f);
        Ends(mesh, xf, hl, 0.88f, 0.72f, 0.42f);
        if (body == CarBody.Taxi)
        {
            // Tavan tabelasi ve dama seridi
            Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation(-0.25f, 1.53f, 0) * xf, new Vector3(0.22f, 0.15f, 0.55f), 0.03f, 2, TaxiYellow);
            Shapes.Box(mesh(M.Rubber), Matrix4x4.CreateTranslation(-0.25f, 1.52f, 0) * xf, new Vector3(0.225f, 0.045f, 0.555f), Dark);
            foreach (var s in new[] { -1f, 1f })
            {
                for (var i = 0; i < 18; i++)
                {
                    if (i % 2 == 0)
                    {
                        Shapes.Box(mesh(M.Rubber), Matrix4x4.CreateTranslation(-1.35f + i * 0.13f, 0.74f, s * 0.882f) * xf, new Vector3(0.13f, 0.065f, 0.004f), Dark);
                    }
                }
            }
        }
    }

    /// <summary>Kamyonet/minibus: kutu govde, egimli on cam. Zabita ve teslimat araci da bu.</summary>
    public static void Van(Func<int, MeshData> mesh, Matrix4x4 xf, Color paint, Color stripe, bool windows, bool beaconBar)
    {
        const float rear = -2.3f, nose = 2.6f, hw = 0.98f;
        var toX = Matrix4x4.CreateRotationY(MathF.PI / 2) * xf;
        var pm = mesh(M.Plastic);
        var start = pm.VertexCount;
        Shapes.Loft(pm, toX,
        [
            new(rear, 0.78f, hw - 0.02f, 0.4f, 0.1f),
            new(rear + 0.06f, 0.78f, hw, 0.42f, 0.12f),
            new(nose - 0.35f, 0.76f, hw, 0.42f, 0.14f),
            new(nose, 0.7f, hw - 0.04f, 0.32f, 0.14f),
        ], 3, paint);
        Shapes.Loft(pm, toX,
        [
            new(rear, 1.7f, hw - 0.02f, 0.52f, 0.12f),
            new(rear + 0.06f, 1.7f, hw, 0.54f, 0.14f),
            new(1.55f, 1.7f, hw, 0.54f, 0.14f),
            new(2.3f, 1.2f, hw - 0.02f, 0.05f, 0.05f),
        ], 3, paint);
        Shapes.ShadeByHeight(pm, start, 0.35f, 2.2f, 0.15f);

        // On cam (egimli), kapi camlari, yan pencereler (minibus)
        var glass = mesh(M.WindowGlass);
        var a = new Vector3(1.6f, 2.16f, 0);
        var c = new Vector3(2.3f, 1.26f, 0);
        var d = Vector3.Normalize(c - a);
        var phi = MathF.Atan2(d.Y, d.X);
        var nrm = new Vector3(-MathF.Sin(phi), MathF.Cos(phi), 0);
        Shapes.Box(glass, Matrix4x4.CreateRotationZ(phi) * Matrix4x4.CreateTranslation((a + c) / 2 + nrm * 0.012f) * xf, new Vector3(Vector3.Distance(a, c) * 0.86f, 0.02f, 1.72f), GlassColor);
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Box(glass, Matrix4x4.CreateTranslation(1.25f, 1.68f, s * (hw + 0.004f)) * xf, new Vector3(0.62f, 0.55f, 0.02f), GlassColor);
            if (windows)
            {
                foreach (var x in new[] { -1.8f, -0.85f, 0.1f })
                {
                    Shapes.Box(glass, Matrix4x4.CreateTranslation(x, 1.72f, s * (hw + 0.004f)) * xf, new Vector3(0.82f, 0.5f, 0.02f), GlassColor);
                }
            }

            // Bel seridi, kapi cizgisi, ayna
            Shapes.Box(pm, Matrix4x4.CreateTranslation((rear + nose) / 2 - 0.15f, 1.12f, s * (hw + 0.006f)) * xf, new Vector3(nose - rear - 0.5f, 0.16f, 0.012f), stripe);
            Shapes.Box(mesh(M.Rubber), Matrix4x4.CreateTranslation(0.88f, 1.35f, s * (hw + 0.002f)) * xf, new Vector3(0.014f, 1.3f, 0.004f), Trim);
            Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation(1.75f, 1.55f, s * (hw + 0.1f)) * xf, new Vector3(0.08f, 0.2f, 0.12f), 0.025f, 1, Dark);
        }

        // Arka kapilar: orta cizgi, kulp; minibuste arka camlar
        Shapes.Box(mesh(M.Rubber), Matrix4x4.CreateTranslation(rear - 0.002f, 1.3f, 0) * xf, new Vector3(0.004f, 1.6f, 0.014f), Trim);
        Shapes.Box(mesh(M.Metal), Matrix4x4.CreateTranslation(rear - 0.012f, 1.25f, 0.12f) * xf, new Vector3(0.02f, 0.03f, 0.14f), Gfx.Hex(0x9AA0A6));
        if (windows)
        {
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Box(glass, Matrix4x4.CreateTranslation(rear - 0.004f, 1.75f, s * 0.47f) * xf, new Vector3(0.02f, 0.5f, 0.78f), GlassColor);
            }
        }

        Wheels(mesh, xf, 1.65f, 0.84f, 0.36f, 0.98f, rearX: -1.45f);
        Ends(mesh, xf, nose, hw, 0.9f, 0.45f, rear);
        if (beaconBar)
        {
            Shapes.RoundedBox(mesh(M.Metal), Matrix4x4.CreateTranslation(1.2f, 2.27f, 0) * xf, new Vector3(0.3f, 0.06f, 1.3f), 0.02f, 1, Gfx.Hex(0x2B2B2B));
        }
    }

    /// <summary>Dort tekerlek: camurluk boslugu (koyu yarim disk), lastik, jant, gobek.</summary>
    private static void Wheels(Func<int, MeshData> mesh, Matrix4x4 xf, float frontX, float z, float r, float bodyHalfW, float rearX = float.NaN)
    {
        var rx = float.IsNaN(rearX) ? -frontX : rearX;
        var arch = new List<Vector2>();
        for (var i = 0; i <= 12; i++)
        {
            var t = MathF.PI * i / 12;
            arch.Add(new Vector2(MathF.Cos(t) * (r + 0.07f), MathF.Sin(t) * (r + 0.07f) - 0.04f));
        }

        foreach (var x in new[] { frontX, rx })
        {
            foreach (var s in new[] { -1f, 1f })
            {
                var c = new Vector3(x, r, s * z);
                Shapes.Extrude(mesh(M.Rubber), Matrix4x4.CreateTranslation(x, r, s * (bodyHalfW - 0.004f)) * xf, arch, 0.012f, Gfx.Hex(0x121212));
                var axis = Matrix4x4.CreateRotationX(s * MathF.PI / 2) * Matrix4x4.CreateTranslation(c) * xf;
                Shapes.Lathe(mesh(M.Rubber), axis,
                    [new(r * 0.64f, -0.105f), new(r * 0.9f, -0.11f), new(r, -0.08f), new(r * 1.01f, 0f), new(r, 0.08f), new(r * 0.9f, 0.11f), new(r * 0.64f, 0.105f)], 16, Dark, smoothProfile: true);
                Shapes.Cylinder(mesh(M.Metal), Matrix4x4.CreateTranslation(0, -0.09f, 0) * axis, r * 0.64f, 0.17f, 14, Gfx.Hex(0xA6ACAF));
                Shapes.Cylinder(mesh(M.Metal), Matrix4x4.CreateTranslation(0, 0.07f, 0) * axis, r * 0.2f, 0.03f, 10, Gfx.Hex(0xD0D3D4));
                for (var k = 0; k < 5; k++)
                {
                    var a = k * MathF.Tau / 5;
                    Shapes.Box(mesh(M.Metal), Matrix4x4.CreateRotationY(a) * Matrix4x4.CreateTranslation(MathF.Cos(a) * r * 0.38f, 0.081f, -MathF.Sin(a) * r * 0.38f) * axis,
                        new Vector3(r * 0.42f, 0.006f, 0.05f), Gfx.Hex(0x7B7D7D));
                }
            }
        }
    }

    /// <summary>Tamponlar, farlar, stoplar, izgara ve plakalar.</summary>
    private static void Ends(Func<int, MeshData> mesh, Matrix4x4 xf, float hl, float hw, float lightY, float bumperY, float rear = float.NaN)
    {
        var rx = float.IsNaN(rear) ? -hl : rear;
        var dark = mesh(M.Rubber);
        Shapes.RoundedBox(dark, Matrix4x4.CreateTranslation(hl - 0.02f, bumperY, 0) * xf, new Vector3(0.14f, 0.18f, hw * 1.9f), 0.05f, 2, Trim);
        Shapes.RoundedBox(dark, Matrix4x4.CreateTranslation(rx + 0.02f, bumperY, 0) * xf, new Vector3(0.14f, 0.18f, hw * 1.9f), 0.05f, 2, Trim);
        Shapes.RoundedBox(dark, Matrix4x4.CreateTranslation(hl - 0.005f, lightY - 0.12f, 0) * xf, new Vector3(0.03f, 0.13f, hw * 0.75f), 0.02f, 1, Dark);
        var pm = mesh(M.Plastic);
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation(hl - 0.04f, lightY, s * hw * 0.66f) * xf, new Vector3(0.1f, 0.12f, 0.32f), 0.03f, 1, HeadLight);
            Shapes.RoundedBox(pm, Matrix4x4.CreateTranslation(rx + 0.03f, lightY + 0.06f, s * hw * 0.7f) * xf, new Vector3(0.08f, 0.14f, 0.28f), 0.03f, 1, TailLight);
        }

        // Plakalar: beyaz zemin, solda mavi TR seridi
        foreach (var (x, side) in new[] { (hl + 0.055f, 1f), (rx - 0.055f, -1f) })
        {
            Shapes.Box(pm, Matrix4x4.CreateTranslation(x, bumperY + 0.02f, 0) * xf, new Vector3(0.012f, 0.11f, 0.5f), Gfx.Hex(0xF4F6F7));
            Shapes.Box(pm, Matrix4x4.CreateTranslation(x + side * 0.002f, bumperY + 0.02f, side * 0.215f) * xf, new Vector3(0.012f, 0.11f, 0.07f), Gfx.Hex(0x1F4E9A));
            for (var i = 0; i < 6; i++)
            {
                Shapes.Box(dark, Matrix4x4.CreateTranslation(x + side * 0.002f, bumperY + 0.02f, -side * (0.13f - i * 0.055f)) * xf, new Vector3(0.012f, 0.06f, 0.03f), Dark);
            }
        }
    }
}
