namespace SerpentsEyes.Core;

/// <summary>
/// One progression entry: a tag such as "Progression.Class.WellRounded" with an
/// integer counter (1 = unlocked/done once, N = a count, 0 = reached but not completed).
/// </summary>
public sealed class TagRecord
{
    private const string ProgressionPrefix = "Progression.";

    private readonly int _originalValue;

    /// <summary>Creates a record that did not come from a file.</summary>
    /// <param name="fullTag">Full tag, e.g. "Progression.Class.WellRounded".</param>
    /// <param name="value">Counter value.</param>
    public TagRecord(string fullTag, int value)
        : this(fullTag, value, -1, 0)
    {
    }

    /// <param name="fullTag">Full tag, e.g. "Progression.Class.WellRounded".</param>
    /// <param name="value">Counter value.</param>
    /// <param name="tagSourceStart">Offset of this record's tag FString in the source buffer, or -1 when not parsed from a file.</param>
    /// <param name="tagSourceLength">Byte length of that FString, including its length prefix.</param>
    internal TagRecord(string fullTag, int value, int tagSourceStart, int tagSourceLength)
    {
        ArgumentNullException.ThrowIfNull(fullTag);
        FullTag = fullTag;
        Value = value;
        _originalValue = value;
        TagSourceStart = tagSourceStart;
        TagSourceLength = tagSourceLength;

        if (fullTag.StartsWith(ProgressionPrefix, StringComparison.Ordinal))
        {
            string rest = fullTag[ProgressionPrefix.Length..];
            int dot = rest.IndexOf('.');
            if (dot > 0)
            {
                Category = rest[..dot];
                Name = rest[(dot + 1)..];
                return;
            }
            Category = "Other";
            Name = rest;
            return;
        }

        Category = "Other";
        Name = fullTag;
    }

    /// <summary>The raw tag exactly as stored in the file.</summary>
    public string FullTag { get; }

    /// <summary>Second segment of a "Progression.X.Y" tag (e.g. "Class"), or "Other".</summary>
    public string Category { get; }

    /// <summary>Everything after the category (e.g. "WellRounded", "Run.Started").</summary>
    public string Name { get; }

    /// <summary>The counter. Mutable so the library can back a save editor.</summary>
    public int Value { get; set; }

    /// <summary>Offset of this record's tag FString in the source buffer, or -1 when it was not parsed from a file.</summary>
    internal int TagSourceStart { get; }

    /// <summary>Byte length of the tag FString in the source buffer, including its length prefix.</summary>
    internal int TagSourceLength { get; }

    /// <summary>True when the tag's original bytes are available to copy back verbatim.</summary>
    internal bool HasTagSource => TagSourceStart >= 0;

    /// <summary>True when <see cref="Value"/> differs from the value read out of the file.</summary>
    internal bool IsValueModified => Value != _originalValue;

    /// <summary>Renders as "tag = value", e.g. "Progression.Meta.Run.Started = 43".</summary>
    public override string ToString() => $"{FullTag} = {Value}";
}
