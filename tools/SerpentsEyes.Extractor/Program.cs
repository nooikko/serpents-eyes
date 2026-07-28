using SerpentsEyes.Extractor;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        Regenerates the game-data database from an unpacked copy of Serpent's Gaze.

        Usage:
          extractor <content-root> [--out <dir>]    regenerate TagDatabase.g.cs
          extractor <content-root> --icons          export PNG icons into the app
          extractor <content-root> --probe          dump raw strings from known assets

        <content-root> is the Content directory of an unpacked game, e.g.
          .../extracted/NinjaGarden/Content
        It may also be supplied via the SERPENTS_GAZE_CONTENT environment variable.

        The game ships its assets in UE5 IoStore containers (.utoc/.ucas), so they must be
        unpacked first with a third-party tool such as retoc. See the README.

        --out defaults to <repo>/artifacts. --icons reads the manifest written there by a
        normal run, so run without --icons first.
        """);
    return 0;
}

string? contentRoot =
    args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
    ?? Environment.GetEnvironmentVariable(ExtractorPaths.ContentRootVariable);

if (string.IsNullOrWhiteSpace(contentRoot))
{
    Console.Error.WriteLine(
        $"No content root given. Pass it as the first argument or set {ExtractorPaths.ContentRootVariable}. " +
        "Run with --help for details.");
    return 1;
}

if (!Directory.Exists(contentRoot))
{
    Console.Error.WriteLine($"Content root not found: {contentRoot}");
    return 1;
}

int outIndex = Array.IndexOf(args, "--out");
if (outIndex >= 0)
{
    if (outIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("--out requires a directory argument.");
        return 1;
    }
    ExtractorPaths.OutputDirectory = args[outIndex + 1];
}

if (args.Contains("--probe"))
{
    Probe.Run(contentRoot);
    return 0;
}

if (args.Contains("--icons"))
{
    return IconExporter.Run(contentRoot);
}

return Extractor.Run(contentRoot);
