namespace DdoItemTracker.Core.Catalog;

/// <summary>One enchantment line. Toggles (upstream type "Bool") have no bonus type or value.</summary>
public sealed record Effect(string Name, string? BonusType, string? Value, bool IsToggle);

public sealed record SetTier(int PiecesRequired, IReadOnlyList<Effect> Effects);

public sealed record CatalogSet(string Name, IReadOnlyList<SetTier> Tiers);

public sealed record CatalogItem
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int MinLevel { get; init; }
    public required string Slot { get; init; }
    public string? Type { get; init; }
    public string? Pack { get; init; }
    public IReadOnlyList<string> Quests { get; init; } = [];
    public IReadOnlyList<Effect> Effects { get; init; } = [];
    public IReadOnlyList<string> CraftingSlots { get; init; } = [];
    public IReadOnlyList<string> SetNames { get; init; } = [];
    public bool IsRare { get; init; }
    public bool IsArtifact { get; init; }
    public string? WikiUrl { get; init; }
}

public sealed record CatalogVersion(string UpstreamCommit, DateTimeOffset UpstreamCommitDateUtc, DateTimeOffset BuiltUtc);

public sealed record ItemCatalog(CatalogVersion Version, IReadOnlyList<CatalogItem> Items, IReadOnlyList<CatalogSet> Sets);
