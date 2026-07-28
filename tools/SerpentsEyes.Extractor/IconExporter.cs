using System.Buffers.Binary;
using System.Text.Json;
using SkiaSharp;

namespace SerpentsEyes.Extractor;

/// <summary>
/// Exports the UI textures referenced by the tag database to PNG files that the app
/// embeds as Avalonia resources. Decodes UE cooked texture payloads directly (the
/// game's packages use unversioned properties, so CUE4Parse-style parsing would need
/// a .usmap we don't have — but texture mips are self-describing enough to find).
///
/// Cooked FTexture2DMipMap layout: [uint32 bulkFlags][int32 elementCount]
/// [int32 sizeOnDisk][int64 offsetInFile][pixel bytes if inline][int32 sizeX][int32 sizeY].
/// For inline (PF_B8G8R8A8) mips the pixels sit between the header and the dimensions;
/// for DXT the pixels live in the sibling .ubulk at the stored offset.
/// </summary>
internal static class IconExporter
{
    /// <summary>
    /// Longest edge of an exported icon, in pixels.
    /// </summary>
    /// <remarks>
    /// The app draws these at 118px in the card grid and 170px in the detail pane, so 384
    /// leaves better than 2x headroom for high-DPI displays. The source textures are up to
    /// 671x1202, which is roughly 30x more pixels than anything on screen ever needs.
    /// </remarks>
    private const int MaxDimension = 384;

    /// <summary>
    /// WebP quality. These are painted card illustrations, not screenshots or line art, so
    /// lossy compression at this level is visually indistinguishable at the sizes drawn while
    /// being roughly an order of magnitude smaller than PNG.
    /// </summary>
    private const int WebpQuality = 90;

    public const string Extension = ".webp";

    public static int Run(string contentRoot)
    {
        string manifestPath = ExtractorPaths.IconManifest;
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine(
                $"Icon manifest not found: {manifestPath}{Environment.NewLine}" +
                "Run the extractor without --icons first; a normal run writes the manifest.");
            return 1;
        }

        var manifest = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(manifestPath))!;
        Console.WriteLine($"Icon manifest: {manifest.Count} textures");

        string outDir = ExtractorPaths.IconOutputDirectory;
        Directory.CreateDirectory(outDir);

        int ok = 0, failed = 0;
        foreach (string gamePath in manifest)
        {
            string key = gamePath[(gamePath.LastIndexOf('/') + 1)..];
            try
            {
                string? basePath = LocateAsset(contentRoot, gamePath);
                if (basePath is null)
                {
                    Console.WriteLine($"  MISSING {gamePath}");
                    failed++;
                    continue;
                }

                using var bitmap = DecodeTexture(basePath);
                if (bitmap is null)
                {
                    Console.WriteLine($"  UNDECODABLE {gamePath}");
                    failed++;
                    continue;
                }

                using var scaled = Downscale(bitmap);
                using var image = SKImage.FromBitmap(scaled);
                using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
                using var stream = File.Create(Path.Combine(outDir, key + Extension));
                data.SaveTo(stream);
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED {gamePath}: {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }
        Console.WriteLine($"Exported {ok} {Extension} icons to {outDir} ({failed} failed)");
        return ok > 0 ? 0 : 1;
    }

    /// <summary>
    /// Scales a decoded texture down so its longest edge is at most <see cref="MaxDimension"/>,
    /// preserving aspect ratio. Returns the original when it is already small enough.
    /// </summary>
    private static SKBitmap Downscale(SKBitmap source)
    {
        int longest = Math.Max(source.Width, source.Height);
        if (longest <= MaxDimension)
        {
            return source;
        }

        double scale = (double)MaxDimension / longest;
        var size = new SKSizeI(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)));

        // The caller owns `source` and disposes it, so never dispose it here.
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        return source.Resize(size, sampling) ?? source;
    }

    /// <summary>Maps "/Game/Textures/UI/X" to the on-disk .uasset path, trying a "_256" suffix variant.</summary>
    private static string? LocateAsset(string contentRoot, string gamePath)
    {
        string rel = gamePath.Replace("/Game/", "").Replace('/', Path.DirectorySeparatorChar);
        string direct = Path.Combine(contentRoot, rel + ".uasset");
        if (File.Exists(direct))
        {
            return direct;
        }
        string suffixed = Path.Combine(contentRoot, rel + "_256.uasset");
        return File.Exists(suffixed) ? suffixed : null;
    }

    /// <summary>
    /// Confirmed layout of this game's cooked textures (verified on PF_DXT5 inline,
    /// PF_B8G8R8A8 inline, and PF_DXT5+ubulk samples):
    ///   [int32 SizeX][int32 SizeY][int32 packed][fstring "PF_..."]
    ///   [int32 FirstMip][int32 NumMips][int32 flags][mip0 pixels if inline]
    /// and the file ends with [SizeX][SizeY][1][12 zero bytes][magic 9E2A83C1].
    /// When a .ubulk exists, mip0 is simply its first ExpectedSize bytes.
    /// </summary>
    private static SKBitmap? DecodeTexture(string uassetPath)
    {
        string uexpPath = Path.ChangeExtension(uassetPath, ".uexp");
        if (!File.Exists(uexpPath))
        {
            return null;
        }
        byte[] uexp = File.ReadAllBytes(uexpPath);

        int pfLenPos = FindPixelFormatString(uexp, out string format);
        if (pfLenPos < 12)
        {
            return null;
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(uexp.AsSpan(pfLenPos - 12));
        int height = BinaryPrimitives.ReadInt32LittleEndian(uexp.AsSpan(pfLenPos - 8));
        if (width is < 4 or > 8192 || height is < 4 or > 8192)
        {
            return null;
        }

        (int mip0Size, bool isDxt5, bool isDxt1) = format switch
        {
            "PF_B8G8R8A8" => (width * height * 4, false, false),
            "PF_DXT5" => ((width + 3) / 4 * ((height + 3) / 4) * 16, true, false),
            "PF_DXT1" => ((width + 3) / 4 * ((height + 3) / 4) * 8, false, true),
            _ => (0, false, false),
        };
        if (mip0Size == 0)
        {
            return null;
        }

        string ubulkPath = Path.ChangeExtension(uassetPath, ".ubulk");
        ReadOnlySpan<byte> mip0;
        if (File.Exists(ubulkPath))
        {
            byte[] ubulk = File.ReadAllBytes(ubulkPath);
            if (ubulk.Length < mip0Size)
            {
                return null;
            }
            mip0 = ubulk.AsSpan(0, mip0Size);
        }
        else
        {
            int stringLen = BinaryPrimitives.ReadInt32LittleEndian(uexp.AsSpan(pfLenPos));
            int pixelsStart = pfLenPos + 4 + stringLen + 12; // FirstMip + NumMips + flags
            if (pixelsStart + mip0Size > uexp.Length)
            {
                return null;
            }
            mip0 = uexp.AsSpan(pixelsStart, mip0Size);
        }

        if (isDxt5 || isDxt1)
        {
            return BgraToBitmap(DecompressDxt(mip0, width, height, isDxt5), width, height);
        }
        return BgraToBitmap(mip0, width, height);
    }

    /// <summary>Finds the length-prefixed "PF_*" FString in the uexp; returns the length-field position.</summary>
    private static int FindPixelFormatString(byte[] uexp, out string format)
    {
        format = "";
        int limit = Math.Min(uexp.Length - 8, 4096); // it sits in the small property header
        for (int pos = 0; pos < limit; pos++)
        {
            if (uexp[pos] != (byte)'P' || uexp[pos + 1] != (byte)'F' || uexp[pos + 2] != (byte)'_')
            {
                continue;
            }
            int lenPos = pos - 4;
            if (lenPos < 0)
            {
                continue;
            }
            int len = BinaryPrimitives.ReadInt32LittleEndian(uexp.AsSpan(lenPos));
            if (len is < 4 or > 64 || lenPos + 4 + len > uexp.Length || uexp[lenPos + 4 + len - 1] != 0)
            {
                continue;
            }
            format = System.Text.Encoding.ASCII.GetString(uexp, pos, len - 1);
            return lenPos;
        }
        return -1;
    }

    private static SKBitmap BgraToBitmap(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bgra.CopyTo(bitmap.GetPixelSpan());
        return bitmap;
    }

    /// <summary>Minimal BC1/BC3 block decompression to BGRA.</summary>
    private static byte[] DecompressDxt(ReadOnlySpan<byte> data, int width, int height, bool isDxt5)
    {
        byte[] output = new byte[width * height * 4];
        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;
        int blockSize = isDxt5 ? 16 : 8;

        Span<byte> alpha = stackalloc byte[16];
        Span<(byte R, byte G, byte B)> palette = stackalloc (byte, byte, byte)[4];
        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                int offset = (by * blocksWide + bx) * blockSize;
                alpha.Fill(255);

                if (isDxt5)
                {
                    DecodeBc3AlphaBlock(data.Slice(offset, 8), alpha);
                    offset += 8;
                }

                ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                ushort c1 = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
                uint indices = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);

                palette[0] = Expand565(c0);
                palette[1] = Expand565(c1);
                if (c0 > c1 || isDxt5)
                {
                    palette[2] = Mix(palette[0], palette[1], 2, 1);
                    palette[3] = Mix(palette[0], palette[1], 1, 2);
                }
                else
                {
                    palette[2] = Mix(palette[0], palette[1], 1, 1);
                    palette[3] = (0, 0, 0); // + transparent in BC1 punch-through
                }

                for (int py = 0; py < 4; py++)
                {
                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px, y = by * 4 + py;
                        if (x >= width || y >= height)
                        {
                            continue;
                        }
                        int idx = (int)((indices >> ((py * 4 + px) * 2)) & 0b11);
                        var (r, g, b) = palette[idx];
                        byte a = alpha[py * 4 + px];
                        if (!isDxt5 && idx == 3 && c0 <= c1)
                        {
                            a = 0;
                        }
                        int o = (y * width + x) * 4;
                        output[o] = b;
                        output[o + 1] = g;
                        output[o + 2] = r;
                        output[o + 3] = a;
                    }
                }
            }
        }
        return output;
    }

    private static void DecodeBc3AlphaBlock(ReadOnlySpan<byte> block, Span<byte> alpha)
    {
        byte a0 = block[0], a1 = block[1];
        Span<byte> table = stackalloc byte[8];
        table[0] = a0;
        table[1] = a1;
        if (a0 > a1)
        {
            for (int i = 1; i < 7; i++)
            {
                table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            }
        }
        else
        {
            for (int i = 1; i < 5; i++)
            {
                table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
            }
            table[6] = 0;
            table[7] = 255;
        }

        ulong bits = 0;
        for (int i = 5; i >= 0; i--)
        {
            bits = (bits << 8) | block[2 + i];
        }
        for (int i = 0; i < 16; i++)
        {
            alpha[i] = table[(int)((bits >> (i * 3)) & 0b111)];
        }
    }

    private static (byte R, byte G, byte B) Expand565(ushort c)
    {
        int r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
        return ((byte)(r << 3 | r >> 2), (byte)(g << 2 | g >> 4), (byte)(b << 3 | b >> 2));
    }

    private static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, int wa, int wb)
        => ((byte)((a.R * wa + b.R * wb) / (wa + wb)),
            (byte)((a.G * wa + b.G * wb) / (wa + wb)),
            (byte)((a.B * wa + b.B * wb) / (wa + wb)));

}
