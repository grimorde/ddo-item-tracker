using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;

namespace DdoItemTracker.Presentation.Tests;

public class SettingsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static SettingsViewModel Create(SessionFixture f)
    {
        var vm = new SettingsViewModel(f.Session, f.Dialogs, f.Files, new FixedClock(Now), f.CatalogUpdates());
        vm.Activate();
        return vm;
    }

    private static string BackupWithOneCharacter()
    {
        var other = new TrackerData();
        TrackerOperations.AddCharacter(other, "Moonsea", "Keeper");
        return TrackerBackupService.Export(other, Now);
    }

    [Fact]
    public void CatalogVersion_IsShown()
    {
        using var f = new SessionFixture();
        Assert.Equal("Catalog from 27 Sep 2026 (83bc99b, built-in), 6 items", Create(f).CatalogVersionText);
    }

    [Fact]
    public async Task ExportBackup_SavesAReadableBackup()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var vm = Create(f);

        await vm.ExportBackupCommand.ExecuteAsync(null);

        var (name, content) = Assert.Single(f.Files.Saved);
        Assert.Equal("ddoitemtracker-backup-20260928-120000.json", name);
        Assert.Equal("Grimorde", Assert.Single(TrackerBackupService.Parse(content).Characters).Name);
        Assert.Equal(("Backup saved", "Saved to:\nC:/backups/file.json"), Assert.Single(f.Dialogs.Alerts));
    }

    [Fact]
    public async Task ExportBackup_Cancelled_SaysNothing()
    {
        using var f = new SessionFixture();
        f.Files.SavePathToReturn = null;
        await Create(f).ExportBackupCommand.ExecuteAsync(null);
        Assert.Empty(f.Dialogs.Alerts);
    }

    [Fact]
    public async Task ExportBackup_Failure_IsReported()
    {
        using var f = new SessionFixture();
        f.Files.SaveFailure = new IOException("Disk full");
        await Create(f).ExportBackupCommand.ExecuteAsync(null);
        Assert.Equal(("Backup failed", "Disk full"), Assert.Single(f.Dialogs.Alerts));
    }

    [Fact]
    public async Task ShareBackup_SharesTheFile()
    {
        using var f = new SessionFixture();
        await Create(f).ShareBackupCommand.ExecuteAsync(null);
        Assert.Equal("ddoitemtracker-backup-20260928-120000.json", Assert.Single(f.Files.Shared).Name);
    }

    [Fact]
    public async Task RestoreBackup_Merge_AddsRecords()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var vm = Create(f);
        f.Files.TextToPick = BackupWithOneCharacter();
        f.Dialogs.ChooseAnswers.Enqueue(SettingsViewModel.MergeOption);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(2, f.Session.Data.Characters.Count);
        Assert.Equal(("Restore complete", "1 added, 0 updated, 0 skipped."), Assert.Single(f.Dialogs.Alerts));
    }

    [Fact]
    public async Task RestoreBackup_ReplaceDeclined_ChangesNothing()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var vm = Create(f);
        f.Files.TextToPick = BackupWithOneCharacter();
        f.Dialogs.ChooseAnswers.Enqueue(SettingsViewModel.ReplaceOption);
        f.Dialogs.ConfirmAnswers.Enqueue(false);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal("Grimorde", Assert.Single(f.Session.Data.Characters).Name);
    }

    [Fact]
    public async Task RestoreBackup_Replace_ReplacesEverything()
    {
        using var f = new SessionFixture();
        f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var vm = Create(f);
        f.Files.TextToPick = BackupWithOneCharacter();
        f.Dialogs.ChooseAnswers.Enqueue(SettingsViewModel.ReplaceOption);
        f.Dialogs.ConfirmAnswers.Enqueue(true);

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal("Keeper", Assert.Single(f.Session.Data.Characters).Name);
    }

    [Fact]
    public async Task RestoreBackup_LifeTrackerFile_ExplainsAndChangesNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Files.TextToPick = """{"SchemaVersion":2,"Characters":[{"Id":"a1","Server":"Cormyr","Name":"Grimorde"}],"Folders":[]}""";

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.StartsWith("This file isn't a DDO Item Tracker backup.", Assert.Single(f.Dialogs.Alerts).Message);
        Assert.Empty(f.Dialogs.Asked);
        Assert.Empty(f.Session.Data.Characters);
    }

    [Fact]
    public async Task RestoreBackup_UnreadableFile_ExplainsAndChangesNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Files.PickFailure = new IOException("The file is locked.");

        await vm.RestoreBackupCommand.ExecuteAsync(null);

        Assert.Equal(("Restore backup", "That file couldn't be opened. The file is locked."), Assert.Single(f.Dialogs.Alerts));
        Assert.Empty(f.Dialogs.Asked);
    }
}
