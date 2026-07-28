using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SerpentsEyes.App.ViewModels;

/// <summary>Loads and caches the game icons embedded as Avalonia resources.</summary>
public static class IconStore
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

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
        var uri = new Uri($"avares://SerpentsEyes.App/Assets/Icons/{iconKey}.webp");
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
