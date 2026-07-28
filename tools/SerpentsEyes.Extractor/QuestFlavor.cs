namespace SerpentsEyes.Extractor;

/// <summary>Text describing where a quest collectible comes from.</summary>
internal sealed record QuestFlavorEntry(string Tag, string Text);

/// <summary>
/// Matches the reward strings to the quest tags they describe.
/// </summary>
/// <remarks>
/// StringTable_Rewards holds the line shown when a quest collectible is picked up, and those
/// lines name where it comes from: "You pull a weirdly appetizing kidney out of the corpse of
/// the Sunclad Wanderer". That is the one piece of genuinely useful quest prose the game ships
/// in a form we can read — the quest assets themselves are compiled blueprints with no step
/// text in them at all.
///
/// The keys are <c>quest_&lt;owner&gt;_&lt;item&gt;</c> lowercased. Four of the five match the
/// tag's item segment exactly; "BerserkerKidney" is keyed as just "kidney", so an exact match is
/// tried first and a containment match second.
/// </remarks>
internal static class QuestFlavor
{
    private const string KeyPrefix = "quest_";

    public static List<QuestFlavorEntry> Build(
        IReadOnlyDictionary<string, string> tableEntries,
        IReadOnlyList<QuestDefinitionEntry> quests)
    {
        var rewards = tableEntries
            .Where(kv => kv.Key.StartsWith(KeyPrefix, StringComparison.Ordinal) && kv.Value.Length > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var result = new List<QuestFlavorEntry>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (QuestDefinitionEntry quest in quests)
        {
            string owner = quest.OwnerKey.ToLowerInvariant();
            foreach (string tag in quest.Tags)
            {
                string item = tag[(tag.LastIndexOf('.') + 1)..];
                if (item.Length == 0 || char.IsDigit(item[0]))
                {
                    continue; // Part/Event indices, not collectibles.
                }

                string lower = item.ToLowerInvariant();
                string exact = $"{KeyPrefix}{owner}_{lower}";

                string? key = rewards.ContainsKey(exact)
                    ? exact
                    : rewards.Keys.FirstOrDefault(k =>
                        k.StartsWith($"{KeyPrefix}{owner}_", StringComparison.OrdinalIgnoreCase)
                        && lower.Contains(k[($"{KeyPrefix}{owner}_".Length)..], StringComparison.OrdinalIgnoreCase));

                if (key is not null)
                {
                    result.Add(new QuestFlavorEntry(tag, rewards[key]));
                    used.Add(key);
                }
            }
        }

        foreach (string unmatched in rewards.Keys.Where(k => !used.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            Console.WriteLine($"  quest reward string with no matching tag: {unmatched}");
        }

        Console.WriteLine($"Quest collectible sources: {result.Count} matched, {rewards.Count - used.Count} unmatched");
        return [.. result.OrderBy(r => r.Tag, StringComparer.Ordinal)];
    }
}
