namespace SerpentsEyes.Core;

/// <summary>
/// One progression entry: a tag such as "Progression.Class.WellRounded" with an
/// integer counter (1 = unlocked/done once, N = a count, 0 = reached but not completed).
/// </summary>
public sealed class TagRecord
{
    private const string ProgressionPrefix = "Progression.";

    public TagRecord(string fullTag, int value)
    {
        ArgumentNullException.ThrowIfNull(fullTag);
        FullTag = fullTag;
        Value = value;

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

    public override string ToString() => $"{FullTag} = {Value}";
}
