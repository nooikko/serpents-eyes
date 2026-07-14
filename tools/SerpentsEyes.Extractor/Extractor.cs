using System.Text;
using System.Text.RegularExpressions;

namespace SerpentsEyes.Extractor;

internal static partial class Extractor
{
    private const string WorkbenchReports = @"C:\Users\elija\Documents\serpents_gaze_workbench\reports";

    /// <summary>Tag categories that live in per-item definition assets.</summary>
    [GeneratedRegex(@"^Progression\.(Class|Weapon|Blessing|Mushroom|Item|Utility|Curse)\.[A-Za-z0-9_.]+$")]
    private static partial Regex DefinitionTag();

    [GeneratedRegex(@"^Gameplay\.Prayer\.([A-Za-z0-9]+)$")]
    private static partial Regex PrayerAffinity();

    /// <summary>A string-table key: lowercase snake with a known display-text suffix.</summary>
    [GeneratedRegex(@"^[a-z0-9]+(_[a-z0-9]+)*_(name|desc|description|title|passive|unlock|lore)$")]
    private static partial Regex DisplayKey();

    [GeneratedRegex(@"^level_([a-z0-9_]+)_title$")]
    private static partial Regex LevelTitleKey();

    /// <summary>UE rich-text markup: &lt;tag&gt;…&lt;/&gt; wrappers and self-closing &lt;tag/&gt;.</summary>
    [GeneratedRegex(@"</?[^<>]*>")]
    private static partial Regex RichTextMarkup();

    /// <summary>Asset basename prefixes that are definition assets, in name-resolution priority order.</summary>
    private static readonly string[] PrefixPriority =
        ["Tree_", "Class_", "CA_", "Blessing_", "Mushroom_", "Seed_", "Utility_", "Weed_", "InventoryItem_"];

    private static readonly string[] Gods =
        ["Dream", "Heretic", "Keeper", "Matriarch", "Reflection", "Sael", "Tree"];

    private sealed record AssetInfo(string BaseName, string RelPath, List<string> Tags,
        Dictionary<string, string?> Keys, string? PrayerGod, List<string> OrderedStrings);

    public static int Run(string contentRoot)
    {
        // 1. String tables: authoritative key -> display text.
        var tableEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(Path.Combine(contentRoot, "StringTables"), "*.uexp"))
        {
            var (ns, entries) = StringTableParser.Parse(File.ReadAllBytes(path));
            tableNames.Add(ns);
            foreach (var (key, value) in entries)
            {
                tableEntries[key] = value;
            }
        }
        Console.WriteLine($"String tables: {tableEntries.Count} entries");

        // FText namespaces seen in definition assets; some don't have a matching table.
        tableNames.UnionWith(["Weapons", "Blessings", "Mushrooms", "Seeds", "Relics", "Utility", "Items", "Classes", "Curses"]);

        // 2. Definition assets.
        var assets = new List<AssetInfo>();
        foreach (string uasset in EnumerateDefinitionAssets(contentRoot))
        {
            var asset = ReadAsset(contentRoot, uasset, tableNames);
            if (asset.Tags.Count > 0)
            {
                assets.Add(asset);
            }
        }
        Console.WriteLine($"Definition assets carrying tags: {assets.Count}");

        // 3. Group per tag, pick the best asset for names by prefix priority.
        var byTag = new Dictionary<string, List<AssetInfo>>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            foreach (string tag in asset.Tags)
            {
                (byTag.TryGetValue(tag, out var list) ? list : byTag[tag] = []).Add(asset);
            }
        }

        var entriesOut = new List<TagEntry>();
        foreach (var (tag, carriers) in byTag.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            carriers.Sort((a, b) => PrefixRank(a.BaseName).CompareTo(PrefixRank(b.BaseName)));
            string category = tag.Split('.')[1];

            string? name = null, desc = null, unlock = null, flavor = null, god = null;
            foreach (var carrier in carriers)
            {
                name ??= Resolve(carrier, tableEntries, "_name");
                desc ??= Resolve(carrier, tableEntries, "_desc", "_description") ?? Resolve(carrier, tableEntries, "_passive");
                unlock ??= Resolve(carrier, tableEntries, "_unlock");
                flavor ??= carrier.Keys.GetValueOrDefault("__flavor");
                god ??= carrier.PrayerGod;
            }

            string[] internalIds = [.. carriers.Select(c => c.BaseName).Distinct(StringComparer.OrdinalIgnoreCase)];
            entriesOut.Add(new TagEntry(tag, category, Clean(name), Clean(desc), Clean(unlock), Clean(flavor), god, internalIds));
        }

        // 4. Synthetic entries for the gods (Prayer.* and KillsFor.* families).
        foreach (string godName in Gods)
        {
            string? lore = Clean(tableEntries.GetValueOrDefault($"{godName.ToLowerInvariant()}_lore"));
            entriesOut.Add(new TagEntry($"Progression.Prayer.{godName}", "Prayer",
                $"The {godName}", lore, null, null, godName, []));
            entriesOut.Add(new TagEntry($"Progression.KillsFor.{godName}", "KillsFor",
                $"Kills for the {godName}", lore, null, null, godName, []));
        }

        // 5. Map titles from the Levels table.
        var mapTitles = new List<(string Key, string Title)>();
        foreach (var (key, value) in tableEntries)
        {
            var match = LevelTitleKey().Match(key);
            if (match.Success && value.Length > 0)
            {
                mapTitles.Add((match.Groups[1].Value.Replace("_", ""), value));
            }
        }
        mapTitles.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        // 6. Emit.
        string generatedPath = Path.Combine(FindRepoRoot(), "src", "SerpentsEyes.Core", "GameData", "TagDatabase.g.cs");
        File.WriteAllText(generatedPath, GenerateCSharp(entriesOut, mapTitles));
        Console.WriteLine($"Wrote {generatedPath}");

        string jsonPath = Path.Combine(WorkbenchReports, "tag_database.json");
        File.WriteAllText(jsonPath, GenerateJson(entriesOut, mapTitles));
        Console.WriteLine($"Wrote {jsonPath}");

        // 7. Summary.
        Console.WriteLine($"\nTags: {entriesOut.Count} | maps: {mapTitles.Count}");
        foreach (var group in entriesOut.GroupBy(e => e.Category).OrderBy(g => g.Key))
        {
            int named = group.Count(e => e.DisplayName is not null);
            Console.WriteLine($"  {group.Key,-10} {group.Count(),3} tags, {named,3} named");
        }
        var unnamed = entriesOut.Where(e => e.DisplayName is null).ToList();
        if (unnamed.Count > 0)
        {
            Console.WriteLine($"\nUnnamed tags ({unnamed.Count}):");
            foreach (var e in unnamed)
            {
                Console.WriteLine($"  {e.Tag}  (assets: {string.Join(", ", e.InternalIds)})");
            }
        }
        return 0;
    }

    private static IEnumerable<string> EnumerateDefinitionAssets(string contentRoot)
    {
        string gas = Path.Combine(contentRoot, "GameplayAbilitySystem");
        foreach (string file in Directory.EnumerateFiles(gas, "*.uasset", SearchOption.AllDirectories))
        {
            if (PrefixRank(Path.GetFileNameWithoutExtension(file)) < int.MaxValue)
            {
                yield return file;
            }
        }
        string curses = Path.Combine(contentRoot, "Blueprints", "CurseCards");
        foreach (string file in Directory.EnumerateFiles(curses, "CA_*.uasset", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static int PrefixRank(string baseName)
    {
        for (int i = 0; i < PrefixPriority.Length; i++)
        {
            if (baseName.StartsWith(PrefixPriority[i], StringComparison.Ordinal))
            {
                return i;
            }
        }
        return int.MaxValue;
    }

    private static AssetInfo ReadAsset(string contentRoot, string uassetPath, HashSet<string> namespaces)
    {
        string baseName = Path.GetFileNameWithoutExtension(uassetPath);
        string relPath = Path.GetRelativePath(contentRoot, uassetPath).Replace('\\', '/');

        var tags = new List<string>();
        string? prayerGod = null;
        foreach (string s in UexpStrings.Scan(File.ReadAllBytes(uassetPath)))
        {
            if (DefinitionTag().IsMatch(s) && !tags.Contains(s))
            {
                tags.Add(s);
            }
            var prayer = PrayerAffinity().Match(s);
            if (prayer.Success)
            {
                prayerGod = prayer.Groups[1].Value;
            }
        }

        // The .uexp holds FText blobs as (namespace, key, inline value) — the value is
        // absent when the text resolves through a string table at runtime.
        var keys = new Dictionary<string, string?>(StringComparer.Ordinal);
        var ordered = new List<string>();
        string uexpPath = Path.ChangeExtension(uassetPath, ".uexp");
        if (File.Exists(uexpPath))
        {
            ordered = UexpStrings.Scan(File.ReadAllBytes(uexpPath));
            for (int i = 0; i < ordered.Count; i++)
            {
                string s = ordered[i];
                if (!DisplayKey().IsMatch(s))
                {
                    continue;
                }
                string? value = null;
                if (i + 1 < ordered.Count)
                {
                    string next = ordered[i + 1];
                    bool nextIsKey = DisplayKey().IsMatch(next);
                    bool nextIsNamespace = namespaces.Contains(next);
                    if (!nextIsKey && !nextIsNamespace)
                    {
                        value = next;
                        i++;
                        // A quoted line straight after a description is flavor text.
                        if (i + 1 < ordered.Count && ordered[i + 1].StartsWith('"') &&
                            (s.EndsWith("_desc") || s.EndsWith("_description")))
                        {
                            keys["__flavor"] = ordered[++i].Trim('"');
                        }
                    }
                }
                keys[s] = value;
            }
        }
        return new AssetInfo(baseName, relPath, tags, keys, prayerGod, ordered);
    }

    /// <summary>Finds the display text for a key suffix: string-table value wins, inline value is the fallback.</summary>
    private static string? Resolve(AssetInfo asset, Dictionary<string, string> tables, params string[] suffixes)
    {
        foreach (string suffix in suffixes)
        {
            // Prefer the shortest matching key: "stronk_name" over "stronk_soul_name".
            foreach (var key in asset.Keys.Keys.Where(k => k.EndsWith(suffix, StringComparison.Ordinal))
                         .OrderBy(k => k.Length))
            {
                string? value = tables.GetValueOrDefault(key) ?? asset.Keys[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        return null;
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        string cleaned = RichTextMarkup().Replace(text, "");
        cleaned = cleaned.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SerpentsEyes.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new InvalidOperationException("Could not locate repo root (SerpentsEyes.slnx)");
    }

    internal sealed record TagEntry(string Tag, string Category, string? DisplayName, string? Description,
        string? UnlockHint, string? Flavor, string? God, string[] InternalIds);

    private static string GenerateCSharp(List<TagEntry> entries, List<(string Key, string Title)> mapTitles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by tools/SerpentsEyes.Extractor from Serpent's Gaze game data.");
        sb.AppendLine("// Do not edit by hand — re-run the extractor instead.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("namespace SerpentsEyes.Core.GameData;");
        sb.AppendLine();
        sb.AppendLine("public static partial class TagDatabase");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly GameTagInfo[] Entries =");
        sb.AppendLine("    [");
        foreach (var e in entries)
        {
            sb.Append("        new(").Append(Lit(e.Tag)).Append(", ").Append(Lit(e.Category)).Append(", ")
              .Append(Lit(e.DisplayName)).Append(", ").Append(Lit(e.Description)).Append(", ")
              .Append(Lit(e.UnlockHint)).Append(", ").Append(Lit(e.Flavor)).Append(", ")
              .Append(Lit(e.God)).Append(", [")
              .Append(string.Join(", ", e.InternalIds.Select(Lit))).AppendLine("]),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    private static readonly (string Key, string Title)[] MapTitleEntries =");
        sb.AppendLine("    [");
        foreach (var (key, title) in mapTitles)
        {
            sb.Append("        (").Append(Lit(key)).Append(", ").Append(Lit(title)).AppendLine("),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateJson(List<TagEntry> entries, List<(string Key, string Title)> mapTitles)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            tags = entries,
            maps = mapTitles.Select(m => new { m.Key, m.Title }),
        }, options);
    }

    private static string Lit(string? s)
        => s is null
            ? "null"
            : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
}
