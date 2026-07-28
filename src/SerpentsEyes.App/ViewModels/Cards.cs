using Avalonia.Media.Imaging;
using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.App.ViewModels;

/// <summary>Collection state of a card: locked, unlocked, or never lockable at all.</summary>
public enum CardState
{
    Unlocked,
    Locked,
    AlwaysAvailable,
}

/// <summary>One tile in the content grid.</summary>
public sealed record ItemCard(
    string Tag,
    string Name,
    string CategoryKey,
    Bitmap? Icon,
    CardState State,
    int? Value,
    GameTagInfo? Info)
{
    public bool IsLocked => State == CardState.Locked;
    public double CardOpacity => IsLocked ? 0.38 : 1.0;
    public bool HasIcon => Icon is not null;
}

/// <summary>One god ("Divinity") tile.</summary>
public sealed record GodCard(
    GodInfo God,
    Bitmap? Symbol,
    int TimesPrayed,
    int BossKills)
{
    public string Name => Capitalize(God.FullName);
    public bool IsTouched => TimesPrayed > 0 || BossKills > 0;
    public double CardOpacity => IsTouched ? 1.0 : 0.38;
    public string StatsLine => $"Prayed ×{TimesPrayed} · {BossKills} boss kills";

    internal static string Capitalize(string s)
        => s.Length > 0 && char.IsLower(s[0]) ? char.ToUpperInvariant(s[0]) + s[1..] : s;
}

/// <summary>One step within a questline panel.</summary>
public sealed record QuestStepRow(QuestStep Step)
{
    public string Label => Step.Label;

    public bool IsDone => Step.Value > 0;

    /// <summary>Filled for a finished step, hollow for one still outstanding.</summary>
    public string Marker => IsDone ? "●" : "○";

    public double RowOpacity => IsDone ? 1.0 : 0.45;

    /// <summary>True for anything that is not a numbered stage, which is drawn more quietly.</summary>
    public bool IsAside => Step.Kind is not QuestStepKind.Part;

    /// <summary>
    /// Trailing detail. Stages are binary so their counter says nothing, but collectibles and
    /// run tallies are counts worth seeing.
    /// </summary>
    public string? Detail => Step.Kind switch
    {
        QuestStepKind.Item when Step.Value > 0 => $"×{Step.Value}",
        QuestStepKind.Other when Step.Value > 0 => $"{Step.Value}",
        _ => null,
    };

    public bool HasDetail => Detail is not null;

    public string KindLabel => Step.Kind switch
    {
        QuestStepKind.Event => "Encounter",
        QuestStepKind.Optional => "Optional",
        QuestStepKind.Item => "Item",
        QuestStepKind.Other => "Tally",
        _ => string.Empty,
    };

    public bool HasKindLabel => KindLabel.Length > 0;
}

/// <summary>One NPC's questline, as a panel of ordered steps.</summary>
public sealed record QuestLineCard(QuestLine Line)
{
    public string OwnerName => Line.OwnerName;

    public IReadOnlyList<QuestStepRow> Rows => [.. Line.Steps.Select(s => new QuestStepRow(s))];

    public bool IsComplete => Line.IsComplete;

    public bool IsUnstarted => Line.CompletedParts == 0;

    /// <summary>"Complete", "Not started", or "3 of 6".</summary>
    public string Progress => Line switch
    {
        { IsComplete: true } => "Complete",
        { CompletedParts: 0 } => "Not started",
        var l => $"{l.CompletedParts} of {l.TotalParts}",
    };

    public double ProgressMax => Math.Max(1, Line.TotalParts);

    public double ProgressValue => Line.CompletedParts;

    public double CardOpacity => IsUnstarted ? 0.55 : 1.0;
}

/// <summary>One row in the computed scaling table of the detail pane.</summary>
public sealed record ScalingRow(string Formula, string? LevelValues, string? Note);

/// <summary>A blessing listed on a god's detail pane.</summary>
public sealed record GodBlessingRow(string Name, bool Owned)
{
    public string Dot => Owned ? "●" : "○";
}

/// <summary>A lock-rule row on the god detail pane ("Kill 1 boss … ✓").</summary>
public sealed record LockRuleRow(string Rule, bool Met)
{
    public string Mark => Met ? "✓" : "·";
}
