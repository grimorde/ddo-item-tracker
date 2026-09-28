using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Filtering;

public enum OwnershipFilter
{
    All,
    Owned,
    NotOwned,
}

public sealed record ItemFilter
{
    public string? Search { get; init; }
    public int? MinLevelFrom { get; init; }
    public int? MinLevelTo { get; init; }
    public string? Slot { get; init; }
    public string? Type { get; init; }
    public string? Pack { get; init; }
    public string? Quest { get; init; }
    public bool InSetOnly { get; init; }
    public bool ArtifactOnly { get; init; }
    public OwnershipFilter Ownership { get; init; } = OwnershipFilter.All;
    /// <summary>When set, Owned / Not owned look only at copies on this server.</summary>
    public string? OwnershipServer { get; init; }

    /// <summary>True when a filter other than the search box needs catalog fields.</summary>
    public bool HasItemFieldFilters =>
        MinLevelFrom is not null || MinLevelTo is not null || Slot is not null || Type is not null
        || Pack is not null || Quest is not null || InSetOnly || ArtifactOnly;
}

public static class ItemQuery
{
    public static IReadOnlyList<CatalogItem> Apply(IEnumerable<CatalogItem> items, ItemFilter filter, TrackerData data)
    {
        var owned = filter.Ownership == OwnershipFilter.All
            ? null
            : OwnedCounts(data, filter.OwnershipServer);
        return items.Where(i => MatchesItem(i, filter) && MatchesOwnership(i, filter.Ownership, owned)).ToList();
    }

    public static bool MatchesItem(CatalogItem item, ItemFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search)
            && !item.Name.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (filter.MinLevelFrom is int lo && item.MinLevel < lo) return false;
        if (filter.MinLevelTo is int hi && item.MinLevel > hi) return false;
        if (filter.Slot is not null && !Same(item.Slot, filter.Slot)) return false;
        if (filter.Type is not null && !Same(item.Type, filter.Type)) return false;
        if (filter.Pack is not null && !Same(item.Pack, filter.Pack)) return false;
        if (filter.Quest is not null && !item.Quests.Any(q => Same(q, filter.Quest))) return false;
        if (filter.InSetOnly && item.SetNames.Count == 0) return false;
        if (filter.ArtifactOnly && !item.IsArtifact) return false;
        return true;
    }

    public static IReadOnlyDictionary<string, int> OwnedCounts(TrackerData data, string? server = null)
    {
        var canonical = server is null ? null : Servers.Canonical(server) ?? server;
        return data.OwnedCopies
            .Where(c => canonical is null || c.Server == canonical)
            .GroupBy(c => c.ItemKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    private static bool MatchesOwnership(CatalogItem item, OwnershipFilter ownership, IReadOnlyDictionary<string, int>? owned) =>
        ownership switch
        {
            OwnershipFilter.Owned => owned!.ContainsKey(item.Key),
            OwnershipFilter.NotOwned => !owned!.ContainsKey(item.Key),
            _ => true,
        };

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
