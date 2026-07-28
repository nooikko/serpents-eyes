namespace SerpentsEyes.Core;

/// <summary>
/// File header for NG_SaveFormat_4. Fields whose meaning is unknown are preserved
/// verbatim so serialization reproduces the original bytes exactly.
/// </summary>
public sealed class SaveHeader
{
    /// <summary>First int32 of the file. Observed value: 522.</summary>
    public int Unknown1 { get; set; }

    /// <summary>Second int32 of the file. Observed value: 1013.</summary>
    public int Unknown2 { get; set; }

    /// <summary>First component of what is likely a version triplet. Observed: 5.</summary>
    public short VersionA { get; set; }

    /// <summary>Second component of the version triplet. Observed: 5.</summary>
    public short VersionB { get; set; }

    /// <summary>Third component of the version triplet. Observed: 4, matching NG_SaveFormat_4.</summary>
    public short VersionC { get; set; }

    /// <summary>Four unidentified bytes. Observed: 52 3C 00 80.</summary>
    public byte[] Unknown3 { get; set; } = new byte[4];

    /// <summary>Build/branch identifier. Observed: "++NinjaGarden+live".</summary>
    public string BuildId { get; set; } = string.Empty;

    /// <summary>Single unidentified byte between the build id and format id. Observed: 0x03.</summary>
    public byte Unknown4 { get; set; }

    /// <summary>Save format class path. Observed: "/Script/NinjaGarden.NG_SaveFormat_4".</summary>
    public string FormatId { get; set; } = string.Empty;

    internal SaveHeader Clone() => new()
    {
        Unknown1 = Unknown1,
        Unknown2 = Unknown2,
        VersionA = VersionA,
        VersionB = VersionB,
        VersionC = VersionC,
        Unknown3 = (byte[])Unknown3.Clone(),
        BuildId = BuildId,
        Unknown4 = Unknown4,
        FormatId = FormatId,
    };

    /// <summary>Field-by-field comparison, used to decide whether the original header bytes can be reused.</summary>
    internal bool ValueEquals(SaveHeader other)
        => Unknown1 == other.Unknown1
        && Unknown2 == other.Unknown2
        && VersionA == other.VersionA
        && VersionB == other.VersionB
        && VersionC == other.VersionC
        && Unknown3.AsSpan().SequenceEqual(other.Unknown3)
        && string.Equals(BuildId, other.BuildId, StringComparison.Ordinal)
        && Unknown4 == other.Unknown4
        && string.Equals(FormatId, other.FormatId, StringComparison.Ordinal);
}
