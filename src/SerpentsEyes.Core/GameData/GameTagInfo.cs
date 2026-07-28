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
    bool HasProgression,
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

/// <summary>
/// A questline as the game defines it, read from the quest assets under Content/Quests.
/// </summary>
/// <remarks>
/// The tag list is the complete set of stages, encounters and collectibles the questline knows
/// about, which is what makes a true "3 of 6" possible: a save only records the stages the
/// player has already reached.
/// </remarks>
/// <param name="OwnerKey">Owner segment used in the tags, e.g. "LordMalvo".</param>
/// <param name="Tags">Every tag the questline defines.</param>
public sealed record QuestDefinition(string OwnerKey, string[] Tags);
