namespace DdoItemTracker.Core.Ownership;

public static class OwnershipRules
{
    /// <summary>Returns null when the copy is valid, otherwise a message for the player.</summary>
    public static string? ValidateCopy(TrackerData data, OwnedCopy copy)
    {
        if (string.IsNullOrWhiteSpace(copy.ItemKey)) return "Choose an item.";
        var server = Servers.Canonical(copy.Server);
        if (server is null) return "Choose a server.";
        if (!Enum.IsDefined(copy.Storage)) return "Choose where it's stored.";

        if (copy.Storage == StorageType.SharedBank)
            return copy.CharacterId is null ? null : "Shared Bank items don't belong to a character.";

        if (copy.CharacterId is null) return "Choose a character.";
        var character = data.Characters.FirstOrDefault(c => c.Id == copy.CharacterId);
        if (character is null) return "That character no longer exists.";
        if (character.Server != server) return $"{character.Name} is on {character.Server}, not {server}.";
        return null;
    }
}
