using CommunityToolkit.Mvvm.ComponentModel;
using DdoItemTracker.Core.Ownership;
using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Presentation.State;

namespace DdoItemTracker.Presentation.ViewModels;

/// <summary>
/// Base for screens that show session data. Pages call <see cref="Activate"/> when they appear and
/// <see cref="Deactivate"/> when they disappear, so a hidden page never holds a subscription.
/// </summary>
public abstract class SessionViewModel(TrackerSession session, IDialogService dialogs) : ObservableObject
{
    private bool _active;

    protected TrackerSession Session { get; } = session;
    protected IDialogService Dialogs { get; } = dialogs;

    public void Activate()
    {
        if (!_active)
        {
            Session.Changed += OnSessionChanged;
            _active = true;
        }
        Refresh();
    }

    public void Deactivate()
    {
        if (!_active) return;
        Session.Changed -= OnSessionChanged;
        _active = false;
    }

    public abstract void Refresh();

    /// <summary>Applies a change, telling the player in plain words if it breaks a rule or cannot be saved.</summary>
    protected async Task<bool> TryApplyAsync(Action<TrackerData> change)
    {
        try
        {
            Session.Apply(change);
            return true;
        }
        catch (TrackerRuleException ex)
        {
            await Dialogs.AlertAsync("Can't do that", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.AlertAsync("Couldn't save", $"Your change couldn't be saved. {ex.Message}");
        }
        return false;
    }

    /// <summary>Lets the player pick a file and reads it; a file that can't be opened gets a plain message.</summary>
    /// <returns>The file's text, or null when the player cancelled or the file couldn't be read.</returns>
    protected async Task<string?> PickTextAsync(IFileService files, string pickerTitle, string alertTitle)
    {
        try
        {
            return await files.PickJsonTextAsync(pickerTitle);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dialogs.AlertAsync(alertTitle, $"That file couldn't be opened. {ex.Message}");
            return null;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Refresh();
}
