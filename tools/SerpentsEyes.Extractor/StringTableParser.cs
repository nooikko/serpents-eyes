using System.Text;

namespace SerpentsEyes.Extractor;

/// <summary>
/// Parses a UE StringTable .uexp payload:
/// [4-byte flags][2 bytes][fstring tableNamespace][int32 count] then count ×
/// ([fstring key][fstring value]), followed by a zero sentinel and a trailing hash.
/// </summary>
internal static class StringTableParser
{
    public static (string Namespace, Dictionary<string, string> Entries) Parse(byte[] data)
    {
        int pos = 6; // 4-byte flag block + 2 bytes
        string ns = ReadFString(data, ref pos);
        int count = BitConverter.ToInt32(data, pos);
        pos += 4;

        var entries = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string key = ReadFString(data, ref pos);
            string value = ReadFString(data, ref pos);
            entries[key] = value;
        }
        return (ns, entries);
    }

    private static string ReadFString(byte[] data, ref int pos)
    {
        int len = BitConverter.ToInt32(data, pos);
        pos += 4;
        if (len == 0)
        {
            return string.Empty;
        }
        if (len < 0) // UTF-16LE, -len chars including NUL
        {
            int chars = -len;
            string wide = Encoding.Unicode.GetString(data, pos, (chars - 1) * 2);
            pos += chars * 2;
            return wide;
        }
        string value = Encoding.ASCII.GetString(data, pos, len - 1);
        pos += len;
        return value;
    }
}
