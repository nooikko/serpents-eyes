

namespace SerpentsEyes.Core.GameData;

/// <summary>What kind of step a quest tag represents.</summary>
public enum QuestStepKind
{
    /// <summary>A numbered stage of the questline. These are the backbone and are strictly ordered.</summary>
    Part,

    /// <summary>A numbered side encounter within the questline.</summary>
    Event,

    /// <summary>A stage the game marks skippable.</summary>
    Optional,

    /// <summary>A collectible the questline asks for. The counter is how many you have handed over.</summary>
    Item,

    /// <summary>Bookkeeping the game keeps against the questline, e.g. how many runs you have talked to them.</summary>
    Other,
}

/// <summary>One quest tag, decomposed into who it belongs to and where it sits in their questline.</summary>
/// <param name="OwnerKey">Raw owner segment from the tag, e.g. "LordMalvo".</param>
/// <param name="OwnerName">Display name for that owner.</param>
/// <param name="Kind">What sort of step this is.</param>
/// <param name="Order">Index within its kind, or -1 when the tag is not numbered.</param>
/// <param name="Label">Human-readable label for the step.</param>
/// <param name="FullTag">The original tag.</param>
/// <param name="Value">Counter from the save.</param>
public sealed record QuestStep(
    string OwnerKey,
    string OwnerName,
    QuestStepKind Kind,
    int Order,
    string Label,
    string FullTag,
    int Value);

/// <summary>
/// Decomposes <c>Progression.Quest.*</c> tags into ordered questlines per NPC.
/// </summary>
/// <remarks>
/// The game stores quest progress as <c>Progression.Quest.&lt;Owner&gt;.Part.&lt;N&gt;</c>, with
/// <c>Event</c>, <c>Skippable</c> and bare collectible segments alongside. The owner segment
/// matches the folders under <c>Content/Quests</c>, so the tag alone carries both whose quest it
/// is and what order the stages run in — no extra data needed.
///
/// The quest assets themselves are compiled blueprints with no step prose in them, so steps are
/// labelled structurally ("Stage 1") rather than with in-game descriptions, which the game does
/// not ship in a form we can read.
/// </remarks>
public static class QuestLines
{
    /// <summary>Tag prefix that identifies a quest record.</summary>
    public const string Category = "Quest";

    /// <summary>
    /// Display names for quest owners. Where the game's own StringTable_Characters has a name it
    /// is used verbatim; the rest are the folder names under Content/Quests, spaced out.
    /// </summary>
    private static readonly Dictionary<string, string> OwnerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TheWitness"] = "The Witness",       // StringTable_Characters: thewitness_name
        ["Vagabond"] = "Vagabond",            // StringTable_Characters: vagabond_name
        ["Mujica"] = "Mujica",                // StringTable_Characters: mujica_name
        ["LordMalvo"] = "Lord Malvo",
        ["Druid"] = "The Druid",
        ["Shaper"] = "The Shaper",
        ["Sael"] = "Sael",
        ["PrisonerBrothers"] = "The Prisoner Brothers",
    };

    /// <summary>Display name for an owner key, falling back to splitting the key on word boundaries.</summary>
    public static string OwnerDisplayName(string ownerKey)
        => OwnerNames.TryGetValue(ownerKey, out string? name) ? name : TagText.SplitWords(ownerKey);

    /// <summary>
    /// Parses a quest record. Returns null when the record is not a quest tag.
    /// </summary>
    public static QuestStep? Parse(TagRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!string.Equals(record.Category, Category, StringComparison.Ordinal))
        {
            return null;
        }

        // record.Name is everything after "Progression.Quest.", e.g. "LordMalvo.Part.2".
        string[] parts = record.Name.Split('.');
        if (parts.Length == 0 || parts[0].Length == 0)
        {
            return null;
        }

        string owner = parts[0];
        string ownerName = OwnerDisplayName(owner);
        string[] rest = parts[1..];

        if (rest.Length >= 2 && int.TryParse(rest[1], out int index))
        {
            QuestStepKind kind = rest[0] switch
            {
                "Part" => QuestStepKind.Part,
                "Event" => QuestStepKind.Event,
                "Skippable" => QuestStepKind.Optional,
                _ => QuestStepKind.Other,
            };

            // Numbered steps get their label in Build, where the whole questline is visible:
            // tag numbering is not consistent between owners (most start at 0, Lord Malvo's
            // parts start at 1), so position in the line is the only reliable stage number.
            string label = kind == QuestStepKind.Other
                ? TagText.SplitWords(string.Join(' ', rest))
                : string.Empty;

            return new QuestStep(owner, ownerName, kind, index, label, record.FullTag, record.Value);
        }

        // Unnumbered: a collectible (LordMalvo.PygmyMeat) or bookkeeping
        // (Druid.RunsWhenInteracting).
        string tail = string.Join('.', rest);
        bool bookkeeping = tail.StartsWith("Runs", StringComparison.OrdinalIgnoreCase);
        return new QuestStep(
            owner,
            ownerName,
            bookkeeping ? QuestStepKind.Other : QuestStepKind.Item,
            -1,
            TagText.SplitWords(tail),
            record.FullTag,
            record.Value);
    }

    /// <summary>
    /// Groups a profile's quest records into questlines, owners in alphabetical order by display
    /// name, and steps within each owner in narrative order: parts, then encounters, then
    /// optional steps, then collectibles and bookkeeping.
    /// </summary>
    public static IReadOnlyList<QuestLine> Build(SaveProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return [.. profile.Records
            .Select(Parse)
            .Where(s => s is not null)
            .Select(s => s!)
            .GroupBy(s => s.OwnerKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new QuestLine(g.Key, g.First().OwnerName, NumberSteps(g)))
            .OrderBy(q => q.OwnerName, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Orders one owner's steps and labels the numbered ones by their position in the line.
    /// </summary>
    private static IReadOnlyList<QuestStep> NumberSteps(IEnumerable<QuestStep> steps)
    {
        var ordered = steps
            .OrderBy(s => s.Kind)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Label, StringComparer.Ordinal)
            .ToList();

        var counters = new Dictionary<QuestStepKind, int>();
        for (int i = 0; i < ordered.Count; i++)
        {
            QuestStep step = ordered[i];
            if (step.Label.Length > 0)
            {
                continue;
            }

            counters.TryGetValue(step.Kind, out int seen);
            counters[step.Kind] = ++seen;

            ordered[i] = step with
            {
                Label = step.Kind switch
                {
                    QuestStepKind.Part => $"Stage {seen}",
                    QuestStepKind.Event => $"Encounter {seen}",
                    QuestStepKind.Optional => $"Optional step {seen}",
                    _ => step.Label,
                },
            };
        }

        return ordered;
    }
}

/// <summary>All of one NPC's quest steps, in narrative order.</summary>
/// <param name="OwnerKey">Raw owner segment from the tag.</param>
/// <param name="OwnerName">Display name.</param>
/// <param name="Steps">Steps in order.</param>
public sealed record QuestLine(string OwnerKey, string OwnerName, IReadOnlyList<QuestStep> Steps)
{
    /// <summary>Numbered stages only — the questline's backbone.</summary>
    public IEnumerable<QuestStep> Parts => Steps.Where(s => s.Kind == QuestStepKind.Part);

    /// <summary>How many stages have been completed (a stage counts as done once its value is above zero).</summary>
    public int CompletedParts => Parts.Count(s => s.Value > 0);

    /// <summary>How many stages this questline has records for.</summary>
    public int TotalParts => Parts.Count();

    /// <summary>True when every recorded stage is complete.</summary>
    public bool IsComplete => TotalParts > 0 && CompletedParts == TotalParts;
}
