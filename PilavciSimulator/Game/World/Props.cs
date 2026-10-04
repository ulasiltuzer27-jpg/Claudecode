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
        b.Draw(M.Metal, basePos, m =>
        {
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(basePos), 0.12f, 0.35f, 10, pole);
            Shapes.Frustum(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.35f, 0)), 0.07f, 0.05f, 4.4f, 8, pole, false, false);
            var top = basePos + new Vector3(0, 4.7f, 0);
            Shapes.Beam(m, top, top + armDir * 1.1f + new Vector3(0, 0.15f, 0), 0.07f, pole);
            var head = top + armDir * 1.15f + new Vector3(0, 0.1f, 0);
            Shapes.Frustum(m, Matrix4x4.CreateTranslation(head - new Vector3(0, 0.12f, 0)), 0.28f, 0.1f, 0.22f, 10, pole, true, true);
        });
        var bulbPos = basePos + new Vector3(0, 4.62f, 0) + armDir * 1.15f;
        b.Draw(M.Emissive, bulbPos, m => Shapes.Sphere(m, Matrix4x4.CreateTranslation(bulbPos), 0.13f, 6, 10, new Color(255, 238, 200, 120)));
        b.Layout.Lamps.Add(new LampInfo(bulbPos - new Vector3(0, 0.3f, 0), 13f, new Vector3(3.2f, 2.5f, 1.6f), LampKind.Street));
    }

    // ── Agac ─────────────────────────────────────────────────────────
    public static void Tree(BuildContext b, Vector3 basePos, float scale = 1f)
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
            for (var i = 0; i < 3; i++)
            {
                Shapes.Box(m, Matrix4x4.CreateTranslation(0, 0.45f, -0.15f + i * 0.15f) * xf, new Vector3(1.6f, 0.05f, 0.12f), Color.White, 1f);
            }

            for (var i = 0; i < 2; i++)
            {
                Shapes.Box(m, Matrix4x4.CreateRotationX(-0.2f) * Matrix4x4.CreateTranslation(0, 0.7f + i * 0.17f, 0.27f + i * 0.03f) * xf, new Vector3(1.6f, 0.12f, 0.04f), Color.White, 1f);
            }
        });
        b.Draw(M.Metal, basePos, m =>
        {
            foreach (var x in new[] { -0.7f, 0.7f })
            {
                Shapes.Box(m, Matrix4x4.CreateTranslation(x, 0.22f, 0) * xf, new Vector3(0.06f, 0.44f, 0.45f), metal);
                Shapes.Box(m, Matrix4x4.CreateTranslation(x, 0.65f, 0.28f) * xf, new Vector3(0.05f, 0.45f, 0.05f), metal);
            }
        });
    }

    // ── Cop kutusu ───────────────────────────────────────────────────
    public static void Bin(BuildContext b, Vector3 basePos)
    {
        b.ColliderYaw(basePos, new Vector3(0.45f, 0.9f, 0.45f), 0);
        b.Draw(M.Metal, basePos, m =>
        {
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(basePos), 0.22f, 0.85f, 12, Gfx.Hex(0x2E7D4F));
            Shapes.Cylinder(m, Matrix4x4.CreateTranslation(basePos + new Vector3(0, 0.85f, 0)), 0.24f, 0.06f, 12, Gfx.Hex(0x1E5D3A));
        });
    }

    // ── Park etmis araba ─────────────────────────────────────────────
    public static void ParkedCar(BuildContext b, Vector3 basePos, float yaw, Color body)
    {
        b.ColliderYaw(basePos, new Vector3(4.2f, 1.45f, 1.8f), yaw);
        var xf = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(basePos);
        CarBody(b, basePos, xf, body);
    }

    public static void CarBody(BuildContext b, Vector3 at, Matrix4x4 xf, Color body)
    {
        b.Draw(M.Plastic, at, m => CarMesh(m, xf, body));
        b.Draw(M.WindowGlass, at, m => CarGlass(m, xf));
        b.Draw(M.Rubber, at, m => CarWheels(m, xf));
    }

    public static void CarMesh(MeshData m, Matrix4x4 xf, Color body)
    {
        Shapes.Box(m, Matrix4x4.CreateTranslation(0, 0.55f, 0) * xf, new Vector3(4.1f, 0.6f, 1.75f), body);
        Shapes.Box(m, Matrix4x4.CreateTranslation(-0.2f, 1.08f, 0) * xf, new Vector3(2.2f, 0.5f, 1.6f), body);
        // Tamponlar
        Shapes.Box(m, Matrix4x4.CreateTranslation(2.06f, 0.4f, 0) * xf, new Vector3(0.1f, 0.2f, 1.7f), Gfx.Hex(0x333333));
        Shapes.Box(m, Matrix4x4.CreateTranslation(-2.06f, 0.4f, 0) * xf, new Vector3(0.1f, 0.2f, 1.7f), Gfx.Hex(0x333333));
        // Farlar
        Shapes.Box(m, Matrix4x4.CreateTranslation(2.04f, 0.65f, 0.6f) * xf, new Vector3(0.06f, 0.14f, 0.3f), Gfx.Hex(0xFFF6D5));
        Shapes.Box(m, Matrix4x4.CreateTranslation(2.04f, 0.65f, -0.6f) * xf, new Vector3(0.06f, 0.14f, 0.3f), Gfx.Hex(0xFFF6D5));
        Shapes.Box(m, Matrix4x4.CreateTranslation(-2.04f, 0.65f, 0.6f) * xf, new Vector3(0.06f, 0.12f, 0.28f), Gfx.Hex(0xC0392B));
        Shapes.Box(m, Matrix4x4.CreateTranslation(-2.04f, 0.65f, -0.6f) * xf, new Vector3(0.06f, 0.12f, 0.28f), Gfx.Hex(0xC0392B));
    }

    public static void CarGlass(MeshData m, Matrix4x4 xf)
    {
        var g = new Color(60, 75, 90, 40);
        Shapes.Box(m, Matrix4x4.CreateTranslation(-0.2f, 1.1f, 0) * xf, new Vector3(2.24f, 0.36f, 1.62f), g);
    }

    public static void CarWheels(MeshData m, Matrix4x4 xf)
    {
        foreach (var x in new[] { -1.3f, 1.3f })
        {
            foreach (var z in new[] { -0.82f, 0.82f })
            {
                var w = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(x, 0.33f, z - 0.11f) * xf;
                Shapes.Cylinder(m, w, 0.33f, 0.22f, 12, Gfx.Hex(0x1C1C1C));
            }
        }
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
        }

        b.Rng = rng;
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
        // Pervaz
        b.Box(M.Plaster, new Vector3(x - w / 2 - 0.2f, y - 0.2f, MathF.Min(z, z + facing * 0.22f)), new Vector3(x + w / 2 + 0.2f, y - 0.1f, MathF.Max(z, z + facing * 0.22f)), frame, 0.5f, null);
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
            Shapes.Beam(m, p0 + new Vector3(0, 0.9f, 0), p1 + new Vector3(0, 0.9f, 0), 0.05f, rail);
            for (var i = 0; i <= 8; i++)
            {
                var p = Vector3.Lerp(p0, p1, i / 8f);
                Shapes.Beam(m, p, p + new Vector3(0, 0.9f, 0), 0.025f, rail);
            }

            Shapes.Beam(m, p0 + new Vector3(0, 0.9f, 0), new Vector3(p0.X, p0.Y + 0.9f, z0), 0.05f, rail);
            Shapes.Beam(m, p1 + new Vector3(0, 0.9f, 0), new Vector3(p1.X, p1.Y + 0.9f, z0), 0.05f, rail);
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
