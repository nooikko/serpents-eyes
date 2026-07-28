using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SerpentsEyes.Extractor;

internal static partial class Extractor
{
    /// <summary>Tag categories that live in per-item definition assets.</summary>
    [GeneratedRegex(@"^Progression\.(Class|Weapon|Blessing|Mushroom|Item|Utility|Curse)\.[A-Za-z0-9_.]+$")]
    private static partial Regex DefinitionTag();

    /// <summary>Identity tags carried by ALL definition assets, including never-unlockable ones.</summary>
    [GeneratedRegex(@"^Item\.(Seed|Weed|Tree|Blessing|Mushroom|Utility|Class|Curse)\.[A-Za-z0-9_.]+$")]
    private static partial Regex ItemIdentityTag();

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

    [GeneratedRegex(@"^/Game/Textures/UI/")]
    private static partial Regex UiTexturePath();

    /// <summary>Asset basename prefixes that are definition assets, in name-resolution priority order.</summary>
    private static readonly string[] PrefixPriority =
        ["Tree_", "Class_", "CA_", "Blessing_", "Mushroom_", "Seed_", "Utility_", "Weed_", "InventoryItem_"];

    /// <summary>
    /// God tag key → (full in-game name, has a statue, hidden from the Divinities view,
    /// community-wiki theme summary).
    /// </summary>
    /// <remarks>
    /// Two different reasons a tag with a god key is not a Divinity you can devote to:
    ///
    /// <list type="bullet">
    /// <item><description><b>No statue.</b> Sael has a Prayer tag and a KillsFor tag but no
    /// statue, no lore, no prayer prompt and no blessings. It is an NPC with a questline, and
    /// the game data says so on its own.</description></item>
    /// <item><description><b>Hidden.</b> The Keeper of Eyes does have a statue, lore and a
    /// prayer prompt in the files, but it is legacy content that was reworked into other things
    /// and is not reachable in the current game. Nothing in the data distinguishes it, so this
    /// is player knowledge, recorded here deliberately rather than inferred.</description></item>
    /// </list>
    /// </remarks>
    private static readonly (string Key, string FullName, bool HasStatue, bool Hidden, string? Themes)[] GodsMeta =
    [
        ("Dream", "the Dream Thing", true, false, "On-hit, status and Relic-usage blessings"),
        ("Heretic", "the Heretic", true, false, "Crit and Fire blessings"),
        ("Keeper", "the Keeper of Eyes", true, true, null),
        ("Matriarch", "the Weeping Matriarch", true, false, "Blood generation, Sanguine and Bleed blessings"),
        ("Reflection", "the Reflection", true, false, "Buffing, Summons and Physical damage blessings"),
        ("Tree", "Magnolia", true, false, "Support, Healing, Rot and Blight blessings"),
        ("Sael", "Sael", false, false, null),
    ];

    private sealed record AssetInfo(string BaseName, string RelPath, List<string> Tags,
        List<string> ItemTags, Dictionary<string, string?> Keys, string? PrayerGod, string? IconPath)
    {
        /// <summary>
        /// Grouping identity: the progression tag's (category, leaf) when present
        /// (weapons rename between tag families, so progression wins), else the
        /// Item.* identity, else a curse-card fallback from the asset name.
        /// </summary>
        public (string Category, string Leaf)? Identity()
        {
            if (Tags.Count > 0)
            {
                string[] parts = Tags[0].Split('.', 3);
                return (parts[1], parts[2]);
            }
            if (ItemTags.Count > 0)
            {
                string[] parts = ItemTags[0].Split('.', 3);
                return (MapItemCategory(parts[1]), parts[2]);
            }
            if (BaseName.StartsWith("CA_", StringComparison.Ordinal))
            {
                return ("Curse", BaseName[3..]);
            }
            return null;
        }

        private static string MapItemCategory(string itemCategory) => itemCategory switch
        {
            "Seed" or "Weed" => "Item",
            "Tree" => "Weapon",
            _ => itemCategory,
        };
    }

    internal sealed record MasteryEntry(string Name, string Description, string RawDescription);

    internal sealed record TagEntry(string Tag, string Category, bool HasProgression, string? DisplayName,
        string? Description, string? RawDescription, string? UnlockHint, string? Flavor, string? God,
        string? IconKey, string? SymbolKey, List<MasteryEntry> Masteries, string[] InternalIds,
        string? IconPath, string? SymbolPath);

    internal sealed record GodEntry(string Key, string FullName, string? Lore, string? StatuePrompt,
        string? Themes, string? SymbolKey, bool HasStatue, bool Hidden, string? SymbolPath);

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
        tableNames.UnionWith(["Weapons", "Blessings", "Mushrooms", "Seeds", "Relics", "Utility", "Items", "Classes", "Curses", "WeaponUpgrades"]);

        // 2. Definition assets — ALL of them; items without a Progression tag are the
        // always-available part of the catalog.
        var assets = new List<AssetInfo>();
        foreach (string uasset in EnumerateDefinitionAssets(contentRoot))
        {
            var asset = ReadAsset(contentRoot, uasset, tableNames);
            if (asset.Identity() is not null)
            {
                assets.Add(asset);
            }
        }
        Console.WriteLine($"Definition assets: {assets.Count} " +
            $"({assets.Count(a => a.Tags.Count > 0)} with progression tags)");

        // 3. Weapon masteries, grouped by tree family (Trees/<Family>/Upgrades is shared by variants).
        var masteriesByFamily = CollectMasteries(contentRoot, tableNames);
        Console.WriteLine($"Weapon families with masteries: {masteriesByFamily.Count} " +
            $"({masteriesByFamily.Sum(kv => kv.Value.Count)} masteries total)");

        // 4. Group per identity (category, leaf), pick the best asset for names by prefix priority.
        var byIdentity = new Dictionary<(string Category, string Leaf), List<AssetInfo>>();
        foreach (var asset in assets)
        {
            var identity = asset.Identity()!.Value;
            (byIdentity.TryGetValue(identity, out var list) ? list : byIdentity[identity] = []).Add(asset);
        }

        var entriesOut = new List<TagEntry>();
        foreach (var (identity, carriers) in byIdentity.OrderBy(kv => kv.Key.Category, StringComparer.Ordinal)
                     .ThenBy(kv => kv.Key.Leaf, StringComparer.Ordinal))
        {
            carriers.Sort((a, b) => PrefixRank(a.BaseName).CompareTo(PrefixRank(b.BaseName)));
            string category = identity.Category;
            string? progressionTag = carriers.SelectMany(c => c.Tags)
                .FirstOrDefault(t => t.StartsWith($"Progression.{category}.", StringComparison.Ordinal));
            string tag = progressionTag
                ?? carriers.SelectMany(c => c.ItemTags).FirstOrDefault()
                ?? $"Item.Curse.{identity.Leaf}";

            string? name = null, rawDesc = null, unlock = null, flavor = null, god = null;
            string? iconPath = null, symbolPath = null;
            foreach (var carrier in carriers)
            {
                name ??= Resolve(carrier, tableEntries, "_name");
                rawDesc ??= Resolve(carrier, tableEntries, "_desc", "_description") ?? Resolve(carrier, tableEntries, "_passive");
                unlock ??= Resolve(carrier, tableEntries, "_unlock");
                flavor ??= carrier.Keys.GetValueOrDefault("__flavor");
                god ??= carrier.PrayerGod;
                if (carrier.IconPath is not null)
                {
                    // Class portraits live on Class_ carriers; InventoryItem_ carries the god symbol.
                    if (carrier.BaseName.StartsWith("InventoryItem_Class_", StringComparison.Ordinal))
                    {
                        symbolPath ??= carrier.IconPath;
                    }
                    else
                    {
                        iconPath ??= carrier.IconPath;
                    }
                }
            }
            iconPath ??= symbolPath;

            List<MasteryEntry> masteries = [];
            if (category == "Weapon")
            {
                string? family = carriers.Select(c => FamilyOf(c.RelPath))
                    .FirstOrDefault(f => f is not null && masteriesByFamily.ContainsKey(f));
                if (family is not null)
                {
                    masteries = masteriesByFamily[family];
                }
            }

            string[] internalIds = [.. carriers.Select(c => c.BaseName).Distinct(StringComparer.OrdinalIgnoreCase)];
            // Stat weeds and a few pool items carry no _name key; fall back to the tag leaf.
            name ??= System.Text.RegularExpressions.Regex.Replace(identity.Leaf, "([a-z0-9])([A-Z])", "$1 $2");
            entriesOut.Add(new TagEntry(tag, category, progressionTag is not null, Clean(name), Clean(rawDesc),
                TrimRaw(rawDesc), Clean(unlock), Clean(flavor), god, IconKey(iconPath), IconKey(symbolPath),
                masteries, internalIds, iconPath, symbolPath));
        }

        // 5. Gods table + synthetic Prayer/KillsFor entries.
        var gods = new List<GodEntry>();
        foreach (var (key, fullName, hasStatue, hidden, themes) in GodsMeta)
        {
            string lower = key.ToLowerInvariant();
            string symbolFile = key == "Sael" ? "/Game/Textures/UI/Deities/saelicon" : $"/Game/Textures/UI/Deities/UI_HousesSymbol_{key}_01";
            gods.Add(new GodEntry(key, fullName,
                Clean(tableEntries.GetValueOrDefault($"{lower}_lore")),
                Clean(tableEntries.GetValueOrDefault($"pray_{lower}")),
                themes, IconKey(symbolFile), hasStatue, hidden, symbolFile));
        }

        foreach (var g in gods)
        {
            entriesOut.Add(new TagEntry($"Progression.Prayer.{g.Key}", "Prayer", true,
                $"Devotion · {Capitalize(g.FullName)}", g.Lore, null, null, null, g.Key,
                g.SymbolKey, null, [], [], g.SymbolPath, null));
            entriesOut.Add(new TagEntry($"Progression.KillsFor.{g.Key}", "KillsFor", true,
                $"Boss kills for {g.FullName}", g.Lore, null, null, null, g.Key,
                g.SymbolKey, null, [], [], g.SymbolPath, null));
        }

        // Content belonging to a hidden god goes with it. Blessings are chosen at their god's
        // statue, so a blessing whose statue cannot be reached cannot be obtained either;
        // listing it would imply otherwise. Derived from the Hidden flag rather than named
        // tag-by-tag, so hiding another god later needs no further edits here.
        var hiddenGods = gods.Where(g => g.Hidden).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (hiddenGods.Count > 0)
        {
            int removed = entriesOut.RemoveAll(e => e.God is { } god && hiddenGods.Contains(god));
            Console.WriteLine($"Dropped {removed} entries belonging to hidden gods: {string.Join(", ", hiddenGods)}");
        }

        // 6. Wiki-curated unlock hints (game-authored strings win).
        ApplyWikiHints(ref entriesOut);

        // 7. Map titles from the Levels table.
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

        // 8. Blessing lock rules (real in-game strings).
        var lockRules = new Dictionary<int, string>();
        if (tableEntries.GetValueOrDefault("blessing_locked_1bosskill") is { } rule1) { lockRules[1] = rule1; }
        if (tableEntries.GetValueOrDefault("blessing_locked_3bosskill") is { } rule3) { lockRules[3] = rule3; }

        // 9. Emit. The generated source goes into the Core project; the reports and the icon
        // manifest go to the output directory (default <repo>/artifacts, override with --out).
        var quests = QuestCollector.Run(contentRoot);

        string generatedPath = ExtractorPaths.GeneratedTagDatabase;
        File.WriteAllText(generatedPath, GenerateCSharp(entriesOut, mapTitles, gods, lockRules, quests));
        Console.WriteLine($"Wrote {generatedPath}");

        ExtractorPaths.EnsureOutputDirectory();

        string jsonPath = ExtractorPaths.TagDatabaseReport;
        File.WriteAllText(jsonPath, GenerateJson(entriesOut, mapTitles, gods));
        Console.WriteLine($"Wrote {jsonPath}");

        // Icon manifest for the --icons export step.
        var iconPaths = entriesOut.SelectMany(e => new[] { e.IconPath, e.SymbolPath })
            .Concat(gods.Select(g => g.SymbolPath))
            .Where(p => p is not null).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        string manifestPath = ExtractorPaths.IconManifest;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(iconPaths, JsonOptions));
        Console.WriteLine($"Wrote {manifestPath} ({iconPaths.Count} unique textures)");

        // 10. Summary.
        Console.WriteLine($"\nTags: {entriesOut.Count} | maps: {mapTitles.Count} | gods: {gods.Count}");
        foreach (var group in entriesOut.GroupBy(e => e.Category).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {group.Key,-10} {group.Count(),3} tags | named {group.Count(e => e.DisplayName is not null),3}" +
                $" | icons {group.Count(e => e.IconKey is not null),3} | hints {group.Count(e => e.UnlockHint is not null),3}");
        }
        return 0;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static Dictionary<string, List<MasteryEntry>> CollectMasteries(string contentRoot, HashSet<string> namespaces)
    {
        var result = new Dictionary<string, List<MasteryEntry>>(StringComparer.OrdinalIgnoreCase);
        string treesRoot = Path.Combine(contentRoot, "GameplayAbilitySystem", "Trees");
        foreach (string family in Directory.EnumerateDirectories(treesRoot).Select(Path.GetFileName).OfType<string>())
        {
            string upgrades = Path.Combine(treesRoot, family, "Upgrades");
            if (!Directory.Exists(upgrades))
            {
                continue;
            }
            var list = new List<MasteryEntry>();
            foreach (string uexp in Directory.EnumerateFiles(upgrades, "Upgrade_*.uexp", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var pairs = ReadKeyValues(File.ReadAllBytes(uexp), namespaces);
                string? name = FirstWithSuffix(pairs, "_name");
                string? desc = FirstWithSuffix(pairs, "_desc", "_description");
                if (name is not null && desc is not null)
                {
                    list.Add(new MasteryEntry(Clean(name)!, Clean(desc)!, TrimRaw(desc)!));
                }
            }
            if (list.Count > 0)
            {
                result[family] = list;
            }
        }
        return result;
    }

    private static string? FirstWithSuffix(Dictionary<string, string?> pairs, params string[] suffixes)
    {
        foreach (string suffix in suffixes)
        {
            foreach (var key in pairs.Keys.Where(k => k.EndsWith(suffix, StringComparison.Ordinal)).OrderBy(k => k.Length))
            {
                if (!string.IsNullOrWhiteSpace(pairs[key]))
                {
                    return pairs[key];
                }
            }
        }
        return null;
    }

    /// <summary>"GameplayAbilitySystem/Trees/Mace/Weapon_Warhammer/Tree_Warhammer.uasset" → "Mace".</summary>
    private static string? FamilyOf(string relPath)
    {
        string[] parts = relPath.Split('/');
        int i = Array.IndexOf(parts, "Trees");
        return i >= 0 && i + 1 < parts.Length ? parts[i + 1] : null;
    }

    private static void ApplyWikiHints(ref List<TagEntry> entries)
    {
        string hintsPath = Path.Combine(AppContext.BaseDirectory, "wiki_hints.json");
        if (!File.Exists(hintsPath))
        {
            hintsPath = Path.Combine(ExtractorPaths.RepoRoot, "tools", "SerpentsEyes.Extractor", "wiki_hints.json");
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(hintsPath));

        // Deduplicate weapons: untagged tree variants share their family's masteries
        // and duplicate a real weapon; only the wiki-confirmed starters survive.
        var canonicalUntagged = doc.RootElement.GetProperty("canonicalUntaggedWeapons")
            .EnumerateArray().Select(e => e.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int dropped = entries.RemoveAll(e =>
            e is { Category: "Weapon", HasProgression: false }
            && (e.DisplayName is null || !canonicalUntagged.Contains(e.DisplayName)));
        Console.WriteLine($"Dropped {dropped} duplicate/internal weapon variants");

        // Drop content whose game-authored description documents a removed mechanic. The game
        // still ships the old text, so there is nothing to extract that would be correct.
        if (doc.RootElement.TryGetProperty("hiddenTags", out var hidden))
        {
            var hiddenTags = hidden.EnumerateArray()
                .Select(e => e.GetProperty("tag").GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            int removed = entries.RemoveAll(e => hiddenTags.Contains(e.Tag));
            Console.WriteLine($"Dropped {removed} entries describing removed mechanics");
        }

        var byTag = new Dictionary<string, string>(StringComparer.Ordinal);
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hint in doc.RootElement.GetProperty("hints").EnumerateArray())
        {
            string text = hint.GetProperty("hint").GetString()!;
            if (hint.TryGetProperty("tag", out var tag))
            {
                byTag[tag.GetString()!] = text;
            }
            else if (hint.TryGetProperty("name", out var name))
            {
                byName[name.GetString()!] = text;
            }
        }

        int applied = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.UnlockHint is not null)
            {
                continue; // game-authored strings win
            }
            string? hint = byTag.GetValueOrDefault(e.Tag)
                ?? (e.DisplayName is not null ? byName.GetValueOrDefault(e.DisplayName) : null);
            if (hint is not null)
            {
                entries[i] = e with { UnlockHint = hint };
                applied++;
            }
        }
        Console.WriteLine($"Wiki hints applied: {applied}");
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
        var itemTags = new List<string>();
        string? prayerGod = null, iconPath = null;
        foreach (string s in UexpStrings.Scan(File.ReadAllBytes(uassetPath)))
        {
            if (DefinitionTag().IsMatch(s) && !tags.Contains(s))
            {
                tags.Add(s);
            }
            if (ItemIdentityTag().IsMatch(s) && !itemTags.Contains(s))
            {
                itemTags.Add(s);
            }
            var prayer = PrayerAffinity().Match(s);
            if (prayer.Success)
            {
                prayerGod = prayer.Groups[1].Value;
            }
            if (iconPath is null && UiTexturePath().IsMatch(s))
            {
                iconPath = s;
            }
        }

        var keys = new Dictionary<string, string?>(StringComparer.Ordinal);
        string uexpPath = Path.ChangeExtension(uassetPath, ".uexp");
        if (File.Exists(uexpPath))
        {
            keys = ReadKeyValues(File.ReadAllBytes(uexpPath), namespaces);
        }
        return new AssetInfo(baseName, relPath, tags, itemTags, keys, prayerGod, iconPath);
    }

    /// <summary>
    /// Extracts FText (namespace, key, inline value) triples from a .uexp string stream.
    /// The value is absent when the text resolves through a string table at runtime.
    /// </summary>
    private static Dictionary<string, string?> ReadKeyValues(byte[] uexpBytes, HashSet<string> namespaces)
    {
        var keys = new Dictionary<string, string?>(StringComparer.Ordinal);
        var ordered = UexpStrings.Scan(uexpBytes);
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
        return keys;
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
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " "); // stripped inline glyphs leave double spaces
        }
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>Keeps UE markup intact; only normalizes line endings and trims.</summary>
    private static string? TrimRaw(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    /// <summary>"/Game/Textures/UI/Weapons/WeaponCards_Cinder_02" → "WeaponCards_Cinder_02".</summary>
    private static string? IconKey(string? texturePath)
        => texturePath is null ? null : texturePath[(texturePath.LastIndexOf('/') + 1)..];

    private static string Capitalize(string s)
        => s.Length > 0 && char.IsLower(s[0]) ? char.ToUpperInvariant(s[0]) + s[1..] : s;

    private static string GenerateCSharp(List<TagEntry> entries, List<(string Key, string Title)> mapTitles,
        List<GodEntry> gods, Dictionary<int, string> lockRules, List<QuestDefinitionEntry> quests)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by tools/SerpentsEyes.Extractor from Serpent's Gaze game data.");
        sb.AppendLine("// Unlock hints partly curated from https://serpents-gaze.fandom.com/ (CC-BY-SA).");
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
              .Append(e.HasProgression ? "true" : "false").Append(", ")
              .Append(Lit(e.DisplayName)).Append(", ").Append(Lit(e.Description)).Append(", ")
              .Append(Lit(e.RawDescription)).Append(", ").Append(Lit(e.UnlockHint)).Append(", ")
              .Append(Lit(e.Flavor)).Append(", ").Append(Lit(e.God)).Append(", ")
              .Append(Lit(e.IconKey)).Append(", ").Append(Lit(e.SymbolKey)).Append(", [");
            sb.Append(string.Join(", ", e.Masteries.Select(m =>
                $"new WeaponMastery({Lit(m.Name)}, {Lit(m.Description)}, {Lit(m.RawDescription)})")));
            sb.Append("], [").Append(string.Join(", ", e.InternalIds.Select(id => Lit(id)))).AppendLine("]),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    private static readonly GodInfo[] GodEntries =");
        sb.AppendLine("    [");
        foreach (var g in gods)
        {
            sb.Append("        new(").Append(Lit(g.Key)).Append(", ").Append(Lit(g.FullName)).Append(", ")
              .Append(Lit(g.Lore)).Append(", ").Append(Lit(g.StatuePrompt)).Append(", ")
              .Append(Lit(g.Themes)).Append(", ").Append(Lit(g.SymbolKey)).Append(", ")
              .Append(g.HasStatue ? "true" : "false").Append(", ")
              .Append(g.Hidden ? "true" : "false").AppendLine("),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    private static readonly Dictionary<int, string> BlessingLockRuleEntries = new()");
        sb.AppendLine("    {");
        foreach (var (kills, rule) in lockRules.OrderBy(kv => kv.Key))
        {
            sb.Append("        [").Append(kills).Append("] = ").Append(Lit(rule)).AppendLine(",");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    private static readonly (string Key, string Title)[] MapTitleEntries =");
        sb.AppendLine("    [");
        foreach (var (key, title) in mapTitles)
        {
            sb.Append("        (").Append(Lit(key)).Append(", ").Append(Lit(title)).AppendLine("),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    private static readonly QuestDefinition[] QuestEntries =");
        sb.AppendLine("    [");
        foreach (var q in quests)
        {
            sb.Append("        new(").Append(Lit(q.OwnerKey)).Append(", [")
              .Append(string.Join(", ", q.Tags.Select(t => Lit(t)))).AppendLine("]),");
        }
        sb.AppendLine("    ];");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateJson(List<TagEntry> entries, List<(string Key, string Title)> mapTitles, List<GodEntry> gods)
        => JsonSerializer.Serialize(new
        {
            tags = entries,
            gods,
            maps = mapTitles.Select(m => new { m.Key, m.Title }),
        }, JsonOptions);

    private static string Lit(string? s)
        => s is null
            ? "null"
            : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
}
