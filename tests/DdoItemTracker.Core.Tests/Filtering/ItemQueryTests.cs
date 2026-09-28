using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Filtering;

public class ItemQueryTests
{
    private static readonly IReadOnlyList<CatalogItem> Items = Fixtures.ConvertSample().Catalog.Items;

    private static IReadOnlyList<string> Keys(ItemFilter f, TrackerData? data = null) =>
        ItemQuery.Apply(Items, f, data ?? new TrackerData()).Select(i => i.Key).ToList();

    private static TrackerData OwnChainsBeltOn(string server)
    {
        var data = new TrackerData();
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = server, Storage = StorageType.SharedBank });
        return data;
    }

    [Fact]
    public void EmptyFilter_ReturnsEverything() => Assert.Equal(Items.Count, Keys(new ItemFilter()).Count);

    [Fact]
    public void Search_IgnoresCaseAndSurroundingSpaces() =>
        Assert.Equal(["Absorption Gauntlet|18|Gloves"], Keys(new ItemFilter { Search = "  absorption " }));

    [Fact]
    public void LevelRange_IsInclusive() =>
        Assert.Equal(
            ["Adherent's Pendant|11|Necklace", "Chains|8|Belt", "Chains|8|Necklace", "Cloak of Winter's End (level 8)|8|Cloak"],
            Keys(new ItemFilter { MinLevelFrom = 8, MinLevelTo = 11 }));

    [Fact]
    public void Slot_Type_Pack_Quest_Filter()
    {
        Assert.Equal(2, Keys(new ItemFilter { Slot = "necklace" }).Count);
        Assert.Equal(["Aeon, the Blazing Reign|33|Weapon"], Keys(new ItemFilter { Type = "Long Bows" }));
        Assert.Equal(2, Keys(new ItemFilter { Pack = "Against the Slave Lords" }).Count);
        Assert.Equal(2, Keys(new ItemFilter { Quest = "Timeline Fragment Exchange" }).Count);
    }

    [Fact]
    public void InSetOnly_KeepsSetItems() =>
        Assert.Equal(["Absorption Gauntlet|18|Gloves", "Adherent's Pendant|11|Necklace"], Keys(new ItemFilter { InSetOnly = true }));

    [Fact]
    public void ArtifactOnly_KeepsArtifacts()
    {
        var items = new[] { TestCatalogs.Item("Art", artifact: true), TestCatalogs.Item("Plain") };
        Assert.Equal(["Art"], ItemQuery.Apply(items, new ItemFilter { ArtifactOnly = true }, new TrackerData()).Select(i => i.Name));
    }

    [Fact]
    public void Ownership_OwnedAndNotOwned()
    {
        var data = OwnChainsBeltOn("Cormyr");
        Assert.Equal(["Chains|8|Belt"], Keys(new ItemFilter { Ownership = OwnershipFilter.Owned }, data));
        Assert.DoesNotContain("Chains|8|Belt", Keys(new ItemFilter { Ownership = OwnershipFilter.NotOwned }, data));
        Assert.Equal(Items.Count - 1, Keys(new ItemFilter { Ownership = OwnershipFilter.NotOwned }, data).Count);
    }

    [Fact]
    public void Ownership_ScopedToServer()
    {
        var data = OwnChainsBeltOn("Cormyr");
        Assert.Empty(Keys(new ItemFilter { Ownership = OwnershipFilter.Owned, OwnershipServer = "Thrane" }, data));
        Assert.Single(Keys(new ItemFilter { Ownership = OwnershipFilter.Owned, OwnershipServer = "cormyr" }, data));
    }

    [Fact]
    public void Filters_Combine() =>
        Assert.Equal(["Chains|8|Necklace"], Keys(new ItemFilter { Search = "chains", Slot = "Necklace" }));

    [Fact]
    public void OwnedCounts_CountsCopiesPerKey()
    {
        var data = OwnChainsBeltOn("Cormyr");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = "Thrane", Storage = StorageType.SharedBank });
        Assert.Equal(2, ItemQuery.OwnedCounts(data)["Chains|8|Belt"]);
        Assert.Equal(1, ItemQuery.OwnedCounts(data, "Thrane")["Chains|8|Belt"]);
    }
}
