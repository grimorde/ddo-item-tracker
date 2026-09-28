using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Ownership;

public class OwnershipRulesTests
{
    private static (TrackerData Data, Character Grim) Setup()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        return (data, grim);
    }

    private static OwnedCopy Copy(string? server = null, string? characterId = null, StorageType? storage = null) =>
        new() { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = server, CharacterId = characterId, Storage = storage };

    [Fact]
    public void NoLocationAtAll_IsValid() => Assert.Null(OwnershipRules.ValidateCopy(Setup().Data, Copy()));

    [Fact]
    public void ServerOnly_IsValid() => Assert.Null(OwnershipRules.ValidateCopy(Setup().Data, Copy("Thrane")));

    [Fact]
    public void SharedBank_IsValid() => Assert.Null(OwnershipRules.ValidateCopy(Setup().Data, Copy("Cormyr", storage: StorageType.SharedBank)));

    [Theory]
    [InlineData(null)]
    [InlineData(StorageType.Inventory)]
    [InlineData(StorageType.Bank)]
    public void Character_WithOrWithoutStorage_IsValid(StorageType? storage)
    {
        var (data, grim) = Setup();
        Assert.Null(OwnershipRules.ValidateCopy(data, Copy("Cormyr", grim.Id, storage)));
    }

    [Fact]
    public void StorageOrCharacterWithoutServer_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Choose a server first.", OwnershipRules.ValidateCopy(data, Copy(storage: StorageType.SharedBank)));
        Assert.Equal("Choose a server first.", OwnershipRules.ValidateCopy(data, Copy(characterId: grim.Id)));
    }

    [Fact]
    public void SharedBankWithCharacter_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Shared Bank items don't belong to a character.", OwnershipRules.ValidateCopy(data, Copy("Cormyr", grim.Id, StorageType.SharedBank)));
    }

    [Theory]
    [InlineData(StorageType.Inventory)]
    [InlineData(StorageType.Bank)]
    public void InventoryOrBankWithoutCharacter_IsRejected(StorageType storage)
    {
        Assert.Equal("Choose a character for Inventory or Bank.", OwnershipRules.ValidateCopy(Setup().Data, Copy("Cormyr", storage: storage)));
    }

    [Fact]
    public void CharacterOnAnotherServer_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Grimorde is on Cormyr, not Thrane.", OwnershipRules.ValidateCopy(data, Copy("Thrane", grim.Id)));
    }

    [Fact]
    public void UnknownCharacter_IsRejected() =>
        Assert.Equal("That character no longer exists.", OwnershipRules.ValidateCopy(Setup().Data, Copy("Cormyr", "missing")));

    [Fact]
    public void UnknownServer_IsRejected() =>
        Assert.Equal("Choose a server.", OwnershipRules.ValidateCopy(Setup().Data, Copy("Lamannia")));

    [Fact]
    public void MissingItem_IsRejected()
    {
        var copy = Copy();
        copy.ItemKey = " ";
        Assert.Equal("Choose an item.", OwnershipRules.ValidateCopy(Setup().Data, copy));
    }

    [Fact]
    public void UndefinedStorageValue_IsRejected() =>
        Assert.Equal("Choose where it's stored.", OwnershipRules.ValidateCopy(Setup().Data, Copy("Cormyr", storage: (StorageType)99)));

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData(" thrane ", "Thrane")]
    [InlineData("Lamannia", "Lamannia")]
    public void NormaliseServer(string? input, string? expected) => Assert.Equal(expected, OwnershipRules.NormaliseServer(input));
}
