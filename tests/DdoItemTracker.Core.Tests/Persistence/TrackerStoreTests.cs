using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Tests.Persistence;

public sealed class TrackerStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ddo-item-tracker-tests", Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static TrackerData Sample(string name = "Grimorde")
    {
        var data = new TrackerData();
        var c = TrackerOperations.AddCharacter(data, "Cormyr", name);
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = c.Id });
        return data;
    }

    [Fact]
    public void Load_WithNoFiles_ReturnsEmptyNewFile()
    {
        var result = new TrackerStore(_dir).Load();
        Assert.Equal(LoadOutcome.NewFile, result.Outcome);
        Assert.Empty(result.Data.Characters);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample());

        var result = store.Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Equal("Grimorde", Assert.Single(result.Data.Characters).Name);
        Assert.Equal(StorageType.Bank, Assert.Single(result.Data.OwnedCopies).Storage);
        Assert.Contains("\"Bank\"", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Save_KeepsPreviousVersionAsBak()
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample("First"));
        store.Save(Sample("Second"));

        Assert.Contains("First", File.ReadAllText(store.BackupPath));
        Assert.Contains("Second", File.ReadAllText(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void Load_UnreadableMainFile_RecoversFromBakAndPreservesBadFile(string badContent)
    {
        var store = new TrackerStore(_dir);
        store.Save(Sample("First"));
        store.Save(Sample("Second"));
        File.WriteAllText(store.FilePath, badContent);

        var result = store.Load();

        Assert.Equal(LoadOutcome.RecoveredFromBackup, result.Outcome);
        Assert.Equal("First", Assert.Single(result.Data.Characters).Name);
        Assert.NotNull(result.PreservedCopyPath);
        Assert.Equal(badContent, File.ReadAllText(result.PreservedCopyPath!));
    }

    [Fact]
    public void Load_BothUnreadable_StartsEmptyAndPreservesBoth()
    {
        var store = new TrackerStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "bad main");
        File.WriteAllText(store.BackupPath, "bad backup");

        var result = store.Load();

        Assert.Equal(LoadOutcome.BothUnreadable, result.Outcome);
        Assert.Empty(result.Data.Characters);
        var preserved = Directory.GetFiles(_dir, "*.unreadable-*").Select(File.ReadAllText).Order().ToList();
        Assert.Equal(["bad backup", "bad main"], preserved);
    }

    [Fact]
    public void Load_NullListsInFile_BecomeEmptyLists()
    {
        var store = new TrackerStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, """{"SchemaVersion":1,"Characters":null}""");

        var result = store.Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Empty(result.Data.Characters);
        Assert.Empty(result.Data.Folders);
        Assert.Empty(result.Data.OwnedCopies);
    }
}
