namespace SerpentsEyes.Core;

/// <summary>Thrown when a save file does not match the expected NG_SaveFormat_4 layout.</summary>
public sealed class SaveFormatException : Exception
{
    /// <summary>Byte offset in the file where parsing failed, or -1 if unknown.</summary>
    public long Offset { get; }

    public SaveFormatException(string message, long offset = -1, Exception? inner = null)
        : base(offset >= 0 ? $"{message} (at byte offset 0x{offset:X})" : message, inner)
    {
        Offset = offset;
    }
}
