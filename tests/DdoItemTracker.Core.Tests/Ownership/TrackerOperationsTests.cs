using DdoItemTracker.Core.Ownership;

namespace DdoItemTracker.Core.Tests.Ownership;

public class TrackerOperationsTests
{
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
        Assert.Equal("Cormyr", c.Server);
        Assert.Equal("Grimorde", c.Name);
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
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id });
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "B|1|Ring", ItemName = "B", Server = "Cormyr", Storage = StorageType.SharedBank });

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.DeleteCopies);

        Assert.Empty(data.Characters);
        Assert.Equal("B|1|Ring", Assert.Single(data.OwnedCopies).ItemKey);
    }

    [Fact]
    public void DeleteCharacter_MoveToSharedBank_KeepsCopiesOnSameServer()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Thrane", "Grimorde");
        TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Thrane", Storage = StorageType.Equipped, CharacterId = grim.Id });

        TrackerOperations.DeleteCharacter(data, grim.Id, CharacterCopyDisposal.MoveToSharedBank);

        var copy = Assert.Single(data.OwnedCopies);
        Assert.Null(copy.CharacterId);
        Assert.Equal(StorageType.SharedBank, copy.Storage);
        Assert.Equal("Thrane", copy.Server);
        Assert.Null(OwnershipRules.ValidateCopy(data, copy));
    }

    [Fact]
    public void DeleteFolder_UnfilesItsCharacters()
    {
        var data = new TrackerData();
        var folder = TrackerOperations.AddFolder(data, "Mains");
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde", folder.Id);

        TrackerOperations.DeleteFolder(data, folder.Id);

        Assert.Empty(data.Folders);
        Assert.Null(grim.FolderId);
    }

    [Fact]
    public void MoveCharacterToFolder_UnknownFolder_IsRejected()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        Assert.Throws<TrackerRuleException>(() => TrackerOperations.MoveCharacterToFolder(data, grim.Id, "nope"));
        var folder = TrackerOperations.AddFolder(data, "Mains");
        TrackerOperations.MoveCharacterToFolder(data, grim.Id, folder.Id);
        Assert.Equal(folder.Id, grim.FolderId);
    }

    [Fact]
    public void AddCopy_SeveralCopiesOfOneItem_AreAllKept()
    {
        var data = new TrackerData();
        for (var i = 0; i < 2; i++)
            TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });
        Assert.Equal(2, data.OwnedCopies.Count);
    }

    [Fact]
    public void AddCopy_NormalisesServerAndNoteAndStampsTime()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "cormyr", Storage = StorageType.SharedBank, Note = "   " });
        Assert.Equal("Cormyr", copy.Server);
        Assert.Null(copy.Note);
        Assert.NotEqual(default, copy.AddedUtc);
    }

    [Fact]
    public void AddCopy_Invalid_IsRejectedAndNotStored()
    {
        var data = new TrackerData();
        var ex = Assert.Throws<TrackerRuleException>(() =>
            TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank }));
        Assert.Equal("Choose a character.", ex.Message);
        Assert.Empty(data.OwnedCopies);
    }

    [Fact]
    public void UpdateCopy_ReplacesById_AndRejectedEditLeavesOriginal()
    {
        var data = new TrackerData();
        var grim = TrackerOperations.AddCharacter(data, "Cormyr", "Grimorde");
        var original = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });

        Assert.Throws<TrackerRuleException>(() => TrackerOperations.UpdateCopy(data, new OwnedCopy { Id = original.Id, ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank }));
        Assert.Same(original, Assert.Single(data.OwnedCopies));

        TrackerOperations.UpdateCopy(data, new OwnedCopy { Id = original.Id, ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.Bank, CharacterId = grim.Id, AddedUtc = original.AddedUtc });
        Assert.Equal(StorageType.Bank, Assert.Single(data.OwnedCopies).Storage);
    }

    [Fact]
    public void RemoveCopy_RemovesById()
    {
        var data = new TrackerData();
        var copy = TrackerOperations.AddCopy(data, new OwnedCopy { ItemKey = "A|1|Ring", ItemName = "A", Server = "Cormyr", Storage = StorageType.SharedBank });
        TrackerOperations.RemoveCopy(data, copy.Id);
        Assert.Empty(data.OwnedCopies);
    }
}
