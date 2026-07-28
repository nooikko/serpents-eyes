namespace SerpentsEyes.Extractor;

/// <summary>
/// Resolves the directories the extractor reads from and writes to.
/// </summary>
/// <remarks>
/// These used to be absolute paths on the original author's machine, which made the tool
/// unrunnable by anyone else and made <c>--icons</c> impossible. Everything is now derived from
/// the command line, an environment variable, or the repo the tool was built in.
/// </remarks>
internal static class ExtractorPaths
{
    /// <summary>Environment variable holding the unpacked game's Content directory.</summary>
    public const string ContentRootVariable = "SERPENTS_GAZE_CONTENT";

    private static string? _outputDirectory;

    /// <summary>Repo root, found by walking up from the build output to the solution file.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>Where reports and the icon manifest are written. Defaults to &lt;repo&gt;/artifacts.</summary>
    public static string OutputDirectory
    {
        get => _outputDirectory ??= Path.Combine(RepoRoot, "artifacts");
        set => _outputDirectory = Path.GetFullPath(value);
    }

    /// <summary>The generated half of TagDatabase, written into the Core project.</summary>
    public static string GeneratedTagDatabase =>
        Path.Combine(RepoRoot, "src", "SerpentsEyes.Core", "GameData", "TagDatabase.g.cs");

    /// <summary>The app's icon resource directory, the destination for <c>--icons</c>.</summary>
    public static string IconOutputDirectory =>
        Path.Combine(RepoRoot, "src", "SerpentsEyes.App", "Assets", "Icons");

    /// <summary>The texture list written by a normal run and consumed by <c>--icons</c>.</summary>
    public static string IconManifest => Path.Combine(OutputDirectory, "icon_manifest.json");

    public static string TagDatabaseReport => Path.Combine(OutputDirectory, "tag_database.json");

    public static void EnsureOutputDirectory() => Directory.CreateDirectory(OutputDirectory);

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SerpentsEyes.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new InvalidOperationException(
            "Could not locate the repo root (no SerpentsEyes.slnx above the build output). " +
            "Run the extractor from a source checkout.");
    }
}
