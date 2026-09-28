namespace DdoItemTracker.Helpers;

public static class SettingsHelper
{
    /// <summary>0 = follow the system, 1 = light, 2 = dark (as in DDO Life Tracker).</summary>
    public static int Theme
    {
        get => Preferences.Get(nameof(Theme), 0);
        set => Preferences.Set(nameof(Theme), value);
    }

    public static string LastSeenVersion
    {
        get => Preferences.Get(nameof(LastSeenVersion), string.Empty);
        set => Preferences.Set(nameof(LastSeenVersion), value);
    }
}
