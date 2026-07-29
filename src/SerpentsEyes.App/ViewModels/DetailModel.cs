using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using SerpentsEyes.Core;
using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.App.ViewModels;

/// <summary>Everything the right-hand detail pane shows for the current selection.</summary>
public sealed partial class DetailModel
{
    // Common
    public required string Name { get; init; }
    public Bitmap? Icon { get; init; }
    public string? CategoryLabel { get; init; }
    public string? StatusText { get; init; }
    public string? CounterText { get; init; }
    public string? UnlockHint { get; init; }
    public string? RawDescription { get; init; }
    public string? Flavor { get; init; }
    public IReadOnlyList<ScalingRow> ScalingRows { get; init; } = [];
    public IReadOnlyList<WeaponMastery> Masteries { get; init; } = [];

    // God-specific
    public bool IsGod { get; init; }
    public string? Lore { get; init; }
    public string? StatuePrompt { get; init; }
    public string? Themes { get; init; }
    public IReadOnlyList<LockRuleRow> LockRules { get; init; } = [];
    public IReadOnlyList<GodBlessingRow> Blessings { get; init; } = [];

    public bool HasMasteries => Masteries.Count > 0;
    public bool HasScaling => ScalingRows.Count > 0;
    public bool HasBlessings => Blessings.Count > 0;

    [GeneratedRegex(@"\{p_([a-z]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex AttrPlaceholder();

    private static readonly Dictionary<string, string> AttrNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fai"] = "Faith", ["str"] = "Strength", ["dex"] = "Dexterity", ["int"] = "Intelligence",
        ["luk"] = "Luck", ["sta"] = "Stamina", ["tou"] = "Toughness", ["swi"] = "Swiftness", ["fin"] = "Finesse",
    };

    public static DetailModel ForItem(ItemCard card)
    {
        var info = card.Info;

        // Wording follows what the category's counter actually means. An Aspect is unlocked once
        // and stays unlocked; a Blessing counter is how many times it has been taken, so calling
        // it "unlocked" describes something the game does not model.
        string status = card.State switch
        {
            CardState.Locked => TagSemantics.MissingVerb(card.CategoryKey),
            CardState.AlwaysAvailable => "Always available",
            _ when card.Value == 0 => "Found, not yet completed",
            _ => TagSemantics.HaveVerb(card.CategoryKey),
        };
        string? counter = card.Value is { } v && !card.IsLocked
            ? TagSemantics.CounterText(card.CategoryKey, card.Tag, v)
            : null;

        // The god tag on an item is thematic grouping that drives the card art and nothing else —
        // no shop, altar or loot logic reads it, and Sael, not a divinity at all, is one of the
        // values it takes. Naming the god in the detail pane ("Blessed by …") read as a game
        // mechanic to anyone who saw it, so it is deliberately not surfaced here. The god page
        // still lists its own blessings, which is where that relationship does mean something.

        return new DetailModel
        {
            Name = card.Name,
            Icon = card.Icon,
            CategoryLabel = Display.CategorySingular(card.CategoryKey),
            StatusText = status,
            CounterText = counter,
            // Shown whether or not it is already unlocked: "how did I get this" is worth
            // answering after the fact, and hiding it made the app less useful the more you
            // had unlocked.
            UnlockHint = info?.UnlockHint,
            RawDescription = info?.RawDescription ?? info?.Description,
            Flavor = info?.Flavor,
            ScalingRows = BuildScalingRows(info?.RawDescription),
            Masteries = info?.Masteries ?? [],
        };
    }

    public static DetailModel ForGod(GodCard card, SaveProfile? profile)
    {
        var ownedTags = profile?.Records.ToDictionary(r => r.FullTag, r => r.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);

        var lockRules = TagDatabase.BlessingLockRules
            .OrderBy(kv => kv.Key)
            .Select(kv => new LockRuleRow(kv.Value, card.BossKills >= kv.Key))
            .ToList();

        var blessings = TagDatabase.All
            .Where(t => t.Category is "Blessing" && string.Equals(t.God, card.God.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.DisplayName, StringComparer.Ordinal)
            .Select(t => new GodBlessingRow(t.DisplayName ?? t.Tag, ownedTags.ContainsKey(t.Tag)))
            .ToList();

        return new DetailModel
        {
            Name = card.Name,
            Icon = card.Symbol,
            IsGod = true,
            StatusText = card.IsTouched ? $"Prayed ×{card.TimesPrayed} · {card.BossKills} boss kills while blessed" : "Never prayed to",
            Lore = card.God.Lore,
            StatuePrompt = card.God.StatuePrompt,
            Themes = card.God.Themes,
            LockRules = card.God.HasStatue ? lockRules : [],
            Blessings = blessings,
        };
    }

    private static List<ScalingRow> BuildScalingRows(string? rawDescription)
    {
        if (rawDescription is null)
        {
            return [];
        }

        var rows = new List<ScalingRow>();
        foreach (var segment in UeRichText.Parse(rawDescription).Where(s => s.Kind == RichSegmentKind.Math))
        {
            if (!segment.Text.Contains('{'))
            {
                continue; // constant expression — nothing scales
            }
            string pretty = PrettyFormula(segment.Text);
            if (ScalingMath.IsLevelOnly(segment.Text))
            {
                var values = Enumerable.Range(1, 5)
                    .Select(level => ScalingMath.TryEvaluate(segment.Text, level, out double r) ? Trim(r) : "?");
                rows.Add(new ScalingRow(pretty, $"Lv 1–5:  {string.Join("  ·  ", values)}", null));
            }
            else
            {
                var attrs = AttrPlaceholder().Matches(segment.Text)
                    .Select(m => AttrNames.GetValueOrDefault(m.Groups[1].Value, m.Groups[1].Value))
                    .Distinct().ToList();
                rows.Add(new ScalingRow(pretty, null, $"Scales with {string.Join(", ", attrs)}"));
            }
        }
        return rows;
    }

    /// <summary>"3+2*{l}" → "3 + (2 × Lv)"; "{p_fai}*(2+{l}*1)" → "Faith × (2 + (Lv × 1))".</summary>
    internal static string PrettyFormula(string expression)
    {
        string? formatted = ScalingMath.TryFormat(expression, ResolvePlaceholder);
        if (formatted is not null)
        {
            return formatted;
        }

        // Unparseable formula: fall back to naive symbol substitution.
        string pretty = AttrPlaceholder().Replace(expression, m => AttrNames.GetValueOrDefault(m.Groups[1].Value, m.Groups[1].Value));
        pretty = pretty.Replace("{l}", "Lv").Replace("{L}", "Lv");
        pretty = pretty.Replace("*", " × ").Replace("+", " + ").Replace("/", " ÷ ");
        while (pretty.Contains("  "))
        {
            pretty = pretty.Replace("  ", " ");
        }
        return pretty.Trim();
    }

    private static string ResolvePlaceholder(string name)
    {
        if (name.Equals("l", StringComparison.OrdinalIgnoreCase))
        {
            return "Lv";
        }
        return name.StartsWith("p_", StringComparison.OrdinalIgnoreCase)
            ? AttrNames.GetValueOrDefault(name[2..], name[2..])
            : name;
    }

    private static string Trim(double value)
        => value == Math.Floor(value) ? ((long)value).ToString() : value.ToString("0.##");
}
