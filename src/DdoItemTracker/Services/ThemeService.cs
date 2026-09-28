using DdoItemTracker.Helpers;
using DdoItemTracker.Resources.Styles;

namespace DdoItemTracker.Services;

public static class ThemeService
{
    public static void SetTheme()
    {
        var app = Application.Current;
        if (app?.Resources?.MergedDictionaries is not { } dictionaries) return;

        foreach (var existing in dictionaries.Where(d => d is LightTheme or DarkTheme).ToList())
            dictionaries.Remove(existing);

        var theme = SettingsHelper.Theme;
        app.UserAppTheme = theme switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
        var dark = theme == 2 || (theme == 0 && app.RequestedTheme == AppTheme.Dark);
        dictionaries.Add(dark ? new DarkTheme() : new LightTheme());
    }
}
