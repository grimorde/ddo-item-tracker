using DdoItemTracker.Services;

namespace DdoItemTracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.SetTheme();
        RequestedThemeChanged += (_, _) => ThemeService.SetTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell()) { Title = "DDO Item Tracker" };
}
