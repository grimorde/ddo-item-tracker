using System.Globalization;

namespace DdoItemTracker.Core.Catalog;

/// <summary>An item's identity across catalog versions: name, minimum level and slot.</summary>
public static class ItemKey
{
    public static string For(string name, int minLevel, string slot) =>
        $"{name}|{minLevel.ToString(CultureInfo.InvariantCulture)}|{slot}";
}
