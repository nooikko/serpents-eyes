using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Every progression tag stores a counter, but the counters do not all mean the same thing.
/// Getting this wrong makes the app state things the game does not model — "20 of 32 Blessings
/// unlocked" when the number is really how many have ever been taken.
/// </summary>
public class TagSemanticsTests
{
    [Theory]
    [InlineData("Class", ProgressKind.Unlock)]
    [InlineData("Weapon", ProgressKind.Unlock)]
    [InlineData("Blessing", ProgressKind.Encounterable)]
    [InlineData("Item", ProgressKind.Encounterable)]
    [InlineData("Mushroom", ProgressKind.Encounterable)]
    [InlineData("Utility", ProgressKind.Encounterable)]
    [InlineData("Shortcut", ProgressKind.Checklist)]
    [InlineData("Kill", ProgressKind.Tally)]
    [InlineData("Quest", ProgressKind.Quest)]
    public void Classifies_Categories(string category, ProgressKind expected)
    {
        Assert.Equal(expected, TagSemantics.KindOf(category));
    }

    [Fact]
    public void Only_Real_Unlocks_Get_A_Completion_Total()
    {
        // Aspects and Weapons are unlocked once and stay unlocked, so "12 of 14" is a target.
        Assert.True(TagSemantics.HasCompletionTotal("Class"));
        Assert.True(TagSemantics.HasCompletionTotal("Weapon"));

        // These are offered per run; a completion bar would invent a goal the game lacks.
        Assert.False(TagSemantics.HasCompletionTotal("Blessing"));
        Assert.False(TagSemantics.HasCompletionTotal("Utility"));
        Assert.False(TagSemantics.HasCompletionTotal("Item"));
        Assert.False(TagSemantics.HasCompletionTotal("Kill"));
    }

    [Fact]
    public void Wording_Matches_What_The_Counter_Means()
    {
        Assert.Equal("Unlocked", TagSemantics.HaveVerb("Class"));
        Assert.Equal("Discovered", TagSemantics.HaveVerb("Blessing"));
        Assert.Equal("Found", TagSemantics.HaveVerb("Shortcut"));

        Assert.Equal("Locked", TagSemantics.MissingVerb("Class"));
        Assert.Equal("Not yet seen", TagSemantics.MissingVerb("Blessing"));
    }

    [Fact]
    public void Unknown_Categories_Do_Not_Claim_Completion()
    {
        // A game update adding a category must not produce a bogus x/y bar.
        Assert.False(TagSemantics.HasCompletionTotal("SomethingNew"));
        Assert.Equal(ProgressKind.Encounterable, TagSemantics.KindOf("SomethingNew"));
    }

    [Theory]
    [InlineData(null, ProgressStatus.Locked)]
    [InlineData(0, ProgressStatus.InProgress)]
    [InlineData(1, ProgressStatus.Unlocked)]
    [InlineData(42, ProgressStatus.Unlocked)]
    public void Status_Reads_The_Counter(int? value, ProgressStatus expected)
    {
        Assert.Equal(expected, TagSemantics.Status(value));
    }

    [Fact]
    public void Sael_Is_Not_A_Divinity()
    {
        // Sael has Prayer and KillsFor tags but no statue, no lore and no prayer prompt, and
        // grants no blessings. HasStatue is what separates the six real gods from it.
        var sael = TagDatabase.FindGod("Sael");

        Assert.NotNull(sael);
        Assert.False(sael!.HasStatue);
        Assert.DoesNotContain(TagDatabase.All, t => t.Category == "Blessing" && t.God == "Sael");
        Assert.Equal(6, TagDatabase.Gods.Count(g => g.HasStatue));
    }

    [Fact]
    public void Content_Describing_The_Removed_Doom_Mechanic_Is_Gone()
    {
        Assert.Null(TagDatabase.Find("Progression.Blessing.DoomProc"));
        Assert.Null(TagDatabase.Find("Progression.Mushroom.DotFocus_Doom"));
        Assert.DoesNotContain(TagDatabase.All, t => (t.Description ?? "").Contains("Doom", StringComparison.OrdinalIgnoreCase));
    }
}
