using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// Shortcut and Location tags name places in the game's internal vocabulary. The level string
/// table has real names for the same places, so the prettified tag is strictly worse than what
/// the game calls them.
/// </summary>
public class PlaceNamesTests
{
    [Theory]
    [InlineData("Attaresh. Cathedral", "The Sanguine Cathedral")]
    [InlineData("Majin.Camp", "Jirahka Ruins")]
    [InlineData("Blacksmith", "Djinn's Domain")]
    public void Resolves_Direct_Matches(string tagName, string expected)
    {
        Assert.Equal(expected, PlaceNames.LevelTitle(tagName));
    }

    [Theory]
    [InlineData("Majin.Bridge.0")]
    [InlineData("Majin.Bridge.1")]
    [InlineData("Majin.Bridge.2")]
    public void Several_Shortcuts_Can_Share_One_Level(string tagName)
    {
        // The trailing index distinguishes shortcuts inside one level; it is not part of the
        // level key.
        Assert.Equal("The Great Divide", PlaceNames.LevelTitle(tagName));
    }

    [Fact]
    public void Resolves_Index_Suffixed_Tags()
    {
        Assert.Equal("The Grand Library", PlaceNames.LevelTitle("Attaresh.Library.0"));
    }

    [Theory]
    [InlineData("Attaresh.CityPlaza")]
    [InlineData("Attaresh. CityPlaza_02")]
    [InlineData("Attaresh. CityPlaza_03")]
    public void Resolves_The_Plaza_Spelling_Difference(string tagName)
    {
        // Shortcut tags say "CityPlaza"; the level key says "plaza".
        Assert.Equal("Streets of Min'Esh", PlaceNames.LevelTitle(tagName));
    }

    [Fact]
    public void Every_Shortcut_And_Location_In_The_Fixture_Resolves()
    {
        var profile = SaveProfile.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_pending_tags.sav"));

        var unresolved = profile.Records
            .Where(r => r.Category is "Shortcut" or "Location")
            .Where(r => PlaceNames.LevelTitle(r.Name) is null)
            .Select(r => r.Name)
            .ToList();

        Assert.True(unresolved.Count == 0, $"unresolved: {string.Join(", ", unresolved)}");
    }

    [Fact]
    public void Unknown_Places_Return_Null_Rather_Than_Guessing()
    {
        Assert.Null(PlaceNames.LevelTitle("Nowhere.Invented.7"));
        Assert.Null(PlaceNames.LevelTitle(""));
    }

    [Fact]
    public void Quest_Collectibles_Carry_The_Games_Own_Pickup_Line()
    {
        // StringTable_Rewards says where each quest item comes from, which is the only genuinely
        // useful quest prose the game ships in a readable form.
        string? kidney = TagDatabase.QuestFlavor("Progression.Quest.LordMalvo.BerserkerKidney");

        Assert.NotNull(kidney);
        Assert.Contains("Sunclad Wanderer", kidney!, StringComparison.Ordinal);
        Assert.NotNull(TagDatabase.QuestFlavor("Progression.Quest.Mujica.GlassShard"));
    }

    [Fact]
    public void Stages_Have_No_Flavor_Because_The_Game_Ships_None()
    {
        Assert.Null(TagDatabase.QuestFlavor("Progression.Quest.Mujica.Part.0"));
    }
}
