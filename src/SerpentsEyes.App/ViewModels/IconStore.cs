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

        var uri = new Uri($"avares://SerpentsEyes.App/Assets/Icons/{iconKey}.png");
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
