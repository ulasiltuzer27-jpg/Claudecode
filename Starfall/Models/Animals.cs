using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

/// <summary>
/// Adalilar. Her biri ayni sozlesmeye uyar: Root (ayak hizasinda), Head (oyuncuya bakmak
/// icin pivot), Eyes (goz kirpma), Update(dt). Turlerin kendine has bir "huyu" var: baykus
/// kanat cirpar, kurbaganin bogazi sisar, tavsanin kulagi seyirir, kunduz kuyrugunu vurur...
/// </summary>
public sealed class AnimalModel
{
    public readonly string Kind;
    public readonly Node Root = new();
    public readonly Node Body = new();
    public readonly Node Head = new();
    public Node? Eyes;
    public Vector3? LookTarget;
    public float Talking;

    // turlere ozel parcalar
    private Node[]? _wings, _ears;
    private Node? _throat, _tailPivot, _flippers, _scarfEnd;
    private readonly List<Node> _extra = new();

    private float _t, _blink, _quirk, _quirkT, _lookYaw;
    private readonly Random _r;

    public static readonly string[] Kinds = { "owl", "hedgehog", "frog", "bear", "rabbit", "beaver" };

    private static Node M(Geo g) => FoxModel.M(g);
    private static Node M(params Geo[] parts) => FoxModel.M(Merge(parts));
    private static Node Pivot(float x, float y, float z) => new() { Position = V(x, y, z) };

    public static Node BigEyes(float spacing, float y, float z, float r, string iris = P.Eye)
    {
        var parts = new List<Geo>();
        foreach (var s in new[] { -1f, 1f })
        {
            parts.Add(Sphere("#ffffff", r, V(s * spacing, 0, 0), V(1, 1, 0.7f), 12, 10));
            parts.Add(Sphere(iris, r * 0.62f, V(s * spacing, 0, r * 0.45f), V(1, 1.1f, 0.6f), 10, 8));
            parts.Add(Sphere("#ffffff", r * 0.2f, V(s * spacing - r * 0.2f, r * 0.25f, r * 0.78f), null, 6, 4));
        }
        var eyes = new Node { Position = V(0, y, z) };
        eyes.Add(M(Merge(parts)));
        return eyes;
    }

    /// <summary>(0,1,0) eksenini dir'e ceviren donusum (three.js setFromUnitVectors).</summary>
    public static Matrix4x4 AlignUp(Vector3 dir)
    {
        var from = Vector3.UnitY;
        float d = Vector3.Dot(from, dir);
        Quaternion q;
        if (d < -0.999999f) q = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathX.Pi);
        else
        {
            var c = Vector3.Cross(from, dir);
            q = Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1 + d));
        }
        return Matrix4x4.CreateFromQuaternion(q);
    }

    public AnimalModel(string kind, int seed = 0)
    {
        Kind = kind;
        _r = new Random(seed * 31 + kind.Length * 7 + kind[0]);
        _t = (float)_r.NextDouble() * 10;
        _blink = 1 + (float)_r.NextDouble() * 3;
        _quirk = 3 + (float)_r.NextDouble() * 4;
        Root.Add(Body);
        switch (kind)
        {
            case "owl": BuildOwl(); break;
            case "hedgehog": BuildHedgehog(); break;
            case "frog": BuildFrog(); break;
            case "bear": BuildBear(); break;
            case "rabbit": BuildRabbit(); break;
            case "beaver": BuildBeaver(); break;
            case "penguin": BuildPenguin(); break;
            case "seal": BuildSeal(); break;
            case "goat": BuildGoat(); break;
            case "mole": BuildMole(); break;
            case "cat": BuildCat(); break;
            case "polarbear": BuildPolarBear(); break;
            default: throw new ArgumentException("bilinmeyen adali turu: " + kind);
        }
    }

    public void Update(float dt)
    {
        _t += dt;
        float t = _t;
        _blink -= dt;
        if (_blink < 0) _blink = 2 + (float)_r.NextDouble() * 4;
        if (Eyes != null) Eyes.Scale = new Vector3(1, _blink < 0.12f ? 0.1f : 1, 1);

        _quirk -= dt;
        if (_quirk < 0)
        {
            _quirk = 4 + (float)_r.NextDouble() * 5;
            _quirkT = 1;
        }
        _quirkT = MathF.Max(0, _quirkT - dt * 1.2f);

        float breathe = MathF.Sin(t * 2) * 0.015f;
        Body.Scale = new Vector3(1 + breathe, 1 - breathe * 0.6f + Talking * MathF.Abs(MathF.Sin(t * 14)) * 0.025f, 1 + breathe);

        float target = 0, pitch = 0;
        if (LookTarget is { } lt)
        {
            var local = Root.WorldToLocal(lt);
            target = Math.Clamp(MathF.Atan2(local.X, local.Z), -1.1f, 1.1f);
            pitch = Math.Clamp(-MathF.Atan2(local.Y - 0.6f, MathX.Hypot(local.X, local.Z)) * 0.5f, -0.3f, 0.3f);
        }
        float k = MathF.Min(1, dt * 4);
        _lookYaw += (target - _lookYaw) * k;
        var hr = Head.Rotation;
        hr.Y = _lookYaw;
        hr.X += (pitch - hr.X) * k;
        Head.Rotation = hr;

        Quirk(t, _quirkT);
    }

    private void Quirk(float t, float q)
    {
        switch (Kind)
        {
            case "owl":
            {
                float flap = q > 0 ? MathF.Sin(q * 30) * 0.6f * q : 0;
                _wings![0].Rotation = V(0, 0, -0.1f - MathF.Abs(flap));
                _wings[1].Rotation = V(0, 0, 0.1f + MathF.Abs(flap));
                Head.Rotation = Head.Rotation with { Z = MathF.Sin(t * 0.7f) * 0.1f };
                break;
            }
            case "hedgehog":
                Body.Position = V(0, MathF.Abs(MathF.Sin(q * 12)) * 0.06f * q, 0);
                break;
            case "frog":
                _throat!.Scale = new Vector3(0.4f + MathF.Max(0, MathF.Sin(t * 3)) * 0.5f);
                Body.Position = V(0, q > 0 ? MathF.Abs(MathF.Sin(q * MathX.Pi)) * 0.25f : 0, 0);
                break;
            case "bear":
                Body.Rotation = V(0, 0, MathF.Sin(t * 0.8f) * 0.04f);
                break;
            case "rabbit":
            {
                float tw = q > 0 ? MathF.Sin(q * 25) * 0.4f * q : 0;
                float rx = -0.1f + MathF.Sin(t * 1.1f) * 0.06f;
                _ears![0].Rotation = V(rx, 0, 0.15f + tw);
                _ears[1].Rotation = V(rx, 0, -0.15f - MathF.Sin(t * 1.3f) * 0.05f);
                break;
            }
            case "beaver":
                _tailPivot!.Rotation = V(q > 0 ? -MathF.Abs(MathF.Sin(q * 18)) * 0.6f * q : MathF.Sin(t * 1.5f) * 0.05f, 0, 0);
                break;
            case "penguin":
            {
                // kanatcik cirpma + sallanarak durma
                float flap = q > 0 ? MathF.Abs(MathF.Sin(q * 22)) * 0.7f * q : 0;
                _wings![0].Rotation = V(0, 0, -0.15f - flap);
                _wings[1].Rotation = V(0, 0, 0.15f + flap);
                Body.Rotation = V(0, 0, MathF.Sin(t * 2.2f) * 0.06f);
                break;
            }
            case "seal":
                _flippers!.Rotation = V(q > 0 ? MathF.Sin(q * 20) * 0.5f * q : MathF.Sin(t * 1.2f) * 0.08f, 0, 0);
                Body.Position = V(0, q > 0 ? MathF.Abs(MathF.Sin(q * MathX.Pi * 2)) * 0.06f : 0, 0);
                break;
            case "goat":
                Head.Rotation = Head.Rotation with { Z = q > 0 ? MathF.Sin(q * 16) * 0.12f * q : 0 };
                _ears![0].Rotation = V(0, 0, 1.2f + MathF.Sin(t * 1.7f) * 0.08f);
                _ears[1].Rotation = V(0, 0, -1.2f - MathF.Sin(t * 1.5f) * 0.08f);
                break;
            case "mole":
                // burnunu oynatip etrafi koklar
                _extra[0].Scale = new Vector3(1 + MathF.Abs(MathF.Sin(t * 9)) * 0.12f * (q > 0 ? 1 : 0.3f));
                Body.Rotation = V(0, q > 0 ? MathF.Sin(q * 10) * 0.25f * q : 0, 0);
                break;
            case "cat":
                _tailPivot!.Rotation = V(-0.4f + MathF.Sin(t * 1.4f) * 0.1f, MathF.Sin(t * 0.9f) * 0.5f, 0);
                _ears![0].Rotation = V(0, 0, 0.1f + (q > 0 ? MathF.Sin(q * 30) * 0.3f * q : 0));
                break;
            case "polarbear":
                Body.Rotation = V(0, 0, MathF.Sin(t * 0.7f) * 0.035f);
                if (_scarfEnd != null) _scarfEnd.Rotation = V(MathF.Sin(t * 2.1f) * 0.15f, 0, 0.2f);
                break;
        }
    }

    // ---------------------------------------------------------------- Ada 1

    private void BuildOwl()
    {
        var parts = new List<Geo>
        {
            Blob(P.Owl, 0.36f, 0.44f, 0.34f, 2).Translate(0, 0.5f, 0),
            Blob(P.OwlLight, 0.26f, 0.32f, 0.2f, 2).Translate(0, 0.45f, 0.16f),
        };
        float[] xs = { -0.1f, 0, 0.1f };
        for (int i = 0; i < 3; i++)
            parts.Add(Cone(P.OwlDark, 0.035f, 0.06f, V(xs[i], 0.5f - i % 2 * 0.08f, 0.34f), V(MathX.Pi, 0, 0), 4));
        parts.Add(Sphere(P.Beak, 0.06f, V(-0.1f, 0.06f, 0.08f), V(1, 0.5f, 1.4f), 8, 6));
        parts.Add(Sphere(P.Beak, 0.06f, V(0.1f, 0.06f, 0.08f), V(1, 0.5f, 1.4f), 8, 6));
        Body.Add(M(Merge(parts)));
        _wings = new Node[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var w = Pivot(s * 0.32f, 0.7f, 0);
            w.Add(M(Blob(P.OwlDark, 0.1f, 0.32f, 0.24f, 1).Translate(s * 0.04f, -0.22f, -0.02f)));
            Body.Add(w);
            _wings[i] = w;
        }
        Head.Position = V(0, 0.98f, 0);
        Head.Add(M(
            Sphere(P.Owl, 0.3f, null, V(1.05f, 0.9f, 0.95f), 16, 12),
            Sphere(P.OwlLight, 0.13f, V(-0.12f, 0, 0.2f), V(1, 1, 0.5f), 12, 8),
            Sphere(P.OwlLight, 0.13f, V(0.12f, 0, 0.2f), V(1, 1, 0.5f), 12, 8),
            Cone(P.Owl, 0.07f, 0.18f, V(-0.2f, 0.26f, 0), V(0, 0, 0.4f), 5),
            Cone(P.Owl, 0.07f, 0.18f, V(0.2f, 0.26f, 0), V(0, 0, -0.4f), 5),
            Cone(P.Beak, 0.045f, 0.12f, V(0, -0.07f, 0.28f), V(MathX.Pi / 2 + 0.5f, 0, 0), 6),
            // yuvarlak gozluk
            Part(TorusGeo(0.1f, 0.012f, 6, 16), P.Metal, V(-0.12f, 0.01f, 0.29f)),
            Part(TorusGeo(0.1f, 0.012f, 6, 16), P.Metal, V(0.12f, 0.01f, 0.29f)),
            Box(P.Metal, 0.05f, 0.015f, 0.015f, V(0, 0.03f, 0.3f))));
        Eyes = BigEyes(0.12f, 0.01f, 0.24f, 0.07f, "#3a2a1a");
        Head.Add(Eyes);
        Body.Add(Head);
    }

    private void BuildHedgehog()
    {
        var parts = new List<Geo>
        {
            Blob(P.Hedgehog, 0.36f, 0.38f, 0.36f, 2).Translate(0, 0.4f, 0),
            Blob(P.HedgehogFace, 0.27f, 0.27f, 0.2f, 2).Translate(0, 0.36f, 0.18f),
            // onluk
            Box(P.Apron, 0.34f, 0.26f, 0.05f, V(0, 0.24f, 0.3f), V(-0.2f, 0, 0)),
            Sphere("#2a1d1c", 0.06f, V(-0.12f, 0.04f, 0.12f), V(1, 0.5f, 1.4f), 8, 6),
            Sphere("#2a1d1c", 0.06f, V(0.12f, 0.04f, 0.12f), V(1, 0.5f, 1.4f), 8, 6),
        };
        for (int i = 0; i < 46; i++)
        {
            double u = (i * 0.618) % 1;
            double v = i / 46.0;
            double theta = u * Math.PI * 2;
            double phi = 0.15 + v * 1.25;
            var dir = new Vector3((float)(Math.Sin(phi) * Math.Cos(theta)), (float)Math.Cos(phi), (float)(Math.Sin(phi) * Math.Sin(theta)));
            if (dir.Z > 0.45f) continue; // yuz tarafi acik kalsin
            var pos = dir * 0.36f + V(0, 0.4f, -0.03f);
            parts.Add(Cone(P.HedgehogSpikes, 0.05f, 0.2f, null, null, 4).Transform(AlignUp(dir)).Translate(pos.X, pos.Y, pos.Z));
        }
        Body.Add(M(Merge(parts)));
        Head.Position = V(0, 0.5f, 0.26f);
        Head.Add(M(
            Cone(P.HedgehogFace, 0.1f, 0.2f, V(0, -0.04f, 0.1f), V(MathX.Pi / 2, 0, 0), 8),
            Sphere("#2a1d1c", 0.04f, V(0, -0.04f, 0.21f), null, 8, 6),
            Sphere(P.Blush, 0.035f, V(-0.14f, -0.06f, 0.06f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.035f, V(0.14f, -0.06f, 0.06f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere("#c2a38a", 0.06f, V(-0.17f, 0.12f, -0.05f), null, 6, 4),
            Sphere("#c2a38a", 0.06f, V(0.17f, 0.12f, -0.05f), null, 6, 4)));
        Eyes = FoxModel.MakeEyes(0.085f, 0.05f, 0.05f, 0.035f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    private void BuildFrog()
    {
        var parts = new List<Geo>
        {
            Blob(P.Frog, 0.36f, 0.26f, 0.32f, 2).Translate(0, 0.27f, 0),
            Blob(P.FrogLight, 0.26f, 0.17f, 0.2f, 2).Translate(0, 0.21f, 0.14f),
            Blob(P.FrogDark, 0.14f, 0.1f, 0.24f, 1).Translate(-0.28f, 0.12f, -0.04f),
            Blob(P.FrogDark, 0.14f, 0.1f, 0.24f, 1).Translate(0.28f, 0.12f, -0.04f),
            Blob(P.Frog, 0.07f, 0.14f, 0.07f, 1).Translate(-0.18f, 0.12f, 0.24f),
            Blob(P.Frog, 0.07f, 0.14f, 0.07f, 1).Translate(0.18f, 0.12f, 0.24f),
        };
        foreach (var p in new[] { V(-0.12f, 0.42f, -0.1f), V(0.15f, 0.4f, -0.18f), V(0.02f, 0.46f, -0.22f) })
            parts.Add(Sphere(P.FrogDark, 0.04f, p, V(1, 0.4f, 1), 6, 4));
        Body.Add(M(Merge(parts)));
        _throat = new Node { Position = V(0, 0.2f, 0.26f), Scale = new Vector3(0.4f) };
        _throat.Add(M(Sphere(P.FrogLight, 0.1f, null, V(1, 0.7f, 0.6f), 10, 8)));
        Body.Add(_throat);
        Head.Position = V(0, 0.4f, 0.1f);
        Head.Add(M(
            Sphere(P.Frog, 0.12f, V(-0.14f, 0.06f, 0), V(1, 1, 0.9f), 10, 8),
            Sphere(P.Frog, 0.12f, V(0.14f, 0.06f, 0), V(1, 1, 0.9f), 10, 8),
            Part(TorusGeo(0.13f, 0.012f, 4, 14, MathX.Pi * 0.8f), "#2e5e24", V(0, -0.04f, 0.2f), V(0.2f, 0, MathX.Pi + MathX.Pi * 0.1f)),
            // yaris bandi
            Part(TorusGeo(0.21f, 0.03f, 6, 18), "#ff5a5a", V(0, 0.02f, -0.02f), V(MathX.Pi / 2, 0, 0), V(1.25f, 1, 0.8f))));
        Eyes = BigEyes(0.14f, 0.1f, 0.08f, 0.075f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    private void BuildBear()
    {
        Body.Add(M(
            Blob(P.Bear, 0.55f, 0.6f, 0.5f, 2).Translate(0, 0.6f, 0),
            Blob(P.BearLight, 0.38f, 0.42f, 0.24f, 2).Translate(0, 0.55f, 0.28f),
            Blob(P.Bear, 0.16f, 0.14f, 0.24f, 1).Translate(-0.3f, 0.13f, 0.32f),
            Blob(P.Bear, 0.16f, 0.14f, 0.24f, 1).Translate(0.3f, 0.13f, 0.32f),
            Blob(P.Bear, 0.13f, 0.3f, 0.13f, 1).RotateX(-0.5f).Translate(-0.48f, 0.66f, 0.2f),
            Blob(P.Bear, 0.13f, 0.3f, 0.13f, 1).RotateX(-0.5f).Translate(0.48f, 0.66f, 0.2f)));
        Head.Position = V(0, 1.28f, 0.06f);
        Head.Add(M(
            Sphere(P.Bear, 0.36f, null, V(1, 0.9f, 0.95f), 16, 12),
            Sphere(P.Bear, 0.11f, V(-0.27f, 0.27f, -0.02f), null, 8, 6),
            Sphere(P.Bear, 0.11f, V(0.27f, 0.27f, -0.02f), null, 8, 6),
            Sphere(P.BearLight, 0.06f, V(-0.27f, 0.27f, 0.05f), null, 6, 4),
            Sphere(P.BearLight, 0.06f, V(0.27f, 0.27f, 0.05f), null, 6, 4),
            Blob(P.BearLight, 0.16f, 0.12f, 0.14f, 2).Translate(0, -0.1f, 0.3f),
            Sphere("#2a1d1c", 0.06f, V(0, -0.05f, 0.43f), V(1.2f, 0.8f, 0.8f), 8, 6),
            // balikci sapkasi
            Cyl("#7a8f4a", 0.42f, 0.42f, 0.03f, V(0, 0.24f, -0.02f), null, 18),
            Cyl("#8ba35a", 0.27f, 0.3f, 0.16f, V(0, 0.33f, -0.02f), null, 16),
            Box("#f2c94c", 0.05f, 0.1f, 0.02f, V(0.24f, 0.32f, 0.12f), V(0, 0.6f, 0.3f))));
        Eyes = FoxModel.MakeEyes(0.13f, 0.04f, 0.3f, 0.04f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    private void BuildRabbit()
    {
        Body.Add(M(
            Blob(P.Rabbit, 0.28f, 0.32f, 0.27f, 2).Translate(0, 0.33f, 0),
            Blob(P.RabbitGrey, 0.18f, 0.2f, 0.12f, 2).Translate(0, 0.3f, 0.17f),
            Blob(P.Rabbit, 0.12f, 0.1f, 0.2f, 1).Translate(-0.17f, 0.08f, 0.1f),
            Blob(P.Rabbit, 0.12f, 0.1f, 0.2f, 1).Translate(0.17f, 0.08f, 0.1f),
            Sphere("#ffffff", 0.1f, V(0, 0.25f, -0.27f), null, 8, 6),
            // bahcivan atkisi
            Part(TorusGeo(0.16f, 0.04f, 6, 14), "#f2c94c", V(0, 0.58f, 0.02f), V(MathX.Pi / 2, 0, 0))));
        Head.Position = V(0, 0.78f, 0.04f);
        Head.Add(M(
            Sphere(P.Rabbit, 0.22f, null, V(1, 0.92f, 0.95f), 14, 10),
            Blob(P.Rabbit, 0.09f, 0.07f, 0.07f, 2).Translate(-0.07f, -0.08f, 0.17f),
            Blob(P.Rabbit, 0.09f, 0.07f, 0.07f, 2).Translate(0.07f, -0.08f, 0.17f),
            Sphere(P.RabbitPink, 0.035f, V(0, -0.04f, 0.21f), V(1.2f, 0.8f, 0.8f), 8, 6),
            Sphere(P.Blush, 0.035f, V(-0.14f, -0.05f, 0.13f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.035f, V(0.14f, -0.05f, 0.13f), V(1, 0.6f, 0.4f), 6, 4)));
        _ears = new Node[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var e = Pivot(s * 0.09f, 0.15f, -0.02f);
            e.Rotation = V(0, 0, -s * 0.15f);
            e.Add(M(
                Blob(P.Rabbit, 0.06f, 0.24f, 0.035f, 1).Translate(0, 0.22f, 0),
                Blob(P.RabbitPink, 0.035f, 0.18f, 0.02f, 1).Translate(0, 0.22f, 0.02f)));
            Head.Add(e);
            _ears[i] = e;
        }
        Eyes = FoxModel.MakeEyes(0.085f, 0.03f, 0.17f, 0.035f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    private void BuildBeaver()
    {
        Body.Add(M(
            Blob(P.Beaver, 0.3f, 0.36f, 0.28f, 2).Translate(0, 0.38f, 0),
            Blob("#d8b08a", 0.2f, 0.24f, 0.13f, 2).Translate(0, 0.34f, 0.18f),
            Blob(P.BeaverDark, 0.12f, 0.09f, 0.18f, 1).Translate(-0.17f, 0.08f, 0.1f),
            Blob(P.BeaverDark, 0.12f, 0.09f, 0.18f, 1).Translate(0.17f, 0.08f, 0.1f),
            // alet kemeri
            Part(TorusGeo(0.27f, 0.035f, 6, 16), "#6b4a2f", V(0, 0.26f, 0), V(MathX.Pi / 2, 0, 0), V(1.05f, 1, 1)),
            Box("#c9c9c9", 0.05f, 0.12f, 0.03f, V(0.2f, 0.2f, 0.18f), V(0, 0, 0.2f))));
        _tailPivot = Pivot(0, 0.12f, -0.25f);
        _tailPivot.Add(M(
            Blob(P.BeaverTail, 0.16f, 0.04f, 0.26f, 1).Translate(0, 0, -0.22f),
            Box("#4a352a", 0.2f, 0.01f, 0.01f, V(0, 0.04f, -0.15f)),
            Box("#4a352a", 0.2f, 0.01f, 0.01f, V(0, 0.04f, -0.25f)),
            Box("#4a352a", 0.01f, 0.01f, 0.3f, V(0, 0.045f, -0.2f))));
        Body.Add(_tailPivot);
        Head.Position = V(0, 0.84f, 0.03f);
        Head.Add(M(
            Sphere(P.Beaver, 0.23f, null, V(1, 0.92f, 0.95f), 14, 10),
            Blob("#d8b08a", 0.11f, 0.08f, 0.08f, 2).Translate(0, -0.08f, 0.17f),
            Sphere("#2a1d1c", 0.04f, V(0, -0.03f, 0.24f), V(1.3f, 0.8f, 0.8f), 8, 6),
            Box(P.Tooth, 0.07f, 0.07f, 0.02f, V(0, -0.16f, 0.22f)),
            Box("#d9d0b0", 0.004f, 0.07f, 0.022f, V(0, -0.16f, 0.222f)),
            Sphere(P.BeaverDark, 0.06f, V(-0.18f, 0.15f, -0.02f), null, 6, 4),
            Sphere(P.BeaverDark, 0.06f, V(0.18f, 0.15f, -0.02f), null, 6, 4),
            // baret
            Sphere("#ffcc33", 0.24f, V(0, 0.07f, -0.01f), V(1, 0.75f, 1), 14, 8),
            Cyl("#ffcc33", 0.28f, 0.28f, 0.025f, V(0, 0.07f, 0.03f), null, 16),
            Box("#e0a91f", 0.04f, 0.05f, 0.4f, V(0, 0.25f, -0.01f))));
        Eyes = FoxModel.MakeEyes(0.09f, 0.03f, 0.18f, 0.035f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    // ---------------------------------------------------------------- yeni adalilar

    /// <summary>Penguen Paytak: kizak yarisi. Kirmizi bere + kar gozlugu alnina itilmis.</summary>
    private void BuildPenguin()
    {
        Body.Add(M(
            Blob("#2b3346", 0.27f, 0.36f, 0.25f, 2).Translate(0, 0.4f, 0),
            Blob("#f4f1ea", 0.2f, 0.29f, 0.14f, 2).Translate(0, 0.37f, 0.13f),
            Sphere("#ffb02e", 0.07f, V(-0.09f, 0.03f, 0.09f), V(1, 0.4f, 1.6f), 8, 6),
            Sphere("#ffb02e", 0.07f, V(0.09f, 0.03f, 0.09f), V(1, 0.4f, 1.6f), 8, 6),
            // atki
            Part(TorusGeo(0.18f, 0.045f, 6, 14), "#3fa7ff", V(0, 0.64f, 0.01f), V(MathX.Pi / 2 - 0.1f, 0, 0)),
            Box("#3fa7ff", 0.07f, 0.2f, 0.03f, V(0.1f, 0.54f, 0.17f), V(0.15f, 0, 0.15f))));
        _wings = new Node[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var w = Pivot(s * 0.25f, 0.56f, 0);
            w.Add(M(Blob("#2b3346", 0.05f, 0.22f, 0.12f, 1).Translate(s * 0.03f, -0.17f, 0)));
            Body.Add(w);
            _wings[i] = w;
        }
        Head.Position = V(0, 0.84f, 0.02f);
        Head.Add(M(
            Sphere("#2b3346", 0.21f, null, V(1, 0.95f, 0.95f), 14, 10),
            Sphere("#f4f1ea", 0.09f, V(-0.075f, -0.01f, 0.13f), V(1, 1.2f, 0.6f), 10, 8),
            Sphere("#f4f1ea", 0.09f, V(0.075f, -0.01f, 0.13f), V(1, 1.2f, 0.6f), 10, 8),
            Cone("#ffb02e", 0.05f, 0.13f, V(0, -0.06f, 0.24f), V(MathX.Pi / 2, 0, 0), 6, V(1.2f, 1, 0.7f)),
            Sphere(P.Blush, 0.03f, V(-0.13f, -0.07f, 0.12f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.03f, V(0.13f, -0.07f, 0.12f), V(1, 0.6f, 0.4f), 6, 4),
            // bere + ponpon
            Sphere("#ff5a5a", 0.205f, V(0, 0.08f, -0.01f), V(1, 0.7f, 1), 14, 8),
            Cyl("#ffffff", 0.21f, 0.21f, 0.05f, V(0, 0.07f, -0.01f), null, 16),
            Sphere("#ffffff", 0.06f, V(0, 0.24f, -0.02f), null, 8, 6),
            // kar gozlugu (alnina itilmis)
            Part(TorusGeo(0.06f, 0.014f, 6, 14), "#ffd34d", V(-0.07f, 0.13f, 0.15f), V(-0.5f, 0, 0)),
            Part(TorusGeo(0.06f, 0.014f, 6, 14), "#ffd34d", V(0.07f, 0.13f, 0.15f), V(-0.5f, 0, 0)),
            Sphere("#7fd1ff", 0.055f, V(-0.07f, 0.13f, 0.15f), V(1, 1, 0.3f), 8, 6),
            Sphere("#7fd1ff", 0.055f, V(0.07f, 0.13f, 0.15f), V(1, 1, 0.3f), 8, 6)));
        Eyes = FoxModel.MakeEyes(0.075f, 0.0f, 0.18f, 0.03f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    /// <summary>Fok Deniz: buzda balikci. Yesil yun bere, ucunda olta.</summary>
    private void BuildSeal()
    {
        Body.Add(M(
            Blob("#9aa6b2", 0.34f, 0.3f, 0.5f, 2).Translate(0, 0.3f, -0.05f),
            Blob("#c8d0d8", 0.24f, 0.2f, 0.36f, 2).Translate(0, 0.18f, 0.08f),
            Blob("#9aa6b2", 0.26f, 0.32f, 0.24f, 2).RotateX(-0.35f).Translate(0, 0.52f, 0.22f),
            Sphere("#7d8894", 0.04f, V(-0.12f, 0.45f, -0.2f), V(1, 0.5f, 1), 6, 4),
            Sphere("#7d8894", 0.05f, V(0.14f, 0.4f, -0.3f), V(1, 0.5f, 1), 6, 4),
            Blob("#7d8894", 0.07f, 0.04f, 0.2f, 1).Translate(-0.27f, 0.1f, 0.2f).RotateY(-0.3f),
            Blob("#7d8894", 0.07f, 0.04f, 0.2f, 1).Translate(0.27f, 0.1f, 0.2f).RotateY(0.3f)));
        _flippers = Pivot(0, 0.12f, -0.5f);
        _flippers.Add(M(
            Blob("#7d8894", 0.2f, 0.04f, 0.12f, 1).Translate(0, 0, -0.08f)));
        Body.Add(_flippers);
        Head.Position = V(0, 0.82f, 0.32f);
        Head.Add(M(
            Sphere("#9aa6b2", 0.22f, null, V(1, 0.92f, 1), 14, 10),
            Blob("#d7dde3", 0.1f, 0.07f, 0.08f, 2).Translate(-0.055f, -0.07f, 0.17f),
            Blob("#d7dde3", 0.1f, 0.07f, 0.08f, 2).Translate(0.055f, -0.07f, 0.17f),
            Sphere("#2a1d1c", 0.04f, V(0, -0.03f, 0.23f), V(1.3f, 0.9f, 0.8f), 8, 6),
            Box("#e8e8e8", 0.13f, 0.004f, 0.004f, V(-0.16f, -0.06f, 0.18f), V(0, 0.4f, 0.15f)),
            Box("#e8e8e8", 0.13f, 0.004f, 0.004f, V(0.16f, -0.06f, 0.18f), V(0, -0.4f, -0.15f)),
            Sphere(P.Blush, 0.03f, V(-0.14f, -0.06f, 0.12f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.03f, V(0.14f, -0.06f, 0.12f), V(1, 0.6f, 0.4f), 6, 4),
            // yun bere
            Sphere("#3c8f5a", 0.215f, V(0, 0.07f, -0.02f), V(1, 0.72f, 1), 14, 8),
            Cyl("#2f7347", 0.22f, 0.22f, 0.06f, V(0, 0.06f, -0.02f), null, 16),
            Sphere("#f2c94c", 0.055f, V(0, 0.23f, -0.03f), null, 8, 6)));
        Eyes = FoxModel.MakeEyes(0.085f, 0.03f, 0.17f, 0.036f);
        Head.Add(Eyes);
        Body.Add(Head);
        // olta
        var rod = Pivot(0.3f, 0.35f, 0.3f);
        rod.Rotation = V(-0.7f, 0, -0.2f);
        rod.Add(M(
            Cyl("#7a5230", 0.012f, 0.018f, 1.1f, V(0, 0.55f, 0), null, 5),
            Cyl("#cfcfcf", 0.03f, 0.03f, 0.04f, V(0, 0.15f, 0.03f), V(MathX.Pi / 2, 0, 0), 8)));
        Body.Add(rod);
    }

    /// <summary>Dag Kecisi Kaya: tirmanma ogretmeni. Kivrik boynuzlar, tirmanma ipi.</summary>
    private void BuildGoat()
    {
        var horn = new List<Vector3>();
        for (int i = 0; i <= 8; i++)
        {
            float a = i / 8f * 3.6f;
            horn.Add(V(0.02f + MathF.Sin(a) * 0.09f * (1 - i / 14f), 0.04f + i * 0.012f + (1 - MathF.Cos(a)) * 0.05f, -MathF.Sin(a * 0.5f) * 0.12f));
        }
        var hornL = Tube(horn, 18, 0.028f, 6).Color("#d9c9a3");
        var hornR = hornL.Clone().Scale(-1, 1, 1);
        Body.Add(M(
            Blob("#efe9de", 0.3f, 0.3f, 0.42f, 2).Translate(0, 0.62f, -0.02f),
            Blob("#e2d9c8", 0.2f, 0.18f, 0.28f, 2).Translate(0, 0.52f, 0.12f),
            Cyl("#8a7d6a", 0.055f, 0.045f, 0.42f, V(-0.15f, 0.22f, 0.22f), null, 6),
            Cyl("#8a7d6a", 0.055f, 0.045f, 0.42f, V(0.15f, 0.22f, 0.22f), null, 6),
            Cyl("#8a7d6a", 0.055f, 0.045f, 0.42f, V(-0.15f, 0.22f, -0.25f), null, 6),
            Cyl("#8a7d6a", 0.055f, 0.045f, 0.42f, V(0.15f, 0.22f, -0.25f), null, 6),
            Box("#3a3330", 0.08f, 0.04f, 0.08f, V(-0.15f, 0.02f, 0.22f)),
            Box("#3a3330", 0.08f, 0.04f, 0.08f, V(0.15f, 0.02f, 0.22f)),
            Box("#3a3330", 0.08f, 0.04f, 0.08f, V(-0.15f, 0.02f, -0.25f)),
            Box("#3a3330", 0.08f, 0.04f, 0.08f, V(0.15f, 0.02f, -0.25f)),
            Cone("#efe9de", 0.06f, 0.14f, V(0, 0.78f, -0.42f), V(-2.2f, 0, 0), 5),
            // capraz tirmanma ipi
            Part(TorusGeo(0.3f, 0.025f, 6, 18), "#ff7a3d", V(0, 0.64f, 0), V(0.2f, 0, 0.7f), V(1, 1.05f, 1.2f)),
            Cyl("#c0c0c0", 0.03f, 0.03f, 0.1f, V(0.2f, 0.45f, 0.24f), V(0, 0, 0.4f), 8)));
        Head.Position = V(0, 0.98f, 0.36f);
        _ears = new Node[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var e = Pivot(s * 0.12f, 0.05f, -0.06f);
            e.Rotation = V(0, 0, -s * 1.2f);
            e.Add(M(Blob("#e2d9c8", 0.05f, 0.11f, 0.025f, 1).Translate(0, 0.09f, 0)));
            Head.Add(e);
            _ears[i] = e;
        }
        Head.Add(M(
            Sphere("#efe9de", 0.17f, null, V(0.95f, 0.95f, 1.05f), 14, 10),
            Blob("#e2d9c8", 0.1f, 0.09f, 0.14f, 2).Translate(0, -0.08f, 0.14f),
            Sphere("#3a3330", 0.03f, V(-0.035f, -0.06f, 0.27f), null, 6, 4),
            Sphere("#3a3330", 0.03f, V(0.035f, -0.06f, 0.27f), null, 6, 4),
            Cone("#e2d9c8", 0.05f, 0.16f, V(0, -0.22f, 0.14f), V(MathX.Pi, 0, 0), 6),
            hornL.Translate(-0.06f, 0.1f, -0.02f),
            hornR.Translate(0.06f, 0.1f, -0.02f)));
        Eyes = FoxModel.MakeEyes(0.09f, 0.03f, 0.13f, 0.032f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    /// <summary>Kostebek Profesor: kazi ve hazine haritalari. Tek gozluk + kurek.</summary>
    private void BuildMole()
    {
        Body.Add(M(
            Blob("#5a4a5e", 0.3f, 0.32f, 0.3f, 2).Translate(0, 0.34f, 0),
            Blob("#7b6a80", 0.2f, 0.22f, 0.14f, 2).Translate(0, 0.3f, 0.17f),
            // yelek
            Box("#7a3b3b", 0.36f, 0.22f, 0.04f, V(0, 0.36f, 0.25f), V(-0.12f, 0, 0)),
            Sphere("#f2c94c", 0.018f, V(0, 0.4f, 0.28f), null, 6, 4),
            Sphere("#f2c94c", 0.018f, V(0, 0.33f, 0.29f), null, 6, 4),
            // pembe kurek eller
            Blob("#ffb3c1", 0.09f, 0.06f, 0.11f, 1).Translate(-0.27f, 0.22f, 0.15f),
            Blob("#ffb3c1", 0.09f, 0.06f, 0.11f, 1).Translate(0.27f, 0.22f, 0.15f),
            Blob("#5a4a5e", 0.1f, 0.07f, 0.16f, 1).Translate(-0.15f, 0.06f, 0.1f),
            Blob("#5a4a5e", 0.1f, 0.07f, 0.16f, 1).Translate(0.15f, 0.06f, 0.1f),
            // kucuk kurek
            Cyl("#8a5a34", 0.015f, 0.015f, 0.6f, V(0.34f, 0.32f, 0.08f), V(0.2f, 0, -0.25f), 5),
            Box("#a8b0b8", 0.12f, 0.15f, 0.02f, V(0.41f, 0.02f, 0.14f), V(0.2f, 0, -0.25f))));
        Head.Position = V(0, 0.68f, 0.06f);
        var nose = Pivot(0, -0.04f, 0.22f);
        nose.Add(M(
            Cone("#ffb3c1", 0.06f, 0.1f, V(0, 0, 0.02f), V(MathX.Pi / 2, 0, 0), 8),
            Sphere("#ff7f9a", 0.035f, V(0, 0, 0.08f), null, 8, 6)));
        Head.Add(nose);
        _extra.Add(nose);
        Head.Add(M(
            Sphere("#5a4a5e", 0.2f, null, V(1, 0.9f, 1.05f), 14, 10),
            Sphere(P.Blush, 0.03f, V(-0.12f, -0.06f, 0.13f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.03f, V(0.12f, -0.06f, 0.13f), V(1, 0.6f, 0.4f), 6, 4),
            // tek gozluk + zinciri
            Part(TorusGeo(0.05f, 0.01f, 6, 16), "#f2c94c", V(0.065f, 0.03f, 0.18f)),
            Sphere("#cfe8ff", 0.045f, V(0.065f, 0.03f, 0.178f), V(1, 1, 0.2f), 8, 6),
            Cyl("#f2c94c", 0.004f, 0.004f, 0.16f, V(0.11f, -0.04f, 0.15f), V(0, 0, 0.5f), 4),
            // kasif sapkasi
            Sphere("#d9c08a", 0.19f, V(0, 0.09f, -0.01f), V(1, 0.6f, 1), 14, 8),
            Cyl("#d9c08a", 0.27f, 0.27f, 0.02f, V(0, 0.08f, -0.01f), null, 18),
            Cyl("#7a5230", 0.195f, 0.195f, 0.035f, V(0, 0.1f, -0.01f), null, 16)));
        Eyes = FoxModel.MakeEyes(0.065f, 0.03f, 0.17f, 0.022f);
        Head.Add(Eyes);
        Body.Add(Head);
    }

    /// <summary>Kedi Foto: fotografci. Boynunda fotograf makinesi, bere.</summary>
    private void BuildCat()
    {
        Body.Add(M(
            Blob("#f0a35a", 0.25f, 0.3f, 0.24f, 2).Translate(0, 0.33f, 0),
            Blob("#fff1e0", 0.16f, 0.2f, 0.11f, 2).Translate(0, 0.3f, 0.15f),
            Blob("#f0a35a", 0.1f, 0.08f, 0.16f, 1).Translate(-0.14f, 0.06f, 0.1f),
            Blob("#f0a35a", 0.1f, 0.08f, 0.16f, 1).Translate(0.14f, 0.06f, 0.1f),
            Box("#d88a45", 0.2f, 0.03f, 0.02f, V(0, 0.42f, -0.22f), V(0.3f, 0, 0)),
            Box("#d88a45", 0.2f, 0.03f, 0.02f, V(0, 0.32f, -0.24f), V(0.1f, 0, 0)),
            // fotograf makinesi + kayis
            Part(TorusGeo(0.17f, 0.012f, 4, 16), "#3a3a3a", V(0, 0.48f, 0.05f), V(MathX.Pi / 2 - 0.5f, 0, 0)),
            Box("#2e2e33", 0.16f, 0.1f, 0.07f, V(0, 0.36f, 0.26f)),
            Cyl("#555560", 0.04f, 0.045f, 0.06f, V(0, 0.36f, 0.31f), V(MathX.Pi / 2, 0, 0), 12),
            Cyl("#8fd3ff", 0.028f, 0.028f, 0.01f, V(0, 0.36f, 0.345f), V(MathX.Pi / 2, 0, 0), 12),
            Box("#ff5a5a", 0.03f, 0.02f, 0.02f, V(0.05f, 0.42f, 0.25f))));
        _tailPivot = Pivot(0, 0.15f, -0.22f);
        _tailPivot.Add(M(Tube(new List<Vector3> { V(0, 0, 0), V(0, 0.12f, -0.1f), V(0, 0.32f, -0.12f), V(0.05f, 0.45f, -0.04f) }, 14, 0.04f, 6).Color("#f0a35a")));
        Body.Add(_tailPivot);
        Head.Position = V(0, 0.74f, 0.04f);
        Head.Add(M(
            Sphere("#f0a35a", 0.22f, null, V(1.05f, 0.9f, 0.95f), 14, 10),
            Blob("#fff1e0", 0.11f, 0.07f, 0.08f, 2).Translate(0, -0.08f, 0.15f),
            Cone("#ff8fa3", 0.025f, 0.03f, V(0, -0.04f, 0.21f), V(-MathX.Pi / 2, 0, 0), 3),
            Box("#d88a45", 0.04f, 0.012f, 0.01f, V(0, 0.15f, 0.17f)),
            Box("#d88a45", 0.03f, 0.012f, 0.01f, V(-0.05f, 0.14f, 0.165f), V(0, 0, 0.3f)),
            Box("#d88a45", 0.03f, 0.012f, 0.01f, V(0.05f, 0.14f, 0.165f), V(0, 0, -0.3f)),
            Box("#ffffff", 0.14f, 0.003f, 0.003f, V(-0.16f, -0.06f, 0.15f), V(0, 0.3f, 0.1f)),
            Box("#ffffff", 0.14f, 0.003f, 0.003f, V(0.16f, -0.06f, 0.15f), V(0, -0.3f, -0.1f)),
            Sphere(P.Blush, 0.032f, V(-0.13f, -0.06f, 0.13f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.032f, V(0.13f, -0.06f, 0.13f), V(1, 0.6f, 0.4f), 6, 4),
            // bere
            Sphere("#5b5fc7", 0.2f, V(0.03f, 0.12f, -0.02f), V(1.1f, 0.45f, 1.05f), 14, 8),
            Sphere("#5b5fc7", 0.03f, V(0.05f, 0.22f, -0.02f), null, 6, 4)));
        _ears = new Node[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var e = Pivot(s * 0.12f, 0.12f, 0);
            e.Rotation = V(0, 0, -s * 0.15f);
            e.Add(M(
                Cone("#f0a35a", 0.07f, 0.14f, V(0, 0.06f, 0), null, 4),
                Cone("#ffb3c1", 0.04f, 0.08f, V(0, 0.05f, 0.025f), null, 4)));
            Head.Add(e);
            _ears[i] = e;
        }
        Eyes = BigEyes(0.08f, 0.03f, 0.15f, 0.045f, "#3f7a3a");
        Head.Add(Eyes);
        Body.Add(Head);
    }

    /// <summary>Kutup Ayisi Bulut: kis dukkani. Kocaman, kalin orgu atkili.</summary>
    private void BuildPolarBear()
    {
        Body.Add(M(
            Blob("#f3f1ec", 0.6f, 0.62f, 0.55f, 2).Translate(0, 0.62f, 0),
            Blob("#e6e2d8", 0.4f, 0.42f, 0.26f, 2).Translate(0, 0.56f, 0.3f),
            Blob("#f3f1ec", 0.17f, 0.15f, 0.26f, 1).Translate(-0.32f, 0.13f, 0.34f),
            Blob("#f3f1ec", 0.17f, 0.15f, 0.26f, 1).Translate(0.32f, 0.13f, 0.34f),
            Blob("#f3f1ec", 0.14f, 0.32f, 0.14f, 1).RotateX(-0.5f).Translate(-0.52f, 0.66f, 0.2f),
            Blob("#f3f1ec", 0.14f, 0.32f, 0.14f, 1).RotateX(-0.5f).Translate(0.52f, 0.66f, 0.2f),
            // onluk (dukkan)
            Box("#4a7bd0", 0.5f, 0.42f, 0.05f, V(0, 0.45f, 0.5f), V(-0.12f, 0, 0)),
            Box("#ffffff", 0.18f, 0.1f, 0.02f, V(0, 0.42f, 0.53f), V(-0.12f, 0, 0)),
            // orgu atki
            Part(TorusGeo(0.33f, 0.08f, 8, 18), "#e2463f", V(0, 1.06f, 0.04f), V(MathX.Pi / 2 - 0.15f, 0, 0)),
            Part(TorusGeo(0.33f, 0.03f, 4, 18), "#ffffff", V(0, 1.06f, 0.04f), V(MathX.Pi / 2 - 0.15f, 0, 0), V(1.08f, 1.08f, 1))));
        _scarfEnd = Pivot(0.2f, 1.0f, 0.3f);
        _scarfEnd.Add(M(
            Box("#e2463f", 0.14f, 0.36f, 0.05f, V(0, -0.18f, 0)),
            Box("#ffffff", 0.145f, 0.04f, 0.055f, V(0, -0.25f, 0)),
            Box("#ffffff", 0.145f, 0.04f, 0.055f, V(0, -0.33f, 0))));
        Body.Add(_scarfEnd);
        Head.Position = V(0, 1.34f, 0.08f);
        Head.Add(M(
            Sphere("#f3f1ec", 0.37f, null, V(1, 0.88f, 1), 16, 12),
            Sphere("#f3f1ec", 0.1f, V(-0.27f, 0.26f, -0.04f), null, 8, 6),
            Sphere("#f3f1ec", 0.1f, V(0.27f, 0.26f, -0.04f), null, 8, 6),
            Sphere("#d9d3c6", 0.055f, V(-0.27f, 0.26f, 0.03f), null, 6, 4),
            Sphere("#d9d3c6", 0.055f, V(0.27f, 0.26f, 0.03f), null, 6, 4),
            Blob("#e6e2d8", 0.17f, 0.13f, 0.17f, 2).Translate(0, -0.1f, 0.3f),
            Sphere("#2a1d1c", 0.065f, V(0, -0.04f, 0.46f), V(1.2f, 0.8f, 0.8f), 8, 6),
            Sphere(P.Blush, 0.05f, V(-0.22f, -0.1f, 0.25f), V(1, 0.6f, 0.4f), 6, 4),
            Sphere(P.Blush, 0.05f, V(0.22f, -0.1f, 0.25f), V(1, 0.6f, 0.4f), 6, 4)));
        Eyes = FoxModel.MakeEyes(0.13f, 0.05f, 0.31f, 0.04f);
        Head.Add(Eyes);
        Body.Add(Head);
    }
}
