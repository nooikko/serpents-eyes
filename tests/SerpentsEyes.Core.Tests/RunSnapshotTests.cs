namespace SerpentsEyes.Core.Tests;

/// <summary>
/// The trailer's leading int32 counts the tag strings that follow it. It is 0 in every save
/// taken between runs, which is what the original three fixtures all captured; when it is
/// non-zero, reading it as an opaque field shifts the map name, position, and loadout by one
/// string. profile_pending_tags.sav is a real save with a count of 1.
/// </summary>
public class RunSnapshotTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Pending_Tag_List_Is_Read_As_A_Count_Prefixed_List()
    {
        var snapshot = SaveProfile.Load(Fixture("profile_pending_tags.sav")).RunSnapshot;

        Assert.Equal(["Progression.Item.BasicCrit"], snapshot.PendingTags);
    }

    [Fact]
    public void Loadout_Survives_A_Non_Empty_Pending_Tag_List()
    {
        // Before the count was understood these came out empty, with the loadout bytes
        // stranded in Remainder.
        var snapshot = SaveProfile.Load(Fixture("profile_pending_tags.sav")).RunSnapshot;

        Assert.Equal(
            ["Class_Stronk", "Tree_Warhammer", "Mushroom_EarthenMight"],
            snapshot.Loadout.Select(e => e.Id).ToArray());
        Assert.All(snapshot.Loadout, e => Assert.Equal("Item", e.SlotType));
    }

    [Fact]
    public void Map_Name_Is_Not_Swallowed_By_The_Pending_Tag_List()
    {
        var snapshot = SaveProfile.Load(Fixture("profile_pending_tags.sav")).RunSnapshot;

        Assert.Equal("None", snapshot.MapName);
    }

    [Fact]
    public void None_Map_Means_No_Run_In_Progress()
    {
        // Unreal writes the null FName as the literal "None" rather than clearing the field,
        // so a non-empty map name is not on its own evidence of a run.
        var between = SaveProfile.Load(Fixture("profile_pending_tags.sav")).RunSnapshot;
        var during = SaveProfile.Load(Fixture("profile_0.sav")).RunSnapshot;

        Assert.False(between.HasRun);
        Assert.True(during.HasRun);
        Assert.Equal("Majin_HolyCity", during.MapName);
    }

    [Fact]
    public void Position_Is_Zero_Between_Runs()
    {
        var snapshot = SaveProfile.Load(Fixture("profile_pending_tags.sav")).RunSnapshot;

        Assert.Equal(0, snapshot.X);
        Assert.Equal(0, snapshot.Y);
        Assert.Equal(0, snapshot.Z);
    }

    [Fact]
    public void Empty_Pending_Tag_List_Still_Parses_The_Run()
    {
        var snapshot = SaveProfile.Load(Fixture("profile_0.sav")).RunSnapshot;

        Assert.Empty(snapshot.PendingTags);
        Assert.True(snapshot.HasPositionData);
        Assert.Equal(3, snapshot.Loadout.Count);
    }

    [Fact]
    public void Synthetic_Pending_Tags_Round_Trip()
    {
        byte[] original = new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1))
            .TrailerWithPendingTags(
                [SaveFileBuilder.Ansi("Progression.Item.BasicCrit"), SaveFileBuilder.Ansi("Progression.Item.Other")],
                SaveFileBuilder.Ansi("Majin_HolyCity"),
                SaveFileBuilder.Ansi("Item"),
                SaveFileBuilder.Ansi("Class_Stronk"))
            .ToArray();

        var profile = SaveProfile.Parse(original);

        Assert.Equal(
            ["Progression.Item.BasicCrit", "Progression.Item.Other"],
            profile.RunSnapshot.PendingTags);
        Assert.Equal("Majin_HolyCity", profile.RunSnapshot.MapName);
        Assert.Equal(original, profile.ToBytes());
    }

    [Fact]
    public void Unrecognized_Trailer_Is_Preserved_Verbatim()
    {
        // An implausible count means these bytes are not the run block. The parser must keep
        // them rather than guessing, so the file still round-trips.
        byte[] original = new SaveFileBuilder()
            .Header()
            .Records((SaveFileBuilder.Ansi("Progression.Class.WellRounded"), 1))
            .TrailerWithoutRun([0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02])
            .ToArray();

        var profile = SaveProfile.Parse(original);

        Assert.False(profile.RunSnapshot.HasPositionData);
        Assert.Equal(original, profile.ToBytes());
    }
}
