using System.Text;

namespace SerpentsEyes.Core.Internal;

internal static class SaveSerializer
{
    public static byte[] Serialize(SaveProfile profile)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream); // BinaryWriter is little-endian

        SaveHeader header = profile.Header;
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

        writer.Write(profile.Records.Count);
        foreach (TagRecord record in profile.Records)
        {
            WriteFString(writer, record.FullTag);
            writer.Write(record.Value);
        }

        RunSnapshot snapshot = profile.RunSnapshot;
        writer.Write(snapshot.Unknown1);
        if (snapshot.HasPositionData)
        {
            WriteFString(writer, snapshot.MapName);
            writer.Write(snapshot.X);
            writer.Write(snapshot.Y);
            writer.Write(snapshot.Z);
            writer.Write(snapshot.Unknown2);
            foreach (LoadoutEntry entry in snapshot.Loadout)
            {
                WriteFString(writer, entry.SlotType);
                // An empty id marks a truncated pair captured during parsing; the
                // original file had nothing there, so write nothing.
                if (entry.Id.Length > 0)
                {
                    WriteFString(writer, entry.Id);
                }
            }
        }
        writer.Write(snapshot.Remainder);

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Writes an Unreal-style FString: int32 length including trailing NUL, then bytes.</summary>
    private static void WriteFString(BinaryWriter writer, string value)
    {
        if (value.Length == 0)
        {
            writer.Write(0);
            return;
        }

        bool isAscii = true;
        foreach (char c in value)
        {
            if (c > 127)
            {
                isAscii = false;
                break;
            }
        }

        if (isAscii)
        {
            writer.Write(value.Length + 1);
            writer.Write(Encoding.ASCII.GetBytes(value));
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
