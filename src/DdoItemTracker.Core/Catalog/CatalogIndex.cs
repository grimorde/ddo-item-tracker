namespace DdoItemTracker.Core.Catalog;

/// <summary>Fast lookups over a loaded catalog.</summary>
public sealed class CatalogIndex
{
    private readonly Dictionary<string, CatalogItem> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogSet> _sets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<CatalogItem>> _members;

    public CatalogIndex(ItemCatalog catalog)
    {
        Catalog = catalog;
        foreach (var item in catalog.Items) _byKey.TryAdd(item.Key, item);
        foreach (var set in catalog.Sets) _sets.TryAdd(set.Name, set);

        _members = catalog.Items
            .SelectMany(i => i.SetNames.Select(n => (Set: n, Item: i)))
            .GroupBy(p => p.Set, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CatalogItem>)g.Select(p => p.Item)
                    .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.MinLevel)
                    .ThenBy(i => i.Slot, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        Slots = DistinctSorted(catalog.Items.Select(i => i.Slot));
        Types = DistinctSorted(catalog.Items.Select(i => i.Type));
        Packs = DistinctSorted(catalog.Items.Select(i => i.Pack));
        Quests = DistinctSorted(catalog.Items.SelectMany(i => i.Quests));
    }

    public ItemCatalog Catalog { get; }
    public IReadOnlyList<string> Slots { get; }
    public IReadOnlyList<string> Types { get; }
    public IReadOnlyList<string> Packs { get; }
    public IReadOnlyList<string> Quests { get; }

    public CatalogItem? Find(string key) => _byKey.GetValueOrDefault(key);

    public CatalogSet? FindSet(string name) => _sets.GetValueOrDefault(name);

    public IReadOnlyList<CatalogItem> SetMembers(string setName) =>
        _members.TryGetValue(setName, out var members) ? members : [];

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
