namespace SerpentsEyes.Core.GameData;

/// <summary>What a category's counters actually represent.</summary>
/// <remarks>
/// Every progression tag stores a cumulative counter, but the counters do not all mean the same
/// thing, and treating them uniformly produces wrong statements. An Aspect really is unlocked
/// once and stays unlocked; a Blessing counter is how many times it has been taken across runs,
/// so "20 of 32 Blessings unlocked" describes something the game does not model.
/// </remarks>
public enum ProgressKind
{
    /// <summary>A one-off unlock that persists: Aspects and Weapons. "12 of 14 unlocked" is true.</summary>
    Unlock,

    /// <summary>
    /// Run-scoped content that is offered repeatedly; the counter is how many times it has been
    /// taken. Blessings, Seeds, Callings and Relics. Best described as discovered, not unlocked.
    /// </summary>
    Encounterable,

    /// <summary>A one-off world flag with no meaningful count: Shortcuts, Locations, Emotes.</summary>
    Checklist,

    /// <summary>The count is the point: Boss Kills, Prayers, statue kills.</summary>
    Tally,

    /// <summary>Ordered questlines, handled by <see cref="QuestLines"/>.</summary>
    Quest,
}

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

    /// <summary>
    /// Maps a raw counter to its meaning: absent means never reached, 0 means reached but not
    /// completed, and anything higher means unlocked (the value is how many times).
    /// </summary>
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

    /// <summary>
    /// What a category's counters represent. Drives whether a completion bar makes sense and
    /// whether "unlocked" is an honest word to use.
    /// </summary>
    public static ProgressKind KindOf(string category) => category switch
    {
        "Class" or "Weapon" => ProgressKind.Unlock,
        "Blessing" or "Item" or "Mushroom" or "Utility" or "Curse" => ProgressKind.Encounterable,
        "Shortcut" or "Location" or "Emotes" => ProgressKind.Checklist,
        "Kill" or "Prayer" or "KillsFor" or "Meta" => ProgressKind.Tally,
        "Quest" => ProgressKind.Quest,
        _ => ProgressKind.Encounterable,
    };

    /// <summary>
    /// True when an "x of y" completion bar is meaningful for the category. Only real unlocks
    /// have a total worth completing; a tally has no ceiling and a checklist has no catalogue of
    /// things you have not found yet.
    /// </summary>
    public static bool HasCompletionTotal(string category) => KindOf(category) is ProgressKind.Unlock;

    /// <summary>
    /// Verb for having a thing, appropriate to the category: "Unlocked" for a persistent unlock,
    /// "Discovered" for something offered per run.
    /// </summary>
    public static string HaveVerb(string category) => KindOf(category) switch
    {
        ProgressKind.Unlock => "Unlocked",
        ProgressKind.Checklist => "Found",
        ProgressKind.Tally => "Recorded",
        _ => "Discovered",
    };

    /// <summary>Word for the opposite state, e.g. "Locked" or "Not yet seen".</summary>
    public static string MissingVerb(string category) => KindOf(category) switch
    {
        ProgressKind.Unlock => "Locked",
        ProgressKind.Checklist => "Not found",
        _ => "Not yet seen",
    };
}
