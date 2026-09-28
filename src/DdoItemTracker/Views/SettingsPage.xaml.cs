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
        ShowSelectedTheme();
        VersionLabel.Text = $"DDO Item Tracker {AppInfo.Current.VersionString}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Activate();
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        ShowSelectedTheme();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Deactivate();
        Application.Current!.RequestedThemeChanged -= OnRequestedThemeChanged;
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => ShowSelectedTheme();

    private void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        var theme = int.Parse((string)e.Parameter!);
        if (theme == SettingsHelper.Theme) return;
        SettingsHelper.Theme = theme;
        ThemeService.SetTheme();
        ShowSelectedTheme();
    }

    // Colours match the appearance selector in DDO Life Tracker.
    private void ShowSelectedTheme()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var options = new[]
        {
            (SystemOption, SystemIcon, SystemLabel),
            (LightOption, LightIcon, LightLabel),
            (DarkOption, DarkIcon, DarkLabel),
        };

        for (var i = 0; i < options.Length; i++)
        {
            var (border, icon, label) = options[i];
            var selected = i == SettingsHelper.Theme;
            var background = Color.FromArgb(selected ? "#106881" : dark ? "#2B2B2B" : "#F3F2F1");
            border.BackgroundColor = background;
            border.Stroke = background;
            icon.Color = Color.FromArgb(selected ? "#FFFFFF" : dark ? "#F3F3F3" : "#323130");
            label.TextColor = Color.FromArgb(selected || dark ? "#FFFFFF" : "#323130");
        }
    }

    private async void OnCharactersClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(CharactersPage));

    private async void OnWhatsNewClicked(object? sender, EventArgs e) => await Navigation.PushModalAsync(new WhatsNewPage());
}
