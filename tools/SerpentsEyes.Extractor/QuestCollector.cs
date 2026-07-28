using System.Text;

namespace SerpentsEyes.Extractor;

/// <summary>One questline as the game defines it: an owner and every tag its quest asset names.</summary>
internal sealed record QuestDefinitionEntry(string OwnerKey, IReadOnlyList<string> Tags);

/// <summary>
/// Reads the questline definitions out of <c>Content/Quests</c>.
/// </summary>
/// <remarks>
/// A save only records the stages a player has actually reached, so it cannot say how long a
/// questline is: a save two stages into the Witness's six-stage line looks identical to a
/// completed two-stage line. The quest assets name every stage, which is the only way to show a
/// true "3 of 6" or to list a questline the player has never started at all.
///
/// The tags are read by scanning the asset for <c>Progression.Quest.*</c> strings rather than by
/// parsing UE's property graph. The tag text is stored verbatim in the asset's name table, and a
/// full property parse would need the .usmap this project deliberately does without.
/// </remarks>
internal static class QuestCollector
{
    private const string TagPrefix = "Progression.Quest.";

    public static List<QuestDefinitionEntry> Run(string contentRoot)
    {
        string questsRoot = Path.Combine(contentRoot, "Quests");
        var byOwner = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(questsRoot))
        {
            Console.WriteLine($"No Quests directory under {contentRoot}; skipping questlines");
            return [];
        }

        foreach (string path in Directory.EnumerateFiles(questsRoot, "*.uasset", SearchOption.AllDirectories))
        {
            foreach (string tag in ScanTags(path))
            {
                string rest = tag[TagPrefix.Length..];
                int dot = rest.IndexOf('.');
                if (dot <= 0)
                {
                    continue;
                }

                string owner = rest[..dot];
                if (!byOwner.TryGetValue(owner, out var set))
                {
                    byOwner[owner] = set = new SortedSet<string>(StringComparer.Ordinal);
                }
                set.Add(tag);
            }
        }

        var result = byOwner
            .Select(kv => new QuestDefinitionEntry(kv.Key, [.. kv.Value]))
            .OrderBy(q => q.OwnerKey, StringComparer.Ordinal)
            .ToList();

        Console.WriteLine($"Questlines: {result.Count} owners, {result.Sum(q => q.Tags.Count)} tags");
        return result;
    }

    /// <summary>Pulls every Progression.Quest.* string out of one cooked asset.</summary>
    private static IEnumerable<string> ScanTags(string path)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            yield break;
        }

        var run = new StringBuilder();
        foreach (byte b in data)
        {
            if (b >= 0x20 && b < 0x7F)
            {
                run.Append((char)b);
                continue;
            }

            if (run.Length > TagPrefix.Length && run.ToString().StartsWith(TagPrefix, StringComparison.Ordinal))
            {
                yield return run.ToString();
            }
            run.Clear();
        }

        if (run.Length > TagPrefix.Length && run.ToString().StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            yield return run.ToString();
        }
    }
}
