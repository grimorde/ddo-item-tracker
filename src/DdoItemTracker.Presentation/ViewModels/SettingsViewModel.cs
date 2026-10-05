using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Import;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

public partial class SettingsViewModel(TrackerSession session, IDialogService dialogs, IFileService files, TimeProvider clock,
    CatalogUpdateCoordinator catalogUpdates)
    : SessionViewModel(session, dialogs)
{
    public const string MergeOption = "Merge with what's here";
    public const string ReplaceOption = "Replace everything here";

    [ObservableProperty] private string _catalogVersionText = string.Empty;

    public override void Refresh()
    {
        var version = Session.Catalog.Catalog.Version;
        var commit = version.UpstreamCommit.Length > 7 ? version.UpstreamCommit[..7] : version.UpstreamCommit;
        var date = version.UpstreamCommitDateUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var source = Session.CatalogIsDownloaded ? "updated" : "built-in";
        CatalogVersionText = $"Catalog from {date} ({commit}, {source}), {Session.Catalog.Catalog.Items.Count:N0} items";
    }

    [RelayCommand]
    private Task CheckForCatalogUpdate() => catalogUpdates.CheckAsync(userInitiated: true);

    [RelayCommand]
    private Task ResetCatalog() => catalogUpdates.ResetToBuiltInAsync();

    public string BackupFileName() =>
        $"ddoitemtracker-backup-{clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.json";

    [RelayCommand]
    private async Task ExportBackup()
    {
        try
        {
            var path = await files.SaveTextAsync(BackupFileName(), TrackerBackupService.Export(Session.Data, clock.GetUtcNow()));
            if (path is not null) await Dialogs.AlertAsync("Backup saved", $"Saved to:\n{path}");
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Backup failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ShareBackup()
    {
        try
        {
            await files.ShareTextAsync(BackupFileName(), TrackerBackupService.Export(Session.Data, clock.GetUtcNow()), "Share DDO Item Tracker backup");
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Share failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RestoreBackup()
    {
        var text = await PickTextAsync(files, "Choose a DDO Item Tracker backup", "Restore backup");
        if (text is null) return;

        TrackerBackup backup;
        try
        {
            backup = TrackerBackupService.Parse(text);
        }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Restore backup", ex.Message);
            return;
        }

        var choice = await Dialogs.ChooseAsync("How should the backup be restored?", [MergeOption, ReplaceOption]);
        if (choice is null) return;
        var mode = choice == ReplaceOption ? ImportMode.Replace : ImportMode.Merge;
        if (mode == ImportMode.Replace && !await Dialogs.ConfirmAsync("Replace everything?",
                "This replaces all characters and items on this device with the backup. Continue?", "Replace", "Cancel"))
            return;

        BackupImportResult? result = null;
        if (await TryApplyAsync(d => result = TrackerBackupService.Import(d, backup, mode)) && result is not null)
            await Dialogs.AlertAsync("Restore complete", $"{result.Added} added, {result.Updated} updated, {result.Skipped} skipped.");
    }
}
