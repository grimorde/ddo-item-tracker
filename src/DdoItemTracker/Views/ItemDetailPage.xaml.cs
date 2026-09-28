using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class ItemDetailPage : ContentPage, IQueryAttributable
{
    private readonly ItemDetailViewModel _viewModel;

    public ItemDetailPage(ItemDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Detail.BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("key", out var value) && value is string key)
        {
            _viewModel.Load(key);
            Title = _viewModel.Title;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Activate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Deactivate();
    }
}
