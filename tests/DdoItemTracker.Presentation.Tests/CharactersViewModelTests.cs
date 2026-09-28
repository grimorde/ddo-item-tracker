using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class CharactersViewModelTests
{
    private const string LifeTrackerBackup = """
        {"SchemaVersion":2,"Characters":[
          {"Id":"a1","Server":"Cormyr","Name":"Grimorde","FolderId":"f1","HeroicPastLives":{"Fighter":3}},
          {"Id":"a2","Server":"thrane ","Name":"Alt Two"},
          {"Id":"a3","Server":"Lamannia","Name":"Tester"}],"Folders":[{"Id":"f1","Name":"Mains"}]}
        """;

    private static CharactersViewModel Create(SessionFixture f)
    {
        var vm = new CharactersViewModel(f.Session, f.Dialogs, f.Files);
        vm.Activate();
        return vm;
    }

    private static (Character Grim, Character Alt) AddTwo(SessionFixture f)
    {
        var grim = f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var alt = f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Thrane", "Alt"));
        f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = Keys.ChainsBelt, ItemName = "Chains", Server = "Cormyr", CharacterId = grim.Id, Storage = StorageType.Bank }));
        return (grim, alt);
    }

    private static CharacterRow Row(CharactersViewModel vm, string name) => vm.Groups.SelectMany(g => g).Single(r => r.Name == name);

    [Fact]
    public void Characters_AreGroupedByServer_WithCopyCount()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);

        Assert.False(vm.IsEmpty);
        Assert.Equal(["Cormyr", "Thrane"], vm.Groups.Select(g => g.Server));
        Assert.Equal("1 copy", Row(vm, "Grimorde").Details);
        Assert.Equal("0 copies", Row(vm, "Alt").Details);
    }

    [Fact]
    public async Task AddCharacter_AsksServerThenName()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        Assert.True(vm.IsEmpty);
        f.Dialogs.ChooseAnswers.Enqueue("Moonsea");
        f.Dialogs.PromptAnswers.Enqueue(" Keeper ");

        await vm.AddCharacterCommand.ExecuteAsync(null);

        Assert.Equal(Servers.All, f.Dialogs.ChooseOptions[0]);
        Assert.Equal(("Moonsea", "Keeper"), (Row(vm, "Keeper").Server, Row(vm, "Keeper").Name));
    }

    [Fact]
    public async Task AddCharacter_Cancelled_AddsNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Dialogs.ChooseAnswers.Enqueue(null);
        await vm.AddCharacterCommand.ExecuteAsync(null);
        Assert.Empty(f.Session.Data.Characters);
    }

    [Fact]
    public async Task AddCharacter_DuplicateName_ExplainsWhy()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.ChooseAnswers.Enqueue("Cormyr");
        f.Dialogs.PromptAnswers.Enqueue("grimorde");

        await vm.AddCharacterCommand.ExecuteAsync(null);

        Assert.Equal(("Can't do that", "There is already a character called grimorde on Cormyr."), Assert.Single(f.Dialogs.Alerts));
    }

    [Fact]
    public async Task Rename_UsesPrompt()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.PromptAnswers.Enqueue("Alt Two");
        await vm.RenameCommand.ExecuteAsync(Row(vm, "Alt"));
        Assert.Contains(f.Session.Data.Characters, c => c.Name == "Alt Two");
    }

    [Fact]
    public async Task Delete_WithoutCopies_JustConfirms()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.ConfirmAnswers.Enqueue(true);
        await vm.DeleteCommand.ExecuteAsync(Row(vm, "Alt"));
        Assert.DoesNotContain(f.Session.Data.Characters, c => c.Name == "Alt");
    }

    [Fact]
    public async Task Delete_WithCopies_CanKeepThemOnTheServer()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.ChooseAnswers.Enqueue("Keep them, recorded on Cormyr only");

        await vm.DeleteCommand.ExecuteAsync(Row(vm, "Grimorde"));

        Assert.DoesNotContain(f.Session.Data.Characters, c => c.Name == "Grimorde");
        var copy = Assert.Single(f.Session.Data.OwnedCopies);
        Assert.Equal(("Cormyr", (string?)null, (StorageType?)null), (copy.Server, copy.CharacterId, copy.Storage));
    }

    [Fact]
    public async Task Delete_WithCopies_CanDeleteThemToo()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.ChooseAnswers.Enqueue("Delete them too");
        await vm.DeleteCommand.ExecuteAsync(Row(vm, "Grimorde"));
        Assert.Empty(f.Session.Data.OwnedCopies);
    }

    [Fact]
    public async Task Delete_WithCopies_Cancelled_ChangesNothing()
    {
        using var f = new SessionFixture();
        AddTwo(f);
        var vm = Create(f);
        f.Dialogs.ChooseAnswers.Enqueue(null);

        await vm.DeleteCommand.ExecuteAsync(Row(vm, "Grimorde"));

        Assert.Equal(2, f.Session.Data.Characters.Count);
        Assert.Equal((StorageType?)StorageType.Bank, Assert.Single(f.Session.Data.OwnedCopies).Storage);
    }

    [Fact]
    public async Task ImportFromLifeTracker_PreviewsThenImports()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Files.TextToPick = LifeTrackerBackup;
        f.Dialogs.ConfirmAnswers.Enqueue(true);

        await vm.ImportFromLifeTrackerCommand.ExecuteAsync(null);

        var preview = Assert.Single(f.Dialogs.Asked);
        Assert.Contains("2 new, 0 already here, 1 skipped.", preview);
        Assert.Contains("- Tester (Lamannia): Server \"Lamannia\" isn't supported.", preview);
        Assert.Equal(2, f.Session.Data.Characters.Count);
        Assert.Equal(("Import complete", "2 added, 0 updated, 1 skipped."), Assert.Single(f.Dialogs.Alerts));
    }

    [Fact]
    public async Task ImportFromLifeTracker_WrongFile_ExplainsAndChangesNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Files.TextToPick = """{"SchemaVersion":1,"Characters":[],"OwnedCopies":[]}""";

        await vm.ImportFromLifeTrackerCommand.ExecuteAsync(null);

        Assert.Equal("This is a DDO Item Tracker backup. Use Restore backup in Settings instead.", Assert.Single(f.Dialogs.Alerts).Message);
        Assert.Empty(f.Dialogs.Asked);
        Assert.Empty(f.Session.Data.Characters);
    }

    [Fact]
    public async Task ImportFromLifeTracker_PickCancelled_DoesNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        await vm.ImportFromLifeTrackerCommand.ExecuteAsync(null);
        Assert.Empty(f.Dialogs.Alerts);
        Assert.Empty(f.Dialogs.Asked);
    }

    [Fact]
    public async Task ImportFromLifeTracker_UnreadableFile_ExplainsAndChangesNothing()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        f.Files.PickFailure = new UnauthorizedAccessException("Access denied.");

        await vm.ImportFromLifeTrackerCommand.ExecuteAsync(null);

        Assert.Equal(("Import from DDO Life Tracker", "That file couldn't be opened. Access denied."), Assert.Single(f.Dialogs.Alerts));
        Assert.Empty(f.Session.Data.Characters);
    }
}
