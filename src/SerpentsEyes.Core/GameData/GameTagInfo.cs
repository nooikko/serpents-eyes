namespace SerpentsEyes.Core.GameData;

/// <summary>
/// Everything the game data knows about one progression tag: player-facing name,
/// description, unlock hint, and the internal asset ids that reference it.
/// Extracted from the game files by tools/SerpentsEyes.Extractor.
/// </summary>
public sealed record GameTagInfo(
    string Tag,
    string Category,
    string? DisplayName,
    string? Description,
    string? UnlockHint,
    string? Flavor,
    string? God,
    string[] InternalIds);
