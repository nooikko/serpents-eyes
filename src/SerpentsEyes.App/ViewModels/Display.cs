using System.Text.RegularExpressions;

namespace SerpentsEyes.App.ViewModels;

/// <summary>Presentation helpers for turning raw save tags into readable text.</summary>
public static partial class Display
{
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelBoundary();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"WellRounded" → "Well Rounded", "Run.Started" → "Run · Started".</summary>
    public static string Prettify(string raw)
    {
        string spaced = CamelBoundary().Replace(raw, "$1 $2");
        spaced = spaced.Replace("_", " ").Replace(".", " · ");
        return Whitespace().Replace(spaced, " ").Trim();
    }

    /// <summary>Save-file category key → the game's own vocabulary (mushrooms are "Callings", utilities are "Relics").</summary>
    public static string CategoryDisplay(string categoryKey) => categoryKey switch
    {
        "Class" => "Classes",
        "Weapon" => "Weapons",
        "Item" => "Seeds",
        "Blessing" => "Blessings",
        "Mushroom" => "Callings",
        "Utility" => "Relics",
        "Prayer" => "Prayers",
        "KillsFor" => "Statue Kills",
        "Kill" => "Boss Kills",
        "Curse" => "Curses",
        "Quest" => "Quests",
        "Shortcut" => "Shortcuts",
        "Location" => "Locations",
        "Emotes" => "Emotes",
        "Meta" => "Meta",
        _ => Prettify(categoryKey),
    };

    /// <summary>Singular label for a loadout chip, in game vocabulary.</summary>
    public static string CategorySingular(string categoryKey) => categoryKey switch
    {
        "Item" => "Seed",
        "Mushroom" => "Calling",
        "Utility" => "Relic",
        _ => Prettify(categoryKey),
    };
}

/// <summary>A save file the user can pick from the title bar.</summary>
public sealed record ProfileChoice(string Path, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Sidebar entry: one tag category. When the game data knows the full content list
/// for the category, <see cref="Total"/> is set and completion can be shown.
/// </summary>
public sealed record CategoryItem(string Key, string DisplayName, int Owned, int? Total)
{
    public bool HasCompletion => Total is not null;
    public string CountText => Total is { } total ? $"{Owned}/{total}" : Owned.ToString();
    public bool IsComplete => Total is { } total && Owned >= total;
    public double ProgressMax => Total ?? 1;
    public double ProgressValue => Owned;
}

/// <summary>One row in the record table; locked rows are known content absent from the save.</summary>
public sealed record RecordRow(
    string DisplayName, string RawTag, string CategoryName, int Value, string Tooltip,
    bool IsLocked = false, string? UnlockHint = null)
{
    public bool HasUnlockHint => UnlockHint is not null;
    public double NameOpacity => IsLocked ? 0.45 : 1.0;
}

/// <summary>One equipped-loadout chip in the run snapshot card.</summary>
public sealed record LoadoutChip(string Kind, string Name);
