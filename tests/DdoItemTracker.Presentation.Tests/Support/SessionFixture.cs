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

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }
}
