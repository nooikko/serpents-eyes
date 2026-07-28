using System.Buffers.Binary;
using System.Text;

namespace SerpentsEyes.Core.Internal;

/// <summary>Little-endian primitive reader over a byte buffer, tracking its own offset.</summary>
internal ref struct SpanReader(ReadOnlySpan<byte> data)
{
    /// <summary>
    /// Upper bound on a single FString. Real saves top out around 40 bytes; this only
    /// exists so a corrupt length field cannot drive a huge allocation or a wild read.
    /// </summary>
    public const int MaxPlausibleStringLength = 4096;

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
    /// <remarks>
    /// The single-byte branch decodes as Latin-1 rather than ASCII. Latin-1 is a bijection
    /// over all 256 byte values, so a high byte survives as the matching char instead of
    /// being replaced with '?' by the ASCII decoder fallback.
    /// </remarks>
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
            if (length > MaxPlausibleStringLength)
            {
                throw new SaveFormatException($"Implausible string length {length}", start);
            }
            if (length > Remaining)
            {
                throw new SaveFormatException($"String length {length} exceeds remaining data", start);
            }
            string value = Encoding.Latin1.GetString(_data.Slice(Position, length - 1));
            Position += length;
            return value;
        }

        long charCount = -(long)length;
        if (charCount > MaxPlausibleStringLength)
        {
            throw new SaveFormatException($"Implausible UTF-16 string length {charCount}", start);
        }
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

    /// <summary>Moves the read position, for speculative parsing that needs to rewind.</summary>
    public void Seek(int position)
    {
        if (position < 0 || position > _data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        Position = position;
    }

    private readonly void Require(int count)
    {
        if (Position + count > _data.Length)
        {
            throw new SaveFormatException($"Unexpected end of file: needed {count} byte(s)", Position);
        }
    }
}
