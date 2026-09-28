using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class ItemDetailViewModelTests
{
    private static ItemDetailViewModel Open(SessionFixture f, string key)
    {
        var vm = new ItemDetailViewModel(f.Session, f.Navigator, f.Dialogs);
        vm.Load(key);
        return vm;
    }

    private static OwnedCopy AddCopy(SessionFixture f, string key, string name = "x", string server = "Cormyr") =>
        f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = key, ItemName = name, Server = server, Storage = StorageType.SharedBank }));

    [Fact]
    public void Load_ShowsItemDetails()
    {
        using var f = new SessionFixture();
        var vm = Open(f, Keys.Gauntlet);

        Assert.True(vm.IsInCatalog);
        Assert.Equal("Absorption Gauntlet", vm.Title);
        Assert.Equal("ML 18 · Gloves · Hand items · Vecna Unleashed", vm.Subtitle);
        Assert.Equal("Quest: Taken in Hand", vm.QuestsText);
        Assert.Equal(["Magical Sheltering +10 (Insight)", "Immunity to Fear"], vm.Effects);
        Assert.Equal(["Yellow Augment Slot"], vm.CraftingSlots);
        Assert.True(vm.HasWikiUrl);
    }

    [Fact]
    public void SetBlock_ShowsTiersMembersAndOwnership()
    {
        using var f = new SessionFixture();
        AddCopy(f, Keys.Buckler);
        var vm = Open(f, Keys.Gauntlet);

        var set = Assert.Single(vm.Sets);
        Assert.Equal("Forbidden Knowledge", set.Name);
        Assert.True(set.HasDetails);
        Assert.Equal(new SetTierLine("3 pieces", "Physical Sheltering +10 (Profane)"), set.Tiers[0]);
        Assert.Equal("Melee Power +5 (Profane)\nRanged Power +5 (Profane)", set.Tiers[1].Effects);
        Assert.Equal(["Absorption Gauntlet", "Azure Buckler"], set.Members.Select(m => m.Name));
        Assert.True(set.Members[0].IsCurrent);
        Assert.True(set.Members[1].IsOwned);
        Assert.Equal("You own 1 of 2 pieces", set.OwnedSummary);
    }

    [Fact]
    public void SetWithoutBonusData_SaysSo()
    {
        using var f = new SessionFixture();
        var set = Assert.Single(Open(f, Keys.Pendant).Sets);
        Assert.False(set.HasDetails);
        Assert.Empty(set.Tiers);
    }

    [Fact]
    public void Copies_ListLocations_AndRefreshWhileActive()
    {
        using var f = new SessionFixture();
        var vm = Open(f, Keys.ChainsBelt);
        vm.Activate();
        Assert.Equal("Your copies (0)", vm.CopiesHeader);

        AddCopy(f, Keys.ChainsBelt, server: "Thrane");

        Assert.Equal("Thrane · Shared Bank", Assert.Single(vm.Copies).Location);
        Assert.Equal("Your copies (1)", vm.CopiesHeader);
    }

    [Fact]
    public async Task AddAndEditCopy_Navigate()
    {
        using var f = new SessionFixture();
        var copy = AddCopy(f, Keys.ChainsBelt);
        var vm = Open(f, Keys.ChainsBelt);

        await vm.AddCopyCommand.ExecuteAsync(null);
        await vm.EditCopyCommand.ExecuteAsync(vm.Copies[0]);

        Assert.Equal([$"copy:{Keys.ChainsBelt}:", $"copy:{Keys.ChainsBelt}:{copy.Id}"], f.Navigator.Calls);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task DeleteCopy_AsksFirst(bool confirm, int remaining)
    {
        using var f = new SessionFixture();
        AddCopy(f, Keys.ChainsBelt);
        var vm = Open(f, Keys.ChainsBelt);
        f.Dialogs.ConfirmAnswers.Enqueue(confirm);

        await vm.DeleteCopyCommand.ExecuteAsync(vm.Copies[0]);

        Assert.Equal(remaining, f.Session.Data.OwnedCopies.Count);
        Assert.Equal(remaining, vm.Copies.Count);
    }

    [Fact]
    public async Task HaveOne_AddsACopyWithNoLocation()
    {
        using var f = new SessionFixture();
        var vm = Open(f, Keys.ChainsBelt);

        await vm.HaveOneCommand.ExecuteAsync(null);
        await vm.HaveOneCommand.ExecuteAsync(null);

        Assert.Equal(["Location not recorded", "Location not recorded"], vm.Copies.Select(c => c.Location));
        Assert.Equal("Your copies (2)", vm.CopiesHeader);
        var copy = f.Session.Data.OwnedCopies[0];
        Assert.Equal(("Chains", (string?)null), (copy.ItemName, copy.Server));
        Assert.Empty(f.Navigator.Calls);
    }

    [Fact]
    public void ItemNoLongerInCatalog_ShowsSavedNameAndCopies()
    {
        using var f = new SessionFixture();
        AddCopy(f, "Retired Ring|5|Ring", name: "Retired Ring");
        var vm = Open(f, "Retired Ring|5|Ring");

        Assert.False(vm.IsInCatalog);
        Assert.Equal("Retired Ring", vm.Title);
        Assert.Equal("This item is not in the current catalog. Your copies are kept.", vm.NotInCatalogMessage);
        Assert.Single(vm.Copies);
        Assert.Empty(vm.Effects);
        Assert.Empty(vm.Sets);
    }

    [Fact]
    public async Task OpenMember_NavigatesToOtherPiecesOnly()
    {
        using var f = new SessionFixture();
        var vm = Open(f, Keys.Gauntlet);
        var members = vm.Sets[0].Members;

        await vm.OpenMemberCommand.ExecuteAsync(members[0]); // the item itself
        await vm.OpenMemberCommand.ExecuteAsync(members[1]);

        Assert.Equal([$"item:{Keys.Buckler}"], f.Navigator.Calls);
    }

    [Fact]
    public async Task OpenWiki_OpensUrl()
    {
        using var f = new SessionFixture();
        var vm = Open(f, Keys.Gauntlet);
        await vm.OpenWikiCommand.ExecuteAsync(null);
        Assert.Equal(["url:https://ddowiki.com/page/Item:Absorption_Gauntlet"], f.Navigator.Calls);
    }
}
