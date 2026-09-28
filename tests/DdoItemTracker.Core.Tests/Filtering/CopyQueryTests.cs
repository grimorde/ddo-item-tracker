using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Filtering;

public class CopyQueryTests
{
    private static readonly CatalogIndex Index = new(Fixtures.ConvertSample().Catalog);

    private static (TrackerData Data, Character Grim, Character Alt) Setup()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var alt = TrackerOperations.AddCharacter(data, "Thrane", "Alt");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Absorption Gauntlet|18|Gloves", ItemName = "Absorption Gauntlet", Server = "Cormyr", Storage = StorageType.SharedBank });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Thrane", Storage = StorageType.Equipped, CharacterId = alt.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Retired Ring|5|Ring", ItemName = "Retired Ring", Server = "Thrane", Storage = StorageType.SharedBank });
        return (data, grim, alt);
    }

    [Fact]
    public void Apply_SortsByServerThenSharedBankThenHolder()
    {
        var (data, _, _) = Setup();
        var rows = CopyQuery.Apply(data, Index, new CopyFilter());
        Assert.Equal(
            ["Cormyr/Shared Bank/Absorption Gauntlet", "Cormyr/Grimorde/Chains", "Thrane/Shared Bank/Retired Ring", "Thrane/Alt/Chains"],
            rows.Select(r => $"{r.Copy.Server}/{r.HolderName}/{r.ItemName}"));
    }

    [Fact]
    public void Apply_FiltersByServerCharacterAndStorage()
    {
        var (data, grim, _) = Setup();
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Server = "Thrane" }).Count);
        Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { CharacterId = grim.Id }));
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Storage = StorageType.SharedBank }).Count);
    }

    [Fact]
    public void CopyWhoseItemLeftTheCatalog_IsShownByItsSavedName()
    {
        var (data, _, _) = Setup();
        var row = Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { NotInCatalogOnly = true }));
        Assert.False(row.IsInCatalog);
        Assert.Equal("Retired Ring", row.ItemName);
    }

    [Fact]
    public void CopyNotInCatalog_MatchesSearchButNotItemFieldFilters()
    {
        var (data, _, _) = Setup();
        Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Search = "retired" } }));
        Assert.DoesNotContain(
            CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Slot = "Ring" } }),
            r => !r.IsInCatalog);
    }

    [Fact]
    public void ItemFilters_ApplyToCatalogCopies()
    {
        var (data, _, _) = Setup();
        var rows = CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Slot = "Belt" } });
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("Chains", r.ItemName));
    }

    [Fact]
    public void Summary_CountsDistinctCatalogItemsOwned()
    {
        var (data, _, _) = Setup();
        Assert.Equal((2, Index.Catalog.Items.Count), CopyQuery.Summary(data, Index));
    }
}
