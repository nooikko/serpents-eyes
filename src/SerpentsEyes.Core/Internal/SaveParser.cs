namespace SerpentsEyes.Core.Internal;

internal static class SaveParser
{
    private const int MaxPlausibleStringLength = SpanReader.MaxPlausibleStringLength;

    /// <summary>Smallest a record can be on disk: a 4-byte length, a 1-byte NUL payload, a 4-byte value.</summary>
    private const int MinRecordSize = 9;

    /// <summary>
    /// Substring every known save format id contains ("/Script/NinjaGarden.NG_SaveFormat_4").
    /// Matching on the family rather than the exact string keeps a future format bump readable
    /// while still rejecting files that merely happen to have the right shape.
    /// </summary>
    private const string FormatIdMarker = "NG_SaveFormat";

    /// <summary>
    /// Sanity bound on <see cref="RunSnapshot.PendingTags"/>. Observed counts are 0 or 1; a
    /// larger value means these bytes are not the run block and the trailer is something else.
    /// </summary>
    private const int MaxPendingTags = 1024;

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

        int headerLength = reader.Position;

        if (!header.FormatId.Contains(FormatIdMarker, StringComparison.Ordinal))
        {
            throw new SaveFormatException(
                $"Not a Serpent's Gaze save: expected a format id containing '{FormatIdMarker}', found '{header.FormatId}'",
                headerLength);
        }

        int recordCount = reader.ReadInt32();
        if (recordCount < 0)
        {
            throw new SaveFormatException($"Implausible record count {recordCount}", reader.Position - 4);
        }

        // Bound the count against what the file could actually hold before allocating for it,
        // so a corrupt 4-byte field cannot drive a large allocation from a tiny file.
        int maxPossible = reader.Remaining / MinRecordSize;
        if (recordCount > maxPossible)
        {
            throw new SaveFormatException(
                $"Record count {recordCount} exceeds what the remaining {reader.Remaining} byte(s) can hold",
                reader.Position - 4);
        }

        var records = new List<TagRecord>(recordCount);
        for (int i = 0; i < recordCount; i++)
        {
            int tagStart = reader.Position;
            string tag = reader.ReadFString();
            int tagLength = reader.Position - tagStart;
            int value = reader.ReadInt32();
            records.Add(new TagRecord(tag, value, tagStart, tagLength));
        }

        int trailerStart = reader.Position;
        var snapshot = ParseSnapshot(ref reader, data);

        var layout = new SourceLayout(
            data.ToArray(),
            headerLength,
            trailerStart,
            header.Clone(),
            snapshot.Clone());

        return new SaveProfile(header, records, snapshot, layout);
    }

    private static RunSnapshot ParseSnapshot(ref SpanReader reader, ReadOnlySpan<byte> data)
    {
        var snapshot = new RunSnapshot();
        if (reader.Remaining == 0)
        {
            return snapshot;
        }

        snapshot.HasTrailer = true;

        int mark = reader.Position;
        if (TryParseRunBlock(ref reader, data, snapshot))
        {
            snapshot.HasPositionData = true;
            snapshot.Remainder = reader.ReadRemaining();
            return snapshot;
        }

        // Not a shape we recognize. Rewind and keep the whole trailer verbatim so it still
        // round-trips; the caller just gets no run detail.
        reader.Seek(mark);
        snapshot.PendingTags.Clear();
        snapshot.Loadout.Clear();
        snapshot.Remainder = reader.ReadRemaining();
        return snapshot;
    }

    /// <summary>
    /// Attempts to read the run block: a count-prefixed list of tag names, the map name, the
    /// player position, and the loadout. Returns false without guaranteeing the reader position
    /// if the bytes do not match, so callers must rewind.
    /// </summary>
    private static bool TryParseRunBlock(ref SpanReader reader, ReadOnlySpan<byte> data, RunSnapshot snapshot)
    {
        if (reader.Remaining < 4)
        {
            return false;
        }

        int pendingCount = reader.ReadInt32();
        if (pendingCount < 0 || pendingCount > MaxPendingTags)
        {
            return false;
        }

        for (int i = 0; i < pendingCount; i++)
        {
            if (!IsPlausibleFString(reader, data))
            {
                return false;
            }
            snapshot.PendingTags.Add(reader.ReadFString());
        }

        if (!IsPlausibleFString(reader, data))
        {
            return false;
        }
        snapshot.MapName = reader.ReadFString();

        // Three doubles and a float follow the map name.
        if (reader.Remaining < (8 * 3) + 4)
        {
            return false;
        }
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
                // Half a pair: not the shape we know. Record it with HasId false so the
                // serializer can tell it apart from a pair whose id is the empty string.
                snapshot.Loadout.Add(new LoadoutEntry(slotType, string.Empty, HasId: false));
                break;
            }
            snapshot.Loadout.Add(new LoadoutEntry(slotType, reader.ReadFString()));
        }

        return true;
    }

    /// <summary>
    /// True when the bytes at the current position look like a single-byte FString:
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
