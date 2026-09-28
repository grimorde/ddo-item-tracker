using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Formatting;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

public sealed record CharacterOption(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record MyItemRow(string CopyId, string ItemKey, string ItemName, string Subtitle, string? Note, bool IsInCatalog)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}

public sealed class CopyGroup(string title, IEnumerable<MyItemRow> rows) : List<MyItemRow>(rows)
{
    public string Title { get; } = title;
}

public partial class MyItemsViewModel : SessionViewModel
{
    public const string NotInCatalogSubtitle = "Not in the current catalog";
    public static readonly CharacterOption AnyCharacter = new(string.Empty, ItemFilterPanel.Any);

    private readonly INavigator _navigator;
    private bool _rebuilding;

    [ObservableProperty] private string? _selectedServer = ItemFilterPanel.Any;
    [ObservableProperty] private IReadOnlyList<CharacterOption> _characterOptions = [AnyCharacter];
    [ObservableProperty] private CharacterOption? _selectedCharacter = AnyCharacter;
    [ObservableProperty] private string? _selectedStorage = ItemFilterPanel.Any;
    [ObservableProperty] private bool _notInCatalogOnly;
    [ObservableProperty] private IReadOnlyList<CopyGroup> _groups = [];
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    public MyItemsViewModel(TrackerSession session, INavigator navigator, IDialogService dialogs) : base(session, dialogs)
    {
        _navigator = navigator;
        Filters = new ItemFilterPanel(session.Catalog);
        Filters.FilterChanged += (_, _) => Refresh();
    }

    public ItemFilterPanel Filters { get; }
    public IReadOnlyList<string> ServerOptions { get; } = [ItemFilterPanel.Any, StorageNames.NotRecorded, .. Servers.All];
    public IReadOnlyList<string> StorageOptions { get; } = [ItemFilterPanel.Any, StorageNames.NotRecorded, .. StorageNames.All];

    public override void Refresh()
    {
        if (_rebuilding) return;
        RebuildCharacterOptions(); // picks up characters added or renamed in Settings
        var data = Session.Data;
        var filter = new CopyFilter
        {
            Item = Filters.BuildFilter() with { Ownership = OwnershipFilter.All, OwnershipServer = null },
            NoServer = SelectedServer == StorageNames.NotRecorded,
            Server = Servers.Canonical(SelectedServer),
            CharacterId = string.IsNullOrEmpty(SelectedCharacter?.Id) ? null : SelectedCharacter.Id,
            NoStorage = SelectedStorage == StorageNames.NotRecorded,
            Storage = StorageNames.Parse(SelectedStorage),
            NotInCatalogOnly = NotInCatalogOnly,
        };
        var rows = CopyQuery.Apply(data, Session.Catalog, filter);
        Groups = rows
            .GroupBy(LocationFormatter.GroupTitle)
            .Select(g => new CopyGroup(g.Key, g.Select(r => new MyItemRow(
                r.Copy.Id,
                r.Copy.ItemKey,
                r.ItemName,
                r.Item is null ? NotInCatalogSubtitle : CatalogViewModel.Subtitle(r.Item),
                r.Copy.Note,
                r.IsInCatalog))))
            .ToList();

        IsEmpty = data.OwnedCopies.Count == 0;
        var (owned, total) = CopyQuery.Summary(data, Session.Catalog);
        Summary = $"{owned:N0} of {total:N0} items owned";
    }

    partial void OnSelectedServerChanged(string? value) => Refresh();

    partial void OnSelectedCharacterChanged(CharacterOption? value) => Refresh();

    partial void OnSelectedStorageChanged(string? value) => Refresh();

    partial void OnNotInCatalogOnlyChanged(bool value) => Refresh();

    private void RebuildCharacterOptions()
    {
        _rebuilding = true;
        try
        {
            var server = Servers.Canonical(SelectedServer);
            var characters = SelectedServer == StorageNames.NotRecorded
                ? new List<CharacterOption>()
                : Session.Data.Characters
                    .Where(c => server is null || c.Server == server)
                    .OrderBy(c => c.Server, StringComparer.Ordinal)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new CharacterOption(c.Id, server is null ? $"{c.Name} ({c.Server})" : c.Name))
                    .ToList();
            IReadOnlyList<CharacterOption> options = [AnyCharacter, .. characters];
            // Read the selection before replacing the list: a bound Picker re-selects whatever sits at its old
            // index when its items change, which would otherwise silently switch to a different character.
            var keepId = SelectedCharacter?.Id;
            // Only replace the list when it really changed, so a bound Picker is not reset on every refresh.
            if (!options.SequenceEqual(CharacterOptions)) CharacterOptions = options;
            SelectedCharacter = CharacterOptions.FirstOrDefault(c => c.Id == keepId) ?? AnyCharacter;
        }
        finally
        {
            _rebuilding = false;
        }
    }

    [RelayCommand]
    private Task OpenCopy(MyItemRow row) => _navigator.OpenItemAsync(row.ItemKey);
}
