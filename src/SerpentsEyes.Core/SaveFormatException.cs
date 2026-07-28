namespace SerpentsEyes.Core;

/// <summary>Thrown when a save file does not match the expected NG_SaveFormat_4 layout.</summary>
public sealed class SaveFormatException : Exception
{
    /// <summary>Byte offset in the file where parsing failed, or -1 if unknown.</summary>
    public long Offset { get; }

    /// <summary>Creates the exception, appending the byte offset to the message when known.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="offset">Byte offset where parsing failed, or -1 if unknown.</param>
    /// <param name="inner">The underlying exception, if any.</param>
    public SaveFormatException(string message, long offset = -1, Exception? inner = null)
        : base(offset >= 0 ? $"{message} (at byte offset 0x{offset:X})" : message, inner)
    {
        Offset = offset;
    }
}
