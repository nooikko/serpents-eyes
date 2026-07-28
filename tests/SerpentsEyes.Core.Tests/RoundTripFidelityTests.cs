using System.Text;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Round-trip tests over hand-built files. Each case covers an encoding the format allows and
/// the real fixtures happen never to contain, where re-encoding from parsed values instead of
/// reusing the original bytes changes the file's length and shifts everything after it.
/// </summary>
public class RoundTripFidelityTests
{
    private static void AssertRoundTrips(byte[] original)
    {
        byte[] rewritten = SaveProfile.Parse(original).ToBytes();
        Assert.Equal(original, rewritten);
    }

    [Fact]
    public void NulOnly_FString_In_Header_Keeps_Its_Length()
    {
        // Length 1 (a lone NUL) and length 0 both decode to "". Re-encoding as length 0
        // drops a byte and shifts the rest of the file.
        AssertRoundTrips(new SaveFileBuilder()
            .Header(buildId: SaveFileBuilder.NulOnly())
            .Records((SaveFileBuilder.Ansi("Progression.Meta.Run.Started"), 43))
            .NoTrailer()
            .ToArray());
    }

    [Fact]
    public void ZeroLength_FString_In_Header_Stays_Zero_Length()
    {
        AssertRoundTrips(new SaveFileBuilder()
            .Header(buildId: SaveFileBuilder.ZeroLength())
            .Records((SaveFileBuilder.Ansi("Progression.Meta.Run.Started"), 43))
            .NoTrailer()
            .ToArray());
    }

    [Fact]
    public void NulOnly_Tag_Keeps_Its_Length()
    {
        AssertRoundTrips(new SaveFileBuilder()
            .Header()
            .Records(
                (SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1),
                (SaveFileBuilder.NulOnly(), 7))
            .NoTrailer()
            .ToArray());
    }

    [Fact]
    public void NulOnly_Loadout_Id_Keeps_Its_Length()
    {
        // The parser accepts a length-1 id as "", which the old serializer could not tell
        // apart from a truncated pair and so dropped entirely: five bytes lost.
        AssertRoundTrips(new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1))
            .TrailerWithRun(
                SaveFileBuilder.Ansi("Majin_HolyCity"),
                SaveFileBuilder.Ansi("Item"),
                SaveFileBuilder.NulOnly())
            .ToArray());
    }

    [Fact]
    public void Truncated_Loadout_Pair_Does_Not_Gain_Bytes()
    {
        // A slot type with no id after it: the file really does end there.
        AssertRoundTrips(new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1))
            .TrailerWithRun(
                SaveFileBuilder.Ansi("Majin_HolyCity"),
                SaveFileBuilder.Ansi("Item"),
                SaveFileBuilder.Ansi("Class_Stronk"),
                SaveFileBuilder.Ansi("Item"))
            .ToArray());
    }

    [Fact]
    public void File_Without_A_Trailer_Does_Not_Gain_Four_Bytes()
    {
        // Nothing follows the last record. Writing RunSnapshot.Unknown1 unconditionally
        // appended four zero bytes that were never in the file.
        byte[] original = SaveFileBuilder.Minimal();

        byte[] rewritten = SaveProfile.Parse(original).ToBytes();

        Assert.Equal(original.Length, rewritten.Length);
        Assert.Equal(original, rewritten);
    }

    [Fact]
    public void High_Bytes_In_Single_Byte_FString_Are_Preserved()
    {
        // Encoding.ASCII maps every byte >= 0x80 to '?', so these bytes used to be
        // silently replaced on the way out and shown as mojibake on the way in.
        byte[] original = new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.RawBytes(Encoding.Latin1.GetBytes("Progression.Curse.Café")), 1))
            .NoTrailer()
            .ToArray();

        var profile = SaveProfile.Parse(original);

        Assert.Equal("Progression.Curse.Café", profile.Records[0].FullTag);
        Assert.Equal(original, profile.ToBytes());
    }

    [Fact]
    public void Wide_Strings_Round_Trip()
    {
        AssertRoundTrips(new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Wide("Progression.Curse.日本語"), 2))
            .NoTrailer()
            .ToArray());
    }

    [Fact]
    public void Trailer_Without_A_Run_Round_Trips()
    {
        AssertRoundTrips(new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1))
            .TrailerWithoutRun([0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF])
            .ToArray());
    }

    [Fact]
    public void Editing_A_Value_Changes_Exactly_Those_Four_Bytes()
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_0.sav"));
        var profile = SaveProfile.Parse(original);
        // 43 -> 0x01020304, chosen so all four bytes of the int32 change.
        profile.Find("Progression.Meta.Run.Started")!.Value = 0x01020304;

        byte[] edited = profile.ToBytes();

        Assert.Equal(original.Length, edited.Length);
        int[] differing = [.. Enumerable.Range(0, original.Length).Where(i => original[i] != edited[i])];
        Assert.Equal(4, differing.Length);
        Assert.Equal(differing[0] + 3, differing[3]); // contiguous: one int32
    }

    [Fact]
    public void Adding_A_Record_Leaves_Existing_Records_Byte_Identical()
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_0.sav"));
        var profile = SaveProfile.Parse(original);
        int originalCount = profile.Records.Count;
        profile.AddRecord("Progression.Class.Invented", 1);

        byte[] rewritten = SaveProfile.Parse(profile.ToBytes()).ToBytes();
        var reparsed = SaveProfile.Parse(rewritten);

        Assert.Equal(originalCount + 1, reparsed.Records.Count);
        Assert.Equal(1, reparsed.Find("Progression.Class.Invented")!.Value);
        Assert.Equal("Majin_HolyCity", reparsed.RunSnapshot.MapName);
    }

    [Fact]
    public void Reserializing_Twice_Is_Stable()
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_0.sav"));

        byte[] once = SaveProfile.Parse(original).ToBytes();
        byte[] twice = SaveProfile.Parse(once).ToBytes();

        Assert.Equal(original, twice);
    }
}
