using System.Text.Json;

namespace DdoItemTracker.Core.Import;

public sealed record LifeTrackerCharacter(string Id, string Server, string Name);

public sealed record LifeTrackerBackup(IReadOnlyList<LifeTrackerCharacter> Characters);

/// <summary>Reads the characters from a DDO Life Tracker backup. Folders, past lives and tomes are ignored.</summary>
public static class LifeTrackerBackupReader
{
    public const string NotALifeTrackerBackup = "This file isn't a DDO Life Tracker backup.";

    public static LifeTrackerBackup Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(NotALifeTrackerBackup, ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            JsonElement characters;

            if (root.ValueKind == JsonValueKind.Array)
            {
                characters = root; // Life Tracker's legacy format: a bare character list
            }
            else if (root.ValueKind == JsonValueKind.Object && TryGet(root, "Characters", out characters) && characters.ValueKind == JsonValueKind.Array)
            {
                if (TryGet(root, "OwnedCopies", out _))
                    throw new InvalidDataException("This is a DDO Item Tracker backup. Use Restore backup in Settings instead.");
            }
            else
            {
                throw new InvalidDataException(NotALifeTrackerBackup);
            }

            var characterList = new List<LifeTrackerCharacter>();
            foreach (var c in characters.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object || !TryGet(c, "Server", out _) || !TryGet(c, "Name", out _))
                    throw new InvalidDataException(NotALifeTrackerBackup);
                characterList.Add(new LifeTrackerCharacter(
                    Str(c, "Id") ?? string.Empty,
                    Str(c, "Server") ?? string.Empty,
                    Str(c, "Name") ?? string.Empty));
            }

            return new LifeTrackerBackup(characterList);
        }
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? Str(JsonElement obj, string name) =>
        TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

}
