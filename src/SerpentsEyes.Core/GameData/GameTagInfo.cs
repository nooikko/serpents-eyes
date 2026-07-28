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
/// <param name="Key">Tag key, e.g. "Matriarch".</param>
/// <param name="FullName">In-game name, e.g. "the Weeping Matriarch".</param>
/// <param name="Lore">The game's own lore string for the statue.</param>
/// <param name="StatuePrompt">The prompt shown when devoting at the statue.</param>
/// <param name="Themes">Community-wiki summary of what the god's blessings do.</param>
/// <param name="SymbolKey">Icon key for the house symbol.</param>
/// <param name="HasStatue">Whether the game defines a statue for it at all.</param>
/// <param name="Hidden">
/// Legacy content that is present in the files but not reachable in the current game.
/// </param>
public sealed record GodInfo(
    string Key,
    string FullName,
    string? Lore,
    string? StatuePrompt,
    string? Themes,
    string? SymbolKey,
    bool HasStatue,
    bool Hidden)
{
    /// <summary>
    /// True for the Divinities a player can actually devote to, which is what the app should
    /// list.
    /// </summary>
    /// <remarks>
    /// Two separate exclusions. Sael has no statue, no lore and no blessings — the data alone
    /// rules it out. The Keeper of Eyes does have all of those in the files but is reworked
    /// legacy content that cannot be reached in the current game, which nothing in the data
    /// reveals; that one is recorded player knowledge.
    /// </remarks>
    public bool IsDivinity => HasStatue && !Hidden;
}

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
