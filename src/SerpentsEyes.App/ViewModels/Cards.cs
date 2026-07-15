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
