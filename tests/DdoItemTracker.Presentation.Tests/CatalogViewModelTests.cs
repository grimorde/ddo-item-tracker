using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class CatalogViewModelTests
{
    private static CatalogViewModel Create(SessionFixture f)
    {
        var vm = new CatalogViewModel(f.Session, f.Navigator, f.Dialogs, f.CatalogUpdates());
        vm.Activate();
        return vm;
    }

    private static void AddSharedCopy(SessionFixture f, string key) =>
        f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = key, ItemName = "x", Server = "Cormyr", Storage = StorageType.SharedBank }));

    [Fact]
    public void Activate_ListsEveryItem()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        Assert.Equal(6, vm.Results.Count);
        Assert.Equal("6 items", vm.Summary);
        Assert.Equal("ML 18 · Gloves · Vecna Unleashed", vm.Results.Single(r => r.Key == Keys.Gauntlet).Subtitle);
    }

    [Fact]
    public void OptionLists_StartWithAny()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        Assert.Equal("Any", vm.Filters.Slots[0]);
        Assert.Contains("Gloves", vm.Filters.Slots);
        Assert.Equal(["Any", "Cormyr", "Moonsea", "Shadowdale", "Thrane"], vm.Filters.OwnershipServers);
        Assert.Equal(["All", "Owned", "Not owned"], vm.Filters.OwnershipOptions);
    }

    [Fact]
    public void Search_TrimsAndIgnoresCase_AndUpdatesSummary()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Filters.SearchText = "  chains ";
        Assert.Equal(2, vm.Results.Count);
        Assert.Equal("2 of 6 items", vm.Summary);
    }

    [Fact]
    public void PickerFilters_Apply()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Filters.SelectedSlot = "Necklace";
        Assert.Equal([Keys.Pendant, Keys.ChainsNecklace], vm.Results.Select(r => r.Key));
        vm.Filters.SelectedSlot = "Any";
        vm.Filters.SelectedPack = "Vecna Unleashed";
        Assert.Equal(2, vm.Results.Count);
        vm.Filters.SelectedPack = null; // a Picker can clear its selection
        vm.Filters.ArtifactOnly = true;
        Assert.Equal([Keys.Relic], vm.Results.Select(r => r.Key));
    }

    [Fact]
    public void LevelText_IgnoresAnythingNotANumber()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Filters.MinLevelText = "11";
        vm.Filters.MaxLevelText = "18";
        Assert.Equal(3, vm.Results.Count);
        vm.Filters.MaxLevelText = "abc";
        Assert.Equal(4, vm.Results.Count);
    }

    [Fact]
    public void OwnedFilterAndBadge_FollowTheSession()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Filters.SelectedOwnership = "Owned";
        Assert.Empty(vm.Results);

        AddSharedCopy(f, Keys.ChainsBelt);
        AddSharedCopy(f, Keys.ChainsBelt);

        var row = Assert.Single(vm.Results);
        Assert.Equal("×2", row.Badge);
        vm.Filters.SelectedOwnershipServer = "Thrane";
        Assert.Empty(vm.Results);
    }

    [Fact]
    public void Deactivated_DoesNotRefresh()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Deactivate();
        AddSharedCopy(f, Keys.ChainsBelt);
        Assert.Equal(0, vm.Results.Single(r => r.Key == Keys.ChainsBelt).OwnedCount);
    }

    [Fact]
    public void Clear_ResetsEveryFilter()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        vm.Filters.SearchText = "zzz";
        vm.Filters.SelectedSlot = "Belt";
        vm.Filters.InSetOnly = true;
        vm.Filters.SelectedOwnership = "Owned";

        vm.Filters.ClearCommand.Execute(null);

        Assert.Equal(new Core.Filtering.ItemFilter(), vm.Filters.BuildFilter());
        Assert.Equal(6, vm.Results.Count);
    }

    [Fact]
    public async Task OpenItem_Navigates()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        await vm.OpenItemCommand.ExecuteAsync(vm.Results[0]);
        Assert.Equal([$"item:{vm.Results[0].Key}"], f.Navigator.Calls);
    }

    [Fact]
    public async Task OnAppearing_WithHealthyData_ShowsNoAlert()
    {
        using var f = new SessionFixture();
        var vm = new CatalogViewModel(f.Session, f.Navigator, f.Dialogs, f.CatalogUpdates());
        await vm.OnAppearingAsync();
        Assert.Empty(f.Dialogs.Alerts);
        Assert.Equal(6, vm.Results.Count);
    }

    [Fact]
    public void OwnedCountChange_UpdatesTheRowInPlace()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        var list = vm.Results;

        AddSharedCopy(f, Keys.ChainsBelt);

        Assert.Same(list, vm.Results);
        Assert.Equal("×1", vm.Results.Single(r => r.Key == Keys.ChainsBelt).Badge);
    }

    [Fact]
    public void Reactivating_WithNothingChanged_KeepsTheSameList()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        var list = vm.Results;
        vm.Deactivate();
        vm.Activate();
        Assert.Same(list, vm.Results);
    }
}
