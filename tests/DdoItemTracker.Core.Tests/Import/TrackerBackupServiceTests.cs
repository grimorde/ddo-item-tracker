using System.Text.Json;
using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;

namespace DdoItemTracker.Core.Tests.Import;

public class TrackerBackupServiceTests
{
    private static TrackerData Sample()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Thrane", Storage = StorageType.SharedBank });
        return data;
    }

    private static string Snapshot(TrackerData data) => JsonSerializer.Serialize(data, TrackerJson.Options);

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExportThenReplaceIntoEmpty_ReproducesData()
    {
        var source = Sample();
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(source, Now));
        var target = new TrackerData();

        var result = TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.Equal(Snapshot(source), Snapshot(target));
        Assert.Equal(new BackupImportResult(3, 0, 0), result); // 1 character + 2 copies
        Assert.Equal(Now, backup.ExportedUtc);
    }

    [Fact]
    public void MergeSameBackupTwice_ChangesNothingTheSecondTime()
    {
        var json = TrackerBackupService.Export(Sample(), Now);
        var target = new TrackerData();
        TrackerBackupService.Import(target, TrackerBackupService.Parse(json), ImportMode.Merge);
        var afterFirst = Snapshot(target);

        TrackerBackupService.Import(target, TrackerBackupService.Parse(json), ImportMode.Merge);

        Assert.Equal(afterFirst, Snapshot(target));
    }

    [Fact]
    public void Merge_KeepsExistingRecordsNotInBackup_AndIncomingWins()
    {
        var target = Sample();
        var extra = TrackerOperations.AddCharacter(target, "Moonsea", "Keeper");
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(target, Now));
        backup.Characters.Single(c => c.Name == "Grimorde").Name = "Grimorde Renamed";
        backup.Characters.RemoveAll(c => c.Id == extra.Id);

        TrackerBackupService.Import(target, backup, ImportMode.Merge);

        Assert.Contains(target.Characters, c => c.Name == "Keeper");
        Assert.Contains(target.Characters, c => c.Name == "Grimorde Renamed");
    }

    [Fact]
    public void Replace_RemovesRecordsNotInBackup()
    {
        var target = Sample();
        TrackerOperations.AddCharacter(target, "Moonsea", "Keeper");
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));

        TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.DoesNotContain(target.Characters, c => c.Name == "Keeper");
    }

    [Fact]
    public void InvalidRecordsInBackup_AreSkippedAndCounted()
    {
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));
        backup.Characters.Add(new Character { Server = "Lamannia", Name = "Tester" });
        backup.OwnedCopies!.Add(new OwnedCopy { ItemKey = "B|1|Ring", ItemName = "B", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = "missing" });
        var target = new TrackerData();

        var result = TrackerBackupService.Import(target, backup, ImportMode.Replace);

        Assert.Equal(2, result.Skipped);
        Assert.DoesNotContain(target.Characters, c => c.Name == "Tester");
        Assert.DoesNotContain(target.OwnedCopies, c => c.ItemKey == "B|1|Ring");
    }

    [Fact]
    public void Parse_LifeTrackerBackup_PointsToTheRightImport()
    {
        const string lifeTracker = """{"SchemaVersion":2,"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Grimorde"}],"Folders":[]}""";
        var ex = Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse(lifeTracker));
        Assert.Equal("This file isn't a DDO Item Tracker backup. To bring in characters from DDO Life Tracker, use Import from DDO Life Tracker in Settings, under Characters.", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Parse_Garbage_Throws(string json)
    {
        Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse(json));
    }

    [Fact]
    public void Parse_NewerSchema_Throws()
    {
        var ex = Assert.Throws<InvalidDataException>(() => TrackerBackupService.Parse("""{"SchemaVersion":99,"OwnedCopies":[]}"""));
        Assert.Equal("This backup was made by a newer version of DDO Item Tracker. Update the app, then try again.", ex.Message);
    }

    [Fact]
    public void Merge_SameNameCharacterWithDifferentId_BringsItsCopies()
    {
        var backup = TrackerBackupService.Parse(TrackerBackupService.Export(Sample(), Now));
        var target = new TrackerData();
        var mine = TrackerOperations.AddCharacter(target, "Cormyr", "grimorde");

        var result = TrackerBackupService.Import(target, backup, ImportMode.Merge);

        Assert.Single(target.Characters);
        Assert.Equal(mine.Id, Assert.Single(target.OwnedCopies, c => c.ItemKey == "Chains|8|Belt").CharacterId);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Import_WhenProcessingFails_LeavesDataUnchanged()
    {
        var target = Sample();
        var before = Snapshot(target);
        var backup = new TrackerBackup { Characters = [null!], OwnedCopies = [] };

        Assert.ThrowsAny<Exception>(() => TrackerBackupService.Import(target, backup, ImportMode.Replace));

        Assert.Equal(before, Snapshot(target));
    }

    [Fact]
    public void Parse_DropsNullRecordsAndFillsMissingIds()
    {
        var backup = TrackerBackupService.Parse("""
            {"SchemaVersion":1,"Characters":[null,{"Id":null,"Server":"Cormyr","Name":"NoId"}],"OwnedCopies":[null]}
            """);

        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(backup.Characters).Id));
        Assert.Empty(backup.OwnedCopies!);
    }

    [Fact]
    public void RoundTrip_KeepsCopiesWithNoLocation()
    {
        var source = new TrackerData();
        TrackerOperations.AddCopy(source, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A" });
        var target = new TrackerData();

        var result = TrackerBackupService.Import(target, TrackerBackupService.Parse(TrackerBackupService.Export(source, Now)), ImportMode.Replace);

        Assert.Equal(new BackupImportResult(1, 0, 0), result);
        var copy = Assert.Single(target.OwnedCopies);
        Assert.Null(copy.Server);
        Assert.Null(copy.Storage);
    }
}
