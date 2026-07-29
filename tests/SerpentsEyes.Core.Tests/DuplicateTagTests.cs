using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Records are a list on disk, not a map, and nothing in the format stops a tag appearing twice.
/// Such a file is well-formed — it parses and round-trips exactly — so every consumer has to
/// tolerate it rather than throwing halfway through building a view of it.
/// </summary>
public class DuplicateTagTests
{
    private const string Tag = "Progression.Quest.Shaper.Part.0";

    private static byte[] WithDuplicate()
    {
        byte[] tag = SaveFileBuilder.Ansi(Tag);
        return new SaveFileBuilder()
            .Header()
            .Records((tag, 1), (tag, 2), (SaveFileBuilder.Ansi("Progression.Meta.Run.Started"), 7))
            .NoTrailer()
            .ToArray();
    }

    [Fact]
    public void A_Repeated_Tag_Parses_And_Keeps_Both_Records()
    {
        var profile = SaveProfile.Parse(WithDuplicate());

        Assert.Equal(3, profile.Records.Count);
        Assert.Equal([1, 2], profile.Records.Where(r => r.FullTag == Tag).Select(r => r.Value).ToArray());
    }

    [Fact]
    public void A_Repeated_Tag_Still_Round_Trips_Byte_For_Byte()
    {
        byte[] original = WithDuplicate();

        Assert.Equal(original, SaveProfile.Parse(original).ToBytes());
    }

    [Fact]
    public void ValuesByTag_Keeps_The_Later_Record()
    {
        var profile = SaveProfile.Parse(WithDuplicate());

        var values = profile.ValuesByTag();

        Assert.Equal(2, values.Count);
        Assert.Equal(2, values[Tag]);
        Assert.Equal(7, values["Progression.Meta.Run.Started"]);
    }

    [Fact]
    public void QuestLines_Build_Does_Not_Throw_On_A_Repeated_Tag()
    {
        // This threw ArgumentException from ToDictionary, which broke the load path for a file
        // that is otherwise perfectly readable.
        var profile = SaveProfile.Parse(WithDuplicate());

        var lines = QuestLines.Build(profile);

        var shaper = lines.FirstOrDefault(l => l.Steps.Any(s => s.FullTag == Tag));
        Assert.NotNull(shaper);
        Assert.Equal(2, shaper!.Steps.Single(s => s.FullTag == Tag).Value);
    }
}
