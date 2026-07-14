namespace SerpentsEyes.Extractor;

/// <summary>Diagnostic mode: verify parsing assumptions against a few known assets.</summary>
internal static class Probe
{
    public static void Run(string contentRoot)
    {
        Console.WriteLine("== StringTable parse check ==");
        foreach (string name in new[] { "StringTable_Prayers", "StringTable_Characters", "StringTable_UI", "StringTable_World" })
        {
            string path = Path.Combine(contentRoot, "StringTables", name + ".uexp");
            var (ns, entries) = StringTableParser.Parse(File.ReadAllBytes(path));
            Console.WriteLine($"\n{name}: namespace='{ns}', {entries.Count} entries");
            foreach (var (k, v) in entries.Take(45))
            {
                Console.WriteLine($"  {k} = {Truncate(v)}");
            }
        }

        Console.WriteLine("\n== Definition asset string sequences ==");
        string[] samples =
        [
            @"GameplayAbilitySystem\Trees\BerserkerBlade\Tree_BerserkerBlade",
            @"GameplayAbilitySystem\Trees\Mace\Weapon_Warhammer\Tree_Warhammer",
            @"GameplayAbilitySystem\Classes\Default\Stronk\Class_Stronk",
            @"GameplayAbilitySystem\Blessings\ChanceHoT\Blessing_ChanceHoT",
        ];
        foreach (string rel in samples)
        {
            foreach (string ext in new[] { ".uasset", ".uexp" })
            {
                string path = Path.Combine(contentRoot, rel + ext);
                if (!File.Exists(path))
                {
                    Console.WriteLine($"\n{rel}{ext}: MISSING");
                    continue;
                }
                var strings = UexpStrings.Scan(File.ReadAllBytes(path));
                Console.WriteLine($"\n{rel}{ext}: {strings.Count} strings");
                foreach (string s in strings.Take(40))
                {
                    Console.WriteLine($"  | {Truncate(s)}");
                }
            }
        }
    }

    private static string Truncate(string s)
    {
        s = s.Replace("\r", "\\r").Replace("\n", "\\n");
        return s.Length <= 90 ? s : s[..90] + "…";
    }
}
