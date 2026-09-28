using DdoItemTracker.Helpers;
using DdoItemTracker.Presentation.ViewModels;
using DdoItemTracker.Services;

namespace DdoItemTracker.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        ThemePicker.SelectedIndex = SettingsHelper.Theme;
        VersionLabel.Text = $"DDO Item Tracker {AppInfo.Current.VersionString}";
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

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (ThemePicker.SelectedIndex < 0 || ThemePicker.SelectedIndex == SettingsHelper.Theme) return;
        SettingsHelper.Theme = ThemePicker.SelectedIndex;
        ThemeService.SetTheme();
    }

    private async void OnCharactersClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(CharactersPage));

    private async void OnWhatsNewClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new WhatsNewPage());
}
