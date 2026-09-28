using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Filtering;

public sealed record CopyFilter
{
    public ItemFilter Item { get; init; } = new();
    public string? Server { get; init; }
    /// <summary>Only copies with no server recorded.</summary>
    public bool NoServer { get; init; }
    public string? CharacterId { get; init; }
    public StorageType? Storage { get; init; }
    /// <summary>Only copies with no storage recorded.</summary>
    public bool NoStorage { get; init; }
    public bool NotInCatalogOnly { get; init; }
}

/// <param name="HolderName">The character's name, "Shared Bank", or null when neither is recorded.</param>
public sealed record OwnedCopyRow(OwnedCopy Copy, CatalogItem? Item, string ItemName, string? HolderName)
{
    public bool IsInCatalog => Item is not null;
}

/// <summary>The My Items view: owned copies with their catalog item and holder, in location order.</summary>
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
            if (filter.NoServer && copy.Server is not null) continue;
            if (server is not null && copy.Server != server) continue;
            if (filter.CharacterId is not null && copy.CharacterId != filter.CharacterId) continue;
            if (filter.NoStorage && copy.Storage is not null) continue;
            if (filter.Storage is { } storage && copy.Storage != storage) continue;

            var item = index.Find(copy.ItemKey);
            if (filter.NotInCatalogOnly && item is not null) continue;
            if (item is not null ? !ItemQuery.MatchesItem(item, filter.Item) : !MatchesMissingItem(copy, filter.Item)) continue;

            var holder = copy.CharacterId is not null
                ? names.GetValueOrDefault(copy.CharacterId, "(unknown character)")
                : copy.Storage == StorageType.SharedBank ? SharedBankHolder : null;
            rows.Add(new OwnedCopyRow(copy, item, item?.Name ?? copy.ItemName, holder));
        }

        return rows
            .OrderBy(r => r.Copy.Server is null ? 0 : 1)
            .ThenBy(r => r.Copy.Server, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => LevelRank(r.Copy))
            .ThenBy(r => r.HolderName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Copy.Storage is null ? -1 : (int)r.Copy.Storage)
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

    // Server only, then Shared Bank, then characters.
    private static int LevelRank(OwnedCopy copy) =>
        copy.CharacterId is not null ? 2 : copy.Storage == StorageType.SharedBank ? 1 : 0;

    // Only the search box can apply to an item the catalog no longer has; any other item filter excludes it.
    private static bool MatchesMissingItem(OwnedCopy copy, ItemFilter filter) =>
        !filter.HasItemFieldFilters
        && (string.IsNullOrWhiteSpace(filter.Search)
            || copy.ItemName.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase));
}
