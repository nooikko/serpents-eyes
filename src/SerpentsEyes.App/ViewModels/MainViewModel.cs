using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using SerpentsEyes.Core;
using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public const string DivinitiesKey = "@Divinities";

    /// <summary>Sidebar ordering. Meta is API-only; Prayer/KillsFor fold into Divinities.</summary>
    private static readonly string[] CategoryOrder =
    [
        "Class", "Weapon", "Item", "Blessing", "Mushroom", "Utility", "Curse",
        DivinitiesKey, "Quest", "Shortcut", "Kill", "Location", "Emotes",
    ];

    private static readonly HashSet<string> HiddenCategories =
        new(["Meta", "Prayer", "KillsFor"], StringComparer.Ordinal);

    /// <summary>Game-data tags grouped by category — the "everything that exists" side of completion.</summary>
    private static readonly Dictionary<string, List<GameTagInfo>> DbByCategory =
        TagDatabase.All.GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    private SaveProfile? _profile;
    private ProfileChoice? _selectedProfile;
    private CategoryItem? _selectedCategory;
    private object? _selectedItem;
    private DetailModel? _detail;
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
    public ObservableCollection<object> Items { get; } = [];
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
                RefreshItems();
            }
        }
    }

    /// <summary>The selected card (ItemCard or GodCard); drives the detail pane.</summary>
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetField(ref _selectedItem, value))
            {
                Detail = value switch
                {
                    ItemCard card => DetailModel.ForItem(card, _profile),
                    GodCard card => DetailModel.ForGod(card, _profile),
                    _ => null,
                };
            }
        }
    }

    public DetailModel? Detail
    {
        get => _detail;
        private set
        {
            if (SetField(ref _detail, value))
            {
                OnPropertyChanged(nameof(HasDetail));
            }
        }
    }

    public bool HasDetail => _detail is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                RefreshItems();
            }
        }
    }

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

        // Force a reload even when the selection object did not change.
        if (_selectedProfile is not null)
        {
            LoadProfile(_selectedProfile.Path);
        }
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
        RefreshItems();
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
            .Where(k => !HiddenCategories.Contains(k))
            .Append(DivinitiesKey)
            .Distinct()
            .OrderBy(k => { int i = Array.IndexOf(CategoryOrder, k); return i < 0 ? int.MaxValue : i; })
            .ThenBy(k => k, StringComparer.Ordinal)
            .ToList();

        foreach (string key in categoryKeys)
        {
            if (key == DivinitiesKey)
            {
                int touched = TagDatabase.Gods.Count(g =>
                    ownedByCategory.GetValueOrDefault("Prayer")?.Contains($"Progression.Prayer.{g.Key}") == true
                    || ownedByCategory.GetValueOrDefault("KillsFor")?.Contains($"Progression.KillsFor.{g.Key}") == true);
                Categories.Add(new CategoryItem(DivinitiesKey, "Divinities", touched, TagDatabase.Gods.Count));
                continue;
            }

            var owned = ownedByCategory.GetValueOrDefault(key) ?? [];
            int? total = null;
            if (DbByCategory.TryGetValue(key, out var known))
            {
                // The completion bar tracks unlockables only; base-pool items can't be missing.
                total = known.Where(t => t.HasProgression).Select(t => t.Tag)
                    .Union(owned, StringComparer.Ordinal).Count();
            }
            Categories.Add(new CategoryItem(key, Display.CategoryDisplay(key), owned.Count, total));
        }

        SelectedCategory = Categories.FirstOrDefault(c => c.Key == previousKey) ?? Categories.FirstOrDefault();
    }

    private void RefreshSnapshot()
    {
        Loadout.Clear();
        var snapshot = _profile?.RunSnapshot;
        HasRun = snapshot?.HasRun == true && snapshot.MapName != "None";
        if (snapshot is null || !HasRun)
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

    private void RefreshItems()
    {
        Items.Clear();
        SelectedItem = null;
        if (_profile is null)
        {
            return;
        }

        string search = _searchText.Trim();
        bool searching = search.Length > 0;

        if (!searching && _selectedCategory?.Key == DivinitiesKey)
        {
            foreach (var god in BuildGodCards())
            {
                Items.Add(god);
            }
            return;
        }

        var ownedValues = _profile.Records.ToDictionary(r => r.FullTag, r => r.Value, StringComparer.Ordinal);

        IEnumerable<string> categories = searching
            ? CategoryOrder.Where(k => k != DivinitiesKey)
            : [_selectedCategory?.Key ?? "Class"];

        foreach (string category in categories)
        {
            foreach (var card in BuildCards(category, ownedValues, search))
            {
                Items.Add(card);
            }
        }

        if (searching)
        {
            foreach (var god in BuildGodCards().Where(g => g.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            {
                Items.Add(god);
            }
        }
    }

    private IEnumerable<ItemCard> BuildCards(string category, Dictionary<string, int> ownedValues, string search)
    {
        var known = DbByCategory.GetValueOrDefault(category) ?? [];

        // Owned records (including save-only tags the DB doesn't know) and the
        // always-available base pool sort together; locked unlockables go last.
        var owned = _profile!.Records
            .Where(r => r.Category == category)
            .Select(r => MakeCard(r.FullTag, category, TagDatabase.Find(r.FullTag), CardState.Unlocked, r.Value));

        var available = known
            .Where(t => !t.HasProgression)
            .Select(t => MakeCard(t.Tag, category, t, CardState.AlwaysAvailable, null));

        var locked = known
            .Where(t => t.HasProgression && !ownedValues.ContainsKey(t.Tag))
            .Select(t => MakeCard(t.Tag, category, t, CardState.Locked, null));

        var cards = owned.Concat(available).OrderBy(c => c.Name, StringComparer.Ordinal)
            .Concat(locked.OrderBy(c => c.Name, StringComparer.Ordinal));

        foreach (var card in cards)
        {
            if (search.Length == 0
                || card.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || card.Tag.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                yield return card;
            }
        }
    }

    private static ItemCard MakeCard(string tag, string category, GameTagInfo? info, CardState state, int? value)
    {
        string leaf = tag.Split('.') is { Length: >= 3 } parts ? string.Join(" · ", parts[2..]) : tag;
        string name = info?.DisplayName ?? Display.Prettify(leaf);
        return new ItemCard(tag, name, category, IconStore.Get(info?.IconKey), state, value, info);
    }

    private IEnumerable<GodCard> BuildGodCards()
    {
        var values = _profile?.Records.ToDictionary(r => r.FullTag, r => r.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var god in TagDatabase.Gods)
        {
            yield return new GodCard(god, IconStore.Get(god.SymbolKey),
                values.GetValueOrDefault($"Progression.Prayer.{god.Key}"),
                values.GetValueOrDefault($"Progression.KillsFor.{god.Key}"));
        }
    }
}
