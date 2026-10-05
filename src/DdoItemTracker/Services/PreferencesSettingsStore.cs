using System.Globalization;
using System.Runtime.CompilerServices;
using DdoItemTracker.Presentation.Services;

namespace DdoItemTracker.Services;

public sealed class PreferencesSettingsStore : ISettingsStore
{
    public string? LastServer { get => Get(); set => Set(value); }
    public string? LastHeldIn { get => Get(); set => Set(value); }
    public string? LastStorage { get => Get(); set => Set(value); }

    public DateTimeOffset? LastCatalogCheckUtc
    {
        get => DateTimeOffset.TryParse(Get(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
        set => Set(value?.ToString("o", CultureInfo.InvariantCulture));
    }

    private static string? Get([CallerMemberName] string key = "") => Preferences.Get(key, null);

    private static void Set(string? value, [CallerMemberName] string key = "")
    {
        if (value is null) Preferences.Remove(key);
        else Preferences.Set(key, value);
    }
}
