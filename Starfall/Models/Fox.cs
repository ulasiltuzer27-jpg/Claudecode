using System.Numerics;
using Starfall.Core;
using Starfall.Render;
using static Starfall.Render.Geo;

namespace Starfall.Models;

public struct FoxPose
{
    public float Speed;
    public bool Grounded;
    public float Vy;
    public bool Gliding;
    public bool Swimming;
    public bool Climbing;
    public bool Boating;
    public float ClimbPhase;
}

/// <summary>
/// Mina: hiyerarsik ilkel parcalar, tamamen prosedurel animasyon (iskelet yok).
/// JS'teki FoxModel'in birebir karsiligi + kiyafet yuvalari (sapka, gozluk, canta, atki rengi).
/// </summary>
public sealed class FoxModel
{
    public readonly Node Root = new() { Name = "fox" };
    public readonly Node Body = new();
    public readonly Node Head, Eyes, ScarfTail, Tail, TailInner, Glider;
    public readonly Node HatSlot, FaceSlot, BackSlot;
    public readonly Node[] Ears = new Node[2];
    public readonly Node[] Legs = new Node[4];
    private readonly float[] _legPhase = { 0, MathX.Pi, MathX.Pi, 0 };
    public readonly Material ScarfMat = Material.Char();
    private float _t, _walk, _blink = 2, _earT = 3, _squash, _squashVel, _lookYaw;
    public float Flip;
    private float _glider;
    public Vector3? LookTarget;
    private readonly Random _r = new(3);

    public static Node M(Geo g, Material? mat = null) => new(MeshData.From(g, false), mat ?? Material.Char());

    public static Node MakeEyes(float spacing, float y, float z, float r = 0.045f, string color = P.Eye)
    {
        var parts = new List<Geo>();
        foreach (var s in new[] { -1f, 1f })
        {
            parts.Add(Sphere(color, r, V(s * spacing, 0, 0), V(1, 1.3f, 0.6f), 10, 8));
            parts.Add(Sphere(P.EyeShine, r * 0.32f, V(s * spacing - r * 0.3f, r * 0.45f, r * 0.45f), null, 6, 4));
        }
        var eyes = new Node { Position = V(0, y, z) };
        eyes.Add(M(Merge(parts)));
        return eyes;
    }

    public FoxModel()
    {
        Body.Position = V(0, 0.36f, 0);
        Root.Add(Body);

        Body.Add(M(Merge(
            Blob(P.FoxOrange, 0.24f, 0.22f, 0.32f, 2),
            Blob(P.FoxCream, 0.18f, 0.15f, 0.24f, 2).Translate(0, -0.07f, 0.05f))));

        // atki (rengi kiyafetle degisir: tint)
        Body.Add(new Node(MeshData.From(Part(TorusGeo(0.17f, 0.05f, 6, 14), "#ffffff", V(0, 0.12f, 0.2f), V(MathX.Pi / 2 - 0.35f, 0, 0)), false), ScarfMat));
        ScarfTail = new Node { Position = V(0.1f, 0.12f, 0.12f) };
        ScarfTail.Add(new Node(MeshData.From(Box("#d0d0d0", 0.07f, 0.03f, 0.2f, V(0, 0, -0.1f)), false), ScarfMat));
        Body.Add(ScarfTail);
        SetScarf(P.Scarf);

        Head = new Node { Position = V(0, 0.27f, 0.27f) };
        Head.Add(M(Merge(
            Sphere(P.FoxOrange, 0.24f, null, V(1, 0.92f, 0.95f), 16, 12),
            Blob(P.FoxCream, 0.12f, 0.085f, 0.1f, 2).Translate(-0.11f, -0.08f, 0.12f),
            Blob(P.FoxCream, 0.12f, 0.085f, 0.1f, 2).Translate(0.11f, -0.08f, 0.12f),
            Cone(P.FoxCream, 0.095f, 0.2f, V(0, -0.06f, 0.24f), V(MathX.Pi / 2, 0, 0), 10),
            Sphere(P.Nose, 0.036f, V(0, -0.045f, 0.335f), null, 8, 6),
            Sphere(P.Blush, 0.035f, V(-0.155f, -0.04f, 0.14f), V(1, 0.6f, 0.4f), 8, 6),
            Sphere(P.Blush, 0.035f, V(0.155f, -0.04f, 0.14f), V(1, 0.6f, 0.4f), 8, 6),
            Blob(P.FoxOrangeDark, 0.08f, 0.05f, 0.08f, 1).Translate(0, 0.2f, 0.06f))));
        Eyes = MakeEyes(0.095f, 0.035f, 0.195f);
        Head.Add(Eyes);
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1 : 1;
            var ear = new Node { Position = V(s * 0.13f, 0.16f, -0.02f), Rotation = V(0, 0, -s * 0.32f) };
            ear.Add(M(Merge(
                Cone(P.FoxOrange, 0.09f, 0.24f, V(0, 0.11f, 0), null, 6),
                Cone(P.FoxCream, 0.05f, 0.14f, V(0, 0.08f, 0.035f), null, 6),
                Cone(P.FoxDark, 0.045f, 0.08f, V(0, 0.2f, 0), null, 6))));
            Head.Add(ear);
            Ears[i] = ear;
        }
        HatSlot = new Node { Position = V(0, 0.2f, 0), Rotation = V(-0.12f, 0, 0) };
        FaceSlot = new Node { Position = V(0, 0.035f, 0.2f) };
        Head.Add(HatSlot);
        Head.Add(FaceSlot);
        Body.Add(Head);

        Tail = new Node { Position = V(0, 0.04f, -0.28f) };
        TailInner = new Node();
        Tail.Add(TailInner);
        TailInner.Add(M(Merge(
            Blob(P.FoxOrange, 0.13f, 0.13f, 0.24f, 2).RotateX(-0.7f).Translate(0, 0.1f, -0.16f),
            Blob(P.FoxCream, 0.1f, 0.1f, 0.13f, 2).RotateX(-0.9f).Translate(0, 0.25f, -0.33f))));
        Body.Add(Tail);

        BackSlot = new Node { Position = V(0, 0.16f, -0.08f) };
        Body.Add(BackSlot);

        float[][] legPos = { new[] { -0.12f, 0.17f }, new[] { 0.12f, 0.17f }, new[] { -0.12f, -0.15f }, new[] { 0.12f, -0.15f } };
        for (int i = 0; i < 4; i++)
        {
            var leg = new Node { Position = V(legPos[i][0], -0.12f, legPos[i][1]) };
            leg.Add(M(Merge(
                Cyl(P.FoxDark, 0.05f, 0.045f, 0.2f, V(0, -0.1f, 0), null, 7),
                Sphere(P.FoxDark, 0.06f, V(0, -0.21f, 0.02f), V(1, 0.7f, 1.2f), 8, 6))));
            Body.Add(leg);
            Legs[i] = leg;
        }

        // yaprak planor
        Glider = new Node { Position = V(0, 0.2f, 0.05f) };
        var leaf = new Shape2D().MoveTo(0, -0.55f).QuadTo(0.55f, -0.1f, 0, 0.6f).QuadTo(-0.55f, -0.1f, 0, -0.55f).Points();
        Glider.Add(new Node(MeshData.From(Merge(
            Cyl(P.LeafDark, 0.015f, 0.015f, 0.55f, V(0, 0.27f, 0), null, 5),
            Part(Extrude(leaf, 0.02f), P.Leaf, V(0, 0.56f, 0), V(-MathX.Pi / 2, 0, 0), V(1.1f, 1, 1)),
            Box(P.LeafDark, 0.03f, 0.03f, 1.05f, V(0, 0.585f, 0))), true), new Material { DoubleSided = true }));
        Glider.Visible = false;
        Glider.Scale = new Vector3(0.01f);
        Body.Add(Glider);
    }

    public void SetScarf(string hex) => ScarfMat.Tint = MathX.Hex(hex);

    public readonly Dictionary<OutfitSlot, string?> Worn = new();

    /// <summary>Kiyafet giy/cikar (null = bos). Atki renkleri tint ile uygulanir.</summary>
    public void SetOutfit(OutfitSlot slot, string? id)
    {
        Worn[slot] = id;
        if (slot == OutfitSlot.Scarf)
        {
            SetScarf(Outfits.Get(id)?.Color ?? P.Scarf);
            return;
        }
        var node = slot switch { OutfitSlot.Hat => HatSlot, OutfitSlot.Face => FaceSlot, _ => BackSlot };
        foreach (var c in node.Children.ToList()) node.Remove(c);
        if (id == null) return;
        var mesh = Outfits.Mesh(id);
        if (mesh != null) node.Add(new Node(mesh, Material.Char()));
    }

    public void Land(float impact) => _squashVel -= MathF.Min(6, impact * 0.35f);
    public void Jump() => _squashVel += 3.5f;
    public void DoFlip() => Flip = 1;

    public void Update(float dt, in FoxPose s)
    {
        _t += dt;
        float t = _t;
        float speed = s.Speed;

        _squashVel += (-_squash * 90 - _squashVel * 11) * dt;
        _squash += _squashVel * dt;
        float sq = _squash;
        Body.Scale = new Vector3(1 + sq * 0.5f, 1 - sq, 1 + sq * 0.5f);

        bool moving = s.Grounded && speed > 0.05f;
        _walk += dt * (6 + speed * 9) * (moving ? 1 : 0);
        float ph = _walk;
        float amp = moving ? MathF.Min(1, speed) * 0.9f : 0;

        float bodyY = 0.36f, pitch = 0, roll = 0;
        if (s.Climbing)
        {
            bodyY = 0.42f;
            pitch = -1.25f;
            float cp = s.ClimbPhase;
            for (int i = 0; i < 4; i++) Legs[i].Rotation = V(MathF.Sin(cp + _legPhase[i]) * 0.8f - (i < 2 ? 0.6f : -0.2f), 0, 0);
        }
        else if (s.Boating)
        {
            bodyY = 0.3f;
            for (int i = 0; i < 4; i++) Legs[i].Rotation = V(i < 2 ? -1.2f : 1.3f, 0, 0);
        }
        else if (s.Swimming)
        {
            bodyY = 0.2f + MathF.Sin(t * 3) * 0.02f;
            pitch = -0.25f;
            for (int i = 0; i < 4; i++) Legs[i].Rotation = V(MathF.Sin(t * 12 + _legPhase[i]) * 0.9f, 0, 0);
        }
        else if (!s.Grounded)
        {
            float tuck = s.Gliding ? 0.2f : MathX.Clamp(-s.Vy * 0.05f, -0.6f, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float x = s.Gliding ? (front ? -0.9f : 0.9f) : front ? -0.6f - tuck : 0.6f + tuck;
                Legs[i].Rotation = V(x, 0, 0);
            }
            pitch = s.Gliding ? 0.15f : MathX.Clamp(-s.Vy * 0.02f, -0.25f, 0.25f);
        }
        else
        {
            for (int i = 0; i < 4; i++) Legs[i].Rotation = V(MathF.Sin(ph + _legPhase[i]) * amp, 0, 0);
            bodyY += MathF.Abs(MathF.Sin(ph)) * 0.04f * amp;
            pitch = 0.06f * MathF.Min(1.4f, speed);
            roll = MathF.Sin(ph) * 0.04f * amp;
        }

        if (Flip > 0)
        {
            Flip = MathF.Max(0, Flip - dt / 0.38f);
            pitch += -(1 - Flip) * MathX.TwoPi;
        }

        float breathe = MathF.Sin(t * 2.2f) * 0.012f;
        Body.Position = V(0, bodyY + breathe, 0);
        Body.Rotation = V(pitch, 0, roll);

        float wag = moving ? MathF.Sin(ph * 0.5f) * 0.25f : MathF.Sin(t * 2.4f) * 0.45f;
        Tail.Rotation = V(s.Grounded ? -0.1f + speed * 0.35f : s.Gliding ? 0.6f : 0.2f, wag, 0);

        ScarfTail.Rotation = V(0.3f + (s.Grounded ? speed * 0.6f : 1.1f) + MathF.Sin(t * 9) * 0.08f * (0.3f + speed), MathF.Sin(t * 5) * 0.2f, 0);

        _blink -= dt;
        if (_blink < 0) _blink = 2 + (float)_r.NextDouble() * 3.5f;
        Eyes.Scale = new Vector3(1, _blink < 0.12f ? 0.12f : 1, 1);

        _earT -= dt;
        if (_earT < 0) _earT = 2 + (float)_r.NextDouble() * 4;
        float twitch = _earT < 0.2f ? MathF.Sin(_earT * 40) * 0.3f : 0;
        float earX = s.Grounded ? -speed * 0.25f : -0.5f;
        Ears[0].Rotation = V(earX, 0, 0.32f + twitch + (s.Grounded ? 0 : 0.35f));
        Ears[1].Rotation = V(earX, 0, -0.32f - twitch * 0.5f - (s.Grounded ? 0 : 0.35f));

        float targetYaw = 0;
        if (LookTarget is { } lt)
        {
            Root.UpdateWorld(Root.Parent?.World ?? Matrix4x4.Identity);
            var local = Root.WorldToLocal(lt);
            targetYaw = MathX.Clamp(MathF.Atan2(local.X, local.Z), -0.9f, 0.9f);
        }
        _lookYaw += (targetYaw - _lookYaw) * MathF.Min(1, dt * 6);
        Head.Rotation = V(s.Swimming ? 0.25f : 0, _lookYaw, 0);

        float gt = s.Gliding ? 1 : 0;
        _glider += (gt - _glider) * MathF.Min(1, dt * 12);
        Glider.Visible = _glider > 0.02f;
        Glider.Scale = new Vector3(MathF.Max(0.01f, _glider));
        Glider.Rotation = V(0, 0, MathF.Sin(t * 3) * 0.08f);
    }
}
