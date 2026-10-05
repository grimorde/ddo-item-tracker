namespace DdoItemTracker.Presentation.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
    /// <returns>The chosen option, or null when cancelled.</returns>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);
    /// <returns>The entered text, or null when cancelled.</returns>
    Task<string?> PromptAsync(string title, string message, string? initialValue = null);
}

public interface INavigator
{
    Task OpenItemAsync(string itemKey);
    Task OpenCopyEditorAsync(string itemKey, string? copyId);
    Task BackAsync();
    Task OpenUrlAsync(string url);
}

public interface IFileService
{
    /// <returns>The file's text, or null when the player cancels.</returns>
    Task<string?> PickJsonTextAsync(string title);
    /// <returns>Where the file was saved, or null when the player cancels.</returns>
    Task<string?> SaveTextAsync(string suggestedFileName, string content);
    Task ShareTextAsync(string fileName, string content, string title);
}

/// <summary>Small per-device preferences, such as the last location used on the copy form.</summary>
public interface ISettingsStore
{
    string? LastServer { get; set; }
    /// <summary>"shared" for the Shared Bank, a character id, or null.</summary>
    string? LastHeldIn { get; set; }
    string? LastStorage { get; set; }
    /// <summary>When the app last reached GitHub to look for a catalog update.</summary>
    DateTimeOffset? LastCatalogCheckUtc { get; set; }
}
