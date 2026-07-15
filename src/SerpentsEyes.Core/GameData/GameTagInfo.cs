namespace SerpentsEyes.Core.GameData;

/// <summary>One weapon mastery (upgrade) as shown at the Blacksmith Anvil.</summary>
public sealed record WeaponMastery(string Name, string Description, string RawDescription);

/// <summary>
/// Everything the game data knows about one progression tag: player-facing name,
/// description (cleaned and raw with UE markup), unlock hint, icon keys, and the
/// internal asset ids that reference it.
/// Extracted from the game files by tools/SerpentsEyes.Extractor.
/// </summary>
public sealed record GameTagInfo(
    string Tag,
    string Category,
    string? DisplayName,
    string? Description,
    string? RawDescription,
    string? UnlockHint,
    string? Flavor,
    string? God,
    string? IconKey,
    string? SymbolKey,
    WeaponMastery[] Masteries,
    string[] InternalIds);

/// <summary>
/// One of the seven gods ("Divinities"). Lore and prompts are the game's own strings;
/// Themes comes from the community wiki (CC-BY-SA).
/// </summary>
public sealed record GodInfo(
    string Key,
    string FullName,
    string? Lore,
    string? StatuePrompt,
    string? Themes,
    string? SymbolKey,
    bool HasStatue);
