using System.Text.Json;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Import;

public sealed class TrackerBackup
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ExportedUtc { get; set; }
    public List<Character> Characters { get; set; } = [];
    /// <summary>Null when the file isn't ours; a DDO Life Tracker backup has no OwnedCopies.</summary>
    public List<OwnedCopy>? OwnedCopies { get; set; }
}

public enum ImportMode
{
    Merge,
    Replace,
}

public sealed record BackupImportResult(int Added, int Updated, int Skipped);

public static class TrackerBackupService
{
    private const string NotOurs =
        "This file isn't a DDO Item Tracker backup. To bring in characters from DDO Life Tracker, use Import from DDO Life Tracker in Settings, under Characters.";

    public static string Export(TrackerData data, DateTimeOffset now) =>
        JsonSerializer.Serialize(new TrackerBackup
        {
            ExportedUtc = now,
            Characters = data.Characters,
            OwnedCopies = data.OwnedCopies,
        }, TrackerJson.Options);

    public static TrackerBackup Parse(string json)
    {
        TrackerBackup? backup;
        try
        {
            backup = JsonSerializer.Deserialize<TrackerBackup>(json, TrackerJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(NotOurs, ex);
        }
        if (backup?.OwnedCopies is null) throw new InvalidDataException(NotOurs);
        if (backup.SchemaVersion > TrackerBackup.CurrentSchemaVersion)
            throw new InvalidDataException("This backup was made by a newer version of DDO Item Tracker. Update the app, then try again.");
        backup.Characters = (backup.Characters ?? []).Where(c => c is not null).ToList();
        backup.OwnedCopies = backup.OwnedCopies.Where(c => c is not null).ToList();
        foreach (var c in backup.Characters) if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Guid.NewGuid().ToString();
        foreach (var c in backup.OwnedCopies) if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Guid.NewGuid().ToString();
        return backup;
    }

    /// <summary>
    /// Applies a backup. The work is done on a copy of <paramref name="data"/>, which is only
    /// updated once the whole backup has been processed, so a failure part way leaves it untouched.
    /// </summary>
    public static BackupImportResult Import(TrackerData data, TrackerBackup backup, ImportMode mode)
    {
        var work = mode == ImportMode.Replace
            ? new TrackerData()
            : JsonSerializer.Deserialize<TrackerData>(JsonSerializer.Serialize(data, TrackerJson.Options), TrackerJson.Options)!;
        int added = 0, updated = 0, skipped = 0;
        // Incoming character Id -> Id of the same-named character already here.
        var characterIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var character in backup.Characters)
        {
            var server = Servers.Canonical(character.Server);
            if (server is null || string.IsNullOrWhiteSpace(character.Name))
            {
                skipped++;
                continue;
            }
            character.Server = server;
            character.Name = character.Name.Trim();

            var namesake = work.Characters.FirstOrDefault(c =>
                c.Id != character.Id && c.Server == server && TrackerOperations.IsSameName(c.Name, character.Name));
            if (namesake is not null)
            {
                if (work.Characters.Any(c => c.Id == character.Id))
                {
                    skipped++; // renaming this character onto another one's name would create a duplicate
                    continue;
                }
                // The same character recorded under a different Id (for example re-created on a new device).
                characterIds[character.Id] = namesake.Id;
                updated++;
                continue;
            }
            if (Upsert(work.Characters, character, c => c.Id)) updated++; else added++;
        }

        foreach (var copy in backup.OwnedCopies ?? [])
        {
            copy.Server = OwnershipRules.NormaliseServer(copy.Server);
            if (copy.CharacterId is not null && characterIds.TryGetValue(copy.CharacterId, out var mapped)) copy.CharacterId = mapped;
            if (OwnershipRules.ValidateCopy(work, copy) is not null) { skipped++; continue; }
            if (Upsert(work.OwnedCopies, copy, c => c.Id)) updated++; else added++;
        }

        data.Characters.Clear();
        data.Characters.AddRange(work.Characters);
        data.OwnedCopies.Clear();
        data.OwnedCopies.AddRange(work.OwnedCopies);
        return new BackupImportResult(added, updated, skipped);
    }

    /// <returns>True when an existing record was replaced, false when the record was added.</returns>
    private static bool Upsert<T>(List<T> list, T item, Func<T, string> id)
    {
        var index = list.FindIndex(x => id(x) == id(item));
        if (index < 0)
        {
            list.Add(item);
            return false;
        }
        list[index] = item;
        return true;
    }
}
