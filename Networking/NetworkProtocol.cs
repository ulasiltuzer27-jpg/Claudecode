using Microsoft.Xna.Framework;

namespace PixelSurvival.Networking;

/// <summary>Kablo üzerindeki mesaj türleri. Değerler DEĞİŞTİRİLMEZ — protokol sabiti.</summary>
public enum MessageType : byte
{
    Welcome = 1,
    ClientInput = 2,
    Snapshot = 3,
    TileChange = 4,
    PlayerLeft = 5,
    InventoryDelta = 6,

    /// <summary>Madde 12: dünya saati ve hava. Host otoriter.</summary>
    WorldTime = 7
}

/// <summary>Snapshot içindeki tek bir oyuncunun durumu.</summary>
public readonly record struct PlayerState(
    byte PlayerId,
    float X,
    float Y,
    byte Facing,
    byte Health,
    byte Flags)
{
    public const byte FlagMoving = 1 << 0;
    public const byte FlagGathering = 1 << 1;
    public const byte FlagAttacking = 1 << 2;
    public const byte FlagDead = 1 << 3;

    public bool IsMoving => (Flags & FlagMoving) != 0;
    public bool IsGathering => (Flags & FlagGathering) != 0;
    public bool IsAttacking => (Flags & FlagAttacking) != 0;
    public bool IsDead => (Flags & FlagDead) != 0;
}

/// <summary>Bir istemcinin tek tick'lik girdisi.</summary>
public readonly record struct InputFrame(uint Tick, float MoveX, float MoveY, byte Flags)
{
    public const byte FlagGather = 1 << 0;
    public const byte FlagAttack = 1 << 1;
    public const byte FlagBuild = 1 << 2;

    public bool Gather => (Flags & FlagGather) != 0;
    public bool Attack => (Flags & FlagAttack) != 0;
    public bool Build => (Flags & FlagBuild) != 0;
}

/// <summary>
/// AŞAMA 1 / MADDE 10 — kablo protokolü.
///
/// Kodlama AÇIKÇA yazıldı; <c>BinaryWriter</c>/<c>BinaryReader</c> yerine
/// elle bayt yazımı tercih edildi ki bayt düzeni platformdan ve .NET
/// sürümünden bağımsız olsun ve başka bir dilde birebir yeniden
/// üretilebilsin. <c>Tools/verify_protocol.py</c> tam olarak bunu yapıyor:
/// aynı düzeni bağımsız olarak çözüp gidiş-dönüş testi uyguluyor.
///
/// Tüm çok baytlı alanlar LITTLE-ENDIAN.
///
/// Hareket vektörü float olarak DEĞİL, -100..100 aralığında sbyte olarak
/// gider: iki eksen için 8 bayt yerine 2 bayt. Saniyede 20 tick ve çok
/// oyuncuyla bu fark birikir; hassasiyet kaybı (%1) hareket için önemsiz.
/// </summary>
public static class NetworkProtocol
{
    /// <summary>Sunucu tick hızı. 50 ms.</summary>
    public const int TicksPerSecond = 20;

    public const int DefaultPort = 7777;

    /// <summary>Uyumsuz sürümlerin sessizce garip davranmasını engeller.</summary>
    public const byte ProtocolVersion = 1;

    // ---------------- yazma ----------------

    public static byte[] WriteWelcome(byte assignedId, int worldSeed)
    {
        var buffer = new byte[7];
        buffer[0] = (byte)MessageType.Welcome;
        buffer[1] = ProtocolVersion;
        buffer[2] = assignedId;
        WriteInt32(buffer, 3, worldSeed);
        return buffer;
    }

    public static byte[] WriteClientInput(InputFrame input)
    {
        var buffer = new byte[8];
        buffer[0] = (byte)MessageType.ClientInput;
        WriteUInt32(buffer, 1, input.Tick);
        buffer[5] = (byte)(sbyte)Math.Clamp(MathF.Round(input.MoveX * 100f), -100f, 100f);
        buffer[6] = (byte)(sbyte)Math.Clamp(MathF.Round(input.MoveY * 100f), -100f, 100f);
        buffer[7] = input.Flags;
        return buffer;
    }

    public static byte[] WriteSnapshot(uint tick, IReadOnlyList<PlayerState> players)
    {
        // 1 tür + 4 tick + 1 sayı + oyuncu başına 12 bayt
        var buffer = new byte[6 + players.Count * 12];
        buffer[0] = (byte)MessageType.Snapshot;
        WriteUInt32(buffer, 1, tick);
        buffer[5] = (byte)players.Count;

        var offset = 6;
        foreach (var player in players)
        {
            buffer[offset] = player.PlayerId;
            WriteSingle(buffer, offset + 1, player.X);
            WriteSingle(buffer, offset + 5, player.Y);
            buffer[offset + 9] = player.Facing;
            buffer[offset + 10] = player.Health;
            buffer[offset + 11] = player.Flags;
            offset += 12;
        }

        return buffer;
    }

    public static byte[] WriteTileChange(int tileX, int tileY, ushort tileIndex)
    {
        var buffer = new byte[11];
        buffer[0] = (byte)MessageType.TileChange;
        WriteInt32(buffer, 1, tileX);
        WriteInt32(buffer, 5, tileY);
        WriteUInt16(buffer, 9, tileIndex);
        return buffer;
    }

    public static byte[] WritePlayerLeft(byte playerId) =>
        [(byte)MessageType.PlayerLeft, playerId];

    public static byte[] WriteInventoryDelta(string itemId, int amount)
    {
        var name = System.Text.Encoding.UTF8.GetBytes(itemId);
        if (name.Length > 255)
        {
            throw new ArgumentException("item kimliği 255 bayttan uzun olamaz", nameof(itemId));
        }

        var buffer = new byte[6 + name.Length];
        buffer[0] = (byte)MessageType.InventoryDelta;
        WriteInt32(buffer, 1, amount);
        buffer[5] = (byte)name.Length;
        name.CopyTo(buffer, 6);
        return buffer;
    }

    /// <summary>
    /// Dünya saati ve hava. Saniyede bir kez yeterli — saat sürekli akıyor,
    /// her tick göndermenin faydası yok.
    /// </summary>
    public static byte[] WriteWorldTime(double worldSeconds, string weatherKey)
    {
        var key = System.Text.Encoding.UTF8.GetBytes(weatherKey);
        if (key.Length > 255)
        {
            throw new ArgumentException("hava anahtarı çok uzun", nameof(weatherKey));
        }

        var buffer = new byte[10 + key.Length];
        buffer[0] = (byte)MessageType.WorldTime;
        WriteUInt64(buffer, 1, BitConverter.DoubleToUInt64Bits(worldSeconds));
        buffer[9] = (byte)key.Length;
        key.CopyTo(buffer, 10);
        return buffer;
    }

    // ---------------- okuma ----------------

    public static MessageType PeekType(ReadOnlySpan<byte> data) =>
        data.Length == 0 ? 0 : (MessageType)data[0];

    public static bool TryReadWelcome(ReadOnlySpan<byte> data, out byte assignedId, out int seed)
    {
        assignedId = 0;
        seed = 0;

        if (data.Length < 7 || data[1] != ProtocolVersion)
        {
            return false;
        }

        assignedId = data[2];
        seed = ReadInt32(data, 3);
        return true;
    }

    public static bool TryReadClientInput(ReadOnlySpan<byte> data, out InputFrame input)
    {
        input = default;
        if (data.Length < 8)
        {
            return false;
        }

        input = new InputFrame(
            ReadUInt32(data, 1),
            (sbyte)data[5] / 100f,
            (sbyte)data[6] / 100f,
            data[7]);
        return true;
    }

    public static bool TryReadSnapshot(ReadOnlySpan<byte> data, out uint tick,
                                       out List<PlayerState> players)
    {
        tick = 0;
        players = [];

        if (data.Length < 6)
        {
            return false;
        }

        tick = ReadUInt32(data, 1);
        var count = data[5];

        if (data.Length < 6 + count * 12)
        {
            return false;
        }

        var offset = 6;
        for (var i = 0; i < count; i++)
        {
            players.Add(new PlayerState(
                data[offset],
                ReadSingle(data, offset + 1),
                ReadSingle(data, offset + 5),
                data[offset + 9],
                data[offset + 10],
                data[offset + 11]));
            offset += 12;
        }

        return true;
    }

    public static bool TryReadTileChange(ReadOnlySpan<byte> data, out Point tile, out ushort index)
    {
        tile = Point.Zero;
        index = 0;

        if (data.Length < 11)
        {
            return false;
        }

        tile = new Point(ReadInt32(data, 1), ReadInt32(data, 5));
        index = ReadUInt16(data, 9);
        return true;
    }

    public static bool TryReadPlayerLeft(ReadOnlySpan<byte> data, out byte playerId)
    {
        playerId = 0;
        if (data.Length < 2)
        {
            return false;
        }

        playerId = data[1];
        return true;
    }

    public static bool TryReadInventoryDelta(ReadOnlySpan<byte> data, out string itemId,
                                             out int amount)
    {
        itemId = "";
        amount = 0;

        if (data.Length < 6)
        {
            return false;
        }

        amount = ReadInt32(data, 1);
        var length = data[5];

        if (data.Length < 6 + length)
        {
            return false;
        }

        itemId = System.Text.Encoding.UTF8.GetString(data.Slice(6, length));
        return true;
    }

    public static bool TryReadWorldTime(ReadOnlySpan<byte> data, out double worldSeconds,
                                        out string weatherKey)
    {
        worldSeconds = 0;
        weatherKey = "";

        if (data.Length < 10)
        {
            return false;
        }

        worldSeconds = BitConverter.UInt64BitsToDouble(ReadUInt64(data, 1));
        var length = data[9];

        if (data.Length < 10 + length)
        {
            return false;
        }

        weatherKey = System.Text.Encoding.UTF8.GetString(data.Slice(10, length));
        return true;
    }

    // ---------------- little-endian temel işlemler ----------------

    private static void WriteUInt16(byte[] b, int i, ushort v)
    {
        b[i] = (byte)v;
        b[i + 1] = (byte)(v >> 8);
    }

    private static void WriteInt32(byte[] b, int i, int v) => WriteUInt32(b, i, (uint)v);

    private static void WriteUInt32(byte[] b, int i, uint v)
    {
        b[i] = (byte)v;
        b[i + 1] = (byte)(v >> 8);
        b[i + 2] = (byte)(v >> 16);
        b[i + 3] = (byte)(v >> 24);
    }

    private static void WriteUInt64(byte[] b, int i, ulong v)
    {
        for (var k = 0; k < 8; k++)
        {
            b[i + k] = (byte)(v >> (k * 8));
        }
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> b, int i)
    {
        ulong v = 0;
        for (var k = 0; k < 8; k++)
        {
            v |= (ulong)b[i + k] << (k * 8);
        }

        return v;
    }

    private static void WriteSingle(byte[] b, int i, float v) =>
        WriteUInt32(b, i, BitConverter.SingleToUInt32Bits(v));

    private static ushort ReadUInt16(ReadOnlySpan<byte> b, int i) =>
        (ushort)(b[i] | (b[i + 1] << 8));

    private static int ReadInt32(ReadOnlySpan<byte> b, int i) => (int)ReadUInt32(b, i);

    private static uint ReadUInt32(ReadOnlySpan<byte> b, int i) =>
        (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));

    private static float ReadSingle(ReadOnlySpan<byte> b, int i) =>
        BitConverter.UInt32BitsToSingle(ReadUInt32(b, i));
}
