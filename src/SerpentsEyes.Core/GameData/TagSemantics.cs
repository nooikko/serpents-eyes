namespace SerpentsEyes.Core.GameData;

/// <summary>Semantic state of a progression tag, derived from its counter.</summary>
public enum ProgressStatus
{
    /// <summary>Tag absent from the save — never touched.</summary>
    Locked,

    /// <summary>Counter is 0 — reached but not completed (e.g. a quest step in progress).</summary>
    InProgress,

    /// <summary>Counter ≥ 1 — unlocked / discovered / done at least once.</summary>
    Unlocked,
}

/// <summary>
/// Turns raw save counters into meaning. The integer on every progression tag is a
/// cumulative "times acquired" counter (grants stack via the game's
/// R_AddProgressionTags reward), but what a count *means* differs by category.
/// </summary>
public static class TagSemantics
{
    /// <summary>The tag of the starting Aspect, re-granted on every run (hence its high count).</summary>
    public const string StartingAspectTag = "Progression.Class.Newborn";

    public static ProgressStatus Status(int? value) => value switch
    {
        null => ProgressStatus.Locked,
        0 => ProgressStatus.InProgress,
        _ => ProgressStatus.Unlocked,
    };

    /// <summary>
    /// Human explanation of a counter for a given category, e.g. "Obtained ×4",
    /// "45 boss kills while blessed", "Prayed 25 times". Returns null when the
    /// counter carries no extra information worth showing (value 1 on an unlockable).
    /// </summary>
    public static string? CounterText(string category, string tag, int value)
    {
        if (value == 0)
        {
            return category == "Quest" ? "Step reached, not yet completed" : "Found, not yet completed";
        }

        return category switch
        {
            "KillsFor" => value == 1 ? "1 boss kill while blessed" : $"{value} boss kills while blessed",
            "Prayer" => value == 1 ? "Prayed once" : $"Prayed {value} times",
            "Kill" => value == 1 ? "Defeated once" : $"Defeated {value} times",
            "Meta" => $"{value}",
            "Quest" => value == 1 ? null : $"Done ×{value}",
            "Class" when tag == StartingAspectTag => $"Runs begun ×{value} (starting Aspect, granted every run)",
            _ => value == 1 ? null : $"Obtained ×{value}",
        };
    }

    /// <summary>Categories the UI should hide by default; they remain fully available in the API.</summary>
    public static bool IsMetaCategory(string category) => category == "Meta";
}
