using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.Tests.Support;

/// <summary>A loaded session over the sample catalog, saving into a temporary folder.</summary>
internal sealed class SessionFixture : IDisposable
{
    public SessionFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "ddo-item-tracker-presentation-tests", Guid.NewGuid().ToString());
        Store = new TrackerStore(Directory);
        Session = new TrackerSession(Store, SampleCatalog.Create());
        Session.Load();
    }

    public string Directory { get; }
    public TrackerStore Store { get; }
    public TrackerSession Session { get; }
    public FakeDialogs Dialogs { get; } = new();
    public FakeNavigator Navigator { get; } = new();
    public FakeFiles Files { get; } = new();
    public FakeSettings Settings { get; } = new();
    public FakeCatalogChecker CatalogChecker { get; } = new();
    public CatalogStore CatalogStore => new(Directory);
    public static DateTimeOffset Now { get; } = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    public CatalogUpdateCoordinator CatalogUpdates(bool storedWasUnreadable = false) =>
        new(Session, CatalogStore, CatalogChecker, () => SampleCatalog.Create().Catalog, Dialogs, Settings, new FixedClock(Now), storedWasUnreadable);

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }
}
