using System.Text.RegularExpressions;

namespace SerpentsEyes.Core.GameData;

/// <summary>Turns raw tag segments into readable text.</summary>
/// <remarks>
/// Lives in Core rather than the app because the game data itself needs it: quest owners and
/// step names come straight out of tag segments and have to be readable before they reach a UI.
/// </remarks>
public static partial class TagText
{
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"WellRounded" → "Well Rounded". Underscores become spaces; dots become "·".</summary>
    public static string Humanize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        string spaced = CamelBoundary().Replace(raw, "$1 $2");
        spaced = spaced.Replace("_", " ").Replace(".", " · ");
        return Whitespace().Replace(spaced, " ").Trim();
    }

    /// <summary>Like <see cref="Humanize"/> but keeps dots as spaces, for names read as one phrase.</summary>
    public static string SplitWords(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        string spaced = CamelBoundary().Replace(raw, "$1 $2");
        spaced = spaced.Replace("_", " ").Replace(".", " ");
        return Whitespace().Replace(spaced, " ").Trim();
    }
}
