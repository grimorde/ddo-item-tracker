using System.Globalization;
using System.Text.Json;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Persistence;

public enum LoadOutcome
{
    NewFile,
    Loaded,
    RecoveredFromBackup,
    BothUnreadable,
}

public sealed record LoadResult(TrackerData Data, LoadOutcome Outcome, string? PreservedCopyPath);

/// <summary>Saves the player's data atomically and keeps the previous version as a .bak.</summary>
public sealed class TrackerStore(string directoryPath, TimeProvider? clock = null)
{
    public const string FileName = "tracker.json";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string DirectoryPath { get; } = directoryPath;
    public string FilePath => Path.Combine(DirectoryPath, FileName);
    public string BackupPath => FilePath + ".bak";
    private string TempPath => FilePath + ".tmp";

    public LoadResult Load()
    {
        var mainExists = File.Exists(FilePath);
        var backupExists = File.Exists(BackupPath);
        if (!mainExists && !backupExists) return new LoadResult(new TrackerData(), LoadOutcome.NewFile, null);

        if (mainExists && TryRead(FilePath) is { } data) return new LoadResult(data, LoadOutcome.Loaded, null);

        var preserved = mainExists ? Preserve(FilePath) : null;
        if (backupExists && TryRead(BackupPath) is { } recovered)
            return new LoadResult(recovered, LoadOutcome.RecoveredFromBackup, preserved);

        if (backupExists) Preserve(BackupPath);
        return new LoadResult(new TrackerData(), LoadOutcome.BothUnreadable, preserved);
    }

    public void Save(TrackerData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(TempPath, JsonSerializer.Serialize(data, TrackerJson.Options));
        if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, BackupPath);
        else File.Move(TempPath, FilePath);
    }

    private static TrackerData? TryRead(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonSerializer.Deserialize<TrackerData>(json, TrackerJson.Options);
            if (data is null) return null;
            data.Characters ??= [];
            data.Folders ??= [];
            data.OwnedCopies ??= [];
            return data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string Preserve(string path)
    {
        var stamp = _clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var destination = $"{path}.unreadable-{stamp}";
        File.Copy(path, destination, overwrite: true);
        return destination;
    }
}
