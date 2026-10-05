using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class MyItemsPage : ContentPage
{
    private readonly MyItemsViewModel _viewModel;
    private readonly ItemDetailViewModel _detail;

    public MyItemsPage(MyItemsViewModel viewModel, ItemDetailViewModel detail)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _detail = detail;
        DetailPane.BindingContext = detail;
        SizeChanged += (_, _) => UpdateSplit();
    }

    /// <summary>At the same width as the Catalog, the item detail shows beside the list (spec 5.7).</summary>
    private bool IsSplit => Width >= CatalogPage.SplitWidth;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Activate();
        if (_detail.ItemKey.Length > 0) _detail.Activate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Deactivate();
        _detail.Deactivate();
    }

    private void UpdateSplit()
    {
        Split.ColumnDefinitions[1].Width = IsSplit ? new GridLength(1.2, GridUnitType.Star) : new GridLength(0);
        DetailPane.IsVisible = IsSplit && _detail.ItemKey.Length > 0;
        if (!IsSplit) CopyList.ClearSelection();
    }

    private async void OnCopySelected(object? sender, MyItemRow row)
    {
        if (IsSplit)
        {
            _detail.Load(row.ItemKey);
            _detail.Activate();
            UpdateSplit();
            return;
        }
        CopyList.ClearSelection();
        await HideKeyboardAsync();
        await _viewModel.OpenCopyCommand.ExecuteAsync(row);
    }

    /// <summary>Selecting a copy does not take focus from the search box, so the keyboard would follow us to the item page.</summary>
    private async Task HideKeyboardAsync()
    {
        if (SearchEntry.IsSoftInputShowing()) await SearchEntry.HideSoftInputAsync(CancellationToken.None);
        SearchEntry.Unfocus();
    }

    private void OnFiltersClicked(object? sender, EventArgs e)
    {
        FilterArea.IsVisible = !FilterArea.IsVisible;
        FiltersButton.Text = FilterArea.IsVisible ? "Hide filters" : "Show filters";
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        ClearSearchButton.IsVisible = !string.IsNullOrEmpty(e.NewTextValue);

    /// <summary>Clears the search without focusing the box, so the keyboard stays closed.</summary>
    private void OnClearSearchClicked(object? sender, EventArgs e) => SearchEntry.Text = string.Empty;

    private async void OnSettingsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));
}
