using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.Core.Tests;

public class UeRichTextTests
{
    [Fact]
    public void Parses_Real_Seed_Description()
    {
        // Straight from Seed_BasicCrit ("Splendid Ocotillo").
        var segments = UeRichText.Parse("Add +<math>3+2*{l}</>% crit.");

        Assert.Equal(3, segments.Count);
        Assert.Equal(new RichSegment(RichSegmentKind.Text, "Add +"), segments[0]);
        Assert.Equal(new RichSegment(RichSegmentKind.Math, "3+2*{l}"), segments[1]);
        Assert.Equal(new RichSegment(RichSegmentKind.Text, "% crit."), segments[2]);
    }

    [Fact]
    public void Parses_Damage_Types_Math_Attribute_And_Stat_Icon()
    {
        var segments = UeRichText.Parse(
            "deal <Math highlight=\"\">{p_fai}*(2+{l}*1)</> <BL>Sanguine</> damage *<Icon.Stats.Faith/>");

        Assert.Contains(segments, s => s.Kind == RichSegmentKind.Math && s.Text == "{p_fai}*(2+{l}*1)");
        Assert.Contains(segments, s => s.Kind == RichSegmentKind.Styled && s.Text == "Sanguine" && s.Tag == "BL");
        Assert.Contains(segments, s => s.Kind == RichSegmentKind.StatIcon && s.Text == "Faith");
    }

    [Fact]
    public void Parses_Input_Glyphs()
    {
        var segments = UeRichText.Parse("<input name=\"IA_Ability_Primary\"/> Primary: {desc}");
        Assert.Equal(RichSegmentKind.Input, segments[0].Kind);
        Assert.Equal("IA_Ability_Primary", segments[0].Text);
    }

    [Theory]
    [InlineData("3+2*{l}", 1, 5.0)]
    [InlineData("3+2*{l}", 5, 13.0)]
    [InlineData("20+15*{l}", 3, 65.0)]
    [InlineData("{p_fai}*(2+{l}*1)", 4, 6.0)] // p_fai defaults to 1
    [InlineData("2.5 + 0.75 * 2", 1, 4.0)]
    public void Evaluates_Scaling_Formulas(string expr, int level, double expected)
    {
        Assert.True(ScalingMath.TryEvaluate(expr, level, out double result));
        Assert.Equal(expected, result, precision: 5);
    }

    [Fact]
    public void Level_Only_Detection()
    {
        Assert.True(ScalingMath.IsLevelOnly("3+2*{l}"));
        Assert.False(ScalingMath.IsLevelOnly("{p_fai}*(2+{l}*1)"));
    }

    [Fact]
    public void Garbage_Formula_Fails_Gracefully()
    {
        Assert.False(ScalingMath.TryEvaluate("3+*oops", 1, out _));
        Assert.False(ScalingMath.TryEvaluate("(1+2", 1, out _));
    }
}
