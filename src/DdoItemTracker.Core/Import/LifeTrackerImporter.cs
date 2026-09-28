using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Import;

public enum LifeTrackerImportAction
{
    Add,
    Update,
    SkipUnknownServer,
    SkipInvalid,
}

public sealed record LifeTrackerImportEntry(
    LifeTrackerCharacter Source,
    LifeTrackerImportAction Action,
    string? ExistingCharacterId,
    string? Reason);

public sealed record LifeTrackerImportPlan(IReadOnlyList<LifeTrackerImportEntry> Entries)
{
    public int AddCount => Entries.Count(e => e.Action == LifeTrackerImportAction.Add);
    public int UpdateCount => Entries.Count(e => e.Action == LifeTrackerImportAction.Update);
    public int SkipCount => Entries.Count(e => e.Action is LifeTrackerImportAction.SkipUnknownServer or LifeTrackerImportAction.SkipInvalid);
}

public static class LifeTrackerImporter
{
    private const string DuplicateInFile = "Appears more than once in the file.";

    /// <summary>Works out what an import would do, without changing anything.</summary>
    public static LifeTrackerImportPlan Plan(TrackerData data, LifeTrackerBackup backup)
    {
        var entries = new List<LifeTrackerImportEntry>();
        var matchedIds = new HashSet<string>(StringComparer.Ordinal);
        var plannedNew = new List<(string Server, string Name)>();

        foreach (var source in backup.Characters)
        {
            var server = Servers.Canonical(source.Server);
            if (server is null)
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipUnknownServer, null, $"Server \"{source.Server.Trim()}\" isn't supported."));
                continue;
            }
            var name = source.Name.Trim();
            if (name.Length == 0)
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, "No character name."));
                continue;
            }

            var existing =
                data.Characters.FirstOrDefault(c => source.Id.Length > 0 && c.LifeTrackerId == source.Id && c.Server == server)
                ?? data.Characters.FirstOrDefault(c => c.Server == server && TrackerOperations.IsSameName(c.Name, name));

            if (existing is not null)
            {
                if (!matchedIds.Add(existing.Id))
                {
                    entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, DuplicateInFile));
                    continue;
                }
                if (data.Characters.Any(c => c.Id != existing.Id && c.Server == server && TrackerOperations.IsSameName(c.Name, name)))
                {
                    entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, existing.Id, $"Another {server} character is already called {name}."));
                    continue;
                }
                entries.Add(new(source, LifeTrackerImportAction.Update, existing.Id, null));
                continue;
            }

            if (plannedNew.Any(p => p.Server == server && TrackerOperations.IsSameName(p.Name, name)))
            {
                entries.Add(new(source, LifeTrackerImportAction.SkipInvalid, null, DuplicateInFile));
                continue;
            }
            plannedNew.Add((server, name));
            entries.Add(new(source, LifeTrackerImportAction.Add, null, null));
        }

        return new LifeTrackerImportPlan(entries);
    }

    /// <summary>Plans against the current data and applies the plan. Returns the plan that was applied.</summary>
    public static LifeTrackerImportPlan Apply(TrackerData data, LifeTrackerBackup backup)
    {
        var plan = Plan(data, backup);
        var folderNames = backup.Folders.ToDictionary(f => f.Id, f => f.Name, StringComparer.Ordinal);

        foreach (var entry in plan.Entries)
        {
            if (entry.Action is not (LifeTrackerImportAction.Add or LifeTrackerImportAction.Update)) continue;
            var source = entry.Source;
            var folderId = source.FolderId is { } f && folderNames.TryGetValue(f, out var folderName)
                ? FindOrCreateFolder(data, folderName)
                : null;
            var lifeTrackerId = source.Id.Length > 0 ? source.Id : null;

            if (entry.Action == LifeTrackerImportAction.Add)
            {
                data.Characters.Add(new Character
                {
                    Server = Servers.Canonical(source.Server)!,
                    Name = source.Name.Trim(),
                    FolderId = folderId,
                    LifeTrackerId = lifeTrackerId,
                });
            }
            else
            {
                var character = data.Characters.First(c => c.Id == entry.ExistingCharacterId);
                character.Name = source.Name.Trim();
                character.LifeTrackerId = lifeTrackerId ?? character.LifeTrackerId;
                if (folderId is not null) character.FolderId = folderId;
            }
        }

        return plan;
    }

    private static string FindOrCreateFolder(TrackerData data, string name)
    {
        var folder = data.Folders.FirstOrDefault(f => TrackerOperations.IsSameName(f.Name, name));
        if (folder is null)
        {
            folder = new Folder { Name = name };
            data.Folders.Add(folder);
        }
        return folder.Id;
    }
}
