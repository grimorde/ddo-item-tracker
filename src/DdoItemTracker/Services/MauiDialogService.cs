using DdoItemTracker.Presentation.Services;

namespace DdoItemTracker.Services;

public sealed class MauiDialogService : IDialogService
{
    private const string Cancel = "Cancel";

    private static Page CurrentPage =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No page is showing.");

    public Task AlertAsync(string title, string message) => CurrentPage.DisplayAlertAsync(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        var choice = await CurrentPage.DisplayActionSheetAsync(title, Cancel, null, options.ToArray());
        return choice is not null && options.Contains(choice) ? choice : null;
    }

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null) =>
        CurrentPage.DisplayPromptAsync(title, message, "OK", Cancel, initialValue: initialValue ?? string.Empty);
}
