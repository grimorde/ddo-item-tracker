namespace DdoItemTracker.Core.Ownership;

public enum CharacterCopyDisposal
{
    DeleteCopies,
    MoveToSharedBank,
}

/// <summary>Every change to <see cref="TrackerData"/> goes through here so the rules hold.</summary>
public static class TrackerOperations
{
    public static bool IsSameName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Character AddCharacter(TrackerData data, string server, string name, string? folderId = null)
    {
        var canonical = Servers.Canonical(server) ?? throw new TrackerRuleException("Choose a server.");
        var trimmed = RequireName(name, "Enter a character name.");
        EnsureNameFree(data, canonical, trimmed, exceptId: null);
        if (folderId is not null) FindFolder(data, folderId);
        var character = new Character { Server = canonical, Name = trimmed, FolderId = folderId };
        data.Characters.Add(character);
        return character;
    }

    public static void RenameCharacter(TrackerData data, string characterId, string newName)
    {
        var character = FindCharacter(data, characterId);
        var trimmed = RequireName(newName, "Enter a character name.");
        EnsureNameFree(data, character.Server, trimmed, character.Id);
        character.Name = trimmed;
    }

    public static void MoveCharacterToFolder(TrackerData data, string characterId, string? folderId)
    {
        var character = FindCharacter(data, characterId);
        if (folderId is not null) FindFolder(data, folderId);
        character.FolderId = folderId;
    }

    public static void DeleteCharacter(TrackerData data, string characterId, CharacterCopyDisposal disposal)
    {
        var character = FindCharacter(data, characterId);
        if (disposal == CharacterCopyDisposal.DeleteCopies)
        {
            data.OwnedCopies.RemoveAll(c => c.CharacterId == characterId);
        }
        else
        {
            foreach (var copy in data.OwnedCopies.Where(c => c.CharacterId == characterId))
            {
                copy.CharacterId = null;
                copy.Storage = StorageType.SharedBank;
                copy.Server = character.Server;
            }
        }
        data.Characters.Remove(character);
    }

    public static Folder AddFolder(TrackerData data, string name)
    {
        var trimmed = RequireName(name, "Enter a folder name.");
        if (data.Folders.Any(f => IsSameName(f.Name, trimmed)))
            throw new TrackerRuleException($"There is already a folder called {trimmed}.");
        var folder = new Folder { Name = trimmed };
        data.Folders.Add(folder);
        return folder;
    }

    public static void DeleteFolder(TrackerData data, string folderId)
    {
        foreach (var c in data.Characters.Where(c => c.FolderId == folderId)) c.FolderId = null;
        data.Folders.RemoveAll(f => f.Id == folderId);
    }

    public static OwnedCopy AddCopy(TrackerData data, OwnedCopy copy)
    {
        Normalise(copy);
        if (OwnershipRules.ValidateCopy(data, copy) is { } error) throw new TrackerRuleException(error);
        if (data.OwnedCopies.Any(c => c.Id == copy.Id)) throw new TrackerRuleException("This copy is already recorded.");
        if (copy.AddedUtc == default) copy.AddedUtc = DateTimeOffset.UtcNow;
        data.OwnedCopies.Add(copy);
        return copy;
    }

    /// <summary>Replaces the stored copy with the same Id. Pass a new instance so a rejected edit changes nothing.</summary>
    public static void UpdateCopy(TrackerData data, OwnedCopy copy)
    {
        var index = data.OwnedCopies.FindIndex(c => c.Id == copy.Id);
        if (index < 0) throw new TrackerRuleException("That copy no longer exists.");
        Normalise(copy);
        if (OwnershipRules.ValidateCopy(data, copy) is { } error) throw new TrackerRuleException(error);
        if (copy.AddedUtc == default) copy.AddedUtc = data.OwnedCopies[index].AddedUtc;
        data.OwnedCopies[index] = copy;
    }

    public static void RemoveCopy(TrackerData data, string copyId) =>
        data.OwnedCopies.RemoveAll(c => c.Id == copyId);

    private static void Normalise(OwnedCopy copy)
    {
        copy.Server = Servers.Canonical(copy.Server) ?? copy.Server;
        copy.Note = string.IsNullOrWhiteSpace(copy.Note) ? null : copy.Note.Trim();
    }

    private static string RequireName(string? name, string message)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length == 0 ? throw new TrackerRuleException(message) : trimmed;
    }

    private static void EnsureNameFree(TrackerData data, string server, string name, string? exceptId)
    {
        if (data.Characters.Any(c => c.Id != exceptId && c.Server == server && IsSameName(c.Name, name)))
            throw new TrackerRuleException($"There is already a character called {name} on {server}.");
    }

    private static Character FindCharacter(TrackerData data, string id) =>
        data.Characters.FirstOrDefault(c => c.Id == id) ?? throw new TrackerRuleException("That character no longer exists.");

    private static Folder FindFolder(TrackerData data, string id) =>
        data.Folders.FirstOrDefault(f => f.Id == id) ?? throw new TrackerRuleException("That folder no longer exists.");
}
