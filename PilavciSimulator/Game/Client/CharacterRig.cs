using System.Numerics;
using Raylib_cs;
using PilavciSimulator.Content;
using PilavciSimulator.Core;
using PilavciSimulator.Engine.Rendering;
using PilavciSimulator.Sim.Data;
using PilavciSimulator.Sim.Entities;

namespace PilavciSimulator.Client;

public enum HairStyle : byte
{
    Short,
    Long,
    Bun,
    Curly,
    Bald,
    Buzz,
}

public enum FacialHair : byte
{
    None,
    Mustache,
    Pala,
    Beard,
}

public enum HatKind : byte
{
    None,
    Kasket,
    Fedora,
    HardHat,
    Headscarf,
    Beanie,
    Toque,
    UniformCap,
}

/// <summary>Karakterin gorunusu: tohumdan ve musteri tipinden kararli sekilde turetilir.</summary>
public sealed class Appearance
{
    public Color Skin;
    public Color Hair;
    public Color Eyes = Gfx.Hex(0x3B2A1E);
    public Color Shirt;
    public Color Pants;
    public Color Shoes;
    public Color HatColor;
    public float Height = 1f;
    public float Width = 1f;
    public HairStyle HairStyle;
    public FacialHair Facial;
    public HatKind Hat;
    public bool Glasses;
    public bool Backpack;
    public bool Tie;
    public bool Apron;
    public bool LongSleeves;
    public bool Child;
    public bool Elderly;
    public bool Uniform;
    public Color ApronColor;
    /// <summary>Onluk deseni: 0 duz, 1 cizgili, 2 kareli.</summary>
    public byte ApronStyle;

    private static readonly uint[] Skins = [0xF1C27D, 0xE0AC69, 0xC68642, 0x8D5524, 0xFFDBAC, 0xEAC086];
    private static readonly uint[] Hairs = [0x2C1B10, 0x3B2414, 0x6A4E42, 0x111111, 0xA0522D, 0xC0C0C0, 0xE6BE8A];
    private static readonly uint[] EyeColors = [0x3B2A1E, 0x2E1F14, 0x4E6B3A, 0x3C6E8F, 0x5D4037];

    public static IReadOnlyList<uint> SkinPalette => Skins;
    public static IReadOnlyList<uint> HairPalette => Hairs;

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
        a.Eyes = Gfx.Hex(EyeColors[r.Range(0, EyeColors.Length)]);
        var scarf = r.Chance(looks.Headscarf);
        var hardHat = !scarf && r.Chance(looks.HardHat);
        var cap = !scarf && !hardHat && r.Chance(looks.Cap);
        var hat = !scarf && !hardHat && !cap && r.Chance(looks.Hat);
        a.Hat = scarf ? HatKind.Headscarf : hardHat ? HatKind.HardHat : cap ? HatKind.Kasket : hat ? HatKind.Fedora : HatKind.None;
        var mustache = !scarf && !a.Child && r.Chance(looks.Mustache);
        var beard = !scarf && !a.Child && r.Chance(looks.Beard);
        a.Facial = beard ? FacialHair.Beard : mustache ? (r.Chance(0.4f) ? FacialHair.Pala : FacialHair.Mustache) : FacialHair.None;
        a.Glasses = r.Chance(looks.Glasses);
        a.Backpack = r.Chance(looks.Backpack);
        a.Tie = !scarf && r.Chance(looks.Tie);
        var bald = !scarf && !a.Child && a.Elderly && r.Chance(0.35f);
        var longHair = !scarf && !mustache && r.Chance(0.3f);
        a.HairStyle = bald ? HairStyle.Bald : longHair ? (r.Chance(0.35f) ? HairStyle.Bun : HairStyle.Long) : r.Chance(0.18f) ? HairStyle.Curly : r.Chance(0.2f) ? HairStyle.Buzz : HairStyle.Short;
        a.LongSleeves = a.Elderly || a.Tie || scarf || r.Chance(0.35f);
        a.HatColor = Gfx.Hex(new uint[] { 0x2C3E50, 0x7B241C, 0x4D5656, 0x1E8449, 0xD4AC0D }[r.Range(0, 5)]);
        if (scarf)
        {
            a.HatColor = Gfx.Hex(new uint[] { 0x8E44AD, 0xC0392B, 0x2471A3, 0xD35400, 0x117A65, 0xF5B7B1 }[r.Range(0, 6)]);
        }

        if (a.Hat == HatKind.HardHat)
        {
            a.HatColor = Gfx.Hex(0xF4D03F);
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
        a.Hat = HatKind.Kasket;
        a.HatColor = a.ApronColor;
        a.Backpack = a.Tie = false;
        a.Height = 1.02f;
        a.Child = false;
        return a;
    }

    public static Appearance Zabita()
    {
        var a = For(null, 99);
        a.Shirt = Gfx.Hex(0x1F3A93);
        a.Pants = Gfx.Hex(0x1B2631);
        a.Hat = HatKind.UniformCap;
        a.HatColor = Gfx.Hex(0x1B2631);
        a.Facial = FacialHair.Mustache;
        a.HairStyle = HairStyle.Short;
        a.Uniform = true;
        a.LongSleeves = true;
        return a;
    }

    /// <summary>Pisirilmis bas/govde modelleri icin anahtar (ayni gorunus = ayni model).</summary>
    public ulong Key
    {
        get
        {
            var h = 1469598103934665603UL;
            void Mix(ulong v)
            {
                h ^= v;
                h *= 1099511628211UL;
            }

            void C(Color c) => Mix(((ulong)c.R << 16) | ((ulong)c.G << 8) | c.B);
            C(Skin);
            C(Hair);
            C(Eyes);
            C(Shirt);
            C(Pants);
            C(HatColor);
            C(ApronColor);
            Mix((ulong)HairStyle | ((ulong)Facial << 8) | ((ulong)Hat << 16) | ((ulong)ApronStyle << 24));
            Mix((Glasses ? 1UL : 0) | (Backpack ? 2UL : 0) | (Tie ? 4UL : 0) | (Apron ? 8UL : 0) | (LongSleeves ? 16UL : 0) |
                (Child ? 32UL : 0) | (Elderly ? 64UL : 0) | (Uniform ? 128UL : 0));
            return h;
        }
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

/// <summary>Yuz ifadesi: agiz bicimi, konusma, goz kirpma zamani.</summary>
public struct Face
{
    /// <summary>0 notr, 1 gulumseme, 2 asik surat.</summary>
    public byte Expression;
    public bool Talking;
    public float Time;
    public uint Seed;

    public static Face From(Mood mood, bool talking, float time, uint seed) => new()
    {
        Expression = mood switch
        {
            Mood.Happy or Mood.Love => 1,
            Mood.Angry => 2,
            _ => 0,
        },
        Talking = talking,
        Time = time,
        Seed = seed,
    };
}

/// <summary>
/// Insan karakteri. Uzuvlar kapsul (bukulunce eklemde bosluk kalmaz),
/// eller basparmakli eldiven, bas ve govde her gorunus icin tek parca
/// "pisirilir": goz, kas, agiz, sac, sapka, onluk renkleri koselerde.
/// Boylece bir karakter ~14 cizim cagrisi, uzakta daha az.
/// Iskelet animasyonu yok: eklemler kodla dondurulur.
/// </summary>
public sealed class CharacterRig
{
    private const int CacheCapacity = 220;

    private readonly ModelLibrary _lib;
    private readonly Renderer _r;
    private readonly RenderModel _upperArm, _foreArm, _hand, _thigh, _shin, _shoe, _eyelids, _plate, _pkg, _spoon;
    private readonly RenderModel[] _mouths = new RenderModel[4];
    private readonly Dictionary<ulong, LinkedListNode<(ulong Key, RenderModel Head, RenderModel Torso)>> _cache = new();
    private readonly LinkedList<(ulong Key, RenderModel Head, RenderModel Torso)> _lru = new();

    public CharacterRig(ModelLibrary lib, Renderer r)
    {
        _lib = lib;
        _r = r;
        var w = Color.White;
        _upperArm = lib.Get("ch2_uarm", b => Shapes.CapsuleBetween(b[M.Fabric], Vector3.Zero, new Vector3(0, -0.29f, 0), 0.048f, 6, 10, w));
        _foreArm = lib.Get("ch2_farm", b => Shapes.CapsuleBetween(b[M.Skin], Vector3.Zero, new Vector3(0, -0.26f, 0), 0.04f, 6, 10, w));
        _hand = lib.Get("ch2_hand", b =>
        {
            Shapes.RoundedBox(b[M.Skin], Matrix4x4.CreateTranslation(0, -0.055f, 0), new Vector3(0.058f, 0.09f, 0.034f), 0.014f, 2, w);
            Shapes.CapsuleBetween(b[M.Skin], new Vector3(0, -0.028f, -0.016f), new Vector3(0, -0.068f, -0.04f), 0.012f, 4, 8, w);
        });
        _thigh = lib.Get("ch2_thigh", b => Shapes.CapsuleBetween(b[M.Fabric], Vector3.Zero, new Vector3(0, -0.43f, 0), 0.067f, 6, 10, w));
        _shin = lib.Get("ch2_shin", b => Shapes.CapsuleBetween(b[M.Fabric], Vector3.Zero, new Vector3(0, -0.42f, 0), 0.054f, 6, 10, w));
        _shoe = lib.Get("ch2_shoe", b =>
        {
            Shapes.RoundedBox(b[M.Rubber], Matrix4x4.CreateTranslation(0, 0.012f, -0.045f), new Vector3(0.1f, 0.085f, 0.25f), 0.035f, 2, w);
            // Taban: tint ile carpilinca koyulasan gri
            Shapes.RoundedBox(b[M.Rubber], Matrix4x4.CreateTranslation(0, -0.03f, -0.045f), new Vector3(0.106f, 0.018f, 0.26f), 0.008f, 1, new Color(90, 90, 90, 255));
        });
        _eyelids = lib.Get("ch3_lids", b =>
        {
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Sphere(b[M.Skin], Matrix4x4.CreateScale(1.05f, 0.85f, 0.62f) * Matrix4x4.CreateTranslation(0, 0, 0.005f) * HeadShape.Frame(s * 0.038f, 0.122f), 0.0185f, 5, 8, w);
            }
        });
        // Agizlar yuzeye oturur: notr, gulumseme, asik, konusurken acik
        _mouths[0] = lib.Get("ch3_mouth0", b => Shapes.Tube(b[M.Skin], Face([(-0.019f, 0.046f), (0, 0.045f), (0.019f, 0.046f)], 0.0025f), 0.0045f, 6, w));
        _mouths[1] = lib.Get("ch3_mouth1", b => Shapes.Tube(b[M.Skin], Face([(-0.025f, 0.052f), (-0.013f, 0.043f), (0, 0.041f), (0.013f, 0.043f), (0.025f, 0.052f)], 0.0025f), 0.0045f, 6, w));
        _mouths[2] = lib.Get("ch3_mouth2", b => Shapes.Tube(b[M.Skin], Face([(-0.022f, 0.039f), (-0.011f, 0.045f), (0, 0.047f), (0.011f, 0.045f), (0.022f, 0.039f)], 0.0025f), 0.0045f, 6, w));
        _mouths[3] = lib.Get("ch3_mouth3", b => Shapes.Sphere(b[M.Skin], Matrix4x4.CreateScale(1.3f, 0.85f, 0.55f) * Matrix4x4.CreateTranslation(0, 0, -0.002f) * HeadShape.Frame(0, 0.045f), 0.0135f, 5, 8, w));
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
                p.LeftShoulder = s * 0.5f;
                p.RightShoulder = -s * 0.5f;
                p.LeftElbow = -0.3f;
                p.RightElbow = -0.3f;
                p.LeftHip = -s * 0.48f;
                p.RightHip = s * 0.48f;
                p.LeftKnee = MathF.Max(0, -MathF.Cos(phase)) * 0.75f;
                p.RightKnee = MathF.Max(0, MathF.Cos(phase)) * 0.75f;
                p.Bob = MathF.Abs(MathF.Cos(phase)) * 0.03f;
                p.Spine = 0.04f;
                p.HeadYaw = MathF.Sin(phase * 0.5f) * 0.04f;
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
                // Nefes ve etrafa bakinma
                p.Bob = MathF.Sin(t * 1.7f + seed) * 0.006f;
                p.LeftShoulder = MathF.Sin(t * 1.1f + seed) * 0.03f;
                p.RightShoulder = -MathF.Sin(t * 1.1f + seed) * 0.03f;
                p.LeftElbow = -0.12f;
                p.RightElbow = -0.12f;
                p.HeadYaw = MathF.Sin(t * 0.37f + seed % 7) * 0.25f;
                break;
        }

        return p;
    }

    /// <summary>
    /// Karakteri ciz. holds: 0 yok, 1 tabak, 2 paket, 3 kasik.
    /// lod: 0 tam, 1 orta (yuz ifadesi yok), 2 uzak (el/ayak yok).
    /// </summary>
    public void Draw(Vector3 pos, float yaw, Appearance a, Pose p, int holds = 0, bool castShadow = true, float scaleBias = 1f, Face face = default, int lod = 0)
    {
        var (headModel, torsoModel) = Baked(a);
        var flags = castShadow ? DrawFlags.None : DrawFlags.NoShadow;
        // Cocukta bas buyuk, bacak ve govde kisa
        var legS = a.Child ? 0.8f : 1f;
        var torsoS = a.Child ? 0.84f : 1f;
        var headS = a.Child ? 1.14f : 1f;
        var s = a.Height * scaleBias;
        var wdt = a.Width;
        var root = Matrix4x4.CreateScale(wdt, 1, wdt) * Matrix4x4.CreateScale(s) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(pos);
        var hipY = 0.95f * legS - p.Sit * 0.45f * legS + p.Bob;
        Matrix4x4 J(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

        // Bacaklar
        foreach (var side in new[] { -1f, 1f })
        {
            // Poz kurali: negatif kalca/omuz = one dogru; pozitif diz = bukulme.
            var hipA = -(side < 0 ? p.LeftHip : p.RightHip);
            var kneeA = -(side < 0 ? p.LeftKnee : p.RightKnee);
            var hip = Matrix4x4.CreateScale(legS) * Matrix4x4.CreateRotationX(hipA) * J(side * 0.1f, hipY, 0) * root;
            _r.Submit(_thigh, hip, a.Pants, flags);
            var kneeM = Matrix4x4.CreateRotationX(kneeA) * J(0, -0.44f, 0) * hip;
            _r.Submit(_shin, kneeM, a.Pants, flags);
            if (lod < 2)
            {
                var foot = Matrix4x4.CreateRotationX(-(hipA + kneeA)) * J(0, -0.47f, 0) * kneeM;
                _r.Submit(_shoe, foot, a.Shoes, flags);
            }
        }

        // Govde (yasli hafif one egik)
        var spine = p.Spine + (a.Elderly ? 0.1f : 0f);
        var torso = Matrix4x4.CreateScale(torsoS) * Matrix4x4.CreateRotationX(spine) * J(0, hipY + 0.62f * torsoS, 0) * root;
        _r.Submit(torsoModel, torso, Color.White, flags);

        // Bas
        var headPitch = p.Head + (a.Elderly ? -0.06f : 0f);
        var head = Matrix4x4.CreateScale(headS / torsoS) * Matrix4x4.CreateRotationX(headPitch) * Matrix4x4.CreateRotationY(p.HeadYaw) * J(0, 0.085f, 0) * torso;
        _r.Submit(headModel, head, Color.White, flags);
        if (lod == 0)
        {
            // Goz kirpma: her ~3,7 sn'de 0,12 sn kapali
            var tb = face.Time + (face.Seed % 97) * 0.37f;
            if (tb % 3.7f < 0.12f)
            {
                _r.Submit(_eyelids, head, a.Skin, DrawFlags.NoShadow);
            }

            var talkOpen = face.Talking && MathF.Sin(face.Time * 17f + face.Seed) > 0.1f;
            _r.Submit(talkOpen ? _mouths[3] : _mouths[Math.Min((int)face.Expression, 2)], head,
                talkOpen ? Gfx.Hex(0x4A1C1C) : Gfx.Hex(0x9E5A4A), DrawFlags.NoShadow);
        }

        // Kollar
        var sleeve = a.LongSleeves ? a.Shirt : a.Skin;
        foreach (var side in new[] { -1f, 1f })
        {
            var sh = side < 0 ? p.LeftShoulder : p.RightShoulder;
            var shSide = side < 0 ? p.LeftShoulderSide : p.RightShoulderSide;
            var el = side < 0 ? p.LeftElbow : p.RightElbow;
            var shoulder = Matrix4x4.CreateRotationZ(side * (0.08f + shSide)) * Matrix4x4.CreateRotationX(-sh) * J(side * 0.215f, -0.045f, 0) * torso;
            _r.Submit(_upperArm, shoulder, a.Shirt, flags);
            var elbow = Matrix4x4.CreateRotationX(-el) * J(0, -0.29f, 0) * shoulder;
            _r.Submit(_foreArm, elbow, sleeve, flags);
            if (lod < 2)
            {
                var hand = J(0, -0.265f, 0) * elbow;
                _r.Submit(_hand, hand, a.Skin, flags);
            }
        }

        // Elde tasinan
        if (holds is 1 or 2)
        {
            var chest = J(0, -0.42f, -0.3f) * torso;
            _r.Submit(holds == 1 ? _plate : _pkg, chest, Color.White, flags);
        }
        else if (holds == 3)
        {
            var sh = p.RightShoulder;
            var shoulder = Matrix4x4.CreateRotationX(-sh) * J(0.215f, -0.045f, 0) * torso;
            var hand = J(0, -0.26f, 0) * Matrix4x4.CreateRotationX(-p.RightElbow) * J(0, -0.29f, 0) * shoulder;
            _r.Submit(_spoon, Matrix4x4.CreateRotationX(-1.2f) * hand, Color.White, flags);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Gorunuse ozel pisirilmis bas ve govde
    // ═══════════════════════════════════════════════════════════════════

    private (RenderModel Head, RenderModel Torso) Baked(Appearance a)
    {
        var key = a.Key;
        if (_cache.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            return (node.Value.Head, node.Value.Torso);
        }

        var hb = new ModelBuilder();
        BuildHead(hb, a);
        var tb = new ModelBuilder();
        BuildTorso(tb, a);
        var entry = (key, hb.Build(), tb.Build());
        var n = _lru.AddFirst(entry);
        _cache[key] = n;
        if (_lru.Count > CacheCapacity)
        {
            var last = _lru.Last!;
            _lru.RemoveLast();
            _cache.Remove(last.Value.Key);
            _r.Release(last.Value.Head);
            _r.Release(last.Value.Torso);
        }

        return (entry.Item2, entry.Item3);
    }

    /// <summary>Yuz uzerindeki (x, y) noktalarini yuzeye oturtur (egri boyunca boru icin).</summary>
    private static Vector3[] Face((float X, float Y)[] pts, float lift)
    {
        var r = new Vector3[pts.Length];
        for (var i = 0; i < pts.Length; i++)
        {
            r[i] = HeadShape.OnFace(pts[i].X, pts[i].Y, lift).Position;
        }

        return r;
    }

    private static Color Mul(Color c, float k) => new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), (byte)255);

    /// <summary>Bas uzayi: boyun tabani y=0 (omuz cizgisinin 0,1 m ustu), yuz -Z'ye bakar.</summary>
    public static void BuildHead(ModelBuilder b, Appearance a)
    {
        var m = b[M.Skin];
        var skin = a.Skin;
        var hair = a.Hair;
        var brow = Mul(a.Hair, a.HairStyle == HairStyle.Bald ? 0.8f : 0.85f);
        // Boyun, kafatasi, cene
        Shapes.CapsuleBetween(m, new Vector3(0, -0.11f, 0.005f), new Vector3(0, 0.02f, 0.005f), 0.05f, 6, 12, Mul(skin, 0.95f));
        HeadShape.Mesh(m, Matrix4x4.Identity, skin);
        // Kulaklar ve burun
        foreach (var s in new[] { -1f, 1f })
        {
            Shapes.Sphere(m, Matrix4x4.CreateScale(0.42f, 1f, 0.75f) * Matrix4x4.CreateTranslation(s * 0.104f, 0.098f, 0.01f), 0.028f, 5, 8, Mul(skin, 0.96f));
        }

        Shapes.CapsuleBetween(m, HeadShape.OnFace(0, 0.112f, -0.006f).Position, HeadShape.OnFace(0, 0.089f, 0.006f).Position, 0.012f, 5, 8, Mul(skin, 0.97f));
        // Gozler: ak, iris, bebek yuzeye gomulu (yalnizca on kubbesi gorunur); kaslar yuzeyi izler
        foreach (var s in new[] { -1f, 1f })
        {
            var eye = HeadShape.Frame(s * 0.038f, 0.122f);
            Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 0.82f, 0.45f) * Matrix4x4.CreateTranslation(0, 0, 0.0049f) * eye, 0.0175f, 6, 12, Gfx.Hex(0xF4F1EC));
            Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 1f, 0.42f) * Matrix4x4.CreateTranslation(0, 0, -0.0003f) * eye, 0.0095f, 5, 10, a.Eyes);
            Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 1f, 0.5f) * Matrix4x4.CreateTranslation(0, 0, -0.0025f) * eye, 0.0048f, 3, 8, Gfx.Hex(0x0E0B09));
            Shapes.Tube(m, [HeadShape.OnFace(s * 0.019f, 0.149f, 0.003f).Position, HeadShape.OnFace(s * 0.039f, 0.155f, 0.0035f).Position, HeadShape.OnFace(s * 0.059f, 0.15f, 0.003f).Position], 0.0055f, 5, brow);
        }

        // Sac
        var hatCovers = a.Hat is HatKind.Headscarf or HatKind.HardHat or HatKind.Beanie or HatKind.Toque or HatKind.UniformCap;
        if (!hatCovers || a.Hat == HatKind.UniformCap)
        {
            switch (a.HairStyle)
            {
                case HairStyle.Short:
                    Shapes.Sphere(m, Matrix4x4.CreateScale(0.97f, 0.78f, 1.04f) * Matrix4x4.CreateTranslation(0, 0.142f, 0.014f), 0.118f, 8, 16, hair);
                    break;
                case HairStyle.Buzz:
                    Shapes.Sphere(m, Matrix4x4.CreateScale(0.95f, 0.8f, 1.02f) * Matrix4x4.CreateTranslation(0, 0.132f, 0.01f), 0.116f, 8, 16, Mul(hair, 0.9f));
                    break;
                case HairStyle.Long:
                    Shapes.Sphere(m, Matrix4x4.CreateScale(0.99f, 0.8f, 1.05f) * Matrix4x4.CreateTranslation(0, 0.142f, 0.014f), 0.12f, 8, 16, hair);
                    Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(0, 0.045f, 0.07f), new Vector3(0.21f, 0.2f, 0.07f), 0.03f, 2, hair);
                    foreach (var s in new[] { -1f, 1f })
                    {
                        Shapes.CapsuleBetween(m, new Vector3(s * 0.098f, 0.135f, -0.01f), new Vector3(s * 0.095f, 0.0f, 0.02f), 0.024f, 5, 8, hair);
                    }

                    break;
                case HairStyle.Bun:
                    Shapes.Sphere(m, Matrix4x4.CreateScale(0.97f, 0.8f, 1.04f) * Matrix4x4.CreateTranslation(0, 0.142f, 0.014f), 0.119f, 8, 16, hair);
                    Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.19f, 0.085f), 0.045f, 6, 10, hair);
                    break;
                case HairStyle.Curly:
                    Shapes.Sphere(m, Matrix4x4.CreateScale(0.95f, 0.78f, 1.02f) * Matrix4x4.CreateTranslation(0, 0.14f, 0.012f), 0.114f, 6, 12, hair);
                    for (var i = 0; i < 16; i++)
                    {
                        var ang = i * 2.39996f;
                        var y = 0.12f + (i % 4) * 0.025f;
                        var r = 0.11f - (i % 4) * 0.012f;
                        var cx = MathF.Cos(ang) * r;
                        var cz = MathF.Sin(ang) * r + 0.015f;
                        if (cz < -0.06f && y < 0.17f)
                        {
                            continue; // alin acik
                        }

                        Shapes.Sphere(m, Matrix4x4.CreateTranslation(cx, y, cz), 0.032f, 4, 7, Mul(hair, 0.9f + (i % 3) * 0.06f));
                    }

                    break;
                case HairStyle.Bald:
                    Shapes.Arc(m, Matrix4x4.CreateTranslation(0, 0.088f, 0.004f), 0.106f, 0.02f, 0.12f * MathF.PI, 0.76f * MathF.PI, 14, 6, hair);
                    break;
            }
        }

        // Yuz kili
        switch (a.Facial)
        {
            case FacialHair.Mustache:
                Shapes.Tube(m, Face([(-0.034f, 0.064f), (0, 0.07f), (0.034f, 0.064f)], 0.006f), 0.009f, 6, hair, radii: [0.006f, 0.011f, 0.006f]);
                break;
            case FacialHair.Pala:
                Shapes.Tube(m, Face([(-0.045f, 0.041f), (-0.031f, 0.064f), (0, 0.071f), (0.031f, 0.064f), (0.045f, 0.041f)], 0.006f),
                    0.01f, 6, hair, radii: [0.005f, 0.009f, 0.012f, 0.009f, 0.005f]);
                break;
            case FacialHair.Beard:
                Shapes.Sphere(m, Matrix4x4.CreateScale(0.98f, 0.66f, 0.9f) * Matrix4x4.CreateTranslation(0, 0.038f, -0.022f), 0.094f, 8, 14, hair);
                Shapes.Tube(m, Face([(-0.034f, 0.064f), (0, 0.07f), (0.034f, 0.064f)], 0.007f), 0.009f, 6, hair, radii: [0.006f, 0.01f, 0.006f]);
                break;
        }

        // Sapkalar (sapka rengi; kumas)
        var hc = a.HatColor;
        switch (a.Hat)
        {
            case HatKind.Kasket:
                Shapes.Sphere(m, Matrix4x4.CreateScale(1.03f, 0.55f, 1.07f) * Matrix4x4.CreateTranslation(0, 0.168f, 0.008f), 0.123f, 7, 16, hc);
                Shapes.Extrude(m, Matrix4x4.CreateRotationX(-MathF.PI / 2 + 0.16f) * Matrix4x4.CreateTranslation(0, 0.165f, -0.1f),
                    [new(-0.085f, 0), new(0.085f, 0), new(0.065f, -0.07f), new(0, -0.085f), new(-0.065f, -0.07f)], 0.008f, Mul(hc, 0.85f));
                Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.236f, 0.01f), 0.01f, 3, 6, Mul(hc, 0.8f));
                break;
            case HatKind.Fedora:
                Shapes.Cylinder(m, Matrix4x4.CreateScale(1f, 1f, 0.92f) * Matrix4x4.CreateTranslation(0, 0.18f, 0.006f), 0.185f, 0.012f, 20, hc);
                Shapes.Lathe(m, Matrix4x4.CreateTranslation(0, 0.18f, 0.006f), [new(0.118f, 0), new(0.112f, 0.08f), new(0.09f, 0.11f), new(0, 0.1f)], 18, hc, smoothProfile: true);
                Shapes.Torus(m, Matrix4x4.CreateTranslation(0, 0.2f, 0.006f), 0.117f, 0.012f, 18, 4, Mul(hc, 0.5f));
                break;
            case HatKind.HardHat:
                Shapes.Sphere(m, Matrix4x4.CreateScale(1.06f, 0.8f, 1.12f) * Matrix4x4.CreateTranslation(0, 0.152f, 0.004f), 0.131f, 8, 16, hc);
                Shapes.Cylinder(m, Matrix4x4.CreateScale(1f, 1f, 1.1f) * Matrix4x4.CreateTranslation(0, 0.148f, 0.004f), 0.152f, 0.01f, 20, hc);
                Shapes.Tube(m, [new(0, 0.15f, -0.14f), new(0, 0.262f, -0.03f), new(0, 0.258f, 0.06f), new(0, 0.15f, 0.15f)], 0.012f, 5, Mul(hc, 0.9f));
                break;
            case HatKind.Beanie:
                Shapes.Sphere(m, Matrix4x4.CreateScale(1.02f, 0.92f, 1.06f) * Matrix4x4.CreateTranslation(0, 0.14f, 0.008f), 0.124f, 8, 16, hc);
                Shapes.Torus(m, Matrix4x4.CreateScale(1f, 1f, 1.04f) * Matrix4x4.CreateTranslation(0, 0.13f, 0.008f), 0.121f, 0.02f, 18, 5, Mul(hc, 0.85f));
                Shapes.Sphere(m, Matrix4x4.CreateTranslation(0, 0.262f, 0.01f), 0.028f, 4, 7, Mul(hc, 1.1f));
                break;
            case HatKind.Toque:
                Shapes.Cylinder(m, Matrix4x4.CreateTranslation(0, 0.15f, 0.008f), 0.118f, 0.07f, 18, Gfx.Hex(0xF7F7F7));
                foreach (var (x, z) in new[] { (0f, 0f), (0.055f, 0.02f), (-0.055f, 0.02f), (0f, 0.06f), (0f, -0.05f) })
                {
                    Shapes.Sphere(m, Matrix4x4.CreateTranslation(x, 0.26f, z + 0.008f), 0.075f, 6, 10, Gfx.Hex(0xFAFAFA));
                }

                break;
            case HatKind.UniformCap:
                Shapes.Cylinder(m, Matrix4x4.CreateScale(1f, 1f, 1.04f) * Matrix4x4.CreateTranslation(0, 0.15f, 0.006f), 0.12f, 0.06f, 18, hc);
                Shapes.Cylinder(m, Matrix4x4.CreateScale(1f, 1f, 1.1f) * Matrix4x4.CreateTranslation(0, 0.205f, 0.008f), 0.13f, 0.02f, 18, hc);
                Shapes.Extrude(m, Matrix4x4.CreateRotationX(-MathF.PI / 2 + 0.25f) * Matrix4x4.CreateTranslation(0, 0.152f, -0.11f),
                    [new(-0.07f, 0), new(0.07f, 0), new(0.05f, -0.05f), new(-0.05f, -0.05f)], 0.006f, Gfx.Hex(0x111111));
                Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 1f, 0.4f) * Matrix4x4.CreateTranslation(0, 0.19f, -0.126f), 0.016f, 4, 8, Gfx.Hex(0xF1C40F));
                break;
            case HatKind.Headscarf:
            {
                // Yuz acikligi -Z yonunde (aci 3pi/2)
                var front = 1.5f * MathF.PI;
                List<Vector2> profile = [new(0.088f, -0.07f), new(0.114f, -0.005f), new(0.126f, 0.06f), new(0.129f, 0.12f), new(0.12f, 0.18f), new(0.088f, 0.215f), new(0.042f, 0.233f), new(0, 0.237f)];
                Shapes.Lathe(m, Matrix4x4.CreateTranslation(0, 0, 0.006f), profile, 22, hc, smoothProfile: true, startAngle: front + 0.62f, sweep: MathF.Tau - 1.24f);
                Shapes.Lathe(m, Matrix4x4.CreateTranslation(0, 0, 0.006f), profile.Skip(4).ToList(), 8, hc, smoothProfile: true, startAngle: front - 0.66f, sweep: 1.32f);
                // Yuz cevresi kenari
                Shapes.Arc(m, Matrix4x4.CreateScale(0.95f, 1f, 1f) * Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.1f, -0.098f), 0.085f, 0.012f, 0.1f * MathF.PI, 0.8f * MathF.PI, 14, 5, Mul(hc, 0.9f));
                Shapes.Frustum(m, Matrix4x4.CreateTranslation(0, -0.13f, 0.01f), 0.17f, 0.095f, 0.09f, 18, Mul(hc, 0.95f), false, false);
                break;
            }
        }

        if (a.Glasses)
        {
            var frame = Gfx.Hex(0x1C1C1C);
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Torus(m, Matrix4x4.CreateScale(1.15f, 1f, 0.9f) * Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0, -0.012f) * HeadShape.Frame(s * 0.039f, 0.122f), 0.02f, 0.0032f, 14, 4, frame);
                Shapes.Tube(m, [HeadShape.OnFace(s * 0.062f, 0.125f, 0.01f).Position, new(s * 0.098f, 0.13f, -0.06f), new(s * 0.104f, 0.115f, 0.0f)], 0.003f, 4, frame);
            }

            Shapes.Tube(m, Face([(-0.017f, 0.125f), (0, 0.128f), (0.017f, 0.125f)], 0.011f), 0.003f, 4, frame);
        }
    }

    /// <summary>Govde uzayi: omuz cizgisi y=0, kalca altı y≈-0,66; on -Z.</summary>
    public static void BuildTorso(ModelBuilder b, Appearance a)
    {
        var m = b[M.Fabric];
        var shirt = a.Shirt;
        var start = m.VertexCount;
        // Govde: Z boyunca kesitler, sonra X etrafinda donup Y eksenine oturur.
        var toY = Matrix4x4.CreateRotationX(-MathF.PI / 2);
        var waist = a.Elderly ? 0.165f : 0.155f;
        Shapes.Loft(m, toY,
        [
            new Shapes.LoftSection(-0.68f, 0f, 0.164f, 0.104f, 0.08f),
            new Shapes.LoftSection(-0.5f, 0f, 0.168f, 0.104f, 0.09f),
            new Shapes.LoftSection(-0.36f, 0.004f, waist, 0.1f, 0.085f),
            new Shapes.LoftSection(-0.18f, 0.012f, 0.186f, 0.118f, 0.1f),
            new Shapes.LoftSection(-0.06f, 0.004f, 0.205f, 0.11f, 0.095f),
            new Shapes.LoftSection(0.0f, 0f, 0.17f, 0.092f, 0.082f),
            new Shapes.LoftSection(0.035f, 0f, 0.07f, 0.06f, 0.05f),
        ], 4, shirt);
        // Kalca bolgesi pantolon renginde
        for (var i = start; i < m.VertexCount; i++)
        {
            if (m.Positions[i].Y < -0.505f)
            {
                m.Colors[i] = a.Pants;
            }
        }

        // Kemer ve yaka
        var belt = a.Uniform ? Gfx.Hex(0x111111) : Gfx.Hex(0x2B1D14);
        Shapes.Torus(m, Matrix4x4.CreateScale(1.66f, 1f, 1.04f) * Matrix4x4.CreateTranslation(0, -0.5f, 0), 0.102f, 0.012f, 20, 4, belt);
        Shapes.Torus(m, Matrix4x4.CreateScale(1f, 0.6f, 0.85f) * Matrix4x4.CreateTranslation(0, 0.02f, 0), 0.075f, 0.014f, 16, 5, Mul(shirt, 0.88f));

        if (a.Tie)
        {
            Shapes.Extrude(m, Matrix4x4.CreateTranslation(0, 0, -0.133f),
                [new(-0.012f, 0.01f), new(0.012f, 0.01f), new(0.02f, -0.02f), new(0.03f, -0.33f), new(0, -0.37f), new(-0.03f, -0.33f), new(-0.02f, -0.02f)], 0.008f, Gfx.Hex(0x922B21));
        }

        if (a.Uniform)
        {
            // Rozet ve apoletler
            Shapes.Sphere(m, Matrix4x4.CreateScale(1f, 1.2f, 0.35f) * Matrix4x4.CreateTranslation(-0.09f, -0.13f, -0.128f), 0.022f, 4, 8, Gfx.Hex(0xF1C40F));
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(s * 0.15f, -0.01f, 0), new Vector3(0.09f, 0.02f, 0.11f), 0.008f, 1, Mul(shirt, 0.7f));
            }
        }

        if (a.Apron)
        {
            var ac = a.ApronColor;
            var bib = new List<Vector2> { new(-0.1f, -0.04f), new(0.1f, -0.04f), new(0.12f, -0.3f), new(-0.12f, -0.3f) };
            var skirt = new List<Vector2> { new(-0.17f, -0.33f), new(0.17f, -0.33f), new(0.185f, -0.83f), new(-0.185f, -0.83f) };
            Shapes.Extrude(m, Matrix4x4.CreateTranslation(0, 0, -0.132f), bib, 0.008f, ac);
            Shapes.Extrude(m, Matrix4x4.CreateTranslation(0, 0, -0.114f), skirt, 0.008f, ac);
            if (a.ApronStyle == 1)
            {
                for (var x = -0.15f; x <= 0.151f; x += 0.06f)
                {
                    Shapes.Box(m, Matrix4x4.CreateTranslation(x, -0.58f, -0.1195f), new Vector3(0.018f, 0.48f, 0.002f), Mul(ac, 0.65f));
                }
            }
            else if (a.ApronStyle == 2)
            {
                for (var y = -0.38f; y > -0.8f; y -= 0.08f)
                {
                    Shapes.Box(m, Matrix4x4.CreateTranslation(0, y, -0.1195f), new Vector3(0.35f, 0.018f, 0.002f), Mul(ac, 0.7f));
                }

                for (var x = -0.14f; x <= 0.141f; x += 0.07f)
                {
                    Shapes.Box(m, Matrix4x4.CreateTranslation(x, -0.58f, -0.1197f), new Vector3(0.018f, 0.48f, 0.002f), Mul(ac, 0.7f));
                }
            }

            // Cep ve askilar, bel bagi
            Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(0, -0.5f, -0.121f), new Vector3(0.16f, 0.08f, 0.006f), 0.004f, 1, Mul(ac, 0.88f));
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Tube(m, [new(s * 0.09f, -0.045f, -0.134f), new(s * 0.1f, 0.025f, -0.08f), new(s * 0.1f, 0.03f, 0.05f), new(s * 0.08f, -0.1f, 0.112f)], 0.009f, 4, ac);
            }

            Shapes.Torus(m, Matrix4x4.CreateScale(1.68f, 1f, 1.08f) * Matrix4x4.CreateTranslation(0, -0.36f, 0.002f), 0.098f, 0.01f, 20, 4, ac);
        }

        if (a.Backpack)
        {
            var bc = a.HatColor;
            Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(0, -0.27f, 0.175f), new Vector3(0.28f, 0.36f, 0.13f), 0.05f, 2, bc);
            Shapes.RoundedBox(m, Matrix4x4.CreateTranslation(0, -0.36f, 0.245f), new Vector3(0.2f, 0.14f, 0.04f), 0.02f, 1, Mul(bc, 0.85f));
            foreach (var s in new[] { -1f, 1f })
            {
                Shapes.Tube(m, [new(s * 0.08f, -0.42f, 0.11f), new(s * 0.1f, -0.1f, -0.05f), new(s * 0.1f, 0.03f, -0.02f), new(s * 0.09f, -0.05f, 0.11f)], 0.012f, 4, Mul(bc, 0.7f));
            }
        }

        // Hafif sahte ortam kapatmasi: alt kisimlar biraz koyu
        Shapes.ShadeByHeight(m, start, -0.7f, 0.0f, 0.18f);
    }
}
