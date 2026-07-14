namespace SerpentsEyes.Core.Internal;

internal static class SaveParser
{
    private const int MaxPlausibleStringLength = 4096;
    private const int MaxPlausibleRecordCount = 1_000_000;

    public static SaveProfile Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);

        var header = new SaveHeader
        {
            Unknown1 = reader.ReadInt32(),
            Unknown2 = reader.ReadInt32(),
            VersionA = reader.ReadInt16(),
            VersionB = reader.ReadInt16(),
            VersionC = reader.ReadInt16(),
            Unknown3 = reader.ReadBytes(4),
            BuildId = reader.ReadFString(),
            Unknown4 = reader.ReadByte(),
            FormatId = reader.ReadFString(),
        };

        int recordCount = reader.ReadInt32();
        if (recordCount < 0 || recordCount > MaxPlausibleRecordCount)
        {
            throw new SaveFormatException($"Implausible record count {recordCount}", reader.Position - 4);
        }

        var records = new List<TagRecord>(recordCount);
        for (int i = 0; i < recordCount; i++)
        {
            string tag = reader.ReadFString();
            int value = reader.ReadInt32();
            records.Add(new TagRecord(tag, value));
        }

        var snapshot = ParseSnapshot(ref reader, data);
        return new SaveProfile(header, records, snapshot);
    }

    private static RunSnapshot ParseSnapshot(ref SpanReader reader, ReadOnlySpan<byte> data)
    {
        var snapshot = new RunSnapshot();
        if (reader.Remaining == 0)
        {
            return snapshot;
        }

        snapshot.Unknown1 = reader.ReadInt32();

        if (!IsPlausibleFString(reader, data))
        {
            // No recognizable map name — likely no run in progress. Preserve the rest verbatim.
            snapshot.Remainder = reader.ReadRemaining();
            return snapshot;
        }

        snapshot.HasPositionData = true;
        snapshot.MapName = reader.ReadFString();
        snapshot.X = reader.ReadDouble();
        snapshot.Y = reader.ReadDouble();
        snapshot.Z = reader.ReadDouble();
        snapshot.Unknown2 = reader.ReadSingle();

        // Loadout entries are (slotType, id) string pairs; a zero/implausible length ends the list.
        while (IsPlausibleFString(reader, data))
        {
            string slotType = reader.ReadFString();
            if (!IsPlausibleFString(reader, data))
            {
                // Half a pair: not the shape we know. Rewind is impossible, but the slot
                // string round-trips identically, so keep it as an entry with no id.
                snapshot.Loadout.Add(new LoadoutEntry(slotType, string.Empty));
                break;
            }
            snapshot.Loadout.Add(new LoadoutEntry(slotType, reader.ReadFString()));
        }

        snapshot.Remainder = reader.ReadRemaining();
        return snapshot;
    }

    /// <summary>
    /// True when the bytes at the current position look like an ASCII FString:
    /// a sane positive length whose payload fits and ends with a NUL byte.
    /// </summary>
    private static bool IsPlausibleFString(in SpanReader reader, ReadOnlySpan<byte> data)
    {
        int? length = reader.PeekInt32();
        if (length is null or <= 0 || length > MaxPlausibleStringLength)
        {
            return false;
        }
        int payloadEnd = reader.Position + 4 + length.Value;
        return payloadEnd <= data.Length && data[payloadEnd - 1] == 0;
    }
}
