using DdoItemTracker.Core.Catalog;

namespace DdoItemTracker.Core.Tests.Catalog;

public class ItemKeyTests
{
    [Fact]
    public void For_JoinsNameLevelAndSlot()
    {
        Assert.Equal(
            "Cloak of Winter's End (level 8)|8|Cloak",
            ItemKey.For("Cloak of Winter's End (level 8)", 8, "Cloak"));
    }

    [Fact]
    public void For_SameNameDifferentSlot_GivesDifferentKeys()
    {
        Assert.NotEqual(ItemKey.For("Chains", 8, "Belt"), ItemKey.For("Chains", 8, "Necklace"));
    }
}
