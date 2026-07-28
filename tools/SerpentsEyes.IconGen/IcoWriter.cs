using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace SerpentsEyes.IconGen;

/// <summary>
/// Writes a multi-resolution Windows .ico. SkiaSharp cannot encode one, and the format is
/// small enough that a dependency would cost more than the forty lines it takes.
/// </summary>
/// <remarks>
/// Sizes up to 128 are stored as bottom-up 32bpp DIBs, which every version of the Windows
/// shell reads. PNG-compressed entries are only officially blessed at 256x256, and smaller
/// PNG entries render with black backgrounds in some shell surfaces — so 256 is the only PNG.
/// </remarks>
internal static class IcoWriter
{
    private const int DirectoryEntrySize = 16;
    private const int HeaderSize = 6;
    private const int BitmapInfoHeaderSize = 40;

    public static void Write(string path, IReadOnlyList<SKBitmap> images)
    {
        byte[][] payloads = [.. images.Select(Encode)];

        using var stream = File.Create(path);
        Span<byte> buffer = stackalloc byte[DirectoryEntrySize];

        BinaryPrimitives.WriteUInt16LittleEndian(buffer[..2], 0);           // reserved
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[2..4], 1);          // type: icon
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..6], (ushort)images.Count);
        stream.Write(buffer[..HeaderSize]);

        int offset = HeaderSize + (DirectoryEntrySize * images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            buffer.Clear();
            // 256 is stored as 0: the field is one byte and 256 does not fit.
            buffer[0] = (byte)(images[i].Width == 256 ? 0 : images[i].Width);
            buffer[1] = (byte)(images[i].Height == 256 ? 0 : images[i].Height);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..6], 1);      // colour planes
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[6..8], 32);     // bits per pixel
            BinaryPrimitives.WriteInt32LittleEndian(buffer[8..12], payloads[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(buffer[12..16], offset);
            stream.Write(buffer);
            offset += payloads[i].Length;
        }

        foreach (byte[] payload in payloads)
        {
            stream.Write(payload);
        }
    }

    private static byte[] Encode(SKBitmap bitmap) =>
        bitmap.Width == 256 ? EncodePng(bitmap) : EncodeDib(bitmap);

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException($"Could not PNG-encode the {bitmap.Width}px icon.");
        return data.ToArray();
    }

    private static byte[] EncodeDib(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] pixels = ReadUnpremultiplied(bitmap);

        int colourStride = width * 4;
        int maskStride = (width + 31) / 32 * 4;
        int colourBytes = colourStride * height;
        int maskBytes = maskStride * height;

        byte[] output = new byte[BitmapInfoHeaderSize + colourBytes + maskBytes];
        var span = output.AsSpan();

        BinaryPrimitives.WriteInt32LittleEndian(span[..4], BitmapInfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..8], width);
        // Doubled: the header describes the colour rows and the AND mask rows together.
        BinaryPrimitives.WriteInt32LittleEndian(span[8..12], height * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..14], 1);          // colour planes
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..16], 32);         // bits per pixel
        BinaryPrimitives.WriteInt32LittleEndian(span[16..20], 0);           // BI_RGB
        BinaryPrimitives.WriteInt32LittleEndian(span[20..24], colourBytes + maskBytes);

        // DIB rows run bottom-up.
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> source = pixels.AsSpan((height - 1 - y) * colourStride, colourStride);
            source.CopyTo(span.Slice(BitmapInfoHeaderSize + (y * colourStride), colourStride));
        }

        // The AND mask is left all-zero: with a 32bpp image the alpha channel is authoritative,
        // but the mask still has to be there for the entry to be a well-formed icon.
        return output;
    }

    private static byte[] ReadUnpremultiplied(SKBitmap bitmap)
    {
        var info = new SKImageInfo(
            bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        byte[] pixels = new byte[info.BytesSize];

        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using SKPixmap pixmap = bitmap.PeekPixels()
                ?? throw new InvalidOperationException("Rendered bitmap exposed no pixels.");
            if (!pixmap.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes))
            {
                throw new InvalidOperationException(
                    $"Could not read the {bitmap.Width}px icon back as unpremultiplied BGRA.");
            }
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }
}
