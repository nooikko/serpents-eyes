using System.Text;

namespace SerpentsEyes.Core.Internal;

/// <summary>
/// Writes a <see cref="SaveProfile"/> back to the on-disk format.
/// </summary>
/// <remarks>
/// Every region is emitted one of two ways. If the region is unchanged since parsing and its
/// original bytes are available, those bytes are copied straight through — that is what makes
/// an untouched profile serialize byte-identically regardless of how the game encoded it.
/// Otherwise the region is re-encoded canonically. See <see cref="SourceLayout"/>.
/// </remarks>
internal static class SaveSerializer
{
    public static byte[] Serialize(SaveProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream); // BinaryWriter is little-endian

        SourceLayout? layout = profile.SourceLayout;

        WriteHeader(writer, profile.Header, layout);

        writer.Write(profile.Records.Count);
        foreach (TagRecord record in profile.Records)
        {
            WriteRecord(writer, record, layout);
        }

        WriteTrailer(writer, profile.RunSnapshot, layout);

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteHeader(BinaryWriter writer, SaveHeader header, SourceLayout? layout)
    {
        if (layout is not null && header.ValueEquals(layout.Header))
        {
            writer.Write(layout.Bytes, 0, layout.HeaderLength);
            return;
        }

        writer.Write(header.Unknown1);
        writer.Write(header.Unknown2);
        writer.Write(header.VersionA);
        writer.Write(header.VersionB);
        writer.Write(header.VersionC);
        if (header.Unknown3.Length != 4)
        {
            throw new SaveFormatException($"Header.Unknown3 must be exactly 4 bytes, got {header.Unknown3.Length}");
        }
        writer.Write(header.Unknown3);
        WriteFString(writer, header.BuildId);
        writer.Write(header.Unknown4);
        WriteFString(writer, header.FormatId);
    }

    private static void WriteRecord(BinaryWriter writer, TagRecord record, SourceLayout? layout)
    {
        // The tag is immutable, so its original bytes are always safe to reuse when we have
        // them. Only the value is rewritten, and only when the caller actually changed it.
        if (layout is not null && record.HasTagSource)
        {
            writer.Write(layout.Bytes, record.TagSourceStart, record.TagSourceLength);
        }
        else
        {
            WriteFString(writer, record.FullTag);
        }

        writer.Write(record.Value);
    }

    private static void WriteTrailer(BinaryWriter writer, RunSnapshot snapshot, SourceLayout? layout)
    {
        if (layout is not null && snapshot.ValueEquals(layout.Trailer))
        {
            writer.Write(layout.Bytes, layout.TrailerStart, layout.Bytes.Length - layout.TrailerStart);
            return;
        }

        if (!snapshot.HasTrailer)
        {
            // The file ended with the last record. Do not invent a trailer.
            return;
        }

        if (snapshot.HasPositionData)
        {
            writer.Write(snapshot.PendingTags.Count);
            foreach (string tag in snapshot.PendingTags)
            {
                WriteFString(writer, tag);
            }
            WriteFString(writer, snapshot.MapName);
            writer.Write(snapshot.X);
            writer.Write(snapshot.Y);
            writer.Write(snapshot.Z);
            writer.Write(snapshot.Unknown2);
            foreach (LoadoutEntry entry in snapshot.Loadout)
            {
                WriteFString(writer, entry.SlotType);
                if (entry.HasId)
                {
                    WriteFString(writer, entry.Id);
                }
            }
        }
        writer.Write(snapshot.Remainder);
    }

    /// <summary>
    /// Writes an Unreal-style FString: int32 length including trailing NUL, then bytes.
    /// Single-byte encoding is Latin-1, matching <see cref="SpanReader.ReadFString"/>.
    /// </summary>
    private static void WriteFString(BinaryWriter writer, string value)
    {
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        bool isSingleByte = true;
        foreach (char c in value)
        {
            if (c > 0xFF)
            {
                isSingleByte = false;
                break;
            }
        }

        if (isSingleByte)
        {
            writer.Write(value.Length + 1);
            writer.Write(Encoding.Latin1.GetBytes(value));
            writer.Write((byte)0);
        }
        else
        {
            writer.Write(-(value.Length + 1));
            writer.Write(Encoding.Unicode.GetBytes(value));
            writer.Write((short)0);
        }
    }
}
