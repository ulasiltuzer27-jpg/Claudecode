using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Client;

/// <summary>Karakterin gorunusu: tohumdan ve musteri tipinden kararli sekilde turetilir.</summary>
public sealed class Appearance
{
    public Color Skin;
    public Color Hair;
    public Color Shirt;
    public Color Pants;
    public Color Shoes;
    public Color HatColor;
    public float Height = 1f;
    public float Width = 1f;
    public bool Mustache;
    public bool Beard;
    public bool Headscarf;
    public bool Cap;
    public bool Hat;
    public bool HardHat;
    public bool Glasses;
    public bool Backpack;
    public bool Tie;
    public bool Apron;
    public bool Bald;
    public bool LongHair;
    public bool Child;
    public bool Elderly;
    public bool Uniform;
    public Color ApronColor;

    private static readonly uint[] Skins = [0xF1C27D, 0xE0AC69, 0xC68642, 0x8D5524, 0xFFDBAC, 0xEAC086];
    private static readonly uint[] Hairs = [0x2C1B10, 0x3B2414, 0x6A4E42, 0x111111, 0xA0522D, 0xC0C0C0, 0xE6BE8A];

    public static Appearance For(CustomerTypeDef? type, uint seed)
    {
        var r = new Rng(seed | 1);
        var a = new Appearance();
        var looks = type?.Looks ?? new CustomerLooks();
        a.Skin = Gfx.Hex(Skins[r.Range(0, Skins.Length)]);
        a.Elderly = looks.Elderly && r.Chance(0.7f);
        a.Hair = a.Elderly ? Gfx.Hex(r.Chance(0.5f) ? 0xC8C8C8u : 0xE5E5E5u) : Gfx.Hex(Hairs[r.Range(0, Hairs.Length - 2)]);
        a.Shirt = looks.Shirts.Count > 0 ? Gfx.Hex(Convert.ToUInt32(looks.Shirts[r.Range(0, looks.Shirts.Count)], 16)) : Gfx.Hex(0x5D6D7E);
        a.Pants = looks.Pants.Count > 0 ? Gfx.Hex(Convert.ToUInt32(looks.Pants[r.Range(0, looks.Pants.Count)], 16)) : Gfx.Hex(0x2C3E50);
        a.Shoes = r.Chance(0.5f) ? Gfx.Hex(0x1C1C1C) : Gfx.Hex(0x5D4037);
        a.Height = r.Range(looks.Height[0], looks.Height[1]);
        a.Width = r.Range(0.92f, 1.12f);
        a.Child = looks.Child;
        a.Headscarf = r.Chance(looks.Headscarf);
        a.HardHat = !a.Headscarf && r.Chance(looks.HardHat);
        a.Cap = !a.Headscarf && !a.HardHat && r.Chance(looks.Cap);
        a.Hat = !a.Headscarf && !a.HardHat && !a.Cap && r.Chance(looks.Hat);
        a.Mustache = !a.Headscarf && !a.Child && r.Chance(looks.Mustache);
        a.Beard = !a.Headscarf && !a.Child && r.Chance(looks.Beard);
        a.Glasses = r.Chance(looks.Glasses);
        a.Backpack = r.Chance(looks.Backpack);
        a.Tie = !a.Headscarf && r.Chance(looks.Tie);
        a.Bald = !a.Headscarf && !a.Child && a.Elderly && r.Chance(0.35f);
        a.LongHair = !a.Headscarf && !a.Mustache && r.Chance(0.3f);
        a.HatColor = Gfx.Hex(new uint[] { 0x2C3E50, 0x7B241C, 0x4D5656, 0x1E8449, 0xD4AC0D }[r.Range(0, 5)]);
        if (a.Headscarf)
        {
            a.HatColor = Gfx.Hex(new uint[] { 0x8E44AD, 0xC0392B, 0x2471A3, 0xD35400, 0x117A65, 0xF5B7B1 }[r.Range(0, 6)]);
        }

        return a;
    }

    public static Appearance ForPlayer(byte colorIndex, uint seed)
    {
        var a = For(null, seed);
        a.Shirt = Gfx.Hex(0xF4F6F7);
        a.Pants = Gfx.Hex(0x34495E);
        a.Apron = true;
        a.ApronColor = Gfx.Hex(PlayerEntity.Colors[colorIndex % PlayerEntity.Colors.Length]);
        a.Cap = true;
        a.Hat = a.HardHat = a.Headscarf = false;
        a.HatColor = a.ApronColor;
        a.Backpack = a.Tie = false;
        a.Height = 1.02f;
        return a;
    }

    public static Appearance Zabita()
    {
        var a = For(null, 99);
        a.Shirt = Gfx.Hex(0x1F3A93);
        a.Pants = Gfx.Hex(0x1B2631);
        a.Cap = true;
        a.HatColor = Gfx.Hex(0x1B2631);
        a.Mustache = true;
        a.Uniform = true;
        return a;
    }
}

/// <summary>Bir karenin eklem acilari (radyan).</summary>
public struct Pose
{
    public float LeftShoulder, RightShoulder, LeftShoulderSide, RightShoulderSide;
    public float LeftElbow, RightElbow;
    public float LeftHip, RightHip, LeftKnee, RightKnee;
    public float Spine, Head, HeadYaw, Bob, Sit;
}

/// <summary>
/// Kutu-parcali insan: govde, bas, kollar, bacaklar ayri mesh; eklemler
/// kodla dondurulur (iskelet animasyonu yok). Yurume, bekleme, konusma,
/// yeme, el sallama, kizgin, mutlu, oturma pozlari.
/// </summary>
public sealed class CharacterRig
{
    private readonly ModelLibrary _lib;
    private readonly Renderer _r;

    private readonly RenderModel _torso, _head, _hairShort, _hairLong, _bald, _cap, _hat, _hardHat, _scarf, _upperArm, _foreArm, _hand;
    private readonly RenderModel _thigh, _shin, _shoe, _mustache, _beard, _glasses, _backpack, _tie, _apron, _plate, _pkg, _spoon;

    public CharacterRig(ModelLibrary lib, Renderer r)
    {
        _lib = lib;
        _r = r;
        var w = Color.White;
        _torso = lib.Get("ch_torso", b =>
        {
            Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.55f) * Matrix4x4.CreateTranslation(0, -0.62f, 0), 0.19f, 0.23f, 0.62f, 10, w);
            Shapes.Sphere(b[M.Fabric], Matrix4x4.CreateScale(1, 0.45f, 0.6f), 0.23f, 4, 10, w);
        });
        _head = lib.Get("ch_head", b =>
        {
            Shapes.Cylinder(b[M.Skin], Matrix4x4.CreateTranslation(0, -0.1f, 0), 0.055f, 0.1f, 8, w);
            Shapes.Sphere(b[M.Skin], Matrix4x4.CreateScale(0.92f, 1.08f, 1f) * Matrix4x4.CreateTranslation(0, 0.1f, 0), 0.115f, 8, 12, w);
            Shapes.Box(b[M.Skin], Matrix4x4.CreateTranslation(0, 0.08f, -0.115f), new Vector3(0.03f, 0.05f, 0.04f), w);
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(s * 0.04f, 0.125f, -0.103f), new Vector3(0.025f, 0.022f, 0.01f), Gfx.Hex(0x1C1C1C));
                Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(s * 0.042f, 0.158f, -0.104f), new Vector3(0.04f, 0.01f, 0.01f), Gfx.Hex(0x3B2A1E));
                Shapes.Sphere(b[M.Skin], Matrix4x4.CreateScale(0.5f, 1, 1) * Matrix4x4.CreateTranslation(s * 0.11f, 0.1f, 0), 0.03f, 3, 6, w);
            }

            Shapes.Box(b[M.White], Matrix4x4.CreateTranslation(0, 0.03f, -0.105f), new Vector3(0.045f, 0.008f, 0.01f), Gfx.Hex(0x9E5A4A));
        });
        _hairShort = lib.Get("ch_hair", b => Shapes.Sphere(b[M.Hair], Matrix4x4.CreateScale(0.96f, 0.75f, 1.02f) * Matrix4x4.CreateTranslation(0, 0.14f, 0.012f), 0.12f, 6, 12, w));
        _hairLong = lib.Get("ch_hairlong", b =>
        {
            Shapes.Sphere(b[M.Hair], Matrix4x4.CreateScale(0.98f, 0.8f, 1.04f) * Matrix4x4.CreateTranslation(0, 0.14f, 0.012f), 0.122f, 6, 12, w);
            Shapes.Box(b[M.Hair], Matrix4x4.CreateTranslation(0, 0.02f, 0.06f), new Vector3(0.22f, 0.22f, 0.1f), w);
        });
        _bald = lib.Get("ch_bald", b => Shapes.Box(b[M.Hair], Matrix4x4.CreateTranslation(0, 0.08f, 0.07f), new Vector3(0.22f, 0.08f, 0.1f), w));
        _cap = lib.Get("ch_cap", b =>
        {
            Shapes.Sphere(b[M.Fabric], Matrix4x4.CreateScale(1.02f, 0.6f, 1.06f) * Matrix4x4.CreateTranslation(0, 0.17f, 0.005f), 0.122f, 5, 12, w);
            Shapes.Box(b[M.Fabric], Matrix4x4.CreateRotationX(0.15f) * Matrix4x4.CreateTranslation(0, 0.175f, -0.13f), new Vector3(0.2f, 0.012f, 0.1f), w);
        });
        _hat = lib.Get("ch_hat", b =>
        {
            Shapes.Cylinder(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.19f, 0), 0.2f, 0.012f, 16, w);
            Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateTranslation(0, 0.19f, 0), 0.12f, 0.1f, 0.1f, 12, w);
        });
        _hardHat = lib.Get("ch_hardhat", b =>
        {
            Shapes.Sphere(b[M.Plastic], Matrix4x4.CreateScale(1.08f, 0.75f, 1.12f) * Matrix4x4.CreateTranslation(0, 0.16f, 0), 0.13f, 5, 12, w);
            Shapes.Cylinder(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.16f, 0), 0.16f, 0.012f, 14, w);
        });
        _scarf = lib.Get("ch_scarf", b =>
        {
            Shapes.Sphere(b[M.Fabric], Matrix4x4.CreateScale(1.08f, 1.12f, 1.1f) * Matrix4x4.CreateTranslation(0, 0.11f, 0.01f), 0.125f, 6, 12, w);
            Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateScale(1, 1, 0.8f) * Matrix4x4.CreateTranslation(0, -0.12f, 0.01f), 0.17f, 0.1f, 0.14f, 10, w);
        });
        _upperArm = lib.Get("ch_uarm", b => Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.3f, 0), 0.045f, 0.055f, 0.3f, 8, w));
        _foreArm = lib.Get("ch_farm", b => Shapes.Frustum(b[M.Skin], Matrix4x4.CreateTranslation(0, -0.27f, 0), 0.035f, 0.045f, 0.27f, 8, w));
        _hand = lib.Get("ch_hand", b => Shapes.Box(b[M.Skin], Matrix4x4.CreateTranslation(0, -0.05f, 0), new Vector3(0.06f, 0.1f, 0.035f), w));
        _thigh = lib.Get("ch_thigh", b => Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.44f, 0), 0.06f, 0.075f, 0.44f, 8, w));
        _shin = lib.Get("ch_shin", b => Shapes.Frustum(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.43f, 0), 0.045f, 0.06f, 0.43f, 8, w));
        _shoe = lib.Get("ch_shoe", b => Shapes.Box(b[M.Rubber], Matrix4x4.CreateTranslation(0, 0.04f, -0.04f), new Vector3(0.1f, 0.08f, 0.24f), w));
        _mustache = lib.Get("ch_mustache", b => Shapes.Box(b[M.Hair], Matrix4x4.CreateTranslation(0, 0.055f, -0.112f), new Vector3(0.085f, 0.022f, 0.02f), w));
        _beard = lib.Get("ch_beard", b => Shapes.Sphere(b[M.Hair], Matrix4x4.CreateScale(1, 0.8f, 0.6f) * Matrix4x4.CreateTranslation(0, 0.02f, -0.06f), 0.1f, 5, 10, w));
        _glasses = lib.Get("ch_glasses", b =>
        {
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(s * 0.042f, 0.125f, -0.118f), new Vector3(0.05f, 0.035f, 0.008f), w);
            }

            Shapes.Box(b[M.Plastic], Matrix4x4.CreateTranslation(0, 0.13f, -0.118f), new Vector3(0.04f, 0.008f, 0.008f), w);
        });
        _backpack = lib.Get("ch_backpack", b => Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.28f, 0.17f), new Vector3(0.3f, 0.38f, 0.14f), w));
        _tie = lib.Get("ch_tie", b => Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.2f, -0.13f), new Vector3(0.05f, 0.32f, 0.01f), w));
        _apron = lib.Get("ch_apron", b => Shapes.Box(b[M.Fabric], Matrix4x4.CreateTranslation(0, -0.45f, -0.135f), new Vector3(0.36f, 0.7f, 0.012f), w));
        _plate = lib.Plate();
        _pkg = lib.Package();
        _spoon = lib.Spoon();
    }

    /// <summary>Animasyon durumundan poz.</summary>
    public static Pose PoseFor(CustomerAnim anim, float t, float speed, uint seed)
    {
        var p = new Pose();
        var phase = t * MathF.Tau * (0.9f + speed * 0.35f) + (seed % 100) * 0.1f;
        switch (anim)
        {
            case CustomerAnim.Walk:
            {
                var s = MathF.Sin(phase);
                p.LeftShoulder = s * 0.55f;
                p.RightShoulder = -s * 0.55f;
                p.LeftElbow = -0.25f;
                p.RightElbow = -0.25f;
                p.LeftHip = -s * 0.5f;
                p.RightHip = s * 0.5f;
                p.LeftKnee = MathF.Max(0, -MathF.Cos(phase)) * 0.7f;
                p.RightKnee = MathF.Max(0, MathF.Cos(phase)) * 0.7f;
                p.Bob = MathF.Abs(MathF.Cos(phase)) * 0.035f;
                p.Spine = 0.04f;
                break;
            }
            case CustomerAnim.Talk:
                p.RightShoulder = -0.5f + MathF.Sin(t * 5) * 0.2f;
                p.RightElbow = -1.0f;
                p.Head = MathF.Sin(t * 6) * 0.08f;
                p.LeftShoulder = 0.05f;
                break;
            case CustomerAnim.Eat:
            {
                var bite = MathF.Max(0, MathF.Sin(t * 1.6f));
                p.LeftShoulder = -0.7f;
                p.LeftElbow = -1.2f;
                p.RightShoulder = -0.5f - bite * 0.9f;
                p.RightElbow = -1.3f - bite * 0.6f;
                p.Head = 0.15f - bite * 0.1f;
                break;
            }
            case CustomerAnim.Wave:
                p.RightShoulder = -2.6f;
                p.RightShoulderSide = 0.3f;
                p.RightElbow = -0.4f + MathF.Sin(t * 10) * 0.4f;
                break;
            case CustomerAnim.Angry:
                p.LeftShoulder = -0.3f + MathF.Sin(t * 12) * 0.2f;
                p.RightShoulder = -0.3f - MathF.Sin(t * 12) * 0.2f;
                p.LeftElbow = -1.4f;
                p.RightElbow = -1.4f;
                p.Head = MathF.Sin(t * 9) * 0.1f;
                p.Spine = -0.05f;
                break;
            case CustomerAnim.Happy:
                p.LeftShoulder = -0.4f;
                p.RightShoulder = -0.4f;
                p.LeftShoulderSide = 0.4f + MathF.Sin(t * 8) * 0.1f;
                p.RightShoulderSide = 0.4f + MathF.Sin(t * 8) * 0.1f;
                p.Bob = MathF.Abs(MathF.Sin(t * 8)) * 0.04f;
                break;
            case CustomerAnim.Sit:
                p.Sit = 1;
                p.LeftHip = -1.55f;
                p.RightHip = -1.55f; // one dogru
                p.LeftKnee = 1.55f;
                p.RightKnee = 1.55f;
                var bite2 = MathF.Max(0, MathF.Sin(t * 1.6f));
                p.RightShoulder = -0.6f - bite2 * 0.7f;
                p.RightElbow = -1.3f;
                p.LeftShoulder = -0.5f;
                p.LeftElbow = -1.2f;
                break;
            default:
                // Nefes
                p.Bob = MathF.Sin(t * 1.7f + seed) * 0.006f;
                p.LeftShoulder = MathF.Sin(t * 1.1f + seed) * 0.03f;
                p.RightShoulder = -MathF.Sin(t * 1.1f + seed) * 0.03f;
                p.HeadYaw = MathF.Sin(t * 0.37f + seed % 7) * 0.25f;
                break;
        }

        return p;
    }

    /// <summary>Karakteri ciz. holds: 0 yok, 1 tabak, 2 paket, 3 kasik (oyuncu).</summary>
    public void Draw(Vector3 pos, float yaw, Appearance a, Pose p, int holds = 0, bool castShadow = true, float scaleBias = 1f)
    {
        var s = a.Height * scaleBias * (a.Child ? 1f : 1f);
        var w = a.Width;
        var flags = castShadow ? DrawFlags.None : DrawFlags.NoShadow;
        var root = Matrix4x4.CreateScale(w, 1, w) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(pos);
        var hipY = 0.95f - p.Sit * 0.45f + p.Bob;
        Matrix4x4 J(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

        // Bacaklar
        foreach (var side in new[] { -1f, 1f })
        {
            // Poz kurali: negatif kalca/omuz = one dogru; pozitif diz = bukulme.
            var hipA = -(side < 0 ? p.LeftHip : p.RightHip);
            var kneeA = -(side < 0 ? p.LeftKnee : p.RightKnee);
            var hip = Matrix4x4.CreateRotationX(hipA) * J(side * 0.1f, hipY, 0) * root;
            _r.Submit(_thigh, hip, a.Pants, flags);
            var kneeM = Matrix4x4.CreateRotationX(kneeA) * J(0, -0.44f, 0) * hip;
            _r.Submit(_shin, kneeM, a.Pants, flags);
            var foot = Matrix4x4.CreateRotationX(-(hipA + kneeA)) * J(0, -0.47f, 0) * kneeM;
            _r.Submit(_shoe, foot, a.Shoes, flags);
        }

        // Govde
        var torso = Matrix4x4.CreateRotationX(p.Spine) * J(0, hipY + 0.62f, 0) * root;
        _r.Submit(_torso, torso, a.Shirt, flags);
        if (a.Apron)
        {
            _r.Submit(_apron, torso, a.ApronColor, flags);
        }

        if (a.Tie)
        {
            _r.Submit(_tie, torso, Gfx.Hex(0x922B21), flags);
        }

        if (a.Backpack)
        {
            _r.Submit(_backpack, torso, a.HatColor, flags);
        }

        // Bas
        var head = Matrix4x4.CreateRotationX(p.Head) * Matrix4x4.CreateRotationY(p.HeadYaw) * J(0, 0.1f, 0) * torso;
        _r.Submit(_head, head, a.Skin, flags);
        if (a.Headscarf)
        {
            _r.Submit(_scarf, head, a.HatColor, flags);
        }
        else
        {
            if (a.Bald)
            {
                _r.Submit(_bald, head, a.Hair, flags);
            }
            else if (!a.HardHat)
            {
                _r.Submit(a.LongHair ? _hairLong : _hairShort, head, a.Hair, flags);
            }

            if (a.Cap)
            {
                _r.Submit(_cap, head, a.HatColor, flags);
            }
            else if (a.Hat)
            {
                _r.Submit(_hat, head, a.HatColor, flags);
            }
            else if (a.HardHat)
            {
                _r.Submit(_hardHat, head, Gfx.Hex(0xF4D03F), flags);
            }
        }

        if (a.Mustache)
        {
            _r.Submit(_mustache, head, a.Hair, flags);
        }

        if (a.Beard)
        {
            _r.Submit(_beard, head, a.Hair, flags);
        }

        if (a.Glasses)
        {
            _r.Submit(_glasses, head, Gfx.Hex(0x1C1C1C), flags);
        }

        // Kollar
        foreach (var side in new[] { -1f, 1f })
        {
            var sh = side < 0 ? p.LeftShoulder : p.RightShoulder;
            var shSide = side < 0 ? p.LeftShoulderSide : p.RightShoulderSide;
            var el = side < 0 ? p.LeftElbow : p.RightElbow;
            var shoulder = Matrix4x4.CreateRotationZ(side * (0.08f + shSide)) * Matrix4x4.CreateRotationX(-sh) * J(side * 0.23f, -0.04f, 0) * torso;
            _r.Submit(_upperArm, shoulder, a.Shirt, flags);
            var elbow = Matrix4x4.CreateRotationX(-el) * J(0, -0.3f, 0) * shoulder;
            _r.Submit(_foreArm, elbow, a.Skin, flags);
            var hand = J(0, -0.27f, 0) * elbow;
            _r.Submit(_hand, hand, a.Skin, flags);
        }

        // Elde tasinan
        if (holds is 1 or 2)
        {
            var chest = J(0, -0.42f, -0.3f) * torso;
            _r.Submit(holds == 1 ? _plate : _pkg, chest, Color.White, flags);
        }
    }
}
