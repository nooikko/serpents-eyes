using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

public class GameDataV2Tests
{
    [Fact]
    public void Raw_Description_Preserves_Scaling_Markup()
    {
        var seed = TagDatabase.Find("Progression.Item.BasicCrit");
        Assert.NotNull(seed);
        Assert.Equal("Splendid Ocotillo", seed.DisplayName);
        Assert.Contains("<math>3+2*{l}</>", seed.RawDescription);
        Assert.DoesNotContain("<math>", seed.Description); // cleaned copy stays clean
    }

    [Fact]
    public void Icons_Exist_For_Weapons_Classes_And_Relics()
    {
        Assert.Equal("WeaponCards_Cinder_02", TagDatabase.Find("Progression.Weapon.BerserkerBlade")?.IconKey);
        Assert.NotNull(TagDatabase.Find("Progression.Class.Summoner")?.IconKey);
        Assert.NotNull(TagDatabase.Find("Progression.Utility.Sparkball")?.IconKey);
    }

    [Fact]
    public void Gods_Table_Has_All_Seven_With_Full_Names()
    {
        Assert.Equal(7, TagDatabase.Gods.Count);
        Assert.Equal("the Dream Thing", TagDatabase.FindGod("Dream")?.FullName);
        Assert.Equal("the Keeper of Eyes", TagDatabase.FindGod("Keeper")?.FullName);
        Assert.Equal("the Weeping Matriarch", TagDatabase.FindGod("Matriarch")?.FullName);
        Assert.Equal("Magnolia", TagDatabase.FindGod("Tree")?.FullName);
        Assert.False(TagDatabase.FindGod("Sael")?.HasStatue);
    }

    [Fact]
    public void Blessing_Lock_Rules_Are_The_Real_Ingame_Strings()
    {
        Assert.Equal("Kill 1 boss while blessed by this god to unlock", TagDatabase.BlessingLockRules[1]);
        Assert.Equal("Kill 3 bosses while blessed by this god to unlock", TagDatabase.BlessingLockRules[3]);
    }

    [Fact]
    public void Weapons_Carry_Masteries()
    {
        var blade = TagDatabase.Find("Progression.Weapon.BerserkerBlade");
        Assert.NotNull(blade);
        Assert.NotEmpty(blade.Masteries);
        Assert.Contains(blade.Masteries, m => m.Name.Contains("Vali's Claw"));
    }

    [Fact]
    public void Wiki_Hints_Fill_Unlock_Gaps()
    {
        Assert.Contains("Dreamcaller", TagDatabase.Find("Progression.Class.Summoner")?.UnlockHint);
        Assert.Contains("The Carver", TagDatabase.Find("Progression.Mushroom.EarthenMight")?.UnlockHint);
        // Game-authored strings must not be overwritten by wiki hints.
        Assert.Equal("Defeat the Great Sister", TagDatabase.Find("Progression.Weapon.BerserkerBlade")?.UnlockHint);
    }

    [Theory]
    [InlineData(null, ProgressStatus.Locked)]
    [InlineData(0, ProgressStatus.InProgress)]
    [InlineData(1, ProgressStatus.Unlocked)]
    [InlineData(45, ProgressStatus.Unlocked)]
    public void Status_Maps_Counters_To_Meaning(int? value, ProgressStatus expected)
        => Assert.Equal(expected, TagSemantics.Status(value));

    [Fact]
    public void CounterText_Speaks_Human()
    {
        Assert.Equal("45 boss kills while blessed", TagSemantics.CounterText("KillsFor", "Progression.KillsFor.Tree", 45));
        Assert.Equal("Prayed 25 times", TagSemantics.CounterText("Prayer", "Progression.Prayer.Matriarch", 25));
        Assert.Equal("Defeated 13 times", TagSemantics.CounterText("Kill", "Progression.Kill.Vali", 13));
        Assert.Null(TagSemantics.CounterText("Class", "Progression.Class.Tanky", 1));
        Assert.Contains("starting Aspect", TagSemantics.CounterText("Class", TagSemantics.StartingAspectTag, 4));
        Assert.Equal("Obtained ×3", TagSemantics.CounterText("Mushroom", "Progression.Mushroom.FatRoll", 3));
    }
}
