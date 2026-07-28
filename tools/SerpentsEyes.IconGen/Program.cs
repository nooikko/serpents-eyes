using SerpentsEyes.IconGen;
using SkiaSharp;

// Regenerates the app icon. The committed .ico and .png are the build inputs; this tool is
// how you change them.
//
//   dotnet run --project tools/SerpentsEyes.IconGen
//   dotnet run --project tools/SerpentsEyes.IconGen -- --preview <dir>
//
// --preview additionally writes every size as its own PNG plus a magnified contact sheet,
// which is the only practical way to see whether an edit survives 16x16.

// Sizes the Windows shell asks for across the DPI settings it ships with. 128 is left out:
// nothing requests it, and the shell interpolates it from 256 without anyone noticing.
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];

string repoRoot = FindRepoRoot();
string assetDirectory = Path.Combine(repoRoot, "src", "SerpentsEyes.App", "Assets");
string icoPath = Path.Combine(assetDirectory, "AppIcon.ico");
string pngPath = Path.Combine(assetDirectory, "AppIcon.png");

var bitmaps = sizes.Select(EyeMark.Render).ToList();
try
{
    IcoWriter.Write(icoPath, bitmaps);
    Console.WriteLine($"{icoPath}  ({string.Join(", ", sizes)} px)");

    // The window icon everywhere except Windows, which uses the .ico above for its frames.
    using (SKData data = bitmaps[^1].Encode(SKEncodedImageFormat.Png, 100))
    using (FileStream file = File.Create(pngPath))
    {
        data.SaveTo(file);
    }
    Console.WriteLine(pngPath);

    if (args is [var flag, var previewDirectory] && flag is "--preview")
    {
        WritePreview(previewDirectory, sizes, bitmaps);
    }
}
finally
{
    foreach (SKBitmap bitmap in bitmaps)
    {
        bitmap.Dispose();
    }
}

return 0;

static void WritePreview(string directory, int[] sizes, List<SKBitmap> bitmaps)
{
    Directory.CreateDirectory(directory);
    for (int i = 0; i < sizes.Length; i++)
    {
        using SKData data = bitmaps[i].Encode(SKEncodedImageFormat.Png, 100);
        using FileStream file = File.Create(Path.Combine(directory, $"icon-{sizes[i]}.png"));
        data.SaveTo(file);
    }

    // Contact sheet: every size at 1:1 on the top row, and again at 4x underneath so the
    // small ones can actually be judged.
    const int Pad = 12;
    const int Zoom = 4;
    int[] small = [.. sizes.Where(s => s <= 64)];
    int sheetWidth = Pad + small.Sum(s => (s * Zoom) + Pad);
    int sheetHeight = (Pad * 3) + 64 + (64 * Zoom);

    using var sheet = new SKBitmap(sheetWidth, sheetHeight);
    using (var canvas = new SKCanvas(sheet))
    {
        canvas.Clear(new SKColor(0x2B, 0x2B, 0x2B));
        var nearest = new SKSamplingOptions(SKFilterMode.Nearest);

        int x = Pad;
        for (int i = 0; i < small.Length; i++)
        {
            int size = small[i];
            canvas.DrawBitmap(bitmaps[i], x, Pad + ((64 - size) / 2), nearest);
            canvas.DrawBitmap(
                bitmaps[i],
                new SKRect(x, (Pad * 2) + 64, x + (size * Zoom), (Pad * 2) + 64 + (size * Zoom)),
                nearest);
            x += (size * Zoom) + Pad;
        }
    }

    string sheetPath = Path.Combine(directory, "contact-sheet.png");
    using (SKData data = sheet.Encode(SKEncodedImageFormat.Png, 100))
    using (FileStream file = File.Create(sheetPath))
    {
        data.SaveTo(file);
    }
    Console.WriteLine(sheetPath);
}

static string FindRepoRoot()
{
    string? directory = AppContext.BaseDirectory;
    while (directory is not null && !File.Exists(Path.Combine(directory, "SerpentsEyes.slnx")))
    {
        directory = Path.GetDirectoryName(directory);
    }
    return directory ?? throw new InvalidOperationException(
        "Could not locate the repo root (no SerpentsEyes.slnx above the build output).");
}
