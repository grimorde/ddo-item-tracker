using DdoItemTracker.Helpers;
using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class CatalogPage : ContentPage
{
    /// <summary>At this width and above (Windows, tablets) the item detail shows beside the list.</summary>
    public const double SplitWidth = 900;

    private readonly CatalogViewModel _viewModel;
    private readonly ItemDetailViewModel _detail;

    public CatalogPage(CatalogViewModel viewModel, ItemDetailViewModel detail)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _detail = detail;
        DetailPane.BindingContext = detail;
        SizeChanged += (_, _) => UpdateSplit();
    }

    private bool IsSplit => Width >= SplitWidth;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
        if (_detail.ItemKey.Length > 0) _detail.Activate();
        await WhatsNewHelper.ShowIfNeededAsync(Navigation);
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
    }

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ItemRow row) return;
        if (IsSplit)
        {
            _detail.Load(row.Key);
            _detail.Activate();
            UpdateSplit();
            return;
        }
        ItemList.SelectedItem = null;
        await _viewModel.OpenItemCommand.ExecuteAsync(row);
    }

    private async void OnSettingsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(SettingsPage));

    private void OnFiltersClicked(object? sender, EventArgs e)
    {
        FilterPanel.IsVisible = !FilterPanel.IsVisible;
        FiltersButton.Text = FilterPanel.IsVisible ? "Hide filters" : "Show filters";
    }
}
