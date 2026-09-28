using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Presentation.State;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class CopyEditorViewModelTests
{
    private static CopyEditorViewModel Open(SessionFixture f, string key = Keys.ChainsBelt, string? copyId = null, TrackerSession? session = null)
    {
        var vm = new CopyEditorViewModel(session ?? f.Session, f.Navigator, f.Dialogs, f.Settings);
        vm.Load(key, copyId);
        return vm;
    }

    private static (Character Grim, Character Alt) AddCharacters(SessionFixture f) =>
        (f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde")),
         f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Thrane", "Alt")));

    private static HeldInOption Held(CopyEditorViewModel vm, string label) => vm.HeldInOptions.Single(o => o.Label == label);

    [Fact]
    public async Task NewCopy_StartsWithNoLocation_AndSavesThat()
    {
        using var f = new SessionFixture();
        var vm = Open(f);

        Assert.Equal(("Add copy", "Chains", "Not recorded"), (vm.Title, vm.ItemName, vm.SelectedServer));
        Assert.Equal(["Not recorded", "Cormyr", "Moonsea", "Shadowdale", "Thrane"], vm.ServerOptions);
        Assert.False(vm.ShowHeldIn);
        Assert.False(vm.ShowStorage);
        Assert.True(vm.CanSave);

        await vm.SaveCommand.ExecuteAsync(null);

        var copy = Assert.Single(f.Session.Data.OwnedCopies);
        Assert.Equal(((string?)null, (string?)null, (StorageType?)null), (copy.Server, copy.CharacterId, copy.Storage));
        Assert.Equal(["back"], f.Navigator.Calls);
    }

    [Fact]
    public async Task ServerOnly_AndSharedBank_AreSaved()
    {
        using var f = new SessionFixture();
        var vm = Open(f);
        vm.SelectedServer = "Thrane";

        Assert.True(vm.ShowHeldIn);
        Assert.Equal(["Not recorded", "Shared Bank"], vm.HeldInOptions.Select(o => o.Label));
        Assert.False(vm.ShowStorage);
        await vm.SaveCommand.ExecuteAsync(null);

        var shared = Open(f);
        shared.SelectedServer = "Thrane";
        shared.SelectedHeldIn = CopyEditorViewModel.HeldSharedBank;
        await shared.SaveCommand.ExecuteAsync(null);

        Assert.Equal(
            [("Thrane", (string?)null, (StorageType?)null), ("Thrane", null, StorageType.SharedBank)],
            f.Session.Data.OwnedCopies.Select(c => (c.Server, c.CharacterId, c.Storage)));
    }

    [Fact]
    public async Task Character_WithStorage_IsSavedAndRemembered()
    {
        using var f = new SessionFixture();
        var (grim, _) = AddCharacters(f);
        var vm = Open(f);
        vm.SelectedServer = "Cormyr";
        vm.SelectedHeldIn = Held(vm, "Grimorde");

        Assert.True(vm.ShowStorage);
        Assert.Equal(["Not recorded", "Inventory", "Bank"], vm.StorageOptions);
        vm.SelectedStorage = "Bank";
        vm.Note = " swap set ";
        await vm.SaveCommand.ExecuteAsync(null);

        var copy = Assert.Single(f.Session.Data.OwnedCopies);
        Assert.Equal(("Cormyr", grim.Id, (StorageType?)StorageType.Bank, "swap set"), (copy.Server, copy.CharacterId, copy.Storage, copy.Note));
        Assert.Equal(("Cormyr", grim.Id, "Bank"), (f.Settings.LastServer, f.Settings.LastHeldIn, f.Settings.LastStorage));
    }

    [Fact]
    public void NewCopy_StartsFromRememberedLocation()
    {
        using var f = new SessionFixture();
        var (_, alt) = AddCharacters(f);
        f.Settings.LastServer = "Thrane";
        f.Settings.LastHeldIn = alt.Id;
        f.Settings.LastStorage = "Inventory";

        var vm = Open(f);

        Assert.Equal(("Thrane", "Alt", "Inventory"), (vm.SelectedServer, vm.SelectedHeldIn?.Label, vm.SelectedStorage));
    }

    [Fact]
    public async Task SwitchingServer_DropsACharacterFromTheOldServer()
    {
        using var f = new SessionFixture();
        AddCharacters(f);
        var vm = Open(f);
        vm.SelectedServer = "Cormyr";
        vm.SelectedHeldIn = Held(vm, "Grimorde");
        vm.SelectedStorage = "Inventory";

        vm.SelectedServer = "Thrane";

        Assert.Equal(CopyEditorViewModel.HeldNotRecorded, vm.SelectedHeldIn);
        Assert.Contains(vm.HeldInOptions, o => o.Label == "Alt");
        Assert.False(vm.ShowStorage);
        await vm.SaveCommand.ExecuteAsync(null);
        var copy = Assert.Single(f.Session.Data.OwnedCopies);
        Assert.Equal(("Thrane", (string?)null, (StorageType?)null), (copy.Server, copy.CharacterId, copy.Storage));
    }

    [Fact]
    public void SwitchingServer_KeepsSharedBank()
    {
        using var f = new SessionFixture();
        var vm = Open(f);
        vm.SelectedServer = "Cormyr";
        vm.SelectedHeldIn = CopyEditorViewModel.HeldSharedBank;
        vm.SelectedServer = "Moonsea";
        Assert.Equal(CopyEditorViewModel.HeldSharedBank, vm.SelectedHeldIn);
    }

    [Fact]
    public async Task EditCopy_AddsDetailToAnUnlocatedCopy()
    {
        using var f = new SessionFixture();
        var (grim, _) = AddCharacters(f);
        var original = f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = Keys.ChainsBelt, ItemName = "Chains", Note = "old" }));
        var vm = Open(f, copyId: original.Id);
        Assert.Equal(("Edit copy", "Not recorded", "old"), (vm.Title, vm.SelectedServer, vm.Note));

        vm.SelectedServer = "Cormyr";
        vm.SelectedHeldIn = Held(vm, "Grimorde");
        await vm.SaveCommand.ExecuteAsync(null);

        var copy = Assert.Single(f.Session.Data.OwnedCopies);
        Assert.Equal((original.Id, "Cormyr", grim.Id, (StorageType?)null, original.AddedUtc), (copy.Id, copy.Server, copy.CharacterId, copy.Storage, copy.AddedUtc));
    }

    [Fact]
    public void EditCopy_LoadsEveryLevel()
    {
        using var f = new SessionFixture();
        var (grim, _) = AddCharacters(f);
        var copy = f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = Keys.ChainsBelt, ItemName = "Chains", Server = "Cormyr", CharacterId = grim.Id, Storage = StorageType.Inventory }));
        var vm = Open(f, copyId: copy.Id);
        Assert.Equal(("Cormyr", "Grimorde", "Inventory"), (vm.SelectedServer, vm.SelectedHeldIn?.Label, vm.SelectedStorage));
    }

    [Fact]
    public async Task SaveFailure_AlertsAndStaysOnForm()
    {
        using var f = new SessionFixture();
        Directory.CreateDirectory(f.Directory);
        var notAFolder = Path.Combine(f.Directory, "blocked");
        File.WriteAllText(notAFolder, "this is a file, so the store cannot create its folder here");
        var session = new TrackerSession(new TrackerStore(notAFolder), SampleCatalog.Create());
        session.Load();
        var vm = Open(f, session: session);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't save", Assert.Single(f.Dialogs.Alerts).Title);
        Assert.Empty(f.Navigator.Calls);
        Assert.Empty(session.Data.OwnedCopies);
    }

    [Fact]
    public async Task Cancel_GoesBackWithoutSaving()
    {
        using var f = new SessionFixture();
        var vm = Open(f);
        await vm.CancelCommand.ExecuteAsync(null);
        Assert.Equal(["back"], f.Navigator.Calls);
        Assert.Empty(f.Session.Data.OwnedCopies);
    }
}
