using DdoItemTracker.Views;

namespace DdoItemTracker.Helpers;

public static class WhatsNewHelper
{
    private static bool _shownThisSession;

    public static async Task ShowIfNeededAsync(INavigation navigation)
    {
        if (_shownThisSession || SettingsHelper.LastSeenVersion == AppInfo.Current.VersionString) return;
        _shownThisSession = true;
        await navigation.PushModalAsync(new WhatsNewPage());
    }
}
