namespace SerpentsEyes.Core;

/// <summary>Locates Serpent's Gaze save files on the local machine.</summary>
public static class SaveLocator
{
    /// <summary>Path under the platform's local-app-data root that the game writes to.</summary>
    private static readonly string[] SaveSubPath = ["SerpentsGaze", "Saved", "SaveGames", "Steam"];

    /// <summary>
    /// The save directory on the current platform: on Windows,
    /// <c>%LOCALAPPDATA%\SerpentsGaze\Saved\SaveGames\Steam</c>.
    /// </summary>
    /// <remarks>
    /// This is the path to show a user who has no saves. Use <see cref="FindProfiles()"/> to
    /// actually locate files — on Linux and macOS the game runs under Proton and writes inside a
    /// Wine prefix, which this path does not describe.
    /// </remarks>
    public static string DefaultSaveDirectory => Path.Combine(
        [Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), .. SaveSubPath]);

    /// <summary>
    /// Every directory worth checking for saves on this machine, most likely first.
    /// </summary>
    /// <remarks>
    /// On Windows this is just the one path. Elsewhere the game is a Windows build running under
    /// Proton, so its <c>%LOCALAPPDATA%</c> lives inside a per-title Wine prefix under whichever
    /// Steam library the game was installed to.
    /// </remarks>
    public static IReadOnlyList<string> CandidateSaveDirectories()
    {
        var candidates = new List<string> { DefaultSaveDirectory };

        if (!OperatingSystem.IsWindows())
        {
            foreach (string library in SteamLibraryRoots())
            {
                string compatData = Path.Combine(library, "steamapps", "compatdata");
                foreach (string prefix in SafeEnumerateDirectories(compatData))
                {
                    candidates.Add(Path.Combine(
                        [prefix, "pfx", "drive_c", "users", "steamuser", "AppData", "Local", .. SaveSubPath]));
                }
            }
        }

        return [.. candidates.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Enumerates .sav files in every candidate directory, primary profiles first
    /// (profile_0, profile_1, … then autosaves and backups). Never throws.
    /// </summary>
    public static IReadOnlyList<string> FindProfiles()
    {
        var found = new List<string>();
        foreach (string directory in CandidateSaveDirectories())
        {
            found.AddRange(FindProfiles(directory));
        }
        return found;
    }

    /// <summary>
    /// Enumerates .sav files in one directory, primary profiles first. Returns an empty list when
    /// the directory does not exist or cannot be read.
    /// </summary>
    public static IReadOnlyList<string> FindProfiles(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        List<string> files;
        try
        {
            if (!Directory.Exists(directory))
            {
                return [];
            }
            files = [.. Directory.EnumerateFiles(directory, "*.sav")];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Unreadable, a broken junction, a disconnected network path, or a path the
            // platform rejects. A viewer should show no saves, not fail to start.
            return [];
        }

        return [.. files
            .OrderBy(IsPrimaryProfile, DescendingBool)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>True for "profile_0.sav" but not "profile_0_autosave.sav" or "profile_0_bak1.sav".</summary>
    private static bool IsPrimaryProfile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith("profile_", StringComparison.OrdinalIgnoreCase)
            && name.LastIndexOf('_') == "profile".Length;
    }

    private static readonly IComparer<bool> DescendingBool =
        Comparer<bool>.Create(static (a, b) => b.CompareTo(a));

    /// <summary>Steam library roots, read from libraryfolders.vdf where it exists.</summary>
    private static IEnumerable<string> SteamLibraryRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots =
        [
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, "Library", "Application Support", "Steam"),
        ];

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            yield return root;

            // libraryfolders.vdf lists additional drives as: "path"  "/mnt/games/SteamLibrary"
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            string[] lines;
            try
            {
                lines = File.Exists(vdf) ? File.ReadAllLines(vdf) : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.Ordinal))
                {
                    continue;
                }
                int open = trimmed.IndexOf('"', "\"path\"".Length);
                int close = open >= 0 ? trimmed.IndexOf('"', open + 1) : -1;
                if (close > open)
                {
                    yield return trimmed[(open + 1)..close];
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateDirectories(path) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
