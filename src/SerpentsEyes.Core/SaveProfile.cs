using SerpentsEyes.Core.Internal;

namespace SerpentsEyes.Core;

/// <summary>
/// A parsed Serpent's Gaze save file (NG_SaveFormat_4). Guarantees byte-perfect
/// round-trip: <c>SaveProfile.Parse(bytes).ToBytes()</c> reproduces the input exactly.
/// </summary>
public sealed class SaveProfile
{
    private readonly List<TagRecord> _records;

    internal SaveProfile(SaveHeader header, List<TagRecord> records, RunSnapshot runSnapshot)
    {
        Header = header;
        _records = records;
        RunSnapshot = runSnapshot;
    }

    public SaveHeader Header { get; }

    /// <summary>Progression records in file order. Values are mutable; use <see cref="AddRecord"/> / <see cref="RemoveRecord"/> to change the set.</summary>
    public IReadOnlyList<TagRecord> Records => _records;

    /// <summary>Snapshot of the run in progress (map, position, loadout).</summary>
    public RunSnapshot RunSnapshot { get; }

    /// <summary>Loads and parses a save file from disk.</summary>
    public static SaveProfile Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Parses a save file from raw bytes.</summary>
    public static SaveProfile Parse(ReadOnlySpan<byte> data) => SaveParser.Parse(data);

    /// <summary>Serializes back to the on-disk format.</summary>
    public byte[] ToBytes() => SaveSerializer.Serialize(this);

    /// <summary>Writes the save to disk. Consider backing up the original first.</summary>
    public void Save(string path) => File.WriteAllBytes(path, ToBytes());

    /// <summary>Finds a record by its full tag (ordinal comparison), or null.</summary>
    public TagRecord? Find(string fullTag)
        => _records.Find(r => string.Equals(r.FullTag, fullTag, StringComparison.Ordinal));

    public TagRecord AddRecord(string fullTag, int value)
    {
        var record = new TagRecord(fullTag, value);
        _records.Add(record);
        return record;
    }

    public bool RemoveRecord(TagRecord record) => _records.Remove(record);
}
