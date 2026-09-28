using DdoItemTracker.Presentation.Services;
using DdoItemTracker.Views;

namespace DdoItemTracker.Services;

public sealed class ShellNavigator : INavigator
{
    public Task OpenItemAsync(string itemKey) =>
        Shell.Current.GoToAsync(nameof(ItemDetailPage), new Dictionary<string, object> { ["key"] = itemKey });

    public Task OpenCopyEditorAsync(string itemKey, string? copyId) =>
        Shell.Current.GoToAsync(nameof(CopyEditorPage), new Dictionary<string, object> { ["key"] = itemKey, ["copyId"] = copyId ?? string.Empty });

    public Task BackAsync() => Shell.Current.GoToAsync("..");

    public Task OpenUrlAsync(string url) => Launcher.Default.OpenAsync(new Uri(url));
}
