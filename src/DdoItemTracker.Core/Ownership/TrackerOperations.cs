namespace DdoItemTracker.Core.Ownership;

public enum CharacterCopyDisposal
{
    /// <summary>Keep the copies, recorded on the character's server only.</summary>
    KeepOnServer,
    DeleteCopies,
}

/// <summary>Every change to <see cref="TrackerData"/> goes through here so the rules hold.</summary>
public static class TrackerOperations
{
    public static bool IsSameName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    public static Character AddCharacter(TrackerData data, string server, string name)
    {
        var canonical = Servers.Canonical(server) ?? throw new TrackerRuleException("Choose a server.");
        var trimmed = RequireName(name);
        EnsureNameFree(data, canonical, trimmed, exceptId: null);
        var character = new Character { Server = canonical, Name = trimmed };
        data.Characters.Add(character);
        return character;
    }

    public static void RenameCharacter(TrackerData data, string characterId, string newName)
    {
        var character = FindCharacter(data, characterId);
        var trimmed = RequireName(newName);
        EnsureNameFree(data, character.Server, trimmed, character.Id);
        character.Name = trimmed;
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
                copy.Storage = null;
                copy.Server = character.Server;
            }
        }
        data.Characters.Remove(character);
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
        copy.Server = OwnershipRules.NormaliseServer(copy.Server);
        copy.Note = string.IsNullOrWhiteSpace(copy.Note) ? null : copy.Note.Trim();
    }

    private static string RequireName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return trimmed.Length == 0 ? throw new TrackerRuleException("Enter a character name.") : trimmed;
    }

    private static void EnsureNameFree(TrackerData data, string server, string name, string? exceptId)
    {
        if (data.Characters.Any(c => c.Id != exceptId && c.Server == server && IsSameName(c.Name, name)))
            throw new TrackerRuleException($"There is already a character called {name} on {server}.");
    }

    private static Character FindCharacter(TrackerData data, string id) =>
        data.Characters.FirstOrDefault(c => c.Id == id) ?? throw new TrackerRuleException("That character no longer exists.");
}
