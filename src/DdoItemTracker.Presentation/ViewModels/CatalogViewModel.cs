using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Presentation.Formatting;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

public sealed record ItemRow(string Key, string Name, string Subtitle, int OwnedCount)
{
    public string Badge => OwnedCount > 0 ? $"×{OwnedCount}" : string.Empty;
    public bool IsOwned => OwnedCount > 0;
}

public partial class CatalogViewModel : SessionViewModel
{
    private readonly INavigator _navigator;

    [ObservableProperty] private ObservableCollection<ItemRow> _results = [];
    [ObservableProperty] private string _summary = string.Empty;

    public CatalogViewModel(TrackerSession session, INavigator navigator, IDialogService dialogs) : base(session, dialogs)
    {
        _navigator = navigator;
        Filters = new ItemFilterPanel(session.Catalog);
        Filters.FilterChanged += (_, _) => Refresh();
    }

    public ItemFilterPanel Filters { get; }

    public static string Subtitle(CatalogItem item) =>
        string.Join(LocationFormatter.Separator, new[] { $"ML {item.MinLevel}", item.Slot, item.Pack }.Where(s => s is not null));

    public override void Refresh()
    {
        var all = Session.Catalog.Catalog.Items;
        var matches = ItemQuery.Apply(all, Filters.BuildFilter(), Session.Data);
        var counts = ItemQuery.OwnedCounts(Session.Data);
        var rows = matches.Select(i => new ItemRow(i.Key, i.Name, Subtitle(i), counts.GetValueOrDefault(i.Key))).ToList();
        UpdateResults(rows);
        Summary = matches.Count == all.Count ? $"{all.Count:N0} items" : $"{matches.Count:N0} of {all.Count:N0} items";
    }

    /// <summary>
    /// When the same items are listed (only owned counts changed, or nothing did), rows are replaced in place
    /// so the list keeps its scroll position. A different set of items replaces the whole list.
    /// </summary>
    private void UpdateResults(List<ItemRow> rows)
    {
        if (rows.Count != Results.Count || !rows.Select(r => r.Key).SequenceEqual(Results.Select(r => r.Key)))
        {
            Results = new ObservableCollection<ItemRow>(rows);
            return;
        }
        for (var i = 0; i < rows.Count; i++)
            if (!Results[i].Equals(rows[i])) Results[i] = rows[i];
    }

    /// <summary>Call from the page's OnAppearing. Shows the startup data message once, if there is one.</summary>
    public async Task OnAppearingAsync()
    {
        Activate();
        if (Session.TakeStartupMessage() is { } message) await Dialogs.AlertAsync("Your item list", message);
    }

    [RelayCommand]
    private Task OpenItem(ItemRow row) => _navigator.OpenItemAsync(row.Key);
}
