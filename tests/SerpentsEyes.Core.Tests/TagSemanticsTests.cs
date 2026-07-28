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
    public void Sael_Is_Excluded_Because_The_Data_Says_So()
    {
        // Sael has Prayer and KillsFor tags but no statue, no lore and no prayer prompt, and
        // grants no blessings. Nothing curated is needed to rule it out.
        var sael = TagDatabase.FindGod("Sael");

        Assert.NotNull(sael);
        Assert.False(sael!.HasStatue);
        Assert.False(sael.IsDivinity);
        Assert.DoesNotContain(TagDatabase.All, t => t.Category == "Blessing" && t.God == "Sael");
    }

    [Fact]
    public void Keeper_Is_Excluded_As_Legacy_Content()
    {
        // The Keeper of Eyes looks complete in the files — statue, lore, prayer prompt — so it
        // cannot be filtered on data alone. It is reworked legacy content that is unreachable in
        // the current game, which is recorded knowledge rather than something extracted.
        var keeper = TagDatabase.FindGod("Keeper");

        Assert.NotNull(keeper);
        Assert.True(keeper!.HasStatue);
        Assert.True(keeper.Hidden);
        Assert.False(keeper.IsDivinity);
    }

    [Fact]
    public void Five_Divinities_Are_Shown()
    {
        var shown = TagDatabase.Gods.Where(g => g.IsDivinity).Select(g => g.Key).ToList();

        Assert.Equal(5, shown.Count);
        Assert.DoesNotContain("Sael", shown);
        Assert.DoesNotContain("Keeper", shown);
        Assert.Contains("Matriarch", shown);
    }

    [Fact]
    public void No_Player_Facing_Text_Mentions_The_Removed_Doom_Mechanic()
    {
        static bool Mentions(string? s) => s?.Contains("Doom", StringComparison.OrdinalIgnoreCase) == true;

        var offenders = TagDatabase.All
            .Where(t => Mentions(t.DisplayName) || Mentions(t.Description) || Mentions(t.RawDescription)
                     || Mentions(t.Flavor) || Mentions(t.UnlockHint)
                     || t.Masteries.Any(m => Mentions(m.Name) || Mentions(m.Description) || Mentions(m.RawDescription)))
            .Select(t => t.DisplayName ?? t.Tag)
            .ToList();

        Assert.True(offenders.Count == 0, $"Doom still visible on: {string.Join(", ", offenders)}");
        Assert.Null(TagDatabase.Find("Progression.Blessing.DoomProc"));
        Assert.Null(TagDatabase.Find("Progression.Mushroom.DotFocus_Doom"));
    }

    [Fact]
    public void Content_Belonging_To_A_Hidden_God_Is_Dropped()
    {
        // A blessing is chosen at its god's statue, so one belonging to an unreachable statue
        // cannot be obtained. Perplexing Awareness was the Keeper's surviving blessing.
        var hidden = TagDatabase.Gods.Where(g => g.Hidden).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(hidden);
        Assert.DoesNotContain(TagDatabase.All, t => t.God is { } g && hidden.Contains(g));
        Assert.Null(TagDatabase.Find("Progression.Blessing.SoulConversionOrbs"));
    }
}
