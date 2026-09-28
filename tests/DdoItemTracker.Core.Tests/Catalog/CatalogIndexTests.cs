using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogIndexTests
{
    private static CatalogIndex Sample() => new(Fixtures.ConvertSample().Catalog);

    [Fact]
    public void Find_ReturnsItemByKey_OrNull()
    {
        var index = Sample();
        Assert.Equal("Absorption Gauntlet", index.Find("Absorption Gauntlet|18|Gloves")?.Name);
        Assert.Null(index.Find("Nothing|1|Ring"));
    }

    [Fact]
    public void FindSet_ReturnsSet_OrNullForUnresolved()
    {
        var index = Sample();
        Assert.NotNull(index.FindSet("Forbidden Knowledge"));
        Assert.Null(index.FindSet("Oasis of Morality"));
    }

    [Fact]
    public void SetMembers_ListsItemsInTheSet()
    {
        var catalog = TestCatalogs.Catalog([
            TestCatalogs.Item("Zeta Ring", sets: ["S"]),
            TestCatalogs.Item("Alpha Ring", sets: ["S"]),
            TestCatalogs.Item("Other"),
        ]);
        var members = new CatalogIndex(catalog).SetMembers("S");
        Assert.Equal(["Alpha Ring", "Zeta Ring"], members.Select(m => m.Name));
        Assert.Empty(new CatalogIndex(catalog).SetMembers("Missing"));
    }

    [Fact]
    public void OptionLists_AreDistinctAndSorted()
    {
        var index = Sample();
        Assert.Equal(["Belt", "Cloak", "Gloves", "Necklace", "Weapon"], index.Slots);
        Assert.Contains("Vecna Unleashed", index.Packs);
        Assert.Contains("Timeline Fragment Exchange", index.Quests);
        Assert.Equal(index.Types.Distinct().Count(), index.Types.Count);
    }
}
