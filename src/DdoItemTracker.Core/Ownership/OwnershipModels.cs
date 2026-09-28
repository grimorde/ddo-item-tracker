namespace DdoItemTracker.Core.Ownership;

/// <summary>Where a copy is kept. Null on a copy means "not recorded". Inventory includes equipped gear.</summary>
public enum StorageType
{
    SharedBank,
    Inventory,
    Bank,
}

public sealed class Character
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Server { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LifeTrackerId { get; set; }
}

/// <summary>
/// One owned copy of an item. Every part of the location is optional (spec 4.2): nothing, a server,
/// a server's Shared Bank, or a character on a server with an optional Inventory or Bank.
/// </summary>
public sealed class OwnedCopy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ItemKey { get; set; } = string.Empty;
    /// <summary>Name at the time the copy was recorded, shown if the item leaves the catalog.</summary>
    public string ItemName { get; set; } = string.Empty;
    public string? Server { get; set; }
    public string? CharacterId { get; set; }
    public StorageType? Storage { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset AddedUtc { get; set; }
}

public sealed class TrackerData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Character> Characters { get; set; } = [];
    public List<OwnedCopy> OwnedCopies { get; set; } = [];
}
