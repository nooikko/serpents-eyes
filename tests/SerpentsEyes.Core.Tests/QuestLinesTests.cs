using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Quest tags carry both the owner and the running order, so the browser can group them without
/// any curated data. These tests pin the decomposition, including the numbering quirk that makes
/// raw tag indices unusable as stage numbers.
/// </summary>
public class QuestLinesTests
{
    private static SaveProfile Fixture() =>
        SaveProfile.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_pending_tags.sav"));

    private static QuestStep Parse(string tag, int value) =>
        QuestLines.Parse(new TagRecord(tag, value))!;

    [Fact]
    public void Parses_Owner_And_Stage_From_A_Part_Tag()
    {
        var step = Parse("Progression.Quest.LordMalvo.Part.2", 1);

        Assert.Equal("LordMalvo", step.OwnerKey);
        Assert.Equal("Lord Malvo", step.OwnerName);
        Assert.Equal(QuestStepKind.Part, step.Kind);
        Assert.Equal(2, step.Order);
    }

    [Theory]
    [InlineData("Progression.Quest.Mujica.Event.1", QuestStepKind.Event)]
    [InlineData("Progression.Quest.Mujica.Skippable.0", QuestStepKind.Optional)]
    [InlineData("Progression.Quest.LordMalvo.PygmyMeat", QuestStepKind.Item)]
    [InlineData("Progression.Quest.Druid.RunsWhenInteracting", QuestStepKind.Other)]
    public void Classifies_Step_Kinds(string tag, QuestStepKind expected)
    {
        Assert.Equal(expected, Parse(tag, 1).Kind);
    }

    [Fact]
    public void Collectible_Labels_Are_Humanized()
    {
        Assert.Equal("Berserker Kidney", Parse("Progression.Quest.LordMalvo.BerserkerKidney", 5).Label);
    }

    [Fact]
    public void Non_Quest_Records_Are_Ignored()
    {
        Assert.Null(QuestLines.Parse(new TagRecord("Progression.Class.WellRounded", 1)));
    }

    [Fact]
    public void Stages_Are_Numbered_By_Position_Not_By_Raw_Index()
    {
        // Lord Malvo's parts start at 1 in the files while every other owner starts at 0.
        // Numbering off the raw index would show his questline starting at "Stage 2".
        var profile = Fixture();

        var malvo = QuestLines.Build(profile).Single(q => q.OwnerKey == "LordMalvo");

        Assert.Equal("Stage 1", malvo.Parts.First().Label);
        Assert.Equal(1, malvo.Parts.First().Order); // raw index preserved
    }

    [Fact]
    public void Groups_Every_Quest_Record_By_Owner()
    {
        var profile = Fixture();
        int questRecords = profile.Records.Count(r => r.Category == "Quest");

        var lines = QuestLines.Build(profile);

        Assert.Equal(questRecords, lines.Sum(l => l.Steps.Count));
        Assert.All(lines, l => Assert.NotEmpty(l.Steps));
    }

    [Fact]
    public void Owners_Are_Ordered_By_Display_Name()
    {
        var names = QuestLines.Build(Fixture()).Select(l => l.OwnerName).ToList();

        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Fact]
    public void Parts_Come_Before_Encounters_And_Collectibles()
    {
        var vagabond = QuestLines.Build(Fixture()).Single(q => q.OwnerKey == "Vagabond");

        int lastPart = vagabond.Steps.ToList().FindLastIndex(s => s.Kind == QuestStepKind.Part);
        int firstOther = vagabond.Steps.ToList().FindIndex(s => s.Kind != QuestStepKind.Part);

        Assert.True(lastPart < firstOther || firstOther < 0);
    }

    [Fact]
    public void Completion_Counts_Only_Stages()
    {
        var witness = QuestLines.Build(Fixture()).Single(q => q.OwnerKey == "TheWitness");

        // Two stages, one done; the two encounters must not inflate the total.
        Assert.Equal(2, witness.TotalParts);
        Assert.Equal(1, witness.CompletedParts);
        Assert.False(witness.IsComplete);
    }

    [Fact]
    public void Unknown_Owner_Falls_Back_To_Split_Words()
    {
        Assert.Equal("Some New Npc", QuestLines.OwnerDisplayName("SomeNewNpc"));
    }
}
