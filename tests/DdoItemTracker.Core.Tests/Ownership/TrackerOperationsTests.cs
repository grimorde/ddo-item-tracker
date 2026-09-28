using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Ownership;

public class TrackerOperationsTests
{
    private static OwnedCopy Copy(string key = "A|1|Ring", string? server = null, string? characterId = null, StorageType? storage = null) =>
        new() { ItemKey = key, ItemName = key.Split('|')[0], Server = server, CharacterId = characterId, Storage = storage };

    [Fact]
    public void Servers_AreTheFourLiveWorlds()
    {
        Assert.Equal(["Cormyr", "Moonsea", "Shadowdale", "Thrane"], Servers.All);
        Assert.Null(Servers.Canonical("Lamannia"));
        Assert.Equal("Thrane", Servers.Canonical("  thrane "));
    }

    [Fact]
    public void AddCharacter_TrimsNameAndCanonicalisesServer()
    {
        var data = new TrackerData();
        var c = TrackerOperations.AddCharacter(data, "cormyr", "  Grimorde ");
        Assert.Equal(("Cormyr", "Grimorde"), (c.Server, c.Name));
        Assert.Single(data.Characters);
    }

    [Fact]
    public void AddCharacter_SameNameDifferingOnlyByCaseOrSpaces_IsRejected()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var ex = Assert.Throws<TrackerRuleException>(() => TrackerOperations.AddCharacter(data, "Cormyr", "grimorde "));
        Assert.Equal("There is already a character called grimorde on Cormyr.", ex.Message);
    }

    [Fact]
    public void AddCharacter_SameNameOnAnotherServer_IsAllowed()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        TrackerOperations.AddCharacter(data, "Thrane", "Grimorde");
        Assert.Equal(2, data.Characters.Count);
    }

    [Theory]
    [InlineData("Lamannia", "Grimorde", "Choose a server.")]
    [InlineData("Cormyr", "   ", "Enter a character name.")]
    public void AddCharacter_InvalidInput_IsRejected(string server, string name, string message)
    {
        var ex = Assert.Throws<TrackerRuleException>(() => TrackerOperations.AddCharacter(new TrackerData(), server, name));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void RenameCharacter_ToAnotherCharactersName_IsRejected()
    {
        var data = new TrackerData();
        TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var alt = TrackerOperations.AddCharacter(data, "Cormyr", "Alt");
        Assert.Throws<TrackerRuleException>(() => TrackerOperations.RenameCharacter(data, alt.Id, "GRIMORDE"));
        TrackerOperations.RenameCharacter(data, alt.Id, "Alt Two");
        Assert.Equal("Alt Two", alt.Name);
    }

    [Fact]
    public void DeleteCharacter_DeleteCopies_RemovesOnlyThatCharactersCopies()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        TrackerOperations.AddCopy(data, Copy("A|1|Ring", "Cormyr", grim.Id, StorageType.Bank));
        TrackerOperations.AddCopy(data, Copy("B|1|Ring", "Cormyr", storage: StorageType.SharedBank));

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.DeleteCopies);

        Assert.Empty(data.Characters);
        Assert.Equal("B|1|Ring", Assert.Single(data.OwnedCopies).ItemKey);
    }

    [Fact]
    public void DeleteCharacter_KeepOnServer_KeepsCopiesWithServerOnly()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Thrane", "Grimorde");
        TrackerOperations.AddCopy(data, Copy("A|1|Ring", "Thrane", grim.Id, StorageType.Inventory));

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.KeepOnServer);

        var copy = Assert.Single(data.OwnedCopies);
        Assert.Equal(("Thrane", (string?)null, (StorageType?)null), (copy.Server, copy.CharacterId, copy.Storage));
        Assert.Null(OwnershipRules.ValidateCopy(data, copy));
    }

    [Fact]
    public void AddCopy_WithNoLocation_IsStored()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, Copy());
        Assert.Equal(((string?)null, (string?)null, (StorageType?)null), (copy.Server, copy.CharacterId, copy.Storage));
    }

    [Fact]
    public void AddCopy_SeveralCopiesOfOneItem_AreAllKept()
    {
        var data = new TrackerData();
        TrackerOperations.AddCopy(data, Copy());
        TrackerOperations.AddCopy(data, Copy());
        Assert.Equal(2, data.OwnedCopies.Count);
    }

    [Fact]
    public void AddCopy_NormalisesServerAndNoteAndStampsTime()
    {
        var data = new TrackerData();
        var copy = Copy(server: "cormyr");
        copy.Note = "   ";
        TrackerOperations.AddCopy(data, copy);
        Assert.Equal("Cormyr", copy.Server);
        Assert.Null(copy.Note);
        Assert.NotEqual(default, copy.AddedUtc);

        var blank = TrackerOperations.AddCopy(data, Copy(server: "  "));
        Assert.Null(blank.Server);
    }

    [Fact]
    public void AddCopy_Invalid_IsRejectedAndNotStored()
    {
        var data = new TrackerData();
        var ex = Assert.Throws<TrackerRuleException>(() => TrackerOperations.AddCopy(data, Copy(server: "Cormyr", storage: StorageType.Bank)));
        Assert.Equal("Choose a character for Inventory or Bank.", ex.Message);
        Assert.Empty(data.OwnedCopies);
    }

    [Fact]
    public void UpdateCopy_ReplacesById_AndRejectedEditLeavesOriginal()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var original = TrackerOperations.AddCopy(data, Copy());

        var bad = Copy(server: "Cormyr", storage: StorageType.Bank);
        bad.Id = original.Id;
        Assert.Throws<TrackerRuleException>(() => TrackerOperations.UpdateCopy(data, bad));
        Assert.Same(original, Assert.Single(data.OwnedCopies));

        var good = Copy(server: "Cormyr", characterId: grim.Id, storage: StorageType.Bank);
        good.Id = original.Id;
        TrackerOperations.UpdateCopy(data, good);
        var stored = Assert.Single(data.OwnedCopies);
        Assert.Equal((StorageType?)StorageType.Bank, stored.Storage);
        Assert.Equal(original.AddedUtc, stored.AddedUtc);
    }

    [Fact]
    public void RemoveCopy_RemovesById()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, Copy());
        TrackerOperations.RemoveCopy(data, copy.Id);
        Assert.Empty(data.OwnedCopies);
    }
}
