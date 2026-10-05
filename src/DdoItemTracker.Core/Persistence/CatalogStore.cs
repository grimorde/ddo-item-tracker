using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Persistence;

/// <param name="WasUnreadable">True when a downloaded catalog exists but couldn't be read.</param>
public sealed record StoredCatalogLoad(ItemCatalog? Catalog, bool WasUnreadable);

/// <summary>Which catalog the app starts with, and whether it is a downloaded one.</summary>
public sealed record CatalogChoice(ItemCatalog Catalog, bool IsDownloaded, bool StoredWasUnreadable)
{
    /// <summary>
    /// Uses the downloaded catalog only when it is newer than the built-in one, so an app update that ships
    /// a newer built-in catalog is never hidden by an older download.
    /// </summary>
    public static CatalogChoice Choose(ItemCatalog builtIn, StoredCatalogLoad stored) =>
        stored.Catalog is { } downloaded && downloaded.Version.UpstreamCommitDateUtc > builtIn.Version.UpstreamCommitDateUtc
            ? new CatalogChoice(downloaded, true, false)
            : new CatalogChoice(builtIn, false, stored.WasUnreadable);
}

/// <summary>Keeps the most recently downloaded catalog in the app's data folder, saved atomically.</summary>
public sealed class CatalogStore(string directoryPath)
{
    public const string FileName = "catalog.json";

    public string DirectoryPath { get; } = directoryPath;
    public string FilePath => Path.Combine(DirectoryPath, FileName);
    private string TempPath => FilePath + ".tmp";

    public StoredCatalogLoad Load()
    {
        if (!File.Exists(FilePath)) return new StoredCatalogLoad(null, false);
        try
        {
            return new StoredCatalogLoad(CatalogSerializer.Deserialize(File.ReadAllText(FilePath)), false);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new StoredCatalogLoad(null, true);
        }
    }

    public void Save(ItemCatalog catalog)
    {
        Directory.CreateDirectory(DirectoryPath);
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(CatalogSerializer.Serialize(catalog));
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(TempPath, FilePath, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
