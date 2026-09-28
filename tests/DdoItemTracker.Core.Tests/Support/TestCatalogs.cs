using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Support;

internal static class TestCatalogs
{
    public static CatalogItem Item(
        string name,
        int minLevel = 1,
        string slot = "Ring",
        string[]? sets = null,
        string? type = null,
        string? pack = null,
        string[]? quests = null,
        bool artifact = false) => new()
    {
        Key = ItemKey.For(name, minLevel, slot),
        Name = name,
        MinLevel = minLevel,
        Slot = slot,
        Type = type,
        Pack = pack,
        Quests = quests ?? [],
        SetNames = sets ?? [],
        IsArtifact = artifact,
    };

    public static ItemCatalog Catalog(IEnumerable<CatalogItem> items, IEnumerable<CatalogSet>? sets = null) => new(
        new CatalogVersion("test", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
        items.ToList(),
        sets?.ToList() ?? new List<CatalogSet> { new("Test Set", []) });

    public static ItemCatalog WithItemCount(int count) =>
        Catalog(Enumerable.Range(1, count).Select(n => Item($"Item {n}")));

    public static ConversionReport CleanReport(int input) => new(input, 0, 0, [], []);
}
