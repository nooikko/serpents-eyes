using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using SerpentsEyes.Core;
using SerpentsEyes.Core.GameData;

namespace SerpentsEyes.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public const string DivinitiesKey = "@Divinities";

    /// <summary>Sidebar key for the Quests category, which the game data calls "Quest".</summary>
    public const string QuestsKey = "Quest";

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

    /// <summary>How long to let typing settle before rebuilding the card grid.</summary>
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(150);

    private SaveProfile? _profile;
    private ProfileChoice? _selectedProfile;
    private CategoryItem? _selectedCategory;
    private object? _selectedItem;
    private DetailModel? _detail;
    private string _searchText = string.Empty;
    private string? _statusMessage;
    private bool _hasRun;
    private bool _isBusy;
    private bool _isQuestView;
    private string _mapName = string.Empty;
    private string _positionText = string.Empty;
    private string _fileInfoText = string.Empty;

    /// <summary>Incremented per load so a superseded load cannot overwrite a newer one.</summary>
    private int _loadGeneration;

    private CancellationTokenSource? _searchDebounce;

    public MainViewModel()
    {
        RefreshProfiles();
    }

    public ObservableCollection<ProfileChoice> Profiles { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];
    public ObservableCollection<object> Items { get; } = [];
    public ObservableCollection<LoadoutChip> Loadout { get; } = [];

    /// <summary>Questlines, shown instead of the card grid when the Quests category is selected.</summary>
    public ObservableCollection<QuestLineCard> QuestLines { get; } = [];

    /// <summary>
    /// True when the Quests category is showing. Questlines are ordered sequences rather than a
    /// flat set, so they get a list of panels instead of the card grid.
    /// </summary>
    public bool IsQuestView
    {
        get => _isQuestView;
        private set
        {
            if (SetField(ref _isQuestView, value))
            {
                OnPropertyChanged(nameof(ShowCardGrid));
                OnPropertyChanged(nameof(DetailPaneWidth));
            }
        }
    }

    /// <summary>
    /// Width of the detail column. Collapsed in the quest view: questlines are read in place, so
    /// an empty "select something" pane would cost a column of questlines for nothing.
    /// </summary>
    public GridLength DetailPaneWidth => IsQuestView ? new GridLength(0) : new GridLength(320);

    /// <summary>The card grid is the default view; a status message or the quest view replaces it.</summary>
    public bool ShowCardGrid => !HasStatus && !IsQuestView;

    public ProfileChoice? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value) && value is not null)
            {
                _ = LoadProfileAsync(value.Path);
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
                    ItemCard card => DetailModel.ForItem(card),
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
                DebounceRefreshItems();
            }
        }
    }

    /// <summary>True while a save file is being read and parsed.</summary>
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(ShowCardGrid));
            }
        }
    }

    public bool HasStatus => _statusMessage is not null;

    public bool HasRun { get => _hasRun; private set => SetField(ref _hasRun, value); }
    public string MapName { get => _mapName; private set => SetField(ref _mapName, value); }
    public string PositionText { get => _positionText; private set => SetField(ref _positionText, value); }
    public string FileInfoText { get => _fileInfoText; private set => SetField(ref _fileInfoText, value); }

    /// <summary>Rescans the save directories and reloads the current (or best) profile.</summary>
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
            SetProfile(null);
            StatusMessage =
                $"No save files found in {SaveLocator.DefaultSaveDirectory}. Use Open… to pick one, or drop a .sav file here.";
            return;
        }

        ProfileChoice target =
            Profiles.FirstOrDefault(p => p.Path == previous)
            ?? Profiles.FirstOrDefault(p => p.DisplayName.Equals("profile_0", StringComparison.OrdinalIgnoreCase))
            ?? Profiles[0];

        // ProfileChoice is a record, so re-selecting an equal value would not fire the setter
        // and Reload would do nothing. Assign the field directly and load exactly once.
        _selectedProfile = target;
        OnPropertyChanged(nameof(SelectedProfile));
        _ = LoadProfileAsync(target.Path);
    }

    /// <summary>Opens a save from an arbitrary path, adding it to the profile list.</summary>
    public void OpenFile(string path)
    {
        var choice = Profiles.FirstOrDefault(p => p.Path == path);
        if (choice is null)
        {
            choice = new ProfileChoice(path, Path.GetFileNameWithoutExtension(path));
            Profiles.Add(choice);
        }

        if (Equals(_selectedProfile, choice))
        {
            _ = LoadProfileAsync(choice.Path);
            return;
        }
        SelectedProfile = choice;
    }

    /// <summary>
    /// Reads and parses off the UI thread, then applies the result on it.
    /// </summary>
    /// <remarks>
    /// Never throws: this is invoked without awaiting from property setters and the constructor,
    /// so an escaping exception would surface as an unobserved task fault rather than a message
    /// the user can act on.
    /// </remarks>
    private async Task LoadProfileAsync(string path)
    {
        int generation = ++_loadGeneration;
        IsBusy = true;
        try
        {
            (SaveProfile? profile, string info, string? error) = await Task.Run<(SaveProfile?, string, string?)>(() =>
            {
                try
                {
                    // Stat before reading so the reported size matches the bytes we parsed.
                    var fileInfo = new FileInfo(path);
                    string details = $"{fileInfo.Length:N0} bytes · saved {fileInfo.LastWriteTime:g}";
                    return (SaveProfile.Load(path), details, null);
                }
                catch (Exception ex)
                {
                    // A viewer must survive anything on disk, including files that are not
                    // saves at all, so this deliberately does not filter by exception type.
                    return (null, string.Empty, ex.Message);
                }
            }).ConfigureAwait(true);

            // A newer load started while this one was running; its result wins.
            if (generation != _loadGeneration)
            {
                return;
            }

            _profile = profile;
            FileInfoText = info;
            StatusMessage = error is null ? null : $"Could not read {Path.GetFileName(path)}: {error}";

            RebuildCategories();
            RefreshSnapshot();
            RefreshItems();
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsBusy = false;
            }
        }
    }

    private void SetProfile(SaveProfile? profile)
    {
        _profile = profile;
        FileInfoText = string.Empty;
        RebuildCategories();
        RefreshSnapshot();
        RefreshItems();
    }

    /// <summary>
    /// Coalesces keystrokes so the card grid rebuilds once when typing pauses rather than on
    /// every character. The grid is not virtualized, so a rebuild realizes every card image.
    /// </summary>
    private async void DebounceRefreshItems()
    {
        var previous = _searchDebounce;
        var cts = new CancellationTokenSource();
        _searchDebounce = cts;
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(SearchDebounce, cts.Token).ConfigureAwait(true);
            RefreshItems();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later keystroke.
        }
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
                // Statue-less entries are not Divinities; see BuildGodCards.
                var divinities = TagDatabase.Gods.Where(g => g.HasStatue).ToList();
                int touched = divinities.Count(g =>
                    ownedByCategory.GetValueOrDefault("Prayer")?.Contains($"Progression.Prayer.{g.Key}") == true
                    || ownedByCategory.GetValueOrDefault("KillsFor")?.Contains($"Progression.KillsFor.{g.Key}") == true);
                Categories.Add(new CategoryItem(DivinitiesKey, "Divinities", touched, divinities.Count));
                continue;
            }

            if (key == QuestsKey)
            {
                // "41" was a count of raw tags, which is not a thing anyone tracks. Questlines
                // are, and the game's definitions give a real total including lines never begun.
                var lines = Core.GameData.QuestLines.Build(_profile);
                Categories.Add(new CategoryItem(
                    QuestsKey,
                    Display.CategoryDisplay(QuestsKey),
                    lines.Count(l => l.IsComplete),
                    lines.Count));
                continue;
            }

            var owned = ownedByCategory.GetValueOrDefault(key) ?? [];
            int? total = null;

            // A completion bar only makes sense where there is something to complete. Aspects and
            // Weapons are unlocked once and stay unlocked, so "12 of 14" is a real target.
            // Blessings and the rest are offered per run and their counters are usage counts, so
            // a bar there would invent a goal the game does not have.
            if (TagSemantics.HasCompletionTotal(key) && DbByCategory.TryGetValue(key, out var known))
            {
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
        HasRun = snapshot?.HasRun == true;
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
        QuestLines.Clear();
        SelectedItem = null;
        IsQuestView = false;
        if (_profile is null)
        {
            return;
        }

        string search = _searchText.Trim();
        bool searching = search.Length > 0;

        if (!searching && _selectedCategory?.Key == QuestsKey)
        {
            IsQuestView = true;
            foreach (var line in Core.GameData.QuestLines.Build(_profile))
            {
                QuestLines.Add(new QuestLineCard(line));
            }
            return;
        }

        if (!searching && _selectedCategory?.Key == DivinitiesKey)
        {
            foreach (var god in BuildGodCards())
            {
                Items.Add(god);
            }
            return;
        }

        var ownedValues = _profile.Records.ToDictionary(r => r.FullTag, r => r.Value, StringComparer.Ordinal);

        // Search spans the categories this profile actually has, not a fixed list: a game
        // update adding a category, or anything landing in TagRecord's "Other" bucket, still
        // gets a sidebar entry and must be reachable from search too.
        IEnumerable<string> categories = searching
            ? Categories.Select(c => c.Key).Where(k => k != DivinitiesKey)
            : _selectedCategory is null ? [] : [_selectedCategory.Key];

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

        // Only gods with a statue are Divinities you can actually devote to. Sael has a
        // Prayer tag and a KillsFor tag but no statue, no lore and no prayer prompt, and grants
        // no blessings — it is an NPC with a questline, not one of the seven.
        foreach (var god in TagDatabase.Gods.Where(g => g.HasStatue))
        {
            yield return new GodCard(god, IconStore.Get(god.SymbolKey),
                values.GetValueOrDefault($"Progression.Prayer.{god.Key}"),
                values.GetValueOrDefault($"Progression.KillsFor.{god.Key}"));
        }
    }
}
