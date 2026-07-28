using System.Text;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Builds synthetic NG_SaveFormat_4 files byte by byte.
/// </summary>
/// <remarks>
/// The real fixtures are all well-formed in the same way, so they cannot exercise the encodings
/// the format permits but the game happens not to emit: NUL-only FStrings, zero-length FStrings,
/// non-ASCII single-byte payloads, and files that stop at the last record. Those are exactly the
/// cases where a naive serializer silently changes the byte count, so they need hand-built input.
/// </remarks>
internal sealed class SaveFileBuilder
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;

    public SaveFileBuilder()
    {
        _writer = new BinaryWriter(_stream);
    }

    public static byte[] Minimal() => new SaveFileBuilder().Header().Records().NoTrailer().ToArray();

    /// <summary>Writes a standard header. Any FString argument left null uses the observed real value.</summary>
    public SaveFileBuilder Header(byte[]? buildId = null, byte[]? formatId = null)
    {
        _writer.Write(522);
        _writer.Write(1013);
        _writer.Write((short)5);
        _writer.Write((short)5);
        _writer.Write((short)4);
        _writer.Write(new byte[] { 0x52, 0x3C, 0x00, 0x80 });
        _writer.Write(buildId ?? Ansi("++NinjaGarden+live"));
        _writer.Write((byte)3);
        _writer.Write(formatId ?? Ansi("/Script/NinjaGarden.NG_SaveFormat_4"));
        return this;
    }

    /// <summary>Writes the record count followed by each record's raw tag bytes and value.</summary>
    public SaveFileBuilder Records(params (byte[] Tag, int Value)[] records)
    {
        _writer.Write(records.Length);
        foreach ((byte[] tag, int value) in records)
        {
            _writer.Write(tag);
            _writer.Write(value);
        }
        return this;
    }

    /// <summary>Ends the file immediately after the last record, with no trailer at all.</summary>
    public SaveFileBuilder NoTrailer() => this;

    /// <summary>Writes a trailer with no run in progress: the leading int32 and then opaque bytes.</summary>
    public SaveFileBuilder TrailerWithoutRun(byte[] remainder)
    {
        _writer.Write(0);
        _writer.Write(remainder);
        return this;
    }

    /// <summary>Writes a full run trailer: map, position, loadout pairs, then padding and terminator.</summary>
    public SaveFileBuilder TrailerWithRun(byte[] mapName, params byte[][] loadoutStrings)
        => TrailerWithPendingTags([], mapName, loadoutStrings);

    /// <summary>
    /// Writes a run trailer preceded by a count-prefixed list of tag strings. Live saves carry
    /// one of these whenever something was unlocked during the run.
    /// </summary>
    public SaveFileBuilder TrailerWithPendingTags(byte[][] pendingTags, byte[] mapName, params byte[][] loadoutStrings)
    {
        _writer.Write(pendingTags.Length);
        foreach (byte[] tag in pendingTags)
        {
            _writer.Write(tag);
        }
        _writer.Write(mapName);
        _writer.Write(16240.1);
        _writer.Write(-2544.7);
        _writer.Write(3526.2);
        _writer.Write(73.0f);
        foreach (byte[] s in loadoutStrings)
        {
            _writer.Write(s);
        }
        _writer.Write(new byte[] { 0, 0, 0, 0 });
        _writer.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        return this;
    }

    public byte[] ToArray()
    {
        _writer.Flush();
        return _stream.ToArray();
    }

    /// <summary>A normal single-byte FString: int32 length including the NUL, payload, NUL.</summary>
    public static byte[] Ansi(string value)
    {
        byte[] payload = Encoding.Latin1.GetBytes(value);
        var result = new byte[4 + payload.Length + 1];
        BitConverter.GetBytes(payload.Length + 1).CopyTo(result, 0);
        payload.CopyTo(result, 4);
        return result;
    }

    /// <summary>A single-byte FString carrying raw bytes, including values above 0x7F.</summary>
    public static byte[] RawBytes(params byte[] payload)
    {
        var result = new byte[4 + payload.Length + 1];
        BitConverter.GetBytes(payload.Length + 1).CopyTo(result, 0);
        payload.CopyTo(result, 4);
        return result;
    }

    /// <summary>An FString whose length is 1: just the terminating NUL, decoding to "".</summary>
    public static byte[] NulOnly() => [1, 0, 0, 0, 0];

    /// <summary>An FString whose length is 0: no payload at all, also decoding to "".</summary>
    public static byte[] ZeroLength() => [0, 0, 0, 0];

    /// <summary>A UTF-16LE FString: negative int32 char count including the NUL, then the chars.</summary>
    public static byte[] Wide(string value)
    {
        byte[] payload = Encoding.Unicode.GetBytes(value);
        var result = new byte[4 + payload.Length + 2];
        BitConverter.GetBytes(-(value.Length + 1)).CopyTo(result, 0);
        payload.CopyTo(result, 4);
        return result;
    }
}
