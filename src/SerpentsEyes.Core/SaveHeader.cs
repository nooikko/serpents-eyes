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

    /// <summary>Likely a version triplet. Observed: 5, 5, 4.</summary>
    public short VersionA { get; set; }
    public short VersionB { get; set; }
    public short VersionC { get; set; }

    /// <summary>Four unidentified bytes. Observed: 52 3C 00 80.</summary>
    public byte[] Unknown3 { get; set; } = new byte[4];

    /// <summary>Build/branch identifier. Observed: "++NinjaGarden+live".</summary>
    public string BuildId { get; set; } = string.Empty;

    /// <summary>Single unidentified byte between the build id and format id. Observed: 0x03.</summary>
    public byte Unknown4 { get; set; }

    /// <summary>Save format class path. Observed: "/Script/NinjaGarden.NG_SaveFormat_4".</summary>
    public string FormatId { get; set; } = string.Empty;
}
