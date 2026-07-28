namespace SerpentsEyes.Core.Tests;

public class SaveLocatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "serpents-eyes-tests", Guid.NewGuid().ToString("n"));

    public SaveLocatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
        GC.SuppressFinalize(this);
    }

    private void Touch(string name) => File.WriteAllBytes(Path.Combine(_dir, name), []);

    [Fact]
    public void Missing_Directory_Returns_Empty()
    {
        Assert.Empty(SaveLocator.FindProfiles(Path.Combine(_dir, "does-not-exist")));
    }

    [Fact]
    public void Finds_Only_Sav_Files()
    {
        Touch("profile_0.sav");
        Touch("notes.txt");
        Touch("profile_0.sav.bak");

        var found = SaveLocator.FindProfiles(_dir);

        Assert.Single(found);
        Assert.EndsWith("profile_0.sav", found[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_Primary_Profiles_Before_Autosaves_And_Backups()
    {
        // The old plain filename sort put profile_0_autosave ahead of profile_1, so the
        // profile picker opened a backup by default when more than one profile existed.
        Touch("profile_0_autosave.sav");
        Touch("profile_1.sav");
        Touch("profile_0_bak1.sav");
        Touch("profile_0.sav");

        string[] names = [.. SaveLocator.FindProfiles(_dir).Select(p => Path.GetFileName(p))];

        Assert.Equal(
            ["profile_0.sav", "profile_1.sav", "profile_0_autosave.sav", "profile_0_bak1.sav"],
            names);
    }

    [Fact]
    public void Empty_Directory_Returns_Empty()
    {
        Assert.Empty(SaveLocator.FindProfiles(_dir));
    }

    [Fact]
    public void Candidate_Directories_Always_Include_The_Default()
    {
        Assert.Contains(SaveLocator.DefaultSaveDirectory, SaveLocator.CandidateSaveDirectories());
    }

    [Fact]
    public void FindProfiles_Over_All_Candidates_Does_Not_Throw()
    {
        // Whatever this machine looks like, discovery must not be able to take the app down.
        _ = SaveLocator.FindProfiles();
    }

    [Fact]
    public void Invalid_Path_Characters_Return_Empty_Rather_Than_Throwing()
    {
        Assert.Empty(SaveLocator.FindProfiles("\0invalid"));
    }
}
