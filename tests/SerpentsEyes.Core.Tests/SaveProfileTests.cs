using SerpentsEyes.Core;

namespace SerpentsEyes.Core.Tests;

public class SaveProfileTests
{
    // Fixtures are snapshots of real saves; the live save directory changes while the game runs.
    private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static string ProfilePath => Path.Combine(FixtureDir, "profile_0.sav");

    public static TheoryData<string> AllSaveFiles()
    {
        var data = new TheoryData<string>();
        foreach (string file in Directory.EnumerateFiles(FixtureDir, "*.sav"))
        {
            data.Add(file);
        }
        return data;
    }

    [Fact]
    public void Parses_Header()
    {
        var profile = SaveProfile.Load(ProfilePath);

        Assert.Equal(522, profile.Header.Unknown1);
        Assert.Equal(1013, profile.Header.Unknown2);
        Assert.Equal(5, profile.Header.VersionA);
        Assert.Equal(5, profile.Header.VersionB);
        Assert.Equal(4, profile.Header.VersionC);
        Assert.Equal("++NinjaGarden+live", profile.Header.BuildId);
        Assert.Equal("/Script/NinjaGarden.NG_SaveFormat_4", profile.Header.FormatId);
    }

    [Fact]
    public void Parses_All_150_Records_With_Known_Values()
    {
        var profile = SaveProfile.Load(ProfilePath);

        Assert.Equal(150, profile.Records.Count);
        Assert.Equal(43, profile.Find("Progression.Meta.Run.Started")!.Value);
        Assert.Equal(3, profile.Find("Progression.Meta.Run.Victory")!.Value);
        Assert.Equal(1, profile.Find("Progression.Class.WellRounded")!.Value);
        Assert.Equal(25, profile.Find("Progression.Prayer.Matriarch")!.Value);
        Assert.Equal(0, profile.Find("Progression.Quest.Sael.Part.2")!.Value);
    }

    [Fact]
    public void Splits_Tags_Into_Category_And_Name()
    {
        var profile = SaveProfile.Load(ProfilePath);

        var record = profile.Find("Progression.Meta.Run.Started")!;
        Assert.Equal("Meta", record.Category);
        Assert.Equal("Run.Started", record.Name);

        // Tag with an embedded space, straight from the real file.
        var shortcut = profile.Find("Progression.Shortcut.Attaresh. Cathedral")!;
        Assert.Equal("Shortcut", shortcut.Category);
        Assert.Equal("Attaresh. Cathedral", shortcut.Name);
    }

    [Fact]
    public void Parses_Run_Snapshot()
    {
        var profile = SaveProfile.Load(ProfilePath);
        var snapshot = profile.RunSnapshot;

        Assert.True(snapshot.HasPositionData);
        Assert.Equal("Majin_HolyCity", snapshot.MapName);
        Assert.Equal(16240.1, snapshot.X, tolerance: 0.1);
        Assert.Equal(-2544.7, snapshot.Y, tolerance: 0.1);
        Assert.Equal(3526.2, snapshot.Z, tolerance: 0.1);
        Assert.Equal(73.0f, snapshot.Unknown2, tolerance: 0.01f);

        Assert.Equal(3, snapshot.Loadout.Count);
        Assert.All(snapshot.Loadout, e => Assert.Equal("Item", e.SlotType));
        Assert.Equal(["Class_Stronk", "Tree_Warhammer", "Mushroom_EarthenMight"],
            snapshot.Loadout.Select(e => e.Id).ToArray());
    }

    [Theory]
    [MemberData(nameof(AllSaveFiles))]
    public void RoundTrip_Is_Byte_Perfect(string path)
    {
        byte[] original = File.ReadAllBytes(path);

        byte[] rewritten = SaveProfile.Parse(original).ToBytes();

        Assert.Equal(original, rewritten);
    }

    [Fact]
    public void Editing_A_Value_Survives_Reserialization()
    {
        var profile = SaveProfile.Load(ProfilePath);
        profile.Find("Progression.Meta.Run.Started")!.Value = 99;

        var reparsed = SaveProfile.Parse(profile.ToBytes());

        Assert.Equal(99, reparsed.Find("Progression.Meta.Run.Started")!.Value);
        Assert.Equal("Majin_HolyCity", reparsed.RunSnapshot.MapName);
        Assert.Equal(150, reparsed.Records.Count);
    }

    [Fact]
    public void AddRecord_Rejects_A_Tag_The_Parser_Could_Never_Read_Back()
    {
        var profile = SaveProfile.Load(ProfilePath);

        // Accepting this would build a profile that serializes to a file Parse then rejects.
        Assert.Throws<ArgumentException>(() => profile.AddRecord(new string('a', 5000), 1));
        Assert.Equal(150, profile.Records.Count);
    }

    [Fact]
    public void Truncated_File_Throws_SaveFormatException()
    {
        byte[] original = File.ReadAllBytes(ProfilePath);

        var ex = Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(original.AsSpan(0, 40)));
        Assert.True(ex.Offset >= 0);
    }

    [Fact]
    public void Garbage_Input_Throws_SaveFormatException()
    {
        byte[] garbage = new byte[64];
        Array.Fill(garbage, (byte)0xAB);

        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(garbage));
    }
}
