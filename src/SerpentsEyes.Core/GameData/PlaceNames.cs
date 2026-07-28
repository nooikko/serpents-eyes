namespace SerpentsEyes.Core.GameData;

/// <summary>
/// Resolves Shortcut and Location tags to the level they belong to.
/// </summary>
/// <remarks>
/// Shortcut tags name a place in the game's internal vocabulary — "Attaresh.Library.0",
/// "Majin.Bridge.2" — which prettifies into "Attaresh · Library · 0". The level string table
/// already gives those places real names ("The Grand Library", "The Great Divide"), so the
/// prettified tag is strictly worse than what the game itself calls them.
///
/// The tags do not line up with the level keys exactly: several shortcuts sit in one level and
/// carry a trailing index, and the plaza is "CityPlaza" in shortcut tags against "plaza" in
/// level keys. Both differences are handled here rather than by editing the extracted data,
/// which should keep saying what the game says.
/// </remarks>
public static class PlaceNames
{
    /// <summary>
    /// Shortcut-tag spellings that differ from the level key for the same place.
    /// </summary>
    private static readonly (string From, string To)[] Aliases =
    [
        ("cityplaza", "plaza"),
    ];

    /// <summary>
    /// The level's in-game name for a Shortcut or Location tag's name segment, or null when it
    /// cannot be resolved. Pass <see cref="TagRecord.Name"/>, e.g. "Attaresh.Library.0".
    /// </summary>
    public static string? LevelTitle(string tagName)
    {
        ArgumentNullException.ThrowIfNull(tagName);

        string key = Normalize(tagName);
        if (key.Length == 0)
        {
            return null;
        }

        string? title = TagDatabase.MapTitle(key);
        if (title is not null)
        {
            return title;
        }

        // Several shortcuts can share one level and are distinguished by a trailing index.
        string trimmed = key.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (trimmed.Length > 0 && trimmed.Length != key.Length)
        {
            title = TagDatabase.MapTitle(trimmed);
            if (title is not null)
            {
                return title;
            }
            key = trimmed;
        }

        foreach ((string from, string to) in Aliases)
        {
            if (key.Contains(from, StringComparison.Ordinal))
            {
                title = TagDatabase.MapTitle(key.Replace(from, to, StringComparison.Ordinal));
                if (title is not null)
                {
                    return title;
                }
            }
        }

        return null;
    }

    /// <summary>Strips separators and whitespace so tag spellings and level keys can be compared.</summary>
    private static string Normalize(string tagName)
    {
        Span<char> buffer = stackalloc char[tagName.Length];
        int length = 0;
        foreach (char c in tagName)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[length++] = char.ToLowerInvariant(c);
            }
        }
        return new string(buffer[..length]);
    }
}
