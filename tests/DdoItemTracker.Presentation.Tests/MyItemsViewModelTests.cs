using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Tests.Support;
using DdoItemTracker.Presentation.ViewModels;
using static DdoItemTracker.Presentation.Tests.Support.SampleCatalog;

namespace DdoItemTracker.Presentation.Tests;

public class MyItemsViewModelTests
{
    private sealed record World(SessionFixture F, Character Grim, Character Alt);

    private static World Setup()
    {
        var f = new SessionFixture();
        var grim = f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Grimorde"));
        var alt = f.Session.Apply(d => TrackerOperations.AddCharacter(d, "Thrane", "Alt"));
        void Add(string key, string name, string? server = null, string? characterId = null, StorageType? storage = null) =>
            f.Session.Apply(d => TrackerOperations.AddCopy(d, new OwnedCopy { ItemKey = key, ItemName = name, Server = server, CharacterId = characterId, Storage = storage }));
        Add(Keys.Relic, "Relic");
        Add(Keys.Gauntlet, "Absorption Gauntlet", "Cormyr", storage: StorageType.SharedBank);
        Add(Keys.ChainsBelt, "Chains", "Cormyr", grim.Id, StorageType.Bank);
        Add(Keys.Pendant, "Adherent's Pendant", "Thrane", alt.Id);
        Add("Retired Ring|5|Ring", "Retired Ring", "Thrane");
        return new World(f, grim, alt);
    }

    private static MyItemsViewModel Create(SessionFixture f)
    {
        var vm = new MyItemsViewModel(f.Session, f.Navigator, f.Dialogs);
        vm.Activate();
        return vm;
    }

    [Fact]
    public void NoCopies_IsEmpty()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Groups);
        Assert.Equal("You own 0 of 6 named items", vm.Summary);
    }

    [Fact]
    public void Copies_AreGroupedByLocation_StartingWithNotRecorded()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);

        Assert.False(vm.IsEmpty);
        Assert.Equal(
            ["Location not recorded", "Cormyr · Shared Bank", "Cormyr · Grimorde · Bank", "Thrane", "Thrane · Alt"],
            vm.Groups.Select(g => g.Title));
        Assert.Equal("You own 4 of 6 named items", vm.Summary);
    }

    [Fact]
    public void FilterOptions_IncludeNotRecorded()
    {
        using var f = new SessionFixture();
        var vm = Create(f);
        Assert.Equal(["Any", "Not recorded", "Cormyr", "Moonsea", "Shadowdale", "Thrane"], vm.ServerOptions);
        Assert.Equal(["Any", "Not recorded", "Shared Bank", "Inventory", "Bank"], vm.StorageOptions);
    }

    [Fact]
    public void ServerNotRecorded_ShowsOnlyUnlocatedCopies()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);
        vm.SelectedServer = "Not recorded";
        Assert.Equal("Relic", Assert.Single(vm.Groups.SelectMany(g => g)).ItemName);
    }

    [Fact]
    public void ServerFilter_NarrowsGroupsAndCharacterChoices()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);

        vm.SelectedServer = "Thrane";

        Assert.Equal(["Any", "Alt"], vm.CharacterOptions.Select(c => c.Name));
        Assert.Equal(2, vm.Groups.Count);
        vm.SelectedCharacter = vm.CharacterOptions[1];
        Assert.Equal("Thrane · Alt", Assert.Single(vm.Groups).Title);
    }

    [Fact]
    public void StorageFilters_IncludingNotRecorded()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);

        vm.SelectedStorage = "Shared Bank";
        Assert.Equal("Absorption Gauntlet", Assert.Single(vm.Groups.SelectMany(g => g)).ItemName);
        vm.SelectedStorage = "Not recorded";
        Assert.Equal(3, vm.Groups.Sum(g => g.Count));
    }

    [Fact]
    public void SearchFilter_Applies()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);
        vm.Filters.SearchText = "gauntlet";
        Assert.Equal("Absorption Gauntlet", Assert.Single(vm.Groups.SelectMany(g => g)).ItemName);
    }

    [Fact]
    public void NotInCatalogOnly_ShowsRetiredItems()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);

        vm.NotInCatalogOnly = true;

        var row = Assert.Single(vm.Groups.SelectMany(g => g));
        Assert.Equal(("Retired Ring", "Not in the current catalog", false), (row.ItemName, row.Subtitle, row.IsInCatalog));
    }

    [Fact]
    public void CharacterAddedElsewhere_AppearsInCharacterChoices()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);
        w.F.Session.Apply(d => TrackerOperations.AddCharacter(d, "Moonsea", "Keeper"));
        Assert.Contains(vm.CharacterOptions, c => c.Name == "Keeper (Moonsea)");
    }

    [Fact]
    public async Task OpenCopy_OpensTheItem()
    {
        var w = Setup();
        using var _ = w.F;
        var vm = Create(w.F);
        await vm.OpenCopyCommand.ExecuteAsync(vm.Groups[0][0]);
        Assert.Equal([$"item:{Keys.Relic}"], w.F.Navigator.Calls);
    }

    [Fact]
    public void ChangingServer_ResetsCharacter_EvenWhenAPickerPushesTheItemAtTheOldIndex()
    {
        var w = Setup();
        using var _ = w.F;
        w.F.Session.Apply(d => TrackerOperations.AddCharacter(d, "Cormyr", "Aaron"));
        var vm = Create(w.F);
        // Behave like a MAUI Picker: when its list is replaced it re-selects the item at the old (clamped) index.
        var index = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MyItemsViewModel.SelectedCharacter) && vm.SelectedCharacter is { } c)
                index = vm.CharacterOptions.ToList().IndexOf(c);
            if (e.PropertyName == nameof(MyItemsViewModel.CharacterOptions))
                vm.SelectedCharacter = vm.CharacterOptions[Math.Min(index, vm.CharacterOptions.Count - 1)];
        };
        vm.SelectedCharacter = vm.CharacterOptions.Single(c => c.Name == "Grimorde (Cormyr)");

        vm.SelectedServer = "Thrane";

        Assert.Equal(MyItemsViewModel.AnyCharacter, vm.SelectedCharacter);
    }
}
