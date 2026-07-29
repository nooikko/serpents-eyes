using System.Diagnostics;

namespace SerpentsEyes.Core.Tests;

/// <summary>
/// The parser is fed whatever is on disk, including files the game never wrote. Every failure
/// must surface as <see cref="SaveFormatException"/> so callers can catch one thing, and no
/// input may drive a large allocation or a long stall from a small file.
/// </summary>
public class MalformedInputTests
{
    private static string ProfilePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "profile_0.sav");

    [Fact]
    public void Truncation_At_Any_Offset_Throws_Only_SaveFormatException()
    {
        byte[] original = File.ReadAllBytes(ProfilePath);

        for (int length = 0; length < original.Length; length++)
        {
            byte[] truncated = original.AsSpan(0, length).ToArray();
            try
            {
                SaveProfile.Parse(truncated);
            }
            catch (SaveFormatException)
            {
                // The only acceptable failure.
            }
            catch (Exception ex)
            {
                Assert.Fail($"Truncating to {length} byte(s) threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [Fact]
    public void Absurd_Record_Count_Throws_Without_Allocating_For_It()
    {
        // A tiny file claiming a million records used to preallocate the list before
        // reading a single record.
        byte[] file = new SaveFileBuilder().Header().ToArray();
        byte[] withCount = [.. file, .. BitConverter.GetBytes(999_999_999)];

        long before = GC.GetTotalAllocatedBytes(precise: true);
        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(withCount));
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        Assert.True(allocated < 1_000_000, $"parse allocated {allocated} bytes for a {withCount.Length}-byte file");
    }

    [Fact]
    public void Negative_Record_Count_Throws()
    {
        byte[] file = new SaveFileBuilder().Header().ToArray();
        byte[] withCount = [.. file, .. BitConverter.GetBytes(-5)];

        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(withCount));
    }

    [Fact]
    public void Absurd_String_Length_Throws()
    {
        // A build-id FString claiming 2 GB.
        byte[] file = new SaveFileBuilder()
            .Header(buildId: BitConverter.GetBytes(int.MaxValue))
            .ToArray();

        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(file));
    }

    [Fact]
    public void Int_MinValue_String_Length_Throws()
    {
        // Negative lengths mean UTF-16; int.MinValue negated overflows a 32-bit int.
        byte[] file = new SaveFileBuilder()
            .Header(buildId: BitConverter.GetBytes(int.MinValue))
            .ToArray();

        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(file));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(0xAB)]
    [InlineData(0xFF)]
    public void Uniform_Garbage_Throws(byte fill)
    {
        byte[] garbage = new byte[512];
        Array.Fill(garbage, fill);

        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse(garbage));
    }

    [Fact]
    public void Random_Bytes_Never_Hang_Or_Throw_Unexpectedly()
    {
        var random = new Random(20260728); // fixed seed: failures must be reproducible
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 500; i++)
        {
            byte[] garbage = new byte[random.Next(0, 4096)];
            random.NextBytes(garbage);
            try
            {
                SaveProfile.Parse(garbage);
            }
            catch (SaveFormatException)
            {
            }
            catch (Exception ex)
            {
                Assert.Fail($"Seeded input #{i} threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"500 parses took {stopwatch.Elapsed}");
    }

    [Fact]
    public void Mutating_A_Real_Save_Below_The_Header_Stays_Within_Contract()
    {
        // Random bytes practically never satisfy the format-id check, so the test above only
        // ever exercises the header. Keeping a real header and corrupting what follows is what
        // reaches the record and trailer parsers, and what a save someone edited actually looks
        // like. Everything that parses must also round-trip and survive the GameData builders.
        byte[] original = File.ReadAllBytes(ProfilePath);
        int marker = original.AsSpan().IndexOf("NG_SaveFormat"u8);
        Assert.True(marker > 0, "fixture has no format id");
        int bodyStart = marker + original.AsSpan(marker).IndexOf((byte)0) + 1;

        var random = new Random(20260728); // fixed seed: failures must be reproducible
        var stopwatch = Stopwatch.StartNew();
        int parsed = 0;

        for (int i = 0; i < 2000; i++)
        {
            byte[] mutant = (byte[])original.Clone();
            for (int edit = random.Next(1, 5); edit > 0; edit--)
            {
                mutant[random.Next(bodyStart, mutant.Length)] = (byte)random.Next(256);
            }

            SaveProfile profile;
            try
            {
                profile = SaveProfile.Parse(mutant);
            }
            catch (SaveFormatException)
            {
                continue;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Mutant #{i} threw {ex.GetType().Name} from Parse: {ex.Message}");
                continue;
            }

            parsed++;
            try
            {
                Assert.Equal(mutant, profile.ToBytes());
                _ = profile.ValuesByTag();
                _ = Core.GameData.QuestLines.Build(profile);
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                Assert.Fail($"Mutant #{i} parsed but then threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(parsed > 200, $"only {parsed} of 2000 mutants parsed; the body is barely being reached");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"2000 mutants took {stopwatch.Elapsed}");
    }

    [Fact]
    public void Empty_File_Throws()
    {
        Assert.Throws<SaveFormatException>(() => SaveProfile.Parse([]));
    }
}
