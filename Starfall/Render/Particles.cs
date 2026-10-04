using System.Numerics;
using Starfall.Core;

namespace Starfall.Render;

/// <summary>Bir parcacik patlamasinin tarifi (JS'teki emit() secenekleri).</summary>
public struct Emit
{
    public Vector3 Pos;
    public int Count;
    public Vector3 Spread;
    public Vector3 Vel;
    public float VelSpread;
    public float Radial;
    public float Life;
    public float Size;
    public float SizeEnd;
    public Vector3[] Colors;
    public float Gravity;
    public float Drag;
    public float Alpha;
    public float Wobble;

    public static Emit At(Vector3 pos, int count, params string[] colors) => new()
    {
        Pos = pos, Count = count, Life = 1, Size = 0.3f, SizeEnd = -1, Alpha = 1,
        Colors = colors.Select(MathX.Hex).ToArray(),
    };

    public Emit Sp(float s) { Spread = new Vector3(s); return this; }
    public Emit Sp(float x, float y, float z) { Spread = new Vector3(x, y, z); return this; }
    public Emit V(float x, float y, float z, float spread = 0) { Vel = new Vector3(x, y, z); VelSpread = spread; return this; }
    public Emit R(float radial) { Radial = radial; return this; }
    public Emit L(float life) { Life = life; return this; }
    public Emit S(float size, float end = -1) { Size = size; SizeEnd = end; return this; }
    public Emit G(float g) { Gravity = g; return this; }
    public Emit D(float d) { Drag = d; return this; }
    public Emit A(float a) { Alpha = a; return this; }
    public Emit W(float w) { Wobble = w; return this; }
}

/// <summary>Havuzlu parcacik sistemi. Iki ornek: toplamali (parilti, ates) ve normal (toz, duman).</summary>
public sealed class ParticlePool
{
    public readonly bool Additive;
    public readonly int Max;
    public int Count;
    private readonly Vector3[] _pos, _vel, _col;
    private readonly float[] _life, _max, _s0, _s1, _grav, _drag, _a0, _wob, _size, _alpha;
    private readonly Random _r = new(7);
    /// <summary>GPU'ya giden ara bellek: pos3 + rgba + boyut = 8 float.</summary>
    public readonly float[] Packed;

    public ParticlePool(int max, bool additive)
    {
        Max = max;
        Additive = additive;
        _pos = new Vector3[max]; _vel = new Vector3[max]; _col = new Vector3[max];
        _life = new float[max]; _max = new float[max]; _s0 = new float[max]; _s1 = new float[max];
        _grav = new float[max]; _drag = new float[max]; _a0 = new float[max]; _wob = new float[max];
        _size = new float[max]; _alpha = new float[max];
        Packed = new float[max * 8];
    }

    private float R() => (float)_r.NextDouble();

    public void Emit(in Emit o)
    {
        int n = Math.Max(1, o.Count);
        for (int k = 0; k < n; k++)
        {
            if (Count >= Max) return;
            int i = Count++;
            _pos[i] = o.Pos + new Vector3((R() - 0.5f) * 2 * o.Spread.X, (R() - 0.5f) * 2 * o.Spread.Y, (R() - 0.5f) * 2 * o.Spread.Z);
            var v = o.Vel + new Vector3((R() - 0.5f) * 2 * o.VelSpread, (R() - 0.5f) * 2 * o.VelSpread, (R() - 0.5f) * 2 * o.VelSpread);
            if (o.Radial > 0)
            {
                float a = R() * MathX.TwoPi;
                float b = MathF.Acos(2 * R() - 1);
                float s = o.Radial * (0.6f + R() * 0.4f);
                v += new Vector3(MathF.Sin(b) * MathF.Cos(a), MathF.Cos(b), MathF.Sin(b) * MathF.Sin(a)) * s;
            }
            _vel[i] = v;
            _col[i] = o.Colors == null || o.Colors.Length == 0 ? Vector3.One : o.Colors[_r.Next(o.Colors.Length)];
            float life = o.Life * (0.7f + R() * 0.6f);
            _life[i] = life;
            _max[i] = life;
            _s0[i] = o.Size * (0.7f + R() * 0.6f);
            _s1[i] = o.SizeEnd < 0 ? _s0[i] : o.SizeEnd;
            _grav[i] = o.Gravity;
            _drag[i] = o.Drag;
            _a0[i] = o.Alpha <= 0 ? 1 : o.Alpha;
            _wob[i] = o.Wobble;
        }
    }

    public void Update(float dt)
    {
        int i = 0;
        while (i < Count)
        {
            _life[i] -= dt;
            if (_life[i] <= 0) { Kill(i); continue; }
            float t = 1 - _life[i] / _max[i];
            float d = MathF.Max(0, 1 - _drag[i] * dt);
            var v = _vel[i];
            v.X *= d; v.Z *= d;
            v.Y = v.Y * d + _grav[i] * dt;
            if (_wob[i] != 0)
            {
                v.X += MathF.Sin(_life[i] * 5 + i) * _wob[i] * dt;
                v.Z += MathF.Cos(_life[i] * 4 + i * 1.3f) * _wob[i] * dt;
            }
            _vel[i] = v;
            _pos[i] += v * dt;
            _size[i] = _s0[i] + (_s1[i] - _s0[i]) * t;
            float fadeIn = MathF.Min(1, t * 8);
            _alpha[i] = _a0[i] * fadeIn * (1 - t * t);
            int o = i * 8;
            Packed[o] = _pos[i].X; Packed[o + 1] = _pos[i].Y; Packed[o + 2] = _pos[i].Z;
            Packed[o + 3] = _col[i].X; Packed[o + 4] = _col[i].Y; Packed[o + 5] = _col[i].Z;
            Packed[o + 6] = _alpha[i]; Packed[o + 7] = _size[i];
            i++;
        }
    }

    private void Kill(int i)
    {
        int last = --Count;
        if (i == last) return;
        _pos[i] = _pos[last]; _vel[i] = _vel[last]; _col[i] = _col[last];
        _life[i] = _life[last]; _max[i] = _max[last]; _s0[i] = _s0[last]; _s1[i] = _s1[last];
        _grav[i] = _grav[last]; _drag[i] = _drag[last]; _a0[i] = _a0[last]; _wob[i] = _wob[last];
        _size[i] = _size[last]; _alpha[i] = _alpha[last];
    }

    public void Clear() => Count = 0;
}
