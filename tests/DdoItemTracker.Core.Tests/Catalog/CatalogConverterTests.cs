using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Tests.Support;

namespace DdoItemTracker.Core.Tests.Catalog;

public class CatalogConverterTests
{
    private static CatalogItem Get(ConversionResult r, string key) =>
        Assert.Single(r.Catalog.Items, i => i.Key == key);

    [Fact]
    public void Counts_InputInvalidAndDuplicateRecords()
    {
        var r = Fixtures.ConvertSample();

        Assert.Equal(9, r.Report.InputItemCount);
        Assert.Equal(1, r.Report.DroppedInvalid);     // Broken Record has no slot
        Assert.Equal(1, r.Report.DroppedDuplicate);   // Aeon appears twice, identical
        Assert.Empty(r.Report.DuplicateKeys);
        Assert.Equal(7, r.Catalog.Items.Count);
    }

    [Fact]
    public void SameNameOnDifferentSlots_IsKeptAsTwoItems()
    {
        var r = Fixtures.ConvertSample();
        Get(r, "Chains|8|Belt");
        Get(r, "Chains|8|Necklace");
    }

    [Fact]
    public void LevelScaledItems_GetDistinctKeys()
    {
        var r = Fixtures.ConvertSample();
        Get(r, "Cloak of Winter's End (level 4)|4|Cloak");
        Get(r, "Cloak of Winter's End (level 8)|8|Cloak");
    }

    [Fact]
    public void MapsAllItemFields()
    {
        var item = Get(Fixtures.ConvertSample(), "Absorption Gauntlet|18|Gloves");

        Assert.Equal("Absorption Gauntlet", item.Name);
        Assert.Equal(18, item.MinLevel);
        Assert.Equal("Gloves", item.Slot);
        Assert.Equal("Hand items", item.Type);
        Assert.Equal("Vecna Unleashed", item.Pack);
        Assert.Equal(["Taken in Hand"], item.Quests);
        Assert.Equal(["Yellow Augment Slot"], item.CraftingSlots);
        Assert.Equal(["Forbidden Knowledge"], item.SetNames);
        Assert.Equal("https://ddowiki.com/page/Item:Absorption_Gauntlet", item.WikiUrl);
        Assert.Equal(new Effect("Magical Sheltering", "Insight", "10", false), item.Effects[1]);
        Assert.False(item.IsRare);
        Assert.False(item.IsArtifact);
    }

    [Fact]
    public void BoolAffix_BecomesToggle()
    {
        var item = Get(Fixtures.ConvertSample(), "Cloak of Winter's End (level 4)|4|Cloak");
        Assert.Equal(new Effect("Immunity to Fear", null, null, true), item.Effects[2]);
    }

    [Fact]
    public void AffixWithNoBonusType_IsKeptWithNullBonusType()
    {
        var item = Get(Fixtures.ConvertSample(), "Adherent's Pendant|11|Necklace");
        Assert.Equal(new Effect("Required Class: Paladin (UMD", null, "0", false), item.Effects[0]);
        Assert.Equal(5, item.Effects.Count);
    }

    [Fact]
    public void NumericAffixValue_IsStoredAsText()
    {
        const string items = """[{"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":3}]}]""";
        var r = CatalogConverter.Convert(items, "{}", Fixtures.Version);
        Assert.Equal("3", r.Catalog.Items[0].Effects[0].Value);
    }

    [Fact]
    public void SetTiers_AreOrderedByPiecesRequired()
    {
        var set = Assert.Single(Fixtures.ConvertSample().Catalog.Sets);
        Assert.Equal("Forbidden Knowledge", set.Name);
        Assert.Equal([3, 4, 5], set.Tiers.Select(t => t.PiecesRequired));
        Assert.Equal(new Effect("Physical Sheltering", "Profane", "10", false), set.Tiers[0].Effects[0]);
    }

    [Fact]
    public void SetNamesMissingFromSetsFile_AreReported()
    {
        Assert.Equal(["Oasis of Morality"], Fixtures.ConvertSample().Report.UnresolvedSetNames);
    }

    [Fact]
    public void ConflictingRecordsWithSameKey_AreReportedAndFirstIsKept()
    {
        const string items = """
            [{"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":"3"}]},
             {"name":"X","ml":1,"slot":"Ring","affixes":[{"name":"Strength","type":"Enhancement","value":"4"}]}]
            """;
        var r = CatalogConverter.Convert(items, "{}", Fixtures.Version);

        Assert.Equal(["X|1|Ring"], r.Report.DuplicateKeys);
        Assert.Equal(0, r.Report.DroppedDuplicate);
        Assert.Equal("3", Assert.Single(r.Catalog.Items).Effects[0].Value);
    }

    [Fact]
    public void Items_AreSortedByNameThenLevelThenSlot()
    {
        var names = Fixtures.ConvertSample().Catalog.Items.Select(i => i.Key).ToList();
        Assert.Equal(
            [
                "Absorption Gauntlet|18|Gloves",
                "Adherent's Pendant|11|Necklace",
                "Aeon, the Blazing Reign|33|Weapon",
                "Chains|8|Belt",
                "Chains|8|Necklace",
                "Cloak of Winter's End (level 4)|4|Cloak",
                "Cloak of Winter's End (level 8)|8|Cloak",
            ],
            names);
    }

    [Theory]
    [InlineData("not json", "{}")]
    [InlineData("{}", "{}")]      // items must be an array
    [InlineData("[]", "[]")]      // sets must be an object
    public void MalformedInput_ThrowsInvalidDataException(string items, string sets)
    {
        Assert.Throws<InvalidDataException>(() => CatalogConverter.Convert(items, sets, Fixtures.Version));
    }
}
