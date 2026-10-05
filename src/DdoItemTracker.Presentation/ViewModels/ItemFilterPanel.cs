using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Presentation.ViewModels;

/// <summary>The item filters shared by the Catalog and My Items screens.</summary>
public partial class ItemFilterPanel : ObservableObject
{
    public const string Any = "Any";
    private const string AllOption = "All";
    private const string OwnedOption = "Owned";
    private const string NotOwnedOption = "Not owned";

    private static readonly string[] OptionListNames = [nameof(Slots), nameof(Types), nameof(Packs), nameof(Quests)];

    private CatalogIndex _catalog;

    public ItemFilterPanel(CatalogIndex catalog)
    {
        _catalog = catalog;
        _slots = [Any, .. catalog.Slots];
        _types = [Any, .. catalog.Types];
        _packs = [Any, .. catalog.Packs];
        _quests = [Any, .. catalog.Quests];
    }

    [ObservableProperty] private IReadOnlyList<string> _slots;
    [ObservableProperty] private IReadOnlyList<string> _types;
    [ObservableProperty] private IReadOnlyList<string> _packs;
    [ObservableProperty] private IReadOnlyList<string> _quests;
    public IReadOnlyList<string> OwnershipOptions { get; } = [AllOption, OwnedOption, NotOwnedOption];
    public IReadOnlyList<string> OwnershipServers { get; } = [Any, .. Servers.All];

    [ObservableProperty] private string? _searchText = string.Empty;
    [ObservableProperty] private string? _minLevelText = string.Empty;
    [ObservableProperty] private string? _maxLevelText = string.Empty;
    [ObservableProperty] private string? _selectedSlot = Any;
    [ObservableProperty] private string? _selectedType = Any;
    [ObservableProperty] private string? _selectedPack = Any;
    [ObservableProperty] private string? _selectedQuest = Any;
    [ObservableProperty] private bool _inSetOnly;
    [ObservableProperty] private bool _artifactOnly;
    [ObservableProperty] private string? _selectedOwnership = AllOption;
    [ObservableProperty] private string? _selectedOwnershipServer = Any;

    public event EventHandler? FilterChanged;

    public ItemFilter BuildFilter() => new()
    {
        Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
        MinLevelFrom = ParseLevel(MinLevelText),
        MinLevelTo = ParseLevel(MaxLevelText),
        Slot = AnyToNull(SelectedSlot),
        Type = AnyToNull(SelectedType),
        Pack = AnyToNull(SelectedPack),
        Quest = AnyToNull(SelectedQuest),
        InSetOnly = InSetOnly,
        ArtifactOnly = ArtifactOnly,
        Ownership = SelectedOwnership switch
        {
            OwnedOption => OwnershipFilter.Owned,
            NotOwnedOption => OwnershipFilter.NotOwned,
            _ => OwnershipFilter.All,
        },
        OwnershipServer = AnyToNull(SelectedOwnershipServer),
    };

    /// <summary>
    /// Rebuilds the option lists after the catalog is replaced. A chosen option the new catalog no longer has
    /// goes back to <see cref="Any"/>; the others are kept.
    /// </summary>
    public void UseCatalog(CatalogIndex catalog)
    {
        if (ReferenceEquals(catalog, _catalog)) return;
        _catalog = catalog;
        Slots = [Any, .. catalog.Slots];
        Types = [Any, .. catalog.Types];
        Packs = [Any, .. catalog.Packs];
        Quests = [Any, .. catalog.Quests];
        if (!Slots.Contains(SelectedSlot)) SelectedSlot = Any;
        if (!Types.Contains(SelectedType)) SelectedType = Any;
        if (!Packs.Contains(SelectedPack)) SelectedPack = Any;
        if (!Quests.Contains(SelectedQuest)) SelectedQuest = Any;
    }

    [RelayCommand]
    private void Clear()
    {
        SearchText = string.Empty;
        MinLevelText = string.Empty;
        MaxLevelText = string.Empty;
        SelectedSlot = Any;
        SelectedType = Any;
        SelectedPack = Any;
        SelectedQuest = Any;
        InSetOnly = false;
        ArtifactOnly = false;
        SelectedOwnership = AllOption;
        SelectedOwnershipServer = Any;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!OptionListNames.Contains(e.PropertyName)) FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? AnyToNull(string? value) => string.IsNullOrEmpty(value) || value == Any ? null : value;

    private static int? ParseLevel(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? level : null;
}
