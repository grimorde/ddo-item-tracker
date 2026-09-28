using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Views;

public partial class CharactersPage : ContentPage
{
    private readonly CharactersViewModel _viewModel;

    public CharactersPage(CharactersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
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

    private async void OnMoreClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: CharacterRow row }) return;
        const string rename = "Rename", delete = "Delete";
        switch (await DisplayActionSheetAsync(row.Name, "Cancel", null, rename, delete))
        {
            case rename: await _viewModel.RenameCommand.ExecuteAsync(row); break;
            case delete: await _viewModel.DeleteCommand.ExecuteAsync(row); break;
        }
    }
}
