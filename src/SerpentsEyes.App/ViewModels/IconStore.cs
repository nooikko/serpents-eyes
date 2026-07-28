using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SerpentsEyes.App.ViewModels;

/// <summary>Loads and caches the game icons embedded as Avalonia resources.</summary>
public static class IconStore
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The avares authority is the assembly name, so it is read from the assembly rather than
    /// written out. Hardcoding it means renaming the assembly silently stops every icon from
    /// resolving: nothing throws, the lookups just miss and every card falls back to the
    /// placeholder.
    /// </summary>
    private static readonly string ResourceAuthority =
        typeof(IconStore).Assembly.GetName().Name ?? "SerpentsEyes";

    /// <summary>Looks up an icon by key, or returns null when there is no such asset.</summary>
    public static Bitmap? Get(string? iconKey)
    {
        if (string.IsNullOrEmpty(iconKey))
        {
            return null;
        }
        if (Cache.TryGetValue(iconKey, out var cached))
        {
            return cached;
        }

        // WebP: the source textures are painted card art, where lossy compression at display
        // size is an order of magnitude smaller than PNG for no visible difference. Avalonia
        // decodes via Skia, which handles WebP natively.
        var uri = new Uri($"avares://{ResourceAuthority}/Assets/Icons/{iconKey}.webp");
        Bitmap? bitmap = null;
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            bitmap = new Bitmap(stream);
        }
        Cache[iconKey] = bitmap;
        return bitmap;
    }
}
