using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Physics;

namespace PilavciSimulator.World;

/// <summary>Sokak esyalari ve bina cephesi uretecleri.</summary>
public static class Props
{
    public static readonly string[] FacadeColors =
    [
        "E9DCC1", "D9A55B", "C8735A", "A9C4D6", "E3B5A4", "B7D3B5", "EED9A0", "C9C3B8", "EFEBE4", "A8B59A", "D7C4E0", "F0C9A0",
    ];

    // ── Sokak lambasi ────────────────────────────────────────────────
    public static void StreetLamp(BuildContext b, Vector3 basePos, Vector3 armDir)
    {
        var pole = Gfx.Hex(0x2E3338);
        b.ColliderYaw(basePos, new Vector3(0.18f, 4.5f, 0.18f), 0);
        var top = basePos + new Vector3(0, 4.7f, 0);
        var head = top + armDir * 1.15f + new Vector3(0, 0.1f, 0);
        b.Draw(M.Metal, basePos, m =>
        {
            // Dokum kaide, bilezikli direk, kivrik kol ve suslu destek
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(basePos),
                [new(0, 0), new(0.17f, 0), new(0.17f, 0.06f), new(0.13f, 0.1f), new(0.13f, 0.35f), new(0.1f, 0.42f), new(0.085f, 0.6f), new(0.07f, 0.7f), new(0, 0.7f)], 12, pole);
            Shapes.Frustum(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.7f, 0)), 0.07f, 0.05f, 4.05f, 10, pole, false, false);
            foreach (var y in new[] { 1.6f, 3.2f, 4.62f })
            {
                Shapes.Torus(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, y, 0)), 0.065f, 0.018f, 10, 4, pole);
            }

            Shapes.Sphere(m, Matrix4x4.CreateTranslation(top + new Vector3(0, 0.05f, 0)), 0.08f, 5, 8, pole);
            Shapes.Tube(m, Shapes.Curve(top, top + new Vector3(0, 0.35f, 0) + armDir * 0.3f, top + armDir * 1.15f + new Vector3(0, 0.25f, 0), 10), 0.045f, 6, pole);
            Shapes.Tube(m, Shapes.Curve(top - new Vector3(0, 0.55f, 0), top - new Vector3(0, 0.1f, 0) + armDir * 0.5f, top + armDir * 0.75f + new Vector3(0, 0.27f, 0), 8), 0.02f, 4, pole);
            // Fener: ust kapak, ince kafes
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(head + new Vector3(0, 0.08f, 0)),
                [new(0, 0), new(0.24f, 0), new(0.26f, 0.03f), new(0.12f, 0.17f), new(0.05f, 0.2f), new(0.03f, 0.27f), new(0, 0.28f)], 8, pole);
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(head - new Vector3(0, 0.3f, 0)), [new(0, 0), new(0.06f, 0), new(0.16f, 0.06f), new(0.16f, 0.08f), new(0, 0.08f)], 8, pole);
            for (var i = 0; i < 4; i++)
            {
                var a2 = i * MathF.PI / 2 + MathF.PI / 4;
                var o = new Vector3(MathF.Cos(a2), 0, MathF.Sin(a2)) * 0.18f;
                Shapes.Tube(m, [head + o - new Vector3(0, 0.23f, 0), head + o * 1.25f + new Vector3(0, 0.08f, 0)], 0.012f, 4, pole);
            }
        });
        var bulbPos = basePos + new Vector3(0, 4.62f, 0) + armDir * 1.15f;
        b.Draw(M.Emissive, bulbPos, m =>
        {
            Shapes.Sphere(m, Matrix4x4.CreateTranslation(bulbPos), 0.13f, 6, 10, new Color(255, 238, 200, 120));
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(head - new Vector3(0, 0.22f, 0)), [new(0.15f, 0), new(0.22f, 0.3f)], 8, new Color(255, 244, 220, 70));
        });
        b.Layout.Lamps.Add(new LampInfo(bulbPos - new Vector3(0, 0.3f, 0), 13f, new Vector3(3.2f, 2.5f, 1.6f), LampKind.Street));
    }

    // ── Agac ─────────────────────────────────────────────────────────
    public static void Tree(BuildContext b, Vector3 basePos, float scale = 1f)
    {
        // Hazir agac modeli varsa o; carpisma ve RNG her durumda prosedurelden.
        var yaw = Core.Rng.Hash((uint)MathF.Round(basePos.X * 10f), (uint)MathF.Round(basePos.Z * 10f) + 7u) % 360 * MathF.PI / 180f;
        b.Prefab("tree", basePos, yaw, scale, bb => ProceduralTree(bb, basePos, scale));
    }

    private static void ProceduralTree(BuildContext b, Vector3 basePos, float scale)
    {
        var rng = b.Rng;
        b.ColliderYaw(basePos, new Vector3(0.35f, 2.5f, 0.35f), 0);
        var trunk = Gfx.Hex(0x6B4F3A);
        var h = (2.4f + rng.Range(0f, 1.2f)) * scale;
        b.Draw(M.Wood, basePos, m =>
        {
            Shapes.Frustum(m, Matrix4x4.CreateTranslation(basePos), 0.2f * scale, 0.13f * scale, h, 7, trunk, false, false, 0.5f);
            // Toprak kutusu (agac dibi)
            Shapes.BoxOnGround(m, Matrix4x4.CreateTranslation(basePos - new Vector3(0, 0.01f, 0)), new Vector3(1.1f, 0.06f, 1.1f), Gfx.Hex(0x5A4636));
        });
        var leaf = Gfx.Lerp(Gfx.Hex(0x5E8B3E), Gfx.Hex(0x86A84A), rng.NextFloat());
        var count = 3 + rng.Range(0, 2);
        for (var i = 0; i < count; i++)
        {
            var off = new Vector3(rng.Range(-0.8f, 0.8f), h + rng.Range(-0.3f, 1.0f), rng.Range(-0.8f, 0.8f)) * scale;
            var r = rng.Range(1.0f, 1.5f) * scale;
            var c = Gfx.Lerp(leaf, Gfx.Hex(0x4A7A33), rng.NextFloat() * 0.5f);
            var p = basePos + off;
            b.Draw(M.Leaves, p, m => Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 0.82f, 1f) * Matrix4x4.CreateTranslation(p), r, 5, 8, c, 0.6f));
        }

        b.Rng = rng;
    }

    // ── Bank ─────────────────────────────────────────────────────────
    public static void Bench(BuildContext b, Vector3 basePos, float yaw)
    {
        b.ColliderYaw(basePos, new Vector3(1.6f, 0.45f, 0.5f), yaw, ColliderFlags.Surface);
        var xf = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(basePos);
        var metal = Gfx.Hex(0x2D3436);
        b.Draw(M.Wood, basePos, m =>
        {
            // Yuvarlatilmis oturma ve sirt latalari
            for (var i = 0; i < 4; i++)
            {
                Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(0, 0.45f - i * 0.004f, -0.19f + i * 0.125f) * xf, new Vector3(1.62f, 0.045f, 0.1f), 0.018f, 2, Color.White, 1f);
            }

            for (var i = 0; i < 3; i++)
            {
                Shapes.RoundedBox(m, Matrix4x4.CreateRotationX(-0.22f) * Matrix4x4.CreateTranslation(0, 0.62f + i * 0.13f, 0.26f + i * 0.03f) * xf, new Vector3(1.62f, 0.09f, 0.035f), 0.014f, 2, Color.White, 1f);
            }
        });
        b.Draw(M.Metal, basePos, m =>
        {
            // Dokum yan ayaklar: S kivrimli sirt, kol dayama, on ayak
            foreach (var x in new[] { -0.68f, 0.68f })
            {
                Vector3 P(float y, float z) => Vector3.Transform(new Vector3(x, y, z), xf);
                Shapes.Tube(m, Shapes.Curve(P(0.02f, 0.22f), P(0.4f, 0.12f), P(0.44f, 0.24f), P(0.95f, 0.36f), 10), 0.025f, 6, metal);
                Shapes.Tube(m, Shapes.Curve(P(0.02f, -0.2f), P(0.25f, -0.2f), P(0.43f, -0.22f), 6), 0.025f, 6, metal);
                Shapes.Tube(m, Shapes.Curve(P(0.43f, -0.24f), P(0.65f, -0.28f), P(0.62f, 0.05f), P(0.62f, 0.25f), 10), 0.022f, 6, metal);
                Shapes.Tube(m, [P(0.42f, -0.22f), P(0.42f, 0.2f)], 0.02f, 5, metal);
                Shapes.Sphere(m, Matrix4x4.CreateTranslation(P(0.02f, 0.22f)), 0.035f, 3, 6, metal);
                Shapes.Sphere(m, Matrix4x4.CreateTranslation(P(0.02f, -0.2f)), 0.035f, 3, 6, metal);
            }
        });
    }

    // ── Cop kutusu ───────────────────────────────────────────────────
    public static void Bin(BuildContext b, Vector3 basePos)
    {
        b.ColliderYaw(basePos, new Vector3(0.45f, 0.9f, 0.45f), 0);
        b.Draw(M.Metal, basePos, m =>
        {
            var green = Gfx.Hex(0x2E7D4F);
            var dark = Gfx.Hex(0x1E5D3A);
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(basePos), [new(0, 0), new(0.18f, 0), new(0.2f, 0.04f), new(0.22f, 0.78f), new(0.235f, 0.8f)], 14, green);
            for (var i = 0; i < 3; i++)
            {
                Shapes.Torus(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.2f + i * 0.25f, 0)), 0.205f + i * 0.007f, 0.012f, 14, 4, dark);
            }

            // Kubbeli kapak, atma agzi
            Shapes.Lathe(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.8f, 0)), [new(0.245f, 0), new(0.245f, 0.04f), new(0.2f, 0.1f), new(0.08f, 0.14f), new(0, 0.145f)], 14, dark, smoothProfile: true);
            Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.86f, -0.17f)), new Vector3(0.2f, 0.07f, 0.06f), 0.02f, 1, Gfx.Hex(0x111111));
            Shapes.Box(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.5f, -0.212f)), new Vector3(0.12f, 0.12f, 0.004f), Color.White);
        });
    }

    /// <summary>Parktaki bronz marti heykeli: tas kaide, plaket, kanatlari acik marti.</summary>
    public static void GullStatue(Func<int, MeshData> mesh, Vector3 at)
    {
        var stone = Gfx.Hex(0xCFC8BB);
        var bronze = Gfx.Hex(0x8C6E3C);
        var c = mesh(M.Concrete);
        Shapes.RoundedBoxOnGround(c, Matrix4x4.CreateTranslation(at), new Vector3(1.25f, 0.18f, 1.25f), 0.03f, 1, Gfx.Lerp(stone, Color.Black, 0.1f));
        Shapes.RoundedBoxOnGround(c, Matrix4x4.CreateTranslation(at + new Vector3(0, 0.18f, 0)), new Vector3(0.9f, 1.0f, 0.9f), 0.04f, 1, stone);
        Shapes.RoundedBoxOnGround(c, Matrix4x4.CreateTranslation(at + new Vector3(0, 1.18f, 0)), new Vector3(1.05f, 0.12f, 1.05f), 0.03f, 1, Gfx.Lerp(stone, Color.White, 0.2f));
        Shapes.Box(mesh(M.Metal), Matrix4x4.CreateTranslation(at + new Vector3(0, 0.7f, -0.455f)), new Vector3(0.42f, 0.26f, 0.015f), Gfx.Hex(0x6E5A2E));
        // Kaya ve marti (govde, bas, gaga, kanatlar yukari acik, kuyruk)
        var m = mesh(M.Metal);
        var rock = at + new Vector3(0, 1.3f, 0);
        Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 0.55f, 0.8f) * Matrix4x4.CreateTranslation(rock + new Vector3(0, 0.08f, 0)), 0.32f, 5, 9, Gfx.Lerp(bronze, Color.Black, 0.25f));
        var body = rock + new Vector3(0, 0.55f, 0);
        Shapes.Sphere(m, Matrix4x4.CreateScale(0.42f, 0.42f, 1f) * Matrix4x4.CreateRotationX(-0.35f) * Matrix4x4.CreateTranslation(body), 0.36f, 7, 12, bronze);
        Shapes.Sphere(m, Matrix4x4.CreateTranslation(body + new Vector3(0, 0.2f, -0.3f)), 0.11f, 6, 10, bronze);
        Shapes.CapsuleBetween(m, body + new Vector3(0, 0.19f, -0.38f), body + new Vector3(0, 0.16f, -0.5f), 0.025f, 3, 6, Gfx.Lerp(bronze, Color.White, 0.15f));
        Shapes.CapsuleBetween(m, body + new Vector3(-0.06f, -0.12f, 0.05f), rock + new Vector3(-0.07f, 0.2f, 0.05f), 0.02f, 2, 5, bronze);
        Shapes.CapsuleBetween(m, body + new Vector3(0.06f, -0.12f, 0.05f), rock + new Vector3(0.07f, 0.2f, 0.05f), 0.02f, 2, 5, bronze);
        var wing = new List<Vector2> { new(0, 0), new(0.35f, 0.05f), new(0.75f, 0.0f), new(1.05f, -0.12f), new(0.7f, -0.12f), new(0.35f, -0.2f), new(0, -0.22f) };
        foreach (var s in new[] { -1f, 1f })
        {
            // Ayna donusumu (negatif determinant) sarimi bozar; sol kanat icin cokgenin kendisi aynalanir
            var xf = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateRotationZ(s * 0.55f) * Matrix4x4.CreateTranslation(body + new Vector3(s * 0.08f, 0.08f, 0.02f));
            Shapes.Extrude(m, xf, wing.Select(p => new Vector2(p.X * s, p.Y)).ToList(), 0.03f, bronze);
        }

        Shapes.Extrude(m, Matrix4x4.CreateRotationX(MathF.PI / 2 - 0.3f) * Matrix4x4.CreateTranslation(body + new Vector3(0, -0.05f, 0.3f)),
            [new(-0.1f, 0), new(0.1f, 0), new(0.14f, 0.25f), new(0, 0.2f), new(-0.14f, 0.25f)], 0.025f, bronze);
    }

    // ── Park etmis araba ─────────────────────────────────────────────
    public static void ParkedCar(BuildContext b, Vector3 basePos, float yaw, Color body, string group = "car")
    {
        b.ColliderYaw(basePos, new Vector3(4.2f, 1.45f, 1.8f), yaw);
        var hash = Core.Rng.Hash((uint)MathF.Round(basePos.X * 10f), (uint)MathF.Round(basePos.Z * 10f) + 31u);
        var type = VehicleMeshes.BodyFor(hash, parked: true);
        b.Prefab(group, basePos, yaw, 1f, bb =>
            bb.DrawMulti(basePos, mesh => VehicleMeshes.Car(mesh, Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(basePos), type, body)));
    }

    // ── Korkuluk ─────────────────────────────────────────────────────
    public static void Railing(BuildContext b, Vector3 from, Vector3 to, float height = 1.05f, bool collider = true)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.01f)
        {
            return;
        }

        var metal = Gfx.Hex(0x34495E);
        b.Draw(M.Metal, (from + to) / 2, m =>
        {
            Shapes.Beam(m, from + new Vector3(0, height, 0), to + new Vector3(0, height, 0), 0.06f, metal);
            Shapes.Beam(m, from + new Vector3(0, height * 0.5f, 0), to + new Vector3(0, height * 0.5f, 0), 0.04f, metal);
            var n = Math.Max(1, (int)(len / 1.6f));
            for (var i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)n);
                Shapes.Beam(m, p, p + new Vector3(0, height, 0), 0.06f, metal);
            }
        });
        if (collider)
        {
            var min = Vector3.Min(from, to) - new Vector3(0.08f, 0, 0.08f);
            var max = Vector3.Max(from, to) + new Vector3(0.08f, height + 0.4f, 0.08f);
            b.Collider(min, max, ColliderFlags.Solid | ColliderFlags.BlocksNav);
        }
    }

    // ── Bina cephesi ─────────────────────────────────────────────────

    /// <summary>Bina ayarlari.</summary>
    public sealed class BuildingSpec
    {
        public float MinX;
        public float MaxX;
        /// <summary>Cephe duzlemi (z).</summary>
        public float FacadeZ;
        /// <summary>+1: cephe +Z'ye (guneye) bakar, -1: -Z'ye.</summary>
        public float Facing = 1;
        public float Depth = 13;
        public int Floors = 4;
        public string Color = "E9DCC1";
        public string? Shop;
        public string ShopColor = "C0392B";
        public string AwningA = "C0392B";
        public string AwningB = "F5F5F5";
        public bool Pitched = true;
        public bool Cumba = true;
        public float GroundFloor = 4.2f;
        public bool Collide = true;
        /// <summary>Zemin kat vitrin yerine kapali (okul, hastane gibi ozel cepheler kendini cizer).</summary>
        public bool PlainGround;
    }

    /// <summary>
    /// Istanbul apartmani: zemin katta dukkan (vitrin, tente, tabela),
    /// ust katlarda pencereler, bazen cumba ve balkon, kiremit ya da duz cati.
    /// </summary>
    public static void Building(BuildContext b, BuildingSpec s)
    {
        var f = s.Facing;
        var zFront = s.FacadeZ;
        var zBack = s.FacadeZ - f * s.Depth;
        var minZ = MathF.Min(zFront, zBack);
        var maxZ = MathF.Max(zFront, zBack);
        var floorH = 3.1f;
        var height = s.GroundFloor + (s.Floors - 1) * floorH;
        var baseY = 0.15f;
        var wallColor = BuildContext.Hex(s.Color);
        var rng = b.Rng;

        // Govde
        b.Box(M.Plaster, new Vector3(s.MinX, 0, minZ), new Vector3(s.MaxX, baseY + height, maxZ), wallColor, 0.5f,
            s.Collide ? ColliderFlags.Default : null);
        // Zemin kat bandi (koyu)
        var band = Gfx.Lerp(wallColor, Gfx.Hex(0x6E6258), 0.45f);
        b.Box(M.Plaster, new Vector3(s.MinX, baseY + s.GroundFloor - 0.25f, zFront - (f > 0 ? 0 : 0.12f)),
            new Vector3(s.MaxX, baseY + s.GroundFloor, zFront + (f > 0 ? 0.12f : 0)), band, 0.5f, null);
        // Kat silmeleri
        for (var i = 1; i < s.Floors; i++)
        {
            var y = baseY + s.GroundFloor + i * floorH - 0.1f;
            b.Box(M.Plaster, new Vector3(s.MinX, y, zFront - (f > 0 ? 0 : 0.06f)), new Vector3(s.MaxX, y + 0.1f, zFront + (f > 0 ? 0.06f : 0)),
                Gfx.Lerp(wallColor, Color.White, 0.35f), 0.5f, null);
        }

        // Ust kat pencereleri
        var width = s.MaxX - s.MinX;
        var cols = Math.Max(1, (int)(width / 2.6f));
        var spacing = width / cols;
        var cumbaCol = s.Cumba && width > 7 ? cols / 2 : -1;
        for (var fl = 1; fl < s.Floors; fl++)
        {
            var y0 = baseY + s.GroundFloor + (fl - 1) * floorH + 0.9f;
            for (var c = 0; c < cols; c++)
            {
                var cx = s.MinX + spacing * (c + 0.5f);
                if (c == cumbaCol)
                {
                    continue;
                }

                Window(b, new Vector3(cx, y0, zFront), f, 1.15f, 1.55f, rng.Chance(0.55f), wallColor);
                if (fl == 1 && rng.Chance(0.25f) && c != cumbaCol)
                {
                    Balcony(b, new Vector3(cx, y0 - 0.85f, zFront), f, 1.9f);
                }
            }

            if (cumbaCol >= 0)
            {
                var cx = s.MinX + spacing * (cumbaCol + 0.5f);
                var cw = MathF.Min(spacing * 1.4f, 3.6f);
                var y = baseY + s.GroundFloor + (fl - 1) * floorH;
                // Cumba: cikma kutu
                var cumbaColor = Gfx.Lerp(wallColor, Gfx.Hex(0x8E6E53), 0.15f);
                b.Box(M.Plaster, new Vector3(cx - cw / 2, y + 0.1f, f > 0 ? zFront : zFront - 0.75f),
                    new Vector3(cx + cw / 2, y + floorH - 0.05f, f > 0 ? zFront + 0.75f : zFront), cumbaColor, 0.5f, null);
                Window(b, new Vector3(cx, y + 0.95f, zFront + f * 0.75f), f, cw * 0.7f, 1.5f, rng.Chance(0.6f), cumbaColor);
            }
        }

        // Cati
        var roofY = baseY + height;
        if (s.Pitched)
        {
            var roofColor = Gfx.Lerp(Color.White, Gfx.Hex(0xB7A090), rng.NextFloat() * 0.4f);
            var center = new Vector3((s.MinX + s.MaxX) / 2, roofY, (minZ + maxZ) / 2);
            b.Draw(M.Roof, center, m =>
            {
                var xf = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(center);
                Shapes.Wedge(m, xf, new Vector3(s.Depth + 0.6f, 2.6f, width + 0.4f), roofColor, 1f);
            });
            if (rng.Chance(0.6f))
            {
                var ch = new Vector3(s.MinX + width * rng.Range(0.2f, 0.8f), roofY, (minZ + maxZ) / 2 + rng.Range(-2f, 2f));
                b.Box(M.Brick, ch, ch + new Vector3(0.6f, 2.4f, 0.6f), Color.White, 1f, null);
            }
        }
        else
        {
            b.Box(M.Plaster, new Vector3(s.MinX, roofY, minZ), new Vector3(s.MaxX, roofY + 0.6f, minZ + 0.25f), wallColor, 0.5f, null);
            b.Box(M.Plaster, new Vector3(s.MinX, roofY, maxZ - 0.25f), new Vector3(s.MaxX, roofY + 0.6f, maxZ), wallColor, 0.5f, null);
            if (rng.Chance(0.5f))
            {
                // Su deposu ve anten
                var t = new Vector3(s.MinX + width * 0.3f, roofY, (minZ + maxZ) / 2);
                b.Draw(M.Metal, t, m => Shapes.Cylinder(m, Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(t + new Vector3(0.8f, 0.6f, 0)), 0.5f, 1.6f, 10, Gfx.Hex(0xBFC5CA)));
            }
        }

        // Zemin kat
        if (!s.PlainGround)
        {
            ShopFront(b, s, f, zFront, baseY);
            ApartmentDoor(b, s, f, zFront, baseY);
        }

        BuildingDetails(b, s, f, zFront, baseY, roofY, cols, spacing, cumbaCol, floorH);
        b.Rng = rng;
    }

    /// <summary>Konumdan kararli zar (dunya RNG'sini tuketmez; carpisma ve yerlesim degismez).</summary>
    private static float Dice(float x, float y, uint salt) =>
        (Core.Rng.Hash((uint)MathF.Round(x * 10f) + salt * 7919u, (uint)MathF.Round(y * 10f) + salt) & 0xFFFF) / 65535f;

    /// <summary>Apartman giris kapisi: dukkan vitrininin yaninda, basamak, sacak ve kapi numarasi.</summary>
    private static void ApartmentDoor(BuildContext b, BuildingSpec s, float f, float zFront, float baseY)
    {
        var width = s.MaxX - s.MinX;
        var glassW = MathF.Min(width - 2.2f, 7.5f);
        var free = (width - glassW) / 2;
        if (free < 1.35f)
        {
            return;
        }

        var left = Dice(s.MinX, zFront, 3) < 0.5f;
        var cx = left ? s.MinX + free / 2 : s.MaxX - free / 2;
        var dw = MathF.Min(1.0f, free - 0.35f);
        var z0 = zFront;
        var frame = Gfx.Hex(0xD8D3CA);
        var door = Dice(cx, zFront, 4) < 0.5f ? Gfx.Hex(0x5D4037) : Gfx.Hex(0x34495E);
        b.Box(M.Concrete, new Vector3(cx - dw / 2 - 0.15f, baseY, MathF.Min(z0, z0 + f * 0.08f)), new Vector3(cx + dw / 2 + 0.15f, baseY + 2.55f, MathF.Max(z0, z0 + f * 0.08f)), frame, 0.5f, null);
        b.Box(M.Wood, new Vector3(cx - dw / 2, baseY + 0.12f, MathF.Min(z0, z0 + f * 0.1f)), new Vector3(cx + dw / 2, baseY + 2.35f, MathF.Max(z0, z0 + f * 0.1f)), door, 1f, null);
        b.DrawMulti(new Vector3(cx, baseY, zFront), mesh =>
        {
            var fz = z0 + f * 0.105f;
            // Ust cam, kulp, numara plakasi
            Shapes.Box(mesh(M.WindowGlass), Matrix4x4.CreateTranslation(cx, baseY + 1.75f, fz), new Vector3(dw * 0.7f, 0.7f, 0.01f), new Color(55, 68, 82, 30));
            Shapes.Box(mesh(M.Metal), Matrix4x4.CreateTranslation(cx + dw * 0.32f * (left ? 1 : -1), baseY + 1.1f, fz + f * 0.03f), new Vector3(0.03f, 0.22f, 0.03f), Gfx.Hex(0xC9A227));
            Shapes.Box(mesh(M.Plastic), Matrix4x4.CreateTranslation(cx + (dw / 2 + 0.32f) * (left ? 1 : -1), baseY + 2.2f, z0 + f * 0.02f), new Vector3(0.24f, 0.17f, 0.03f), Gfx.Hex(0x1F4E9A));
            Shapes.Box(mesh(M.Plastic), Matrix4x4.CreateTranslation(cx + (dw / 2 + 0.32f) * (left ? 1 : -1), baseY + 2.2f, z0 + f * 0.037f), new Vector3(0.1f, 0.08f, 0.004f), Color.White);
            // Basamak ve sacak
            Shapes.BoxOnGround(mesh(M.Concrete), Matrix4x4.CreateTranslation(cx, 0, z0 + f * 0.3f), new Vector3(dw + 0.5f, baseY + 0.12f, 0.6f), Gfx.Hex(0xBDB6AB));
            Shapes.RoundedBox(mesh(M.Concrete), Matrix4x4.CreateTranslation(cx, baseY + 2.72f, z0 + f * 0.38f), new Vector3(dw + 0.7f, 0.1f, 0.76f), 0.03f, 1, frame);
            // Duvar lambasi
            Shapes.Lathe(mesh(M.Emissive), Matrix4x4.CreateTranslation(cx + (dw / 2 + 0.32f) * (left ? -1 : 1), baseY + 2.35f, z0 + f * 0.1f),
                [new(0, 0), new(0.07f, 0.02f), new(0.08f, 0.12f), new(0.05f, 0.18f), new(0, 0.18f)], 10, new Color(255, 236, 190, 255), smoothProfile: true);
        });
    }

    /// <summary>Cephe ayrintilari: korniş, yagmur borusu, klima, uydu canagi.</summary>
    private static void BuildingDetails(BuildContext b, BuildingSpec s, float f, float zFront, float baseY, float roofY, int cols, float spacing, int cumbaCol, float floorH)
    {
        var wall = BuildContext.Hex(s.Color);
        var light = Gfx.Lerp(wall, Color.White, 0.45f);
        // Korniş: cati altinda cikma silme
        b.Box(M.Plaster, new Vector3(s.MinX - 0.08f, roofY - 0.42f, MathF.Min(zFront, zFront + f * 0.32f)), new Vector3(s.MaxX + 0.08f, roofY - 0.12f, MathF.Max(zFront, zFront + f * 0.32f)), light, 0.5f, null);
        b.Box(M.Plaster, new Vector3(s.MinX - 0.08f, roofY - 0.5f, MathF.Min(zFront, zFront + f * 0.18f)), new Vector3(s.MaxX + 0.08f, roofY - 0.42f, MathF.Max(zFront, zFront + f * 0.18f)), Gfx.Lerp(light, wall, 0.5f), 0.5f, null);
        b.DrawMulti(new Vector3((s.MinX + s.MaxX) / 2, roofY, zFront), mesh =>
        {
            // Yagmur borusu (bir kosede)
            var px = Dice(s.MaxX, zFront, 5) < 0.5f ? s.MinX + 0.18f : s.MaxX - 0.18f;
            var pz = zFront + f * 0.14f;
            Shapes.Tube(mesh(M.Metal), [new Vector3(px, roofY - 0.15f, pz + f * 0.2f), new Vector3(px, roofY - 0.45f, pz), new Vector3(px, baseY + 0.25f, pz), new Vector3(px, baseY + 0.05f, pz + f * 0.18f)], 0.055f, 6, Gfx.Hex(0x8E8E8E));
            // Klimalar: bazi pencerelerin yaninda
            for (var fl = 1; fl < s.Floors; fl++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cx = s.MinX + spacing * (c + 0.5f);
                    if (c == cumbaCol || Dice(cx, fl, 6) > 0.14f)
                    {
                        continue;
                    }

                    var ax = cx + (Dice(cx, fl, 7) < 0.5f ? -1f : 1f) * 1.0f;
                    if (ax < s.MinX + 0.5f || ax > s.MaxX - 0.5f)
                    {
                        continue;
                    }

                    var ay = baseY + s.GroundFloor + (fl - 1) * floorH + 0.75f;
                    var c0 = new Vector3(ax, ay, zFront + f * 0.17f);
                    Shapes.RoundedBox(mesh(M.Metal), Matrix4x4.CreateTranslation(c0), new Vector3(0.72f, 0.5f, 0.3f), 0.03f, 1, Gfx.Hex(0xE5E7E9));
                    Shapes.Cylinder(mesh(M.Metal), Matrix4x4.CreateRotationX(f * MathF.PI / 2) * Matrix4x4.CreateTranslation(c0 + new Vector3(0.08f, 0, f * 0.15f)), 0.19f, 0.006f, 14, Gfx.Hex(0x4D5656));
                    Shapes.Box(mesh(M.Metal), Matrix4x4.CreateTranslation(c0 + new Vector3(0, -0.3f, -f * 0.05f)), new Vector3(0.6f, 0.04f, 0.3f), Gfx.Hex(0x7F8C8D));
                }
            }

            // Uydu canagi (catida ya da korniste)
            if (Dice(s.MinX, s.MaxX, 8) < 0.45f)
            {
                var dx = s.MinX + (s.MaxX - s.MinX) * (0.2f + 0.6f * Dice(s.MinX, zFront, 9));
                var d0 = new Vector3(dx, roofY + 0.05f, zFront - f * 0.6f);
                Shapes.Tube(mesh(M.Metal), [d0, d0 + new Vector3(0, 0.75f, 0)], 0.03f, 5, Gfx.Hex(0x95A5A6));
                var dish = Matrix4x4.CreateRotationX(f * 0.75f) * Matrix4x4.CreateTranslation(d0 + new Vector3(0, 0.8f, f * 0.05f));
                Shapes.Lathe(mesh(M.Metal), dish, [new(0, 0.02f), new(0.2f, 0.04f), new(0.36f, 0.11f), new(0.38f, 0.13f), new(0.34f, 0.12f), new(0.18f, 0.05f), new(0, 0.03f)], 16, Gfx.Hex(0xECF0F1));
                Shapes.Tube(mesh(M.Metal), [Vector3.Transform(new Vector3(0, 0.1f, 0.33f), dish), Vector3.Transform(new Vector3(0, 0.42f, 0), dish)], 0.012f, 4, Gfx.Hex(0x95A5A6));
                Shapes.Sphere(mesh(M.Plastic), Matrix4x4.CreateTranslation(Vector3.Transform(new Vector3(0, 0.44f, 0), dish)), 0.04f, 4, 6, Gfx.Hex(0x2C3E50));
            }
        });
    }

    public static void Window(BuildContext b, Vector3 bottomCenter, float facing, float w, float h, bool lit, Color wall)
    {
        var z = bottomCenter.Z;
        var x = bottomCenter.X;
        var y = bottomCenter.Y;
        var frame = Gfx.Lerp(wall, Color.White, 0.6f);
        var out1 = facing * 0.08f;
        // Cerceve
        b.Box(M.Plaster, new Vector3(x - w / 2 - 0.12f, y - 0.12f, MathF.Min(z, z + out1)), new Vector3(x + w / 2 + 0.12f, y + h + 0.12f, MathF.Max(z, z + out1)), frame, 0.5f, null);
        // Cam (gece yanma isareti kose alfasinda)
        var glassColor = lit ? new Color(70, 85, 100, 128) : new Color(55, 68, 82, 30);
        var gz = z + facing * 0.1f;
        b.Draw(M.WindowGlass, new Vector3(x, y, gz), m =>
        {
            var n = new Vector3(0, 0, facing);
            var right = facing > 0 ? Vector3.UnitX : -Vector3.UnitX;
            var bl = new Vector3(x, y, gz) - right * (w / 2);
            Shapes.Quad(m, bl, bl + right * w, bl + right * w + new Vector3(0, h, 0), bl + new Vector3(0, h, 0), glassColor, 1f);
            _ = n;
        });
        // Orta kayit
        b.Box(M.Plaster, new Vector3(x - 0.04f, y, MathF.Min(z, gz + facing * 0.02f)), new Vector3(x + 0.04f, y + h, MathF.Max(z, gz + facing * 0.02f)), frame, 0.5f, null);
        // Pervaz ve ust lento (ortada kilit tasi)
        b.Box(M.Plaster, new Vector3(x - w / 2 - 0.2f, y - 0.2f, MathF.Min(z, z + facing * 0.22f)), new Vector3(x + w / 2 + 0.2f, y - 0.1f, MathF.Max(z, z + facing * 0.22f)), frame, 0.5f, null);
        b.Box(M.Plaster, new Vector3(x - w / 2 - 0.22f, y + h + 0.12f, MathF.Min(z, z + facing * 0.14f)), new Vector3(x + w / 2 + 0.22f, y + h + 0.3f, MathF.Max(z, z + facing * 0.14f)), frame, 0.5f, null);
        b.Box(M.Plaster, new Vector3(x - 0.12f, y + h + 0.08f, MathF.Min(z, z + facing * 0.18f)), new Vector3(x + 0.12f, y + h + 0.36f, MathF.Max(z, z + facing * 0.18f)), Gfx.Lerp(frame, wall, 0.4f), 0.5f, null);
        // Yatay kayit
        b.Box(M.Plaster, new Vector3(x - w / 2, y + h * 0.68f, MathF.Min(z, z + facing * 0.13f)), new Vector3(x + w / 2, y + h * 0.68f + 0.06f, MathF.Max(z, z + facing * 0.13f)), frame, 0.5f, null);
        // Panjur (bazen)
        if (b.Rng.Chance(0.3f))
        {
            var shutter = Gfx.Hex(0x4E7A4E);
            foreach (var side in new[] { -1f, 1f })
            {
                var sx = x + side * (w / 2 + 0.38f);
                b.Box(M.Wood, new Vector3(sx - 0.25f, y - 0.05f, MathF.Min(z, z + facing * 0.06f)), new Vector3(sx + 0.25f, y + h + 0.05f, MathF.Max(z, z + facing * 0.06f)), shutter, 1f, null);
            }
        }
    }

    public static void Balcony(BuildContext b, Vector3 bottomCenter, float facing, float w)
    {
        var z0 = bottomCenter.Z;
        var z1 = z0 + facing * 0.9f;
        var y = bottomCenter.Y;
        b.Box(M.Concrete, new Vector3(bottomCenter.X - w / 2, y, MathF.Min(z0, z1)), new Vector3(bottomCenter.X + w / 2, y + 0.15f, MathF.Max(z0, z1)), Gfx.Hex(0xD8D3CA), 0.5f, null);
        var rail = Gfx.Hex(0x2F3640);
        b.Draw(M.Metal, bottomCenter, m =>
        {
            var p0 = new Vector3(bottomCenter.X - w / 2, y + 0.15f, z1);
            var p1 = new Vector3(bottomCenter.X + w / 2, y + 0.15f, z1);
            // Boru korkuluk: kupeste, alt kusak, dikmeler (ortada kucuk halka susu)
            var h = new Vector3(0, 0.9f, 0);
            Shapes.Tube(m, [new Vector3(p0.X, p0.Y + 0.9f, z0), p0 + h, p1 + h, new Vector3(p1.X, p1.Y + 0.9f, z0)], 0.028f, 6, rail);
            Shapes.Tube(m, [p0 + new Vector3(0, 0.12f, 0), p1 + new Vector3(0, 0.12f, 0)], 0.016f, 5, rail);
            for (var i = 0; i <= 10; i++)
            {
                var p = Vector3.Lerp(p0, p1, i / 10f);
                Shapes.Tube(m, [p, p + h], 0.012f, 4, rail, false, false);
                if (i % 2 == 1)
                {
                    Shapes.Torus(m, Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(p + new Vector3(0, 0.5f, 0)), 0.06f, 0.008f, 10, 3, rail);
                }
            }
        });
        // Bazen saksi
        if (b.Rng.Chance(0.5f))
        {
            var pot = new Vector3(bottomCenter.X - w / 2 + 0.3f, y + 0.15f, z0 + facing * 0.6f);
            b.Draw(M.Concrete, pot, m => Shapes.Frustum(m, Matrix4x4.CreateTranslation(pot), 0.12f, 0.16f, 0.25f, 8, Gfx.Hex(0xB5651D)));
            b.Draw(M.Leaves, pot, m => Shapes.Sphere(m, Matrix4x4.CreateTranslation(pot + new Vector3(0, 0.38f, 0)), 0.2f, 4, 6, Gfx.Hex(0x6E9E4A)));
        }
    }

    /// <summary>Zemin kat dukkan: vitrin, kapi, tente, tabela.</summary>
    public static void ShopFront(BuildContext b, BuildingSpec s, float f, float zFront, float baseY)
    {
        var width = s.MaxX - s.MinX;
        var cx = (s.MinX + s.MaxX) / 2;
        var glassW = MathF.Min(width - 2.2f, 7.5f);
        var gx0 = cx - glassW / 2;
        var gz = zFront + f * 0.05f;
        var lit = s.Shop is not null;
        var glass = lit ? new Color(80, 92, 98, 128) : new Color(60, 70, 80, 30);
        // Vitrin camı
        b.Draw(M.WindowGlass, new Vector3(cx, baseY, gz), m =>
        {
            var right = f > 0 ? Vector3.UnitX : -Vector3.UnitX;
            var bl = new Vector3(f > 0 ? gx0 : gx0 + glassW, baseY + 0.5f, gz);
            Shapes.Quad(m, bl, bl + right * glassW, bl + right * glassW + new Vector3(0, 2.6f, 0), bl + new Vector3(0, 2.6f, 0), glass, 1f);
        });
        // Dograma
        var frame = Gfx.Hex(0x3D3D3D);
        foreach (var x in new[] { gx0, gx0 + glassW / 3, gx0 + glassW * 2 / 3, gx0 + glassW })
        {
            b.Box(M.Metal, new Vector3(x - 0.05f, baseY + 0.45f, MathF.Min(zFront, gz + f * 0.03f)), new Vector3(x + 0.05f, baseY + 3.15f, MathF.Max(zFront, gz + f * 0.03f)), frame, 1f, null);
        }

        b.Box(M.Metal, new Vector3(gx0 - 0.05f, baseY + 0.4f, MathF.Min(zFront, gz + f * 0.04f)), new Vector3(gx0 + glassW + 0.05f, baseY + 0.5f, MathF.Max(zFront, gz + f * 0.04f)), frame, 1f, null);
        b.Box(M.Metal, new Vector3(gx0 - 0.05f, baseY + 3.1f, MathF.Min(zFront, gz + f * 0.04f)), new Vector3(gx0 + glassW + 0.05f, baseY + 3.2f, MathF.Max(zFront, gz + f * 0.04f)), frame, 1f, null);

        if (s.Shop is not null)
        {
            // Tente: iki renkli seritler, egimli
            var aw0 = new Vector3(gx0 - 0.3f, baseY + 3.55f, zFront);
            var depth = 1.4f;
            var stripes = Math.Max(4, (int)(glassW / 0.6f));
            var colA = BuildContext.Hex(s.AwningA);
            var colB = BuildContext.Hex(s.AwningB);
            b.Draw(M.Fabric, aw0, m =>
            {
                var sw = (glassW + 0.6f) / stripes;
                for (var i = 0; i < stripes; i++)
                {
                    var x0 = aw0.X + i * sw;
                    var a = new Vector3(x0, aw0.Y, zFront + f * 0.02f);
                    var bb = new Vector3(x0 + sw, aw0.Y, zFront + f * 0.02f);
                    var c = new Vector3(x0 + sw, aw0.Y - 0.75f, zFront + f * depth);
                    var d = new Vector3(x0, aw0.Y - 0.75f, zFront + f * depth);
                    var col = i % 2 == 0 ? colA : colB;
                    if (f > 0)
                    {
                        Shapes.Quad(m, d, c, bb, a, col, 1f);
                    }
                    else
                    {
                        Shapes.Quad(m, a, bb, c, d, col, 1f);
                    }

                    // On sarkan etek
                    var e0 = new Vector3(x0, aw0.Y - 0.75f, zFront + f * depth);
                    var e1 = new Vector3(x0 + sw, aw0.Y - 0.75f, zFront + f * depth);
                    if (f > 0)
                    {
                        Shapes.Quad(m, e0 - new Vector3(0, 0.25f, 0), e1 - new Vector3(0, 0.25f, 0), e1, e0, col, 1f);
                    }
                    else
                    {
                        Shapes.Quad(m, e1 - new Vector3(0, 0.25f, 0), e0 - new Vector3(0, 0.25f, 0), e0, e1, col, 1f);
                    }
                }
            });
            b.SignBoard(s.Shop, new Vector3(cx, baseY + 3.85f, zFront + f * 0.13f), MathF.Min(width - 1f, 6.5f), 0.75f,
                new Vector3(0, 0, f), BuildContext.Hex(s.ShopColor), Color.White, lit: true);
            b.Layout.Lamps.Add(new LampInfo(new Vector3(cx, baseY + 2.2f, zFront + f * 1.3f), 7f, new Vector3(1.6f, 1.25f, 0.8f), LampKind.Shop));
        }
    }
}
