using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Presentation.Services;

namespace DdoItemTracker.Presentation.Tests.Support;

/// <summary>Scripted dialogs: queue the answers a test expects to be asked for, in order.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public List<(string Title, string Message)> Alerts { get; } = [];
    public List<string> Asked { get; } = [];
    public Queue<bool> ConfirmAnswers { get; } = new();
    public Queue<string?> ChooseAnswers { get; } = new();
    public Queue<string?> PromptAnswers { get; } = new();
    public List<IReadOnlyList<string>> ChooseOptions { get; } = [];

    public Task AlertAsync(string title, string message)
    {
        Alerts.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Asked.Add($"confirm:{title}:{message}");
        return Task.FromResult(ConfirmAnswers.Dequeue());
    }

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        Asked.Add($"choose:{title}");
        ChooseOptions.Add(options);
        return Task.FromResult(ChooseAnswers.Dequeue());
    }

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null)
    {
        Asked.Add($"prompt:{title}");
        return Task.FromResult(PromptAnswers.Dequeue());
    }
}

internal sealed class FakeNavigator : INavigator
{
    public List<string> Calls { get; } = [];
    public Task OpenItemAsync(string itemKey) { Calls.Add($"item:{itemKey}"); return Task.CompletedTask; }
    public Task OpenCopyEditorAsync(string itemKey, string? copyId) { Calls.Add($"copy:{itemKey}:{copyId}"); return Task.CompletedTask; }
    public Task BackAsync() { Calls.Add("back"); return Task.CompletedTask; }
    public Task OpenUrlAsync(string url) { Calls.Add($"url:{url}"); return Task.CompletedTask; }
}

internal sealed class FakeFiles : IFileService
{
    public string? TextToPick { get; set; }
    public string? SavePathToReturn { get; set; } = "C:/backups/file.json";
    public Exception? SaveFailure { get; set; }
    public List<(string Name, string Content)> Saved { get; } = [];
    public List<(string Name, string Content)> Shared { get; } = [];

    public Exception? PickFailure { get; set; }

    public Task<string?> PickJsonTextAsync(string title) =>
        PickFailure is not null ? Task.FromException<string?>(PickFailure) : Task.FromResult(TextToPick);

    public Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        if (SaveFailure is not null) throw SaveFailure;
        Saved.Add((suggestedFileName, content));
        return Task.FromResult(SavePathToReturn);
    }

    public Task ShareTextAsync(string fileName, string content, string title)
    {
        Shared.Add((fileName, content));
        return Task.CompletedTask;
    }
}

internal sealed class FakeSettings : ISettingsStore
{
    public string? LastServer { get; set; }
    public string? LastHeldIn { get; set; }
    public string? LastStorage { get; set; }
    public DateTimeOffset? LastCatalogCheckUtc { get; set; }
}

/// <summary>Returns <see cref="Result"/> for every check and counts the calls.</summary>
internal sealed class FakeCatalogChecker : ICatalogUpdateChecker
{
    public CatalogCheckResult Result { get; set; } = new CatalogCheckResult.UpToDate();
    public int Calls { get; private set; }
    public List<string> LastOwnedKeys { get; private set; } = [];

    public Task<CatalogCheckResult> CheckAsync(ItemCatalog current, IEnumerable<string> ownedItemKeys, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastOwnedKeys = ownedItemKeys.ToList();
        return Task.FromResult(Result);
    }
}
