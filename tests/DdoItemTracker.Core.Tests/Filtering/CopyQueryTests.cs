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
        void Add(string key, string name, string? server = null, string? characterId = null, StorageType? storage = null) =>
            TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = key, ItemName = name, Server = server, CharacterId = characterId, Storage = storage });
        Add("Chains|8|Belt", "Chains", "Cormyr", grim.Id, StorageType.Bank);
        Add("Absorption Gauntlet|18|Gloves", "Absorption Gauntlet", "Cormyr", storage: StorageType.SharedBank);
        Add("Chains|8|Belt", "Chains", "Thrane", alt.Id, StorageType.Inventory);
        Add("Retired Ring|5|Ring", "Retired Ring", "Thrane", storage: StorageType.SharedBank);
        Add("Adherent's Pendant|11|Necklace", "Adherent's Pendant");
        Add("Chains|8|Necklace", "Chains", "Cormyr");
        Add("Cloak of Winter's End (level 4)|4|Cloak", "Cloak of Winter's End (level 4)", "Cormyr", grim.Id);
        return (data, grim, alt);
    }

    private static string Describe(OwnedCopyRow r) =>
        $"{r.Copy.Server ?? "-"}/{r.HolderName ?? "-"}/{r.Copy.Storage?.ToString() ?? "-"}/{r.ItemName}";

    [Fact]
    public void Apply_OrdersByLocationLevel()
    {
        var (data, _, _) = Setup();
        Assert.Equal(
            [
                "-/-/-/Adherent's Pendant",
                "Cormyr/-/-/Chains",
                "Cormyr/Shared Bank/SharedBank/Absorption Gauntlet",
                "Cormyr/Grimorde/-/Cloak of Winter's End (level 4)",
                "Cormyr/Grimorde/Bank/Chains",
                "Thrane/Shared Bank/SharedBank/Retired Ring",
                "Thrane/Alt/Inventory/Chains",
            ],
            CopyQuery.Apply(data, Index, new CopyFilter()).Select(Describe));
    }

    [Fact]
    public void Apply_FiltersByServerCharacterAndStorage()
    {
        var (data, grim, _) = Setup();
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Server = "Thrane" }).Count);
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { CharacterId = grim.Id }).Count);
        Assert.Equal(2, CopyQuery.Apply(data, Index, new CopyFilter { Storage = StorageType.SharedBank }).Count);
    }

    [Fact]
    public void NoServer_KeepsOnlyCopiesWithoutAServer()
    {
        var (data, _, _) = Setup();
        Assert.Equal("Adherent's Pendant", Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { NoServer = true })).ItemName);
    }

    [Fact]
    public void NoStorage_KeepsOnlyCopiesWithoutAStorage()
    {
        var (data, _, _) = Setup();
        Assert.Equal(3, CopyQuery.Apply(data, Index, new CopyFilter { NoStorage = true }).Count);
        Assert.Single(CopyQuery.Apply(data, Index, new CopyFilter { NoStorage = true, Server = "Cormyr", CharacterId = null, Item = new ItemFilter { Search = "cloak" } }));
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
        Assert.DoesNotContain(CopyQuery.Apply(data, Index, new CopyFilter { Item = new ItemFilter { Slot = "Ring" } }), r => !r.IsInCatalog);
    }

    [Fact]
    public void Summary_CountsDistinctCatalogItemsOwned()
    {
        var (data, _, _) = Setup();
        Assert.Equal((5, Index.Catalog.Items.Count), CopyQuery.Summary(data, Index));
    }
}
