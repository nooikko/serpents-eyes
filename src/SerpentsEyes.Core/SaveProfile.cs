using SerpentsEyes.Core.Internal;

namespace SerpentsEyes.Core;

/// <summary>
/// A parsed Serpent's Gaze save file (NG_SaveFormat_4).
/// </summary>
/// <remarks>
/// <para>
/// Round-trip fidelity: <c>SaveProfile.Parse(bytes).ToBytes()</c> reproduces the input exactly.
/// Regions you do not modify are copied back verbatim from the source buffer, so unknown fields,
/// unusual string encodings, and trailing padding all survive untouched.
/// </para>
/// <para>
/// After an edit, only the modified region is re-encoded; everything else is still copied
/// through. Editing a record's <see cref="TagRecord.Value"/> therefore changes exactly the four
/// bytes of that value and nothing else.
/// </para>
/// </remarks>
public sealed class SaveProfile
{
    /// <summary>How many times to re-read a file that changed underneath us before giving up.</summary>
    private const int TornReadRetries = 3;

    private readonly List<TagRecord> _records;

    internal SaveProfile(SaveHeader header, List<TagRecord> records, RunSnapshot runSnapshot, SourceLayout? sourceLayout = null)
    {
        Header = header;
        _records = records;
        RunSnapshot = runSnapshot;
        SourceLayout = sourceLayout;
    }

    /// <summary>The file header: version fields, build id, and format id.</summary>
    public SaveHeader Header { get; }

    /// <summary>Progression records in file order. Values are mutable; use <see cref="AddRecord"/> / <see cref="RemoveRecord"/> to change the set.</summary>
    public IReadOnlyList<TagRecord> Records => _records;

    /// <summary>Snapshot of the run in progress (map, position, loadout).</summary>
    public RunSnapshot RunSnapshot { get; }

    /// <summary>Original bytes and region offsets, present when this profile came from a file.</summary>
    internal SourceLayout? SourceLayout { get; }

    /// <summary>
    /// Loads and parses a save file from disk. The file is opened so that it can be read while
    /// the game holds it open, which is the normal case for the live save directory.
    /// </summary>
    public static SaveProfile Load(string path) => Parse(ReadAllBytesShared(path));

    /// <summary>Parses a save file from raw bytes.</summary>
    public static SaveProfile Parse(ReadOnlySpan<byte> data) => SaveParser.Parse(data);

    /// <summary>Serializes back to the on-disk format.</summary>
    public byte[] ToBytes() => SaveSerializer.Serialize(this);

    /// <summary>Writes the save to disk. Consider backing up the original first.</summary>
    public void Save(string path) => File.WriteAllBytes(path, ToBytes());

    /// <summary>Finds a record by its full tag (ordinal comparison), or null.</summary>
    public TagRecord? Find(string fullTag)
        => _records.Find(r => string.Equals(r.FullTag, fullTag, StringComparison.Ordinal));

    /// <summary>Appends a new record. The tag is not validated against the game's known tags.</summary>
    /// <param name="fullTag">Full tag, e.g. "Progression.Class.WellRounded".</param>
    /// <param name="value">Counter value.</param>
    /// <returns>The record that was added.</returns>
    public TagRecord AddRecord(string fullTag, int value)
    {
        var record = new TagRecord(fullTag, value);
        _records.Add(record);
        return record;
    }

    /// <summary>Removes a record. Returns false if it was not in this profile.</summary>
    public bool RemoveRecord(TagRecord record) => _records.Remove(record);

    /// <summary>
    /// Reads a file that another process may have open for writing.
    /// </summary>
    /// <remarks>
    /// <see cref="File.ReadAllBytes"/> requests <see cref="FileShare.Read"/>, which fails with an
    /// <see cref="IOException"/> whenever the game has the save open to flush an autosave. Sharing
    /// write and delete access lets the read succeed. The cost is that we may catch the file
    /// mid-write, so the read is repeated if the file changed while we were reading it.
    /// </remarks>
    private static byte[] ReadAllBytesShared(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            long lengthBefore = stream.Length;
            DateTime writtenBefore = File.GetLastWriteTimeUtc(path);

            if (lengthBefore > int.MaxValue)
            {
                throw new SaveFormatException($"File is too large to be a save: {lengthBefore} bytes");
            }

            var buffer = new byte[(int)lengthBefore];
            stream.ReadExactly(buffer);

            bool changed = stream.Length != lengthBefore
                || File.GetLastWriteTimeUtc(path) != writtenBefore;

            if (!changed || attempt >= TornReadRetries)
            {
                return buffer;
            }
        }
    }
}
