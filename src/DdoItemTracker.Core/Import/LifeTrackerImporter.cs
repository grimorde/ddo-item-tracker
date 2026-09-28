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
    /// <remarks>
    /// Matching runs in passes so the result does not depend on the order of the file:
    /// Life Tracker Id matches first, then name matches against characters no Id match claimed,
    /// then name clashes are checked against the final names every character would end up with.
    /// </remarks>
    public static LifeTrackerImportPlan Plan(TrackerData data, LifeTrackerBackup backup)
    {
        var sources = backup.Characters;
        var actions = new LifeTrackerImportEntry?[sources.Count];
        var servers = new string?[sources.Count];
        var fileIds = sources.Where(s => s.Id.Length > 0).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var claimed = new Dictionary<string, int>(StringComparer.Ordinal); // existing character Id -> entry index

        // Pass 0: entries that can never import.
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            servers[i] = Servers.Canonical(source.Server);
            if (servers[i] is null)
                actions[i] = new(source, LifeTrackerImportAction.SkipUnknownServer, null,
                    string.IsNullOrWhiteSpace(source.Server) ? "No server." : $"Server \"{source.Server.Trim()}\" isn't supported.");
            else if (source.Name.Trim().Length == 0)
                actions[i] = new(source, LifeTrackerImportAction.SkipInvalid, null, "No character name.");
            else if (source.Id.Length > 0 && !seenIds.Add(source.Id))
                actions[i] = new(source, LifeTrackerImportAction.SkipInvalid, null, DuplicateInFile);
        }

        // Pass 1: Life Tracker Id matches.
        for (var i = 0; i < sources.Count; i++)
        {
            if (actions[i] is not null || sources[i].Id.Length == 0) continue;
            var match = data.Characters.FirstOrDefault(c => c.LifeTrackerId == sources[i].Id && c.Server == servers[i] && !claimed.ContainsKey(c.Id));
            if (match is null) continue;
            claimed[match.Id] = i;
            actions[i] = new(sources[i], LifeTrackerImportAction.Update, match.Id, null);
        }

        // Pass 2: name matches, only against characters no other Life Tracker character in this file owns.
        for (var i = 0; i < sources.Count; i++)
        {
            if (actions[i] is not null) continue;
            var name = sources[i].Name.Trim();
            var match = data.Characters.FirstOrDefault(c =>
                c.Server == servers[i] && TrackerOperations.IsSameName(c.Name, name) && !claimed.ContainsKey(c.Id)
                && (c.LifeTrackerId is null || !fileIds.Contains(c.LifeTrackerId)));
            if (match is not null)
            {
                claimed[match.Id] = i;
                actions[i] = new(sources[i], LifeTrackerImportAction.Update, match.Id, null);
            }
            else
            {
                actions[i] = new(sources[i], LifeTrackerImportAction.Add, null, null);
            }
        }

        // Pass 3: updates that would rename onto another character's final name are skipped. A skipped
        // update keeps its old name, which can block another update, so repeat until nothing changes.
        var skippedUpdates = new HashSet<int>();
        HashSet<string> taken;
        while (true)
        {
            var active = actions.Select((a, i) => (a, i))
                .Where(x => x.a!.Action == LifeTrackerImportAction.Update && !skippedUpdates.Contains(x.i))
                .ToDictionary(x => x.a!.ExistingCharacterId!, x => x.i, StringComparer.Ordinal);
            taken = data.Characters.Where(c => !active.ContainsKey(c.Id)).Select(c => NameKey(c.Server, c.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var before = skippedUpdates.Count;
            foreach (var i in active.Values.Order())
                if (!taken.Add(NameKey(servers[i]!, sources[i].Name))) skippedUpdates.Add(i);
            if (skippedUpdates.Count == before) break;
        }
        foreach (var i in skippedUpdates)
            actions[i] = actions[i]! with
            {
                Action = LifeTrackerImportAction.SkipInvalid,
                Reason = $"Another {servers[i]} character is already called {sources[i].Name.Trim()}.",
            };

        // Pass 4: adds, in file order, against every final name.
        var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sources.Count; i++)
        {
            if (actions[i]!.Action != LifeTrackerImportAction.Add) continue;
            var key = NameKey(servers[i]!, sources[i].Name);
            if (taken.Add(key))
            {
                addedKeys.Add(key);
                continue;
            }
            actions[i] = actions[i]! with
            {
                Action = LifeTrackerImportAction.SkipInvalid,
                Reason = addedKeys.Contains(key) ? DuplicateInFile : $"Another {servers[i]} character is already called {sources[i].Name.Trim()}.",
            };
        }

        return new LifeTrackerImportPlan(actions.Select(a => a!).ToList());
    }

    private static string NameKey(string server, string name) => $"{server}\n{name.Trim()}";

    /// <summary>Plans against the current data and applies the plan. Returns the plan that was applied.</summary>
    public static LifeTrackerImportPlan Apply(TrackerData data, LifeTrackerBackup backup)
    {
        var plan = Plan(data, backup);
        foreach (var entry in plan.Entries)
        {
            var source = entry.Source;
            var lifeTrackerId = source.Id.Length > 0 ? source.Id : null;
            if (entry.Action == LifeTrackerImportAction.Add)
            {
                data.Characters.Add(new Character
                {
                    Server = Servers.Canonical(source.Server)!,
                    Name = source.Name.Trim(),
                    LifeTrackerId = lifeTrackerId,
                });
            }
            else if (entry.Action == LifeTrackerImportAction.Update)
            {
                var character = data.Characters.First(c => c.Id == entry.ExistingCharacterId);
                character.Name = source.Name.Trim();
                character.LifeTrackerId = lifeTrackerId ?? character.LifeTrackerId;
            }
        }
        return plan;
    }
}
