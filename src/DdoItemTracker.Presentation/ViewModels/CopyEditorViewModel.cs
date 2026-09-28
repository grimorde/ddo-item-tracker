using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Formatting;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

/// <summary>One choice in the "Held in" picker: not recorded, the Shared Bank, or a character.</summary>
public sealed record HeldInOption(string Label, string? CharacterId, bool IsSharedBank)
{
    public const string SharedBankKey = "shared";

    public string? Key => CharacterId ?? (IsSharedBank ? SharedBankKey : null);

    public override string ToString() => Label;
}

public partial class CopyEditorViewModel(TrackerSession session, INavigator navigator, IDialogService dialogs, ISettingsStore settings)
    : SessionViewModel(session, dialogs)
{
    public const string NotRecorded = StorageNames.NotRecorded;
    public static readonly HeldInOption HeldNotRecorded = new(NotRecorded, null, false);
    public static readonly HeldInOption HeldSharedBank = new(StorageNames.Display(StorageType.SharedBank), null, true);

    private OwnedCopy? _existing;
    private bool _loading;

    [ObservableProperty] private string _title = "Add copy";
    [ObservableProperty] private string _itemName = string.Empty;
    [ObservableProperty] private string? _selectedServer = NotRecorded;
    [ObservableProperty] private IReadOnlyList<HeldInOption> _heldInOptions = [HeldNotRecorded];
    [ObservableProperty] private HeldInOption? _selectedHeldIn = HeldNotRecorded;
    [ObservableProperty] private string? _selectedStorage = NotRecorded;
    [ObservableProperty] private string? _note;
    [ObservableProperty] private bool _showHeldIn;
    [ObservableProperty] private bool _showStorage;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private bool _canSave;

    public string ItemKey { get; private set; } = string.Empty;
    public IReadOnlyList<string> ServerOptions { get; } = [NotRecorded, .. Servers.All];
    public IReadOnlyList<string> StorageOptions => StorageNames.CharacterStorage;

    public void Load(string itemKey, string? copyId)
    {
        _loading = true;
        ItemKey = itemKey;
        _existing = copyId is null ? null : Session.Data.OwnedCopies.FirstOrDefault(c => c.Id == copyId);
        ItemName = Session.Catalog.Find(itemKey)?.Name ?? _existing?.ItemName ?? itemKey.Split('|')[0];
        Title = _existing is null ? "Add copy" : "Edit copy";

        if (_existing is not null)
        {
            SelectedServer = _existing.Server ?? NotRecorded;
            RebuildHeldIn(HeldKey(_existing));
            SelectedStorage = _existing.CharacterId is null ? NotRecorded : StorageNames.Display(_existing.Storage);
            Note = _existing.Note;
        }
        else
        {
            SelectedServer = Servers.Canonical(settings.LastServer) ?? NotRecorded;
            RebuildHeldIn(settings.LastHeldIn);
            SelectedStorage = StorageNames.CharacterStorage.Contains(settings.LastStorage) ? settings.LastStorage : NotRecorded;
            Note = null;
        }
        _loading = false;
        Validate();
    }

    public override void Refresh() => Validate();

    public OwnedCopy BuildCopy()
    {
        var server = Servers.Canonical(SelectedServer);
        var held = server is null ? HeldNotRecorded : SelectedHeldIn ?? HeldNotRecorded;
        return new OwnedCopy
        {
            Id = _existing?.Id ?? Guid.NewGuid().ToString(),
            ItemKey = ItemKey,
            ItemName = ItemName,
            Server = server,
            CharacterId = held.CharacterId,
            Storage = held.IsSharedBank ? StorageType.SharedBank
                : held.CharacterId is not null ? StorageNames.Parse(SelectedStorage)
                : null,
            Note = Note,
            AddedUtc = _existing?.AddedUtc ?? default,
        };
    }

    partial void OnSelectedServerChanged(string? value)
    {
        if (_loading) return;
        RebuildHeldIn(SelectedHeldIn?.Key);
        Validate();
    }

    partial void OnSelectedHeldInChanged(HeldInOption? value)
    {
        if (!_loading) Validate();
    }

    partial void OnSelectedStorageChanged(string? value)
    {
        if (!_loading) Validate();
    }

    private static string? HeldKey(OwnedCopy copy) =>
        copy.CharacterId ?? (copy.Storage == StorageType.SharedBank ? HeldInOption.SharedBankKey : null);

    /// <summary>Rebuilds the "Held in" choices for the selected server, keeping the preferred choice when it still exists.</summary>
    private void RebuildHeldIn(string? preferredKey)
    {
        var server = Servers.Canonical(SelectedServer);
        if (server is null)
        {
            HeldInOptions = [HeldNotRecorded];
            SelectedHeldIn = HeldNotRecorded;
            return;
        }
        var characters = Session.Data.Characters
            .Where(c => c.Server == server)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new HeldInOption(c.Name, c.Id, false));
        HeldInOptions = [HeldNotRecorded, HeldSharedBank, .. characters];
        SelectedHeldIn = HeldInOptions.FirstOrDefault(o => o.Key == preferredKey) ?? HeldNotRecorded;
    }

    private void Validate()
    {
        ShowHeldIn = Servers.Canonical(SelectedServer) is not null;
        ShowStorage = ShowHeldIn && SelectedHeldIn?.CharacterId is not null;
        ValidationMessage = OwnershipRules.ValidateCopy(Session.Data, BuildCopy());
        CanSave = ValidationMessage is null;
    }

    [RelayCommand]
    private async Task Save()
    {
        Validate();
        if (!CanSave) return;
        var copy = BuildCopy();
        var isNew = _existing is null;
        var saved = await TryApplyAsync(d =>
        {
            if (isNew) TrackerOperations.AddCopy(d, copy);
            else TrackerOperations.UpdateCopy(d, copy);
        });
        if (!saved) return;

        settings.LastServer = copy.Server;
        settings.LastHeldIn = HeldKey(copy);
        settings.LastStorage = copy.CharacterId is null ? null : StorageNames.Display(copy.Storage);
        await navigator.BackAsync();
    }

    [RelayCommand]
    private Task Cancel() => navigator.BackAsync();
}
