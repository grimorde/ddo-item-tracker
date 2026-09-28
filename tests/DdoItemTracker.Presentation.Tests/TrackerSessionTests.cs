using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Presentation.State;
using DdoItemTracker.Presentation.Tests.Support;

namespace DdoItemTracker.Presentation.Tests;

public class TrackerSessionTests
{
    [Fact]
    public void Apply_SavesAndRaisesChanged()
    {
        using var f = new SessionFixture();
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));

        Assert.Equal(1, raised);
        Assert.Equal("Grimorde", Assert.Single(f.Store.Load().Data.Characters).Name);
    }

    [Fact]
    public void Apply_ReturnsTheChangeResult()
    {
        using var f = new SessionFixture();
        var character = f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        Assert.Same(character, f.Session.Data.Characters[0]);
    }

    [Fact]
    public void Apply_RuleBroken_DoesNotSaveOrNotify()
    {
        using var f = new SessionFixture();
        var raised = 0;
        f.Session.Changed += (_, _) => raised++;

        Assert.Throws<TrackerRuleException>(() => f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Lamannia", "X")));

        Assert.Equal(0, raised);
        Assert.False(File.Exists(f.Store.FilePath));
    }

    [Fact]
    public void Load_ReadsSavedData()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var second = new TrackerSession(f.Store, SampleCatalog.Create());

        second.Load();

        Assert.Single(second.Data.Characters);
        Assert.Equal(LoadOutcome.Loaded, second.LastLoad!.Outcome);
        Assert.Null(second.StartupMessage);
    }

    [Fact]
    public void StartupMessage_AfterRecovery_IsGivenOnce()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "First"));
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Second"));
        File.WriteAllText(f.Store.FilePath, "");
        var session = new TrackerSession(f.Store, SampleCatalog.Create());
        session.Load();

        var message = session.TakeStartupMessage();

        Assert.StartsWith("Your item list couldn't be read, so the previous saved copy was loaded.", message);
        Assert.Contains("tracker.json.unreadable-", message);
        Assert.Null(session.TakeStartupMessage());
    }

    [Fact]
    public void StartupMessage_BothUnreadable_SaysStartedEmpty()
    {
        using var f = new SessionFixture();
        Directory.CreateDirectory(f.Directory);
        File.WriteAllText(f.Store.FilePath, "bad");
        File.WriteAllText(f.Store.BackupPath, "bad");
        var session = new TrackerSession(f.Store, SampleCatalog.Create());
        session.Load();

        Assert.StartsWith("Your item list and its backup couldn't be read", session.TakeStartupMessage());
        Assert.Empty(session.Data.Characters);
    }

    [Fact]
    public void Apply_SaveFails_LeavesDataUnchangedAndDoesNotNotify()
    {
        using var f = new SessionFixture();
        Directory.CreateDirectory(f.Directory);
        var blocked = Path.Combine(f.Directory, "blocked");
        File.WriteAllText(blocked, "a file where the store wants a folder");
        var session = new TrackerSession(new TrackerStore(blocked), SampleCatalog.Create());
        session.Load();
        var raised = 0;
        session.Changed += (_, _) => raised++;

        Assert.ThrowsAny<IOException>(() => session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde")));

        Assert.Empty(session.Data.Characters);
        Assert.Equal(0, raised);
    }
}
