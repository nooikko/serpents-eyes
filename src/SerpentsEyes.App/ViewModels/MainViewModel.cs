using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SerpentsEyes.Core;
using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public const string AllCategoriesKey = "*";

    /// <summary>Sidebar ordering; categories not listed here go to the end alphabetically.</summary>
    private static readonly string[] CategoryOrder =
    [
        "Meta", "Class", "Weapon", "Item", "Blessing", "Mushroom", "Prayer",
        "KillsFor", "Kill", "Curse", "Quest", "Shortcut", "Location", "Emotes", "Utility",
    ];

    /// <summary>Game-data tags grouped by category — the "everything that exists" side of completion.</summary>
    private static readonly Dictionary<string, List<GameTagInfo>> DbByCategory =
        TagDatabase.All.GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    private SaveProfile? _profile;
    private ProfileChoice? _selectedProfile;
    private CategoryItem? _selectedCategory;
    private string _searchText = string.Empty;
    private string? _statusMessage;
    private bool _hasRun;
    private string _mapName = string.Empty;
    private string _positionText = string.Empty;
    private string _fileInfoText = string.Empty;
    private string _completionText = string.Empty;

    public MainViewModel()
    {
        RefreshProfiles();
    }

    public ObservableCollection<ProfileChoice> Profiles { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public ObservableCollection<RecordRow> FilteredRecords { get; } = [];
    public ObservableCollection<LoadoutChip> Loadout { get; } = [];

    public ProfileChoice? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value) && value is not null)
            {
                LoadProfile(value.Path);
            }
        }
    }

    public CategoryItem? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value))
            {
                RefreshRecords();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                RefreshRecords();
            }
        }
    }

    /// <summary>Error or empty-state text; null when a profile is loaded and healthy.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => _statusMessage is not null;

    /// <summary>Footer summary for the selected category, e.g. "19 of 26 Callings unlocked".</summary>
    public string CompletionText { get => _completionText; private set => SetField(ref _completionText, value); }

    public bool HasRun { get => _hasRun; private set => SetField(ref _hasRun, value); }
    public string MapName { get => _mapName; private set => SetField(ref _mapName, value); }
    public string PositionText { get => _positionText; private set => SetField(ref _positionText, value); }
    public string FileInfoText { get => _fileInfoText; private set => SetField(ref _fileInfoText, value); }

    public void RefreshProfiles()
    {
        string? previous = _selectedProfile?.Path;
        Profiles.Clear();
        foreach (string path in SaveLocator.FindProfiles())
        {
            Profiles.Add(new ProfileChoice(path, Path.GetFileNameWithoutExtension(path)));
        }

        if (Profiles.Count == 0)
        {
            StatusMessage = $"No save files found in {SaveLocator.DefaultSaveDirectory}. Open one manually to get started.";
            return;
        }

        SelectedProfile =
            Profiles.FirstOrDefault(p => p.Path == previous)
            ?? Profiles.FirstOrDefault(p => p.DisplayName.Equals("profile_0", StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];
    }

    public void OpenFile(string path)
    {
        var choice = Profiles.FirstOrDefault(p => p.Path == path);
        if (choice is null)
        {
            choice = new ProfileChoice(path, Path.GetFileNameWithoutExtension(path));
            Profiles.Add(choice);
        }
        SelectedProfile = choice;
    }

    private void LoadProfile(string path)
    {
        try
        {
            _profile = SaveProfile.Load(path);
            StatusMessage = null;

            var info = new FileInfo(path);
            FileInfoText = $"{info.Length:N0} bytes · saved {info.LastWriteTime:g}";
        }
        catch (Exception ex) when (ex is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            _profile = null;
            FileInfoText = string.Empty;
            StatusMessage = $"Could not read {Path.GetFileName(path)}: {ex.Message}";
        }

        RebuildCategories();
        RefreshSnapshot();
        RefreshRecords();
    }

    private void RebuildCategories()
    {
        string? previousKey = _selectedCategory?.Key;
        Categories.Clear();

        if (_profile is null)
        {
            SelectedCategory = null;
            return;
        }

        var ownedByCategory = _profile.Records
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Select(r => r.FullTag).ToHashSet(StringComparer.Ordinal));

        var categoryKeys = ownedByCategory.Keys.Union(DbByCategory.Keys, StringComparer.Ordinal)
            .OrderBy(k => { int i = Array.IndexOf(CategoryOrder, k); return i < 0 ? int.MaxValue : i; })
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();

        int allOwned = _profile.Records.Count, allTotal = 0;
        var items = new List<CategoryItem>();
        foreach (string key in categoryKeys)
        {
            var owned = ownedByCategory.GetValueOrDefault(key) ?? [];
            int? total = null;
            if (DbByCategory.TryGetValue(key, out var known))
            {
                // The universe is the union: the save can hold tags the game data doesn't list.
                total = known.Select(t => t.Tag).Union(owned, StringComparer.Ordinal).Count();
            }
            allTotal += total ?? owned.Count;
            items.Add(new CategoryItem(key, Display.CategoryDisplay(key), owned.Count, total));
        }

        Categories.Add(new CategoryItem(AllCategoriesKey, "All", allOwned, allTotal));
        foreach (var item in items)
        {
            Categories.Add(item);
        }

        SelectedCategory = Categories.FirstOrDefault(c => c.Key == previousKey) ?? Categories[0];
    }

    private void RefreshSnapshot()
    {
        Loadout.Clear();
        var snapshot = _profile?.RunSnapshot;
        HasRun = snapshot?.HasRun ?? false;
        if (snapshot is null || !snapshot.HasRun)
        {
            MapName = string.Empty;
            PositionText = string.Empty;
            return;
        }

        MapName = TagDatabase.MapTitle(snapshot.MapName) ?? Display.Prettify(snapshot.MapName);
        PositionText = string.Create(CultureInfo.InvariantCulture,
            $"X {snapshot.X:N0}   Y {snapshot.Y:N0}   Z {snapshot.Z:N0}");

        foreach (var entry in snapshot.Loadout)
        {
            // Loadout ids are definition-asset names like "Tree_Warhammer".
            var info = TagDatabase.FindByInternalId(entry.Id);
            if (info?.DisplayName is not null)
            {
                Loadout.Add(new LoadoutChip(Display.CategorySingular(info.Category), info.DisplayName));
                continue;
            }
            int underscore = entry.Id.IndexOf('_');
            string kind = underscore > 0 ? entry.Id[..underscore] : entry.SlotType;
            string name = underscore > 0 ? entry.Id[(underscore + 1)..] : entry.Id;
            Loadout.Add(new LoadoutChip(Display.Prettify(kind), Display.Prettify(name)));
        }
    }

    private void RefreshRecords()
    {
        FilteredRecords.Clear();
        if (_profile is null)
        {
            return;
        }

        string? categoryKey = _selectedCategory?.Key;
        bool all = categoryKey is null or AllCategoriesKey;
        string search = _searchText.Trim();

        IEnumerable<TagRecord> records = _profile.Records;
        if (!all)
        {
            records = records.Where(r => r.Category == categoryKey);
        }
        if (search.Length > 0)
        {
            records = records.Where(r =>
                r.FullTag.Contains(search, StringComparison.OrdinalIgnoreCase)
                || TagDatabase.Find(r.FullTag)?.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        var rows = records
            .OrderBy(r => { int i = Array.IndexOf(CategoryOrder, r.Category); return i < 0 ? int.MaxValue : i; })
            .ThenBy(r => r.Category, StringComparer.Ordinal)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(MakeRow);
        foreach (var row in rows)
        {
            FilteredRecords.Add(row);
        }

        foreach (var row in LockedRows(all, categoryKey, search))
        {
            FilteredRecords.Add(row);
        }

        UpdateCompletionText();
    }

    /// <summary>Known game content the save has never touched, shown greyed with unlock hints.</summary>
    private IEnumerable<RecordRow> LockedRows(bool all, string? categoryKey, string search)
    {
        var ownedTags = _profile!.Records.Select(r => r.FullTag).ToHashSet(StringComparer.Ordinal);

        IEnumerable<GameTagInfo> known = all
            ? TagDatabase.All
            : DbByCategory.GetValueOrDefault(categoryKey ?? "") ?? Enumerable.Empty<GameTagInfo>();

        var locked = known.Where(t => !ownedTags.Contains(t.Tag));
        if (search.Length > 0)
        {
            locked = locked.Where(t =>
                t.Tag.Contains(search, StringComparison.OrdinalIgnoreCase)
                || t.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        return locked
            .OrderBy(t => { int i = Array.IndexOf(CategoryOrder, t.Category); return i < 0 ? int.MaxValue : i; })
            .ThenBy(t => t.DisplayName ?? t.Tag, StringComparer.Ordinal)
            .Select(MakeLockedRow);
    }

    private static RecordRow MakeLockedRow(GameTagInfo info)
    {
        string leaf = info.Tag.Split('.') is { Length: >= 3 } parts ? string.Join('.', parts[2..]) : info.Tag;
        string display = info.DisplayName ?? Display.Prettify(leaf);
        string? hint = info.UnlockHint ?? (info.God is { } god ? $"Offered by the {god}" : null);

        var tooltip = new StringBuilder(info.Tag);
        tooltip.Append("\n\nNot in your save yet.");
        if (info.Description is { } desc)
        {
            tooltip.Append("\n\n").Append(desc);
        }
        if (info.Flavor is { } flavor)
        {
            tooltip.Append("\n“").Append(flavor).Append('”');
        }
        if (hint is not null)
        {
            tooltip.Append("\n\nHow to get it: ").Append(hint);
        }

        return new RecordRow(display, info.Tag, Display.CategoryDisplay(info.Category), 0,
            tooltip.ToString(), IsLocked: true, UnlockHint: hint);
    }

    private void UpdateCompletionText()
    {
        if (_selectedCategory is { HasCompletion: true, Total: { } total } cat)
        {
            int locked = total - cat.Owned;
            string what = cat.Key == AllCategoriesKey ? "known unlocks" : cat.DisplayName;
            CompletionText = locked == 0
                ? $"All {total} {what} collected"
                : $"{cat.Owned} of {total} {what} collected · greyed rows show what's left and how to get it";
        }
        else
        {
            CompletionText = "Read-only viewer · counters show unlocks, run tallies and kill counts";
        }
    }

    private static RecordRow MakeRow(TagRecord record)
    {
        var info = TagDatabase.Find(record.FullTag);
        string display = info?.DisplayName ?? Display.Prettify(record.Name);

        var tooltip = new StringBuilder(record.FullTag);
        if (info?.Description is { } desc)
        {
            tooltip.Append("\n\n").Append(desc);
        }
        if (info?.Flavor is { } flavor)
        {
            tooltip.Append("\n“").Append(flavor).Append('”');
        }
        if (info?.God is { } god)
        {
            tooltip.Append("\n\nGranted by the ").Append(god);
        }

        return new RecordRow(display, record.FullTag, Display.CategoryDisplay(record.Category), record.Value, tooltip.ToString());
    }
}
