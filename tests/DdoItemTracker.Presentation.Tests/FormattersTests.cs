using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Filtering;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Formatting;

namespace DdoItemTracker.Presentation.Tests;

public class FormattersTests
{
    [Fact]
    public void StorageNames_ListsAndParses()
    {
        Assert.Equal(["Shared Bank", "Inventory", "Bank"], StorageNames.All);
        Assert.Equal(["Not recorded", "Inventory", "Bank"], StorageNames.CharacterStorage);
        Assert.Equal(StorageType.SharedBank, StorageNames.Parse("Shared Bank"));
        Assert.Equal("Not recorded", StorageNames.Display((StorageType?)null));
        Assert.Null(StorageNames.Parse("Not recorded"));
        Assert.Null(StorageNames.Parse("Attic"));
        Assert.Null(StorageNames.Parse(null));
    }

    [Theory]
    [InlineData("Magical Sheltering", "Insight", "10", false, "Magical Sheltering +10 (Insight)")]
    [InlineData("Enhancement Bonus (Weapon)", "Enhancement", "+15", false, "Enhancement Bonus (Weapon) +15 (Enhancement)")]
    [InlineData("Diversion", "Enhancement", "-10", false, "Diversion -10 (Enhancement)")]
    [InlineData("Required Class: Paladin (UMD", null, "0", false, "Required Class: Paladin (UMD")]
    [InlineData("Healing Lore", null, "12", false, "Healing Lore +12")]
    [InlineData("Bashing", "Enhancement", "6d6", false, "Bashing 6d6 (Enhancement)")]
    [InlineData("Immunity to Fear", null, null, true, "Immunity to Fear")]
    [InlineData("Seeker", "Insight", null, false, "Seeker (Insight)")]
    public void EffectFormatter_Formats(string name, string? type, string? value, bool toggle, string expected)
    {
        Assert.Equal(expected, EffectFormatter.Format(new Effect(name, type, value, toggle)));
    }

    [Fact]
    public void LocationFormatter_EveryLevel()
    {
        var grim = new Character { Server = "Cormyr", Name = "Grimorde" };
        Character[] characters = [grim];
        string F(string? server, string? characterId, StorageType? storage) =>
            LocationFormatter.Format(new OwnedCopy { Server = server, CharacterId = characterId, Storage = storage }, characters);

        Assert.Equal("Location not recorded", F(null, null, null));
        Assert.Equal("Cormyr", F("Cormyr", null, null));
        Assert.Equal("Cormyr · Shared Bank", F("Cormyr", null, StorageType.SharedBank));
        Assert.Equal("Cormyr · Grimorde", F("Cormyr", grim.Id, null));
        Assert.Equal("Cormyr · Grimorde · Bank", F("Cormyr", grim.Id, StorageType.Bank));
        Assert.Equal("Cormyr · (unknown character) · Inventory", F("Cormyr", "gone", StorageType.Inventory));
    }

    [Fact]
    public void LocationFormatter_GroupTitle_UsesTheRowsHolder()
    {
        OwnedCopyRow Row(string? server, string? characterId, StorageType? storage, string? holder) =>
            new(new OwnedCopy { Server = server, CharacterId = characterId, Storage = storage }, null, "X", holder);

        Assert.Equal("Location not recorded", LocationFormatter.GroupTitle(Row(null, null, null, null)));
        Assert.Equal("Thrane", LocationFormatter.GroupTitle(Row("Thrane", null, null, null)));
        Assert.Equal("Thrane · Shared Bank", LocationFormatter.GroupTitle(Row("Thrane", null, StorageType.SharedBank, "Shared Bank")));
        Assert.Equal("Thrane · Alt", LocationFormatter.GroupTitle(Row("Thrane", "c1", null, "Alt")));
        Assert.Equal("Thrane · Alt · Inventory", LocationFormatter.GroupTitle(Row("Thrane", "c1", StorageType.Inventory, "Alt")));
    }
}
