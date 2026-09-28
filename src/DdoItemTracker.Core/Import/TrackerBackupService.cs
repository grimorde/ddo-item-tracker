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
    public List<Folder> Folders { get; set; } = [];
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
        "This file isn't a DDO Item Tracker backup. To bring in characters from DDO Life Tracker, use Import from DDO Life Tracker on the Characters page.";

    public static string Export(TrackerData data, DateTimeOffset now) =>
        JsonSerializer.Serialize(new TrackerBackup
        {
            ExportedUtc = now,
            Characters = data.Characters,
            Folders = data.Folders,
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
        backup.Characters ??= [];
        backup.Folders ??= [];
        return backup;
    }

    public static BackupImportResult Import(TrackerData data, TrackerBackup backup, ImportMode mode)
    {
        if (mode == ImportMode.Replace)
        {
            data.Characters.Clear();
            data.Folders.Clear();
            data.OwnedCopies.Clear();
        }
        int added = 0, updated = 0, skipped = 0;

        foreach (var folder in backup.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Name)) { skipped++; continue; }
            Upsert(data.Folders, folder, f => f.Id);
        }

        foreach (var character in backup.Characters)
        {
            var server = Servers.Canonical(character.Server);
            if (server is null || string.IsNullOrWhiteSpace(character.Name)
                || data.Characters.Any(c => c.Id != character.Id && c.Server == server && TrackerOperations.IsSameName(c.Name, character.Name)))
            {
                skipped++;
                continue;
            }
            character.Server = server;
            character.Name = character.Name.Trim();
            if (character.FolderId is not null && data.Folders.All(f => f.Id != character.FolderId)) character.FolderId = null;
            if (Upsert(data.Characters, character, c => c.Id)) updated++; else added++;
        }

        foreach (var copy in backup.OwnedCopies ?? [])
        {
            copy.Server = Servers.Canonical(copy.Server) ?? copy.Server;
            if (OwnershipRules.ValidateCopy(data, copy) is not null) { skipped++; continue; }
            if (Upsert(data.OwnedCopies, copy, c => c.Id)) updated++; else added++;
        }

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
