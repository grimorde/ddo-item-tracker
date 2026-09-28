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

    private static OwnedCopy Copy(StorageType storage, string server = "Cormyr", string? characterId = null) =>
        new() { ItemKey = "Chains|8|Belt", ItemName = "Chains", Server = server, Storage = storage, CharacterId = characterId };

    [Fact]
    public void SharedBankWithoutCharacter_IsValid()
    {
        var (data, _) = Setup();
        Assert.Null(OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank)));
    }

    [Fact]
    public void SharedBankWithCharacter_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Shared Bank items don't belong to a character.", OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank, characterId: grim.Id)));
    }

    [Theory]
    [InlineData(StorageType.Equipped)]
    [InlineData(StorageType.Inventory)]
    [InlineData(StorageType.Bank)]
    public void CharacterStorageWithoutCharacter_IsRejected(StorageType storage)
    {
        var (data, _) = Setup();
        Assert.Equal("Choose a character.", OwnershipRules.ValidateCopy(data, Copy(storage)));
    }

    [Fact]
    public void CharacterOnAnotherServer_IsRejected()
    {
        var (data, grim) = Setup();
        Assert.Equal("Grimorde is on Cormyr, not Thrane.", OwnershipRules.ValidateCopy(data, Copy(StorageType.Bank, "Thrane", grim.Id)));
    }

    [Fact]
    public void UnknownCharacter_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("That character no longer exists.", OwnershipRules.ValidateCopy(data, Copy(StorageType.Bank, characterId: "missing")));
    }

    [Fact]
    public void UnknownServer_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("Choose a server.", OwnershipRules.ValidateCopy(data, Copy(StorageType.SharedBank, "Lamannia")));
    }

    [Fact]
    public void MissingItem_IsRejected()
    {
        var (data, _) = Setup();
        var copy = Copy(StorageType.SharedBank);
        copy.ItemKey = " ";
        Assert.Equal("Choose an item.", OwnershipRules.ValidateCopy(data, copy));
    }

    [Fact]
    public void UndefinedStorageValue_IsRejected()
    {
        var (data, _) = Setup();
        Assert.Equal("Choose where it's stored.", OwnershipRules.ValidateCopy(data, Copy((StorageType)99)));
    }
}
