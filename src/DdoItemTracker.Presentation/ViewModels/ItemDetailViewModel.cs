using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Formatting;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

public sealed record SetTierLine(string Pieces, string Effects);

public sealed record SetMemberRow(string Key, string Name, string Subtitle, bool IsOwned, bool IsCurrent)
{
    public string OwnedText => IsCurrent ? "This item" : IsOwned ? "Owned" : "Not owned";
}

public sealed record SetBlock(string Name, bool HasDetails, IReadOnlyList<SetTierLine> Tiers, IReadOnlyList<SetMemberRow> Members, string OwnedSummary);

public sealed record CopyRow(string Id, string Location, string? Note)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}

public partial class ItemDetailViewModel(TrackerSession session, INavigator navigator, IDialogService dialogs)
    : SessionViewModel(session, dialogs)
{
    public const string NotInCatalogText = "This item is not in the current catalog. Your copies are kept.";

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _questsText = string.Empty;
    [ObservableProperty] private bool _isInCatalog;
    [ObservableProperty] private string? _notInCatalogMessage;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasWikiUrl))] private string? _wikiUrl;
    [ObservableProperty] private IReadOnlyList<string> _effects = [];
    [ObservableProperty] private IReadOnlyList<string> _craftingSlots = [];
    [ObservableProperty] private IReadOnlyList<SetBlock> _sets = [];
    [ObservableProperty] private IReadOnlyList<CopyRow> _copies = [];
    [ObservableProperty] private string _copiesHeader = string.Empty;

    public string ItemKey { get; private set; } = string.Empty;
    public bool HasWikiUrl => WikiUrl is not null;

    public void Load(string itemKey)
    {
        ItemKey = itemKey;
        Refresh();
    }

    public override void Refresh()
    {
        if (ItemKey.Length == 0) return;
        var data = Session.Data;
        var item = Session.Catalog.Find(ItemKey);
        var copies = data.OwnedCopies.Where(c => c.ItemKey == ItemKey).ToList();

        IsInCatalog = item is not null;
        NotInCatalogMessage = item is null ? NotInCatalogText : null;
        Title = item?.Name ?? copies.FirstOrDefault()?.ItemName ?? ItemKey.Split('|')[0];
        Subtitle = item is null
            ? string.Empty
            : string.Join(LocationFormatter.Separator, new[] { $"ML {item.MinLevel}", item.Slot, item.Type, item.Pack }.Where(s => s is not null));
        QuestsText = item is null || item.Quests.Count == 0
            ? string.Empty
            : (item.Quests.Count == 1 ? "Quest: " : "Quests: ") + string.Join(", ", item.Quests);
        WikiUrl = item?.WikiUrl;
        Effects = item?.Effects.Select(EffectFormatter.Format).ToList() ?? [];
        CraftingSlots = item?.CraftingSlots ?? [];
        Sets = item?.SetNames.Select(BuildSet).ToList() ?? [];
        Copies = copies.Select(c => new CopyRow(c.Id, LocationFormatter.Format(c, data.Characters), c.Note)).ToList();
        CopiesHeader = $"Your copies ({Copies.Count})";
    }

    private SetBlock BuildSet(string setName)
    {
        var owned = Session.Data.OwnedCopies.Select(c => c.ItemKey).ToHashSet(StringComparer.Ordinal);
        var members = Session.Catalog.SetMembers(setName)
            .Select(m => new SetMemberRow(m.Key, m.Name, CatalogViewModel.Subtitle(m), owned.Contains(m.Key), m.Key == ItemKey))
            .ToList();
        var set = Session.Catalog.FindSet(setName);
        var tiers = set?.Tiers
            .Select(t => new SetTierLine($"{t.PiecesRequired} pieces", string.Join("\n", t.Effects.Select(EffectFormatter.Format))))
            .ToList() ?? [];
        var summary = $"You own {members.Count(m => m.IsOwned)} of {members.Count} pieces";
        return new SetBlock(setName, set is not null, tiers, members, summary);
    }

    /// <summary>One tap: record a copy with no location.</summary>
    [RelayCommand]
    private async Task HaveOne()
    {
        await TryApplyAsync(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = ItemKey, ItemName = Title }));
        Refresh();
    }

    [RelayCommand]
    private Task AddCopy() => navigator.OpenCopyEditorAsync(ItemKey, null);

    [RelayCommand]
    private Task EditCopy(CopyRow row) => navigator.OpenCopyEditorAsync(ItemKey, row.Id);

    [RelayCommand]
    private async Task DeleteCopy(CopyRow row)
    {
        if (!await Dialogs.ConfirmAsync("Delete copy", $"Remove this copy of {Title} from {row.Location}?", "Delete", "Cancel")) return;
        await TryApplyAsync(d => TrackerOperations.RemoveCopy(d, row.Id));
        Refresh();
    }

    [RelayCommand]
    private Task OpenMember(SetMemberRow member) =>
        member.IsCurrent ? Task.CompletedTask : navigator.OpenItemAsync(member.Key);

    [RelayCommand]
    private Task OpenWiki() => WikiUrl is null ? Task.CompletedTask : navigator.OpenUrlAsync(WikiUrl);
}
