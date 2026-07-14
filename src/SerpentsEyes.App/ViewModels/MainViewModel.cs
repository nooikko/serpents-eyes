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

    private SaveProfile? _profile;
    private ProfileChoice? _selectedProfile;
    private CategoryItem? _selectedCategory;
    private string _searchText = string.Empty;
    private string? _statusMessage;
    private bool _hasRun;
    private string _mapName = string.Empty;
    private string _positionText = string.Empty;
    private string _fileInfoText = string.Empty;

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

        Categories.Add(new CategoryItem(AllCategoriesKey, "All", _profile.Records.Count));

        var groups = _profile.Records
            .GroupBy(r => r.Category)
            .OrderBy(g => { int i = Array.IndexOf(CategoryOrder, g.Key); return i < 0 ? int.MaxValue : i; })
            .ThenBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            Categories.Add(new CategoryItem(group.Key, Display.CategoryDisplay(group.Key), group.Count()));
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
