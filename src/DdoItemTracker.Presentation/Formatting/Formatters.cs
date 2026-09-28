using System.Globalization;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Presentation.Formatting;

public static class StorageNames
{
    public const string NotRecorded = "Not recorded";

    public static IReadOnlyList<string> All { get; } = Enum.GetValues<StorageType>().Select(Display).ToList();

    /// <summary>The choices for a character's storage on the copy form.</summary>
    public static IReadOnlyList<string> CharacterStorage { get; } = [NotRecorded, Display(StorageType.Inventory), Display(StorageType.Bank)];

    public static string Display(StorageType storage) => storage == StorageType.SharedBank ? "Shared Bank" : storage.ToString();

    public static string Display(StorageType? storage) => storage is { } value ? Display(value) : NotRecorded;

    public static StorageType? Parse(string? display)
    {
        foreach (var storage in Enum.GetValues<StorageType>())
            if (Display(storage) == display) return storage;
        return null;
    }
}

public static class EffectFormatter
{
    /// <summary>"Magical Sheltering +10 (Insight)". A zero value is left out; toggles show their name only.</summary>
    public static string Format(Effect effect)
    {
        if (effect.IsToggle) return effect.Name;
        var parts = new List<string> { effect.Name };
        if (FormatValue(effect.Value) is { } value) parts.Add(value);
        if (effect.BonusType is not null) parts.Add($"({effect.BonusType})");
        return string.Join(' ', parts);
    }

    private static string? FormatValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return value;
        if (number == 0) return null;
        return value.StartsWith('+') || value.StartsWith('-') ? value : "+" + value;
    }
}

public static class LocationFormatter
{
    public const string Separator = " · ";
    public const string NotRecordedText = "Location not recorded";

    public static string Format(OwnedCopy copy, IReadOnlyList<Character> characters)
    {
        var holder = copy.CharacterId is not null
            ? characters.FirstOrDefault(c => c.Id == copy.CharacterId)?.Name ?? "(unknown character)"
            : copy.Storage == StorageType.SharedBank ? StorageNames.Display(StorageType.SharedBank) : null;
        return Build(copy, holder);
    }

    public static string GroupTitle(OwnedCopyRow row) => Build(row.Copy, row.HolderName);

    private static string Build(OwnedCopy copy, string? holder)
    {
        if (copy.Server is null) return NotRecordedText;
        if (holder is null) return copy.Server;
        return copy.CharacterId is not null && copy.Storage is { } storage
            ? string.Join(Separator, copy.Server, holder, StorageNames.Display(storage))
            : copy.Server + Separator + holder;
    }
}
