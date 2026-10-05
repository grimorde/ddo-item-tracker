using System.Globalization;
using System.Text;
using DdoItemTracker.Core.Catalog;
using DdoItemTracker.Core.Persistence;
using DdoItemTracker.Presentation.Services;

namespace DdoItemTracker.Presentation.State;

/// <summary>
/// Checks for, applies and resets catalog updates. Used quietly at startup (at most once a day) and on request from Settings.
/// A catalog change never touches the player's data.
/// </summary>
/// <param name="loadBuiltIn">Reads the catalog shipped with the app; only called on reset.</param>
/// <param name="storedWasUnreadable">True when a downloaded catalog was found at startup but couldn't be read.</param>
public sealed class CatalogUpdateCoordinator(
    TrackerSession session,
    CatalogStore store,
    ICatalogUpdateChecker checker,
    Func<ItemCatalog> loadBuiltIn,
    IDialogService dialogs,
    ISettingsStore settings,
    TimeProvider clock,
    bool storedWasUnreadable = false)
{
    public static readonly TimeSpan StartupCheckInterval = TimeSpan.FromHours(24);

    public const string Title = "Catalog update";
    public const string ApplyButton = "Update";
    public const string LaterButton = "Not now";

    private bool _startupDone;
    private bool _busy;

    /// <summary>Runs once per app session: reports an unreadable downloaded catalog, then checks if the last check was over a day ago.</summary>
    public async Task CheckOnStartupAsync()
    {
        if (_startupDone) return;
        _startupDone = true;

        if (storedWasUnreadable)
            await dialogs.AlertAsync("Item catalog",
                "The downloaded item catalog couldn't be read, so the catalog that came with the app is being used. Your items are not affected.");

        if (settings.LastCatalogCheckUtc is { } last && clock.GetUtcNow() - last < StartupCheckInterval) return;
        await CheckAsync(userInitiated: false);
    }

    /// <param name="userInitiated">False keeps quiet unless there is an update to offer.</param>
    public async Task CheckAsync(bool userInitiated)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var current = session.Catalog.Catalog;
            var result = await checker.CheckAsync(current, session.Data.OwnedCopies.Select(c => c.ItemKey).ToList());
            if (result is not CatalogCheckResult.Failed) settings.LastCatalogCheckUtc = clock.GetUtcNow();

            switch (result)
            {
                case CatalogCheckResult.Available available:
                    await OfferAsync(current, available);
                    break;
                case CatalogCheckResult.UpToDate when userInitiated:
                    await dialogs.AlertAsync(Title, "The catalog is up to date.");
                    break;
                case CatalogCheckResult.Unsupported when userInitiated:
                    await dialogs.AlertAsync(Title, "A newer catalog is available, but it needs a newer version of the app. Update the app to get it.");
                    break;
                case CatalogCheckResult.Refused refused when userInitiated:
                    await dialogs.AlertAsync(Title, "A newer catalog was found but doesn't look right, so it wasn't used. "
                        + string.Join(" ", refused.Errors));
                    break;
                case CatalogCheckResult.Failed failed when userInitiated:
                    await dialogs.AlertAsync(Title, failed.Message);
                    break;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Asks, then deletes the downloaded catalog and goes back to the one that came with the app.</summary>
    public async Task ResetToBuiltInAsync()
    {
        if (!session.CatalogIsDownloaded)
        {
            await dialogs.AlertAsync("Item catalog", "You're already using the catalog that came with the app.");
            return;
        }
        if (!await dialogs.ConfirmAsync("Reset catalog?",
                "Go back to the catalog that came with the app? Your items are not affected. You can check for an update again at any time.",
                "Reset", "Cancel"))
            return;

        try
        {
            store.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await dialogs.AlertAsync("Item catalog", $"The downloaded catalog couldn't be removed. {ex.Message}");
            return;
        }
        var builtIn = await Task.Run(() => new CatalogIndex(loadBuiltIn()));
        session.ReplaceCatalog(builtIn, isDownloaded: false);
    }

    public static string Summary(ItemCatalog current, CatalogCheckResult.Available available)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"A newer item catalog is available: {Date(current)} → {Date(available.Catalog)}.");
        text.Append("\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{available.Diff.AddedKeys.Count:N0} {Items(available.Diff.AddedKeys.Count)} added, {available.Diff.RemovedKeys.Count:N0} removed.");
        if (available.Diff.OrphanedCopyCount > 0)
            text.Append(CultureInfo.InvariantCulture,
                $"\n\n{available.Diff.OrphanedCopyCount:N0} of your copies {(available.Diff.OrphanedCopyCount == 1 ? "is" : "are")} not in the new catalog. They are kept and shown as not in the current catalog.");
        foreach (var warning in available.Warnings) text.Append("\n\n").Append(warning);
        return text.ToString();
    }

    private async Task OfferAsync(ItemCatalog current, CatalogCheckResult.Available available)
    {
        if (!await dialogs.ConfirmAsync(Title, Summary(current, available), ApplyButton, LaterButton)) return;

        try
        {
            store.Save(available.Catalog);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await dialogs.AlertAsync(Title, $"The new catalog couldn't be saved, so nothing has changed. {ex.Message}");
            return;
        }
        var index = await Task.Run(() => new CatalogIndex(available.Catalog));
        session.ReplaceCatalog(index, isDownloaded: true);
    }

    private static string Date(ItemCatalog catalog) =>
        catalog.Version.UpstreamCommitDateUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Items(int count) => count == 1 ? "item" : "items";
}
