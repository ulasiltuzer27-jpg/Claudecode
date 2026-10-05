using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Entities;
using PilavciSimulator.Sim.Navigation;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.World;

/// <summary>
/// "Sahil Mahallesi": tek uzun cadde, iki yaninda kaldirim ve binalar,
/// dogu ucunda sahil seridi, iskele ve deniz. Bati ucunda oyuncunun deposu.
///
/// Koordinatlar: X dogu, Z guney, Y yukari; metre. Cadde z=-4..4, kuzey
/// kaldirimi z=-8.5..-4, guney kaldirimi z=4..8.5 (x&lt;55), sahil seridi
/// z=4..16 (x&gt;55). Kaldirim ust yuzeyi y=0.15.
///
/// Ayni kod hem host'ta (carpisma, yol grafigi) hem istemcide (cizim)
/// calisir; geometri yalnizca <c>withGeometry</c> ise uretilir.
/// </summary>
public static class DistrictBuilder
{
    public const float Curb = 0.15f;
    public const float MinX = -128f;
    public const float MaxX = 128f;

    public static DistrictLayout Build(bool withGeometry, ISignProvider? signs, int seed = 1923, IPrefabSource? prefabs = null)
    {
        var layout = new DistrictLayout();
        var geometry = withGeometry ? new StaticScene.Builder() : null;
        layout.Geometry = geometry;
        var b = new BuildContext(layout, geometry, signs, seed, prefabs);

        GroundAndStreet(b);
        NorthSide(b);
        SouthSide(b);
        Promenade(b);
        Depot(b);
        Shop(b);
        StreetFurniture(b);
        Background(b);
        Bounds(b);
        Spots(b);
        Navigation(b);
        return layout;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Zemin, cadde, kaldirimlar
    // ═══════════════════════════════════════════════════════════════════
    private static void GroundAndStreet(BuildContext b)
    {
        var L = b.Layout;
        // Asfalt (yer duzlemi zaten y=0; gorsel icin ince kutu)
        b.Box(M.Asphalt, new Vector3(MinX - 20, -0.5f, -4), new Vector3(MaxX + 20, 0, 4), Color.White, 0.25f, ColliderFlags.Solid | ColliderFlags.Placeable);
        // Serit cizgileri
        for (var x = MinX; x < MaxX; x += 6)
        {
            b.Box(M.White, new Vector3(x, 0, -0.08f), new Vector3(x + 3, 0.01f, 0.08f), Gfx.Hex(0xE8E2D0), 1f, null);
        }

        // Yaya gecitleri
        L.Crosswalks.AddRange([-88f, -40f, 8f, 46f, 88f]);
        foreach (var cx in L.Crosswalks)
        {
            for (var z = -3.6f; z < 3.6f; z += 0.9f)
            {
                b.Box(M.White, new Vector3(cx - 1.6f, 0, z), new Vector3(cx + 1.6f, 0.012f, z + 0.5f), Gfx.Hex(0xEDEAE0), 1f, null);
            }
        }

        var curbColor = Gfx.Hex(0xBDB6AA);
        // Kuzey kaldirimi
        b.Box(M.Cobble, new Vector3(MinX - 10, 0, -8.5f), new Vector3(MaxX + 10, Curb, -4.2f), Gfx.Hex(0xC7BBA8), 0.33f, ColliderFlags.Surface);
        b.Box(M.Concrete, new Vector3(MinX - 10, 0, -4.2f), new Vector3(MaxX + 10, Curb, -4f), curbColor, 0.5f, ColliderFlags.Surface);
        // Guney kaldirimi (x < 55)
        b.Box(M.Cobble, new Vector3(MinX - 10, 0, 4.2f), new Vector3(55, Curb, 8.5f), Gfx.Hex(0xC7BBA8), 0.33f, ColliderFlags.Surface);
        b.Box(M.Concrete, new Vector3(MinX - 10, 0, 4f), new Vector3(55, Curb, 4.2f), curbColor, 0.5f, ColliderFlags.Surface);
        // Yaya gecidi rampalari: kaldirima dusuk esik (gorsel)
    }

    // ═══════════════════════════════════════════════════════════════════
    // Kuzey cephe
    // ═══════════════════════════════════════════════════════════════════
    private static void NorthSide(BuildContext b)
    {
        var face = -8.5f;
        void Apt(float x0, float x1, int floors, string color, string? shop = null, string shopColor = "C0392B", string aw = "C0392B", bool pitched = true)
            => Props.Building(b, new Props.BuildingSpec
            {
                MinX = x0, MaxX = x1, FacadeZ = face, Facing = 1, Depth = 13.5f, Floors = floors, Color = color, Shop = shop,
                ShopColor = shopColor, AwningA = aw, Pitched = pitched,
            });

        Apt(-132, -112, 5, "D9A55B", "TERZİ", "6C3483", "8E44AD");
        // -112..-96 depo (ayri)
        Apt(-96, -84, 4, "A9C4D6", "BAKKAL", "1E8449", "27AE60");
        School(b, -84, -56, face);
        // -56..-47 ara sokak
        AlleyWalls(b, -56, -47, face, -1);
        Apt(-47, -35, 5, "E3B5A4", "BERBER", "1F618D", "2E86C1", pitched: false);
        Apt(-35, -22, 4, "EED9A0", "ECZANE", "C0392B", "E74C3C");
        Apt(-22, -10, 5, "B7D3B5", "FIRIN", "A04000", "D35400");
        Garages(b, -10, 22, face);
        TeaHouse(b, 22, 30, face);
        // 30..42 kiralik dukkan (ayri)
        Apt(42, 56, 5, "C9C3B8", "BÜFE", "B7950B", "F1C40F", pitched: false);
        AlleyWalls(b, 56, 61, face, -1);
        Apt(61, 75, 4, "E9DCC1", "KUAFÖR", "AF7AC5", "D2B4DE");
        Apt(75, 90, 5, "A8B59A", "BALIKÇI", "1A5276", "2471A3");
        Apt(90, 110, 6, "EFEBE4", "OTEL BOĞAZ", "17202A", "2C3E50", pitched: false);
        Apt(110, 132, 5, "F0C9A0", "SİMİTÇİ", "9C640C", "CA6F1E");
    }

    private static void School(BuildContext b, float x0, float x1, float face)
    {
        // Okul: bahce duvari + geri cekilmis bina
        var wall = Gfx.Hex(0xD9C9A8);
        b.Box(M.Brick, new Vector3(x0, Curb, face - 0.4f), new Vector3(x0 + 10.5f, Curb + 1.6f, face), Color.White, 1f);
        b.Box(M.Brick, new Vector3(x1 - 10.5f, Curb, face - 0.4f), new Vector3(x1, Curb + 1.6f, face), Color.White, 1f);
        // Kapi ayaklari
        b.Box(M.Brick, new Vector3(x0 + 10.5f, Curb, face - 0.6f), new Vector3(x0 + 11.2f, Curb + 2.6f, face + 0.1f), Color.White, 1f);
        b.Box(M.Brick, new Vector3(x1 - 11.2f, Curb, face - 0.6f), new Vector3(x1 - 10.5f, Curb + 2.6f, face + 0.1f), Color.White, 1f);
        b.SignBoard("SAHİL İLKOKULU", new Vector3((x0 + x1) / 2, Curb + 3.0f, face + 0.12f), 6.2f, 0.8f, Vector3.UnitZ, Gfx.Hex(0x1F3A5F), Color.White);
        b.Box(M.Metal, new Vector3(x0 + 11.2f, Curb + 2.55f, face - 0.1f), new Vector3(x1 - 11.2f, Curb + 2.65f, face), Gfx.Hex(0x2C3E50), 1f, null);
        // Bahce (beton zemin) ve bina
        b.Box(M.Concrete, new Vector3(x0, 0, face - 8), new Vector3(x1, Curb, face), Gfx.Hex(0xB9B2A6), 0.25f, ColliderFlags.Surface);
        Props.Building(b, new Props.BuildingSpec
        {
            MinX = x0, MaxX = x1, FacadeZ = face - 8, Facing = 1, Depth = 12, Floors = 3, Color = "E8C9A0", Pitched = true, Cumba = false,
            PlainGround = true, GroundFloor = 3.6f,
        });
        // Zemin kat pencereleri ve giris
        for (var x = x0 + 2; x < x1 - 1; x += 3)
        {
            if (MathF.Abs(x - (x0 + x1) / 2) < 2)
            {
                continue;
            }

            Props.Window(b, new Vector3(x, Curb + 1.0f, face - 8), 1, 1.4f, 1.6f, true, Gfx.Hex(0xE8C9A0));
        }

        b.Box(M.Wood, new Vector3((x0 + x1) / 2 - 1.2f, Curb, face - 8.02f), new Vector3((x0 + x1) / 2 + 1.2f, Curb + 2.6f, face - 7.9f), Gfx.Hex(0x7B4F2C), 1f, null);
        // Bayrak diregi
        var pole = new Vector3(x0 + 4, Curb, face - 3);
        b.ColliderYaw(pole, new Vector3(0.2f, 8f, 0.2f), 0);
        b.Draw(M.Metal, pole, m => Shapes.Cylinder(m, Matrix4x4.CreateTranslation(pole), 0.06f, 9f, 8, Gfx.Hex(0xD5D8DC)));
        b.Draw(M.Fabric, pole, m => Shapes.Box(m, Matrix4x4.CreateTranslation(pole + new Vector3(0.9f, 8.3f, 0)), new Vector3(1.7f, 1.1f, 0.02f), Gfx.Hex(0xE30A17)));
        // Bahcede agaclar
        Props.Tree(b, new Vector3(x1 - 4, Curb, face - 4), 1.1f);
        b.Layout.Nav.Add(new Vector3((x0 + x1) / 2, Curb, face - 7), NavTag.Door, "okul");
    }

    private static void Garages(BuildContext b, float x0, float x1, float face)
    {
        var w = (x1 - x0) / 3;
        string[] names = ["USTA KAPORTA", "LASTİKÇİ", "OTO ELEKTRİK"];
        string[] cols = ["7F8C8D", "95A5A6", "A6ACAF"];
        for (var i = 0; i < 3; i++)
        {
            var gx0 = x0 + i * w;
            var gx1 = gx0 + w;
            // Tek katli genis yapi, beton
            b.Box(M.Concrete, new Vector3(gx0, 0, face - 12), new Vector3(gx1, Curb + 5.2f, face - 0.01f), BuildContext.Hex(cols[i]), 0.5f);
            // Acik garaj kapisi (koyu ic)
            b.Box(M.Concrete, new Vector3(gx0 + 1.2f, Curb, face - 0.05f), new Vector3(gx1 - 1.2f, Curb + 3.6f, face + 0.02f), Gfx.Hex(0x2B2B2B), 1f, null);
            // Kepenk (yarim kalkik)
            b.Box(M.Metal, new Vector3(gx0 + 1.2f, Curb + 3.0f, face - 0.02f), new Vector3(gx1 - 1.2f, Curb + 3.7f, face + 0.08f), Gfx.Hex(0xA4A9AD), 1f, null);
            b.SignBoard(names[i], new Vector3((gx0 + gx1) / 2, Curb + 4.4f, face + 0.03f), w - 2, 0.75f, Vector3.UnitZ, Gfx.Hex(0x1C2833), Gfx.Hex(0xF4D03F), lit: true);
            // Lastik yigini
            if (i == 1)
            {
                var t = new Vector3(gx1 - 0.9f, Curb, face + 0.7f);
                b.ColliderYaw(t, new Vector3(0.8f, 1.2f, 0.8f), 0);
                b.Draw(M.Rubber, t, m =>
                {
                    for (var k = 0; k < 4; k++)
                    {
                        Shapes.Torus(m, Matrix4x4.CreateTranslation(t + new Vector3(0, 0.15f + k * 0.26f, 0)), 0.3f, 0.12f, 14, 6, Gfx.Hex(0x1A1A1A));
                    }
                });
            }

            b.Layout.Nav.Add(new Vector3((gx0 + gx1) / 2, Curb, face + 0.6f), NavTag.Door, "sanayi");
        }

        b.SignBoard("OTO SANAYİ SİTESİ", new Vector3((x0 + x1) / 2, Curb + 6.4f, face - 0.4f), 9f, 1.2f, Vector3.UnitZ, Gfx.Hex(0xC0392B), Color.White, 1024, 160);
        b.Box(M.Metal, new Vector3((x0 + x1) / 2 - 4.6f, Curb + 5.2f, face - 0.5f), new Vector3((x0 + x1) / 2 + 4.6f, Curb + 5.8f, face - 0.3f), Gfx.Hex(0x34495E), 1f, null);
    }

    private static void TeaHouse(BuildContext b, float x0, float x1, float face)
    {
        Props.Building(b, new Props.BuildingSpec
        {
            MinX = x0, MaxX = x1, FacadeZ = face, Facing = 1, Depth = 13.5f, Floors = 3, Color = "C8735A", Shop = "ÇAY OCAĞI",
            ShopColor = "6E2C00", AwningA = "784212", AwningB = "F5CBA7", Pitched = true,
        });
        // Disarida masa ve amcalar
        var cx = (x0 + x1) / 2;
        for (var i = 0; i < 2; i++)
        {
            var t = new Vector3(cx - 1.6f + i * 3.2f, Curb, face + 1.6f);
            b.ColliderYaw(t, new Vector3(0.7f, 0.75f, 0.7f), 0, ColliderFlags.Surface);
            b.Draw(M.Wood, t, m =>
            {
                Shapes.Box(m, Matrix4x4.CreateTranslation(t + new Vector3(0, 0.72f, 0)), new Vector3(0.7f, 0.05f, 0.7f), Gfx.Hex(0x8B5A2B));
                Shapes.Cylinder(m, Matrix4x4.CreateTranslation(t), 0.04f, 0.7f, 6, Gfx.Hex(0x3B3B3B));
            });
            // Cay bardaklari
            b.Draw(M.Glass, t, m =>
            {
                Shapes.Frustum(m, Matrix4x4.CreateTranslation(t + new Vector3(0.12f, 0.745f, 0.1f)), 0.025f, 0.03f, 0.08f, 8, new Color(190, 80, 40, 200));
                Shapes.Frustum(m, Matrix4x4.CreateTranslation(t + new Vector3(-0.15f, 0.745f, -0.05f)), 0.025f, 0.03f, 0.08f, 8, new Color(190, 80, 40, 200));
            });
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var seat = t + new Vector3(side * 0.75f, 0, 0);
                b.Draw(M.Wood, seat, m => Shapes.BoxOnGround(m, Matrix4x4.CreateTranslation(seat), new Vector3(0.4f, 0.42f, 0.4f), Gfx.Hex(0x6E4B2A)));
                b.Layout.Decor.Add(new DecorNpc(seat + new Vector3(0, 0.42f, 0), side > 0 ? MathF.PI / 2 : -MathF.PI / 2, (uint)(i * 7 + s + 3), true, "balikci"));
            }
        }
    }

    private static void AlleyWalls(BuildContext b, float x0, float x1, float face, float dir)
    {
        // Ara sokak zemini (kaldirim yuksekliginde) ve uc duvar
        var depth = 22f;
        var z0 = face;
        var z1 = face + dir * depth;
        b.Box(M.Cobble, new Vector3(x0, 0, MathF.Min(z0, z1)), new Vector3(x1, Curb, MathF.Max(z0, z1)), Gfx.Hex(0xB8AD9A), 0.33f, ColliderFlags.Surface);
        b.Box(M.Brick, new Vector3(x0, 0, MathF.Min(z1, z1 - dir * 0.4f)), new Vector3(x1, 3.2f, MathF.Max(z1, z1 - dir * 0.4f)), Color.White, 1f);
        // Ip uzerinde camasir
        var y = 6.5f;
        b.Draw(M.Fabric, new Vector3((x0 + x1) / 2, y, z0 + dir * 8), m =>
        {
            for (var k = 0; k < 5; k++)
            {
                var px = x0 + 1 + k * (x1 - x0 - 2) / 5f;
                var col = BuildContext.Hex(Props.FacadeColors[(k * 5 + 3) % Props.FacadeColors.Length]);
                Shapes.Box(m, Matrix4x4.CreateTranslation(px, y - 0.4f, z0 + dir * 8), new Vector3(0.6f, 0.7f, 0.02f), col);
            }
        });
        b.Draw(M.Metal, new Vector3((x0 + x1) / 2, y, z0 + dir * 8), m => Shapes.Beam(m, new Vector3(x0, y, z0 + dir * 8), new Vector3(x1, y, z0 + dir * 8), 0.015f, Gfx.Hex(0x333333)));
        var mid = (x0 + x1) / 2;
        var near = b.Layout.Nav.Add(new Vector3(mid, Curb, face + dir * -2.2f));
        var far = b.Layout.Nav.Add(new Vector3(mid, Curb, z1 - dir * 1.5f), NavTag.Edge, "alley");
        b.Layout.Nav.Connect(near, far);
        b.Layout.CatSpawns.Add(new Vector3(mid, Curb, z1 - dir * 2.5f));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Guney cephe
    // ═══════════════════════════════════════════════════════════════════
    private static void SouthSide(BuildContext b)
    {
        var face = 8.5f;
        void Apt(float x0, float x1, int floors, string color, string? shop = null, string shopColor = "C0392B", string aw = "C0392B", bool pitched = true)
            => Props.Building(b, new Props.BuildingSpec
            {
                MinX = x0, MaxX = x1, FacadeZ = face, Facing = -1, Depth = 13.5f, Floors = floors, Color = color, Shop = shop,
                ShopColor = shopColor, AwningA = aw, Pitched = pitched,
            });

        Apt(-132, -110, 5, "EFEBE4", "KIRTASİYE", "2471A3", "5DADE2");
        Apt(-110, -92, 4, "D7C4E0", "MARKET", "D35400", "E67E22", pitched: false);
        Park(b, -92, -72, face);
        Apt(-72, -59, 5, "EED9A0", "LOKANTA", "922B21", "C0392B");
        Apt(-59, -45, 4, "A9C4D6");
        Hospital(b, -45, -15, face);
        AlleyWalls(b, -15, -11, face, 1);
        Apt(-11, 4, 5, "E3B5A4", "TEKEL", "1B4F72", "2874A6");
        Apt(4, 18, 4, "B7D3B5", "PASTANE", "AF601A", "F5B041");
        Stadium(b, 18, 55, face);
    }

    private static void Park(BuildContext b, float x0, float x1, float face)
    {
        b.Box(M.Grass, new Vector3(x0, 0, face), new Vector3(x1, Curb + 0.05f, face + 16), Color.White, 0.25f, ColliderFlags.Surface);
        // Cevre bordur
        b.Box(M.Concrete, new Vector3(x0, 0, face), new Vector3(x1, Curb + 0.25f, face + 0.25f), Gfx.Hex(0xBDB6AA), 0.5f, null);
        Props.Tree(b, new Vector3(x0 + 4, Curb, face + 5), 1.3f);
        Props.Tree(b, new Vector3(x1 - 5, Curb, face + 6), 1.2f);
        Props.Tree(b, new Vector3((x0 + x1) / 2, Curb, face + 11), 1.4f);
        Props.Bench(b, new Vector3((x0 + x1) / 2 - 3, Curb + 0.05f, face + 2.2f), MathF.PI);
        Props.Bench(b, new Vector3((x0 + x1) / 2 + 3, Curb + 0.05f, face + 2.2f), MathF.PI);
        // Arka duvar
        b.Box(M.Brick, new Vector3(x0, 0, face + 16), new Vector3(x1, 2.2f, face + 16.4f), Color.White, 1f);
        // Cesme
        var f = new Vector3((x0 + x1) / 2, Curb + 0.05f, face + 7);
        b.ColliderYaw(f, new Vector3(2.2f, 0.7f, 2.2f), 0);
        b.Draw(M.Concrete, f, m =>
        {
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(f), [new(0, 0), new(1.1f, 0), new(1.1f, 0.5f), new(0.95f, 0.5f), new(0.95f, 0.3f), new(0, 0.3f)], 16, Gfx.Hex(0xC9C1B1));
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(f + new Vector3(0, 0.3f, 0)), 0.15f, 1.2f, 8, Gfx.Hex(0xBFB6A5));
        });
        b.Draw(M.Water, f, m => Shapes.Disc(m, Matrix4x4.CreateTranslation(f + new Vector3(0, 0.45f, 0)), 0.95f, 16, Color.White));
        b.Layout.CatSpawns.Add(new Vector3(x0 + 2, Curb, face + 3));
        b.Layout.Nav.Add(new Vector3((x0 + x1) / 2, Curb, face + 3), NavTag.Door, "park");
    }

    private static void Hospital(BuildContext b, float x0, float x1, float face)
    {
        Props.Building(b, new Props.BuildingSpec
        {
            MinX = x0, MaxX = x1, FacadeZ = face + 4, Facing = -1, Depth = 14, Floors = 5, Color = "EDEFF0", Pitched = false, Cumba = false,
            PlainGround = true, GroundFloor = 4f,
        });
        // On avlu
        b.Box(M.Pavers, new Vector3(x0, 0, face), new Vector3(x1, Curb, face + 4), Gfx.Hex(0xD5D0C8), 0.5f, ColliderFlags.Surface);
        var cx = (x0 + x1) / 2;
        // Giris saçagi
        b.Box(M.Concrete, new Vector3(cx - 4, Curb + 3.2f, face + 0.5f), new Vector3(cx + 4, Curb + 3.5f, face + 4), Gfx.Hex(0xE5E8E8), 0.5f, null);
        foreach (var x in new[] { cx - 3.6f, cx + 3.6f })
        {
            b.Box(M.Concrete, new Vector3(x - 0.15f, Curb, face + 0.7f), new Vector3(x + 0.15f, Curb + 3.2f, face + 1f), Gfx.Hex(0xD0D3D4), 0.5f);
        }

        b.Draw(M.WindowGlass, new Vector3(cx, Curb, face + 3.95f), m => Shapes.Quad(m, new Vector3(cx + 2, Curb, face + 3.95f), new Vector3(cx - 2, Curb, face + 3.95f),
            new Vector3(cx - 2, Curb + 2.8f, face + 3.95f), new Vector3(cx + 2, Curb + 2.8f, face + 3.95f), new Color(70, 90, 100, 128)));
        b.SignBoard("DEVLET HASTANESİ", new Vector3(cx, Curb + 4.6f, face + 3.88f), 8f, 1.0f, -Vector3.UnitZ, Gfx.Hex(0xFFFFFF), Gfx.Hex(0xC0392B), 1024, 128, lit: true);
        // Kirmizi arti isaretli kutu
        b.Box(M.Emissive, new Vector3(cx + 5.5f, Curb + 4.3f, face + 3.8f), new Vector3(cx + 6.4f, Curb + 5.2f, face + 3.9f), Gfx.Hex(0xC0392B), 1f, null);
        for (var x = x0 + 2; x < x1 - 1; x += 3)
        {
            if (MathF.Abs(x - cx) < 4.5f)
            {
                continue;
            }

            Props.Window(b, new Vector3(x, Curb + 1.0f, face + 4), -1, 1.5f, 1.8f, true, Gfx.Hex(0xEDEFF0));
        }

        b.Layout.Nav.Add(new Vector3(cx, Curb, face + 3.2f), NavTag.Door, "hastane");
    }

    private static void Stadium(BuildContext b, float x0, float x1, float face)
    {
        var wallZ = face + 1.5f;
        // Dev tribun duvari (egimli kademeler)
        for (var k = 0; k < 4; k++)
        {
            var z0 = wallZ + k * 3.5f;
            b.Box(M.Concrete, new Vector3(x0, 0, z0), new Vector3(x1, 4 + k * 3.5f, z0 + 3.6f), Gfx.Lerp(Gfx.Hex(0xA6ACAF), Gfx.Hex(0x7F8C8D), k / 4f), 0.25f);
        }

        // Renkli seritler (takim renkleri)
        b.Box(M.Plaster, new Vector3(x0, 2.2f, wallZ - 0.05f), new Vector3(x1, 3.2f, wallZ), Gfx.Hex(0xF4D03F), 0.5f, null);
        b.Box(M.Plaster, new Vector3(x0, 3.2f, wallZ - 0.05f), new Vector3(x1, 3.9f, wallZ), Gfx.Hex(0x1F618D), 0.5f, null);
        var cx = (x0 + x1) / 2;
        // Kapi
        b.Box(M.Concrete, new Vector3(cx - 3, Curb, wallZ - 0.06f), new Vector3(cx + 3, Curb + 3.2f, wallZ + 0.01f), Gfx.Hex(0x2B2B2B), 1f, null);
        b.SignBoard("SAHİLSPOR STADYUMU", new Vector3(cx, 5.2f, wallZ - 0.06f), 10f, 1.3f, -Vector3.UnitZ, Gfx.Hex(0x1F618D), Gfx.Hex(0xF4D03F), 1024, 140, lit: true);
        // Isik kuleleri
        foreach (var x in new[] { x0 + 2, x1 - 2 })
        {
            var p = new Vector3(x, 0, wallZ + 12);
            b.Draw(M.Metal, p, m =>
            {
                Shapes.Frustum(m, Matrix4x4.CreateTranslation(p), 0.5f, 0.3f, 24, 6, Gfx.Hex(0x5D6D7E));
                Shapes.Box(m, Matrix4x4.CreateRotationX(-0.4f) * Matrix4x4.CreateTranslation(p + new Vector3(0, 25, -0.5f)), new Vector3(4, 2.4f, 0.4f), Gfx.Hex(0x34495E));
            });
            b.Draw(M.Emissive, p, m => Shapes.Box(m, Matrix4x4.CreateRotationX(-0.4f) * Matrix4x4.CreateTranslation(p + new Vector3(0, 25, -0.75f)), new Vector3(3.6f, 2.0f, 0.05f), new Color(255, 250, 230, 255)));
        }

        // Bilet gisesi
        var kiosk = new Vector3(cx + 6, Curb, face + 0.2f);
        b.Box(M.Plaster, kiosk + new Vector3(-1, 0, 0), kiosk + new Vector3(1, 2.6f, 1.3f), Gfx.Hex(0x1F618D), 0.5f);
        b.Layout.Nav.Add(new Vector3(cx, Curb, face - 0.5f), NavTag.Door, "stadyum");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sahil seridi, iskele, deniz
    // ═══════════════════════════════════════════════════════════════════
    private static void Promenade(BuildContext b)
    {
        var L = b.Layout;
        var pv = Gfx.Hex(0xD8CFC0);
        b.Box(M.Pavers, new Vector3(55, 0, 4.2f), new Vector3(MaxX + 10, Curb, 16), pv, 0.5f, ColliderFlags.Surface);
        b.Box(M.Concrete, new Vector3(55, 0, 4f), new Vector3(MaxX + 10, Curb, 4.2f), Gfx.Hex(0xBDB6AA), 0.5f, ColliderFlags.Surface);
        // Rihtim duvari (denize inen)
        b.Box(M.Concrete, new Vector3(-200, -4f, 16), new Vector3(MaxX + 40, Curb, 16.6f), Gfx.Hex(0x9C9588), 0.25f, null);
        // Korkuluk (iskele girisi haric)
        Props.Railing(b, new Vector3(55, Curb, 15.85f), new Vector3(98, Curb, 15.85f));
        Props.Railing(b, new Vector3(106, Curb, 15.85f), new Vector3(MaxX, Curb, 15.85f));
        // Iskele platformu
        var deckTop = Curb;
        b.Box(M.Wood, new Vector3(97, -0.3f, 16), new Vector3(107, deckTop, 44), Gfx.Hex(0xA98B6A), 0.5f, ColliderFlags.Surface);
        for (var z = 18f; z < 44; z += 4)
        {
            foreach (var x in new[] { 97.4f, 106.6f })
            {
                b.Draw(M.Wood, new Vector3(x, 0, z), m => Shapes.Cylinder(m, Matrix4x4.CreateTranslation(x, -4, z), 0.25f, 4f, 8, Gfx.Hex(0x5D4632)));
            }
        }

        Props.Railing(b, new Vector3(97.2f, deckTop, 16.5f), new Vector3(97.2f, deckTop, 43.5f));
        Props.Railing(b, new Vector3(106.8f, deckTop, 16.5f), new Vector3(106.8f, deckTop, 43.5f));
        // Iskele binasi (bekleme salonu)
        var bx0 = 98f;
        var bx1 = 106f;
        var bz0 = 26f;
        var bz1 = 36f;
        b.Box(M.Plaster, new Vector3(bx0, deckTop, bz0), new Vector3(bx1, deckTop + 4.5f, bz1), Gfx.Hex(0xF2F3F4), 0.5f);
        b.Draw(M.Roof, new Vector3(102, 4.65f, 31), m => Shapes.Wedge(m, Matrix4x4.CreateTranslation(102, deckTop + 4.5f, 31), new Vector3(9.2f, 2.4f, 11f), Color.White, 1f));
        b.Box(M.Concrete, new Vector3(100.5f, deckTop, bz0 - 0.05f), new Vector3(103.5f, deckTop + 2.8f, bz0 + 0.02f), Gfx.Hex(0x2B2B2B), 1f, null);
        b.SignBoard("VAPUR İSKELESİ", new Vector3(102, deckTop + 3.6f, bz0 - 0.06f), 6f, 0.9f, -Vector3.UnitZ, Gfx.Hex(0x154360), Color.White, lit: true);
        // Saat
        b.Draw(M.Emissive, new Vector3(102, 5.8f, bz0 - 0.1f), m => Shapes.Box(m, Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(102, deckTop + 5.3f, bz0 - 0.1f), new Vector3(1f, 0.05f, 1f), Gfx.Hex(0xFDFEFE)));
        L.PierDoor = new Vector3(102, deckTop, bz0 - 0.8f);
        L.FerryDock = new Vector3(102, 0, 52);
        L.FerryFar = new Vector3(60, 0, 420);
        L.SeagullPerches.AddRange([new Vector3(97.3f, deckTop + 1.1f, 20), new Vector3(106.7f, deckTop + 1.1f, 24), new Vector3(80, Curb + 1.1f, 15.85f), new Vector3(118, Curb + 1.1f, 15.85f)]);

        // Sahil boyunca banklar ve fenerler
        for (var x = 62f; x < MaxX - 2; x += 15)
        {
            if (x > 92 && x < 112)
            {
                continue;
            }

            Props.Bench(b, new Vector3(x + 4, Curb, 14.6f), 0);
        }

        // Deniz
        L.Sea = new AreaRect(-600, 16, 700, 900);
        L.SeaLevel = -1.1f;
        // Denize dusmesin
        b.Collider(new Vector3(MinX - 40, -5, 16.4f), new Vector3(97, 4, 16.6f), ColliderFlags.Solid | ColliderFlags.Invisible);
        b.Collider(new Vector3(107, -5, 16.4f), new Vector3(MaxX + 40, 4, 16.6f), ColliderFlags.Solid | ColliderFlags.Invisible);
        b.Collider(new Vector3(96.6f, -5, 16), new Vector3(97, 4, 44), ColliderFlags.Solid | ColliderFlags.Invisible);
        b.Collider(new Vector3(107, -5, 16), new Vector3(107.4f, 4, 44), ColliderFlags.Solid | ColliderFlags.Invisible);
        b.Collider(new Vector3(97, -5, 44), new Vector3(107, 4, 44.4f), ColliderFlags.Solid | ColliderFlags.Invisible);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Depo (oyuncunun mutfagi)
    // ═══════════════════════════════════════════════════════════════════
    private static void Depot(BuildContext b)
    {
        var L = b.Layout;
        const float x0 = -112, x1 = -96, zf = -8.5f, zb = -22f;
        const float doorX0 = -107f, doorX1 = -100.5f;
        const float h = 4.4f;
        var wall = Gfx.Hex(0xE6DDCF);
        var outer = Gfx.Hex(0xC9B08B);
        L.DepotArea = new AreaRect(x0, zb, x1, zf + 0.5f);

        // Zemin (fayans) ve tavan
        b.Box(M.Tiles, new Vector3(x0, 0, zb), new Vector3(x1, Curb, zf), Gfx.Hex(0xD9D4CC), 0.5f, ColliderFlags.Surface);
        b.Box(M.Plaster, new Vector3(x0, Curb + h, zb), new Vector3(x1, Curb + h + 0.3f, zf), Gfx.Hex(0xF2EFE8), 0.5f);
        // Duvarlar (ic yuzey acik renk, dis yuzey bina rengi icin ayri kabuk)
        b.Box(M.Tiles, new Vector3(x0, Curb, zb), new Vector3(x1, Curb + h, zb + 0.3f), wall, 1f);
        b.Box(M.Plaster, new Vector3(x0, Curb, zb), new Vector3(x0 + 0.3f, Curb + h, zf), wall, 0.5f);
        b.Box(M.Plaster, new Vector3(x1 - 0.3f, Curb, zb), new Vector3(x1, Curb + h, zf), wall, 0.5f);
        // On duvar: kapinin iki yani ve ustu
        b.Box(M.Brick, new Vector3(x0, Curb, zf - 0.3f), new Vector3(doorX0, Curb + h, zf), Color.White, 1f);
        b.Box(M.Brick, new Vector3(doorX1, Curb, zf - 0.3f), new Vector3(x1, Curb + h, zf), Color.White, 1f);
        b.Box(M.Brick, new Vector3(doorX0, Curb + 3.4f, zf - 0.3f), new Vector3(doorX1, Curb + h, zf), Color.White, 1f);
        // Kepenk (yukari sarilmis)
        b.Box(M.Metal, new Vector3(doorX0 - 0.1f, Curb + 3.4f, zf), new Vector3(doorX1 + 0.1f, Curb + 3.85f, zf + 0.35f), Gfx.Hex(0x9AA2A8), 1f, null);
        // Ust katlar (depo binasi apartmanin zemin kati)
        b.Box(M.Plaster, new Vector3(x0, Curb + h, zb), new Vector3(x1, Curb + h + 9.3f, zf), outer, 0.5f);
        for (var fl = 0; fl < 3; fl++)
        {
            for (var c = 0; c < 5; c++)
            {
                Props.Window(b, new Vector3(x0 + 1.6f + c * 3.2f, Curb + h + 0.9f + fl * 3.1f, zf), 1, 1.1f, 1.5f, b.Rng.Chance(0.5f), outer);
            }
        }

        b.Draw(M.Roof, new Vector3((x0 + x1) / 2, Curb + h + 9.3f, (zf + zb) / 2), m =>
            Shapes.Wedge(m, Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation((x0 + x1) / 2, Curb + h + 9.3f, (zf + zb) / 2), new Vector3(14f, 2.6f, 16.4f), Color.White, 1f));
        b.SignBoard("PİLAVCI DEPOSU", new Vector3((doorX0 + doorX1) / 2, Curb + 4.05f, zf + 0.04f), 5.8f, 0.8f, Vector3.UnitZ, Gfx.Hex(0xE8792E), Color.White, lit: true);

        // Arka tezgah (ocak, lavabo, kesme tahtasi bunun ustunde)
        var counterTop = Curb + 0.9f;
        // Lavabo (x -106) kendi dolabini ve teknesini cizer: gorsel tezgah orada bolunur, carpisma kutulari tek parca kalir.
        foreach (var (cx0, cx1) in new[] { (x0 + 0.3f, -106.45f), (-105.55f, -100.8f) })
        {
            b.Box(M.Wood, new Vector3(cx0, Curb, zb + 0.3f), new Vector3(cx1, counterTop - 0.05f, zb + 1.0f), Gfx.Hex(0xB9A58B), 0.5f, null);
            b.Box(M.Steel, new Vector3(cx0, counterTop - 0.05f, zb + 0.3f), new Vector3(cx1, counterTop, zb + 1.02f), Color.White, 0.5f, null);
        }

        b.Collider(new Vector3(x0 + 0.3f, Curb, zb + 0.3f), new Vector3(-100.8f, counterTop - 0.05f, zb + 1.0f));
        b.Collider(new Vector3(x0 + 0.3f, counterTop - 0.05f, zb + 0.3f), new Vector3(-100.8f, counterTop, zb + 1.02f), ColliderFlags.Surface);
        // Ada tezgah
        b.Box(M.Wood, new Vector3(-106.4f, Curb, -17.5f), new Vector3(-102.6f, counterTop - 0.05f, -16.4f), Gfx.Hex(0xA48A6A), 0.5f);
        b.Box(M.Steel, new Vector3(-106.5f, counterTop - 0.05f, -17.6f), new Vector3(-102.5f, counterTop, -16.3f), Color.White, 0.5f, ColliderFlags.Surface);
        // Laptop masasi
        b.Box(M.WoodDark, new Vector3(-98.4f, Curb + 0.72f, -13.8f), new Vector3(-96.4f, Curb + 0.77f, -12.0f), Color.White, 0.5f, ColliderFlags.Surface);
        foreach (var (lx, lz) in new[] { (-98.3f, -13.7f), (-98.3f, -12.1f) })
        {
            b.Box(M.Metal, new Vector3(lx, Curb, lz), new Vector3(lx + 0.06f, Curb + 0.72f, lz + 0.06f), Gfx.Hex(0x333333), 1f, ColliderFlags.Solid);
        }

        // Duvar rafi (sus) ve takvim, pilav afisi
        b.Box(M.Wood, new Vector3(x0 + 0.3f, Curb + 1.75f, zb + 0.3f), new Vector3(-100.8f, Curb + 1.8f, zb + 0.6f), Gfx.Hex(0xA48A6A), 0.5f, null);
        b.SignBoard("PİLAV = SEVGİ", new Vector3(-98.2f, Curb + 2.3f, zb + 0.32f), 1.6f, 0.9f, Vector3.UnitZ, Gfx.Hex(0xFFF4E2), Gfx.Hex(0xB9541A), 512, 280);
        // Paspas
        b.Box(M.Fabric, new Vector3(-105, Curb, -10.2f), new Vector3(-102.5f, Curb + 0.01f, -9f), Gfx.Hex(0x7B241C), 1f, null);
        // Ic lambalar
        L.Lamps.Add(new LampInfo(new Vector3(-104, Curb + h - 0.4f, -18f), 9f, new Vector3(2.2f, 2.0f, 1.7f), LampKind.Indoor));
        L.Lamps.Add(new LampInfo(new Vector3(-104, Curb + h - 0.4f, -12f), 9f, new Vector3(2.2f, 2.0f, 1.7f), LampKind.Indoor));
        foreach (var lz in new[] { -18f, -12f })
        {
            var lp = new Vector3(-104, Curb + h - 0.25f, lz);
            b.Draw(M.Emissive, lp, m => Shapes.Box(m, Matrix4x4.CreateTranslation(lp), new Vector3(1.4f, 0.06f, 0.25f), new Color(255, 250, 235, 255)));
        }

        // ── Istasyonlar ──────────────────────────────────────────────
        var backYaw = MathF.PI; // yuzu guneye (odaya) bakar
        L.Stations.Add(new StationPlacement(StationType.Stovetop, new Vector3(-109.4f, counterTop, zb + 0.65f), backYaw, "stovetop"));
        L.Stations.Add(new StationPlacement(StationType.Sink, new Vector3(-106.0f, Curb, zb + 0.65f), backYaw, "sink"));
        L.Stations.Add(new StationPlacement(StationType.CuttingBoard, new Vector3(-103.2f, counterTop, zb + 0.62f), backYaw, "board"));
        L.Stations.Add(new StationPlacement(StationType.Trash, new Vector3(-100.1f, Curb, zb + 0.75f), backYaw, "trash"));
        L.Stations.Add(new StationPlacement(StationType.KazanOcagi, new Vector3(-110.9f, Curb, -18.2f), -MathF.PI / 2, "ocakA"));
        L.Stations.Add(new StationPlacement(StationType.KazanOcagi, new Vector3(-110.9f, Curb, -16.0f), -MathF.PI / 2, "ocakB"));
        L.Stations.Add(new StationPlacement(StationType.Fridge, new Vector3(-96.8f, Curb, -20.6f), MathF.PI / 2, "fridge"));
        L.Stations.Add(new StationPlacement(StationType.Pantry, new Vector3(-96.95f, Curb, -16.6f), MathF.PI / 2, "pantry"));
        L.Stations.Add(new StationPlacement(StationType.Laptop, new Vector3(-97.3f, Curb, -12.9f), MathF.PI / 2, "laptop"));
        L.Stations.Add(new StationPlacement(StationType.Bed, new Vector3(-111.2f, Curb, -11.4f), -MathF.PI / 2, "bed"));
        L.Stations.Add(new StationPlacement(StationType.Pallet, new Vector3(-94.2f, Curb, -6.5f), 0, "pallet"));
        L.Stations.Add(new StationPlacement(StationType.Cart, new Vector3(-103.6f, Curb, -12.6f), -MathF.PI / 2, "cart"));

        L.PalletPos = new Vector3(-94.2f, Curb, -6.5f);
        L.PlayerSpawn = new Vector3(-108.5f, Curb, -12.6f);
        L.PlayerSpawnYaw = -MathF.PI / 2;

        // ── Baslangic esyalari ───────────────────────────────────────
        L.Items.Add(new ItemPlacement(ItemType.Kazan, Vector3.Zero, 0, "ocakA", 0));
        L.Items.Add(new ItemPlacement(ItemType.Tencere, Vector3.Zero, 0, "stovetop", 0));
        L.Items.Add(new ItemPlacement(ItemType.Tencere, Vector3.Zero, 0, "stovetop", 1));
        L.Items.Add(new ItemPlacement(ItemType.Suzgec, new Vector3(-105.6f, counterTop, -16.9f), 0.3f));
        L.Items.Add(new ItemPlacement(ItemType.OlcuKabi, new Vector3(-104.6f, counterTop, -17.1f), 0));
        L.Items.Add(new ItemPlacement(ItemType.Kasik, new Vector3(-103.8f, counterTop, -16.8f), 1.2f));
        L.Items.Add(new ItemPlacement(ItemType.TuzKutusu, new Vector3(-108.0f, counterTop, zb + 0.6f), 0));

        // Depo icine giris dugumu (musteriler girmez; teslimatci ve araba yolu icin)
        L.VanStop = new Vector3(-94f, 0, -2.2f);
        L.VanEntry = new Vector3(MaxX + 15, 0, -2.2f);
        L.VanExit = new Vector3(MinX - 15, 0, -2.2f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Kiralik dukkan (son asama)
    // ═══════════════════════════════════════════════════════════════════
    private static void Shop(BuildContext b)
    {
        var L = b.Layout;
        const float x0 = 30, x1 = 42, zf = -8.5f, zb = -21f;
        const float h = 4.2f;
        L.ShopArea = new AreaRect(x0, zb, x1, zf + 0.5f);
        var wall = Gfx.Hex(0xF3E5C8);
        b.Box(M.Checker, new Vector3(x0, 0, zb), new Vector3(x1, Curb, zf), Color.White, 0.5f, ColliderFlags.Surface);
        b.Box(M.Plaster, new Vector3(x0, Curb + h, zb), new Vector3(x1, Curb + h + 0.3f, zf), Gfx.Hex(0xF7F2E8), 0.5f);
        b.Box(M.Plaster, new Vector3(x0, Curb, zb), new Vector3(x1, Curb + h, zb + 0.3f), wall, 0.5f);
        b.Box(M.Plaster, new Vector3(x0, Curb, zb), new Vector3(x0 + 0.3f, Curb + h, zf), wall, 0.5f);
        b.Box(M.Plaster, new Vector3(x1 - 0.3f, Curb, zb), new Vector3(x1, Curb + h, zf), wall, 0.5f);
        b.Box(M.Wood, new Vector3(x0, Curb, zf - 0.3f), new Vector3(x0 + 1.5f, Curb + h, zf), Gfx.Hex(0x7B4F2C), 1f);
        b.Box(M.Wood, new Vector3(x1 - 1.5f, Curb, zf - 0.3f), new Vector3(x1, Curb + h, zf), Gfx.Hex(0x7B4F2C), 1f);
        b.Box(M.Wood, new Vector3(x0 + 1.5f, Curb + 3.3f, zf - 0.3f), new Vector3(x1 - 1.5f, Curb + h, zf), Gfx.Hex(0x7B4F2C), 1f);
        b.Box(M.Plaster, new Vector3(x0, Curb + h, zb), new Vector3(x1, Curb + h + 9.3f, zf), Gfx.Hex(0xE3B5A4), 0.5f);
        for (var fl = 0; fl < 3; fl++)
        {
            for (var c = 0; c < 4; c++)
            {
                Props.Window(b, new Vector3(x0 + 1.6f + c * 2.9f, Curb + h + 0.9f + fl * 3.1f, zf), 1, 1.1f, 1.5f, b.Rng.Chance(0.5f), Gfx.Hex(0xE3B5A4));
            }
        }

        b.Draw(M.Roof, new Vector3((x0 + x1) / 2, Curb + h + 9.3f, (zf + zb) / 2), m =>
            Shapes.Wedge(m, Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation((x0 + x1) / 2, Curb + h + 9.3f, (zf + zb) / 2), new Vector3(13f, 2.4f, 12.4f), Color.White, 1f));
        // Masalar (istasyon) ve sandalyeler
        var tables = new[] { new Vector3(33, Curb, -11.5f), new Vector3(39, Curb, -11.5f), new Vector3(33, Curb, -14.6f), new Vector3(39, Curb, -14.6f) };
        for (var i = 0; i < tables.Length; i++)
        {
            L.Stations.Add(new StationPlacement(StationType.Table, tables[i], 0, "table" + i));
        }

        L.Lamps.Add(new LampInfo(new Vector3(36, Curb + h - 0.4f, -13f), 10f, new Vector3(2.4f, 2.0f, 1.5f), LampKind.Indoor));
        L.Lamps.Add(new LampInfo(new Vector3(36, Curb + h - 0.4f, -18f), 8f, new Vector3(2.4f, 2.0f, 1.5f), LampKind.Indoor));
        // Kepenk: dukkan alinana kadar kapali (dinamik kapi)
        L.ShopShutter = BoxCollider.FromMinMax(new Vector3(x0 + 1.5f, Curb, zf - 0.1f), new Vector3(x1 - 1.5f, Curb + 3.3f, zf + 0.1f), ColliderFlags.Default | ColliderFlags.Dynamic);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sokak esyalari
    // ═══════════════════════════════════════════════════════════════════
    private static void StreetFurniture(BuildContext b)
    {
        var L = b.Layout;
        // Lambalar
        for (var x = MinX + 6; x < MaxX; x += 18)
        {
            // Depo kepenginin onu (x -107..-100.5) bos kalsin: araba buradan cikar.
            var lx = x is > -108f and < -99f ? -109.2f : x;
            Props.StreetLamp(b, new Vector3(lx, Curb, -4.6f), Vector3.UnitZ);
            if (x < 52)
            {
                Props.StreetLamp(b, new Vector3(x + 9, Curb, 4.6f), -Vector3.UnitZ);
            }
        }

        for (var x = 60f; x < MaxX; x += 16)
        {
            Props.StreetLamp(b, new Vector3(x, Curb, 15.3f), -Vector3.UnitZ);
        }

        // Agaclar (kuzey kaldirimi; depo/okul/dukkan onu bos)
        bool Clear(float x) => !(x > -113 && x < -93) && !(x > -78 && x < -60) && !(x > 28 && x < 44) && !(x > -2 && x < 12) &&
                               !L.Crosswalks.Any(c => MathF.Abs(c - x) < 3);
        for (var x = MinX + 12; x < MaxX; x += 14)
        {
            if (Clear(x))
            {
                Props.Tree(b, new Vector3(x, Curb, -5.6f), 0.9f);
            }
        }

        for (var x = MinX + 5; x < 50; x += 16)
        {
            if (Clear(x) && !(x > -40 && x < -20) && !(x > 25 && x < 45))
            {
                Props.Tree(b, new Vector3(x, Curb, 5.6f), 0.85f);
            }
        }

        // Cop kutulari
        // Depo kapisinin onu bos kalir: araba buradan itilerek cikar.
        foreach (var x in new[] { -90.5f, -60f, -20f, 15f, 52f, 70f, 115f })
        {
            Props.Bin(b, new Vector3(x, Curb, x < 55 && x > -60 ? 7.8f : -7.8f));
        }

        // Park etmis arabalar
        var colors = new[] { 0xC0392B, 0xECF0F1, 0x2C3E50, 0x7F8C8D, 0x2471A3, 0xF1C40F, 0x1E8449 };
        var rng = b.Rng;
        for (var x = MinX + 8; x < MaxX - 6; x += 9.5f)
        {
            if (L.Crosswalks.Any(c => MathF.Abs(c - x) < 6) || (x > -112 && x < -88) || (x > -80 && x < -58) || (x > -40 && x < -18) ||
                (x > -4 && x < 14) || (x > 26 && x < 48) || (x > 84 && x < 102))
            {
                continue;
            }

            if (!rng.Chance(0.55f))
            {
                continue;
            }

            var north = rng.Chance(0.5f);
            var col = Gfx.Hex((uint)colors[rng.Range(0, colors.Length)]);
            Props.ParkedCar(b, new Vector3(x, 0, north ? -3.05f : 3.05f), north ? MathF.PI : 0, col);
        }

        b.Rng = rng;

        // Otobus duragi
        var stop = new Vector3(-62, Curb, 7.4f);
        b.ColliderYaw(stop + new Vector3(0, 0, 0.5f), new Vector3(3.2f, 2.6f, 0.15f), 0);
        b.Draw(M.Metal, stop, m =>
        {
            Shapes.Box(m, Matrix4x4.CreateTranslation(stop + new Vector3(0, 2.6f, 0)), new Vector3(3.4f, 0.1f, 1.4f), Gfx.Hex(0x2E4053));
            foreach (var x in new[] { -1.6f, 1.6f })
            {
                Shapes.Box(m, Matrix4x4.CreateTranslation(stop + new Vector3(x, 1.3f, 0.6f)), new Vector3(0.08f, 2.6f, 0.08f), Gfx.Hex(0x2E4053));
            }
        });
        b.Draw(M.Glass, stop, m => Shapes.Box(m, Matrix4x4.CreateTranslation(stop + new Vector3(0, 1.4f, 0.6f)), new Vector3(3.2f, 2.0f, 0.03f), new Color(200, 220, 230, 60)));
        Props.Bench(b, stop + new Vector3(0, 0, 0.2f), MathF.PI);
        b.SignBoard("DURAK", stop + new Vector3(1.2f, 2.9f, -0.2f), 1.2f, 0.4f, -Vector3.UnitZ, Gfx.Hex(0x2E86C1), Color.White, 256, 96);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Arka plan: tepeler, cami, kule, karsi kiyi
    // ═══════════════════════════════════════════════════════════════════
    private static void Background(BuildContext b)
    {
        if (!b.Visual)
        {
            return;
        }

        var rng = new Core.Rng(77);
        // Kuzeydeki sehir bloklari (sisle kaybolan)
        for (var i = 0; i < 70; i++)
        {
            var x = rng.Range(-260f, 260f);
            var z = rng.Range(-34f, -160f);
            if (z > -40 && MathF.Abs(x) < 140)
            {
                continue;
            }

            var w = rng.Range(8f, 18f);
            var d = rng.Range(8f, 14f);
            var hgt = rng.Range(8f, 22f) + (-z - 30) * 0.12f;
            var col = BuildContext.Hex(Props.FacadeColors[rng.Range(0, Props.FacadeColors.Length)]);
            var y0 = MathF.Max(0, (-z - 40) * 0.12f);
            b.Draw(M.Plaster, new Vector3(x, 0, z), m =>
            {
                Shapes.BoxMinMax(m, new Vector3(x, y0 - 2, z), new Vector3(x + w, y0 + hgt, z + d), col, 0.5f);
                Shapes.Wedge(m, Matrix4x4.CreateTranslation(x + w / 2, y0 + hgt, z + d / 2), new Vector3(w + 0.4f, 2f, d + 0.4f), Gfx.Hex(0xA9533A));
            });
        }

        // Tepeler
        for (var i = 0; i < 12; i++)
        {
            var x = -300 + i * 55 + rng.Range(-10f, 10f);
            var z = -170 + rng.Range(-30f, 10f);
            var r = rng.Range(50f, 90f);
            b.Draw(M.Grass, new Vector3(x, 0, z), m => Shapes.Sphere(m, Matrix4x4.CreateScale(1, 0.35f, 0.7f) * Matrix4x4.CreateTranslation(x, -6, z), r, 6, 12, Gfx.Hex(0x7C9A5A)));
        }

        // Cami (tepede): ana kubbe, yarim kubbeler, iki minare
        var mosque = new Vector3(-30, 16, -140);
        b.Draw(M.Concrete, mosque, m =>
        {
            var stone = Gfx.Hex(0xD8D2C4);
            Shapes.BoxOnGround(m, Matrix4x4.CreateTranslation(mosque), new Vector3(30, 12, 30), stone);
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(mosque + new Vector3(0, 12, 0)), 10, 3, 24, stone);
            for (var k = 0; k < 4; k++)
            {
                var off = Vector3.Transform(new Vector3(13, 12, 0), Matrix4x4.CreateRotationY(k * MathF.PI / 2));
                Shapes.Sphere(m, Matrix4x4.CreateScale(1, 0.8f, 1) * Matrix4x4.CreateTranslation(mosque + off), 5, 6, 14, Gfx.Hex(0x8C9AA3));
            }

            foreach (var mx in new[] { -18f, 18f })
            {
                var mp = mosque + new Vector3(mx, 0, 14);
                Shapes.Cylinder(m, Matrix4x4.CreateTranslation(mp), 1.4f, 34, 12, stone);
                Shapes.Cylinder(m, Matrix4x4.CreateTranslation(mp + new Vector3(0, 24, 0)), 2.0f, 0.8f, 12, stone);
                Shapes.Frustum(m, Matrix4x4.CreateTranslation(mp + new Vector3(0, 34, 0)), 1.5f, 0.05f, 9, 12, Gfx.Hex(0x7A8A93));
            }
        });
        b.Draw(M.Metal, mosque, m => Shapes.Sphere(m, Matrix4x4.CreateScale(1, 0.85f, 1) * Matrix4x4.CreateTranslation(mosque + new Vector3(0, 15, 0)), 10.5f, 10, 24, Gfx.Hex(0x8C9AA3)));

        // Kule (Galata benzeri)
        var tower = new Vector3(-150, 10, -120);
        b.Draw(M.Brick, tower, m =>
        {
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(tower), 7, 34, 16, Gfx.Hex(0xC9B79C));
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(tower + new Vector3(0, 34, 0)), 7.6f, 1.2f, 16, Gfx.Hex(0xB7A58A));
        });
        b.Draw(M.Roof, tower, m => Shapes.Frustum(m, Matrix4x4.CreateTranslation(tower + new Vector3(0, 35.2f, 0)), 7.4f, 0.1f, 14, 16, Gfx.Hex(0x5D6D7E)));

        // Karsi kiyi (denizin oteki yani)
        for (var i = 0; i < 18; i++)
        {
            var x = -500 + i * 60 + rng.Range(-20f, 20f);
            var z = 520 + rng.Range(-40f, 40f);
            var r = rng.Range(70f, 130f);
            b.Draw(M.Grass, new Vector3(x, 0, z), m => Shapes.Sphere(m, Matrix4x4.CreateScale(1, 0.3f, 0.6f) * Matrix4x4.CreateTranslation(x, -10, z), r, 5, 10, Gfx.Hex(0x6E8B5E)));
        }

        for (var i = 0; i < 60; i++)
        {
            var x = rng.Range(-450f, 450f);
            var z = rng.Range(470f, 520f);
            var hgt = rng.Range(6f, 18f);
            var col = BuildContext.Hex(Props.FacadeColors[rng.Range(0, Props.FacadeColors.Length)]);
            b.Draw(M.Plaster, new Vector3(x, 0, z), m => Shapes.BoxMinMax(m, new Vector3(x, 0, z), new Vector3(x + 10, hgt, z + 8), col, 0.5f));
        }

        // Kopru (uzak doguda)
        var bridge = new Vector3(520, 0, 260);
        b.Draw(M.Metal, bridge, m =>
        {
            var col = Gfx.Hex(0xB7BEC2);
            Shapes.Box(m, Matrix4x4.CreateRotationY(-0.9f) * Matrix4x4.CreateTranslation(bridge + new Vector3(0, 40, 0)), new Vector3(800, 3, 22), col);
            foreach (var t in new[] { -160f, 160f })
            {
                var p = bridge + Vector3.Transform(new Vector3(t, 0, 0), Matrix4x4.CreateRotationY(-0.9f));
                Shapes.Box(m, Matrix4x4.CreateTranslation(p + new Vector3(0, 55, 0)), new Vector3(8, 110, 8), col);
            }
        });
    }

    private static void Bounds(BuildContext b)
    {
        // Cadde uclari: gorunmez duvar + gorunur bariyer
        foreach (var x in new[] { MinX - 2f, MaxX + 2f })
        {
            b.Collider(new Vector3(x - 0.5f, -1, -9), new Vector3(x + 0.5f, 5, 16.5f), ColliderFlags.Solid | ColliderFlags.Invisible);
            b.Draw(M.Concrete, new Vector3(x, 0, 0), m =>
            {
                for (var z = -8f; z < 16; z += 2.2f)
                {
                    Shapes.BoxOnGround(m, Matrix4x4.CreateTranslation(x, 0, z + 1.1f), new Vector3(0.6f, 0.9f, 1.9f), z % 4.4f < 2 ? Gfx.Hex(0xE74C3C) : Gfx.Hex(0xF4F6F7));
                }
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Satis noktalari
    // ═══════════════════════════════════════════════════════════════════
    private static void Spots(BuildContext b)
    {
        var L = b.Layout;
        // Kuzey kaldiriminda araba yuzu guneye (musteri caddeden gelir) degil, kaldirima paralel:
        // yaw=0 -> arabanin musteri tarafi -Z (bina tarafi). Kaldirimda musteri arabanin
        // bina tarafinda, oyuncu cadde tarafinda durur.
        L.Spots.Add(new SpotZone { Id = "okul", Center = new Vector3(-69, Curb, -6.3f), Half = new Vector2(6.5f, 2.6f), CartPos = new Vector3(-69, Curb, -5.6f), CartYaw = 0 });
        L.Spots.Add(new SpotZone { Id = "sanayi", Center = new Vector3(5, Curb, -6.3f), Half = new Vector2(6.5f, 2.6f), CartPos = new Vector3(5, Curb, -5.6f), CartYaw = 0 });
        // Guney kaldirimi: musteri tarafi +Z (bina) -> yaw = PI
        L.Spots.Add(new SpotZone { Id = "hastane", Center = new Vector3(-30, Curb, 6.6f), Half = new Vector2(6.5f, 2.6f), CartPos = new Vector3(-30, Curb, 5.6f), CartYaw = MathF.PI });
        L.Spots.Add(new SpotZone { Id = "stadyum", Center = new Vector3(36.5f, Curb, 6.6f), Half = new Vector2(7f, 2.6f), CartPos = new Vector3(36.5f, Curb, 5.6f), CartYaw = MathF.PI });
        L.Spots.Add(new SpotZone { Id = "iskele", Center = new Vector3(92, Curb, 10.5f), Half = new Vector2(7f, 5f), CartPos = new Vector3(92, Curb, 9.0f), CartYaw = MathF.PI });
        L.Spots.Add(new SpotZone { Id = "dukkan", Center = new Vector3(36, Curb, -17.8f), Half = new Vector2(5.5f, 2.6f), CartPos = new Vector3(36, Curb, -18.2f), CartYaw = MathF.PI });
    }

    // ═══════════════════════════════════════════════════════════════════
    // Yaya yol grafigi
    // ═══════════════════════════════════════════════════════════════════
    private static void Navigation(BuildContext b)
    {
        var L = b.Layout;
        var nav = L.Nav;
        // Onceden eklenen kapi/ara sokak dugumleri (indeks < baslangic)
        var preExisting = nav.Nodes.Count;
        var north = nav.Line(new Vector3(MinX + 2, Curb, -6.6f), new Vector3(MaxX - 2, Curb, -6.6f), 7f);
        var south = nav.Line(new Vector3(MinX + 2, Curb, 6.6f), new Vector3(53, Curb, 6.6f), 7f);
        var prom = nav.Line(new Vector3(57, Curb, 11.5f), new Vector3(MaxX - 2, Curb, 11.5f), 7f);
        nav.Connect(south[^1], prom[0]);
        nav.Connect(south[^1], nav.Nearest(new Vector3(57, Curb, 6.6f), i => prom.Contains(i)));
        // Yaya gecitleri
        foreach (var cx in L.Crosswalks)
        {
            var n = Closest(nav, north, new Vector3(cx, Curb, -6.6f));
            var sList = cx > 55 ? prom : south;
            var s = Closest(nav, sList, new Vector3(cx, Curb, 6.6f));
            var a = nav.Add(new Vector3(cx, Curb, -4.6f));
            var c = nav.Add(new Vector3(cx, 0.02f, 0));
            var d = nav.Add(new Vector3(cx, Curb, 4.6f));
            nav.Connect(n, a);
            nav.Connect(a, c);
            nav.Connect(c, d);
            nav.Connect(d, s);
        }

        // Uclar
        nav.Tags[north[0]] = NavTag.Edge;
        nav.Tags[north[^1]] = NavTag.Edge;
        nav.Tags[south[0]] = NavTag.Edge;
        nav.Tags[prom[^1]] = NavTag.Edge;
        nav.Areas[north[0]] = nav.Areas[north[^1]] = nav.Areas[south[0]] = nav.Areas[prom[^1]] = "edge";

        // Iskele kapisi
        var pierEntry = nav.Add(new Vector3(102, Curb, 15.2f));
        var pierDoor = nav.Add(L.PierDoor, NavTag.Door, "iskele");
        nav.Connect(pierEntry, pierDoor);
        nav.Connect(pierEntry, Closest(nav, prom, new Vector3(102, Curb, 11.5f)));

        // Dukkan ici
        var shopDoor = nav.Add(new Vector3(36, Curb, -9.2f));
        var shopIn = nav.Add(new Vector3(36, Curb, -13f));
        nav.Connect(shopDoor, Closest(nav, north, new Vector3(36, Curb, -6.6f)));
        nav.Connect(shopDoor, shopIn);

        // Onceden eklenen kapi ve ara sokak dugumlerini en yakin kaldirim hattina bagla
        for (var i = 0; i < preExisting; i++)
        {
            var p = nav.Nodes[i];
            if (nav.Links[i].Count > 0 && nav.Tags[i] == NavTag.Edge)
            {
                continue;
            }

            var lines = new List<int>();
            lines.AddRange(north);
            lines.AddRange(south);
            lines.AddRange(prom);
            if (nav.Tags[i] == NavTag.None && nav.Links[i].Count > 0)
            {
                // Ara sokagin yakin ucu
                nav.Connect(i, Closest(nav, lines, p));
                continue;
            }

            if (nav.Tags[i] == NavTag.Door)
            {
                nav.Connect(i, Closest(nav, lines, p));
            }
        }

        // Ara sokaklarin yakin uclari (etiketsiz, baglantili) - yukarida baglandi.
        // Satis noktalarina musteri gonderen kaynaklar
        foreach (var spot in L.Spots)
        {
            for (var i = 0; i < nav.Nodes.Count; i++)
            {
                if (nav.Tags[i] == NavTag.None)
                {
                    continue;
                }

                var dist = Vector3.Distance(nav.Nodes[i], spot.Center);
                var relevant = nav.Areas[i] == spot.Id || dist < 70f || (spot.Id == "dukkan" && dist < 90f);
                if (relevant)
                {
                    spot.SourceNodes.Add(i);
                }
            }
        }
    }

    private static int Closest(NavGraph nav, List<int> among, Vector3 p)
    {
        var best = among[0];
        var bd = float.MaxValue;
        foreach (var i in among)
        {
            var d = Vector3.DistanceSquared(nav.Nodes[i], p);
            if (d < bd)
            {
                bd = d;
                best = i;
            }
        }

        return best;
    }
}
