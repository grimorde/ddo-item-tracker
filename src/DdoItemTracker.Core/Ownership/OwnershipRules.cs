namespace DdoItemTracker.Core.Ownership;

public static class OwnershipRules
{
    /// <summary>Blank becomes null; a known server becomes its canonical name; anything else is kept so validation rejects it.</summary>
    public static string? NormaliseServer(string? server) =>
        string.IsNullOrWhiteSpace(server) ? null : Servers.Canonical(server) ?? server;

    /// <summary>Returns null when the copy is valid, otherwise a message for the player.</summary>
    public static string? ValidateCopy(TrackerData data, OwnedCopy copy)
    {
        if (string.IsNullOrWhiteSpace(copy.ItemKey)) return "Choose an item.";
        if (copy.Storage is { } storage && !Enum.IsDefined(storage)) return "Choose where it's stored.";

        if (string.IsNullOrWhiteSpace(copy.Server))
            return copy.CharacterId is null && copy.Storage is null ? null : "Choose a server first.";
        var server = Servers.Canonical(copy.Server);
        if (server is null) return "Choose a server.";

        if (copy.CharacterId is null)
            return copy.Storage is null or StorageType.SharedBank ? null : "Choose a character for Inventory or Bank.";
        if (copy.Storage == StorageType.SharedBank) return "Shared Bank items don't belong to a character.";

        var character = data.Characters.FirstOrDefault(c => c.Id == copy.CharacterId);
        if (character is null) return "That character no longer exists.";
        if (character.Server != server) return $"{character.Name} is on {character.Server}, not {server}.";
        return null;
    }
}
