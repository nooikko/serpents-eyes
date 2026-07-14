namespace SerpentsEyes.Core;

/// <summary>One loadout slot in the current-run snapshot, e.g. ("Item", "Class_Stronk").</summary>
public sealed record LoadoutEntry(string SlotType, string Id);

/// <summary>
/// The trailer of the save file: a snapshot of the run in progress (map, player
/// position, equipped loadout). Unrecognized trailing bytes are kept in
/// <see cref="Remainder"/> so the file round-trips byte-for-byte.
/// </summary>
public sealed class RunSnapshot
{
    /// <summary>int32 between the records and the map name. Observed value: 0.</summary>
    public int Unknown1 { get; set; }

    /// <summary>
    /// True when the trailer contained the map/position/loadout block. When false,
    /// everything after <see cref="Unknown1"/> lives in <see cref="Remainder"/>.
    /// </summary>
    public bool HasPositionData { get; set; }

    /// <summary>Current map, e.g. "Majin_HolyCity".</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Player world position.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    /// <summary>Unidentified float following the position. Observed: 73.0 (possibly health).</summary>
    public float Unknown2 { get; set; }

    /// <summary>Equipped loadout entries for the run in progress.</summary>
    public List<LoadoutEntry> Loadout { get; } = [];

    /// <summary>Unparsed bytes after the loadout (padding + FF FF FF FF terminator), preserved verbatim.</summary>
    public byte[] Remainder { get; set; } = [];

    /// <summary>True when a run appears to be in progress (a map name is present).</summary>
    public bool HasRun => MapName.Length > 0;
}
