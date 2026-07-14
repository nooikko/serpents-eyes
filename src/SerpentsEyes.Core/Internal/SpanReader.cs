using System.Buffers.Binary;
using System.Text;

namespace SerpentsEyes.Core.Internal;

/// <summary>Little-endian primitive reader over a byte buffer, tracking its own offset.</summary>
internal ref struct SpanReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    public int Position { get; private set; }

    public readonly int Remaining => _data.Length - Position;

    public byte ReadByte()
    {
        Require(1);
        return _data[Position++];
    }

    public byte[] ReadBytes(int count)
    {
        Require(count);
        byte[] result = _data.Slice(Position, count).ToArray();
        Position += count;
        return result;
    }

    public short ReadInt16()
    {
        Require(2);
        short value = BinaryPrimitives.ReadInt16LittleEndian(_data[Position..]);
        Position += 2;
        return value;
    }

    public int ReadInt32()
    {
        Require(4);
        int value = BinaryPrimitives.ReadInt32LittleEndian(_data[Position..]);
        Position += 4;
        return value;
    }

    public float ReadSingle()
    {
        Require(4);
        float value = BinaryPrimitives.ReadSingleLittleEndian(_data[Position..]);
        Position += 4;
        return value;
    }

    public double ReadDouble()
    {
        Require(8);
        double value = BinaryPrimitives.ReadDoubleLittleEndian(_data[Position..]);
        Position += 8;
        return value;
    }

    /// <summary>
    /// Reads an Unreal-style FString: int32 length including the trailing NUL, then bytes.
    /// A negative length means UTF-16LE with -length characters (including NUL).
    /// </summary>
    public string ReadFString()
    {
        int start = Position;
        int length = ReadInt32();
        if (length == 0)
        {
            return string.Empty;
        }

        if (length > 0)
        {
            if (length > Remaining)
            {
                throw new SaveFormatException($"String length {length} exceeds remaining data", start);
            }
            string value = Encoding.ASCII.GetString(_data.Slice(Position, length - 1));
            Position += length;
            return value;
        }

        long charCount = -(long)length;
        long byteCountLong = charCount * 2;
        if (byteCountLong > Remaining)
        {
            throw new SaveFormatException($"UTF-16 string length {charCount} exceeds remaining data", start);
        }
        int byteCount = (int)byteCountLong;
        string wide = Encoding.Unicode.GetString(_data.Slice(Position, byteCount - 2));
        Position += byteCount;
        return wide;
    }

    /// <summary>Peeks the int32 at the current position without advancing, or null if fewer than 4 bytes remain.</summary>
    public readonly int? PeekInt32()
        => Remaining >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(_data[Position..]) : null;

    public byte[] ReadRemaining() => ReadBytes(Remaining);

    private readonly void Require(int count)
    {
        if (Position + count > _data.Length)
        {
            throw new SaveFormatException($"Unexpected end of file: needed {count} byte(s)", Position);
        }
    }
}
