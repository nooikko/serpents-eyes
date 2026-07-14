namespace SerpentsEyes.Core;

/// <summary>Locates Serpent's Gaze save files on the local machine.</summary>
public static class SaveLocator
{
    /// <summary>Default Steam save directory: %LOCALAPPDATA%\SerpentsGaze\Saved\SaveGames\Steam.</summary>
    public static string DefaultSaveDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SerpentsGaze", "Saved", "SaveGames", "Steam");

    /// <summary>
    /// Enumerates .sav files in the default save directory, primary profiles first.
    /// Returns an empty list when the directory does not exist.
    /// </summary>
    public static IReadOnlyList<string> FindProfiles()
    {
        string dir = DefaultSaveDirectory;
        if (!Directory.Exists(dir))
        {
            return [];
        }
        return [.. Directory.EnumerateFiles(dir, "*.sav").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];
    }
}
