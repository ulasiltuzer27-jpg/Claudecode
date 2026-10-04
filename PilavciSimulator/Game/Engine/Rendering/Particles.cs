using System.Numerics;
using Raylib_cs;

namespace PilavciSimulator.Engine.Rendering;

public enum ParticleBlend : byte
{
    Alpha,
    Additive,
}

public struct Particle
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Age;
    public float Life;
    public float Size0;
    public float Size1;
    public Color Color0;
    public Color Color1;
    public float Gravity;
    public float Drag;
    public ParticleBlend Blend;
    public bool Streak;
}

/// <summary>
/// CPU parcaciklari: kazan buhari, ocak alevi, duman, yagmur, para
/// pariltisi. Kameraya donuk dortgenler olarak raylib'in anlik kipi
/// (rlgl) ile cizilir; derinlik testi acik, derinlik yazimi kapali.
/// Yagmur "cizgi" (streak) olarak hiz yonunde uzatilir.
/// </summary>
public sealed class ParticleSystem
{
    private Particle[] _items = new Particle[4096];
    private int _count;
    private readonly Texture2D _texture;

    public ParticleSystem(Texture2D softDot) => _texture = softDot;

    public int Count => _count;

    public void Emit(in Particle p)
    {
        if (_count >= _items.Length)
        {
            if (_items.Length >= 32768)
            {
                return;
            }

            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[_count++] = p;
    }

    public void Update(float dt)
    {
        for (var i = 0; i < _count; i++)
        {
            ref var p = ref _items[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _items[i] = _items[--_count];
                i--;
                continue;
            }

            p.Velocity.Y -= p.Gravity * dt;
            if (p.Drag > 0)
            {
                p.Velocity *= MathF.Max(0f, 1f - p.Drag * dt);
            }

            p.Position += p.Velocity * dt;
        }
    }

    public void Clear() => _count = 0;

    /// <summary>BeginMode3D icinde, kamera matrisleri kuruluyken cagrilir.</summary>
    public void Draw(in CameraView cam)
    {
        if (_count == 0)
        {
            return;
        }

        var forward = cam.Forward;
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);

        Rlgl.DisableDepthMask();
        Rlgl.DisableBackfaceCulling();
        DrawPass(ParticleBlend.Alpha, cam.Position, right, up);
        Raylib.BeginBlendMode(BlendMode.Additive);
        DrawPass(ParticleBlend.Additive, cam.Position, right, up);
        Raylib.EndBlendMode();
        Rlgl.EnableBackfaceCulling();
        Rlgl.EnableDepthMask();
    }

    private void DrawPass(ParticleBlend blend, Vector3 camPos, Vector3 right, Vector3 up)
    {
        Rlgl.SetTexture(_texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        for (var i = 0; i < _count; i++)
        {
            ref var p = ref _items[i];
            if (p.Blend != blend)
            {
                continue;
            }

            var t = p.Age / p.Life;
            var size = p.Size0 + (p.Size1 - p.Size0) * t;
            var c = Gfx.Lerp(p.Color0, p.Color1, t);
            Vector3 a, b, cc, d;
            if (p.Streak && p.Velocity.LengthSquared() > 0.01f)
            {
                var dir = Vector3.Normalize(p.Velocity);
                var side = Vector3.Normalize(Vector3.Cross(dir, p.Position - camPos)) * size * 0.08f;
                var len = dir * size;
                a = p.Position - side - len;
                b = p.Position + side - len;
                cc = p.Position + side;
                d = p.Position - side;
            }
            else
            {
                var r = right * size;
                var u = up * size;
                a = p.Position - r - u;
                b = p.Position + r - u;
                cc = p.Position + r + u;
                d = p.Position - r + u;
            }

            Rlgl.Color4ub(c.R, c.G, c.B, c.A);
            Rlgl.TexCoord2f(0, 1);
            Rlgl.Vertex3f(a.X, a.Y, a.Z);
            Rlgl.TexCoord2f(1, 1);
            Rlgl.Vertex3f(b.X, b.Y, b.Z);
            Rlgl.TexCoord2f(1, 0);
            Rlgl.Vertex3f(cc.X, cc.Y, cc.Z);
            Rlgl.TexCoord2f(0, 0);
            Rlgl.Vertex3f(d.X, d.Y, d.Z);
        }

        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Rlgl.SetTexture(0);
    }
}
