using System.Text.Json;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Presentation.State;

/// <summary>
/// The player's loaded data plus the catalog. Every change goes through <see cref="Apply{T}"/>,
/// which runs the change, saves, and tells the screens to refresh. Saves are serialised.
/// </summary>
public sealed class TrackerSession(TrackerStore store, CatalogIndex catalog)
{
    private readonly object _gate = new();
    private bool _startupMessageTaken;

    public CatalogIndex Catalog { get; } = catalog;
    public TrackerData Data { get; private set; } = new();
    public LoadResult? LastLoad { get; private set; }
    public event EventHandler? Changed;

    public void Load()
    {
        lock (_gate)
        {
            LastLoad = store.Load();
            Data = LastLoad.Data;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs a change on a copy of the data and saves the copy. Only once the save succeeds does the copy
    /// become <see cref="Data"/>, so a change that breaks a rule, or that cannot be saved, leaves nothing behind.
    /// </summary>
    public T Apply<T>(Func<TrackerData, T> change)
    {
        T result;
        lock (_gate)
        {
            var working = Clone(Data);
            result = change(working);
            store.Save(working);
            Data = working;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private static TrackerData Clone(TrackerData data) =>
        JsonSerializer.Deserialize<TrackerData>(JsonSerializer.Serialize(data, TrackerJson.Options), TrackerJson.Options)!;

    public void Apply(Action<TrackerData> change) => Apply<object?>(d =>
    {
        change(d);
        return null;
    });

    public string? StartupMessage => LastLoad?.Outcome switch
    {
        LoadOutcome.RecoveredFromBackup =>
            "Your item list couldn't be read, so the previous saved copy was loaded. Your most recent change may be missing."
            + (LastLoad.PreservedCopyPath is { } path ? $" The unreadable file was kept as {Path.GetFileName(path)}." : ""),
        LoadOutcome.BothUnreadable =>
            "Your item list and its backup couldn't be read, so the app started empty. The unreadable files were kept in the app's data folder, so nothing has been deleted.",
        _ => null,
    };

    /// <summary>The startup message the first time it is asked for, then null.</summary>
    public string? TakeStartupMessage()
    {
        if (_startupMessageTaken) return null;
        _startupMessageTaken = true;
        return StartupMessage;
    }
}
