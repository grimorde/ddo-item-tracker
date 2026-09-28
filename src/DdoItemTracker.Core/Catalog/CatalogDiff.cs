namespace DdoItemTracker.Core.Catalog;

public sealed record CatalogDiff(IReadOnlyList<string> AddedKeys, IReadOnlyList<string> RemovedKeys, int OrphanedCopyCount)
{
    /// <param name="ownedItemKeys">One entry per owned copy, so two copies of a removed item count twice.</param>
    public static CatalogDiff Compute(ItemCatalog? current, ItemCatalog candidate, IEnumerable<string> ownedItemKeys)
    {
        var oldKeys = current?.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var newKeys = candidate.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);

        var added = newKeys.Where(k => !oldKeys.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var removed = oldKeys.Where(k => !newKeys.Contains(k)).Order(StringComparer.Ordinal).ToList();
        var orphaned = ownedItemKeys.Count(k => !newKeys.Contains(k));
        return new CatalogDiff(added, removed, orphaned);
    }
}
