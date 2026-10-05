using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Persistence;

public sealed class CatalogStoreTests : IDisposable
{
    private static readonly DateTimeOffset Old = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset New = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ddo-item-tracker-tests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ItemCatalog Catalog(string commit, DateTimeOffset date) =>
        TestCatalogs.WithItemCount(2) with { Version = new CatalogVersion(commit, date, date) };

    [Fact]
    public void Load_WithNoFile_ReturnsNothing()
    {
        Assert.Equal(new StoredCatalogLoad(null, false), new CatalogStore(_dir).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new CatalogStore(_dir);
        store.Save(Catalog("abc", New));

        var loaded = store.Load();

        Assert.False(loaded.WasUnreadable);
        Assert.Equal("abc", loaded.Catalog!.Version.UpstreamCommit);
        Assert.Equal(2, loaded.Catalog.Items.Count);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Save_ReplacesTheEarlierCatalog()
    {
        var store = new CatalogStore(_dir);
        store.Save(Catalog("first", Old));
        store.Save(Catalog("second", New));
        Assert.Equal("second", store.Load().Catalog!.Version.UpstreamCommit);
    }

    [Fact]
    public void Load_DamagedFile_IsUnreadable()
    {
        var store = new CatalogStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{ damaged");

        Assert.Equal(new StoredCatalogLoad(null, true), store.Load());
    }

    [Fact]
    public void Delete_RemovesTheFile_AndIsSafeWhenMissing()
    {
        var store = new CatalogStore(_dir);
        store.Save(Catalog("abc", New));
        store.Delete();
        store.Delete();
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void Choose_NewerDownload_IsUsed()
    {
        var choice = CatalogChoice.Choose(Catalog("built-in", Old), new StoredCatalogLoad(Catalog("download", New), false));
        Assert.Equal(("download", true), (choice.Catalog.Version.UpstreamCommit, choice.IsDownloaded));
    }

    [Fact]
    public void Choose_OlderDownload_IsIgnored_SoANewerBuiltInWins()
    {
        var choice = CatalogChoice.Choose(Catalog("built-in", New), new StoredCatalogLoad(Catalog("download", Old), false));
        Assert.Equal(("built-in", false), (choice.Catalog.Version.UpstreamCommit, choice.IsDownloaded));
    }

    [Fact]
    public void Choose_UnreadableDownload_UsesBuiltIn_AndSaysSo()
    {
        var choice = CatalogChoice.Choose(Catalog("built-in", Old), new StoredCatalogLoad(null, true));
        Assert.Equal(("built-in", false, true), (choice.Catalog.Version.UpstreamCommit, choice.IsDownloaded, choice.StoredWasUnreadable));
    }
}
