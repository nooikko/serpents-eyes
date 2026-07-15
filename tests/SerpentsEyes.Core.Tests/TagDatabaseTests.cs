using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

public class TagDatabaseTests
{
    [Fact]
    public void Contains_All_Definition_Categories()
    {
        var categories = TagDatabase.All.Select(t => t.Category).Distinct().ToHashSet();
        Assert.Superset(
            new HashSet<string> { "Class", "Weapon", "Blessing", "Mushroom", "Item", "Utility", "Curse", "Prayer", "KillsFor" },
            categories);
        Assert.True(TagDatabase.All.Count >= 130, $"Expected 130+ tags, got {TagDatabase.All.Count}");
    }

    [Theory]
    [InlineData("Progression.Class.Summoner", "Aspect of the Summoner")]
    [InlineData("Progression.Weapon.BlacksmithsHammer", "Gatekeeper's Warhammer")]
    [InlineData("Progression.Weapon.BerserkerBlade", "Vali's Cinderblade")]
    [InlineData("Progression.Blessing.ChanceHoT", "Daydreams")]
    [InlineData("Progression.Mushroom.Dexterity", "Finger-Focused Technique")]
    [InlineData("Progression.Curse.HordeCaller", "Dreamcallers")]
    [InlineData("Progression.Curse.Jester", "The Jester")]
    [InlineData("Progression.Prayer.Matriarch", "Devotion · The Weeping Matriarch")]
    public void Known_Tags_Resolve_To_Ingame_Names(string tag, string expectedName)
    {
        Assert.True(TagDatabase.TryGet(tag, out var info), $"Tag {tag} missing from database");
        Assert.Equal(expectedName, info.DisplayName);
    }

    [Fact]
    public void Every_Tag_Has_A_Display_Name()
    {
        var unnamed = TagDatabase.All.Where(t => string.IsNullOrEmpty(t.DisplayName)).Select(t => t.Tag).ToList();
        Assert.Empty(unnamed);
    }

    [Fact]
    public void Loadout_Internal_Ids_Resolve()
    {
        // These are the exact ids the save file stores in the current-run loadout.
        Assert.Equal("Progression.Class.Stronk", TagDatabase.FindByInternalId("Class_Stronk")?.Tag);
        Assert.Equal("Progression.Weapon.BlacksmithsHammer", TagDatabase.FindByInternalId("Tree_Warhammer")?.Tag);
        Assert.Equal("Progression.Mushroom.EarthenMight", TagDatabase.FindByInternalId("Mushroom_EarthenMight")?.Tag);
    }

    [Fact]
    public void Map_Titles_Resolve_From_Save_Level_Names()
    {
        Assert.Equal("Namah, City of Pilgrims", TagDatabase.MapTitle("Majin_HolyCity"));
        Assert.Equal("Jirahka Ruins", TagDatabase.MapTitle("Majin_Camp"));
        Assert.Null(TagDatabase.MapTitle("NotARealMap"));
    }

    [Fact]
    public void Weapon_Unlock_Hints_Are_Extracted()
    {
        Assert.Equal("Defeat the Great Sister", TagDatabase.Find("Progression.Weapon.BerserkerBlade")?.UnlockHint);
    }
}
