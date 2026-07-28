namespace SerpentsEyes.Core;

/// <summary>One loadout slot in the current-run snapshot, e.g. ("Item", "Class_Stronk").</summary>
/// <param name="SlotType">Slot kind as stored in the file, e.g. "Item".</param>
/// <param name="Id">Content id, e.g. "Class_Stronk".</param>
/// <param name="HasId">
/// False when the file ended after the slot type with no id following. Distinguishes a
/// truncated pair from a pair whose id is genuinely the empty string; without it both
/// serialize the same way and one of them loses bytes.
/// </param>
public sealed record LoadoutEntry(string SlotType, string Id, bool HasId = true);

/// <summary>
/// The trailer of the save file: a snapshot of the run in progress (map, player
/// position, equipped loadout). Unrecognized trailing bytes are kept in
/// <see cref="Remainder"/> so the file round-trips byte-for-byte.
/// </summary>
public sealed class RunSnapshot
{
    /// <summary>
    /// True when the file had any bytes at all after the last record. When false the whole
    /// trailer is absent, including the <see cref="PendingTags"/> count, and serialization
    /// must not invent one.
    /// </summary>
    public bool HasTrailer { get; set; }

    /// <summary>
    /// Count-prefixed list of tag names stored between the records and the map name.
    /// </summary>
    /// <remarks>
    /// Observed to hold progression tags unlocked during the run in progress — a save taken
    /// just after unlocking "Progression.Item.BasicCrit" lists exactly that tag, and the list
    /// is empty once the run ends. The precise meaning is unconfirmed, but the count is real:
    /// treating it as an opaque int32 desynchronizes the rest of the trailer whenever it is
    /// non-zero, which is why the map name and loadout used to come out wrong on live saves.
    /// </remarks>
    public List<string> PendingTags { get; } = [];

    /// <summary>
    /// True when the trailer contained the map/position/loadout block. When false the whole
    /// trailer is opaque and lives in <see cref="Remainder"/>.
    /// </summary>
    public bool HasPositionData { get; set; }

    /// <summary>Current map, e.g. "Majin_HolyCity". Unreal's null FName, "None", between runs.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Player world position along the X axis.</summary>
    public double X { get; set; }

    /// <summary>Player world position along the Y axis.</summary>
    public double Y { get; set; }

    /// <summary>Player world position along the Z axis (height).</summary>
    public double Z { get; set; }

    /// <summary>Unidentified float following the position. Observed: 73.0 (possibly health).</summary>
    public float Unknown2 { get; set; }

    /// <summary>Equipped loadout entries for the run in progress.</summary>
    public List<LoadoutEntry> Loadout { get; } = [];

    /// <summary>Unparsed bytes after the loadout (padding + FF FF FF FF terminator), preserved verbatim.</summary>
    public byte[] Remainder { get; set; } = [];

    /// <summary>
    /// True when a run appears to be in progress. Between runs the game writes Unreal's null
    /// FName, the literal string "None", rather than clearing the field.
    /// </summary>
    public bool HasRun => MapName.Length > 0 && !MapName.Equals("None", StringComparison.Ordinal);

    internal RunSnapshot Clone()
    {
        var clone = new RunSnapshot
        {
            HasTrailer = HasTrailer,
            HasPositionData = HasPositionData,
            MapName = MapName,
            X = X,
            Y = Y,
            Z = Z,
            Unknown2 = Unknown2,
            Remainder = (byte[])Remainder.Clone(),
        };
        clone.PendingTags.AddRange(PendingTags);
        clone.Loadout.AddRange(Loadout);
        return clone;
    }

    /// <summary>
    /// Field-by-field comparison, used to decide whether the original trailer bytes can be
    /// reused. Floating-point fields compare bitwise so that -0.0 and NaN payloads, which
    /// are preserved on disk, are not treated as equal to their arithmetic counterparts.
    /// </summary>
    internal bool ValueEquals(RunSnapshot other)
        => HasTrailer == other.HasTrailer
        && HasPositionData == other.HasPositionData
        && PendingTags.SequenceEqual(other.PendingTags, StringComparer.Ordinal)
        && string.Equals(MapName, other.MapName, StringComparison.Ordinal)
        && BitConverter.DoubleToInt64Bits(X) == BitConverter.DoubleToInt64Bits(other.X)
        && BitConverter.DoubleToInt64Bits(Y) == BitConverter.DoubleToInt64Bits(other.Y)
        && BitConverter.DoubleToInt64Bits(Z) == BitConverter.DoubleToInt64Bits(other.Z)
        && BitConverter.SingleToInt32Bits(Unknown2) == BitConverter.SingleToInt32Bits(other.Unknown2)
        && Loadout.SequenceEqual(other.Loadout)
        && Remainder.AsSpan().SequenceEqual(other.Remainder);
}
