namespace DdoItemTracker.Core.Ownership;

public enum StorageType
{
    Equipped,
    Inventory,
    Bank,
    SharedBank,
}

public sealed class Character
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Server { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FolderId { get; set; }
    public string? LifeTrackerId { get; set; }
}

public sealed class Folder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
}

public sealed class OwnedCopy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ItemKey { get; set; } = string.Empty;
    /// <summary>Name at the time the copy was recorded, shown if the item leaves the catalog.</summary>
    public string ItemName { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public StorageType Storage { get; set; }
    public string? CharacterId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset AddedUtc { get; set; }
}

public sealed class TrackerData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<Character> Characters { get; set; } = [];
    public List<Folder> Folders { get; set; } = [];
    public List<OwnedCopy> OwnedCopies { get; set; } = [];
}
