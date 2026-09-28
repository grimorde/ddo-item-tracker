using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Import;

public class LifeTrackerImportTests
{
    // Shape written by DDO Life Tracker 1.4.x (BackupPayload, schema 2, PascalCase, past lives included).
    private const string Backup = """
        {
          "SchemaVersion": 2,
          "ExportedUtc": "2026-09-01T10:00:00Z",
          "Characters": [
            { "Id": "a1", "Server": "Cormyr", "Name": "Grimorde", "FolderId": "f1", "HeroicPastLives": { "Fighter": 3 } },
            { "Id": "a2", "Server": "thrane ", "Name": "Alt Two" },
            { "Id": "a3", "Server": "Lamannia", "Name": "Tester" }
          ],
          "Folders": [ { "Id": "f1", "Name": "Mains" }, { "Id": "f2", "Name": "Unused" } ]
        }
        """;

    [Fact]
    public void Parse_ReadsCharacters_IgnoringFoldersAndPastLives()
    {
        var backup = LifeTrackerBackupReader.Parse(Backup);
        Assert.Equal(3, backup.Characters.Count);
        Assert.Equal(new LifeTrackerCharacter("a1", "Cormyr", "Grimorde"), backup.Characters[0]);
    }

    [Fact]
    public void Parse_AcceptsLegacyBareCharacterList()
    {
        var backup = LifeTrackerBackupReader.Parse("""[{ "Id": "a1", "Server": "Cormyr", "Name": "Grimorde" }]""");
        Assert.Single(backup.Characters);
    }

    [Fact]
    public void Parse_OurOwnBackup_PointsToRestore()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            LifeTrackerBackupReader.Parse("""{"SchemaVersion":1,"Characters":[],"Folders":[],"OwnedCopies":[]}"""));
        Assert.Equal("This is a DDO Item Tracker backup. Use Restore backup in Settings instead.", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"Characters":[{"Foo":1}]}""")]
    [InlineData("""{"items":[]}""")]
    public void Parse_NotALifeTrackerFile_Throws(string json)
    {
        var ex = Assert.Throws<InvalidDataException>(() => LifeTrackerBackupReader.Parse(json));
        Assert.Equal(LifeTrackerBackupReader.NotALifeTrackerBackup, ex.Message);
    }

    [Fact]
    public void Plan_IntoEmptyData_AddsSupportedServers_AndCanonicalisesServerCase()
    {
        var plan = LifeTrackerImporter.Plan(new TrackerData(), LifeTrackerBackupReader.Parse(Backup));

        Assert.Equal(2, plan.AddCount);
        Assert.Equal(0, plan.UpdateCount);
        Assert.Equal(1, plan.SkipCount);
        Assert.Equal(LifeTrackerImportAction.SkipUnknownServer, plan.Entries.Single(e => e.Source.Id == "a3").Action);
        Assert.Equal(LifeTrackerImportAction.Add, plan.Entries.Single(e => e.Source.Id == "a2").Action);
    }

    [Fact]
    public void Plan_DoesNotChangeData()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Plan(data, LifeTrackerBackupReader.Parse(Backup));
        Assert.Empty(data.Characters);
    }

    [Fact]
    public void Apply_AddsCharactersWithLifeTrackerId()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        Assert.Equal("a1", data.Characters.Single(c => c.Name == "Grimorde").LifeTrackerId);
        Assert.Equal("Thrane", data.Characters.Single(c => c.Name == "Alt Two").Server);
    }

    [Fact]
    public void Apply_Twice_UpdatesInsteadOfDuplicating()
    {
        var data = new TrackerData();
        var backup = LifeTrackerBackupReader.Parse(Backup);
        LifeTrackerImporter.Apply(data, backup);

        var second = LifeTrackerImporter.Apply(data, backup);

        Assert.Equal(2, data.Characters.Count);
        Assert.Equal(2, second.UpdateCount);
    }

    [Fact]
    public void ExistingCharacterWithSameName_IsMatchedAndKeepsItsId()
    {
        var data = new TrackerData();
        var existing = TrackerOperations.AddCharacter(data, "Cormyr", "grimorde");

        var plan = LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        Assert.Equal(existing.Id, plan.Entries.Single(e => e.Source.Id == "a1").ExistingCharacterId);
        Assert.Equal("Grimorde", existing.Name);
        Assert.Equal("a1", existing.LifeTrackerId);
    }

    [Fact]
    public void RenameInLifeTracker_IsFollowedByLifeTrackerId()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));

        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Grimorde Reborn"}],"Folders":[]}"""));

        Assert.Contains(data.Characters, c => c.Name == "Grimorde Reborn" && c.LifeTrackerId == "a1");
        Assert.DoesNotContain(data.Characters, c => c.Name == "Grimorde");
    }

    [Fact]
    public void RenameOntoAnotherCharactersName_IsSkipped()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(Backup));
        TrackerOperations.AddCharacter(data, "Cormyr", "Taken");

        var plan = LifeTrackerImporter.Plan(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Taken"}]}"""));

        Assert.Equal(LifeTrackerImportAction.SkipInvalid, Assert.Single(plan.Entries).Action);
    }

    [Fact]
    public void DuplicateInFile_AndBlankName_AreSkipped()
    {
        const string json = """
            {"Characters":[
              {"Id":"x1","Server":"Moonsea","Name":"Twin"},
              {"Id":"x2","Server":"Moonsea","Name":"twin "},
              {"Id":"x3","Server":"Moonsea","Name":"  "}]}
            """;
        var plan = LifeTrackerImporter.Plan(new TrackerData(), LifeTrackerBackupReader.Parse(json));
        Assert.Equal(1, plan.AddCount);
        Assert.Equal(2, plan.SkipCount);
    }

    private static TrackerData WithAliceAsL1()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"L1","Server":"Cormyr","Name":"Alice"}]}"""));
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = data.Characters[0].Id });
        return data;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RenamedCharacterAndNewNamesake_AreResolvedById_InEitherFileOrder(bool newcomerFirst)
    {
        var data = WithAliceAsL1();
        var alice = data.Characters[0];
        const string renamed = """{"Id":"L1","Server":"Cormyr","Name":"Bob"}""";
        const string newcomer = """{"Id":"L2","Server":"Cormyr","Name":"Alice"}""";
        var json = $$"""{"Characters":[{{(newcomerFirst ? newcomer : renamed)}},{{(newcomerFirst ? renamed : newcomer)}}]}""";

        var plan = LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse(json));

        Assert.Equal(0, plan.SkipCount);
        Assert.Equal("Bob", alice.Name);
        Assert.Equal("L1", alice.LifeTrackerId);
        Assert.Equal("Alice", Assert.Single(data.Characters, c => c.LifeTrackerId == "L2").Name);
        Assert.Equal(alice.Id, Assert.Single(data.OwnedCopies).CharacterId);
    }

    [Fact]
    public void SwappedNames_AreBothApplied()
    {
        var data = new TrackerData();
        LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"L1","Server":"Cormyr","Name":"Alice"},{"Id":"L2","Server":"Cormyr","Name":"Bob"}]}"""));

        var plan = LifeTrackerImporter.Apply(data, LifeTrackerBackupReader.Parse("""{"Characters":[{"Id":"L1","Server":"Cormyr","Name":"Bob"},{"Id":"L2","Server":"Cormyr","Name":"Alice"}]}"""));

        Assert.Equal(2, plan.UpdateCount);
        Assert.Equal("Bob", data.Characters.Single(c => c.LifeTrackerId == "L1").Name);
        Assert.Equal("Alice", data.Characters.Single(c => c.LifeTrackerId == "L2").Name);
    }
}
