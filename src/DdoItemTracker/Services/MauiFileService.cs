using System.Text;
using CommunityToolkit.Maui.Storage;
using DdoItemTracker.Presentation.Services;

namespace DdoItemTracker.Services;

public sealed class MauiFileService : IFileService
{
    private static readonly FilePickerFileType JsonFiles = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, [".json"] },
        { DevicePlatform.Android, ["application/json", "text/json", "application/octet-stream"] },
        { DevicePlatform.iOS, ["public.json"] },
    });

    public async Task<string?> PickJsonTextAsync(string title)
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = JsonFiles });
        if (picked is null) return null;
        await using var stream = await picked.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task<string?> SaveTextAsync(string suggestedFileName, string content)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, CancellationToken.None);
        if (result.IsSuccessful) return result.FilePath;
        if (result.Exception is null or OperationCanceledException) return null;
        throw result.Exception;
    }

    public async Task ShareTextAsync(string fileName, string content, string title)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, content);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });
    }
}
