using SerpentsEyes.Extractor;

const string DefaultContentRoot =
    @"C:\Users\elija\Documents\serpents_gaze_workbench\extracted\legacy\NinjaGarden\Content";

string contentRoot = args.FirstOrDefault(a => !a.StartsWith("--")) ?? DefaultContentRoot;
if (!Directory.Exists(contentRoot))
{
    Console.Error.WriteLine($"Content root not found: {contentRoot}");
    return 1;
}

if (args.Contains("--probe"))
{
    Probe.Run(contentRoot);
    return 0;
}

return Extractor.Run(contentRoot);
