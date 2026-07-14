using System.Text;

namespace SerpentsEyes.Extractor;

/// <summary>
/// Recovers length-prefixed ASCII strings (UE FString layout: int32 length including
/// trailing NUL, then bytes) from binary asset data, in file order. Positions that
/// don't look like a string are skipped one byte at a time.
/// </summary>
internal static class UexpStrings
{
    private const int MaxLength = 4096;

    public static List<string> Scan(ReadOnlySpan<byte> data)
    {
        var result = new List<string>();
        int pos = 0;
        while (pos + 4 <= data.Length)
        {
            int len = BitConverter.ToInt32(data.Slice(pos, 4));
            if (len >= 2 && len <= MaxLength && pos + 4 + len <= data.Length
                && data[pos + 4 + len - 1] == 0 && IsPrintableAscii(data.Slice(pos + 4, len - 1)))
            {
                result.Add(Encoding.ASCII.GetString(data.Slice(pos + 4, len - 1)));
                pos += 4 + len;
            }
            else if (len is <= -2 and >= -MaxLength && pos + 4 + -len * 2 <= data.Length
                     && data[pos + 4 + -len * 2 - 1] == 0 && data[pos + 4 + -len * 2 - 2] == 0)
            {
                // Negative length = UTF-16LE with -len characters including the NUL.
                string wide = Encoding.Unicode.GetString(data.Slice(pos + 4, (-len - 1) * 2));
                if (wide.All(c => !char.IsControl(c) || c is '\t' or '\n' or '\r'))
                {
                    result.Add(wide);
                    pos += 4 + -len * 2;
                }
                else
                {
                    pos++;
                }
            }
            else
            {
                pos++;
            }
        }
        return result;
    }

    private static bool IsPrintableAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            // Allow tab/newline: descriptions can be multi-line.
            if (b is not (>= 0x20 and < 0x7F) and not (0x09 or 0x0A or 0x0D))
            {
                return false;
            }
        }
        return true;
    }
}
