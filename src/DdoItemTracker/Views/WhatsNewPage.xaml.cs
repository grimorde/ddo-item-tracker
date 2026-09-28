using DdoItemTracker.Helpers;

namespace DdoItemTracker.Views;

public partial class WhatsNewPage : ContentPage
{
    public WhatsNewPage()
    {
        InitializeComponent();
        HeaderLabel.Text = $"What's new in version {AppInfo.Current.VersionString}";
    }

    private async void OnDismissClicked(object? sender, EventArgs e)
    {
        SettingsHelper.LastSeenVersion = AppInfo.Current.VersionString;
        await Navigation.PopModalAsync();
    }
}
