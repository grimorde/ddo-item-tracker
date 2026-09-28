using DdoItemTracker.Views;

namespace DdoItemTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ItemDetailPage), typeof(ItemDetailPage));
        Routing.RegisterRoute(nameof(CopyEditorPage), typeof(CopyEditorPage));
        Routing.RegisterRoute(nameof(CharactersPage), typeof(CharactersPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}
