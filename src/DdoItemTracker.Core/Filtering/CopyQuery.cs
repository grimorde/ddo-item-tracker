using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Filtering;

public sealed record CopyFilter
{
    public ItemFilter Item { get; init; } = new();
    public string? Server { get; init; }
    public string? CharacterId { get; init; }
    public StorageType? Storage { get; init; }
    public bool NotInCatalogOnly { get; init; }
}

public sealed record OwnedCopyRow(OwnedCopy Copy, CatalogItem? Item, string ItemName, string HolderName)
{
    public bool IsInCatalog => Item is not null;
}

/// <summary>The My Items view: owned copies with their catalog item and holder.</summary>
public static class CopyQuery
{
    public const string SharedBankHolder = "Shared Bank";

    public static IReadOnlyList<OwnedCopyRow> Apply(TrackerData data, CatalogIndex index, CopyFilter filter)
    {
        var names = data.Characters.ToDictionary(c => c.Id, c => c.Name);
        var server = filter.Server is null ? null : Servers.Canonical(filter.Server) ?? filter.Server;
        var rows = new List<OwnedCopyRow>();

        foreach (var copy in data.OwnedCopies)
        {
            if (server is not null && copy.Server != server) continue;
            if (filter.CharacterId is not null && copy.CharacterId != filter.CharacterId) continue;
            if (filter.Storage is { } storage && copy.Storage != storage) continue;

            var item = index.Find(copy.ItemKey);
            if (filter.NotInCatalogOnly && item is not null) continue;
            if (item is not null ? !ItemQuery.MatchesItem(item, filter.Item) : !MatchesMissingItem(copy, filter.Item)) continue;

            var holder = copy.CharacterId is null
                ? SharedBankHolder
                : names.GetValueOrDefault(copy.CharacterId, "(unknown character)");
            rows.Add(new OwnedCopyRow(copy, item, item?.Name ?? copy.ItemName, holder));
        }

        return rows
            .OrderBy(r => r.Copy.Server, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Copy.CharacterId is null ? 0 : 1)
            .ThenBy(r => r.HolderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Copy.Storage)
            .ThenBy(r => r.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static (int Owned, int Total) Summary(TrackerData data, CatalogIndex index)
    {
        var owned = data.OwnedCopies
            .Select(c => c.ItemKey)
            .Where(k => index.Find(k) is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return (owned, index.Catalog.Items.Count);
    }

    // Only the search box can apply to an item the catalog no longer has; any other item filter excludes it.
    private static bool MatchesMissingItem(OwnedCopy copy, ItemFilter filter) =>
        !filter.HasItemFieldFilters
        && (string.IsNullOrWhiteSpace(filter.Search)
            || copy.ItemName.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase));
}
