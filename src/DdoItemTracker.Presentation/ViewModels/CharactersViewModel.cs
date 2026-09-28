using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Import;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

public sealed record CharacterRow(string Id, string Name, string Server, int CopyCount)
{
    public string Details => CopyCount == 1 ? "1 copy" : $"{CopyCount} copies";
}

public sealed class CharacterGroup(string server, IEnumerable<CharacterRow> rows) : List<CharacterRow>(rows)
{
    public string Server { get; } = server;
}

/// <summary>The character pick-list, managed from Settings.</summary>
public partial class CharactersViewModel(TrackerSession session, IDialogService dialogs, IFileService files)
    : SessionViewModel(session, dialogs)
{
    private const string ImportTitle = "Import from DDO Life Tracker";

    [ObservableProperty] private IReadOnlyList<CharacterGroup> _groups = [];
    [ObservableProperty] private bool _isEmpty;

    public override void Refresh()
    {
        var data = Session.Data;
        var copyCounts = data.OwnedCopies.Where(c => c.CharacterId is not null)
            .GroupBy(c => c.CharacterId!).ToDictionary(g => g.Key, g => g.Count());
        var rows = data.Characters.Select(c => new CharacterRow(c.Id, c.Name, c.Server, copyCounts.GetValueOrDefault(c.Id))).ToList();
        Groups = Servers.All
            .Select(server => new CharacterGroup(server, rows.Where(r => r.Server == server).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)))
            .Where(g => g.Count > 0)
            .ToList();
        IsEmpty = data.Characters.Count == 0;
    }

    [RelayCommand]
    private async Task AddCharacter()
    {
        var server = await Dialogs.ChooseAsync("Which server is the character on?", Servers.All);
        if (server is null) return;
        var name = await Dialogs.PromptAsync("New character", $"Name of your character on {server}");
        if (name is null) return;
        await TryApplyAsync(d => TrackerOperations.AddCharacter(d, server, name));
    }

    [RelayCommand]
    private async Task Rename(CharacterRow row)
    {
        var name = await Dialogs.PromptAsync("Rename character", $"New name for {row.Name}", row.Name);
        if (name is null) return;
        await TryApplyAsync(d => TrackerOperations.RenameCharacter(d, row.Id, name));
    }

    [RelayCommand]
    private async Task Delete(CharacterRow row)
    {
        if (row.CopyCount == 0)
        {
            if (await Dialogs.ConfirmAsync("Delete character", $"Delete {row.Name} ({row.Server})?", "Delete", "Cancel"))
                await TryApplyAsync(d => TrackerOperations.DeleteCharacter(d, row.Id, CharacterCopyDisposal.DeleteCopies));
            return;
        }

        var keep = $"Keep them, recorded on {row.Server} only";
        const string delete = "Delete them too";
        var copies = row.CopyCount == 1 ? "1 copy" : $"{row.CopyCount} copies";
        var choice = await Dialogs.ChooseAsync($"{row.Name} holds {copies}. What should happen to them?", [keep, delete]);
        if (choice is null) return;
        var disposal = choice == keep ? CharacterCopyDisposal.KeepOnServer : CharacterCopyDisposal.DeleteCopies;
        await TryApplyAsync(d => TrackerOperations.DeleteCharacter(d, row.Id, disposal));
    }

    [RelayCommand]
    private async Task ImportFromLifeTracker()
    {
        var text = await PickTextAsync(files, "Choose a DDO Life Tracker backup", ImportTitle);
        if (text is null) return;

        LifeTrackerBackup backup;
        try
        {
            backup = LifeTrackerBackupReader.Parse(text);
        }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync(ImportTitle, ex.Message);
            return;
        }

        var plan = LifeTrackerImporter.Plan(Session.Data, backup);
        if (plan.AddCount + plan.UpdateCount == 0)
        {
            await Dialogs.AlertAsync("Nothing to import", PreviewText(plan));
            return;
        }
        if (!await Dialogs.ConfirmAsync(ImportTitle, PreviewText(plan), "Import", "Cancel")) return;

        LifeTrackerImportPlan? applied = null;
        if (await TryApplyAsync(d => applied = LifeTrackerImporter.Apply(d, backup)) && applied is not null)
            await Dialogs.AlertAsync("Import complete", $"{applied.AddCount} added, {applied.UpdateCount} updated, {applied.SkipCount} skipped.");
    }

    public static string PreviewText(LifeTrackerImportPlan plan)
    {
        var text = new StringBuilder($"{plan.AddCount} new, {plan.UpdateCount} already here, {plan.SkipCount} skipped.");
        var skipped = plan.Entries.Where(e => e.Reason is not null).ToList();
        if (skipped.Count == 0) return text.ToString();
        text.Append("\n\nSkipped:");
        foreach (var e in skipped.Take(5))
            text.Append($"\n- {e.Source.Name.Trim()} ({e.Source.Server.Trim()}): {e.Reason}");
        if (skipped.Count > 5) text.Append($"\n- and {skipped.Count - 5} more");
        return text.ToString();
    }
}
