using System.Numerics;
using System.Text;

namespace PilavciSimulator.Net;

/// <summary>
/// Ikili yazici. Hem ag mesajlari hem kayit dosyasi ayni bicimi kullanir:
/// tek bir serilestirici, tek bir gidis-donus testi.
/// </summary>
public sealed class NetWriter
{
    private byte[] _buf;
    private int _pos;

    public NetWriter(int capacity = 1024) => _buf = new byte[capacity];

    public int Length => _pos;
    public ReadOnlySpan<byte> Span => _buf.AsSpan(0, _pos);
    public byte[] ToArray() => _buf.AsSpan(0, _pos).ToArray();
    public void Reset() => _pos = 0;

    private void Ensure(int n)
    {
        if (_pos + n <= _buf.Length)
        {
            return;
        }

        var size = _buf.Length * 2;
        while (size < _pos + n)
        {
            size *= 2;
        }

        Array.Resize(ref _buf, size);
    }

    public void Byte(byte v)
    {
        Ensure(1);
        _buf[_pos++] = v;
    }

    public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);

    public void Short(short v)
    {
        Ensure(2);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 2;
    }

    public void UShort(ushort v)
    {
        Ensure(2);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 2;
    }

    public void Int(int v)
    {
        Ensure(4);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 4;
    }

    public void UInt(uint v)
    {
        Ensure(4);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 4;
    }

    public void Long(long v)
    {
        Ensure(8);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 8;
    }

    public void ULong(ulong v)
    {
        Ensure(8);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 8;
    }

    public void Float(float v)
    {
        Ensure(4);
        BitConverter.TryWriteBytes(_buf.AsSpan(_pos), v);
        _pos += 4;
    }

    /// <summary>Degiskenuzunluklu pozitif tamsayi (kucuk sayilar 1 bayt).</summary>
    public void VarInt(int v)
    {
        var u = (uint)v;
        while (u >= 0x80)
        {
            Byte((byte)(u | 0x80));
            u >>= 7;
        }

        Byte((byte)u);
    }

    public void Vec3(Vector3 v)
    {
        Float(v.X);
        Float(v.Y);
        Float(v.Z);
    }

    /// <summary>Aci (radyan) 16 bit.</summary>
    public void Angle(float radians)
    {
        var t = radians / MathF.Tau;
        t -= MathF.Floor(t);
        UShort((ushort)(t * 65535f));
    }

    /// <summary>0..1 araligi 8 bit.</summary>
    public void Unit8(float v) => Byte((byte)Math.Clamp(v * 255f + 0.5f, 0f, 255f));

    public void String(string? s)
    {
        if (s is null)
        {
            VarInt(0);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(s);
        VarInt(bytes.Length + 1);
        Ensure(bytes.Length);
        bytes.CopyTo(_buf, _pos);
        _pos += bytes.Length;
    }

    public void Bytes(ReadOnlySpan<byte> data)
    {
        VarInt(data.Length);
        Ensure(data.Length);
        data.CopyTo(_buf.AsSpan(_pos));
        _pos += data.Length;
    }
}

public sealed class NetReader
{
    private readonly byte[] _buf;
    private int _pos;
    private readonly int _end;

    public NetReader(byte[] data, int offset = 0, int length = -1)
    {
        _buf = data;
        _pos = offset;
        _end = length < 0 ? data.Length : offset + length;
    }

    public NetReader(ReadOnlyMemory<byte> data) : this(data.ToArray())
    {
    }

    public bool AtEnd => _pos >= _end;
    public int Remaining => _end - _pos;

    private void Need(int n)
    {
        if (_pos + n > _end)
        {
            throw new EndOfStreamException("mesaj beklenenden kisa");
        }
    }

    public byte Byte()
    {
        Need(1);
        return _buf[_pos++];
    }

    public bool Bool() => Byte() != 0;

    public short Short()
    {
        Need(2);
        var v = BitConverter.ToInt16(_buf, _pos);
        _pos += 2;
        return v;
    }

    public ushort UShort()
    {
        Need(2);
        var v = BitConverter.ToUInt16(_buf, _pos);
        _pos += 2;
        return v;
    }

    public int Int()
    {
        Need(4);
        var v = BitConverter.ToInt32(_buf, _pos);
        _pos += 4;
        return v;
    }

    public uint UInt()
    {
        Need(4);
        var v = BitConverter.ToUInt32(_buf, _pos);
        _pos += 4;
        return v;
    }

    public long Long()
    {
        Need(8);
        var v = BitConverter.ToInt64(_buf, _pos);
        _pos += 8;
        return v;
    }

    public ulong ULong()
    {
        Need(8);
        var v = BitConverter.ToUInt64(_buf, _pos);
        _pos += 8;
        return v;
    }

    public float Float()
    {
        Need(4);
        var v = BitConverter.ToSingle(_buf, _pos);
        _pos += 4;
        return v;
    }

    public int VarInt()
    {
        uint result = 0;
        var shift = 0;
        while (true)
        {
            var b = Byte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                break;
            }

            shift += 7;
            if (shift > 28)
            {
                throw new InvalidDataException("bozuk VarInt");
            }
        }

        return (int)result;
    }

    public Vector3 Vec3() => new(Float(), Float(), Float());

    public float Angle() => UShort() / 65535f * MathF.Tau;

    public float Unit8() => Byte() / 255f;

    public string? String()
    {
        var n = VarInt();
        if (n == 0)
        {
            return null;
        }

        n--;
        Need(n);
        var s = Encoding.UTF8.GetString(_buf, _pos, n);
        _pos += n;
        return s;
    }

    public string Str() => String() ?? "";

    public byte[] Bytes()
    {
        var n = VarInt();
        Need(n);
        var r = _buf.AsSpan(_pos, n).ToArray();
        _pos += n;
        return r;
    }
}
